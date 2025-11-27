# Baubit.Mediation vs MediatR Benchmark Results

## Environment

| Component | Version/Details |
|-----------|-----------------|
| OS | Linux (Ubuntu) on Azure |
| CPU | AMD EPYC 7763 64-Core Processor |
| .NET SDK | 10.0.100 |
| Runtime | .NET 9.0 |
| Baubit.Mediation | Current (this repo) |
| Baubit.Caching | 2025.48.7 |
| MediatR | 12.4.1 |
| BenchmarkDotNet | 0.15.6 |

## Benchmark Scenarios

The benchmarks compare Baubit.Mediation against MediatR across three key scenarios:

1. **Simple Request/Response** - Single handler invocation
2. **Pipeline with Behaviors** - Request processing with pre/post behaviors
3. **Parallel Load** - Concurrent request throughput

## Results Summary

### Scenario 1: Simple Request/Response

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Simple Request/Response | 932 ns | 1,072,857 | 361 B | 1.00x (baseline) |
| Baubit: Simple Request/Response (Async) | 396 ns | 2,522,978 | 240 B | **2.4x faster** |
| Baubit: Simple Request/Response (Sync) | **44 ns** | **22,659,831** | **24 B** | **21x faster** |

**Key Takeaway**: Baubit.Mediation's sync path is **21x faster** than MediatR with **15x less memory allocation**.

### Scenario 2: Pipeline with Behaviors

| Method | Mean | Op/s | Allocated |
|--------|------|------|-----------|
| MediatR: With Pipeline Behaviors | 2,117 ns | 472,465 | 889 B |
| Baubit: Request + Notification (Pre/Post) | 34,269 ns | 29,181 | 955 B |

**Note**: MediatR's pipeline behaviors are optimized for in-memory processing, while Baubit's notification system uses cache-backed persistence which adds overhead but provides durability and distributed messaging capabilities.

### Scenario 3: Parallel Request Load (1,000 concurrent operations)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Parallel Load | 133 µs | 7,524 | 312 KB | 1.00x (baseline) |
| Baubit: Parallel Load (Async) | 67 µs | 15,029 | 192 KB | **2x faster** |
| Baubit: Parallel Load (Sync) | 58 µs | 17,279 | 49 KB | **2.3x faster** |

**Key Takeaway**: Under parallel load, Baubit.Mediation processes requests **2-2.3x faster** with **40-85% less memory**.

### Notification Publishing

| Method | Mean | Op/s | Allocated |
|--------|------|------|-----------|
| MediatR: Notification Publish | 899 ns | 1,112,427 | 289 B |
| Baubit: Notification Publish | 5,764 ns | 173,483 | 391 B |

**Note**: Baubit's notification system writes to a persistent cache, enabling features like message replay and distributed pub/sub that MediatR doesn't support.

## Performance Analysis

### Why Baubit.Mediation is Faster

1. **O(1) Handler Lookup**: Uses `ConcurrentDictionary<Type, IRequestHandler>` for constant-time handler resolution instead of LINQ-based iteration.

2. **Reduced Allocations**: Sync path allocates only 24 bytes per operation vs MediatR's 361 bytes.

3. **No DI Container Overhead**: Direct handler invocation without service provider resolution on each request.

4. **Efficient Type Caching**: Handler types are cached at registration time, avoiding repeated type checks.

### Trade-offs

| Feature | MediatR | Baubit.Mediation |
|---------|---------|------------------|
| Simple Request/Response | ✓ | ✓ (21x faster) |
| Pipeline Behaviors | ✓ (optimized) | Via Notifications (cache-backed) |
| Notifications | In-memory only | Cache-backed (durable) |
| Distributed Messaging | ✗ | ✓ (via cache) |
| Message Replay | ✗ | ✓ |

## Recommendations

- **Use Baubit sync path** (`Publish<TRequest, TResponse>`) for maximum performance in hot paths
- **Use MediatR pipeline behaviors** if you need in-memory-only pre/post processing with minimal latency
- **Use Baubit notifications** if you need message durability, replay, or distributed messaging

## Benchmark Configuration

```
BenchmarkDotNet v0.15.6
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 10000)]
Params: OperationCount = [100, 1000]
```

## How to Run

```bash
cd Baubit.Mediation.Benchmark
dotnet run -c Release
```
