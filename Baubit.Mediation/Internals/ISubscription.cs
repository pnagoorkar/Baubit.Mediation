using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Mediation.Internals
{
    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This interface is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this interface directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Base interface for all subscription types. Defines the contract for running and disposing subscriptions.
    /// </remarks>
    public interface ISubscription : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this subscription uses buffered message delivery.
        /// </summary>
        bool EnableBuffering { get; }

        /// <summary>
        /// Gets the cancellation token to monitor for cancellation requests.
        /// </summary>
        CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This interface is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this interface directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Interface for subscriptions that handle notifications of type <typeparamref name="T"/>.
    /// </remarks>
    /// <typeparam name="T">The type of notifications handled by this subscription.</typeparam>
    public interface ISubscription<T> : ISubscription
    {
        /// <summary>
        /// Handles a notification by delivering it directly to the subscriber without buffering.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during notification handling.</param>
        /// <returns><c>true</c> if the notification was processed successfully; otherwise <c>false</c>.</returns>
        bool Handle(T notification, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This interface is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this interface directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Interface for subscriptions that handle request/response pairs.
    /// </remarks>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type implementing <see cref="IResponse"/>.</typeparam>
    public interface ISubscription<TRequest, TResponse> : ISubscription where TRequest : IRequest<TResponse> where TResponse : IResponse
    {

        /// <summary>
        /// Dispatches a request directly to the handler without buffering. Must be implemented by derived classes.
        /// </summary>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// <strong>INTERNAL API - NOT FOR PUBLIC USE</strong>
    /// <para>This interface is part of the internal implementation and may change or be removed in any future version without notice.</para>
    /// <para>Do not use this interface directly in your code. Use <see cref="IMediator"/> instead.</para>
    /// </summary>
    /// <remarks>
    /// Interface for subscriptions that handle stream request/response pairs, producing an async sequence of segments.
    /// </remarks>
    /// <typeparam name="TRequest">The stream request type implementing <see cref="IStreamRequest{TSegment,TResponse}"/>.</typeparam>
    /// <typeparam name="TSegment">The type of each segment produced.</typeparam>
    /// <typeparam name="TResponse">The overall response type implementing <see cref="IResponse"/>.</typeparam>
    public interface ISubscription<TRequest, TSegment, TResponse> : ISubscription where TRequest : IStreamRequest<TSegment, TResponse>
                                                                                  where TSegment : ISegment<TResponse>
                                                                                  where TResponse : IResponse
    {
        /// <summary>
        /// Handles a stream request directly and produces a sequence of segments without buffering.
        /// </summary>
        /// <param name="request">The stream request to handle.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>An async enumerable of segments.</returns>
        IAsyncEnumerable<TSegment> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}
