using Microsoft.Extensions.Logging;

namespace Baubit.Mediation.Test.Pipeline
{
    /// <summary>
    /// Unit tests for <see cref="Baubit.Mediation.Pipeline{T}"/> exercised directly via
    /// <see cref="PipelineBuilder{T}"/> (accessible through <c>InternalsVisibleTo</c>).
    /// </summary>
    public class Test
    {
        private static ILoggerFactory CreateLoggerFactory() => LoggerFactory.Create(_ => { });

        /// <summary>
        /// Builds a pipeline using the internal <see cref="PipelineBuilder{T}.CreateNew"/> factory and
        /// the internal <see cref="PipelineBuilder{T}.Build"/> method, applying <paramref name="configure"/>
        /// to register segments before building.
        /// </summary>
        private static IPipeline<T> BuildPipeline<T>(Action<PipelineBuilder<T>> configure)
        {
            var builder = PipelineBuilder<T>.CreateNew(CreateLoggerFactory()).Value;
            configure(builder);
            return builder.Build().Value;
        }

        // -----------------------------------------------------------------------
        // RunAsync — no segments
        // -----------------------------------------------------------------------

        /// <summary>
        /// A pipeline with no registered segments should use the implicit terminal segment and
        /// return <c>true</c>.  This covers the <c>LinkSegments</c> for-loop zero-iteration path
        /// and the terminal-segment lambda body.
        /// </summary>
        [Fact]
        public async Task RunAsync_NoSegments_ReturnsTrueFromTerminalSegment()
        {
            using var pipeline = BuildPipeline<string>(_ => { });

            var result = await pipeline.RunAsync("item");

            Assert.True(result);
        }

        // -----------------------------------------------------------------------
        // RunAsync — one segment
        // -----------------------------------------------------------------------

        /// <summary>
        /// A single segment should receive the input item and, after calling <c>next</c>,
        /// the terminal segment's <c>true</c> result should propagate back.
        /// This covers the <c>LinkSegments</c> single-iteration path and the closure body.
        /// </summary>
        [Fact]
        public async Task RunAsync_OneSegment_SegmentReceivesItemAndChainReturnsTrue()
        {
            var received = new List<string>();

            using var pipeline = BuildPipeline<string>(pb =>
            {
                pb.Use(async (item, next, ct) =>
                {
                    received.Add(item);
                    return await next(item, ct);
                });
            });

            var result = await pipeline.RunAsync("hello");

            Assert.True(result);
            Assert.Single(received);
            Assert.Equal("hello", received[0]);
        }

        /// <summary>
        /// When a segment short-circuits by returning <c>false</c> without calling <c>next</c>,
        /// <see cref="IPipeline{T}.RunAsync"/> should return <c>false</c>.
        /// </summary>
        [Fact]
        public async Task RunAsync_SegmentReturnsFalse_PipelineReturnsFalse()
        {
            using var pipeline = BuildPipeline<string>(pb =>
            {
                pb.Use((item, next, ct) => Task.FromResult(false));
            });

            var result = await pipeline.RunAsync("item");

            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // RunAsync — multiple segments
        // -----------------------------------------------------------------------

        /// <summary>
        /// Multiple segments must execute in declaration order (first registered → last registered)
        /// and each must receive the original input item.
        /// This covers every iteration of the <c>LinkSegments</c> for-loop.
        /// </summary>
        [Fact]
        public async Task RunAsync_MultipleSegments_ExecuteInDeclarationOrder()
        {
            var order = new List<int>();

            using var pipeline = BuildPipeline<string>(pb =>
            {
                pb.Use(async (item, next, ct) => { order.Add(1); return await next(item, ct); });
                pb.Use(async (item, next, ct) => { order.Add(2); return await next(item, ct); });
                pb.Use(async (item, next, ct) => { order.Add(3); return await next(item, ct); });
            });

            var result = await pipeline.RunAsync("item");

            Assert.True(result);
            Assert.Equal(new[] { 1, 2, 3 }, order);
        }

        /// <summary>
        /// When an intermediate segment short-circuits (returns <c>false</c> without calling <c>next</c>),
        /// subsequent segments must not be invoked and the pipeline must return <c>false</c>.
        /// </summary>
        [Fact]
        public async Task RunAsync_MiddleSegmentShortCircuits_LaterSegmentsNotInvoked()
        {
            var invoked = new List<int>();

            using var pipeline = BuildPipeline<string>(pb =>
            {
                pb.Use(async (item, next, ct) => { invoked.Add(1); return await next(item, ct); });
                pb.Use((item, next, ct) => { invoked.Add(2); return Task.FromResult(false); });
                pb.Use(async (item, next, ct) => { invoked.Add(3); return await next(item, ct); });
            });

            var result = await pipeline.RunAsync("item");

            Assert.False(result);
            Assert.Equal(new[] { 1, 2 }, invoked);
        }

        // -----------------------------------------------------------------------
        // RunAsync — cancellation token
        // -----------------------------------------------------------------------

        /// <summary>
        /// The cancellation token passed to <see cref="IPipeline{T}.RunAsync"/> must be forwarded
        /// to each segment so that segments can observe cancellation.
        /// </summary>
        [Fact]
        public async Task RunAsync_CancellationToken_IsForwardedToSegments()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken? captured = null;

            using var pipeline = BuildPipeline<string>(pb =>
            {
                pb.Use((item, next, ct) =>
                {
                    captured = ct;
                    return Task.FromResult(true);
                });
            });

            await pipeline.RunAsync("item", cts.Token);

            Assert.Equal(cts.Token, captured);
        }

        // -----------------------------------------------------------------------
        // Dispose
        // -----------------------------------------------------------------------

        /// <summary>
        /// After <see cref="IPipeline{T}.Dispose"/> is called, the pipeline should be in a disposed
        /// state.  A second call to <c>Dispose</c> must not throw (idempotent dispose pattern).
        /// </summary>
        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var pipeline = BuildPipeline<string>(pb =>
            {
                pb.Use((item, next, ct) => Task.FromResult(true));
            });

            pipeline.Dispose();

            var exception = Record.Exception(() => pipeline.Dispose());
            Assert.Null(exception);
        }
    }
}
