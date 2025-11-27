```

BenchmarkDotNet v0.15.6, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3
  Job-VAIYHK : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3

InvocationCount=10000  IterationCount=10  WarmupCount=3  

```
| Method                                                              | OperationCount | Mean         | Error        | StdDev      | Op/s        | Ratio  | RatioSD | Gen0    | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------------------------------------------------------------- |--------------- |-------------:|-------------:|------------:|------------:|-------:|--------:|--------:|-------:|-------:|----------:|------------:|
| **&#39;MediatR: Send (Request/Response)&#39;**                                  | **100**            |   **1,175.6 ns** |     **31.73 ns** |    **18.88 ns** |   **850,598.0** |   **1.00** |    **0.02** |       **-** |      **-** |      **-** |     **361 B** |        **1.00** |
| &#39;Baubit: PublishAsync (Sync Handler, Request/Response)&#39;             | 100            |     573.8 ns |      9.38 ns |     4.91 ns | 1,742,735.2 |   0.49 |    0.01 |       - |      - |      - |     304 B |        0.84 |
| &#39;Baubit: Publish (Sync Handler, Request/Response)&#39;                  | 100            |     224.9 ns |      6.31 ns |     3.30 ns | 4,446,608.2 |   0.19 |    0.00 |       - |      - |      - |      88 B |        0.24 |
| &#39;Baubit: PublishAsyncAsync (Async Handler) [NO MEDIATR EQUIVALENT]&#39; | 100            |  17,721.7 ns |  8,660.10 ns | 5,728.12 ns |    56,427.9 |  15.08 |    4.66 |  0.2000 |      - |      - |    3717 B |       10.30 |
| &#39;MediatR: Publish (Notification)&#39;                                   | 100            |   1,093.2 ns |     12.16 ns |     8.04 ns |   914,773.8 |   0.93 |    0.02 |       - |      - |      - |     289 B |        0.80 |
| &#39;Baubit: Publish (Notification)&#39;                                    | 100            |   3,423.9 ns |    355.33 ns |   235.03 ns |   292,063.1 |   2.91 |    0.20 |       - |      - |      - |     445 B |        1.23 |
| &#39;MediatR: Mixed 80% Read, 20% Write&#39;                                | 100            |   4,663.4 ns |  1,717.67 ns | 1,136.13 ns |   214,436.5 |   3.97 |    0.92 |       - |      - |      - |    1441 B |        3.99 |
| &#39;Baubit: Mixed 80% Read, 20% Write&#39;                                 | 100            |           NA |           NA |          NA |          NA |      ? |       ? |      NA |     NA |     NA |        NA |           ? |
| &#39;MediatR: Mixed 50% Read, 50% Write&#39;                                | 100            |   2,287.0 ns |    523.00 ns |   345.93 ns |   437,257.9 |   1.95 |    0.28 |       - |      - |      - |     577 B |        1.60 |
| &#39;Baubit: Mixed 50% Read, 50% Write&#39;                                 | 100            |           NA |           NA |          NA |          NA |      ? |       ? |      NA |     NA |     NA |        NA |           ? |
| &#39;MediatR: Concurrent Send&#39;                                          | 100            |  13,729.5 ns |    270.95 ns |   179.22 ns |    72,835.6 |  11.68 |    0.23 |  1.8000 |      - |      - |   31393 B |       86.96 |
| &#39;Baubit: Concurrent PublishAsync (Sync Handler)&#39;                    | 100            |  12,312.3 ns |    156.38 ns |   103.44 ns |    81,219.9 |  10.48 |    0.18 |  1.5000 |      - |      - |   25792 B |       71.45 |
| &#39;Baubit: Concurrent Publish (Sync Handler)&#39;                         | 100            |  27,537.3 ns |  1,946.10 ns | 1,287.22 ns |    36,314.4 |  23.43 |    1.10 |  0.7000 | 0.6000 |      - |   13215 B |       36.61 |
|                                                                     |                |              |              |             |             |        |         |         |        |        |           |             |
| **&#39;MediatR: Send (Request/Response)&#39;**                                  | **1000**           |   **1,146.3 ns** |    **101.48 ns** |    **60.39 ns** |   **872,370.2** |   **1.00** |    **0.08** |       **-** |      **-** |      **-** |     **361 B** |        **1.00** |
| &#39;Baubit: PublishAsync (Sync Handler, Request/Response)&#39;             | 1000           |     595.3 ns |      5.09 ns |     2.66 ns | 1,679,818.6 |   0.52 |    0.03 |       - |      - |      - |     304 B |        0.84 |
| &#39;Baubit: Publish (Sync Handler, Request/Response)&#39;                  | 1000           |     202.5 ns |      3.38 ns |     2.23 ns | 4,938,440.9 |   0.18 |    0.01 |       - |      - |      - |      88 B |        0.24 |
| &#39;Baubit: PublishAsyncAsync (Async Handler) [NO MEDIATR EQUIVALENT]&#39; | 1000           |  21,924.0 ns | 12,671.77 ns | 8,381.59 ns |    45,612.1 |  19.18 |    7.09 |  0.2000 |      - |      - |    3717 B |       10.30 |
| &#39;MediatR: Publish (Notification)&#39;                                   | 1000           |   1,041.3 ns |     75.11 ns |    49.68 ns |   960,357.0 |   0.91 |    0.07 |       - |      - |      - |     289 B |        0.80 |
| &#39;Baubit: Publish (Notification)&#39;                                    | 1000           |   3,235.8 ns |    187.18 ns |   123.81 ns |   309,046.7 |   2.83 |    0.19 |       - |      - |      - |     442 B |        1.22 |
| &#39;MediatR: Mixed 80% Read, 20% Write&#39;                                | 1000           |   3,421.7 ns |  2,725.10 ns | 1,802.48 ns |   292,250.4 |   2.99 |    1.52 |       - |      - |      - |    1441 B |        3.99 |
| &#39;Baubit: Mixed 80% Read, 20% Write&#39;                                 | 1000           |           NA |           NA |          NA |          NA |      ? |       ? |      NA |     NA |     NA |        NA |           ? |
| &#39;MediatR: Mixed 50% Read, 50% Write&#39;                                | 1000           |   2,269.0 ns |    744.84 ns |   492.67 ns |   440,723.5 |   1.98 |    0.43 |       - |      - |      - |     577 B |        1.60 |
| &#39;Baubit: Mixed 50% Read, 50% Write&#39;                                 | 1000           |           NA |           NA |          NA |          NA |      ? |       ? |      NA |     NA |     NA |        NA |           ? |
| &#39;MediatR: Concurrent Send&#39;                                          | 1000           | 135,716.5 ns |  1,731.65 ns | 1,030.48 ns |     7,368.3 | 118.73 |    6.81 | 18.6000 | 5.4000 |      - |  312193 B |      864.80 |
| &#39;Baubit: Concurrent PublishAsync (Sync Handler)&#39;                    | 1000           | 119,931.0 ns |  1,503.22 ns |   994.29 ns |     8,338.1 | 104.92 |    6.03 | 15.3000 | 4.5000 |      - |  256192 B |      709.67 |
| &#39;Baubit: Concurrent Publish (Sync Handler)&#39;                         | 1000           | 220,133.5 ns |  5,085.99 ns | 3,364.07 ns |     4,542.7 | 192.58 |   11.31 |  6.9000 | 6.8000 | 0.7000 |  113243 B |      313.69 |

Benchmarks with issues:
  MediatorBenchmarks.'Baubit: Mixed 80% Read, 20% Write': Job-VAIYHK(InvocationCount=10000, IterationCount=10, WarmupCount=3) [OperationCount=100]
  MediatorBenchmarks.'Baubit: Mixed 50% Read, 50% Write': Job-VAIYHK(InvocationCount=10000, IterationCount=10, WarmupCount=3) [OperationCount=100]
  MediatorBenchmarks.'Baubit: Mixed 80% Read, 20% Write': Job-VAIYHK(InvocationCount=10000, IterationCount=10, WarmupCount=3) [OperationCount=1000]
  MediatorBenchmarks.'Baubit: Mixed 50% Read, 50% Write': Job-VAIYHK(InvocationCount=10000, IterationCount=10, WarmupCount=3) [OperationCount=1000]
