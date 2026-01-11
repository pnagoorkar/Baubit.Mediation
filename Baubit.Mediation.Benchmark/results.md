# Baubit.Mediation vs MediatR Benchmark Results

---

## Benchmark Run: January 10, 2026

### Environment

| Component | Version/Details |
|-----------|-----------------|
| Date | Jan 10, 2026 |
| OS | Windows 11 (10.0.26200.7462) |
| CPU | Intel Core Ultra 9 185H 2.50GHz, 16 physical cores (22 logical) |
| .NET SDK | 10.0.101 |
| Runtime | .NET 9.0.11 |
| Baubit.Mediation | Current (this repo) |
| Baubit.Caching | 2026.2.2-prerelease |
| MediatR | 12.4.1 |
| BenchmarkDotNet | 0.15.6 |

### Benchmark Scenarios

The benchmarks compare Baubit.Mediation against MediatR across key scenarios:

1. **Notification Aggregation** - Pub/sub notification delivery
2. **Async Mediation** - Single handler invocation (request/response)
3. **Parallel Load** - Concurrent request throughput (100 and 1000 operations)

### Results Summary

#### Scenario 1: Notification Aggregation

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Aggregation | 327-337 ns | 3.0M ops/sec | 289 B | 1.00x (baseline) |
| Baubit: Aggregation | **107-113 ns** | **8.8-9.4M ops/sec** | **72 B** | **3.0x faster** |

**Key Takeaway**: Baubit.Mediation's notification aggregation is **3.0x faster** than MediatR with **75% less memory allocation**. 

**Note**: Baubit's aggregation with `enableBuffering=false` provides cache-free direct delivery. With `enableBuffering=true`, notifications persist to cache, enabling message replay and distributed pub/sub capabilities that MediatR doesn't support.

#### Scenario 2: Async Mediation (Request/Response)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Async Mediation | 379-418 ns | 2.4-2.6M ops/sec | 361 B | 1.00x (baseline) |
| Baubit: Async Mediation | **64-65 ns** | **15.4-15.6M ops/sec** | **168 B** | **6.0x faster** |

**Key Takeaway**: Baubit.Mediation's async wrapper is **6.0x faster** than MediatR with **53% less memory allocation**.

#### Scenario 3: Parallel Load (100 concurrent operations)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Parallel (100 ops) | 7,291 ns | 137K ops/sec | 31.4 KB | 1.00x (baseline) |
| Baubit: Parallel (100 ops) | **2,671 ns** | **374K ops/sec** | **12.2 KB** | **2.7x faster** |

#### Scenario 4: Parallel Load (1,000 concurrent operations)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Parallel (1000 ops) | 73,355 ns | 13.6K ops/sec | 312 KB | 1.00x (baseline) |
| Baubit: Parallel (1000 ops) | **26,842 ns** | **37.3K ops/sec** | **120 KB** | **2.7x faster** |

**Key Takeaway**: Under parallel load, Baubit.Mediation processes requests **2.7x faster** with **61% less memory**.

### Raw Benchmark Output

```
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.7462)
Intel Core Ultra 9 185H 2.50GHz, 1 CPU, 22 logical and 16 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3
  Job-VAIYHK : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3

InvocationCount=10000  IterationCount=10  WarmupCount=3

| Method                                     | OperationCount | Mean         | Error        | StdDev       | Op/s         | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------- |--------------- |-------------:|-------------:|-------------:|-------------:|-------:|--------:|--------:|-------:|----------:|------------:|
| 'MediatR: Aggregation'                     | 100            |    336.88 ns |    17.438 ns |    10.377 ns |  2,968,416.1 |   0.81 |    0.04 |       - |      - |     289 B |        0.80 |
| 'Baubit: Aggregation'                      | 100            |    113.03 ns |     4.878 ns |     2.551 ns |  8,847,600.1 |   0.27 |    0.01 |       - |      - |      72 B |        0.20 |
| 'MediatR: Async Mediation'                 | 100            |    417.88 ns |    29.827 ns |    17.749 ns |  2,393,006.0 |   1.00 |    0.06 |       - |      - |     361 B |        1.00 |
| 'Baubit: Async Mediation'                  | 100            |     64.10 ns |     4.882 ns |     2.553 ns | 15,600,015.6 |   0.15 |    0.01 |       - |      - |     168 B |        0.47 |
| 'MediatR: Async Mediation (Parallel Load)' | 100            |  7,291.20 ns |   631.264 ns |   417.542 ns |    137,151.7 |  17.47 |    1.17 |  2.5000 |      - |   31393 B |       86.96 |
| 'Baubit: Async Mediation (Parallel Load)'  | 100            |  2,671.33 ns |   896.840 ns |   469.065 ns |    374,345.4 |   6.40 |    1.09 |  0.9000 |      - |   12192 B |       33.77 |
|                                            |                |              |              |              |              |        |         |         |        |           |             |
| 'MediatR: Aggregation'                     | 1000           |    326.45 ns |    10.851 ns |     5.675 ns |  3,063,232.8 |   0.86 |    0.02 |       - |      - |     289 B |        0.80 |
| 'Baubit: Aggregation'                      | 1000           |    106.87 ns |     3.373 ns |     1.764 ns |  9,357,272.4 |   0.28 |    0.01 |       - |      - |      72 B |        0.20 |
| 'MediatR: Async Mediation'                 | 1000           |    378.63 ns |     8.408 ns |     5.561 ns |  2,641,100.8 |   1.00 |    0.02 |       - |      - |     361 B |        1.00 |
| 'Baubit: Async Mediation'                  | 1000           |     64.76 ns |     1.637 ns |     0.856 ns | 15,441,034.5 |   0.17 |    0.00 |       - |      - |     168 B |        0.47 |
| 'MediatR: Async Mediation (Parallel Load)' | 1000           | 73,355.10 ns | 2,758.491 ns | 1,824.571 ns |     13,632.3 | 193.78 |    5.33 | 24.8000 | 6.2000 |  312193 B |      864.80 |
| 'Baubit: Async Mediation (Parallel Load)'  | 1000           | 26,841.79 ns | 4,228.689 ns | 2,797.016 ns |     37,255.3 |  70.91 |    7.11 |  9.5000 | 2.9000 |  120192 B |      332.94 |
```

