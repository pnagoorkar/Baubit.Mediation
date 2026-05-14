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

    /// <summary>
    /// Wraps a response segment with the identifier of the request it corresponds to, and an indicator
    /// of whether this is the final (terminal) segment in the stream.
    /// </summary>
    /// <typeparam name="TSegment">The segment type.</typeparam>
    /// <typeparam name="TResponse">The overall response type.</typeparam>
    public class TrackedResponseSegment<TSegment, TResponse> where TSegment : ISegment<TResponse> where TResponse : IResponse
    {
        /// <summary>
        /// Gets the identifier of the stream request this segment corresponds to.
        /// </summary>
        public Guid ForRequest { get; private set; }

        /// <summary>
        /// Gets the segment payload. <c>default</c> when <see cref="IsFinal"/> is <c>true</c>.
        /// </summary>
        public TSegment Segment { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this is the terminal sentinel segment that signals end-of-stream.
        /// When <c>true</c>, <see cref="Segment"/> is not meaningful.
        /// </summary>
        public bool IsFinal { get; private set; }

        /// <summary>
        /// Creates a new segment for the specified request.
        /// </summary>
        /// <param name="forRequest">The identifier of the corresponding stream request.</param>
        /// <param name="segment">The segment payload.</param>
        public TrackedResponseSegment(Guid forRequest, TSegment segment)
        {
            ForRequest = forRequest;
            Segment = segment;
        }

        /// <summary>
        /// Creates a terminal sentinel segment that signals end-of-stream.
        /// </summary>
        /// <param name="forRequest">The identifier of the corresponding stream request.</param>
        /// <param name="isFinal">Must be <c>true</c>; indicates this is the final segment.</param>
        public TrackedResponseSegment(Guid forRequest, bool isFinal)
        {
            ForRequest = forRequest;
            IsFinal = isFinal;
        }
    }
}