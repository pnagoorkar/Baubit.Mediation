using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.SyncInterfaceSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.SyncInterfaceSubscription{TRequest, TResponse}"/>
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
        }

        #endregion

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var handler = new TestSyncHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.SyncHandler);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            var handler = new TestSyncHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(handler, subscription.SyncHandler);
        }

        [Fact]
        public async Task HandleAsync_WithRequest_ReturnsResponse()
        {
            // Arrange
            var handler = new TestSyncHandler();
            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);
            var request = new TestRequest { Value = "test" };

            // Act
            var response = await subscription.HandleAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled: test", response.Result);
        }

        [Fact]
        public async Task HandleAsync_CancellationTokenIgnored_StillInvokesHandler()
        {
            // Arrange
            var handler = new TestSyncHandler();
            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);
            var request = new TestRequest { Value = "test" };
            var cts = new CancellationTokenSource();

            // Act - Note: Synchronous handlers don't use cancellation tokens
            var response = await subscription.HandleAsync(request, cts.Token);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Handled: test", response.Result);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            var handler = new TestSyncHandler();
            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.SyncHandler);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var handler = new TestSyncHandler();
            var cts = new CancellationTokenSource();

            // Act
            var subscription = new Baubit.Mediation.Internals.SyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }
    }
}
