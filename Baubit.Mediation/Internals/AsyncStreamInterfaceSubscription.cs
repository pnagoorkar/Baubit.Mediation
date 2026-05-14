using System.Collections.Generic;
using System.Threading;

namespace Baubit.Mediation.Internals
{
    public class AsyncStreamInterfaceSubscription<TRequest, TSegment, TResponse> : Subscription<TRequest, TSegment, TResponse> where TRequest : IStreamRequest<TSegment, TResponse>
                                                                                  where TSegment : ISegment<TResponse>
                                                                                  where TResponse : IResponse
    {
        public IAsyncStreamRequestHandler<TRequest, TSegment, TResponse> AsyncStreamRequestHandler { get; private set; }

        public AsyncStreamInterfaceSubscription(IAsyncStreamRequestHandler<TRequest, TSegment, TResponse> asyncStreamRequestHandler, 
                                                bool enableBuffering, 
                                                CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            this.AsyncStreamRequestHandler = asyncStreamRequestHandler;
        }

        public override IAsyncEnumerable<TSegment> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return AsyncStreamRequestHandler.HandleAsync(request, cancellationToken);
        }

        protected override void DisposeInternal()
        {
            AsyncStreamRequestHandler = null;
        }
    }
}
