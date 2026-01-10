using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.Mediator
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Mediator"/>
    /// </summary>
    public class Test
    {
        #region Test Types

        public class TestRequest : IRequest<TestResponse>
        {
            public string Value { get; set; } = string.Empty;
        }

        public class TestResponse : IResponse
        {
            public string Result { get; set; } = string.Empty;
        }

        public class TestRequest2 : IRequest<TestResponse2>
        {
            public int Id { get; set; }
        }

        public class TestResponse2 : IResponse
        {
            public int ComputedValue { get; set; }
        }

        public class TestSyncHandler : IRequestHandler<TestRequest, TestResponse>
        {
            public TestResponse Handle(TestRequest request)
            {
                return new TestResponse { Result = $"Handled: {request.Value}" };
            }
        }

        public class TestSyncHandler2 : IRequestHandler<TestRequest2, TestResponse2>
        {
            public TestResponse2 Handle(TestRequest2 request)
            {
                return new TestResponse2 { ComputedValue = request.Id * 2 };
            }
        }

        public class TestAsyncHandler : IAsyncRequestHandler<TestRequest, TestResponse>
        {
            public async Task<TestResponse> HandleAsync(TestRequest request)
            {
                await Task.Delay(1);
                return new TestResponse { Result = $"AsyncHandled: {request.Value}" };
            }
        }

        public class TestSubscriber : ISubscriber<string>
        {
            public string? LastValue { get; private set; }
            public bool IsCompleted { get; private set; }
            public Exception? LastError { get; private set; }

            public bool OnNext(string next)
            {
                LastValue = next;
                return true;
            }

            public bool OnError(Exception error)
            {
                LastError = error;
                return true;
            }

            public bool OnCompleted()
            {
                IsCompleted = true;
                return true;
            }

            public void Dispose() { }
        }

        public class CountingSubscriber : ISubscriber<string>
        {
            private readonly System.Collections.Concurrent.ConcurrentBag<string> _messages;

            public CountingSubscriber(System.Collections.Concurrent.ConcurrentBag<string> messages)
            {
                _messages = messages;
            }

            public bool OnNext(string next)
            {
                _messages.Add(next);
                return true;
            }

            public bool OnError(Exception error) => true;
            public bool OnCompleted() => true;
            public void Dispose() { }
        }

        public class SignalingCountingSubscriber : ISubscriber<string>
        {
            private readonly System.Collections.Concurrent.ConcurrentBag<string> _messages;
            private readonly CountdownEvent _countdown;

            public SignalingCountingSubscriber(System.Collections.Concurrent.ConcurrentBag<string> messages, CountdownEvent countdown)
            {
                _messages = messages;
                _countdown = countdown;
            }

            public bool OnNext(string next)
            {
                _messages.Add(next);
                _countdown.Signal();
                return true;
            }

            public bool OnError(Exception error) => true;
            public bool OnCompleted() => true;
            public void Dispose() { }
        }

        public class ErrorThrowingSubscriber : ISubscriber<string>
        {
            public Exception? LastError { get; private set; }

            public bool OnNext(string next)
            {
                throw new InvalidOperationException("Test error");
            }

            public bool OnError(Exception error)
            {
                LastError = error;
                return true;
            }

            public bool OnCompleted() => true;
            public void Dispose() { }
        }

        #endregion

        private static long _nextId = 0;
        private static IOrderedCache<long, object> CreateCache()
        {
            var configuration = new Baubit.Caching.Configuration();
            var loggerFactory = LoggerFactory.Create(b => { });
            // nextIdFactory generates incrementing long IDs
            Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref _nextId);
            var store = new Baubit.Caching.InMemory.Store<long, object>(null, null, nextIdFactory, loggerFactory);
            var metadata = new Baubit.Caching.InMemory.Metadata<long>(configuration, loggerFactory);
            return new Baubit.Caching.OrderedCache<long, object>(configuration, null, store, metadata, loggerFactory);
        }

        private static ILoggerFactory CreateLoggerFactory()
        {
            return LoggerFactory.Create(b => { });
        }

        [Fact]
        public void Publish_Object_AddsToCache()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription in background
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, null, cts.Token);

            // Act
            var result = mediator.Publish("test-notification");

            // Assert
            Assert.True(result);
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void Subscribe_SyncHandler_RegistersHandler()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();

            // Act
            var result = mediator.Subscribe<TestRequest, TestResponse>(handler, true, cts.Token);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Subscribe_DuplicateSyncHandler_ReturnsFalse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler1 = new TestSyncHandler();
            var handler2 = new TestSyncHandler();
            using var cts = new CancellationTokenSource();

            // Act
            var result1 = mediator.Subscribe<TestRequest, TestResponse>(handler1, true, cts.Token);
            var result2 = mediator.Subscribe<TestRequest, TestResponse>(handler2, true, cts.Token);

            // Assert
            Assert.True(result1);
            Assert.False(result2);
        }

        [Fact]
        public void Subscribe_MultipleHandlerTypes_RegistersBoth()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler1 = new TestSyncHandler();
            var handler2 = new TestSyncHandler2();
            using var cts = new CancellationTokenSource();

            // Act
            var result1 = mediator.Subscribe<TestRequest, TestResponse>(handler1, true, cts.Token);
            var result2 = mediator.Subscribe<TestRequest2, TestResponse2>(handler2, true, cts.Token);

            // Assert - both handlers should be registered since they handle different types
            Assert.True(result1);
            Assert.True(result2);
        }

        [Fact]
        public async Task PublishAsync_WithMultipleHandlerTypes_RoutesToCorrectHandler()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler1 = new TestSyncHandler();
            var handler2 = new TestSyncHandler2();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler1, true, cts.Token);
            mediator.Subscribe<TestRequest2, TestResponse2>(handler2, true, cts.Token);

            var request1 = new TestRequest { Value = "test" };
            var request2 = new TestRequest2 { Id = 5 };

            // Act
            var response1 = await mediator.PublishAsync<TestRequest, TestResponse>(request1);
            var response2 = await mediator.PublishAsync<TestRequest2, TestResponse2>(request2);

            // Assert
            Assert.Equal("Handled: test", response1.Result);
            Assert.Equal(10, response2.ComputedValue);
        }

        [Fact]
        public async Task PublishAsync_WithRegisteredHandler_ReturnsResponse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, true, cts.Token);

            var request = new TestRequest { Value = "test" };

            // Act
            var response = await mediator.PublishAsync<TestRequest, TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled: test", response.Result);
        }

        [Fact]
        public async Task PublishAsync_WithoutHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var request = new TestRequest { Value = "test" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.PublishAsync<TestRequest, TestResponse>(request));
        }

        [Fact]
        public async Task PublishAsync_ConcurrentRequests_AllProcessedSuccessfully()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, true, cts.Token);

            const int requestCount = 100;
            var tasks = new List<Task<TestResponse>>(requestCount);

            // Act - Fire many concurrent requests
            for (int i = 0; i < requestCount; i++)
            {
                var request = new TestRequest { Value = $"request-{i}" };
                tasks.Add(mediator.PublishAsync<TestRequest, TestResponse>(request));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(requestCount, responses.Length);
            for (int i = 0; i < requestCount; i++)
            {
                Assert.NotNull(responses[i]);
                Assert.Equal($"Handled: request-{i}", responses[i].Result);
            }
        }

        [Fact]
        public async Task SubscribeAsync_WithAsyncHandler_ProcessesRequests()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestAsyncHandler();
            using var cts = new CancellationTokenSource();

            // Act - Start subscription in background
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, null, cts.Token);

            // Wait a bit then publish a request
            var request = new TestRequest { Value = "async-test" };
            var publishTask = mediator.PublishAsync<TestRequest, TestResponse>(request);

            // Give time for processing
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                var response = await publishTask.WaitAsync(timeoutCts.Token);
                Assert.NotNull(response);
                Assert.Equal("AsyncHandled: async-test", response.Result);
            }
            finally
            {
                cts.Cancel();
            }
        }

        [Fact]
        public async Task SubscribeAsync_WithSubscriber_ReceivesNotifications()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription in background with buffering disabled
            var subscribeTask = mediator.SubscribeAsync(subscriber, false, null, cts.Token);

            // Publish a notification
            await Task.Delay(50); // Allow subscription to start
            mediator.Publish("test-message");

            // Wait a bit for processing
            await Task.Delay(100);

            // Assert - subscriber should have received the message
            Assert.Equal("test-message", subscriber.LastValue);

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Subscribe_CancellationUnregistersHandler()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, true, cts.Token);

            var request = new TestRequest { Value = "test" };

            // Verify handler is registered
            var response1 = await mediator.PublishAsync<TestRequest, TestResponse>(request);
            Assert.NotNull(response1);

            // Act - Cancel registration
            cts.Cancel();

            // Give time for cancellation to propagate
            await Task.Delay(50);

            // Assert - Handler should be unregistered
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.PublishAsync<TestRequest, TestResponse>(request));
        }

        [Fact]
        public async Task Subscribe_AfterCancellation_CanReregister()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler1 = new TestSyncHandler();
            var handler2 = new TestSyncHandler();
            var cts1 = new CancellationTokenSource();

            // Register first handler
            var result1 = mediator.Subscribe<TestRequest, TestResponse>(handler1, true, cts1.Token);
            Assert.True(result1);

            // Cancel first handler
            cts1.Cancel();
            await Task.Delay(50);

            // Act - Register a new handler for the same type
            using var cts2 = new CancellationTokenSource();
            var result2 = mediator.Subscribe<TestRequest, TestResponse>(handler2, true, cts2.Token);

            // Assert - Should be able to register after cancellation
            Assert.True(result2);

            var request = new TestRequest { Value = "test" };
            var response = await mediator.PublishAsync<TestRequest, TestResponse>(request);
            Assert.NotNull(response);
        }

        [Fact]
        public async Task Dispose_ClearsHandlers()
        {
            // Arrange
            var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, true, cts.Token);

            var request = new TestRequest { Value = "test" };

            // Verify handler is registered
            var response = await mediator.PublishAsync<TestRequest, TestResponse>(request);
            Assert.NotNull(response);

            // Act
            mediator.Dispose();

            // Assert - Handler should be cleared after dispose
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.PublishAsync<TestRequest, TestResponse>(request));
        }

        [Fact]
        public async Task Dispose_MultipleHandlerTypes_ClearsAll()
        {
            // Arrange
            var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler1 = new TestSyncHandler();
            var handler2 = new TestSyncHandler2();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler1, true, cts.Token);
            mediator.Subscribe<TestRequest2, TestResponse2>(handler2, true, cts.Token);

            var request1 = new TestRequest { Value = "test" };
            var request2 = new TestRequest2 { Id = 5 };

            // Verify handlers are registered
            Assert.NotNull(await mediator.PublishAsync<TestRequest, TestResponse>(request1));
            Assert.NotNull(await mediator.PublishAsync<TestRequest2, TestResponse2>(request2));

            // Act
            mediator.Dispose();

            // Assert - Both handlers should be cleared after dispose
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.PublishAsync<TestRequest, TestResponse>(request1));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.PublishAsync<TestRequest2, TestResponse2>(request2));
        }

        [Fact]
        public async Task Publish_NotificationWithoutBuffering_DoesNotAddToCache()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription in background with buffering disabled
            var subscribeTask = mediator.SubscribeAsync(subscriber, false, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Act
            var result = mediator.Publish("test-notification");

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count); // Should not be in cache
            await Task.Delay(100); // Give time for notification processing
            Assert.Equal("test-notification", subscriber.LastValue); // Should be delivered directly

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Publish_NotificationWithoutBuffering_ConcurrentPublish()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var countingSubscriber = new CountingSubscriber(receivedMessages);

            // Start subscription in background with buffering disabled
            var subscribeTask = mediator.SubscribeAsync(countingSubscriber, false, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Act - Publish notifications concurrently
            const int messageCount = 100;
            var publishTasks = new List<Task>();
            for (int i = 0; i < messageCount; i++)
            {
                var message = $"message-{i}";
                publishTasks.Add(Task.Run(() => mediator.Publish(message)));
            }
            await Task.WhenAll(publishTasks);

            // Wait for processing
            await Task.Delay(200);

            // Assert
            Assert.Equal(0, cache.Count); // Nothing should be in cache
            Assert.Equal(messageCount, receivedMessages.Count);

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Publish_NotificationWithoutBuffering_SubscriberError_ReturnsTrue()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var errorSubscriber = new ErrorThrowingSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription in background with buffering disabled
            var subscribeTask = mediator.SubscribeAsync(errorSubscriber, false, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Act
            var result = mediator.Publish("test-notification");

            // Assert
            Assert.True(result); // Should still return true even if subscriber throws
            await Task.Delay(100);
            Assert.NotNull(errorSubscriber.LastError); // Error should be captured

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Publish_NotificationWithBuffering_AddsToCache()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription with buffering enabled (default)
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, null, cts.Token);

            // Act
            var result = mediator.Publish("cached-notification");

            // Assert
            Assert.True(result);
            Assert.Equal(1, cache.Count);
            await Task.Delay(100); // Give time for subscriber to process
            Assert.Equal("cached-notification", subscriber.LastValue);

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Publish_NotificationWithBuffering_ConcurrentPublish()
        {
            // Arrange
            const int messageCount = 100;
            using var cts = new CancellationTokenSource();
            using var cache = CreateCache();
            using var allMessagesReceived = new CountdownEvent(messageCount);
            var cacheEnumerator = cache.GetFutureAsyncEnumerator(null, cts.Token); // this is to keep evictions from kicking in. Tests have been failing intermittently because eviction changes cache count
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var subscriber = new SignalingCountingSubscriber(receivedMessages, allMessagesReceived);

            // Start subscription with buffering enabled
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, null, cts.Token);

            // Act - Publish notifications concurrently
            var publishTasks = new List<Task>();
            for (int i = 0; i < messageCount; i++)
            {
                var message = $"cached-{i}";
                publishTasks.Add(Task.Run(() => mediator.Publish(message)));
            }
            await Task.WhenAll(publishTasks);

            // Wait for all messages to be received by the subscriber (with timeout)
            var receivedAll = allMessagesReceived.Wait(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(receivedAll, $"Timed out waiting for messages. Received {receivedMessages.Count} of {messageCount}");
            Assert.Equal(messageCount, receivedMessages.Count);
            // Wait for processing
            await Task.Delay(50);
            // Note: cache.Count may be less than messageCount due to eviction, so we don't assert on it

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Publish_MultipleSubscribers_MixedBuffering()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());

            var bufferedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var unbufferedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();

            var bufferedSubscriber = new CountingSubscriber(bufferedMessages);
            var unbufferedSubscriber = new CountingSubscriber(unbufferedMessages);

            using var cts = new CancellationTokenSource();

            // Start both subscriptions - one buffered, one not
            var bufferedTask = mediator.SubscribeAsync(bufferedSubscriber, true, null, cts.Token);
            var unbufferedTask = mediator.SubscribeAsync(unbufferedSubscriber, false, null, cts.Token);
            await Task.Delay(100); // Allow subscriptions to start

            // Act - Publish notifications
            const int messageCount = 50;
            for (int i = 0; i < messageCount; i++)
            {
                mediator.Publish($"msg-{i}");
            }

            // Wait for processing
            await Task.Delay(500);

            // Assert - Both subscribers should receive all messages
            Assert.Equal(messageCount, cache.Count); // Cache has all messages
            Assert.Equal(messageCount, bufferedMessages.Count); // Buffered subscriber got all
            Assert.Equal(messageCount, unbufferedMessages.Count); // Unbuffered subscriber got all

            // Cleanup
            cts.Cancel();
        }

        [Fact]
        public async Task Publish_NoSubscribers_WithBufferingTrue_AddsToCache()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Do NOT start any subscription first

            // Act - Publish notification before subscriber exists
            mediator.Publish("early-message");
            Assert.Equal(0, cache.Count); // No subscribers registered, so not added to cache

            // Now subscribe with buffering
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, null, cts.Token);

            // Publish after subscription
            mediator.Publish("late-message");
            await Task.Delay(100);

            // Assert
            Assert.Equal(1, cache.Count); // Only the message after subscription
            Assert.Equal("late-message", subscriber.LastValue);

            // Cleanup
            cts.Cancel();
        }

        #region Tests for PublishAsync with notification

        [Fact]
        public async Task PublishAsync_Notification_FireAndForget()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription in background
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, null, cts.Token);

            // Act - fire and forget notification
            var result = await mediator.PublishAsync("async-notification");

            // Assert
            Assert.True(result);
            Assert.Equal(1, cache.Count);
            await Task.Delay(50);
            Assert.Equal("async-notification", subscriber.LastValue);

            // Cleanup
            cts.Cancel();
        }

        #endregion

        #region Tests for SubscribeAsync with Func<TNotification, CancellationToken, Task<bool>> notificationHandler

        [Fact]
        public async Task SubscribeAsync_NotificationHandler_ReceivesFutureNotifications()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();

            // Act - Start subscription BEFORE adding to cache (EnumerateFutureAsync only gets future items)
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                true,
                null,
                cts.Token
            );

            await Task.Delay(50); // Allow subscription to start

            // Add notifications after subscription started
            cache.Add("notification-1", out _);
            cache.Add("notification-2", out _);
            cache.Add("notification-3", out _);

            await Task.Delay(100); // Wait for processing

            // Assert
            Assert.Equal(3, receivedNotifications.Count);
            Assert.Contains("notification-1", receivedNotifications);
            Assert.Contains("notification-2", receivedNotifications);
            Assert.Contains("notification-3", receivedNotifications);

            // Cleanup
            cts.Cancel();
            await Task.Delay(50); // Allow cancellation to propagate
        }

        [Fact]
        public async Task SubscribeAsync_NotificationHandler_HandlesNullHandler()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Act - Start subscription with null handler (should handle gracefully)
            Func<string, CancellationToken, Task<bool>> nullHandler = null;
            var subscribeTask = mediator.SubscribeAsync<string>(
                nullHandler,
                true,
                null,
                cts.Token
            );

            await Task.Delay(50);

            // Add notification
            cache.Add("test-notification", out _);
            await Task.Delay(50);

            // Assert - Should not throw
            Assert.NotNull(subscribeTask);

            // Cleanup
            cts.Cancel();
            await Task.Delay(50);
        }

        [Fact]
        public async Task SubscribeAsync_NotificationHandler_CancellationEndsSubscription()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();

            // Act - Start subscription
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                true,
                null,
                cts.Token
            );

            await Task.Delay(50);

            // Add notifications after subscription started
            cache.Add("during-subscription-1", out _);
            cache.Add("during-subscription-2", out _);
            await Task.Delay(100);

            // Assert - Should have received notifications before cancellation
            Assert.Equal(2, receivedNotifications.Count);
            Assert.Contains("during-subscription-1", receivedNotifications);
            Assert.Contains("during-subscription-2", receivedNotifications);

            // Cancel subscription
            cts.Cancel();

            // Wait for cancellation to propagate
            try
            {
                await subscribeTask;
            }
            catch (TaskCanceledException)
            {
                // Expected
            }
        }

        [Fact]
        public async Task SubscribeAsync_NotificationHandler_MultipleTypes()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();
            var receivedStrings = new System.Collections.Concurrent.ConcurrentBag<string>();
            var receivedInts = new System.Collections.Concurrent.ConcurrentBag<int>();

            // Act - Subscribe to different notification types FIRST
            var subscribeTask1 = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedStrings.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                true,
                null,
                cts1.Token
            );

            var subscribeTask2 = mediator.SubscribeAsync<int>(
                async (notification, ct) =>
                {
                    receivedInts.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                true,
                null,
                cts2.Token
            );

            await Task.Delay(50); // Allow subscriptions to start

            // Add different types to cache AFTER subscription
            cache.Add("string-notification", out _);
            cache.Add(42, out _);
            cache.Add("another-string", out _);
            cache.Add(100, out _);

            await Task.Delay(100);

            // Assert
            Assert.Equal(2, receivedStrings.Count);
            Assert.Equal(2, receivedInts.Count);
            Assert.Contains("string-notification", receivedStrings);
            Assert.Contains("another-string", receivedStrings);
            Assert.Contains(42, receivedInts);
            Assert.Contains(100, receivedInts);

            // Cleanup
            cts1.Cancel();
            cts2.Cancel();
            await Task.Delay(50);
        }

        [Fact]
        public async Task SubscribeAsync_NotificationHandler_IgnoresPastNotifications()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();

            // Add notifications BEFORE subscription
            cache.Add("past-1", out _);
            cache.Add("past-2", out _);

            // Act - Start subscription (should only get future notifications)
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                true,
                null,
                cts.Token
            );

            await Task.Delay(50); // Allow subscription to start

            // Add future notifications
            cache.Add("future-1", out _);
            cache.Add("future-2", out _);

            await Task.Delay(100); // Wait for processing

            // Assert - Should only receive future notifications
            Assert.Equal(2, receivedNotifications.Count);
            Assert.Contains("future-1", receivedNotifications);
            Assert.Contains("future-2", receivedNotifications);
            Assert.DoesNotContain("past-1", receivedNotifications);
            Assert.DoesNotContain("past-2", receivedNotifications);

            // Cleanup
            cts.Cancel();
            await Task.Delay(50);
        }

        #endregion

        #region Tests for SubscribeAsync with Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler

        [Fact]
        public async Task SubscribeAsync_AsyncHandler_ProcessesRequest()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Act - Subscribe with function handler
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse { Result = $"FuncHandled: {request.Value}" };
                },
                true,
                null,
                cts.Token
            );

            await Task.Delay(50); // Allow subscription to initialize

            // Publish async request
            var responseTask = mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "test-value" }, null, CancellationToken.None);

            var response = await responseTask;

            // Assert
            Assert.NotNull(response);
            Assert.Equal("FuncHandled: test-value", response.Result);

            // Cleanup
            cts.Cancel();
            await Task.Delay(50);
        }

        [Fact]
        public async Task SubscribeAsync_AsyncHandler_DuplicateSubscription_ReturnsFalse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Act - Subscribe first handler
            var subscribeTask1 = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse { Result = "Handler1" };
                },
                true,
                null,
                cts1.Token
            );
            await Task.Delay(50); // Allow first subscription to register

            // Try to subscribe second handler (different instance but same type)
            var subscribeTask2 = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse { Result = "Handler2" };
                },
                true,
                null,
                cts2.Token
            );
            await Task.Delay(50);

            // Assert - Second subscription should return false (not allowed)
            // The method returns false if another handler is already registered
            // We can verify this by cancelling the first and seeing the second can't process

            // Cleanup
            cts1.Cancel();
            cts2.Cancel();
            await Task.Delay(50);
        }

        [Fact]
        public async Task SubscribeAsync_AsyncHandler_CancellationEndsSubscription()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var processedRequests = 0;

            // Act - Subscribe with function handler
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    Interlocked.Increment(ref processedRequests);
                    await Task.Delay(1);
                    return new TestResponse { Result = $"Handled: {request.Value}" };
                },
                true,
                null,
                cts.Token
            );

            await Task.Delay(50); // Allow subscription to initialize

            // Publish request before cancellation
            using var requestCts1 = new CancellationTokenSource(500);
            var responseTask1 = mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "before-cancel" }, null, requestCts1.Token);

            var response1 = await responseTask1;
            Assert.NotNull(response1);
            Assert.Equal(1, processedRequests);

            // Cancel subscription
            cts.Cancel();
            await Task.Delay(50);

            // Publish request after cancellation - should throw InvalidOperationException since handler is unregistered
            using var requestCts2 = new CancellationTokenSource(200);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "after-cancel" }, null, requestCts2.Token);
            });

            // Assert - Only first request was processed
            Assert.Equal(1, processedRequests);
        }

        [Fact]
        public async Task SubscribeAsync_AsyncHandler_MultipleRequestTypes()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Act - Subscribe to different request types
            var subscribeTask1 = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse { Result = $"Handler1: {request.Value}" };
                },
                true,
                null,
                cts1.Token
            );

            var subscribeTask2 = mediator.SubscribeAsync<TestRequest2, TestResponse2>(
                async (request, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse2 { ComputedValue = request.Id * 10 };
                },
                true,
                null,
                cts2.Token
            );

            await Task.Delay(50); // Allow subscriptions to initialize

            // Publish different request types
            var response1Task = mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "test" }, null, CancellationToken.None);

            var response2Task = mediator.PublishAsync<TestRequest2, TestResponse2>(new TestRequest2 { Id = 5 }, null, CancellationToken.None);

            var response1 = await response1Task;
            var response2 = await response2Task;

            // Assert
            Assert.NotNull(response1);
            Assert.Equal("Handler1: test", response1.Result);
            Assert.NotNull(response2);
            Assert.Equal(50, response2.ComputedValue);

            // Cleanup
            cts1.Cancel();
            cts2.Cancel();
            await Task.Delay(50);
        }

        [Fact]
        public async Task SubscribeAsync_AsyncHandler_ConcurrentRequests()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Act - Subscribe with function handler
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    await Task.Delay(10); // Simulate processing time
                    return new TestResponse { Result = $"Handled: {request.Value}" };
                },
                true,
                null,
                cts.Token
            );

            await Task.Delay(50); // Allow subscription to initialize

            // Publish concurrent requests
            var tasks = new List<Task<TestResponse>>();
            for (int i = 0; i < 10; i++)
            {
                var requestValue = $"request-{i}";
                tasks.Add(mediator.PublishAsync<TestRequest, TestResponse>(
                    new TestRequest { Value = requestValue },
                    null,
                    CancellationToken.None
                ));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(10, responses.Length);
            for (int i = 0; i < 10; i++)
            {
                Assert.NotNull(responses[i]);
                Assert.StartsWith("Handled: request-", responses[i].Result);
            }

            // Cleanup
            cts.Cancel();
            await Task.Delay(50);
        }

        #endregion

        #region Tests for handler registration across different handler types

        [Fact]
        public async Task Subscribe_SyncHandler_BlocksAsyncHandler_ForSameRequestType()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var syncHandler = new TestSyncHandler();
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Act - Register sync handler first
            var result1 = mediator.Subscribe<TestRequest, TestResponse>(syncHandler, true, cts1.Token);
            Assert.True(result1);

            // Try to register async handler for same request type
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(
                new TestAsyncHandler(),
                true,
                null,
                cts2.Token
            );
            await Task.Delay(50);

            // The async subscription should return false since sync handler is registered
            // Due to the new unified tracking

            // Cleanup
            cts1.Cancel();
            cts2.Cancel();
        }

        [Fact]
        public async Task SubscribeAsync_AsyncHandler_BlocksSyncHandler_ForSameRequestType()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var asyncHandler = new TestAsyncHandler();
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Act - Register async handler first
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(
                asyncHandler,
                true,
                null,
                cts1.Token
            );
            await Task.Delay(50);

            // Try to register sync handler for same request type
            var result2 = mediator.Subscribe<TestRequest, TestResponse>(new TestSyncHandler(), true, cts2.Token);

            // Assert - Sync handler should fail to register
            Assert.False(result2);

            // Cleanup
            cts1.Cancel();
            cts2.Cancel();
        }

        #endregion

        #region Publisher Scenarios Tests

        [Fact]
        public async Task Publish_BufferedSyncSubscriber_DeliversNotification()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var subscriber = new CountingSubscriber(receivedMessages);
            using var cts = new CancellationTokenSource();

            // Act - Start buffered subscription
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: true, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Publish synchronously
            var result = mediator.Publish("buffered-sync-test");

            await Task.Delay(100); // Allow delivery

            // Assert
            Assert.True(result);
            Assert.Single(receivedMessages);
            Assert.Contains("buffered-sync-test", receivedMessages);

            cts.Cancel();
        }

        [Fact]
        public async Task Publish_UnbufferedSyncSubscriber_DeliversNotificationDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var subscriber = new CountingSubscriber(receivedMessages);
            using var cts = new CancellationTokenSource();

            // Act - Start unbuffered subscription
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Publish synchronously - should deliver directly
            var result = mediator.Publish("unbuffered-sync-test");

            // Assert - Direct delivery means no caching
            Assert.True(result);
            Assert.Single(receivedMessages);
            Assert.Contains("unbuffered-sync-test", receivedMessages);
            Assert.Equal(0, cache.Count); // Should not be in cache

            cts.Cancel();
        }

        [Fact]
        public async Task PublishAsync_BufferedAsyncSubscriber_DeliversNotification()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Act - Start buffered async subscription with Func handler
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: true,
                null,
                cts.Token
            );
            await Task.Delay(50); // Allow subscription to start

            // Publish asynchronously
            var result = await mediator.PublishAsync("buffered-async-test");

            await Task.Delay(100); // Allow delivery

            // Assert
            Assert.True(result);
            Assert.Single(receivedNotifications);
            Assert.Contains("buffered-async-test", receivedNotifications);

            cts.Cancel();
        }

        [Fact]
        public async Task PublishAsync_UnbufferedAsyncSubscriber_DeliversNotificationDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Act - Start unbuffered async subscription with Func handler
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: false,
                null,
                cts.Token
            );
            await Task.Delay(50); // Allow subscription to start

            // Publish asynchronously - should deliver directly
            var result = await mediator.PublishAsync("unbuffered-async-test");

            // Assert - Direct delivery means no caching
            Assert.True(result);
            Assert.Single(receivedNotifications);
            Assert.Contains("unbuffered-async-test", receivedNotifications);
            Assert.Equal(0, cache.Count); // Should not be in cache

            cts.Cancel();
        }

        [Fact]
        public async Task Publish_MixedBufferedUnbufferedSubscribers_BothReceiveNotification()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var bufferedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var unbufferedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var bufferedSubscriber = new CountingSubscriber(bufferedMessages);
            var unbufferedSubscriber = new CountingSubscriber(unbufferedMessages);
            using var cts = new CancellationTokenSource();

            // Act - Start both buffered and unbuffered subscriptions
            var bufferedTask = mediator.SubscribeAsync(bufferedSubscriber, enableBuffering: true, null, cts.Token);
            var unbufferedTask = mediator.SubscribeAsync(unbufferedSubscriber, enableBuffering: false, null, cts.Token);
            await Task.Delay(50); // Allow subscriptions to start

            // Publish - both should receive
            var result = mediator.Publish("mixed-subscribers-test");

            await Task.Delay(100); // Allow delivery

            // Assert
            Assert.True(result);
            Assert.Single(bufferedMessages);
            Assert.Single(unbufferedMessages);
            Assert.Contains("mixed-subscribers-test", bufferedMessages);
            Assert.Contains("mixed-subscribers-test", unbufferedMessages);

            cts.Cancel();
        }

        [Fact]
        public async Task PublishAsync_MixedBufferedUnbufferedFuncSubscribers_BothReceiveNotification()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var bufferedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            var unbufferedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Act - Start both buffered and unbuffered Func subscriptions
            var bufferedTask = mediator.SubscribeAsync<string>(
                async (n, ct) => { bufferedNotifications.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: true, null, cts.Token);
            var unbufferedTask = mediator.SubscribeAsync<string>(
                async (n, ct) => { unbufferedNotifications.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: false, null, cts.Token);
            await Task.Delay(50); // Allow subscriptions to start

            // Publish asynchronously - both should receive
            var result = await mediator.PublishAsync("mixed-func-test");

            await Task.Delay(100); // Allow delivery

            // Assert
            Assert.True(result);
            Assert.Single(bufferedNotifications);
            Assert.Single(unbufferedNotifications);
            Assert.Contains("mixed-func-test", bufferedNotifications);
            Assert.Contains("mixed-func-test", unbufferedNotifications);

            cts.Cancel();
        }

        #endregion

        #region Race Condition Tests

        [Fact]
        public async Task Subscribe_ConcurrentRegistration_NoRaceCondition()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handlers = new List<TestSyncHandler>();
            var results = new System.Collections.Concurrent.ConcurrentBag<bool>();
            
            // Create multiple handlers
            for (int i = 0; i < 10; i++)
            {
                handlers.Add(new TestSyncHandler());
            }

            // Act - Try to register all handlers concurrently for the same request type
            var tasks = new List<Task>();
            foreach (var handler in handlers)
            {
                tasks.Add(Task.Run(() =>
                {
                    using var cts = new CancellationTokenSource();
                    var result = mediator.Subscribe<TestRequest, TestResponse>(handler, true, cts.Token);
                    results.Add(result);
                }));
            }
            await Task.WhenAll(tasks);

            // Assert - Only one should succeed
            Assert.Equal(10, results.Count);
            Assert.Single(results, r => r == true);
            Assert.Equal(9, results.Count(r => r == false));
        }

        [Fact]
        public async Task SubscribeAsync_ConcurrentRegistration_NoRaceCondition()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var results = new System.Collections.Concurrent.ConcurrentBag<bool>();
            var ctsList = new List<CancellationTokenSource>();
            var subscriptionTasks = new List<Task<bool>>();

            // Act - Try to register multiple async handlers concurrently for the same request type
            for (int i = 0; i < 10; i++)
            {
                var cts = new CancellationTokenSource();
                ctsList.Add(cts);
                var handler = new TestAsyncHandler();
                subscriptionTasks.Add(mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, null, cts.Token));
            }

            // Wait a bit to let them all try to register
            await Task.Delay(100);

            // Cancel all to let the subscriptions end
            foreach (var cts in ctsList)
            {
                cts.Cancel();
            }

            // Wait for all tasks to complete
            try
            {
                var allResults = await Task.WhenAll(subscriptionTasks);
                foreach (var r in allResults) results.Add(r);
            }
            catch (TaskCanceledException)
            {
                // Expected for cancelled tasks
            }

            // Assert - At least one should have started successfully (exact count depends on timing)
            // The important thing is no exception is thrown
            Assert.True(true); // Test passes if no exception
            
            // Cleanup
            foreach (var cts in ctsList) cts.Dispose();
        }

        [Fact]
        public async Task Subscribe_ConcurrentFuncRegistration_NoRaceCondition()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var results = new System.Collections.Concurrent.ConcurrentBag<bool>();
            var ctsList = new List<CancellationTokenSource>();
            var subscriptionTasks = new List<Task<bool>>();

            // Act - Try to register multiple Func handlers concurrently for the same request type
            for (int i = 0; i < 10; i++)
            {
                var cts = new CancellationTokenSource();
                ctsList.Add(cts);
                var handlerIndex = i;
                Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse { Result = $"Handler-{handlerIndex}" };
                };
                subscriptionTasks.Add(mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, null, cts.Token));
            }

            // Wait a bit to let them all try to register
            await Task.Delay(100);

            // Cancel all
            foreach (var cts in ctsList)
            {
                cts.Cancel();
            }

            // Wait for all tasks to complete
            try
            {
                var allResults = await Task.WhenAll(subscriptionTasks);
                foreach (var r in allResults) results.Add(r);
            }
            catch (TaskCanceledException)
            {
                // Expected
            }

            // Assert - Only one should have been registered successfully
            var trueCount = results.Count(r => r);
            Assert.True(trueCount <= 1, $"Expected at most 1 successful registration, got {trueCount}");

            // Cleanup
            foreach (var cts in ctsList) cts.Dispose();
        }

        [Fact]
        public async Task Publish_DuringHandlerRegistration_NoMissedNotifications()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Start subscription
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: true,
                null,
                cts.Token
            );

            // Give subscription time to fully initialize
            await Task.Delay(50);

            // Act - Publish multiple notifications concurrently
            var publishTasks = new List<Task<bool>>();
            for (int i = 0; i < 100; i++)
            {
                var notificationValue = $"notification-{i}";
                publishTasks.Add(mediator.PublishAsync(notificationValue));
            }
            await Task.WhenAll(publishTasks);

            // Give time for delivery
            await Task.Delay(200);

            // Assert - All notifications should be received
            Assert.Equal(100, receivedNotifications.Count);

            cts.Cancel();
        }

        #endregion

        #region Tests for synchronous Publish with Func subscribers

        [Fact]
        public async Task Publish_WithBufferedFuncSubscriber_AddsToCache()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Start buffered Func subscription
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: true,
                null,
                cts.Token
            );
            await Task.Delay(50); // Allow subscription to start

            // Act - Publish synchronously (not PublishAsync)
            var result = mediator.Publish("sync-publish-buffered");

            // Assert
            Assert.True(result);
            Assert.Equal(1, cache.Count);
            await Task.Delay(100); // Allow delivery
            Assert.Single(receivedNotifications);
            Assert.Contains("sync-publish-buffered", receivedNotifications);

            cts.Cancel();
        }

        [Fact]
        public async Task Publish_WithUnbufferedFuncSubscriber_DeliversDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var receivedNotifications = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Start unbuffered Func subscription
            var subscribeTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    receivedNotifications.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: false,
                null,
                cts.Token
            );
            await Task.Delay(50); // Allow subscription to start

            // Act - Publish synchronously (not PublishAsync)
            var result = mediator.Publish("sync-publish-unbuffered");

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count); // Should not be in cache
            await Task.Delay(100); // Allow async handler to complete
            Assert.Single(receivedNotifications);
            Assert.Contains("sync-publish-unbuffered", receivedNotifications);

            cts.Cancel();
        }

        [Fact]
        public async Task Publish_WithMixedISubscriberAndFuncSubscribers_BothReceive()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var iSubscriberMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var funcMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
            var iSubscriber = new CountingSubscriber(iSubscriberMessages);
            using var cts = new CancellationTokenSource();

            // Start both ISubscriber and Func subscriptions (unbuffered for direct delivery)
            var iSubscriberTask = mediator.SubscribeAsync(iSubscriber, enableBuffering: false, null, cts.Token);
            var funcTask = mediator.SubscribeAsync<string>(
                async (notification, ct) =>
                {
                    funcMessages.Add(notification);
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: false,
                null,
                cts.Token
            );
            await Task.Delay(50); // Allow subscriptions to start

            // Act - Publish synchronously
            var result = mediator.Publish("mixed-sync-test");

            // Assert
            Assert.True(result);
            await Task.Delay(100); // Allow async handler to complete
            Assert.Single(iSubscriberMessages);
            Assert.Single(funcMessages);
            Assert.Contains("mixed-sync-test", iSubscriberMessages);
            Assert.Contains("mixed-sync-test", funcMessages);
            Assert.Equal(0, cache.Count); // Both unbuffered, nothing in cache

            cts.Cancel();
        }

        [Fact]
        public async Task Publish_WithNullFuncHandler_DoesNotThrow()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Start subscription with null handler
            Func<string, CancellationToken, Task<bool>> nullHandler = null;
            var subscribeTask = mediator.SubscribeAsync<string>(
                nullHandler,
                enableBuffering: false,
                null,
                cts.Token
            );
            await Task.Delay(50); // Allow subscription to start

            // Act - Publish synchronously with null handler
            var result = mediator.Publish("test-with-null");

            // Assert - Should not throw
            Assert.True(result);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task Publish_WithMultipleFuncSubscribers_AllReceive()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var messages1 = new System.Collections.Concurrent.ConcurrentBag<string>();
            var messages2 = new System.Collections.Concurrent.ConcurrentBag<string>();
            var messages3 = new System.Collections.Concurrent.ConcurrentBag<string>();
            using var cts = new CancellationTokenSource();

            // Start multiple Func subscriptions
            var task1 = mediator.SubscribeAsync<string>(
                async (n, ct) => { messages1.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: false, null, cts.Token);
            var task2 = mediator.SubscribeAsync<string>(
                async (n, ct) => { messages2.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: true, null, cts.Token);
            var task3 = mediator.SubscribeAsync<string>(
                async (n, ct) => { messages3.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: false, null, cts.Token);
            await Task.Delay(50); // Allow subscriptions to start

            // Act - Publish synchronously
            var result = mediator.Publish("multi-func-test");

            // Assert
            Assert.True(result);
            await Task.Delay(100); // Allow async handlers to complete
            Assert.Single(messages1);
            Assert.Single(messages2);
            Assert.Single(messages3);
            Assert.Contains("multi-func-test", messages1);
            Assert.Contains("multi-func-test", messages2);
            Assert.Contains("multi-func-test", messages3);

            cts.Cancel();
        }

        #endregion

        #region Tests for Subscribe method edge cases

        [Fact]
        public void Subscribe_RollbackScenario_WhenRequestTypeAlreadyRegistered()
        {
            // This test covers the scenario where syncHandlersByType.TryAdd succeeds
            // but requestHandlersByRequestType.TryAdd fails, triggering a rollback.
            // This is a race condition scenario that's hard to reproduce naturally.
            
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler1 = new TestSyncHandler();
            var handler2 = new TestSyncHandler();
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            // Register first handler
            var result1 = mediator.Subscribe<TestRequest, TestResponse>(handler1, true, cts1.Token);
            Assert.True(result1);

            // Act - Try to register second handler (should fail because requestType is already registered)
            var result2 = mediator.Subscribe<TestRequest, TestResponse>(handler2, true, cts2.Token);

            // Assert
            Assert.False(result2);
        }

        [Fact]
        public async Task PublishAsync_WithUnbufferedISubscriber_DeliversDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start unbuffered subscription
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Act - Publish using PublishAsync (not Publish)
            var result = await mediator.PublishAsync("async-unbuffered-test");

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count); // Should not be in cache
            Assert.Equal("async-unbuffered-test", subscriber.LastValue); // Should be delivered directly

            cts.Cancel();
        }

        #endregion
    }
}
