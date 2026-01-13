using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    /// <summary>
    /// Defines the contract for a mediator that routes requests to handlers and notifications to subscribers.
    /// Thread-safe for concurrent use.
    /// </summary>
    public interface IMediator
    {
        /// <summary>
        /// Publishes a notification to all subscribers. Waits for it to either get cached for delivery 
        /// or delivered (depending on what the subscriber chose).
        /// </summary>
        /// <typeparam name="T">The notification type.</typeparam>
        /// <param name="notification">The notification object to publish.</param>
        /// <returns><c>true</c> if the notification was successfully processed; otherwise <c>false</c>.</returns>
        bool Publish<T>(T notification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a notification asynchronously. Fire and forget. Callers can choose to await.
        /// </summary>
        /// <typeparam name="T">The notification type.</typeparam>
        /// <param name="notification">The notification object to publish.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that completes with <c>true</c> if the notification was successfully processed; otherwise <c>false</c>.</returns>
        Task<bool> PublishAsync<T>(T notification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a request asynchronously and awaits a response from a registered handler.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="request">The request to publish.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Subscribes to notifications of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of notifications to receive.</typeparam>
        /// <param name="subscriber">The subscriber that will receive notifications.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers notifications before delivering.<br/>true by default.<br/> Set to false if the subscriber is capable of processing notifications in parallel </param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes when the subscription ends.</returns>
        Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, bool enableBuffering = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to notifications of a specific type with a named cache enumerator.
        /// </summary>
        /// <typeparam name="T">The type of notifications to receive.</typeparam>
        /// <param name="subscriber">The subscriber that will receive notifications.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers notifications before delivering.</param>
        /// <param name="name">Name for the cache enumerator.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes when the subscription ends.</returns>
        Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, bool enableBuffering, string name, CancellationToken cancellationToken = default);


        Task<bool> SubscribeAsync<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                                       bool enableBuffering = true,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;


        Task<bool> SubscribeAsync<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                                       bool enableBuffering = true,
                                                       string name = null,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Registers an asynchronous request handler for a specific request/response pair.
        /// The handler will process requests from the cache until the cancellation token is triggered.
        /// Only one handler can be registered for a specific request type at a time.
        /// </summary>
        /// <typeparam name="TRequest">The request type to handle.</typeparam>
        /// <typeparam name="TResponse">The response type to return.</typeparam>
        /// <param name="requestHandler">The handler to register.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers requests before delivering.<br/>true by default.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes when the subscription ends with <c>true</c>, or <c>false</c> if a handler for this type is already registered.</returns>
        Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                       bool enableBuffering = true,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Registers an asynchronous request handler for a specific request/response pair with a named cache enumerator.
        /// The handler will process requests from the cache until the cancellation token is triggered.
        /// Only one handler can be registered for a specific request type at a time.
        /// </summary>
        /// <typeparam name="TRequest">The request type to handle.</typeparam>
        /// <typeparam name="TResponse">The response type to return.</typeparam>
        /// <param name="requestHandler">The handler to register.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers requests before delivering.</param>
        /// <param name="name">Name for the cache enumerator.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes when the subscription ends with <c>true</c>, or <c>false</c> if a handler for this type is already registered.</returns>
        Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                       bool enableBuffering = true,
                                                       string name = null,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Subscribes to notifications using a function handler.
        /// The handler will be invoked for each notification of type <typeparamref name="TNotification"/> from the cache until the cancellation token is triggered.
        /// </summary>
        /// <typeparam name="TNotification">The type of notifications to receive.</typeparam>
        /// <param name="notificationHandler">The function to handle notifications. Receives the notification and a cancellation token, returns a task that completes with a boolean result.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers notifications before delivering.<br/>true by default.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes with <c>true</c> when the subscription ends.</returns>
        Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler,
                                                 bool enableBuffering = true,
                                                 CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to notifications using a function handler with a named cache enumerator.
        /// The handler will be invoked for each notification of type <typeparamref name="TNotification"/> from the cache until the cancellation token is triggered.
        /// </summary>
        /// <typeparam name="TNotification">The type of notifications to receive.</typeparam>
        /// <param name="notificationHandler">The function to handle notifications. Receives the notification and a cancellation token, returns a task that completes with a boolean result.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers notifications before delivering.</param>
        /// <param name="name">Name for the cache enumerator.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes with <c>true</c> when the subscription ends.</returns>
        Task<bool> SubscribeAsync<TNotification>(Func<TNotification, CancellationToken, Task<bool>> notificationHandler,
                                                       bool enableBuffering = true,
                                                       string name = null,
                                                 CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to asynchronous requests using a function handler.
        /// The handler will process tracked requests from the cache and produce responses until the cancellation token is triggered.
        /// Only one handler can be registered for a specific request type at a time.
        /// </summary>
        /// <typeparam name="TRequest">The request type to handle.</typeparam>
        /// <typeparam name="TResponse">The response type to return.</typeparam>
        /// <param name="asyncHandler">The function to handle requests. Receives the request and a cancellation token, returns a task that completes with the response.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers requests before delivering.<br/>true by default.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes with <c>true</c> when the subscription ends, or <c>false</c> if another handler for this request type is already registered.</returns>
        Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler,
                                                       bool enableBuffering = true,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Subscribes to asynchronous requests using a function handler with a named cache enumerator.
        /// The handler will process tracked requests from the cache and produce responses until the cancellation token is triggered.
        /// Only one handler can be registered for a specific request type at a time.
        /// </summary>
        /// <typeparam name="TRequest">The request type to handle.</typeparam>
        /// <typeparam name="TResponse">The response type to return.</typeparam>
        /// <param name="asyncHandler">The function to handle requests. Receives the request and a cancellation token, returns a task that completes with the response.</param>
        /// <param name="enableBuffering">Determines if the mediator buffers requests before delivering.</param>
        /// <param name="name">Name for the cache enumerator.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes with <c>true</c> when the subscription ends, or <c>false</c> if another handler for this request type is already registered.</returns>
        Task<bool> SubscribeAsync<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> asyncHandler,
                                                       bool enableBuffering = true,
                                                       string name = null,
                                                       CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;
    }
}