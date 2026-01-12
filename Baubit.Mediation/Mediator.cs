using Baubit.Caching;
using Baubit.Collections;
using Baubit.Identity;
using Baubit.Mediation.Internals;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    /// <summary>
    /// Default implementation of <see cref="IMediator"/> that routes requests to handlers
    /// and notifications to subscribers using an ordered cache for message persistence.
    /// Thread-safe for concurrent use.
    /// </summary>
    public class Mediator : IMediator, IDisposable
    {
        /// <summary>
        /// Tracks whether this instance has been disposed.
        /// </summary>
        private bool disposedValue;

        /// <summary>
        /// Dictionary mapping subscription types to their active subscription lists.
        /// Thread-safe for concurrent access.
        /// </summary>
        private readonly ConcurrentDictionary<Type, IList<ISubscription>> activeSubscriptions = new ConcurrentDictionary<Type, IList<ISubscription>>();

        /// <summary>
        /// The ordered cache used for storing and retrieving messages.
        /// </summary>
        private IOrderedCache<long, object> cache;

        /// <summary>
        /// Logger for diagnostic information.
        /// </summary>
        private ILogger<Mediator> logger;

        /// <summary>
        /// Generator for creating unique GUIDv7 identifiers for tracked requests.
        /// </summary>
        private GuidV7Generator idGenerator;

        /// <summary>
        /// Creates a new <see cref="Mediator"/> instance.
        /// </summary>
        /// <param name="cache">The ordered cache for storing notifications and tracked requests.</param>
        /// <param name="loggerFactory">Factory to create loggers for diagnostics.</param>
        public Mediator(IOrderedCache<long, object> cache,
                        ILoggerFactory loggerFactory)
        {
            this.cache = cache;
            this.logger = loggerFactory.CreateLogger<Mediator>();
            this.idGenerator = GuidV7Generator.CreateNew();
        }

        /// <inheritdoc/>
        public bool Publish<T>(T notification)
        {
            var retVal = true;
            if (!activeSubscriptions.TryGetValue(typeof(ISubscription<T>), out var subscriptions)) return true;
            foreach (var subscription in subscriptions)
            {
                if (subscription is ISubscription<T> notificationSubscription)
                {
                    retVal &= notificationSubscription.Publish(notification, cache);
                }
                else
                {
                    // Unhandled. It is expected that all subscriptions for a given T would be of type ISubscription<T>
                }
            }
            return retVal;
        }

        /// <inheritdoc/>
        public Task<bool> PublishAsync<T>(T notification, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Publish(notification));
        }

        /// <inheritdoc/>
        public async Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            if (!activeSubscriptions.TryGetValue(typeof(ISubscription<TRequest, TResponse>), out var subscriptions)) throw new InvalidOperationException("No handler registered!");
            var subscription = subscriptions.SingleOrDefault();
            if (subscription == null) throw new InvalidOperationException("No handler registered!");
            if (subscription is not ISubscription<TRequest, TResponse> requestSubscription) throw new InvalidOperationException($"Unexpected type of handler registered for {typeof(TRequest).AssemblyQualifiedName}");

            return await requestSubscription.PublishAsync(request, cache, idGenerator, null, cancellationToken).ConfigureAwait(false);
        }

        public Task<bool> SubscribeAsync<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                                       bool enableBuffering = true,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse
        {
            return SubscribeAsync(requestHandler, enableBuffering, null, cancellationToken);
        }

        public async Task<bool> SubscribeAsync<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering = true, string name = null, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            if (requestHandler == null) return false;
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(null, cancellationToken) : null;
            var subscription = new SyncInterfaceSubscription<TRequest, TResponse>(requestHandler, enableBuffering);
            var subscriptions = new List<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TRequest, TResponse>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) return false; // there is a handler already registered to handle TRequest
            try
            {
                return await subscription.RunAsync(cache, enumerator, cancellationToken).ConfigureAwait(false);
            }
            finally { activeSubscriptions.TryRemove(typeof(ISubscription<TRequest, TResponse>), out _); }
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, bool enableBuffering = true, CancellationToken cancellationToken = default)
        {
            return SubscribeAsync(subscriber, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, bool enableBuffering = true, string name = null, CancellationToken cancellationToken = default)
        {
            if (subscriber == null) return false;
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new InterfaceSubscription<T>(subscriber, enableBuffering);
            var subscriptions = new ConcurrentList<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<T>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) cachedSubscription.Add(subscription); // Another subscriber raced to create the subscriptions collection. No worries.
            try
            {
                return await subscription.RunAsync(cache, enumerator, cancellationToken).ConfigureAwait(false);
            }
            finally { cachedSubscription.Remove(subscription); }
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering = true, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsync(requestHandler, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering = true, string name = null, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            if (requestHandler == null) return false;
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new AsyncInterfaceSubscription<TRequest, TResponse>(requestHandler, enableBuffering);
            var subscriptions = new List<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TRequest, TResponse>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) return false; // there is a handler already registered to handle TRequest
            try
            {
                return await subscription.RunAsync(cache, enumerator, cancellationToken).ConfigureAwait(false);
            }
            finally { activeSubscriptions.TryRemove(typeof(ISubscription<TRequest, TResponse>), out _); }
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler, bool enableBuffering = true, CancellationToken cancellationToken = default)
        {
            return SubscribeAsync(notificationHandler, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler, bool enableBuffering = true, string name = null, CancellationToken cancellationToken = default)
        {
            if (notificationHandler == null) return false;
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new FuncSubscription<TNotification>(notificationHandler, enableBuffering);
            var subscriptions = new ConcurrentList<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TNotification>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) cachedSubscription.Add(subscription); // Another subscriber raced to create the subscriptions collection. No worries.
            try
            {
                return await subscription.RunAsync(cache, enumerator, cancellationToken).ConfigureAwait(false);
            }
            finally { cachedSubscription.Remove(subscription); }
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler, bool enableBuffering = true, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsync(asyncHandler, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler, bool enableBuffering = true, string name = null, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            if (asyncHandler == null) return false;
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new AsyncFuncSubscription<TRequest, TResponse>(asyncHandler, enableBuffering);
            var subscriptions = new List<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TRequest, TResponse>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) return false; // there is a handler already registered to handle TRequest
            try
            {
                return await subscription.RunAsync(cache, enumerator, cancellationToken).ConfigureAwait(false);
            }
            finally { cachedSubscription.Remove(subscriptions[0]); }
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cache.Dispose();
                    activeSubscriptions.Clear();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the mediator, clearing all handlers and disposing the cache.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}