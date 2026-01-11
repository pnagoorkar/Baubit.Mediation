using Baubit.Caching;
using Baubit.Identity;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation
{
    internal interface ISubscription
    {
        bool EnableBuffering { get; }
        Task<bool> RunAsync(IOrderedCache<long, object> cache, string name, CancellationToken cancellationToken = default);
    }

    internal interface ISubscription<T> : ISubscription
    {
        bool Publish(T notification, IOrderedCache<long, object> cache, CancellationToken cancellationToken = default);
    }

    internal interface ISubscription<TRequest, TResponse> : ISubscription where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        Task<TResponse> PublishAsync(TRequest request, IOrderedCache<long, object> cache, GuidV7Generator identityGenerator, string name = null, CancellationToken cancellationToken = default);
    }
}
