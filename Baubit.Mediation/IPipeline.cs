using System;
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
        /// Represents the continuation function passed to each <see cref="Segment"/>.
        /// Invoking it transfers control to the next segment in the chain (or to the implicit
        /// terminal segment if there are no further segments).
        /// </summary>
        /// <param name="item">The item to pass to the next segment.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that completes with <c>true</c> when the remainder of the chain succeeds;
        /// <c>false</c> if any downstream segment short-circuits with <c>false</c>.
        /// </returns>
        public delegate Task<bool> Next(T item, CancellationToken cancellationToken = default);

        /// <summary>
        /// Represents a single middleware unit in the pipeline.
        /// Each segment receives the current item, a <see cref="Next"/> continuation for the
        /// remainder of the chain, and a cancellation token.
        /// Call <paramref name="next"/> to continue the chain, or return without calling it to short-circuit.
        /// </summary>
        /// <param name="item">The item being processed.</param>
        /// <param name="next">
        /// The continuation that invokes the next segment. Call as <c>await next(item, ct)</c>.
        /// </param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that completes with <c>true</c> if processing succeeded; <c>false</c> if it should
        /// be considered a failure.
        /// </returns>
        public delegate Task<bool> Segment(T item, Next next, CancellationToken cancellationToken = default);

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
}
