using Baubit.Caching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal class AsyncFuncSubscription<TRequest, TResponse> : Subscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        public Func<TRequest, CancellationToken, Task<TResponse>> FuncHandler { get; private set; }

        internal AsyncFuncSubscription(Func<TRequest, CancellationToken, Task<TResponse>> funcHandler,
                                       bool enableBuffering) : base(enableBuffering)
        {
            FuncHandler = funcHandler;
        }

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

        protected override async Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default)
        {
            return await FuncHandler.Invoke(request, cancellationToken);
        }

        protected override void DisposeInternal()
        {
            FuncHandler = null;
        }
    }
}
