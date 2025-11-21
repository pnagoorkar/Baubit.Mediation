namespace Baubit.Mediation
{
    /// <summary>
    /// Marker for a mediator request.
    /// </summary>
    public interface IRequest<TResponse> where TResponse : IResponse
    {
    }
}
