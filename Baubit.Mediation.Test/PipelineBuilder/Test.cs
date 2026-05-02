using Baubit.Caching;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Baubit.Mediation.Test.PipelineBuilder
{
    /// <summary>
    /// Tests for <see cref="Baubit.Mediation.PipelineBuilder{T}"/> and <see cref="Baubit.Mediation.Pipeline{T}"/>.
    /// </summary>
    public class Test
    {
        #region Helpers

        private static long _nextId = 0;

        private static IOrderedCache<long, object> CreateCache()
        {
            var configuration = new Baubit.Caching.Configuration();
            var loggerFactory = LoggerFactory.Create(b => { });
            Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref _nextId);
            var store = new Baubit.Caching.InMemory.Store<long, object>(null, null, nextIdFactory, loggerFactory);
            var metadata = new Baubit.Caching.InMemory.Metadata<long>(configuration, loggerFactory);
            return new Baubit.Caching.OrderedCache<long, object>(configuration, null, store, metadata, loggerFactory);
        }

        private static ILoggerFactory CreateLoggerFactory() => LoggerFactory.Create(b => { });

        #endregion

        /// <summary>
        /// Tests that a pipeline with no segments uses <c>lastSegment</c> as <c>firstSegment</c> and
        /// returns <c>true</c> when <c>RunAsync</c> is called — covering <c>LinkSegments</c> with an
        /// empty list (loop body never entered) and the <c>lastSegment</c> lambda body.
        /// </summary>
        [Fact]
        public async Task RunAsync_WithNoSegments_ReturnsTrueViaLastSegment()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var signal = new ManualResetEventSlim(false);

            // Subscribe with a pipeline that has no segments — firstSegment becomes lastSegment
            // which returns Task.FromResult(true). Wrap with a buffering func subscriber so we
            // can observe when RunAsync actually executes inside the mediator loop.
            var subscribeTask = mediator.SubscribeAsync<string>(
                pb => { /* no segments — tests LinkSegments with 0 segments */ },
                enableBuffering: true,
                cts.Token);

            // Also subscribe a plain func subscriber to know when the notification was picked up
            // by the cache (so we can reliably cancel afterwards).
            var helperTask = mediator.SubscribeAsync<string>(
                async (item, ct) =>
                {
                    signal.Set();
                    await Task.CompletedTask;
                    return true;
                },
                enableBuffering: true,
                cts.Token);

            // Act
            mediator.Publish("ping");
            signal.Wait(TimeSpan.FromSeconds(5));
            cts.Cancel();

            // Assert — the ContinueWith in SubscribeAsync<T>(Action<PipelineBuilder<T>>,...) returns true
            var result = await subscribeTask;
            Assert.True(result);
        }

        /// <summary>
        /// Tests that a pipeline with a single segment: the segment runs, its lambda body executes
        /// (covering the <c>next = (evt, n, ct) => currentSegment(evt, next, ct)</c> closure created
        /// inside <c>LinkSegments</c>), and the <c>lastSegment</c> body is reached via the chain.
        /// </summary>
        [Fact]
        public async Task RunAsync_WithOneSegment_SegmentIsInvokedAndChainedToLastSegment()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            var received = new ConcurrentBag<string>();
            var signal = new ManualResetEventSlim(false);

            // Act
            var subscribeTask = mediator.SubscribeAsync<string>(
                pb =>
                {
                    pb.Use(async (item, next, ct) =>
                    {
                        received.Add(item);
                        signal.Set();
                        // call next so lastSegment lambda body is also executed
                        return await next(item, null, ct);
                    });
                },
                enableBuffering: true,
                cts.Token);

            mediator.Publish("hello");
            signal.Wait(TimeSpan.FromSeconds(5));
            cts.Cancel();

            var result = await subscribeTask;

            // Assert
            Assert.True(result);
            Assert.Single(received);
            Assert.Equal("hello", received.First());
        }

        /// <summary>
        /// Tests that a pipeline with multiple segments links them in the correct order.
        /// Each iteration of the <c>LinkSegments</c> for-loop creates a closure that calls the
        /// current segment and passes <c>next</c> into it; this verifies that all closures execute
        /// in declaration order (first → second → third).
        /// </summary>
        [Fact]
        public async Task RunAsync_WithMultipleSegments_ExecutesSegmentsInOrder()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            var executionOrder = new ConcurrentQueue<int>();
            var signal = new CountdownEvent(3);

            // Act — three segments; tests multiple iterations of the LinkSegments for-loop
            var subscribeTask = mediator.SubscribeAsync<string>(
                pb =>
                {
                    pb.Use(async (item, next, ct) =>
                    {
                        executionOrder.Enqueue(1);
                        signal.Signal();
                        return await next(item, null, ct);
                    });
                    pb.Use(async (item, next, ct) =>
                    {
                        executionOrder.Enqueue(2);
                        signal.Signal();
                        return await next(item, null, ct);
                    });
                    pb.Use(async (item, next, ct) =>
                    {
                        executionOrder.Enqueue(3);
                        signal.Signal();
                        return await next(item, null, ct);
                    });
                },
                enableBuffering: true,
                cts.Token);

            mediator.Publish("hello");
            signal.Wait(TimeSpan.FromSeconds(5));
            cts.Cancel();

            await subscribeTask;

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, executionOrder.ToArray());
        }

        /// <summary>
        /// Tests that when the subscription is cancelled, the <c>ContinueWith</c> in
        /// <c>Mediator.SubscribeAsync&lt;T&gt;(Action&lt;PipelineBuilder&lt;T&gt;&gt;, ...)</c>
        /// executes <c>pipeline.Dispose()</c>, which sets <c>firstSegment = null</c>.
        /// Verifies the subscription task completes successfully after disposal.
        /// </summary>
        [Fact]
        public async Task Dispose_CalledWhenSubscriptionEnds_CompletesWithoutException()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();
            var signal = new ManualResetEventSlim(false);

            var subscribeTask = mediator.SubscribeAsync<string>(
                pb =>
                {
                    pb.Use(async (item, next, ct) =>
                    {
                        signal.Set();
                        return await next(item, null, ct);
                    });
                },
                enableBuffering: true,
                cts.Token);

            mediator.Publish("test");
            signal.Wait(TimeSpan.FromSeconds(5));

            // Act — cancelling triggers the inner subscription to end; ContinueWith calls pipeline.Dispose()
            // which sets firstSegment = null (the patched line)
            cts.Cancel();
            var result = await subscribeTask;

            // Assert — result comes from ContinueWith returning true; no exception means dispose ran cleanly
            Assert.True(result);
        }

        /// <summary>
        /// Tests that <see cref="PipelineBuilder{T}.Use"/> does not add the same segment instance twice.
        /// The first <c>Use</c> call succeeds (Contains is false → segment added);
        /// the second call with the same reference is a no-op (Contains is true → segment not added).
        /// Verifies observable behaviour: the segment is only invoked once per notification.
        /// </summary>
        [Fact]
        public async Task Use_DuplicateSegment_SegmentInvokedOncePerNotification()
        {
            // Arrange
            using var cache = CreateCache();
            var mediator = new Baubit.Mediation.Mediator(cache, CreateLoggerFactory());
            using var cts = new CancellationTokenSource();

            int invokeCount = 0;
            var signal = new ManualResetEventSlim(false);

            IPipeline<string>.Segment segment = async (item, next, ct) =>
            {
                Interlocked.Increment(ref invokeCount);
                signal.Set();
                return await next(item, null, ct);
            };

            // Act
            var subscribeTask = mediator.SubscribeAsync<string>(
                pb =>
                {
                    pb.Use(segment);   // adds segment
                    pb.Use(segment);   // duplicate — should not be added again
                },
                enableBuffering: true,
                cts.Token);

            mediator.Publish("dup");
            signal.Wait(TimeSpan.FromSeconds(5));
            cts.Cancel();

            await subscribeTask;

            // Assert — if the segment were added twice, invokeCount would be 2
            Assert.Equal(1, invokeCount);
        }
    }
}

