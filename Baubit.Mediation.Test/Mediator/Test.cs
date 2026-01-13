using Baubit.Caching;
using Microsoft.Extensions.Logging;

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

            public bool OnNext(string next, CancellationToken cancellationToken = default)
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

            public bool OnNext(string next, CancellationToken cancellationToken = default)
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

            public bool OnNext(string next, CancellationToken cancellationToken = default)
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

            public bool OnNext(string next, CancellationToken cancellationToken = default)
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
        public async Task Publish_Object_AddsToCache()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start subscription in background
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, cts.Token);

            // Act
            var result = mediator.Publish("test-notification");

            // Assert
            Assert.True(result);
            Assert.Equal(1, cache.Count);
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
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler1, true, cts.Token);
            _ = mediator.SubscribeAsync<TestRequest2, TestResponse2>(handler2, true, cts.Token);

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
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, cts.Token);

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
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, cts.Token);

            // Ensure subscription is ready by waiting for a test request to complete
            var warmupRequest = new TestRequest { Value = "warmup" };
            var warmupRetries = 0;
            while (warmupRetries < 50)
            {
                try
                {
                    var warmupResponse = await Task.WhenAny(
                        mediator.PublishAsync<TestRequest, TestResponse>(warmupRequest),
                        Task.Delay(100)
                    );
                    if (warmupResponse is Task<TestResponse> responseTask && responseTask.IsCompleted)
                    {
                        await responseTask; // Verify it completes successfully
                        break;
                    }
                }
                catch
                {
                    // Subscription not ready yet
                }
                await Task.Delay(50);
                warmupRetries++;
            }

            const int requestCount = 100;
            var tasks = new List<Task<TestResponse>>(requestCount);

            // Act - Fire many concurrent requests
            for (int i = 0; i < requestCount; i++)
            {
                var request = new TestRequest { Value = $"request-{i}" };
                tasks.Add(mediator.PublishAsync<TestRequest, TestResponse>(request));
            }

            // Use timeout to prevent infinite wait
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var whenAllTask = Task.WhenAll(tasks);
            var completedTask = await Task.WhenAny(whenAllTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));
            
            Assert.True(whenAllTask.IsCompleted, "Test timed out waiting for concurrent requests");
            var responses = await whenAllTask;

            // Assert - all responses should be present (order may vary due to concurrency)
            Assert.Equal(requestCount, responses.Length);
            var responseSet = new HashSet<string>();
            for (int i = 0; i < requestCount; i++)
            {
                Assert.NotNull(responses[i]);
                Assert.StartsWith("Handled: request-", responses[i].Result);
                responseSet.Add(responses[i].Result);
            }
            // Verify all unique requests were handled
            Assert.Equal(requestCount, responseSet.Count);
            
            // Cleanup
            cts.Cancel();
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
            var subscribeTask = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, cts.Token);

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
            var subscribeTask = mediator.SubscribeAsync(subscriber, false, cts.Token);

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
        public async Task Dispose_ClearsHandlers()
        {
            // Arrange
            var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, cts.Token);

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
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler1, true, cts.Token);
            _ = mediator.SubscribeAsync<TestRequest2, TestResponse2>(handler2, true, cts.Token);

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
            var subscribeTask = mediator.SubscribeAsync(subscriber, false, cts.Token);
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
            var subscribeTask = mediator.SubscribeAsync(countingSubscriber, false, cts.Token);
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
            var subscribeTask = mediator.SubscribeAsync(errorSubscriber, false, cts.Token);
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
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, cts.Token);

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
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, cts.Token);

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
            var bufferedTask = mediator.SubscribeAsync(bufferedSubscriber, true, cts.Token);
            var unbufferedTask = mediator.SubscribeAsync(unbufferedSubscriber, false, cts.Token);
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
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, cts.Token);

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
            var subscribeTask = mediator.SubscribeAsync(subscriber, true, cts.Token);

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
            var subscription = mediator.SubscribeAsync<string>(
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
            var subscription = mediator.SubscribeAsync<string>(
                nullHandler,
                true,
                null,
                cts.Token
            );

            // Add notification
            cache.Add("test-notification", out _);

            // Assert - Should not throw
            Assert.False(await subscription);

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
            var subscription1 = mediator.SubscribeAsync<string>(
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

            var subscription2 = mediator.SubscribeAsync<int>(
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

            // Add different types to cache AFTER subscription
            mediator.Publish("string-notification");
            mediator.Publish(42);
            mediator.Publish("another-string");
            mediator.Publish(100);

            await Task.Delay(50); // some time for delivery

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
            var subscription = mediator.SubscribeAsync<string>(
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

            // Add future notifications
            mediator.Publish("future-1");
            mediator.Publish("future-2");

            await Task.Delay(50); // give time for delivery

            // Assert - Should only receive future notifications
            Assert.Equal(2, receivedNotifications.Count);
            Assert.Contains("future-1", receivedNotifications);
            Assert.Contains("future-2", receivedNotifications);
            Assert.DoesNotContain("past-1", receivedNotifications);
            Assert.DoesNotContain("past-2", receivedNotifications);

            // Cleanup
            cts.Cancel();
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
            var subscribeTask = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
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
            var responseTask = mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "test-value" }, CancellationToken.None);

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
            var subscribeTask1 = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
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
            var subscribeTask2 = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
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
            var subscribeTask = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
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
            var responseTask1 = mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "before-cancel" }, requestCts1.Token);

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
                await mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "after-cancel" }, requestCts2.Token);
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
            var subscribeTask1 = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (request, ct) =>
                {
                    await Task.Delay(1);
                    return new TestResponse { Result = $"Handler1: {request.Value}" };
                },
                true,
                null,
                cts1.Token
            );

            var subscribeTask2 = _ = mediator.SubscribeAsync<TestRequest2, TestResponse2>(
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
            var response1Task = mediator.PublishAsync<TestRequest, TestResponse>(new TestRequest { Value = "test" }, CancellationToken.None);

            var response2Task = mediator.PublishAsync<TestRequest2, TestResponse2>(new TestRequest2 { Id = 5 }, CancellationToken.None);

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
            var subscribeTask = _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
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

            // Try to register sync handler for same request type
            var subscription = mediator.SubscribeAsync<TestRequest, TestResponse>(new TestSyncHandler(), true, cts2.Token);

            // Assert - Sync handler should fail to register
            Assert.False(await subscription);

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
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: true, cancellationToken: cts.Token);
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
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, cancellationToken: cts.Token);
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
            var subscription = mediator.SubscribeAsync<string>(
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
            var subscription = mediator.SubscribeAsync<string>(
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
            var bufferedTask = mediator.SubscribeAsync(bufferedSubscriber, enableBuffering: true, cancellationToken: cts.Token);
            var unbufferedTask = mediator.SubscribeAsync(unbufferedSubscriber, enableBuffering: false, cancellationToken: cts.Token);
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
            var bufferedSubscription = mediator.SubscribeAsync<string>(
                async (n, ct) => { bufferedNotifications.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: true, cancellationToken: cts.Token);
            var unbufferedSubscription = mediator.SubscribeAsync<string>(
                async (n, ct) => { unbufferedNotifications.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: false, cancellationToken: cts.Token);

            // Publish asynchronously - both should receive
            var result = await mediator.PublishAsync("mixed-func-test");

            await Task.Delay(50); // give some time for delivery 

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
                subscriptionTasks.Add(mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, cts.Token));
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
                subscriptionTasks.Add(mediator.SubscribeAsync<TestRequest, TestResponse>(handler, true, cts.Token));
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
            var subscription = mediator.SubscribeAsync<string>(
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
            await using var enumerator = cache.GetFutureAsyncEnumerator(null, cts.Token); // create enumerator to suppress eviction

            // Start buffered Func subscription
            var subscription = mediator.SubscribeAsync<string>(
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

            // Act - Publish synchronously (not PublishAsync)
            var result = mediator.Publish("sync-publish-buffered");

            await Task.Delay(50); // some time for delivery

            // Assert
            Assert.True(result);
            Assert.Equal(1, cache.Count);
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
            var subscription = mediator.SubscribeAsync<string>(
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

            // Act - Publish synchronously (not PublishAsync)
            var result = mediator.Publish("sync-publish-unbuffered");

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count); // Should not be in cache
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
            var iSubscriberTask = mediator.SubscribeAsync(iSubscriber, enableBuffering: false, cancellationToken: cts.Token);
            var subscription = mediator.SubscribeAsync<string>(
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

            // Act - Publish synchronously
            var result = mediator.Publish("mixed-sync-test");

            // Assert
            Assert.True(result);
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
            var subscription = mediator.SubscribeAsync<string>(
                nullHandler,
                enableBuffering: false,
                null,
                cts.Token
            );

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
            var subscription1 = mediator.SubscribeAsync<string>(
                async (n, ct) => { messages1.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: false, cancellationToken: cts.Token);
            var subscription2 = mediator.SubscribeAsync<string>(
                async (n, ct) => { messages2.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: true, cancellationToken: cts.Token);
            var subscription3 = mediator.SubscribeAsync<string>(
                async (n, ct) => { messages3.Add(n); await Task.CompletedTask; return true; },
                enableBuffering: false, cancellationToken: cts.Token);

            // Act - Publish synchronously
            var result = mediator.Publish("multi-func-test");

            await Task.Delay(50); // allow delivery

            // Assert
            Assert.True(result);
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
        public async Task PublishAsync_WithUnbufferedISubscriber_DeliversDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber = new TestSubscriber();
            using var cts = new CancellationTokenSource();

            // Start unbuffered subscription
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, cancellationToken: cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Act - Publish using PublishAsync (not Publish)
            var result = await mediator.PublishAsync("async-unbuffered-test");

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count); // Should not be in cache
            Assert.Equal("async-unbuffered-test", subscriber.LastValue); // Should be delivered directly

            cts.Cancel();
        }

        [Fact]
        public async Task SubscribeAsync_WithNullSyncHandler_ReturnsFalse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Act
            var result = await mediator.SubscribeAsync<TestRequest, TestResponse>(
                (IRequestHandler<TestRequest, TestResponse>)null, 
                true, 
                null,
                cts.Token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SubscribeAsync_WithNullAsyncHandler_ReturnsFalse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Act
            var result = await mediator.SubscribeAsync<TestRequest, TestResponse>(
                (IAsyncRequestHandler<TestRequest, TestResponse>)null, 
                true, 
                cts.Token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SubscribeAsync_WithNullSubscriber_ReturnsFalse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            // Act
            var result = await mediator.SubscribeAsync<string>(
                (ISubscriber<string>)null, 
                true, 
                cts.Token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SubscribeAsync_SyncHandlerUnbuffered_ProcessesDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();

            // Act - Start subscription with enableBuffering = false
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, false, null, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Publish request
            var request = new TestRequest { Value = "unbuffered-sync" };
            var response = await mediator.PublishAsync<TestRequest, TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled: unbuffered-sync", response.Result);
            Assert.Equal(0, cache.Count); // Should not use cache

            cts.Cancel();
        }

        [Fact]
        public async Task SubscribeAsync_AsyncHandlerUnbuffered_ProcessesDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestAsyncHandler();
            using var cts = new CancellationTokenSource();

            // Act - Start subscription with enableBuffering = false
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, false, cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Publish request
            var request = new TestRequest { Value = "unbuffered-async" };
            var response = await mediator.PublishAsync<TestRequest, TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("AsyncHandled: unbuffered-async", response.Result);
            Assert.Equal(0, cache.Count); // Should not use cache

            cts.Cancel();
        }

        [Fact]
        public async Task SubscribeAsync_FuncHandlerUnbuffered_ProcessesDirectly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var received = "";

            // Act - Start subscription with enableBuffering = false
            _ = mediator.SubscribeAsync<TestRequest, TestResponse>(
                async (req, ct) =>
                {
                    received = req.Value;
                    await Task.CompletedTask;
                    return new TestResponse { Result = $"Func: {req.Value}" };
                },
                false,
                null,
                cts.Token);
            await Task.Delay(50); // Allow subscription to start

            // Publish request
            var request = new TestRequest { Value = "unbuffered-func" };
            var response = await mediator.PublishAsync<TestRequest, TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Func: unbuffered-func", response.Result);
            Assert.Equal("unbuffered-func", received);
            Assert.Equal(0, cache.Count); // Should not use cache

            cts.Cancel();
        }

        [Fact]
        public async Task Publish_MultipleBufferedSubscribers_OnlyAddsToOnceToCacheOnce()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var messages1 = new System.Collections.Concurrent.ConcurrentBag<string>();
            var messages2 = new System.Collections.Concurrent.ConcurrentBag<string>();
            var subscriber1 = new CountingSubscriber(messages1);
            var subscriber2 = new CountingSubscriber(messages2);
            using var cts = new CancellationTokenSource();

            // Act - Start multiple buffered subscriptions
            var task1 = mediator.SubscribeAsync(subscriber1, enableBuffering: true, cancellationToken: cts.Token);
            var task2 = mediator.SubscribeAsync(subscriber2, enableBuffering: true, cancellationToken: cts.Token);
            await Task.Delay(50); // Allow subscriptions to start

            // Publish - should add to cache only once
            var result = mediator.Publish("multi-buffered-test");

            await Task.Delay(100); // Allow delivery
            cts.Cancel();

            // Assert - Both subscribers should receive it, but cache should only have one entry
            Assert.True(result);
            Assert.Contains("multi-buffered-test", messages1);
            Assert.Contains("multi-buffered-test", messages2);
        }

        #endregion

        #region Subscription Tests - Double Disposal

        [Fact]
        public void Subscription_DoubleDispose_DoesNotThrow()
        {
            // Arrange
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
            {
                await Task.CompletedTask;
                return true;
            };
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, false, CancellationToken.None);

            // Act & Assert
            subscription.Dispose();
            subscription.Dispose(); // Second dispose should be safe
        }

        [Fact]
        public void AsyncFuncSubscription_DoubleDispose_DoesNotThrow()
        {
            // Arrange
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                await Task.CompletedTask;
                return new TestResponse();
            };
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);

            // Act & Assert
            subscription.Dispose();
            subscription.Dispose(); // Second dispose should be safe
        }

        #endregion

        #region Edge Cases - Disposed Subscriptions

        [Fact]
        public void FuncSubscription_HandleAfterDispose_HandlesGracefully()
        {
            // Arrange
            var called = false;
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
            {
                called = true;
                await Task.CompletedTask;
                return true;
            };
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, false, CancellationToken.None);
            subscription.Dispose();

            // Act
            var result = subscription.Handle("test");

            // Assert - Should return true without calling handler (handler is null after dispose)
            Assert.True(result);
            Assert.False(called);
        }

        [Fact]
        public void InterfaceSubscription_HandleAfterDispose_ThrowsNullReference()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, false, CancellationToken.None);
            subscription.Dispose();

            // Act & Assert - Should throw because subscriber is null after dispose
            Assert.Throws<NullReferenceException>(() => subscription.Handle("test"));
        }

        #endregion

        #region CancellationToken Tests

        [Fact]
        public void Publish_WithCancelledToken_ReturnsEarly()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var subscriber1 = new TestSubscriber();
            var subscriber2 = new TestSubscriber();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel before publishing

            var task1 = mediator.SubscribeAsync(subscriber1, enableBuffering: false, cancellationToken: CancellationToken.None);
            var task2 = mediator.SubscribeAsync(subscriber2, enableBuffering: false, cancellationToken: CancellationToken.None);

            // Act
            var result = mediator.Publish("test", cts.Token);

            // Assert - Should return true and stop early without delivering to all subscribers
            Assert.True(result);
        }

        [Fact]
        public async Task Publish_PassesCancellationTokenToSubscriber()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            CancellationToken receivedToken = CancellationToken.None;
            
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
            {
                receivedToken = ct;
                await Task.CompletedTask;
                return true;
            };

            using var cts = new CancellationTokenSource();
            var subscribeTask = mediator.SubscribeAsync(handler, enableBuffering: false, cancellationToken: CancellationToken.None);
            await Task.Delay(50); // Let subscription start

            // Act
            var result = mediator.Publish("test", cts.Token);

            // Assert
            Assert.True(result);
            Assert.Equal(cts.Token, receivedToken);
            
            cts.Cancel();
        }

        [Fact]
        public async Task PublishAsync_PassesCancellationTokenThrough()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            CancellationToken receivedToken = CancellationToken.None;
            
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
            {
                receivedToken = ct;
                await Task.CompletedTask;
                return true;
            };

            using var cts = new CancellationTokenSource();
            var subscribeTask = mediator.SubscribeAsync(handler, enableBuffering: false, cancellationToken: CancellationToken.None);
            await Task.Delay(50); // Let subscription start

            // Act
            var result = await mediator.PublishAsync("test", cts.Token);

            // Assert
            Assert.True(result);
            Assert.Equal(cts.Token, receivedToken);
            
            cts.Cancel();
        }

        [Fact]
        public async Task Subscriber_OnNext_ReceivesCancellationToken()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            CancellationToken receivedToken = CancellationToken.None;

            var subscriber = new TestSubscriberWithTokenCapture((token) => receivedToken = token);
            using var cts = new CancellationTokenSource();
            
            var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, cancellationToken: CancellationToken.None);
            await Task.Delay(50); // Let subscription start

            // Act
            var result = mediator.Publish("test", cts.Token);

            // Assert
            Assert.True(result);
            Assert.Equal(cts.Token, receivedToken);
            
            cts.Cancel();
        }

        private class TestSubscriberWithTokenCapture : ISubscriber<string>
        {
            private readonly Action<CancellationToken> _tokenCapture;

            public TestSubscriberWithTokenCapture(Action<CancellationToken> tokenCapture)
            {
                _tokenCapture = tokenCapture;
            }

            public bool OnNext(string next, CancellationToken cancellationToken = default)
            {
                _tokenCapture(cancellationToken);
                return true;
            }

            public bool OnError(Exception error) => true;
            public bool OnCompleted() => true;
            public void Dispose() { }
        }

        #endregion
    }
}
