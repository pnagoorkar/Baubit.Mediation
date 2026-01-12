```

BenchmarkDotNet v0.15.6, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763 2.89GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3
  Job-VAIYHK : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3

InvocationCount=10000  IterationCount=10  WarmupCount=3  

```
| Method                                     | OperationCount | Mean         | Error       | StdDev      | Op/s        | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------- |--------------- |-------------:|------------:|------------:|------------:|-------:|--------:|--------:|-------:|----------:|------------:|
| **&#39;MediatR: Aggregation&#39;**                     | **100**            |   **1,238.1 ns** |   **325.94 ns** |   **215.59 ns** |   **807,668.9** |   **1.09** |    **0.18** |       **-** |      **-** |     **289 B** |        **0.80** |
| &#39;Baubit: Aggregation&#39;                      | 100            |     305.3 ns |     7.40 ns |     4.41 ns | 3,275,541.1 |   0.27 |    0.00 |       - |      - |      64 B |        0.18 |
| &#39;MediatR: Async Mediation&#39;                 | 100            |   1,139.7 ns |    11.64 ns |     7.70 ns |   877,430.8 |   1.00 |    0.01 |       - |      - |     361 B |        1.00 |
| &#39;Baubit: Async Mediation&#39;                  | 100            |     312.3 ns |     3.52 ns |     2.10 ns | 3,202,123.0 |   0.27 |    0.00 |       - |      - |     168 B |        0.47 |
| &#39;MediatR: Async Mediation (Parallel Load)&#39; | 100            |  12,945.0 ns |   169.95 ns |   101.14 ns |    77,249.9 |  11.36 |    0.11 |  1.8000 |      - |   31393 B |       86.96 |
| &#39;Baubit: Async Mediation (Parallel Load)&#39;  | 100            |   7,041.1 ns |   232.59 ns |   138.41 ns |   142,023.0 |   6.18 |    0.12 |  0.7000 |      - |   12192 B |       33.77 |
|                                            |                |              |             |             |             |        |         |         |        |           |             |
| **&#39;MediatR: Aggregation&#39;**                     | **1000**           |   **1,110.0 ns** |    **14.23 ns** |     **9.41 ns** |   **900,927.5** |   **0.99** |    **0.01** |       **-** |      **-** |     **289 B** |        **0.80** |
| &#39;Baubit: Aggregation&#39;                      | 1000           |     292.9 ns |     6.97 ns |     4.61 ns | 3,413,877.4 |   0.26 |    0.00 |       - |      - |      64 B |        0.18 |
| &#39;MediatR: Async Mediation&#39;                 | 1000           |   1,122.3 ns |    17.36 ns |    11.48 ns |   891,049.6 |   1.00 |    0.01 |       - |      - |     361 B |        1.00 |
| &#39;Baubit: Async Mediation&#39;                  | 1000           |     321.9 ns |     1.61 ns |     0.84 ns | 3,106,861.5 |   0.29 |    0.00 |       - |      - |     168 B |        0.47 |
| &#39;MediatR: Async Mediation (Parallel Load)&#39; | 1000           | 125,012.5 ns | 1,593.86 ns | 1,054.24 ns |     7,999.2 | 111.40 |    1.40 | 18.6000 | 5.4000 |  312193 B |      864.80 |
| &#39;Baubit: Async Mediation (Parallel Load)&#39;  | 1000           |  61,399.2 ns |   650.48 ns |   430.25 ns |    16,286.9 |  54.71 |    0.64 |  7.1000 | 1.8000 |  120192 B |      332.94 |
