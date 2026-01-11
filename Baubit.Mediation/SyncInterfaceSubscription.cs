using Baubit.Caching;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    /// <summary>
    /// Subscription implementation that wraps a synchronous <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// Handles request/response pairs by invoking the synchronous handler.
    /// </summary>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IResponse"/>.</typeparam>
    internal class SyncInterfaceSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Gets the synchronous request handler wrapped by this subscription.
        /// </summary>
        public IRequestHandler<TRequest, TResponse> SyncHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncInterfaceSubscription{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="syncHandler">The synchronous handler to wrap.</param>
        /// <param name="enableBuffering">True to enable buffered request handling with tracking; false for direct handling.</param>
        internal SyncInterfaceSubscription(IRequestHandler<TRequest, TResponse> syncHandler,
                                           bool enableBuffering) : base(enableBuffering)
        {
            SyncHandler = syncHandler;
        }

        /// <summary>
        /// Processes buffered tracked requests from the cache, invoking the synchronous handler for each request
        /// and adding the tracked response back to the cache.
        /// </summary>
        /// <param name="cache">The ordered cache containing tracked requests.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache.</param>
        /// <param name="cancellationToken">Token to signal cancellation of message processing.</param>
        /// <returns>A task that completes when message processing ends, returning true on successful completion.</returns>
        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                {
                    var response = SyncHandler.Handle(trackedRequest.Request);
                    cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                }
            }
            return true;
        }

        /// <summary>
        /// Dispatches a request directly to the synchronous handler without buffering.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation (not used for synchronous handlers).</param>
        /// <returns>A completed task containing the response from the handler.</returns>
        protected override Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SyncHandler.Handle(request));
        }

        /// <summary>
        /// Performs internal cleanup by releasing the reference to the synchronous handler.
        /// </summary>
        protected override void DisposeInternal()
        {
            SyncHandler = null;
        }
    }
}
