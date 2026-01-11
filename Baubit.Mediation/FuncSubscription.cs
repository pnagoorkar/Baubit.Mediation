using Baubit.Caching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal class FuncSubscription<T> : Subscription<T>
    {
        public Func<T, CancellationToken, Task<bool>> NotificationHandler { get; private set; }

        internal FuncSubscription(Func<T, CancellationToken, Task<bool>> notificationHandler, 
                                  bool enableBuffering) : base(enableBuffering)
        {
            NotificationHandler = notificationHandler;
        }

        protected override async Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is T notification && NotificationHandler != null)
                {
                    await NotificationHandler.Invoke(notification, cancellationToken);
                }
            }
            return true;
        }

        protected override async Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default)
        {
            if (NotificationHandler == null) return true;
            return await NotificationHandler.Invoke(notification, cancellationToken);
        }

        protected override void DisposeInternal()
        {
            NotificationHandler = null;
        }
    }
}
