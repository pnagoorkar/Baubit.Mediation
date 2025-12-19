using Baubit.Caching;
using Baubit.Caching.InMemory;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Baubit.Mediation.Benchmark
{
    /// <summary>
    /// Comprehensive benchmarks comparing Baubit.Mediation vs MediatR.
    /// Reports operations per second for practical performance assessment.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 10000)]
    [Config(typeof(Config))]
    public class MediatorBenchmarks
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                AddColumn(StatisticColumn.OperationsPerSecond);
            }
        }

        // Baubit.Mediation components
        private Mediator _baubitMediator = null!;
        private IOrderedCache<object> _cache = null!;
        private CancellationTokenSource _cts = null!;

        // MediatR components
        private MediatR.IMediator _mediatRMediator = null!;

        // Shared resources
        private ILoggerFactory _loggerFactory = null!;

        // Test data
        private BaubitRequest _baubitRequest = null!;
        private MediatRRequest _mediatRRequest = null!;
        private BaubitNotification _baubitNotification = null!;
        private MediatRNotification _mediatRNotification = null!;

        [Params(100, 1000)]
        public int OperationCount { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _loggerFactory = LoggerFactory.Create(b => { });

            // Create test data once
            _baubitRequest = new BaubitRequest { Value = 42 };
            _mediatRRequest = new MediatRRequest { Value = 42 };
            _baubitNotification = new BaubitNotification { Message = "Test" };
            _mediatRNotification = new MediatRNotification { Message = "Test" };
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Create fresh Baubit mediator and cache for each iteration
            var store = new Caching.InMemory.Store<object>(_loggerFactory);
            var metadata = new Metadata();
            _cache = new OrderedCache<object>(new Baubit.Caching.Configuration(), null, store, metadata, _loggerFactory);
            _baubitMediator = new Mediator(_cache, _loggerFactory);
            _cts = new CancellationTokenSource();

            // Register sync handler and notification subscriber
            var baubitHandler = new BaubitSyncHandler();
            _ = _baubitMediator.SubscribeAsync(new BaubitNotificationSubscriber(), false, _cts.Token);
            _baubitMediator.Subscribe<BaubitRequest, BaubitResponse>(baubitHandler, _cts.Token);

            // Setup MediatR without behaviors
            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRHandler).Assembly));
            _mediatRMediator = services.BuildServiceProvider().GetRequiredService<MediatR.IMediator>();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _baubitMediator?.Dispose();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _loggerFactory?.Dispose();
        }

        #region Aggregation Comparison

        [Benchmark(Description = "MediatR: Aggregation")]
        public async Task Notify_01_MediatR()
        {
            await _mediatRMediator.Publish(_mediatRNotification);
        }

        [Benchmark(Description = "Baubit: Aggregation")]
        public bool Notify_02_Baubit()
        {
            return _baubitMediator.Publish(_baubitNotification);
        }

        #endregion

        #region Mediation

        [Benchmark(Baseline = true, Description = "MediatR: Async Mediation")]
        public async Task<MediatRResponse> Simple_01_MediatR()
        {
            return await _mediatRMediator.Send(_mediatRRequest);
        }

        [Benchmark(Description = "Baubit: Async Mediation")]
        public async Task<BaubitResponse> Simple_02_Baubit_Async()
        {
            return await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        #endregion

        #region Mediation under parallel load

        [Benchmark(Description = "MediatR: Async Mediation (Parallel Load)")]
        public async Task Parallel_01_MediatR()
        {
            var tasks = new List<Task<MediatRResponse>>(OperationCount);
            for (int i = 0; i < OperationCount; i++)
            {
                tasks.Add(_mediatRMediator.Send(_mediatRRequest));
            }
            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Baubit: Async Mediation (Parallel Load)")]
        public async Task Parallel_02_Baubit_Async()
        {
            var tasks = new List<Task<BaubitResponse>>(OperationCount);
            for (int i = 0; i < OperationCount; i++)
            {
                tasks.Add(_baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest));
            }
            await Task.WhenAll(tasks);
        }
        #endregion
    }
}