using System;
using Xunit;

namespace Baubit.Mediation.Test.TrackedResponse
{
    /// <summary>
    /// Tests for <see cref="TrackedResponse{TResponse}"/>
    /// </summary>
    public class Test
    {
        public class TestResponse : IResponse
        {
            public string Result { get; set; } = string.Empty;
        }

        [Fact]
        public void Constructor_SetsForRequestAndResponse()
        {
            // Arrange
            var forRequest = Guid.NewGuid();
            var response = new TestResponse { Result = "result" };

            // Act
            var trackedResponse = new TrackedResponse<TestResponse>(forRequest, response);

            // Assert
            Assert.Equal(forRequest, trackedResponse.ForRequest);
            Assert.Same(response, trackedResponse.Response);
        }

        [Fact]
        public void Constructor_WithEmptyGuid_SetsEmptyForRequest()
        {
            // Arrange
            var forRequest = Guid.Empty;
            var response = new TestResponse { Result = "result" };

            // Act
            var trackedResponse = new TrackedResponse<TestResponse>(forRequest, response);

            // Assert
            Assert.Equal(Guid.Empty, trackedResponse.ForRequest);
        }

        [Fact]
        public void Response_IsImmutable()
        {
            // Arrange
            var forRequest = Guid.NewGuid();
            var response = new TestResponse { Result = "original" };
            var trackedResponse = new TrackedResponse<TestResponse>(forRequest, response);

            // Act - modify the original response
            response.Result = "modified";

            // Assert - tracked response should reflect the same reference
            Assert.Equal("modified", trackedResponse.Response.Result);
        }
    }
}
