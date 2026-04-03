```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8037)
12th Gen Intel Core i9-12900K, 1 CPU, 24 logical and 16 physical cores
.NET SDK 9.0.311
  [Host]     : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2


```
| Method                | RecordCount | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|---------------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| DefaultPipeline       | 1000        | 1.008 s | 0.0029 s | 0.0027 s |  1.00 |   1.15 MB |        1.00 |
| TunedParallelPipeline | 1000        | 1.010 s | 0.0031 s | 0.0029 s |  1.00 |   1.14 MB |        0.99 |
