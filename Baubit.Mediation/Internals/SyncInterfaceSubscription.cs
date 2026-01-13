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
    /// Subscription implementation that wraps a synchronous <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// Handles request/response pairs by invoking the synchronous handler.
    /// </remarks>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IResponse"/>.</typeparam>
    public class SyncInterfaceSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
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
        public SyncInterfaceSubscription(IRequestHandler<TRequest, TResponse> syncHandler,
                                         bool enableBuffering,
                                         CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            SyncHandler = syncHandler;
        }

        /// <summary>
        /// Dispatches a request directly to the synchronous handler without buffering.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation (not used for synchronous handlers).</param>
        /// <returns>A completed task containing the response from the handler.</returns>
        public override Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
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
