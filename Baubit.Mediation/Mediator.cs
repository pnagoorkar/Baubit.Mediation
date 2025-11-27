using Baubit.Caching;
using Baubit.Collections;
using Baubit.Identity;
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
        private readonly ConcurrentDictionary<Type, IRequestHandler> _syncHandlersByType = new ConcurrentDictionary<Type, IRequestHandler>();
        private IList<IRequestHandler> _asyncHandlers = new ConcurrentList<IRequestHandler>();
        private IOrderedCache<object> _cache;
        private ILogger<Mediator> _logger;
        private GuidV7Generator _idGenerator;

        /// <summary>
        /// Creates a new <see cref="Mediator"/> instance.
        /// </summary>
        /// <param name="cache">The ordered cache for storing notifications and tracked requests.</param>
        /// <param name="loggerFactory">Factory to create loggers for diagnostics.</param>
        public Mediator(IOrderedCache<object> cache,
                   ILoggerFactory loggerFactory)
        {
            _cache = cache;
            _logger = loggerFactory.CreateLogger<Mediator>();
            _idGenerator = GuidV7Generator.CreateNew();
        }

        /// <inheritdoc/>
        public bool Publish(object notification)
        {
            return _cache.Add(notification, out _);
        }

        /// <inheritdoc/>
        public TResponse Publish<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            if (!_syncHandlersByType.TryGetValue(handlerType, out var handler))
            {
                throw new InvalidOperationException("No handler registered!");
            }
            return ((IRequestHandler<TRequest, TResponse>)handler).Handle(request);
        }

        /// <inheritdoc/>
        public async Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            if (!_syncHandlersByType.TryGetValue(handlerType, out var handler))
            {
                throw new InvalidOperationException("No handler registered!");
            }
            return await ((IRequestHandler<TRequest, TResponse>)handler).HandleSyncAsync(request);
        }

        /// <inheritdoc/>
        public async Task<TResponse> PublishAsyncAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var enumerator = _cache.GetFutureAsyncEnumerator(linkedCTS.Token);
            var trackedRequest = new TrackedRequest<TRequest, TResponse>(_idGenerator.GetNext(), request);
            if (!_cache.Add(trackedRequest, out _)) throw new Exception("<TBD>");
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
            // if the code ever reaches here, that assumption must no longer be true
            throw new TaskCanceledException(string.Empty, null);
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, CancellationToken cancellationToken = default)
        {
            var enumerator = _cache.GetFutureAsyncEnumerator(cancellationToken);
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                if (enumerator.Current.Value is T tItem) subscriber.OnNextOrError(tItem);
            }
            return true;
        }

        #region MoveToBaubit.Mediation.Extensions

        //public async IAsyncEnumerable<TType> EnumerateAsync<TType>([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        //{
        //    var enumerator = _cache.GetAsyncEnumerator(cancellationToken);
        //    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        //    {
        //        if (enumerator.Current.Value is TType tItem) yield return tItem;
        //    }
        //}

        //public async IAsyncEnumerable<TType> EnumerateFutureAsync<TType>([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        //{
        //    var enumerator = _cache.GetFutureAsyncEnumerator(cancellationToken);
        //    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        //    {
        //        if (enumerator.Current.Value is TType tItem) yield return tItem;
        //    }
        //}

        #endregion

        /// <inheritdoc/>
        public bool Subscribe<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                                   CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            if (!_syncHandlersByType.TryAdd(handlerType, requestHandler))
            {
                return false;
            }

            CancellationTokenRegistration registration = default;
            registration = cancellationToken.Register(() =>
            {
                _syncHandlersByType.TryRemove(handlerType, out _);
                registration.Dispose();
            });
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            _asyncHandlers.Add(requestHandler);
            try
            {
                var enumerator = _cache.GetFutureAsyncEnumerator(cancellationToken);

                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    if (enumerator.Current.Value is TrackedRequest<TRequest, TResponse> trackedRequest)
                    {
                        var response = await requestHandler.HandleAsyncAsync(trackedRequest.Request).ConfigureAwait(false);
                        var trackedResponse = new TrackedResponse<TResponse>(trackedRequest.Id, response);
                        _cache.Add(trackedResponse, out _);
                    }
                }
            }
            finally
            {
                _asyncHandlers.Remove(requestHandler);
            }
            return true;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cache.Dispose();
                    _syncHandlersByType.Clear();
                    _asyncHandlers.Clear();
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