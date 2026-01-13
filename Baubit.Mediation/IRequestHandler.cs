using System.Threading;

namespace Baubit.Mediation
{
    /// <summary>
    /// Non-generic base interface for all request handlers.
    /// </summary>
    public interface IRequestHandler
    {
    }

    /// <summary>
    /// Defines a synchronous request handler for a specific request/response pair.
    /// </summary>
    /// <typeparam name="TRequest">The request type to handle.</typeparam>
    /// <typeparam name="TResponse">The response type to return.</typeparam>
    public interface IRequestHandler<TRequest, TResponse> : IRequestHandler
        where TRequest : IRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Handles the request synchronously and returns a response.
        /// </summary>
        /// <param name="request">The request payload.</param>
        /// <returns>The response.</returns>
        TResponse Handle(TRequest request, CancellationToken cancellationToken = default);
    }
}