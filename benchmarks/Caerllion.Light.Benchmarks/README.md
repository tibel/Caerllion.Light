# Performance benchmarks

The BenchmarkDotNet suite measures end-to-end batches of 512 messages for typed synchronous
dispatch, already-completed asynchronous dispatch, RPC, and `Actor`. Dispatch benchmarks vary
typed-handler fan-out (1, 4, or 16 handlers). `MemoryDiagnoser` reports time and allocated bytes
per message; actor and RPC workloads report their own per-message costs.

Run a quick smoke/estimate with:

```powershell
dotnet run --configuration Release --project .\benchmarks\Caerllion.Light.Benchmarks -- --filter "*" --job short
```

For comparison-quality measurements, omit `--job short` and run on an otherwise idle machine. Results
are hardware- and runtime-dependent; compare runs on the same machine and runtime.
