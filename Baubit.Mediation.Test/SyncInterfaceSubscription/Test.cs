//using Baubit.Caching;
//using Baubit.Caching.InMemory;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace Baubit.Mediation.Test.SyncInterfaceSubscription
//{
//    /// <summary>
//    /// Tests for <see cref="Baubit.Mediation.Internals.SyncInterfaceSubscription{TRequest, TResponse}"/>
//    /// </summary>
//    public class Test
//    {
//        #region Test Types

//        public class TestRequest : IRequest<TestResponse>
//        {
//            public string Value { get; set; } = string.Empty;
//        }

//        public class TestResponse : IResponse
//        {
//            public string Result { get; set; } = string.Empty;
//        }

//        public class TestSyncHandler : IRequestHandler<TestRequest, TestResponse>
//        {
//            public TestResponse Handle(TestRequest request)
//            {
//                return new TestResponse { Result = $"Handled: {request.Value}" };
//            }
//        }

//        #endregion

//        private static long _nextId = 0;
//        private IOrderedCache<long, object> CreateCache()
//        {
//            var configuration = new Baubit.Caching.Configuration();
//            var loggerFactory = LoggerFactory.Create(b => { });
//            Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref _nextId);
//            var store = new Baubit.Caching.InMemory.Store<long, object>(null, null, nextIdFactory, loggerFactory);
//            var metadata = new Baubit.Caching.InMemory.Metadata<long>(configuration, loggerFactory);
//            return new Baubit.Caching.OrderedCache<long, object>(configuration, null, store, metadata, loggerFactory);
//        }

//        [Fact]
//        public void Constructor_WithValidParameters_CreatesInstance()
//        {
//            // Arrange
//            var handler = new TestSyncHandler();

//            // Act
//            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, true);

//            // Assert
//            Assert.NotNull(subscription);
//            Assert.True(subscription.EnableBuffering);
//            Assert.Same(handler, subscription.SyncHandler);
//        }

//        [Fact]
//        public void Constructor_WithBufferingDisabled_CreatesInstance()
//        {
//            // Arrange
//            var handler = new TestSyncHandler();

//            // Act
//            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, false);

//            // Assert
//            Assert.NotNull(subscription);
//            Assert.False(subscription.EnableBuffering);
//            Assert.Same(handler, subscription.SyncHandler);
//        }

//        [Fact]
//        public void Dispose_ReleasesHandler()
//        {
//            // Arrange
//            var handler = new TestSyncHandler();
//            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, true);

//            // Act
//            subscription.Dispose();

//            // Assert
//            Assert.Null(subscription.SyncHandler);
//        }
//    }
//}
