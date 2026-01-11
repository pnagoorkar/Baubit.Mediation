using Baubit.Caching;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal class FuncSubscription<T> : Subscription<T>
    {
        public Func<T, CancellationToken, Task<bool>> NotificationHandler { get; private set; }

        internal FuncSubscription(Func<T, CancellationToken, Task<bool>> notificationHandler, bool enableBuffering) : base(enableBuffering)
        {
            NotificationHandler = notificationHandler;
        }

        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, string name, CancellationToken cancellationToken = default)
        {
            await using var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is T notification)
                {
                    await NotificationHandler.Invoke(notification, cancellationToken);
                }
            }
            return true;
        }

        protected override async Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default)
        {
            return await NotificationHandler.Invoke(notification, cancellationToken);
        }
    }
}
