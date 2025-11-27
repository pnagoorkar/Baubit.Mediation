using Baubit.Caching;
using Baubit.Caching.InMemory;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Baubit.Mediation.Benchmark
{
    /// <summary>
    /// Comprehensive benchmarks for Baubit.Mediation vs MediatR.
    /// Reports operations per second for practical performance assessment.
    /// Each benchmark iteration creates fresh mediator instances to ensure clean state.
    /// 
    /// Benchmark organization:
    /// - MediatR benchmarks appear first as baseline for each scenario
    /// - Baubit benchmarks follow immediately after for direct comparison
    /// - Grouped by: Request/Response, Notifications, Mixed Workloads, Concurrent
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

        // Mediators - recreated per iteration
        private Mediator _baubitMediator = null!;
        private MediatR.IMediator _mediatRMediator = null!;
        private IOrderedCache<object> _cache = null!;
        private CancellationTokenSource _cts = null!;

        // Mediator for async tests - recreated per iteration
        private Mediator _baubitMediatorAsync = null!;
        private IOrderedCache<object> _cacheAsync = null!;
        private CancellationTokenSource _ctsAsync = null!;

        // Shared resources (reused across iterations)
        private ILoggerFactory _loggerFactory = null!;

        // Test data (reused)
        private BaubitRequest _baubitRequest = null!;
        private MediatRRequest _mediatRRequest = null!;
        private BaubitNotification _baubitNotification = null!;
        private MediatRNotification _mediatRNotification = null!;

        [Params(100, 1_000)]
        public int OperationCount { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Setup shared resources that don't need to be recreated
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
            // Create fresh mediator and cache for each iteration
            var store = new Store<object>(_loggerFactory);
            var metadata = new Metadata();
            _cache = new OrderedCache<object>(new Baubit.Caching.Configuration(), null, store, metadata, _loggerFactory);
            _baubitMediator = new Mediator(_cache, _loggerFactory);
            _cts = new CancellationTokenSource();

            // Register sync handler
            var baubitHandler = new BaubitSyncHandler();
            _ = _baubitMediator.SubscribeAsync(new BaubitNotificationSubscriber(), _cts.Token);
            _baubitMediator.Subscribe<BaubitRequest, BaubitResponse>(baubitHandler, _cts.Token);

            // Create separate fresh mediator for async tests
            var storeAsync = new Store<object>(_loggerFactory);
            var metadataAsync = new Metadata();
            _cacheAsync = new OrderedCache<object>(new Baubit.Caching.Configuration(), null, storeAsync, metadataAsync, _loggerFactory);
            _baubitMediatorAsync = new Mediator(_cacheAsync, _loggerFactory);
            _ctsAsync = new CancellationTokenSource();

            // Start async handler in dedicated mediator
            var baubitAsyncHandler = new BaubitAsyncHandler();
            _ = _baubitMediatorAsync.SubscribeAsync<BaubitRequest, BaubitResponse>(baubitAsyncHandler, _ctsAsync.Token);

            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRHandler).Assembly));

            // Get fresh MediatR mediator from DI
            _mediatRMediator = services.BuildServiceProvider().GetRequiredService<MediatR.IMediator>();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            // Clean up mediators after each iteration
            _cts?.Cancel();
            _cts?.Dispose();
            _baubitMediator?.Dispose();

            _ctsAsync?.Cancel();
            _ctsAsync?.Dispose();
            _baubitMediatorAsync?.Dispose();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _loggerFactory?.Dispose();
        }

        #region Request/Response (Primary Comparison)

        [Benchmark(Baseline = true, Description = "MediatR: Send (Request/Response)")]
        public async Task<MediatRResponse> ReqResp_01_MediatR_Send()
        {
            return await _mediatRMediator.Send(_mediatRRequest);
        }

        [Benchmark(Description = "Baubit: PublishAsync (Sync Handler, Request/Response)")]
        public async Task<BaubitResponse> ReqResp_02_Baubit_PublishAsync_SyncHandler()
        {
            return await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        [Benchmark(Description = "Baubit: Publish (Sync Handler, Request/Response)")]
        public BaubitResponse ReqResp_03_Baubit_Publish_Sync()
        {
            return _baubitMediator.Publish<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        [Benchmark(Description = "Baubit: PublishAsyncAsync (Async Handler) [NO MEDIATR EQUIVALENT]")]
        public async Task<BaubitResponse> ReqResp_04_Baubit_PublishAsyncAsync_AsyncHandler()
        {
            return await _baubitMediatorAsync.PublishAsyncAsync<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        #endregion

        #region Notifications

        [Benchmark(Description = "MediatR: Publish (Notification)")]
        public async Task Notif_01_MediatR_Publish()
        {
            await _mediatRMediator.Publish(_mediatRNotification);
        }

        [Benchmark(Description = "Baubit: Publish (Notification)")]
        public bool Notif_02_Baubit_Publish()
        {
            return _baubitMediator.Publish(_baubitNotification);
        }

        #endregion

        #region Mixed Workload (Reads + Writes)

        [Benchmark(Description = "MediatR: Mixed 80% Read, 20% Write")]
        public async Task Mixed_01_MediatR_80Read_20Write()
        {
            // 4 reads
            for (int i = 0; i < 4; i++)
            {
                await _mediatRMediator.Send(_mediatRRequest);
            }

            // 1 write (notification)
            await _mediatRMediator.Publish(_mediatRNotification);
        }

        [Benchmark(Description = "Baubit: Mixed 80% Read, 20% Write")]
        public async Task Mixed_02_Baubit_80Read_20Write()
        {
            // 4 reads
            for (int i = 0; i < 4; i++)
            {
                await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);
            }

            // 1 write (notification)
            _baubitMediator.Publish(_baubitNotification);
        }

        [Benchmark(Description = "MediatR: Mixed 50% Read, 50% Write")]
        public async Task Mixed_03_MediatR_50Read_50Write()
        {
            // 1 read
            await _mediatRMediator.Send(_mediatRRequest);

            // 1 write (notification)
            await _mediatRMediator.Publish(_mediatRNotification);
        }

        [Benchmark(Description = "Baubit: Mixed 50% Read, 50% Write")]
        public async Task Mixed_04_Baubit_50Read_50Write()
        {
            // 1 read
            await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);

            // 1 write (notification)
            _baubitMediator.Publish(_baubitNotification);
        }

        #endregion

        #region Concurrent Calls

        [Benchmark(Description = "MediatR: Concurrent Send")]
        public async Task Conc_01_MediatR_Concurrent_Send()
        {
            var tasks = new List<Task<MediatRResponse>>(OperationCount);
            for (int i = 0; i < OperationCount; i++)
            {
                tasks.Add(_mediatRMediator.Send(_mediatRRequest));
            }
            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Baubit: Concurrent PublishAsync (Sync Handler)")]
        public async Task Conc_02_Baubit_Concurrent_PublishAsync()
        {
            var tasks = new List<Task<BaubitResponse>>(OperationCount);
            for (int i = 0; i < OperationCount; i++)
            {
                tasks.Add(_baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest));
            }
            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Baubit: Concurrent Publish (Sync Handler)")]
        public void Conc_03_Baubit_Concurrent_Publish_Sync()
        {
            var results = new ConcurrentBag<BaubitResponse>();
            Parallel.For(0, OperationCount, _ =>
            {
                results.Add(_baubitMediator.Publish<BaubitRequest, BaubitResponse>(_baubitRequest));
            });
        }

        #endregion
    }
}
