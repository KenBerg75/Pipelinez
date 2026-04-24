using Pipelinez.Benchmarks;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromTypes(BenchmarkEnvironment.GetRegisteredBenchmarkTypes()).Run(args);
