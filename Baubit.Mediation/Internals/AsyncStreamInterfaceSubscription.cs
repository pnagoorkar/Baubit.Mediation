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
    /// Subscription implementation that wraps an <see cref="IAsyncStreamRequestHandler{TRequest, TSegment, TResponse}"/>
    /// for stream request/response pairs. Handles stream requests by delegating to the provided handler interface.
    /// </remarks>
    /// <typeparam name="TRequest">The stream request type implementing <see cref="IStreamRequest{TSegment, TResponse}"/>.</typeparam>
    /// <typeparam name="TSegment">The type of each segment produced.</typeparam>
    /// <typeparam name="TResponse">The overall response type implementing <see cref="IResponse"/>.</typeparam>
    public class AsyncStreamInterfaceSubscription<TRequest, TSegment, TResponse> : Subscription<TRequest, TSegment, TResponse> where TRequest : IStreamRequest<TSegment, TResponse>
                                                                                  where TSegment : ISegment<TResponse>
                                                                                  where TResponse : IResponse
    {
        /// <summary>
        /// Gets the stream request handler wrapped by this subscription.
        /// </summary>
        public IAsyncStreamRequestHandler<TRequest, TSegment, TResponse> AsyncStreamRequestHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncStreamInterfaceSubscription{TRequest, TSegment, TResponse}"/> class.
        /// </summary>
        /// <param name="asyncStreamRequestHandler">The handler to invoke for each stream request.</param>
        /// <param name="enableBuffering">True to enable buffered stream handling with tracking; false for direct handling.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        public AsyncStreamInterfaceSubscription(IAsyncStreamRequestHandler<TRequest, TSegment, TResponse> asyncStreamRequestHandler,
                                                bool enableBuffering,
                                                CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            this.AsyncStreamRequestHandler = asyncStreamRequestHandler;
        }

        /// <summary>
        /// Dispatches a stream request directly to the handler interface without buffering.
        /// </summary>
        /// <param name="request">The stream request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>An async enumerable of segments from the handler.</returns>
        public override IAsyncEnumerable<TSegment> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return AsyncStreamRequestHandler.HandleAsync(request, cancellationToken);
        }

        /// <summary>
        /// Performs internal cleanup by releasing the reference to the handler.
        /// </summary>
        protected override void DisposeInternal()
        {
            AsyncStreamRequestHandler = null;
        }
    }
}