---

## Benchmark Run: November 28, 2025

### Environment

| Component | Version/Details |
|-----------|-----------------|
| Date | Nov 28, 2025 |
| OS | Windows 11 (10.0.26200.7171) |
| CPU | Intel Core Ultra 9 185H 2.50GHz, 16 physical cores (22 logical) |
| .NET SDK | 10.0.100 |
| Runtime | .NET 9.0.11 |
| Baubit.Mediation | Current (this repo) |
| Baubit.Caching | 2025.48.7 |
| MediatR | 12.4.1 |
| BenchmarkDotNet | 0.15.6 |

### Benchmark Scenarios

The benchmarks compare Baubit.Mediation against MediatR across key scenarios:

1. **Notification Aggregation** - Pub/sub notification delivery
2. **Async Mediation** - Single handler invocation (request/response)
3. **Parallel Load** - Concurrent request throughput (100 and 1000 operations)

### Results Summary

### Scenario 1: Notification Aggregation

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Aggregation | 339-348 ns | 2.9M ops/sec | 289 B | 1.00x (baseline) |
| Baubit: Aggregation | **109-119 ns** | **9.2M ops/sec** | **72 B** | **3.1x faster** |

**Key Takeaway**: Baubit.Mediation's notification aggregation is **3.1x faster** than MediatR with **75% less memory allocation**. 

**Note**: Baubit's aggregation with `enableBuffering=false` provides cache-free direct delivery. With `enableBuffering=true`, notifications persist to cache, enabling message replay and distributed pub/sub capabilities that MediatR doesn't support.

### Scenario 2: Async Mediation (Request/Response)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Async Mediation | 425-434 ns | 2.3M ops/sec | 361 B | 1.00x (baseline) |
| Baubit: Async Mediation | **111 ns** | **9.0M ops/sec** | **240 B** | **3.9x faster** |

**Key Takeaway**: Baubit.Mediation's async wrapper is **3.9x faster** than MediatR with **34% less memory allocation**.

### Scenario 3: Parallel Load (100 concurrent operations)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Parallel (100 ops) | 7,986 ns | 125K ops/sec | 31.4 KB | 1.00x (baseline) |
| Baubit: Parallel (100 ops) | **4,745 ns** | **211K ops/sec** | **19.4 KB** | **1.7x faster** |

### Scenario 4: Parallel Load (1,000 concurrent operations)

| Method | Mean | Op/s | Allocated | Ratio vs MediatR |
|--------|------|------|-----------|------------------|
| MediatR: Parallel (1000 ops) | 73,250 ns | 13.7K ops/sec | 312 KB | 1.00x (baseline) |
| Baubit: Parallel (1000 ops) | **41,241 ns** | **24.2K ops/sec** | **192 KB** | **1.8x faster** |

**Key Takeaway**: Under parallel load, Baubit.Mediation processes requests **1.7-1.8x faster** with **38% less memory**.

## Performance Analysis

### Why Baubit.Mediation is Faster

1. **O(1) Handler Lookup**: Uses `ConcurrentDictionary<Type, IRequestHandler>` for constant-time handler resolution instead of LINQ-based iteration.

2. **Reduced Allocations**: Consistently allocates 34-75% less memory per operation vs MediatR.

3. **No DI Container Overhead**: Direct handler invocation without service provider resolution on each request.

4. **Efficient Type Caching**: Handler types are cached at registration time, avoiding repeated type checks.

### Architecture Trade-offs

