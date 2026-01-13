using System;
using System.Threading;
using Xunit;

namespace Baubit.Mediation.Test.InterfaceSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.InterfaceSubscription{T}"/>
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

        public class TestSubscriberReturnsFalse : ISubscriber<string>
        {
            public bool OnNext(string next)
            {
                return false;
            }

            public bool OnError(Exception error)
            {
                return false;
            }

            public bool OnCompleted()
            {
                return false;
            }

            public void Dispose()
            {
            }
        }

        #endregion

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var subscriber = new TestSubscriber();

            // Act
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(subscriber, subscription.Subscriber);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            var subscriber = new TestSubscriber();

            // Act
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(subscriber, subscription.Subscriber);
        }

        [Fact]
        public void Handle_Unbuffered_InvokesSubscriber()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, false, CancellationToken.None);

            // Act
            var result = subscription.Handle("test-notification");

            // Assert
            Assert.True(result);
            Assert.Equal("test-notification", subscriber.LastReceived);
            Assert.Equal(1, subscriber.CallCount);
        }

        [Fact]
        public void Handle_SubscriberReturnsFalse_ReturnsFalse()
        {
            // Arrange
            var subscriber = new TestSubscriberReturnsFalse();
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, false, CancellationToken.None);

            // Act
            var result = subscription.Handle("test");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dispose_ReleasesSubscriber()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.Subscriber);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var cts = new CancellationTokenSource();

            // Act
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }
    }
}
