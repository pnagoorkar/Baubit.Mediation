using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.AsyncStreamFuncSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.AsyncStreamFuncSubscription{TRequest, TSegment, TResponse}"/>
    /// </summary>
    public class Test
    {
        #region Test Types

        public class TestStreamRequest : IStreamRequest<TestSegment, TestResponse>
        {
            public string Value { get; set; } = string.Empty;
        }

        public class TestSegment : ISegment<TestResponse>
        {
            public string Part { get; set; } = string.Empty;
        }

        public class TestResponse : IResponse { }

        #endregion

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => EmptySegments(ct);

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.FuncHandler);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => EmptySegments(ct);

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(handler, subscription.FuncHandler);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => EmptySegments(ct);

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }

        [Fact]
        public async Task HandleAsync_WithRequest_InvokesHandlerAndYieldsSegments()
        {
            // Arrange
            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => ProduceSegments(req.Value, ct);

            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, false, CancellationToken.None);
            var request = new TestStreamRequest { Value = "test" };

            // Act
            var segments = new System.Collections.Generic.List<string>();
            await foreach (var segment in subscription.HandleAsync(request, CancellationToken.None))
            {
                segments.Add(segment.Part);
            }

            // Assert
            Assert.Equal(3, segments.Count);
            Assert.Equal("test-0", segments[0]);
            Assert.Equal("test-1", segments[1]);
            Assert.Equal("test-2", segments[2]);
        }

        [Fact]
        public async Task HandleAsync_PassesCancellationTokenToHandler()
        {
            // Arrange
            CancellationToken receivedToken = CancellationToken.None;
            var cts = new CancellationTokenSource();

            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => { receivedToken = ct; return EmptySegments(ct); };

            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, false, CancellationToken.None);
            var request = new TestStreamRequest { Value = "test" };

            // Act
            await foreach (var _ in subscription.HandleAsync(request, cts.Token)) { }

            // Assert
            Assert.Equal(cts.Token, receivedToken);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => EmptySegments(ct);

            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.FuncHandler);
        }

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            Func<TestStreamRequest, CancellationToken, IAsyncEnumerable<TestSegment>> handler =
                (req, ct) => EmptySegments(ct);

            var subscription = new Baubit.Mediation.Internals.AsyncStreamFuncSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, CancellationToken.None);

            // Act & Assert
            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();

            Assert.Null(subscription.FuncHandler);
        }

        #region Helpers

        private static async IAsyncEnumerable<TestSegment> EmptySegments([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        private static async IAsyncEnumerable<TestSegment> ProduceSegments(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 3; i++)
            {
                await Task.Yield();
                yield return new TestSegment { Part = $"{prefix}-{i}" };
            }
        }

        #endregion
    }
}