using Baubit.Traceability;
using FluentResults;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    /// <summary>
    /// Represents a middleware pipeline that processes items of type <typeparamref name="T"/> through
    /// a chain of segments. Each segment may transform, filter, or short-circuit the chain.
    /// </summary>
    /// <typeparam name="T">The type of item flowing through the pipeline.</typeparam>
    public interface IPipeline<T> : IDisposable
    {
        /// <summary>
        /// Represents a single middleware unit in the pipeline.
        /// Each segment receives the current item, a reference to the next segment in the chain,
        /// and a cancellation token. It may call <paramref name="next"/> to continue the chain,
        /// or return without calling it to short-circuit.
        /// </summary>
        /// <param name="item">The item being processed.</param>
        /// <param name="next">The next segment in the chain. <c>null</c> when passed in from the terminal call.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that completes with <c>true</c> if processing succeeded; <c>false</c> if it should
        /// be considered a failure.
        /// </returns>
        public delegate Task<bool> Segment(T item, Segment next, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs the pipeline against <paramref name="input"/>, invoking each segment in order.
        /// </summary>
        /// <param name="input">The item to process.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that completes with <c>true</c> when all segments (and the terminal segment) succeed;
        /// <c>false</c> if any segment returns <c>false</c>.
        /// </returns>
        Task<bool> RunAsync(T input, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Internal implementation of <see cref="IPipeline{T}"/> that links an ordered list of
    /// <see cref="IPipeline{T}.Segment"/> delegates into a single invocation chain.
    /// </summary>
    /// <typeparam name="T">The type of item flowing through the pipeline.</typeparam>
    internal sealed class Pipeline<T> : IPipeline<T>
    {
        private bool disposedValue;
        private List<IPipeline<T>.Segment> segments;
        private ILogger<Pipeline<T>> logger;
        private IPipeline<T>.Segment firstSegment;

        /// <summary>
        /// The terminal segment appended automatically at the end of every chain.
        /// Always returns <c>true</c> and acts as the no-op tail of the middleware chain.
        /// </summary>
        private static IPipeline<T>.Segment lastSegment = (_, _, _) => Task.FromResult(true);

        /// <summary>
        /// Initializes a new <see cref="Pipeline{T}"/> by linking the provided segments and
        /// caching the resulting chain as <see cref="firstSegment"/>.
        /// </summary>
        /// <param name="segments">The ordered list of middleware segments to link.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        internal Pipeline(IEnumerable<IPipeline<T>.Segment> segments,
                          ILogger<Pipeline<T>> logger)
        {
            this.segments = segments.ToList();
            this.logger = logger;
            firstSegment = LinkSegments(this.segments);
        }

        /// <summary>
        /// Builds the invocation chain by walking the segment list in reverse and composing
        /// each segment with the accumulated tail. A local <c>capturedNext</c> variable is used
        /// inside each closure to avoid the classic C# loop-variable capture bug.
        /// </summary>
        /// <param name="segments">The ordered list of segments to chain.</param>
        /// <returns>
        /// The first segment of the composed chain. If <paramref name="segments"/> is empty,
        /// <see cref="lastSegment"/> is returned directly.
        /// </returns>
        private static IPipeline<T>.Segment LinkSegments(List<IPipeline<T>.Segment> segments)
        {
            var next = lastSegment;

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var currentSegment = segments[i];
                var capturedNext = next;
                next = (evt, n, ct) => currentSegment(evt, capturedNext, ct);
            }

            return next;
        }

        /// <inheritdoc/>
        public Task<bool> RunAsync(T input, CancellationToken cancellationToken = default) => firstSegment(input, null, cancellationToken);

        /// <summary>
        /// Releases managed resources used by this pipeline.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    segments?.Clear();
                    segments = null;
                    firstSegment = null;
                }

                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Fluent builder for constructing an <see cref="IPipeline{T}"/>.
    /// Segments are added in declaration order and executed in that same order at runtime.
    /// Each segment must be unique; adding the same delegate reference more than once is a no-op.
    /// </summary>
    /// <typeparam name="T">The type of item that will flow through the pipeline.</typeparam>
    public class PipelineBuilder<T> : IDisposable
    {
        private bool disposedValue;
        private List<IPipeline<T>.Segment> segments = new List<IPipeline<T>.Segment>();
        private ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes a new <see cref="PipelineBuilder{T}"/> with the given logger factory.
        /// Use <see cref="CreateNew"/> to obtain an instance.
        /// </summary>
        /// <param name="loggerFactory">The factory used to create loggers for the pipeline.</param>
        private PipelineBuilder(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Creates a new <see cref="PipelineBuilder{T}"/> instance wrapped in a <see cref="Result{T}"/>.
        /// </summary>
        /// <param name="loggerFactory">The factory used to create loggers for the pipeline.</param>
        /// <returns>A successful <see cref="Result{T}"/> containing the new builder.</returns>
        internal static Result<PipelineBuilder<T>> CreateNew(ILoggerFactory loggerFactory) => new PipelineBuilder<T>(loggerFactory);

        /// <summary>
        /// Registers a middleware <see cref="IPipeline{T}.Segment"/> with this builder.
        /// If the same delegate reference has already been registered, this call is a no-op.
        /// </summary>
        /// <param name="segment">The segment to register.</param>
        /// <returns>A successful <see cref="Result{T}"/> containing this builder, enabling method chaining.</returns>
        public Result<PipelineBuilder<T>> Use(IPipeline<T>.Segment segment)
        {
            if (!segments.Contains(segment))
            {
                segments.Add(segment);
            }
            return this;
        }

        /// <summary>
        /// Builds the <see cref="IPipeline{T}"/> from the currently registered segments.
        /// </summary>
        /// <returns>A successful <see cref="Result{T}"/> containing the constructed pipeline.</returns>
        internal Result<IPipeline<T>> Build()
        {
            return new Pipeline<T>(segments, loggerFactory.CreateLogger<Pipeline<T>>());
        }

        /// <summary>
        /// Releases managed resources used by this builder.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    segments?.Clear();
                    segments = null;
                }

                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Extension methods for <see cref="Result{T}"/>-wrapped <see cref="PipelineBuilder{T}"/> instances,
    /// enabling a fluent, railway-oriented construction of pipelines.
    /// </summary>
    public static class PipelineBuilderExtensions
    {
        /// <summary>
        /// Registers a middleware segment on the wrapped builder.
        /// If the builder result is already failed, the failure is propagated without registering the segment.
        /// </summary>
        /// <typeparam name="T">The pipeline item type.</typeparam>
        /// <param name="wrappedBuilder">The result-wrapped builder.</param>
        /// <param name="segment">The middleware segment to register.</param>
        /// <returns>A result containing the builder, or the original failure.</returns>
        public static Result<PipelineBuilder<T>> Use<T>(this Result<PipelineBuilder<T>> wrappedBuilder, IPipeline<T>.Segment segment)
        {
            return wrappedBuilder.Bind(pb => pb.Use(segment));
        }

        /// <summary>
        /// Applies an <see cref="Action{T}"/> to the wrapped builder, allowing imperative configuration
        /// (such as calling <see cref="PipelineBuilder{T}.Use"/> multiple times) inside a fluent chain.
        /// If the builder result is already failed, the failure is propagated without invoking the action.
        /// </summary>
        /// <typeparam name="T">The pipeline item type.</typeparam>
        /// <param name="wrappedBuilder">The result-wrapped builder.</param>
        /// <param name="pipelineBuildAction">An action that receives the builder and may call any of its methods.</param>
        /// <returns>A result containing the builder after the action completes, or the original failure.</returns>
        public static Result<PipelineBuilder<T>> WithBuildAction<T>(this Result<PipelineBuilder<T>> wrappedBuilder, Action<PipelineBuilder<T>> pipelineBuildAction)
        {
            return wrappedBuilder.Bind(pb => { pipelineBuildAction(pb); return Result.Ok(pb); });
        }

        /// <summary>
        /// Finalises the pipeline by calling <see cref="PipelineBuilder{T}.Build()"/> on the wrapped builder.
        /// Throws if the result is failed.
        /// </summary>
        /// <typeparam name="T">The pipeline item type.</typeparam>
        /// <param name="wrappedBuilder">The result-wrapped builder.</param>
        /// <returns>The constructed <see cref="IPipeline{T}"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the builder result is in a failed state.</exception>
        public static IPipeline<T> Build<T>(this Result<PipelineBuilder<T>> wrappedBuilder)
        {
            var buildResult = wrappedBuilder.Bind(pb => pb.Build());
            buildResult.ThrowIfFailed();
            return buildResult.Value;
        }
    }
}
