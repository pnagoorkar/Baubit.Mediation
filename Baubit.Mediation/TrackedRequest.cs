using System;

namespace Baubit.Mediation
{
    /// <summary>
    /// Wraps a request with a unique identifier for tracking through the cache-based async processing pipeline.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    public class TrackedRequest<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Gets the unique identifier for this tracked request.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Gets the underlying request payload.
        /// </summary>
        public TRequest Request { get; private set; }

        /// <summary>
        /// Creates a new tracked request.
        /// </summary>
        /// <param name="id">The unique identifier for tracking.</param>
        /// <param name="request">The request payload.</param>
        public TrackedRequest(Guid id, TRequest request)
        {
            Id = id;
            Request = request;
        }
    }
}