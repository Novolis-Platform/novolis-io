using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<Novolis.IO.Ndjson.Benchmarks.NdjsonBenchmarks>(DefaultConfig.Instance, args);
