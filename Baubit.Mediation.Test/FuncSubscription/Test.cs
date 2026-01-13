using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.FuncSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.FuncSubscription{T}"/>
    /// </summary>
    public class Test
    {
        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) => { await Task.CompletedTask; return true; };

            // Act
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.NotificationHandler);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) => { await Task.CompletedTask; return true; };

            // Act
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(handler, subscription.NotificationHandler);
        }

        [Fact]
        public void Handle_Unbuffered_InvokesHandler()
        {
            // Arrange
            var received = "";
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
            {
                received = msg;
                await Task.CompletedTask;
                return true;
            };
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, false, CancellationToken.None);

            // Act
            var result = subscription.Handle("test");

            // Assert
            Assert.True(result);
            Assert.Equal("test", received);
        }

        [Fact]
        public void Handle_WithNullHandler_ReturnsTrue()
        {
            // Arrange
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(null, false, CancellationToken.None);

            // Act
            var result = subscription.Handle("test");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Handle_HandlerReturnsFalse_ReturnsFalse()
        {
            // Arrange
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) =>
            {
                await Task.CompletedTask;
                return false;
            };
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, false, CancellationToken.None);

            // Act
            var result = subscription.Handle("test");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) => { await Task.CompletedTask; return true; };
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.NotificationHandler);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            Func<string, CancellationToken, Task<bool>> handler = async (msg, ct) => { await Task.CompletedTask; return true; };

            // Act
            var subscription = new Baubit.Mediation.Internals.FuncSubscription<string>(handler, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }
    }
}
