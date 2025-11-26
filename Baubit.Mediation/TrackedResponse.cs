using System;

namespace Baubit.Mediation
{
    /// <summary>
    /// Wraps a response with the identifier of the request it corresponds to.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public class TrackedResponse<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Gets the identifier of the request this response corresponds to.
        /// </summary>
        public Guid ForRequest { get; private set; }

        /// <summary>
        /// Gets the response payload.
        /// </summary>
        public TResponse Response { get; private set; }

        /// <summary>
        /// Creates a new tracked response.
        /// </summary>
        /// <param name="forRequest">The identifier of the corresponding request.</param>
        /// <param name="response">The response payload.</param>
        public TrackedResponse(Guid forRequest, TResponse response)
        {
            ForRequest = forRequest;
            Response = response;
        }
    }
}
