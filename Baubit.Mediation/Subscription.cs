using Baubit.Caching;
using Baubit.Identity;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal abstract class Subscription : ISubscription
    {
        private bool disposedValue;

        public bool EnableBuffering { get; private set; }

        protected Subscription(bool enableBuffering)
        {
            EnableBuffering = enableBuffering;
        }

        public async Task<bool> RunAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            if (EnableBuffering) await ProcessBufferAsync(cache, enumerator, cancellationToken);
            else
            {
                // Await indefinitely while the cancellation token is not cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }

        protected abstract Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default);

        protected abstract void DisposeInternal();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisposeInternal();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
    }

    internal abstract class Subscription<T> : Subscription, ISubscription<T>
    {
        protected Subscription(bool enableBuffering) : base(enableBuffering)
        {

        }

        public bool Publish(T notification, IOrderedCache<long, object> cache, CancellationToken cancellationToken = default)
        {
            if (EnableBuffering) return cache.Add(notification, out _);
            else return DispatchAsync(notification).GetAwaiter().GetResult();
        }

        protected abstract Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default);
    }

    internal abstract class Subscription<TRequest, TResponse> : Subscription, ISubscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        protected Subscription(bool enableBuffering) : base(enableBuffering)
        {

        }

        public async Task<TResponse> PublishAsync(TRequest request, IOrderedCache<long, object> cache, GuidV7Generator identityGenerator, string name = null, CancellationToken cancellationToken = default)
        {
            if (EnableBuffering)
            {
                var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
                var trackedRequest = new TrackedRequest<TRequest, TResponse>(identityGenerator.GetNext(), request);
                cache.Add(trackedRequest, out var entry);
                while (await enumerator.MoveNextAsync())
                {
                    if (enumerator.Current.Value is TrackedResponse<TResponse> trackedResponse && trackedResponse.ForRequest == trackedRequest.Id)
                    {
                        return trackedResponse.Response;
                    }
                }
                throw new TaskCanceledException(); // This should never get executed
            }
            else
            {
                return await DispatchAsync(request, cancellationToken);
            }
        }
        protected abstract Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}
