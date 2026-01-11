using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;

using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.InterfaceSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.InterfaceSubscription{T}"/>
    /// </summary>
    public class Test
    {
        #region Test Types

        public class TestSubscriber : ISubscriber<string>
        {
            public string LastReceived { get; private set; } = "";
            public int CallCount { get; private set; }

            public bool OnNext(string next)
            {
                LastReceived = next;
                CallCount++;
                return true;
            }

            public bool OnError(Exception error)
            {
                return false;
            }

            public bool OnCompleted()
            {
                return true;
            }

            public void Dispose()
            {
                // No cleanup needed
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
            var subscriber = new TestSubscriber();

            // Act
            var subscription = new Baubit.Mediation.InterfaceSubscription<string>(subscriber, true);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(subscriber, subscription.Subscriber);
        }

        [Fact]
        public void Publish_Unbuffered_InvokesSubscriber()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var subscription = new Baubit.Mediation.InterfaceSubscription<string>(subscriber, false);

            // Act
            var result = subscription.Publish("test-notification", CreateCache(), CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal("test-notification", subscriber.LastReceived);
            Assert.Equal(1, subscriber.CallCount);
        }

        [Fact]
        public void Dispose_ReleasesSubscriber()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var subscription = new Baubit.Mediation.InterfaceSubscription<string>(subscriber, true);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.Subscriber);
        }
    }
}
