namespace Baubit.Mediation
{
    /// <summary>
    /// Marker for a mediator request.
    /// </summary>
    public interface IRequest<TResponse> where TResponse : IResponse
    {
    }

    /// <summary>
    /// Marker for a mediator stream request that returns a sequence of <typeparamref name="TSegment"/> values.
    /// </summary>
    /// <typeparam name="TSegment">The type of each streamed segment.</typeparam>
    /// <typeparam name="TResponse">The overall response type associated with this stream.</typeparam>
    public interface IStreamRequest<TSegment, TResponse> : IRequest<TResponse> where TSegment : ISegment<TResponse> where TResponse : IResponse
    {

    }
}