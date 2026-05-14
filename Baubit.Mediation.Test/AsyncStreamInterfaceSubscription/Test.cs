using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Baubit.Mediation.Test.AsyncStreamInterfaceSubscription
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription{TRequest, TSegment, TResponse}"/>
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

        public class TestStreamHandler : IAsyncStreamRequestHandler<TestStreamRequest, TestSegment, TestResponse>
        {
            public async IAsyncEnumerable<TestSegment> HandleAsync(TestStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                for (int i = 0; i < 3; i++)
                {
                    await Task.Yield();
                    yield return new TestSegment { Part = $"{request.Value}-{i}" };
                }
            }
        }

        #endregion

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            var handler = new TestStreamHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.True(subscription.EnableBuffering);
            Assert.Same(handler, subscription.AsyncStreamRequestHandler);
        }

        [Fact]
        public void Constructor_WithBufferingDisabled_CreatesInstance()
        {
            // Arrange
            var handler = new TestStreamHandler();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, false, CancellationToken.None);

            // Assert
            Assert.NotNull(subscription);
            Assert.False(subscription.EnableBuffering);
            Assert.Same(handler, subscription.AsyncStreamRequestHandler);
        }

        [Fact]
        public void CancellationToken_IsSetFromConstructor()
        {
            // Arrange
            var handler = new TestStreamHandler();
            var cts = new CancellationTokenSource();

            // Act
            var subscription = new Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, cts.Token);

            // Assert
            Assert.Equal(cts.Token, subscription.CancellationToken);
        }

        [Fact]
        public async Task HandleAsync_WithRequest_DelegatesToHandlerAndYieldsSegments()
        {
            // Arrange
            var handler = new TestStreamHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, false, CancellationToken.None);
            var request = new TestStreamRequest { Value = "hello" };

            // Act
            var segments = new System.Collections.Generic.List<string>();
            await foreach (var segment in subscription.HandleAsync(request, CancellationToken.None))
            {
                segments.Add(segment.Part);
            }

            // Assert
            Assert.Equal(3, segments.Count);
            Assert.Equal("hello-0", segments[0]);
            Assert.Equal("hello-1", segments[1]);
            Assert.Equal("hello-2", segments[2]);
        }

        [Fact]
        public void Dispose_ReleasesHandler()
        {
            // Arrange
            var handler = new TestStreamHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, CancellationToken.None);

            // Act
            subscription.Dispose();

            // Assert
            Assert.Null(subscription.AsyncStreamRequestHandler);
        }

        [Fact]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var handler = new TestStreamHandler();
            var subscription = new Baubit.Mediation.Internals.AsyncStreamInterfaceSubscription<TestStreamRequest, TestSegment, TestResponse>(
                handler, true, CancellationToken.None);

            // Act & Assert
            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();

            Assert.Null(subscription.AsyncStreamRequestHandler);
        }
    }
}