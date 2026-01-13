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
        public AsyncFuncSubscription(Func<TRequest, CancellationToken, Task<TResponse>> funcHandler,
                                     bool enableBuffering,
                                     CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            FuncHandler = funcHandler;
        }

        /// <summary>
        /// Dispatches a request directly to the handler function without buffering.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler function.</returns>
        public override async Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return await FuncHandler.Invoke(request, cancellationToken).ConfigureAwait(false);
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
