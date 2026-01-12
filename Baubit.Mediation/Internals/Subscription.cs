using Baubit.Caching;
using Baubit.Identity;
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
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="enableBuffering">True to enable buffered message delivery; false for direct delivery.</param>
        protected Subscription(bool enableBuffering)
        {
            EnableBuffering = enableBuffering;
        }

        /// <summary>
        /// Runs the subscription asynchronously, processing messages from the cache until cancelled.
        /// If buffering is enabled, processes messages from the enumerator. Otherwise, waits indefinitely until cancellation.
        /// </summary>
        /// <param name="cache">The ordered cache containing messages.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache. Can be null if buffering is disabled.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the subscription.</param>
        /// <returns>A task that completes when the subscription ends, returning true on successful completion.</returns>
        public async Task<bool> RunAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default)
        {
            if (EnableBuffering) await ProcessBufferAsync(cache, enumerator, cancellationToken).ConfigureAwait(false);
            else
            {
                // Await indefinitely while the cancellation token is not cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }

        /// <summary>
        /// Processes buffered messages from the cache enumerator. Must be implemented by derived classes to define message handling logic.
        /// </summary>
        /// <param name="cache">The ordered cache containing messages.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache.</param>
        /// <param name="cancellationToken">Token to signal cancellation of message processing.</param>
        /// <returns>A task that completes when message processing ends, returning true on successful completion.</returns>
        protected abstract Task<bool> ProcessBufferAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default);

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
        protected Subscription(bool enableBuffering) : base(enableBuffering)
        {

        }

        /// <summary>
        /// Publishes a notification either to the cache (if buffered) or directly to the handler (if unbuffered).
        /// </summary>
        /// <param name="notification">The notification to publish.</param>
        /// <param name="cache">The ordered cache for buffered delivery.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>True if the notification was successfully published; otherwise false.</returns>
        public bool Publish(T notification, IOrderedCache<long, object> cache, CancellationToken cancellationToken = default)
        {
            if (EnableBuffering) return cache.Add(notification, out _);
            else return DispatchAsync(notification).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Dispatches a notification directly to the handler without buffering. Must be implemented by derived classes.
        /// </summary>
        /// <param name="notification">The notification to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes when the notification is handled, returning true on success.</returns>
        protected abstract Task<bool> DispatchAsync(T notification, CancellationToken cancellationToken = default);
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
        protected Subscription(bool enableBuffering) : base(enableBuffering)
        {

        }

        /// <summary>
        /// Publishes a request asynchronously and awaits the response.
        /// If buffering is enabled, tracks the request in the cache and waits for the tracked response.
        /// If buffering is disabled, dispatches the request directly to the handler.
        /// </summary>
        /// <param name="request">The request to publish.</param>
        /// <param name="cache">The ordered cache for tracked request/response pairs.</param>
        /// <param name="identityGenerator">Generator for creating unique request identifiers.</param>
        /// <param name="name">Optional name for the cache enumerator.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        /// <exception cref="TaskCanceledException">Thrown if the operation is cancelled before a response is received.</exception>
        public async Task<TResponse> PublishAsync(TRequest request, IOrderedCache<long, object> cache, GuidV7Generator identityGenerator, string name = null, CancellationToken cancellationToken = default)
        {
            if (EnableBuffering)
            {
                var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
                var trackedRequest = new TrackedRequest<TRequest, TResponse>(identityGenerator.GetNext(), request);
                cache.Add(trackedRequest, out var entry);
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (enumerator.Current.Value is TrackedResponse<TResponse> trackedResponse && trackedResponse.ForRequest == trackedRequest.Id)
                    {
                        return trackedResponse.Response;
                    }
                }
                // Enumerator completed without finding a response (cancellation or unexpected cache issue)
                throw new TaskCanceledException("Response not received before enumeration ended");
            }
            else
            {
                return await DispatchAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Dispatches a request directly to the handler without buffering. Must be implemented by derived classes.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        protected abstract Task<TResponse> DispatchAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}
