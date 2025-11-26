using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using System;
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

        public class TestSyncHandler : IRequestHandler<TestRequest, TestResponse>
        {
            public TestResponse Handle(TestRequest request)
            {
                return new TestResponse { Result = $"Handled: {request.Value}" };
            }

            public Task<TestResponse> HandleSyncAsync(TestRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new TestResponse { Result = $"Handled: {request.Value}" });
            }

            public void Dispose() { }
        }

        public class TestAsyncHandler : IAsyncRequestHandler<TestRequest, TestResponse>
        {
            public async Task<TestResponse> HandleAsyncAsync(TestRequest request)
            {
                await Task.Delay(1);
                return new TestResponse { Result = $"AsyncHandled: {request.Value}" };
            }

            public void Dispose() { }
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

        #endregion

        private static IOrderedCache<object> CreateCache()
        {
            var loggerFactory = LoggerFactory.Create(b => { });
            var store = new Store<object>(loggerFactory);
            var metadata = new Metadata();
            return new OrderedCache<object>(new Baubit.Caching.Configuration(), null, store, metadata, loggerFactory);
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
            var result = mediator.Subscribe<TestRequest, TestResponse>(handler, cts.Token);

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
            var result1 = mediator.Subscribe<TestRequest, TestResponse>(handler1, cts.Token);
            var result2 = mediator.Subscribe<TestRequest, TestResponse>(handler2, cts.Token);

            // Assert
            Assert.True(result1);
            Assert.False(result2);
        }

        [Fact]
        public void Publish_WithRegisteredHandler_ReturnsResponse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, cts.Token);

            var request = new TestRequest { Value = "test" };

            // Act
            var response = mediator.Publish<TestRequest, TestResponse>(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled: test", response.Result);
        }

        [Fact]
        public void Publish_WithoutHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var request = new TestRequest { Value = "test" };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => mediator.Publish<TestRequest, TestResponse>(request));
        }

        [Fact]
        public async Task PublishAsync_WithRegisteredHandler_ReturnsResponse()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, cts.Token);

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
        public async Task SubscribeAsync_WithAsyncHandler_ProcessesRequests()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestAsyncHandler();
            using var cts = new CancellationTokenSource();

            // Act - Start subscription in background
            var subscribeTask = mediator.SubscribeAsync<TestRequest, TestResponse>(handler, cts.Token);

            // Wait a bit then publish a request
            var request = new TestRequest { Value = "async-test" };
            var publishTask = mediator.PublishAsyncAsync<TestRequest, TestResponse>(request);

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

            // Start subscription in background
            var subscribeTask = mediator.SubscribeAsync(subscriber, cts.Token);

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
        public void Subscribe_CancellationUnregistersHandler()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, cts.Token);

            var request = new TestRequest { Value = "test" };

            // Verify handler is registered
            var response1 = mediator.Publish<TestRequest, TestResponse>(request);
            Assert.NotNull(response1);

            // Act - Cancel registration
            cts.Cancel();

            // Give time for cancellation to propagate
            Thread.Sleep(50);

            // Assert - Handler should be unregistered
            Assert.Throws<InvalidOperationException>(() => mediator.Publish<TestRequest, TestResponse>(request));
        }

        [Fact]
        public void Dispose_ClearsHandlers()
        {
            // Arrange
            var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            var handler = new TestSyncHandler();
            using var cts = new CancellationTokenSource();
            mediator.Subscribe<TestRequest, TestResponse>(handler, cts.Token);

            var request = new TestRequest { Value = "test" };

            // Verify handler is registered
            var response = mediator.Publish<TestRequest, TestResponse>(request);
            Assert.NotNull(response);

            // Act
            mediator.Dispose();

            // Assert - Handler should be cleared after dispose
            Assert.Throws<InvalidOperationException>(() => mediator.Publish<TestRequest, TestResponse>(request));
        }
    }
}
