using System;

namespace Baubit.Mediation
{
    public interface ISubscriber<T> : IDisposable
    {
        bool OnNext(T next);
        bool OnError(Exception error);
        bool OnCompleted();
    }

    public static class SubscriberExtensions
    {
        public static bool OnNextOrError<T>(this ISubscriber<T> subscriber, T next)
        {
            try
            {
                return subscriber.OnNext(next);
            }
            catch (Exception exp)
            {
                return subscriber.OnError(exp);
            }
        }
    }
}
