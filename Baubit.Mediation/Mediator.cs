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
        private readonly SemaphoreSlim _handlerRegistrationLock = new SemaphoreSlim(1, 1);
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
        public Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return PublishAsyncCore<TRequest, TResponse>(request, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, string name, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return PublishAsyncCore<TRequest, TResponse>(request, name, cancellationToken);
        }

        private async Task<TResponse> PublishAsyncCore<TRequest, TResponse>(TRequest request, string name, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);

            // Synchronize reads with writes to ensure consistent handler state
            await _handlerRegistrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Check for synchronous handler
                if (syncHandlersByType.TryGetValue(handlerType, out var syncHandlerPair))
                {
                    return await ProcessSyncHandler<TRequest, TResponse>(syncHandlerPair, request, name, cancellationToken).ConfigureAwait(false);
                }

                // Check for async handler
                if (requestHandlersByRequestType.TryGetValue(requestType, out var asyncHandlerPair))
                {
                    return await ProcessAsyncHandler<TRequest, TResponse>(asyncHandlerPair, request, name, cancellationToken).ConfigureAwait(false);
                }

                throw new InvalidOperationException("No handler registered!");
            }
            finally
            {
                _handlerRegistrationLock.Release();
            }
        }

        private Task<TResponse> ProcessSyncHandler<TRequest, TResponse>(
            (IRequestHandler, bool) syncHandlerPair, TRequest request, string name, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var (handler, enableBuffering) = syncHandlerPair;
            if (enableBuffering)
            {
                return PublishAsyncInternal<TRequest, TResponse>(request, name, cancellationToken);
            }
            return Task.FromResult(((IRequestHandler<TRequest, TResponse>)handler).Handle(request));
        }

        private Task<TResponse> ProcessAsyncHandler<TRequest, TResponse>(
            (object Handler, bool EnableBuffering) asyncHandlerPair, TRequest request, string name, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var (asyncHandler, asyncEnableBuffering) = asyncHandlerPair;
            if (!asyncEnableBuffering)
            {
                return PublishAsyncUnbuffered<TRequest, TResponse>(asyncHandler, request, cancellationToken);
            }
            return PublishAsyncInternal<TRequest, TResponse>(request, name, cancellationToken);
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

        private async Task ProcessSyncHandlerRequestsAsync<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                                                                  CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            try
            {
                await using var enumerator = cache.GetFutureAsyncEnumerator(null, cancellationToken);
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                    {
                        var response = requestHandler.Handle(trackedRequest.Request);
                        var trackedResponse = new TrackedResponse<TResponse>(trackedRequest.Id, response);
                        cache.Add(trackedResponse, out _);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected when cancellation is requested
            }
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber,
                                                  bool enableBuffering = true,
                                                  CancellationToken cancellationToken = default)
        {
            return SubscribeAsyncCore<T>(subscriber, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber,
                                                  bool enableBuffering,
                                                  string name,
                                                  CancellationToken cancellationToken = default)
        {
            return SubscribeAsyncCore<T>(subscriber, enableBuffering, name, cancellationToken);
        }

        private async Task<bool> SubscribeAsyncCore<T>(ISubscriber<T> subscriber,
                                                   bool enableBuffering,
                                                   string name,
                                                   CancellationToken cancellationToken)
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
                }
                else
                {
                    // Unbuffered - wait for cancellation (subscription ends only when the caller cancels via the cancellationToken)
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
            }
            finally
            {

                subscribers.Remove(subBufPair);
            }
            return true;
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

            // Atomic registration using semaphore to prevent race conditions
            _handlerRegistrationLock.Wait(cancellationToken);
            try
            {
                if (!TryRegisterSyncHandler(requestType, handlerType, requestHandler, enableBuffering))
                {
                    return false;
                }
            }
            finally
            {
                _handlerRegistrationLock.Release();
            }

            // If buffering is enabled, start a background task to process requests from the cache
            // Fire-and-forget is intentional: the task runs until cancellation and errors are handled internally
            if (enableBuffering)
            {
                _ = ProcessSyncHandlerRequestsAsync(requestHandler, cancellationToken);
            }

            RegisterUnsubscribeCallback(requestType, handlerType, cancellationToken);
            return true;
        }

        private bool TryRegisterSyncHandler<TRequest, TResponse>(
            Type requestType, Type handlerType, IRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
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
                // Rollback the sync handler registration atomically
                syncHandlersByType.TryRemove(handlerType, out _);
                return false;
            }

            return true;
        }

        private void RegisterUnsubscribeCallback(Type requestType, Type handlerType, CancellationToken cancellationToken)
        {
            CancellationTokenRegistration registration = default;
            registration = cancellationToken.Register(() =>
            {
                // Atomic unregistration using semaphore
                _handlerRegistrationLock.Wait();
                try
                {
                    syncHandlersByType.TryRemove(handlerType, out _);
                    requestHandlersByRequestType.TryRemove(requestType, out _);
                }
                finally
                {
                    _handlerRegistrationLock.Release();
                }
                registration.Dispose();
            });
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                                    bool enableBuffering = true,
                                                                    CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsyncCore<TRequest, TResponse>(requestHandler, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                                    bool enableBuffering,
                                                                    string name,
                                                                    CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsyncCore<TRequest, TResponse>(requestHandler, enableBuffering, name, cancellationToken);
        }

        private async Task<bool> SubscribeAsyncCore<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                                     bool enableBuffering,
                                                                     string name,
                                                                     CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);

            // Atomic registration using semaphore
            await _handlerRegistrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!requestHandlersByRequestType.TryAdd(requestType, (requestHandler, enableBuffering)))
                {
                    return false;
                }
            }
            finally
            {
                _handlerRegistrationLock.Release();
            }

            try
            {
                await ProcessAsyncRequestHandler(requestHandler, enableBuffering, name, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await UnregisterRequestHandlerAsync(requestType).ConfigureAwait(false);
            }
            return true;
        }

        private async Task ProcessAsyncRequestHandler<TRequest, TResponse>(
            IAsyncRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering, string name, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            try
            {
                if (enableBuffering)
                {
                    await using var enumerator = cache.GetFutureAsyncEnumerator(name, cancellationToken);
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                        {
                            var response = await requestHandler.HandleAsync(trackedRequest.Request).ConfigureAwait(false);
                            cache.Add(new TrackedResponse<TResponse>(trackedRequest.Id, response), out _);
                        }
                    }
                }
                else
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
            }
        }

        private async Task UnregisterRequestHandlerAsync(Type requestType)
        {
            await _handlerRegistrationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                requestHandlersByRequestType.TryRemove(requestType, out _);
            }
            finally
            {
                _handlerRegistrationLock.Release();
            }
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler,
                                                              bool enableBuffering = true,
                                                              CancellationToken cancellationToken = default)
        {
            return SubscribeAsyncCore<TNotification>(notificationHandler, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler,
                                                              bool enableBuffering,
                                                              string name,
                                                              CancellationToken cancellationToken = default)
        {
            return SubscribeAsyncCore<TNotification>(notificationHandler, enableBuffering, name, cancellationToken);
        }

        private async Task<bool> SubscribeAsyncCore<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler,
                                                               bool enableBuffering,
                                                               string name,
                                                               CancellationToken cancellationToken)
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
                }
                else
                {
                    // Unbuffered - wait for cancellation (direct delivery happens in Publish)
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
            }
            finally
            {
                funcSubscribers.Remove(funcBufPair);
            }
            return true;
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler,
                                                                    bool enableBuffering = true,
                                                                    CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsyncCore<TRequest, TResponse>(asyncHandler, enableBuffering, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler,
                                                                    bool enableBuffering,
                                                                    string name,
                                                                    CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsyncCore<TRequest, TResponse>(asyncHandler, enableBuffering, name, cancellationToken);
        }

        private async Task<bool> SubscribeAsyncCore<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler,
                                                                     bool enableBuffering,
                                                                     string name,
                                                                     CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var requestType = typeof(TRequest);

            // Atomic registration using semaphore
            await _handlerRegistrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!requestHandlersByRequestType.TryAdd(requestType, (asyncHandler, enableBuffering)))
                {
                    return false;
                }
            }
            finally
            {
                _handlerRegistrationLock.Release();
            }

            try
            {
                await ProcessFuncRequestHandler(asyncHandler, enableBuffering, name, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await UnregisterRequestHandlerAsync(requestType).ConfigureAwait(false);
            }
            return true;
        }

        private async Task ProcessFuncRequestHandler<TRequest, TResponse>(
            Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler, bool enableBuffering, string name, CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            try
            {
                if (enableBuffering)
                {
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
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // expected
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cache.Dispose();
                    _handlerRegistrationLock.Dispose();
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