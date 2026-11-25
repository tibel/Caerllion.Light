using BenchmarkDotNet.Running;
using Caerllion.Light.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(TypedDispatchBenchmarks).Assembly).Run(args);
