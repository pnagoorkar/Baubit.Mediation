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

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var handler = new TestAsyncHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.AsyncHandler);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            var handler = new TestAsyncHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(handler, subscription.AsyncHandler);
        }

        [Fact]
        public async Task HandleAsync_WithRequest_ReturnsResponse()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);
            var request = new TestRequest { Value = "direct" };

            // Act
            var response = await subscription.HandleAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("AsyncHandled: direct", response.Result);
        }

        [Fact]
        public async Task HandleAsync_CancellationTokenIgnored_StillInvokesHandler()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);
            var request = new TestRequest { Value = "test" };
            var cts = new CancellationTokenSource();

            // Act - Note: The handler doesn't accept cancellation token, so it's not used
            var response = await subscription.HandleAsync(request, cts.Token);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("AsyncHandled: test", response.Result);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.AsyncHandler);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var cts = new CancellationTokenSource();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var handler = new TestAsyncHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncInterfaceSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Act & Assert - Multiple disposes should not throw
            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();

            Assert.Null(subscription.AsyncHandler);
        }
    }
}
