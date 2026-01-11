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
        private readonly ConcurrentDictionary<Type, (IRequestHandler, bool)> syncHandlersByType = new ConcurrentDictionary<Type, (IRequestHandler, bool)>();
        private readonly ConcurrentDictionary<Type, IList<(ISubscriber, bool)>> subscribersByType = new ConcurrentDictionary<Type, IList<(ISubscriber, bool)>>();
        private readonly ConcurrentDictionary<Type, IList<(Delegate, bool)>> funcSubscribersByType = new ConcurrentDictionary<Type, IList<(Delegate, bool)>>();
        private readonly ConcurrentDictionary<Type, (object Handler, bool EnableBuffering)> requestHandlersByRequestType = new ConcurrentDictionary<Type, (object Handler, bool EnableBuffering)>();
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
            
            // Handle ISubscriber-based subscribers
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

            // Handle Func-based subscribers - support both buffered and unbuffered
            if (funcSubscribersByType.TryGetValue(typeof(T), out var funcSubscriptions))
            {
                foreach (var funcBufPair in funcSubscriptions)
                {
                    if (funcBufPair.Item2)
                    {
                        // Buffered - add to cache for async delivery
                        retVal &= cache.Add(notification, out _);
                    }
                    else
                    {
                        // Unbuffered - direct delivery (fire and forget)
                        var handler = (Func<T, CancellationToken, Task<bool>>)funcBufPair.Item1;
                        if (handler != null)
                        {
                            _ = handler.Invoke(notification, CancellationToken.None);
                        }
                    }
                }
            }
            return retVal;
        }

        /// <inheritdoc/>
        public async Task<bool> PublishAsync<T>(T notification, CancellationToken cancellationToken = default)
        {
            var retVal = true;
            
            // Handle ISubscriber-based subscribers
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

            // Handle Func-based subscribers - support both buffered and unbuffered
            if (funcSubscribersByType.TryGetValue(typeof(T), out var funcSubscriptions))
            {
                foreach (var funcBufPair in funcSubscriptions)
                {
                    if (funcBufPair.Item2)
                    {
                        // Buffered - add to cache for async delivery
                        retVal &= cache.Add(notification, out _);
                    }
                    else
                    {
                        // Unbuffered - direct async delivery
                        var handler = (Func<T, CancellationToken, Task<bool>>)funcBufPair.Item1;
                        if (handler != null)
                        {
                            retVal &= await handler.Invoke(notification, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            return retVal;
        }

        /// <inheritdoc/>
        public Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, string name = null, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);

            // Check if there's a synchronous handler registered - always use direct invocation for sync handlers
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            if (syncHandlersByType.TryGetValue(handlerType, out var syncHandlerPair))
            {
                var (handler, _) = syncHandlerPair;
                // Sync handlers always use direct invocation - enableBuffering stored for future observability
                return Task.FromResult(((IRequestHandler<TRequest, TResponse>)handler).Handle(request));
            }

            // Check if there's an async handler registered (IAsyncRequestHandler or Func handler)
            if (requestHandlersByRequestType.TryGetValue(requestType, out var asyncHandlerPair))
            {
                var (asyncHandler, asyncEnableBuffering) = asyncHandlerPair;
                if (!asyncEnableBuffering)
                {
                    // Unbuffered - direct invocation
                    return PublishAsyncUnbuffered<TRequest, TResponse>(asyncHandler, request, cancellationToken);
                }
                // Buffered - use cache pattern
                return PublishAsyncInternal<TRequest, TResponse>(request, name, cancellationToken);
            }

            throw new InvalidOperationException("No handler registered!");
        }

        private async Task<TResponse> PublishAsyncUnbuffered<TRequest, TResponse>(object handler, TRequest request, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            // Handler can be IAsyncRequestHandler<TRequest, TResponse> or Func<TRequest, CancellationToken, Task<TResponse>>
            if (handler is IAsyncRequestHandler<TRequest, TResponse> asyncRequestHandler)
            {
                return await asyncRequestHandler.HandleAsync(request).ConfigureAwait(false);
            }
            else if (handler is Func<TRequest, CancellationToken, Task<TResponse>> funcHandler)
            {
                return await funcHandler(request, cancellationToken).ConfigureAwait(false);
            }
            throw new InvalidOperationException("Unsupported handler type.");
        }

        private async Task<TResponse> PublishAsyncInternal<TRequest, TResponse>(TRequest request, string name, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            // Use the cache-based async pattern for async handlers
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await using var enumerator = cache.GetFutureAsyncEnumerator(name, linkedCTS.Token);
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
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
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

            if (!syncHandlersByType.TryAdd(handlerType, (requestHandler, enableBuffering)))
            {
                return false;
            }

            // Register in the request type tracking dictionary
            if (!requestHandlersByRequestType.TryAdd(requestType, (requestHandler, enableBuffering)))
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

            // Check if any handler is already registered for this request type
            if (!requestHandlersByRequestType.TryAdd(requestType, (requestHandler, enableBuffering)))
            {
                return false;
            }

            try
            {
                if (enableBuffering)
                {
                    // Buffered - create enumerator BEFORE adding handler to prevent race conditions
                    await using var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
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
                else
                {
                    // Unbuffered - wait for cancellation (direct invocation happens in PublishAsync)
                    var tcs = new TaskCompletionSource<bool>();
                    tcs.RegisterCancellationToken(cancellationToken);
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
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
            await using var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;

            try
            {
                funcSubscribers.Add(funcBufPair);
                
                if (enableBuffering)
                {
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        if (enumerator.Current.Value is TNotification notification && notificationHandler != null)
                        {
                            await notificationHandler.Invoke(notification, cancellationToken).ConfigureAwait(false);
                        }
                        // Continue processing regardless of handler result
                    }
                    return true;
                }
                else
                {
                    // Unbuffered - wait for cancellation (direct delivery happens in Publish)
                    var tcs = new TaskCompletionSource<bool>();
                    tcs.RegisterCancellationToken(cancellationToken);
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
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

            // Check if any handler is already registered for this request type
            if (!requestHandlersByRequestType.TryAdd(requestType, (asyncHandler, enableBuffering)))
            {
                return false;
            }

            try
            {
                if (enableBuffering)
                {
                    // Buffered - create enumerator after adding handler  
                    await using var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                        {
                            var response = await asyncHandler(trackedRequest.Request, cancellationToken).ConfigureAwait(false);
                            cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                        }
                    }
                }
                else
                {
                    // Unbuffered - wait for cancellation (direct invocation happens in PublishAsync)
                    var tcs = new TaskCompletionSource<bool>();
                    tcs.RegisterCancellationToken(cancellationToken);
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
            }
            finally
            {
                requestHandlersByRequestType.TryRemove(requestType, out _);
            }
            return true;
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