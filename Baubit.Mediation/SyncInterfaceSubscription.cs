using Baubit.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal class SyncInterfaceSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        public IRequestHandler<TRequest, TResponse> SyncHandler { get; private set; }

        internal SyncInterfaceSubscription(IRequestHandler<TRequest, TResponse> syncHandler,
                                     bool enableBuffering) : base(enableBuffering)
        {
            SyncHandler = syncHandler;
        }

        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, string name, CancellationToken cancellationToken = default)
        {
            await using var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
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

        protected override Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SyncHandler.Handle(request));
        }
    }
}
