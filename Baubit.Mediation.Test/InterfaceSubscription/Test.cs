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

            public bool OnNext(string next, CancellationToken cancellationToken = default)
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
            public bool OnNext(string next, CancellationToken cancellationToken = default)
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

        [Fact]
        public void Handle_PassesCancellationTokenToSubscriber()
        {
            // Arrange
            CancellationToken receivedToken = CancellationToken.None;
            var subscriber = new TestSubscriberWithTokenCapture((token) => receivedToken = token);
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, false, CancellationToken.None);
            var cts = new CancellationTokenSource();

            // Act
            var result = subscription.Handle("test", cts.Token);

            // Assert
            Assert.True(result);
            Assert.Equal(cts.Token, receivedToken);
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

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var subscriber = new TestSubscriber();
            var subscription = new Baubit.Mediation.Internals.InterfaceSubscription<string>(subscriber, true, CancellationToken.None);

            // Act & Assert - Multiple disposes should not throw
            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();

            Assert.Null(subscription.Subscriber);
        }
    }
}
