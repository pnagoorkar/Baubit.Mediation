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
    /// Base class for all subscription implementations. Manages the lifecycle of a subscription
    /// including buffered and unbuffered message processing.
    /// </remarks>
    public abstract class Subscription : ISubscription
    {
        /// <summary>
        /// Tracks whether this instance has been disposed.
        /// </summary>
        private bool disposedValue;

        /// <summary>
        /// Gets a value indicating whether this subscription uses buffered message delivery.
        /// When true, messages are queued in the cache before delivery. When false, messages are delivered directly.
        /// </summary>
        public bool EnableBuffering { get; private set; }
        
        /// <summary>
        /// Gets the cancellation token to monitor for cancellation requests.
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="enableBuffering">True to enable buffered message delivery; false for direct delivery.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        protected Subscription(bool enableBuffering, 
                               CancellationToken cancellationToken)
        {
            EnableBuffering = enableBuffering;
            CancellationToken = cancellationToken;
        }
        /// <summary>
        /// Performs internal cleanup of subscription-specific resources. Must be implemented by derived classes.
        /// </summary>
        protected abstract void DisposeInternal();

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This class is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this class directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Base class for subscriptions that handle notifications of type <typeparamref name="T"/>.
    /// Supports both buffered and unbuffered notification delivery.
    /// </remarks>
    /// <typeparam name="T">The type of notifications this subscription handles.</typeparam>
    public abstract class Subscription<T> : Subscription, ISubscription<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription{T}"/> class.
        /// </summary>
        /// <param name="enableBuffering">True to enable buffered notification delivery; false for direct delivery.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        protected Subscription(bool enableBuffering,
                               CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {

        }

        /// <inheritdoc/>
        public abstract bool Handle(T notification, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This class is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this class directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Base class for subscriptions that handle request/response pairs of types <typeparamref name="TRequest"/> and <typeparamref name="TResponse"/>.
    /// Supports both buffered (tracked) and unbuffered (direct) request handling.
    /// </remarks>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IResponse"/>.</typeparam>
    public abstract class Subscription<TRequest, TResponse> : Subscription, ISubscription<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="enableBuffering">True to enable buffered request handling with tracking; false for direct handling.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        protected Subscription(bool enableBuffering,
                               CancellationToken cancellationToken) : base(enableBuffering, cancellationToken)
        {

        }

        ///<inheritdoc/>
        public abstract Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}
