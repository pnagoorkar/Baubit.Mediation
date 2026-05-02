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
    public interface IPipeline<T> : IDisposable
    {
        public delegate Task<bool> Segment(T item, Segment next, CancellationToken cancellationToken = default);
        Task<bool> RunAsync(T input, CancellationToken cancellationToken = default);
    }

    internal sealed class Pipeline<T> : IPipeline<T>
    {
        private bool disposedValue;
        private List<IPipeline<T>.Segment> segments;
        private ILogger<Pipeline<T>> logger;
        private IPipeline<T>.Segment firstSegment;

        private static IPipeline<T>.Segment lastSegment = (_, _, _) => Task.FromResult(true);

        internal Pipeline(IEnumerable<IPipeline<T>.Segment> segments,
                          ILogger<Pipeline<T>> logger)
        {
            this.segments = segments.ToList();
            this.logger = logger;
            firstSegment = LinkSegments(this.segments);
        }

        private static IPipeline<T>.Segment LinkSegments(List<IPipeline<T>.Segment> segments)
        {
            var next = lastSegment;

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var currentSegment = segments[i];
                next = (evt, n, ct) => currentSegment(evt, next, ct);
            }

            return next;
        }

        public Task<bool> RunAsync(T input, CancellationToken cancellationToken = default) => firstSegment(input, null, cancellationToken);

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

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class PipelineBuilder<T> : IDisposable
    {
        private bool disposedValue;
        private List<IPipeline<T>.Segment> segments = new List<IPipeline<T>.Segment>();
        private ILoggerFactory loggerFactory;

        private PipelineBuilder(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
        }

        internal static Result<PipelineBuilder<T>> CreateNew(ILoggerFactory loggerFactory) => new PipelineBuilder<T>(loggerFactory);

        public Result<PipelineBuilder<T>> Use(IPipeline<T>.Segment segment)
        {
            if (!segments.Contains(segment))
            {
                segments.Add(segment);
            }
            return this;
        }

        internal Result<IPipeline<T>> Build()
        {
            return new Pipeline<T>(segments, loggerFactory.CreateLogger<Pipeline<T>>());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    segments?.Clear();
                    segments = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public static class PipelineBuilderExtensions
    {
        public static Result<PipelineBuilder<T>> Use<T>(this Result<PipelineBuilder<T>> wrappedBuilder, IPipeline<T>.Segment segment)
        {
            return wrappedBuilder.Bind(pb => pb.Use(segment));
        }

        public static Result<PipelineBuilder<T>> WithBuildAction<T>(this Result<PipelineBuilder<T>> wrappedBuilder, Action<PipelineBuilder<T>> pipelineBuildAction)
        {
            return wrappedBuilder.Bind(pb => { pipelineBuildAction(pb); return Result.Ok(pb); });
        }

        public static IPipeline<T> Build<T>(this Result<PipelineBuilder<T>> wrappedBuilder)
        {
            var buildResult = wrappedBuilder.Bind(pb => pb.Build());
            buildResult.ThrowIfFailed();
            return buildResult.Value;
        }
    }
}
