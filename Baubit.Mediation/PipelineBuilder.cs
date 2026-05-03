using Baubit.Mediation.Internals;
using FluentResults;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Baubit.Traceability;

namespace Baubit.Mediation
{
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
        /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong><br/>
        /// Creates a new <see cref="PipelineBuilder{T}"/> instance wrapped in a <see cref="Result{T}"/>.
        /// </summary>
        /// <param name="loggerFactory">The factory used to create loggers for the pipeline.</param>
        /// <returns>A successful <see cref="Result{T}"/> containing the new builder.</returns>
        public static Result<PipelineBuilder<T>> CreateNew(ILoggerFactory loggerFactory) => new PipelineBuilder<T>(loggerFactory);

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
        /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong><br/>
        /// Builds the <see cref="IPipeline{T}"/> from the currently registered segments.
        /// </summary>
        /// <returns>A successful <see cref="Result{T}"/> containing the constructed pipeline.</returns>
        public Result<IPipeline<T>> Build()
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
