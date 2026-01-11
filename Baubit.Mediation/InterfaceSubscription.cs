using Baubit.Caching;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal class InterfaceSubscription<T> : Subscription<T>
    {
        public ISubscriber<T> Subscriber { get; private set; }
        internal InterfaceSubscription(ISubscriber<T> subscriber, bool enableBuffering) : base(enableBuffering)
        {
            Subscriber = subscriber;
        }

        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is T notification)
                {
                    Subscriber.OnNextOrError(notification);
                }
            }
            return true;
        }

        protected override Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Subscriber.OnNextOrError(notification));
        }

        protected override void DisposeInternal()
        {
            Subscriber = null;
        }
    }
}
