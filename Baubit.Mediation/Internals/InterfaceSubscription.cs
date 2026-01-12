using Baubit.Caching;
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
    /// Subscription implementation that wraps an <see cref="ISubscriber{T}"/> for notifications of type <typeparamref name="T"/>.
    /// Handles notifications by invoking the subscriber's OnNextOrError method.
    /// </remarks>
    /// <typeparam name="T">The type of notifications this subscription handles.</typeparam>
    public class InterfaceSubscription<T> : Subscription<T>
    {
        /// <summary>
        /// Gets the subscriber wrapped by this subscription.
        /// </summary>
        public ISubscriber<T> Subscriber { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InterfaceSubscription{T}"/> class.
        /// </summary>
        /// <param name="subscriber">The subscriber to wrap.</param>
        /// <param name="enableBuffering">True to enable buffered notification delivery; false for direct delivery.</param>
        internal InterfaceSubscription(ISubscriber<T> subscriber, bool enableBuffering) : base(enableBuffering)
        {
            Subscriber = subscriber;
        }

        /// <summary>
        /// Processes buffered notifications from the cache, invoking the subscriber's OnNextOrError method for each notification.
        /// </summary>
        /// <param name="cache">The ordered cache containing notifications.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache.</param>
        /// <param name="cancellationToken">Token to signal cancellation of message processing.</param>
        /// <returns>A task that completes when message processing ends, returning true on successful completion.</returns>
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

        /// <summary>
        /// Dispatches a notification directly to the subscriber without buffering.
        /// </summary>
        /// <param name="notification">The notification to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation (not used for synchronous subscribers).</param>
        /// <returns>A completed task containing the result from the subscriber's OnNextOrError method.</returns>
        protected override Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Subscriber.OnNextOrError(notification));
        }

        /// <summary>
        /// Performs internal cleanup by releasing the reference to the subscriber.
        /// </summary>
        protected override void DisposeInternal()
        {
            Subscriber = null;
        }
    }
}
