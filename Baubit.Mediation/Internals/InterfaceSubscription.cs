using System.Threading;

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
        public InterfaceSubscription(ISubscriber<T> subscriber, bool enableBuffering, CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {
            Subscriber = subscriber;
        }

        public override bool Handle(T notification)
        {
            return Subscriber.OnNextOrError(notification);
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
