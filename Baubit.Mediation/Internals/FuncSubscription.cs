using System;
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
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        public FuncSubscription(Func<T, CancellationToken, Task<bool>> notificationHandler, 
                                bool enableBuffering,
                                CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            NotificationHandler = notificationHandler;
        }

        /// <inheritdoc/>
        public override bool Handle(T notification)
        {
            if (NotificationHandler == null) return true;
            return NotificationHandler.Invoke(notification, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
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
