using System;

namespace Baubit.Mediation
{
    public class TrackedResponse<TResponse> where TResponse : IResponse
    {
        public Guid ForRequest { get; private set; }
        public TResponse Response { get; private set; }
        public TrackedResponse(Guid forRequest, TResponse response)
        {
            ForRequest = forRequest;
            Response = response;
        }
    }
}
