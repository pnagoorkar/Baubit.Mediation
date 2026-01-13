//using Baubit.Caching;
//using Baubit.Caching.InMemory;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace Baubit.Mediation.Test.FuncSubscription
//{
//    /// <summary>
//    /// Tests for <see cref="Baubit.Mediation.Internals.FuncSubscription{T}"/>
//    /// </summary>
//    public class Test
//    {
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
//            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) => { await Task.CompletedTask; return true; };

//            // Act
//            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, true);

//            // Assert
//            Assert.NotNull(subscription);
//            Assert.True(subscription.EnableBuffering);
//            Assert.Same(handler, subscription.NotificationHandler);
//        }

//        [Fact]
//        public void Publish_Unbuffered_InvokesHandler()
//        {
//            // Arrange
//            var received = "";
//            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
//            {
//                received = msg;
//                await Task.CompletedTask;
//                return true;
//            };
//            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, false);

//            // Act
//            var result = subscription.Publish("test", CreateCache(), CancellationToken.None);

//            // Assert
//            Assert.True(result);
//            Assert.Equal("test", received);
//        }

//        [Fact]
//        public void Publish_WithNullHandler_ReturnsTrue()
//        {
//            // Arrange
//            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(null, false);

//            // Act
//            var result = subscription.Publish("test", CreateCache(), CancellationToken.None);

//            // Assert
//            Assert.True(result);
//        }

//        [Fact]
//        public void Dispose_ReleasesHandler()
//        {
//            // Arrange
//            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) => { await Task.CompletedTask; return true; };
//            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, true);

//            // Act
//            subscription.Dispose();

//            // Assert
//            Assert.Null(subscription.NotificationHandler);
//        }
//    }
//}
