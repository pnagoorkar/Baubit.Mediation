namespace Baubit.Mediation
{
    /// <summary>
    /// Marker for a mediator request.
    /// </summary>
    public interface IRequest<TResponse> where TResponse : IResponse
    {
    }

    public interface IStreamRequest<TSegment, TResponse> : IRequest<TResponse> where TSegment : ISegment<TResponse> where TResponse: IResponse
    {

    }
}