using Baubit.Caching;
using Baubit.Caching.InMemory;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Baubit.Mediation.Benchmark
{
    /// <summary>
    /// Comprehensive benchmarks comparing Baubit.Mediation vs MediatR.
    /// 
    /// Benchmark scenarios:
    /// 1. Simple request/response handler
    /// 2. Request pipeline with behaviors (pre/post) - MediatR behaviors vs Baubit notifications
    /// 3. Parallel request load / throughput scenario
    /// 
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
        private MediatR.IMediator _mediatRMediatorWithBehaviors = null!;

        // Shared resources
        private ILoggerFactory _loggerFactory = null!;

        // Test data
        private BaubitRequest _baubitRequest = null!;
        private MediatRRequest _mediatRRequest = null!;
        private BaubitNotification _baubitNotification = null!;
        private MediatRNotification _mediatRNotification = null!;

        [Params(100, 1_000)]
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
            var store = new Store<object>(_loggerFactory);
            var metadata = new Metadata();
            _cache = new OrderedCache<object>(new Baubit.Caching.Configuration(), null, store, metadata, _loggerFactory);
            _baubitMediator = new Mediator(_cache, _loggerFactory);
            _cts = new CancellationTokenSource();

            // Register sync handler and notification subscriber
            var baubitHandler = new BaubitSyncHandler();
            _ = _baubitMediator.SubscribeAsync(new BaubitNotificationSubscriber(), _cts.Token);
            _baubitMediator.Subscribe<BaubitRequest, BaubitResponse>(baubitHandler, _cts.Token);

            // Setup MediatR without behaviors
            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRHandler).Assembly));
            _mediatRMediator = services.BuildServiceProvider().GetRequiredService<MediatR.IMediator>();

            // Setup MediatR with pipeline behaviors (pre/post processing)
            var servicesWithBehaviors = new ServiceCollection();
            servicesWithBehaviors.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(MediatRHandler).Assembly);
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRRequest, MediatRResponse>, LoggingBehavior<MediatRRequest, MediatRResponse>>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRRequest, MediatRResponse>, ValidationBehavior<MediatRRequest, MediatRResponse>>();
            });
            _mediatRMediatorWithBehaviors = servicesWithBehaviors.BuildServiceProvider().GetRequiredService<MediatR.IMediator>();
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

        #region Scenario 1: Simple Request/Response Handler

        [Benchmark(Baseline = true, Description = "MediatR: Simple Request/Response")]
        public async Task<MediatRResponse> Simple_01_MediatR()
        {
            return await _mediatRMediator.Send(_mediatRRequest);
        }

        [Benchmark(Description = "Baubit: Simple Request/Response (Async)")]
        public async Task<BaubitResponse> Simple_02_Baubit_Async()
        {
            return await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        [Benchmark(Description = "Baubit: Simple Request/Response (Sync)")]
        public BaubitResponse Simple_03_Baubit_Sync()
        {
            return _baubitMediator.Publish<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        #endregion

        #region Scenario 2: Request Pipeline with Behaviors (Pre/Post Processing)

        [Benchmark(Description = "MediatR: With Pipeline Behaviors")]
        public async Task<MediatRResponse> Pipeline_01_MediatR_WithBehaviors()
        {
            return await _mediatRMediatorWithBehaviors.Send(_mediatRRequest);
        }

        [Benchmark(Description = "Baubit: Request + Notification (Pre/Post)")]
        public async Task<BaubitResponse> Pipeline_02_Baubit_WithNotifications()
        {
            // Simulate pre-processing via notification
            _baubitMediator.Publish(new BaubitNotification { Message = "Pre" });

            // Execute request
            var response = await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);

            // Simulate post-processing via notification
            _baubitMediator.Publish(new BaubitNotification { Message = "Post" });

            return response;
        }

        #endregion

        #region Scenario 3: Parallel Request Load / Throughput

        [Benchmark(Description = "MediatR: Parallel Load")]
        public async Task Parallel_01_MediatR()
        {
            var tasks = new List<Task<MediatRResponse>>(OperationCount);
            for (int i = 0; i < OperationCount; i++)
            {
                tasks.Add(_mediatRMediator.Send(_mediatRRequest));
            }
            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Baubit: Parallel Load (Async)")]
        public async Task Parallel_02_Baubit_Async()
        {
            var tasks = new List<Task<BaubitResponse>>(OperationCount);
            for (int i = 0; i < OperationCount; i++)
            {
                tasks.Add(_baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest));
            }
            await Task.WhenAll(tasks);
        }

        [Benchmark(Description = "Baubit: Parallel Load (Sync via Parallel.For)")]
        public void Parallel_03_Baubit_Sync()
        {
            var results = new ConcurrentBag<BaubitResponse>();
            Parallel.For(0, OperationCount, _ =>
            {
                results.Add(_baubitMediator.Publish<BaubitRequest, BaubitResponse>(_baubitRequest));
            });
        }

        #endregion

        #region Notifications Comparison

        [Benchmark(Description = "MediatR: Notification Publish")]
        public async Task Notify_01_MediatR()
        {
            await _mediatRMediator.Publish(_mediatRNotification);
        }

        [Benchmark(Description = "Baubit: Notification Publish")]
        public bool Notify_02_Baubit()
        {
            return _baubitMediator.Publish(_baubitNotification);
        }

        #endregion
    }

    #region MediatR Pipeline Behaviors

    /// <summary>
    /// Simulates logging behavior in MediatR pipeline.
    /// </summary>
    public class LoggingBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Pre-processing: simulate logging entry
            _ = request.GetType().Name;

            var response = await next();

            // Post-processing: simulate logging exit
            _ = response?.GetType().Name;

            return response;
        }
    }

    /// <summary>
    /// Simulates validation behavior in MediatR pipeline.
    /// </summary>
    public class ValidationBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Pre-processing: simulate validation (accessing request property)
            _ = request.GetType().Name;

            return await next();
        }
    }

    #endregion
}
