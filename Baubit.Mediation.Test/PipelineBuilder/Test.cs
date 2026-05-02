using Microsoft.Extensions.Logging;

namespace Baubit.Mediation.Test.PipelineBuilder
{
    /// <summary>
    /// Unit tests for <see cref="Baubit.Mediation.PipelineBuilder{T}"/> exercised directly via
    /// the internal <c>CreateNew</c> and <c>Build</c> members (accessible through <c>InternalsVisibleTo</c>).
    /// </summary>
    public class Test
    {
        private static ILoggerFactory CreateLoggerFactory() => LoggerFactory.Create(_ => { });

        // -----------------------------------------------------------------------
        // CreateNew
        // -----------------------------------------------------------------------

        /// <summary>
        /// <see cref="PipelineBuilder{T}.CreateNew"/> must return a successful result containing
        /// a non-null builder instance.
        /// </summary>
        [Fact]
        public void CreateNew_ValidLoggerFactory_ReturnsSuccessfulResult()
        {
            var result = PipelineBuilder<string>.CreateNew(CreateLoggerFactory());

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
        }

        // -----------------------------------------------------------------------
        // Use — first registration
        // -----------------------------------------------------------------------

        /// <summary>
        /// Registering a segment for the first time must succeed and the built pipeline must invoke
        /// that segment when <see cref="IPipeline{T}.RunAsync"/> is called.
        /// </summary>
        [Fact]
        public async Task Use_NewSegment_SegmentIsIncludedInBuiltPipeline()
        {
            var invoked = false;
            IPipeline<string>.Segment segment = (item, next, ct) => { invoked = true; return Task.FromResult(true); };

            var builder = PipelineBuilder<string>.CreateNew(CreateLoggerFactory()).Value;
            builder.Use(segment);
            using var pipeline = builder.Build().Value;

            await pipeline.RunAsync("item");

            Assert.True(invoked);
        }

        /// <summary>
        /// <see cref="PipelineBuilder{T}.Use"/> must return a successful result containing the
        /// same builder instance, enabling method chaining.
        /// </summary>
        [Fact]
        public void Use_NewSegment_ReturnsSuccessfulResultWithSameBuilder()
        {
            IPipeline<string>.Segment segment = (item, next, ct) => Task.FromResult(true);
            var builder = PipelineBuilder<string>.CreateNew(CreateLoggerFactory()).Value;

            var result = builder.Use(segment);

            Assert.True(result.IsSuccess);
            Assert.Same(builder, result.Value);
        }

        // -----------------------------------------------------------------------
        // Use — duplicate registration
        // -----------------------------------------------------------------------

        /// <summary>
        /// Adding the same segment delegate reference twice must be a no-op: the segment should
        /// appear only once in the chain and therefore be invoked exactly once per <c>RunAsync</c> call.
        /// This covers the <c>segments.Contains(segment)</c> true-branch inside
        /// <see cref="PipelineBuilder{T}.Use"/>.
        /// </summary>
        [Fact]
        public async Task Use_DuplicateSegment_SegmentInvokedOnlyOnce()
        {
            var count = 0;
            IPipeline<string>.Segment segment = async (item, next, ct) =>
            {
                count++;
                return await next(item, ct);
            };

            var builder = PipelineBuilder<string>.CreateNew(CreateLoggerFactory()).Value;
            builder.Use(segment); // first registration — added
            builder.Use(segment); // duplicate — must be ignored
            using var pipeline = builder.Build().Value;

            await pipeline.RunAsync("item");

            Assert.Equal(1, count);
        }

        // -----------------------------------------------------------------------
        // Build
        // -----------------------------------------------------------------------

        /// <summary>
        /// <see cref="PipelineBuilder{T}.Build"/> with no registered segments must produce a
        /// working pipeline that returns <c>true</c> (via the implicit terminal segment).
        /// </summary>
        [Fact]
        public async Task Build_NoSegments_ReturnsWorkingPipelineThatReturnsTrue()
        {
            var builder = PipelineBuilder<string>.CreateNew(CreateLoggerFactory()).Value;
            using var pipeline = builder.Build().Value;

            var result = await pipeline.RunAsync("item");

            Assert.True(result);
        }

        /// <summary>
        /// <see cref="PipelineBuilder{T}.Build"/> must return a successful result containing a
        /// non-null <see cref="IPipeline{T}"/> instance.
        /// </summary>
        [Fact]
        public void Build_WithSegments_ReturnsSuccessfulResult()
        {
            var builder = PipelineBuilder<string>.CreateNew(CreateLoggerFactory()).Value;
            builder.Use((item, next, ct) => Task.FromResult(true));

            var result = builder.Build();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            result.Value.Dispose();
        }

        // -----------------------------------------------------------------------
        // Dispose
        // -----------------------------------------------------------------------

        /// <summary>
        /// After <see cref="PipelineBuilder{T}.Dispose"/> is called the builder should be in a
        /// disposed state. A second call must not throw (idempotent dispose pattern).
        /// </summary>
        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var builder = PipelineBuilder<string>.CreateNew(CreateLoggerFactory()).Value;
            builder.Use((item, next, ct) => Task.FromResult(true));

            builder.Dispose();

            var exception = Record.Exception(() => builder.Dispose());
            Assert.Null(exception);
        }
    }
}


