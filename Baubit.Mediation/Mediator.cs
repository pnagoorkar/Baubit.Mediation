using Baubit.Caching;
using Baubit.Collections;
using Baubit.Identity;
using Baubit.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    /// <summary>
    /// Default implementation of <see cref="IMediator"/> that routes requests to handlers
    /// and notifications to subscribers using an ordered cache for message persistence.
    /// Thread-safe for concurrent use.
    /// </summary>
    public class Mediator : IMediator
    {
        private bool disposedValue;
        private readonly ConcurrentDictionary<Type, IRequestHandler> syncHandlersByType = new ConcurrentDictionary<Type, IRequestHandler>();
        private readonly ConcurrentDictionary<Type, IList<(ISubscriber, bool)>> subscribersByType = new ConcurrentDictionary<Type, IList<(ISubscriber, bool)>>();
        private readonly ConcurrentDictionary<Type, IList<(Delegate, bool)>> funcSubscribersByType = new ConcurrentDictionary<Type, IList<(Delegate, bool)>>();
        private readonly ConcurrentDictionary<Type, object> requestHandlersByRequestType = new ConcurrentDictionary<Type, object>();
        private IOrderedCache<long, object> cache;
        private ILogger<Mediator> logger;
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
            if (subscribersByType.TryGetValue(typeof(ISubscriber<T>), out var subscriptions))
            {
                foreach (var subBufPair in subscriptions)
                {
                    if (subBufPair.Item2)
                    {
                        retVal &= cache.Add(notification, out _);
                    }
                    else
                    {
                        retVal &= ((ISubscriber<T>)subBufPair.Item1).OnNextOrError(notification);
                    }
                }
            }

            // Also handle Func-based subscribers
            if (funcSubscribersByType.TryGetValue(typeof(T), out var funcSubscriptions))
            {
                foreach (var funcBufPair in funcSubscriptions)
                {
                    if (funcBufPair.Item2)
                    {
                        retVal &= cache.Add(notification, out _);
                    }
                    // Direct delivery not supported for Func handlers (they always use buffering)
                }
            }
            return retVal;
        }

        /// <inheritdoc/>
        public Task<bool> PublishAsync<T>(T notification, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Publish(notification), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, string name = null, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);

            // Check if there's a synchronous handler registered
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            if (syncHandlersByType.TryGetValue(handlerType, out var syncHandler))
            {
                return await Task.Run(() => ((IRequestHandler<TRequest, TResponse>)syncHandler).Handle(request), cancellationToken).ConfigureAwait(false);
            }

            // Check if there's an async handler registered (IAsyncRequestHandler or Func handler)
            if (requestHandlersByRequestType.ContainsKey(requestType))
            {
                // Use the cache-based async pattern for async handlers
                var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var enumerator = cache.GetFutureAsyncEnumerator(name, linkedCTS.Token);
                var trackedRequest = new TrackedRequest<TRequest, TResponse>(idGenerator.GetNext(), request);
                if (!cache.Add(trackedRequest, out _)) throw new InvalidOperationException("Failed to add request to cache.");
                try
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        if (enumerator.Current.Value is TrackedResponse<TResponse> trackedResponse && trackedRequest.Id == trackedResponse.ForRequest)
                        {
                            return trackedResponse.Response;
                        }
                    }
                }
                finally
                {
                    linkedCTS.Cancel();
                }
                // the assumption is that the cancellation token must have been cancelled for the flow to have reached here without returning directly from the while above
                throw new TaskCanceledException(string.Empty, null);
            }

            throw new InvalidOperationException("No handler registered!");
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber,
                                                  bool enableBuffering = true,
                                                  string name = null,
                                                  CancellationToken cancellationToken = default)
        {
            var subscriberType = typeof(ISubscriber<T>);
            var subscribers = subscribersByType.GetOrAdd(subscriberType, new ConcurrentList<(ISubscriber, bool)>());
            var subBufPair = (subscriber, enableBuffering);
            // Create enumerator BEFORE adding to subscribers to prevent race conditions
            var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            try
            {
                subscribers.Add(subBufPair);
                if (enableBuffering)
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        if (enumerator.Current.Value is T tItem) subscriber.OnNextOrError(tItem);
                    }
                    return true;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    tcs.RegisterCancellationToken(cancellationToken); // subscription ends only when the caller cancels via the cancellationToken
                    return await tcs.Task;
                }
            }
            catch (TaskCanceledException)
            {
                // expected
                return true;
            }
            finally
            {

                subscribers.Remove(subBufPair);
            }
        }

        /// <inheritdoc/>
        public bool Subscribe<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                                   bool enableBuffering = true,
                                                   CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);

            // Check if any handler is already registered for this request type
            if (requestHandlersByRequestType.ContainsKey(requestType))
            {
                return false;
            }

            if (!syncHandlersByType.TryAdd(handlerType, requestHandler))
            {
                return false;
            }

            // Register in the request type tracking dictionary
            if (!requestHandlersByRequestType.TryAdd(requestType, requestHandler))
            {
                // Rollback the sync handler registration
                syncHandlersByType.TryRemove(handlerType, out _);
                return false;
            }

            CancellationTokenRegistration registration = default;
            registration = cancellationToken.Register(() =>
            {
                syncHandlersByType.TryRemove(handlerType, out _);
                requestHandlersByRequestType.TryRemove(requestType, out _);
                registration.Dispose();
            });
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                                    bool enableBuffering = true,
                                                                    string name = null,
                                                                    CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);

            // Create enumerator BEFORE adding to requestHandlersByRequestType to prevent race conditions
            var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);

            // Check if any handler is already registered for this request type
            if (!requestHandlersByRequestType.TryAdd(requestType, requestHandler))
            {
                return false;
            }

            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                    {
                        var response = await requestHandler.HandleAsync(trackedRequest.Request).ConfigureAwait(false);
                        var trackedResponse = new TrackedResponse<TResponse>(trackedRequest.Id, response);
                        cache.Add(trackedResponse, out _);
                    }
                }
            }
            finally
            {
                requestHandlersByRequestType.TryRemove(requestType, out _);
            }
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler,
                                                              bool enableBuffering = true,
                                                              string name = null,
                                                              CancellationToken cancellationToken = default)
        {
            var notificationType = typeof(TNotification);
            var funcSubscribers = funcSubscribersByType.GetOrAdd(notificationType, new ConcurrentList<(Delegate, bool)>());
            var funcBufPair = ((Delegate)notificationHandler, enableBuffering);

            // Create enumerator BEFORE adding to funcSubscribersByType to prevent race conditions
            var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);

            try
            {
                funcSubscribers.Add(funcBufPair);
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (enumerator.Current.Value is TNotification notification && notificationHandler != null)
                    {
                        await notificationHandler.Invoke(notification, cancellationToken);
                    }
                    // Continue processing regardless of handler result
                }
                return true;
            }
            finally
            {
                funcSubscribers.Remove(funcBufPair);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler,
                                                                    bool enableBuffering = true,
                                                                    string name = null,
                                                                    CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);

            // Create enumerator BEFORE adding to requestHandlersByRequestType to prevent race conditions
            var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);

            // Check if any handler is already registered for this request type
            if (!requestHandlersByRequestType.TryAdd(requestType, asyncHandler))
            {
                return false;
            }

            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                    {
                        var response = await asyncHandler(trackedRequest.Request, cancellationToken);
                        cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                    }
                }
                return true;
            }
            finally
            {
                requestHandlersByRequestType.TryRemove(requestType, out _);
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cache.Dispose();
                    syncHandlersByType.Clear();
                    subscribersByType.Clear();
                    funcSubscribersByType.Clear();
                    requestHandlersByRequestType.Clear();
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