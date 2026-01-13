using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.AsyncFuncSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.AsyncFuncSubscription{TRequest, TResponse}"/>
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

        #endregion

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                await Task.CompletedTask;
                return new TestResponse { Result = $"Func: {req.Value}" };
            };

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.FuncHandler);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                await Task.CompletedTask;
                return new TestResponse { Result = $"Func: {req.Value}" };
            };

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(handler, subscription.FuncHandler);
        }

        [Fact]
        public async Task HandleAsync_WithRequest_InvokesHandler()
        {
            // Arrange
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                await Task.Delay(1);
                return new TestResponse { Result = $"Func: {req.Value}" };
            };
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);
            var request = new TestRequest { Value = "test" };

            // Act
            var response = await subscription.HandleAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Func: test", response.Result);
        }

        [Fact]
        public async Task HandleAsync_WithCancellationToken_PassesToHandler()
        {
            // Arrange
            CancellationToken receivedToken = CancellationToken.None;
            var cts = new CancellationTokenSource();
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                receivedToken = ct;
                await Task.CompletedTask;
                return new TestResponse { Result = $"Func: {req.Value}" };
            };
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, false, CancellationToken.None);
            var request = new TestRequest { Value = "test" };

            // Act
            await subscription.HandleAsync(request, cts.Token);

            // Assert
            Assert.Equal(cts.Token, receivedToken);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                await Task.CompletedTask;
                return new TestResponse();
            };
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.FuncHandler);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            Func<TestRequest, CancellationToken, Task<TestResponse>> handler = async (req, ct) =>
            {
                await Task.CompletedTask;
                return new TestResponse();
            };

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncFuncSubscription<TestRequest, TestResponse>(handler, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }
    }
}
