using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation.Internals
{
    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong><br/>
    /// Internal implementation of <see cref="IPipeline{T}"/> that links an ordered list of
    /// <see cref="IPipeline{T}.Segment"/> delegates into a single invocation chain.
    /// </summary>
    /// <typeparam name="T">The type of item flowing through the pipeline.</typeparam>
    public sealed class Pipeline<T> : IPipeline<T>
    {
        private bool disposedValue;
        private List<IPipeline<T>.Segment> segments;
        private ILogger<Pipeline<T>> logger;
        private IPipeline<T>.Next firstNext;

        /// <summary>
        /// The terminal <see cref="IPipeline{T}.Next"/> appended automatically at the end of every chain.
        /// Always returns <c>true</c> and acts as the no-op tail of the middleware chain.
        /// </summary>
        private static readonly IPipeline<T>.Next lastNext = (_, _) => Task.FromResult(true);

        /// <summary>
        /// Initializes a new <see cref="Pipeline{T}"/> by linking the provided segments and
        /// caching the resulting chain entry point as <see cref="firstNext"/>.
        /// </summary>
        /// <param name="segments">The ordered list of middleware segments to link.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        internal Pipeline(IEnumerable<IPipeline<T>.Segment> segments,
                          ILogger<Pipeline<T>> logger)
        {
            this.segments = segments.ToList();
            this.logger = logger;
            firstNext = LinkSegments(this.segments);
        }

        /// <summary>
        /// Builds the invocation chain by walking the segment list in reverse and composing
        /// each segment with the accumulated <see cref="IPipeline{T}.Next"/> tail.
        /// A local <c>capturedNext</c> variable is used inside each closure to avoid the classic
        /// C# loop-variable capture bug.
        /// </summary>
        /// <param name="segments">The ordered list of segments to chain.</param>
        /// <returns>
        /// The entry-point <see cref="IPipeline{T}.Next"/> of the composed chain.
        /// If <paramref name="segments"/> is empty, <see cref="lastNext"/> is returned directly.
        /// </returns>
        private static IPipeline<T>.Next LinkSegments(List<IPipeline<T>.Segment> segments)
        {
            var next = lastNext;

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var currentSegment = segments[i];
                var capturedNext = next;
                next = (item, ct) => currentSegment(item, capturedNext, ct);
            }

            return next;
        }

        /// <inheritdoc/>
        public Task<bool> RunAsync(T input, CancellationToken cancellationToken = default) => firstNext(input, cancellationToken);

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
                    firstNext = null;
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
}
