```

BenchmarkDotNet v0.15.6, Linux Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3
  Job-YFEFPZ : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3

IterationCount=10  WarmupCount=3  

```
| Method                   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Baubit_Publish_Sync      |  68.05 ns | 0.794 ns | 0.525 ns |  1.00 |    0.01 | 0.0052 |      88 B |        1.00 |
| Baubit_PublishAsync_Sync | 124.77 ns | 3.588 ns | 2.373 ns |  1.83 |    0.04 | 0.0181 |     304 B |        3.45 |
| MediatR_Send             | 154.68 ns | 4.046 ns | 2.676 ns |  2.27 |    0.04 | 0.0215 |     360 B |        4.09 |
