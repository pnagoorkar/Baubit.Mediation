namespace Baubit.Mediation
{
    /// <summary>
    /// Marker for a mediator response.
    /// </summary>
    public interface IResponse
    {
    }

    /// <summary>
    /// Marker for a single segment in a streamed response sequence.
    /// </summary>
    /// <typeparam name="TResponse">The overall response type this segment belongs to.</typeparam>
    public interface ISegment<TResponse> where TResponse : IResponse
    {

    }
}