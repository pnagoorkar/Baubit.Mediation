namespace Baubit.Mediation
{
    /// <summary>
    /// Marker for a mediator response.
    /// </summary>
    public interface IResponse
    {
    }

    public interface ISegment<TResponse> where TResponse : IResponse
    {

    }
}