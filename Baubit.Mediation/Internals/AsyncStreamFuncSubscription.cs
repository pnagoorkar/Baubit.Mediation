using System;
using System.Collections.Generic;
using System.Threading;

namespace Baubit.Mediation.Internals
{
    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This class is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this class directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Subscription implementation that wraps a function handler for stream request/response pairs.
    /// Handles stream requests by invoking the provided function that returns an async enumerable of segments.
    /// </remarks>
    /// <typeparam name="TRequest">The stream request type implementing <see cref="IStreamRequest{TSegment, TResponse}"/>.</typeparam>
    /// <typeparam name="TSegment">The type of each segment produced.</typeparam>
    /// <typeparam name="TResponse">The overall response type implementing <see cref="IResponse"/>.</typeparam>
    public class AsyncStreamFuncSubscription<TRequest, TSegment, TResponse> : Subscription<TRequest, TSegment, TResponse>
        where TRequest : IStreamRequest<TSegment, TResponse>
        where TSegment : ISegment<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Gets the stream handler function wrapped by this subscription.
        /// </summary>
        public Func<TRequest, CancellationToken, IAsyncEnumerable<TSegment>> FuncHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncStreamFuncSubscription{TRequest, TSegment, TResponse}"/> class.
        /// </summary>
        /// <param name="funcHandler">The function to invoke for each stream request, returning an async enumerable of segments.</param>
        /// <param name="enableBuffering">True to enable buffered stream handling with tracking; false for direct handling.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        public AsyncStreamFuncSubscription(Func<TRequest, CancellationToken, IAsyncEnumerable<TSegment>> funcHandler,
                                           bool enableBuffering,
                                           CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            FuncHandler = funcHandler;
        }

        /// <summary>
        /// Dispatches a stream request directly to the handler function without buffering.
        /// </summary>
        /// <param name="request">The stream request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>An async enumerable of segments from the handler function.</returns>
        public override IAsyncEnumerable<TSegment> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return FuncHandler.Invoke(request, cancellationToken);
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