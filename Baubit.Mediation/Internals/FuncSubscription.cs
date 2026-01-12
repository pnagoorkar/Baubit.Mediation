using Baubit.Caching;
using System;
using System.Collections.Generic;
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
    /// Subscription implementation that wraps a function handler for notifications of type <typeparamref name="T"/>.
    /// Handles notifications by invoking the provided asynchronous function.
    /// </remarks>
    /// <typeparam name="T">The type of notifications this subscription handles.</typeparam>
    public class FuncSubscription<T> : Subscription<T>
    {
        /// <summary>
        /// Gets the notification handler function wrapped by this subscription.
        /// </summary>
        public Func<T, CancellationToken, Task<bool>> NotificationHandler { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuncSubscription{T}"/> class.
        /// </summary>
        /// <param name="notificationHandler">The function to invoke for each notification.</param>
        /// <param name="enableBuffering">True to enable buffered notification delivery; false for direct delivery.</param>
        internal FuncSubscription(Func<T, CancellationToken, Task<bool>> notificationHandler, 
                                  bool enableBuffering) : base(enableBuffering)
        {
            NotificationHandler = notificationHandler;
        }

        /// <summary>
        /// Processes buffered notifications from the cache, invoking the handler function for each notification.
        /// Null handlers are safely ignored.
        /// </summary>
        /// <param name="cache">The ordered cache containing notifications.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache.</param>
        /// <param name="cancellationToken">Token to signal cancellation of message processing.</param>
        /// <returns>A task that completes when message processing ends, returning true on successful completion.</returns>
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

        /// <summary>
        /// Dispatches a notification directly to the handler function without buffering.
        /// Returns true if the handler is null (no-op case).
        /// </summary>
        /// <param name="notification">The notification to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes when the notification is handled, returning the result from the handler or true if handler is null.</returns>
        protected override async Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default)
        {
            if (NotificationHandler == null) return true;
            return await NotificationHandler.Invoke(notification, cancellationToken);
        }

        /// <summary>
        /// Performs internal cleanup by releasing the reference to the notification handler function.
        /// </summary>
        protected override void DisposeInternal()
        {
            NotificationHandler = null;
        }
    }
}
