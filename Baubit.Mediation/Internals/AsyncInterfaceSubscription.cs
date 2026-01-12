using Baubit.Caching;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation.Internals
{
    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This class is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this class directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Subscription implementation that wraps an asynchronous <see cref="IAsyncRequestHandler{TRequest, TResponse}"/>.
    /// Handles request/response pairs by invoking the asynchronous handler.
    /// </remarks>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IResponse"/>.</typeparam>
    public class AsyncInterfaceSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Gets the asynchronous request handler wrapped by this subscription.
        /// </summary>
        public IAsyncRequestHandler<TRequest, TResponse> AsyncHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncInterfaceSubscription{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="asyncHandler">The asynchronous handler to wrap.</param>
        /// <param name="enableBuffering">True to enable buffered request handling with tracking; false for direct handling.</param>
        public AsyncInterfaceSubscription(IAsyncRequestHandler<TRequest, TResponse> asyncHandler,
                                            bool enableBuffering) : base(enableBuffering)
        {
            AsyncHandler = asyncHandler;
        }

        /// <summary>
        /// Processes buffered tracked requests from the cache, asynchronously invoking the handler for each request
        /// and adding the tracked response back to the cache.
        /// </summary>
        /// <param name="cache">The ordered cache containing tracked requests.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache.</param>
        /// <param name="cancellationToken">Token to signal cancellation of message processing.</param>
        /// <returns>A task that completes when message processing ends, returning true on successful completion.</returns>
        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                {
                    var response = await AsyncHandler.HandleAsync(trackedRequest.Request).ConfigureAwait(false);
                    cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                }
            }
            return true;
        }

        /// <summary>
        /// Dispatches a request directly to the asynchronous handler without buffering.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        protected override async Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return await AsyncHandler.HandleAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs internal cleanup by releasing the reference to the asynchronous handler.
        /// </summary>
        protected override void DisposeInternal()
        {
            AsyncHandler = null;
        }
    }
}
