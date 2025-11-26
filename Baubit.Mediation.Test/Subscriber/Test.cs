using System;
using Xunit;

namespace Baubit.Mediation.Test.Subscriber
{
    /// <summary>
    /// Tests for <see cref="SubscriberExtensions"/>
    /// </summary>
    public class Test
    {
        private class TestSubscriber : ISubscriber<string>
        {
            public string? LastValue { get; private set; }
            public Exception? LastError { get; private set; }
            public bool IsCompleted { get; private set; }
            public bool ThrowOnNext { get; set; }

            public bool OnNext(string next)
            {
                if (ThrowOnNext) throw new InvalidOperationException("OnNext error");
                LastValue = next;
                return true;
            }

            public bool OnError(Exception error)
            {
                LastError = error;
                return true;
            }

            public bool OnCompleted()
            {
                IsCompleted = true;
                return true;
            }

            public void Dispose() { }
        }

        [Fact]
        public void OnNextOrError_Success_CallsOnNext()
        {
            // Arrange
            var subscriber = new TestSubscriber();

            // Act
            var result = subscriber.OnNextOrError("test");

            // Assert
            Assert.True(result);
            Assert.Equal("test", subscriber.LastValue);
            Assert.Null(subscriber.LastError);
        }

        [Fact]
        public void OnNextOrError_WhenOnNextThrows_CallsOnError()
        {
            // Arrange
            var subscriber = new TestSubscriber { ThrowOnNext = true };

            // Act
            var result = subscriber.OnNextOrError("test");

            // Assert
            Assert.True(result);
            Assert.NotNull(subscriber.LastError);
            Assert.IsType<InvalidOperationException>(subscriber.LastError);
            Assert.Equal("OnNext error", subscriber.LastError.Message);
        }
    }
}
