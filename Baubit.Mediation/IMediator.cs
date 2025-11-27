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
        /// Publishes a notification to all subscribers.
        /// </summary>
        /// <param name="notification">The notification object to publish.</param>
        /// <returns><c>true</c> if the notification was successfully added to the cache; otherwise <c>false</c>.</returns>
        bool Publish(object notification);

        /// <summary>
        /// Publishes a request synchronously and returns the response from the registered handler.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="request">The request to publish.</param>
        /// <returns>The response from the handler.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        TResponse Publish<TRequest, TResponse>(TRequest request) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Publishes a request asynchronously using a synchronous handler and returns the response.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="request">The request to publish.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Publishes a request asynchronously to an asynchronous handler and returns the response.
        /// The request is tracked through the cache until a matching response is received.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="request">The request to publish.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        Task<TResponse> PublishAsyncAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Subscribes to notifications of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of notifications to receive.</typeparam>
        /// <param name="subscriber">The subscriber that will receive notifications.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes when the subscription ends.</returns>
        Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a synchronous request handler for a specific request/response pair.
        /// </summary>
        /// <typeparam name="TRequest">The request type to handle.</typeparam>
        /// <typeparam name="TResponse">The response type to return.</typeparam>
        /// <param name="requestHandler">The handler to register.</param>
        /// <param name="cancellationToken">A token that unregisters the handler when cancelled.</param>
        /// <returns><c>true</c> if the handler was registered; <c>false</c> if a handler for this type is already registered.</returns>
        bool Subscribe<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                            CancellationToken cancellationToken) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        /// <summary>
        /// Registers an asynchronous request handler for a specific request/response pair.
        /// The handler will process requests from the cache until the cancellation token is triggered.
        /// </summary>
        /// <typeparam name="TRequest">The request type to handle.</typeparam>
        /// <typeparam name="TResponse">The response type to return.</typeparam>
        /// <param name="requestHandler">The handler to register.</param>
        /// <param name="cancellationToken">A token to cancel the subscription.</param>
        /// <returns>A task that completes when the subscription ends.</returns>
        Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                              CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;
    }
}