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

    public interface IAsyncStreamRequestHandler<TRequest, TSegment, TResponse> : IRequestHandler where TRequest : IStreamRequest<TSegment, TResponse> 
                                                                                                 where TSegment : ISegment<TResponse> 
                                                                                                 where TResponse : IResponse
    {
        IAsyncEnumerable<TSegment> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}