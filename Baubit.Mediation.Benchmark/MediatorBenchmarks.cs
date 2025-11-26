
using Baubit.Caching;
using Baubit.Caching.InMemory;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Baubit.Mediation.Benchmark
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class MediatorBenchmarks
    {
        private Mediator _baubitMediator = null!;
        private MediatR.IMediator _mediatRMediator = null!;
        private IOrderedCache<object> _cache = null!;
        private CancellationTokenSource _cts = null!;
        private BaubitRequest _baubitRequest = null!;
        private MediatRRequest _mediatRRequest = null!;

        [GlobalSetup]
        public void Setup()
        {
            // Setup Baubit.Mediation
            var loggerFactory = LoggerFactory.Create(b => { });
            var store = new Store<object>(loggerFactory);
            var metadata = new Metadata();
            _cache = new OrderedCache<object>(new Baubit.Caching.Configuration(), null, store, metadata, loggerFactory);
            _baubitMediator = new Mediator(_cache, loggerFactory);
            _cts = new CancellationTokenSource();

            var baubitHandler = new BaubitSyncHandler();
            _baubitMediator.Subscribe<BaubitRequest, BaubitResponse>(baubitHandler, _cts.Token);

            // Setup MediatR
            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRHandler).Assembly));
            var provider = services.BuildServiceProvider();
            _mediatRMediator = provider.GetRequiredService<MediatR.IMediator>();

            // Create test requests
            _baubitRequest = new BaubitRequest { Value = 42 };
            _mediatRRequest = new MediatRRequest { Value = 42 };
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _cts.Cancel();
            _cts.Dispose();
            _baubitMediator.Dispose();
        }

        [Benchmark(Baseline = true)]
        public BaubitResponse Baubit_Publish_Sync()
        {
            return _baubitMediator.Publish<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        [Benchmark]
        public async Task<BaubitResponse> Baubit_PublishAsync_Sync()
        {
            return await _baubitMediator.PublishAsync<BaubitRequest, BaubitResponse>(_baubitRequest);
        }

        [Benchmark]
        public async Task<MediatRResponse> MediatR_Send()
        {
            return await _mediatRMediator.Send(_mediatRRequest);
        }
    }
}
