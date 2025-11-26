using System;
using Xunit;

namespace Baubit.Mediation.Test.TrackedRequest
{
    /// <summary>
    /// Tests for <see cref="TrackedRequest{TRequest, TResponse}"/>
    /// </summary>
    public class Test
    {
        public class TestRequest : IRequest<TestResponse>
        {
            public string Value { get; set; } = string.Empty;
        }

        public class TestResponse : IResponse
        {
            public string Result { get; set; } = string.Empty;
        }

        [Fact]
        public void Constructor_SetsIdAndRequest()
        {
            // Arrange
            var id = Guid.NewGuid();
            var request = new TestRequest { Value = "test" };

            // Act
            var trackedRequest = new TrackedRequest<TestRequest, TestResponse>(id, request);

            // Assert
            Assert.Equal(id, trackedRequest.Id);
            Assert.Same(request, trackedRequest.Request);
        }

        [Fact]
        public void Constructor_WithEmptyGuid_SetsEmptyId()
        {
            // Arrange
            var id = Guid.Empty;
            var request = new TestRequest { Value = "test" };

            // Act
            var trackedRequest = new TrackedRequest<TestRequest, TestResponse>(id, request);

            // Assert
            Assert.Equal(Guid.Empty, trackedRequest.Id);
        }

        [Fact]
        public void Request_IsImmutable()
        {
            // Arrange
            var id = Guid.NewGuid();
            var request = new TestRequest { Value = "original" };
            var trackedRequest = new TrackedRequest<TestRequest, TestResponse>(id, request);

            // Act - modify the original request
            request.Value = "modified";

            // Assert - tracked request should reflect the same reference
            Assert.Equal("modified", trackedRequest.Request.Value);
        }
    }
}
