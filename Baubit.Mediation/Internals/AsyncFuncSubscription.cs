using Baubit.Caching;
using System;
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
    /// Subscription implementation that wraps a function handler for request/response pairs.
    /// Handles requests by invoking the provided asynchronous function that returns a response.
    /// </remarks>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IResponse"/>.</typeparam>
    public class AsyncFuncSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Gets the request handler function wrapped by this subscription.
        /// </summary>
        public Func<TRequest, CancellationToken, Task<TResponse>> FuncHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncFuncSubscription{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="funcHandler">The function to invoke for each request, returning a response.</param>
        /// <param name="enableBuffering">True to enable buffered request handling with tracking; false for direct handling.</param>
        internal AsyncFuncSubscription(Func<TRequest, CancellationToken, Task<TResponse>> funcHandler,
                                       bool enableBuffering) : base(enableBuffering)
        {
            FuncHandler = funcHandler;
        }

        /// <summary>
        /// Processes buffered tracked requests from the cache, invoking the handler function for each request
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
                    var response = await FuncHandler.Invoke(trackedRequest.Request, cancellationToken);
                    cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                }
            }
            return true;
        }

        /// <summary>
        /// Dispatches a request directly to the handler function without buffering.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler function.</returns>
        protected override async Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return await FuncHandler.Invoke(request, cancellationToken);
        }

        /// <summary>
        /// Performs internal cleanup by releasing the reference to the handler function.
        /// </summary>
        protected override void DisposeInternal()
        {
            FuncHandler = null;
        }
    }
}
