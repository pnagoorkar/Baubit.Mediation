using System;
using System.Threading;

namespace Baubit.Mediation
{
    /// <summary>
    /// Non-generic base interface for all subscribers. Inherits from <see cref="IDisposable"/> to allow cleanup of resources.
    /// </summary>
    public interface ISubscriber : IDisposable
    {

    }
    /// <summary>
    /// Defines a subscriber that receives notifications of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of notifications to receive.</typeparam>
    public interface ISubscriber<T> : ISubscriber
    {
        /// <summary>
        /// Called when a new notification is received.
        /// </summary>
        /// <param name="next">The notification value.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during notification processing.</param>
        /// <returns><c>true</c> if the notification was processed successfully; otherwise <c>false</c>.</returns>
        bool OnNext(T next, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called when an error occurs during notification processing.
        /// </summary>
        /// <param name="error">The exception that occurred.</param>
        /// <returns><c>true</c> if the error was handled; otherwise <c>false</c>.</returns>
        bool OnError(Exception error);

        /// <summary>
        /// Called when the notification stream completes.
        /// </summary>
        /// <returns><c>true</c> if completion was handled; otherwise <c>false</c>.</returns>
        bool OnCompleted();
    }

    /// <summary>
    /// Extension methods for <see cref="ISubscriber{T}"/>.
    /// </summary>
    public static class SubscriberExtensions
    {
        /// <summary>
        /// Calls <see cref="ISubscriber{T}.OnNext"/> and if an exception occurs, calls <see cref="ISubscriber{T}.OnError"/>.
        /// </summary>
        /// <typeparam name="T">The notification type.</typeparam>
        /// <param name="subscriber">The subscriber instance.</param>
        /// <param name="next">The notification value.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during notification processing.</param>
        /// <returns><c>true</c> if OnNext or OnError returned true; otherwise <c>false</c>.</returns>
        public static bool OnNextOrError<T>(this ISubscriber<T> subscriber, T next, CancellationToken cancellationToken = default)
        {
            try
            {
                return subscriber.OnNext(next, cancellationToken);
            }
            catch (Exception exp)
            {
                return subscriber.OnError(exp);
            }
        }
    }
}