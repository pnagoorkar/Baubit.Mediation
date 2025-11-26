using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    public interface IMediator
    {
        bool Publish(object notification);
        TResponse Publish<TRequest, TResponse>(TRequest request) where TRequest : IRequest<TResponse> where TResponse : IResponse;
        Task<TResponse> PublishAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;
        Task<TResponse> PublishAsyncAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;

        Task<bool> SubscribeAsync<T>(ISubscriber<T> subscriber, CancellationToken cancellationToken = default);
        //IAsyncEnumerable<TType> EnumerateAsync<TType>(CancellationToken cancellationToken = default);
        //IAsyncEnumerable<TType> EnumerateFutureAsync<TType>(CancellationToken cancellationToken = default);
        bool Subscribe<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> requestHandler,
                                            CancellationToken cancellationToken) where TRequest : IRequest<TResponse> where TResponse : IResponse;
        Task<bool> SubscribeAsync<TRequest, TResponse>(IAsyncRequestHandler<TRequest, TResponse> requestHandler,
                                                              CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse> where TResponse : IResponse;
    }
}
