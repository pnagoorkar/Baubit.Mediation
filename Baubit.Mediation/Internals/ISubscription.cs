using Baubit.Caching;
using Baubit.Identity;
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
        /// Runs the subscription asynchronously, processing messages until cancelled.
        /// </summary>
        /// <param name="cache">The ordered cache containing messages.</param>
        /// <param name="enumerator">The asynchronous enumerator for reading messages from the cache.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the subscription.</param>
        /// <returns>A task that completes when the subscription ends, returning true on successful completion.</returns>
        Task<bool> RunAsync(IOrderedCache<long, object> cache, IAsyncEnumerator<IEntry<long, object>> enumerator, CancellationToken cancellationToken = default);
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
        /// Publishes a notification to this subscription, either buffering it or delivering it directly.
        /// </summary>
        /// <param name="notification">The notification to publish.</param>
        /// <param name="cache">The ordered cache for buffered delivery.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>True if the notification was successfully published; otherwise false.</returns>
        bool Publish(T notification, IOrderedCache<long, object> cache, CancellationToken cancellationToken = default);
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
        /// Publishes a request asynchronously and awaits the response.
        /// </summary>
        /// <param name="request">The request to publish.</param>
        /// <param name="cache">The ordered cache for tracked request/response pairs.</param>
        /// <param name="identityGenerator">Generator for creating unique request identifiers.</param>
        /// <param name="name">Optional name for the cache enumerator.</param>
        /// <param name="cancellationToken">Token to signal cancellation of the operation.</param>
        /// <returns>A task that completes with the response from the handler.</returns>
        Task<TResponse> PublishAsync(TRequest request, IOrderedCache<long, object> cache, GuidV7Generator identityGenerator, string name = null, CancellationToken cancellationToken = default);
    }
}
