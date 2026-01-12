using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.AsyncInterfaceSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.AsyncInterfaceSubscription{TRequest, TResponse}"/>
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

        public class TestAsyncHandler : IAsyncRequestHandler<TestRequest, TestResponse>
        {
            public async Task<TestResponse> HandleAsync(TestRequest request)
            {
                await Task.Delay(1);
                return new TestResponse { Result = $"AsyncHandled: {request.Value}" };
            }
        }

        #endregion

        private static long _nextId = 0;
        private IOrderedCache<long, object> CreateCache()
        {
            var configuration = new Baubit.Caching.Configuration();
            var loggerFactory = LoggerFactory.Create(b => { });
            Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref _nextId);
            var store = new Baubit.Caching.InMemory.Store<long, object>(null, null, nextIdFactory, loggerFactory);
            var metadata = new Baubit.Caching.InMemory.Metadata<long>(configuration, loggerFactory);
            return new Baubit.Caching.OrderedCache<long, object>(configuration, null, store, metadata, loggerFactory);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var handler = new TestAsyncHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, true);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.AsyncHandler);
        }

        [Fact]
        public async Task DispatchAsync_WithRequest_ReturnsResponse()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, false);
            var request = new TestRequest { Value = "direct" };

            // Act
            var response = await subscription.PublishAsync(
                request,
                CreateCache(),
                Baubit.Identity.GuidV7Generator.CreateNew(),
                null,
                CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("AsyncHandled: direct", response.Result);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, true);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.AsyncHandler);
        }
    }
}
