using Baubit.Caching;
using Baubit.Identity;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal class AsyncInterfaceSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        public IAsyncRequestHandler<TRequest, TResponse> AsyncHandler { get; private set; }

        internal AsyncInterfaceSubscription(IAsyncRequestHandler<TRequest, TResponse> asyncHandler,
                                            bool enableBuffering) : base(enableBuffering)
        {
            AsyncHandler = asyncHandler;
        }

        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                {
                    var response = await AsyncHandler.HandleAsync(trackedRequest.Request);
                    cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                }
            }
            return true;
        }

        protected override async Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return await AsyncHandler.HandleAsync(request);
        }

        protected override void DisposeInternal()
        {
            AsyncHandler = null;
        }
    }
}
