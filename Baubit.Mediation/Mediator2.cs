using Baubit.Caching;
using Baubit.Collections;
using Baubit.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    public class Mediator2 : IMediator
    {
        private readonly ConcurrentDictionary<Type, IList<ISubscription>> activeSubscriptions = new ConcurrentDictionary<Type, IList<ISubscription>>();

        private IOrderedCache<long, object> cache;
        private ILogger<Mediator> logger;
        private IIdentityGenerator idGenerator;
        public Mediator2(IOrderedCache<long, object> cache,
                        ILoggerFactory loggerFactory)
        {
            this.cache = cache;
            this.logger = loggerFactory.CreateLogger<Mediator>();
            this.idGenerator = IdentityGenerator.CreateNew();
        }
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

        public Task<bool> PublishAsync<T>(T notification, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Publish(notification));
        }

        public Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return PublishAsync<TRequest, TResponse>(request, null, cancellationToken);
        }

        public async Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, string name, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            if (!activeSubscriptions.TryGetValue(typeof(ISubscription<TRequest, TResponse>), out var subscriptions)) throw new Exception("No handler registered! Active subscriptions empty.");
            var subscription = subscriptions.SingleOrDefault();
            if (subscription == null) throw new Exception("No handler registered! Subscription is null.");
            if (subscription is not ISubscription<TRequest, TResponse> requestSubscription) throw new Exception($"Unexpcted type of handler registered for {typeof(TRequest).AssemblyQualifiedName}");

            return await requestSubscription.PublishAsync(request, cache, idGenerator, name, cancellationToken);
        }

        public bool Subscribe<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering = true, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse
        {
            return Subscribe(requestHandler, enableBuffering, null, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task<bool> Subscribe<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering = true, string name = null, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(null, cancellationToken) : null;
            var subscription = new SyncInterfaceSubscription<TRequest, TResponse>(requestHandler, enableBuffering);
            var subscriptions = new List<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TRequest, TResponse>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) return false; // there is a handler already registered to handle TRequest
            try
            {
                return await subscription.RunAsync(cache, name, cancellationToken);
            }
            finally { activeSubscriptions.TryRemove(typeof(ISubscription<TRequest, TResponse>), out _); }
        }

        public Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, bool enableBuffering = true, CancellationToken cancellationToken = default)
        {
            return SubscribeAsync(subscriber, enableBuffering, null, cancellationToken);
        }

        public async Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, bool enableBuffering, string name, CancellationToken cancellationToken = default)
        {
            var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new InterfaceSubscription<T>(subscriber, enableBuffering);
            var subscriptions = new ConcurrentList<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<T>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) cachedSubscription.Add(subscription); // Another subscriber raced to create the subscriptions collection. No worries.
            try
            {
                return await subscription.RunAsync(cache, name, cancellationToken);
            }
            finally { cachedSubscription.Remove(subscription); }
        }

        public Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering = true, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsync(requestHandler, enableBuffering, null, cancellationToken);
        }

        public async Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler, bool enableBuffering, string name, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new AsyncInterfaceSubscription<TRequest, TResponse>(requestHandler, enableBuffering);
            var subscriptions = new List<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TRequest, TResponse>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) return false; // there is a handler already registered to handle TRequest
            try
            {
                return await subscription.RunAsync(cache, name, cancellationToken);
            }
            finally { activeSubscriptions.TryRemove(typeof(ISubscription<TRequest, TResponse>), out _); }
        }

        public Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler, bool enableBuffering = true, CancellationToken cancellationToken = default)
        {
            return SubscribeAsync(notificationHandler, enableBuffering, null, cancellationToken);
        }

        public async Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler, bool enableBuffering, string name, CancellationToken cancellationToken = default)
        {
            var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new FuncSubscription<TNotification>(notificationHandler, enableBuffering);
            var subscriptions = new ConcurrentList<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TNotification>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) cachedSubscription.Add(subscription); // Another subscriber raced to create the subscriptions collection. No worries.
            try
            {
                return await subscription.RunAsync(cache, name, cancellationToken);
            }
            finally { cachedSubscription.Remove(subscription); }
        }

        public Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler, bool enableBuffering = true, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            return SubscribeAsync(asyncHandler, enableBuffering, null, cancellationToken);
        }

        public async Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler, bool enableBuffering, string name, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            where TResponse : IResponse
        {
            var enumerator = enableBuffering ? cache.GetFutureAsyncEnumerator(name, cancellationToken) : null;
            var subscription = new AsyncFuncSubscription<TRequest, TResponse>(asyncHandler, enableBuffering);
            var subscriptions = new List<ISubscription> { subscription };
            var cachedSubscription = activeSubscriptions.GetOrAdd(typeof(ISubscription<TRequest, TResponse>), subscriptions);
            if (!ReferenceEquals(cachedSubscription, subscriptions)) return false; // there is a handler already registered to handle TRequest
            try
            {
                return await subscription.RunAsync(cache, name, cancellationToken);
            }
            finally { cachedSubscription.Remove(subscriptions[0]); }
        }
    }

}