| Feature | MediatR | Baubit.Mediation |
|---------|---------|------------------|
| Request/Response | ✓ | ✓ (3.9x faster) |
| Notifications | ✓ In-memory only | ✓ Cache-backed (3.1x faster) |
| Pipeline Behaviors | ✓ Built-in | Build outside mediator |
| Message Buffering | ✗ | ✓ Configurable (enableBuffering) |
| Direct Delivery | N/A | ✓ (enableBuffering=false) |
| Cache-backed Delivery | ✗ | ✓ (enableBuffering=true) |
| Message Replay | ✗ | ✓ |
| Distributed Messaging | ✗ | ✓ (via Baubit.Caching) |

## Recommendations

**Use Baubit.Mediation when**:
- Maximum throughput is critical (3-4x faster than MediatR)
- Memory efficiency matters (34-75% less allocation)
- You need notification aggregation with configurable buffering
- Message replay or distributed pub/sub capabilities are required
- You're building pipelines outside the mediator

**Use MediatR when**:
- You need built-in pipeline behaviors with minimal setup
- In-memory-only processing is sufficient
- Ecosystem compatibility with existing MediatR extensions is required

## Cache-Backed Async Mediation

Baubit.Mediation is powered by [Baubit.Caching](https://www.nuget.org/packages/Baubit.Caching/), a high-performance hybrid cache. Baubit.Caching is being extended to support distributed systems - once complete, Baubit.Mediation will natively support distributed mediation scenarios.

**Notification Buffering**:
- `enableBuffering=true`: Notifications persist to cache before delivery, enabling replay and distributed scenarios
- `enableBuffering=false`: Notifications bypass cache for minimal latency direct delivery

## Benchmark Configuration

```
BenchmarkDotNet v0.15.6
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 10000)]
Params: OperationCount = [100, 1000]
```

## Raw benchmark output
```
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.7171)
Intel Core Ultra 9 185H 2.50GHz, 1 CPU, 22 logical and 16 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3
  Job-VAIYHK : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3

InvocationCount=10000  IterationCount=10  WarmupCount=3
| Method                                     | OperationCount | Mean        | Error     | StdDev    | Op/s        | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------- |--------------- |------------:|----------:|----------:|------------:|-------:|--------:|--------:|-------:|----------:|------------:|
| 'MediatR: Aggregation'                     | 100            |    348.5 ns |  38.31 ns |  25.34 ns | 2,869,555.7 |   0.82 |    0.06 |       - |      - |     289 B |        0.80 |
| 'Baubit: Aggregation'                      | 100            |    118.8 ns |  17.70 ns |  11.71 ns | 8,415,454.1 |   0.28 |    0.03 |       - |      - |      72 B |        0.20 |
| 'MediatR: Async Mediation'                 | 100            |    424.9 ns |  26.43 ns |  15.73 ns | 2,353,451.9 |   1.00 |    0.05 |       - |      - |     361 B |        1.00 |
| 'Baubit: Async Mediation'                  | 100            |    110.6 ns |  12.46 ns |   6.52 ns | 9,044,862.5 |   0.26 |    0.02 |       - |      - |     240 B |        0.66 |
| 'MediatR: Async Mediation (Parallel Load)' | 100            |  7,986.4 ns | 674.77 ns | 446.32 ns |   125,213.6 |  18.82 |    1.21 |  2.5000 |      - |   31393 B |       86.96 |
| 'Baubit: Async Mediation (Parallel Load)'  | 100            |  4,744.5 ns | 477.86 ns | 316.08 ns |   210,771.8 |  11.18 |    0.82 |  1.5000 |      - |   19392 B |       53.72 |
|                                            |                |             |           |           |             |        |         |         |        |           |             |
| 'MediatR: Aggregation'                     | 1000           |    339.5 ns |  28.62 ns |  17.03 ns | 2,945,325.0 |   0.79 |    0.07 |       - |      - |     289 B |        0.80 |
| 'Baubit: Aggregation'                      | 1000           |    108.9 ns |   9.86 ns |   5.86 ns | 9,183,954.6 |   0.25 |    0.02 |       - |      - |      72 B |        0.20 |
| 'MediatR: Async Mediation'                 | 1000           |    433.8 ns |  53.36 ns |  35.29 ns | 2,305,007.9 |   1.01 |    0.11 |       - |      - |     361 B |        1.00 |
| 'Baubit: Async Mediation'                  | 1000           |    111.7 ns |   5.75 ns |   3.42 ns | 8,952,239.8 |   0.26 |    0.02 |       - |      - |     240 B |        0.66 |
| 'MediatR: Async Mediation (Parallel Load)' | 1000           | 73,250.3 ns | 930.25 ns | 615.30 ns |    13,651.8 | 169.88 |   13.58 | 24.8000 | 6.2000 |  312193 B |      864.80 |
| 'Baubit: Async Mediation (Parallel Load)'  | 1000           | 41,241.2 ns | 511.53 ns | 338.34 ns |    24,247.6 |  95.64 |    7.64 | 15.3000 | 4.1000 |  192192 B |      532.39 |
```

## How to Run

```bash
cd Baubit.Mediation.Benchmark
dotnet run -c Release