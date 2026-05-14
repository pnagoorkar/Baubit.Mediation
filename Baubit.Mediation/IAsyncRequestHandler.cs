using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    /// <summary>
    /// Defines an asynchronous request handler for a specific request/response pair.
    /// Implementations are typically registered with an <see cref="IMediator"/> so that
    /// asynchronous requests published via <see cref="IMediator.PublishAsync{TRequest, TResponse}(TRequest, System.Threading.CancellationToken)"/>
    /// can be processed and a corresponding response produced.
    /// </summary>
    /// <typeparam name="TRequest">The request type to handle.</typeparam>
    /// <typeparam name="TResponse">The response type to return.</typeparam>
    public interface IAsyncRequestHandler<TRequest, TResponse> : IRequestHandler
        where TRequest : IRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Handles the request asynchronously and produces a response.
        /// </summary>
        /// <param name="request">The request payload.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during request processing.</param>
        /// <returns>A task that completes with the response.</returns>
        Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines an asynchronous stream request handler that produces a sequence of <typeparamref name="TSegment"/> values
    /// in response to a single <typeparamref name="TRequest"/>.
    /// </summary>
    /// <typeparam name="TRequest">The stream request type to handle.</typeparam>
    /// <typeparam name="TSegment">The type of each segment produced.</typeparam>
    /// <typeparam name="TResponse">The overall response type.</typeparam>
    public interface IAsyncStreamRequestHandler<TRequest, TSegment, TResponse> : IRequestHandler where TRequest : IStreamRequest<TSegment, TResponse>
                                                                                                 where TSegment : ISegment<TResponse>
                                                                                                 where TResponse : IResponse
    {
        /// <summary>
        /// Handles the stream request and produces a sequence of segments asynchronously.
        /// </summary>
        /// <param name="request">The stream request payload.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during stream production.</param>
        /// <returns>An async enumerable of segments.</returns>
        IAsyncEnumerable<TSegment> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}