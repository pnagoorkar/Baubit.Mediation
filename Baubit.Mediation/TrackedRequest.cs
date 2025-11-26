using System;

namespace Baubit.Mediation
{
    public class TrackedRequest<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        public Guid Id { get; private set; }
        public TRequest Request { get; private set; }
        public TrackedRequest(Guid id, TRequest request)
        {
            Id = id;
            Request = request;
        }
    }
}
