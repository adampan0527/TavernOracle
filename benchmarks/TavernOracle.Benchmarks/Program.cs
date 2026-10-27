namespace TavernOracle.Benchmarks;

using System;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Running;
using TavernOracle.Data.Replays;
using TavernOracle.Data.State;
using TavernOracle.Decision;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 若传入了 BenchmarkDotNet 参数 (例如 --bdn, --filter, -f, --job)
        if (args.Length > 0 && args.Any(a => a.Equals("--bdn", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("--benchmark", StringComparison.OrdinalIgnoreCase) ||
                                             a.StartsWith("--filter", StringComparison.OrdinalIgnoreCase) ||
                                             a.StartsWith("-f", StringComparison.OrdinalIgnoreCase)))
        {
            var bdnArgs = args.Where(a => !a.Equals("--bdn", StringComparison.OrdinalIgnoreCase) &&
                                          !a.Equals("--benchmark", StringComparison.OrdinalIgnoreCase)).ToArray();

            Console.WriteLine("[TavernOracle.Benchmarks] 启动 BenchmarkDotNet 引擎基准分析...");
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(bdnArgs);
            return 0;
        }

        // 默认模式：运行 10,000 次离线基准性能与零 GC 压测套件
        return RunStressBenchmarkSuite(iterations: 10_000);
    }

    private static int RunStressBenchmarkSuite(int iterations)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("      TavernOracle (酒馆先知) - T21 离线基准性能与零 GC 压测套件");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"[1/4] 载入离线盘面对局回放测试数据集 (T06)...");

        var loader = ReplayDatasetLoader.Instance;
        var replaysMap = loader.LoadAllReplays();
        if (replaysMap.Count < 20)
        {
            replaysMap = ReplayDatasetFactory.CreateAll();
        }

        var snapshots = replaysMap.Values.ToArray();
        int replayCount = snapshots.Length;
        Console.WriteLine($"      已成功加载 {replayCount} 组标准对局快照 (涵盖跳蛙转折、绝地保命、多对子三连等)。");

        var pipeline = DecisionPipeline.Instance;

        // 预热阶段
        Console.WriteLine($"[2/4] 执行 JIT Tier-1 预热 (3,000 次跨场景求解)...");
        for (int w = 0; w < 3_000; w++)
        {
            ref readonly var warmSnap = ref snapshots[w % replayCount];
            var warmRes = pipeline.Execute(in warmSnap);
            _ = warmRes.ActionResult.Count;
            _ = warmRes.PositioningResult.Count;
        }

        System.Threading.Thread.Sleep(100);

        // 预分配数组，避免测试代码本身干扰堆内存
        long[] latenciesTicks = new long[iterations];

        // 内存基线清理
        Console.WriteLine($"[3/4] 强制垃圾回收清理基线，就绪 10,000 次热路径高频求解...");
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        int gen0Start = GC.CollectionCount(0);
        int gen1Start = GC.CollectionCount(1);
        int gen2Start = GC.CollectionCount(2);

        // 开始压测
        var totalSw = Stopwatch.StartNew();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        int dummyActionCountSum = 0;
        double dummyScoreSum = 0;
        int dummyPositionCountSum = 0;

        for (int i = 0; i < iterations; i++)
        {
            ref readonly var snapshot = ref snapshots[i % replayCount];

            long start = Stopwatch.GetTimestamp();
            var result = pipeline.Execute(in snapshot);
            long end = Stopwatch.GetTimestamp();

            latenciesTicks[i] = end - start;

            dummyActionCountSum += result.ActionResult.Count;
            dummyScoreSum += result.ActionResult.TotalScore;
            dummyPositionCountSum += result.PositioningResult.Count;
        }

        totalSw.Stop();
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        int gen0Delta = GC.CollectionCount(0) - gen0Start;
        int gen1Delta = GC.CollectionCount(1) - gen1Start;
        int gen2Delta = GC.CollectionCount(2) - gen2Start;
        long totalAllocated = allocatedAfter - allocatedBefore;

        // 计算统计分位数
        double[] latenciesMs = new double[iterations];
        double tickToMs = 1000.0 / Stopwatch.Frequency;
        for (int i = 0; i < iterations; i++)
        {
            latenciesMs[i] = latenciesTicks[i] * tickToMs;
        }

        Array.Sort(latenciesMs);

        double minMs = latenciesMs[0];
        double p50Ms = latenciesMs[(int)(iterations * 0.50)];
        double p90Ms = latenciesMs[(int)(iterations * 0.90)];
        double p95Ms = latenciesMs[(int)(iterations * 0.95)];
        double p99Ms = latenciesMs[(int)(iterations * 0.99)];
        double maxMs = latenciesMs[iterations - 1];
        double avgMs = latenciesMs.Average();
        double totalExecutionMs = totalSw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"[4/4] 压测计算完成！生成性能度量报告：");
        Console.WriteLine();
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine("                         实测性能时延指标 (Latency)");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"  - 连续求解迭代总数:     {iterations:N0} 次");
        Console.WriteLine($"  - 求解总耗时:           {totalExecutionMs:F2} ms");
        Console.WriteLine($"  - 平均求解时延 (Avg):   {avgMs:F4} ms ({avgMs * 1000.0:F1} μs)  [硬指标: <= 3.5 ms  -> {(avgMs <= 3.5 ? "PASS PASS PASS" : "FAIL")}]");
        Console.WriteLine($"  - 最小时延 (Min):       {minMs:F4} ms ({minMs * 1000.0:F1} μs)");
        Console.WriteLine($"  - 中位数时延 (P50):     {p50Ms:F4} ms ({p50Ms * 1000.0:F1} μs)");
        Console.WriteLine($"  - 90分位时延 (P90):     {p90Ms:F4} ms ({p90Ms * 1000.0:F1} μs)");
        Console.WriteLine($"  - 95分位时延 (P95):     {p95Ms:F4} ms ({p95Ms * 1000.0:F1} μs)");
        Console.WriteLine($"  - 99分位时延 (P99):     {p99Ms:F4} ms ({p99Ms * 1000.0:F1} μs)  [硬指标: <= 4.8 ms  -> {(p99Ms <= 4.8 ? "PASS PASS PASS" : "FAIL")}]");
        Console.WriteLine($"  - 最大时延 (Max):       {maxMs:F4} ms ({maxMs * 1000.0:F1} μs)");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine("                   垃圾回收与热路径内存分配 (Zero GC)");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"  - Gen0 GC 触发回收次数: {gen0Delta} 次  [硬指标: 0 次  -> {(gen0Delta == 0 ? "PASS" : "FAIL")}]");
        Console.WriteLine($"  - Gen1 GC 触发回收次数: {gen1Delta} 次  [硬指标: 0 次  -> {(gen1Delta == 0 ? "PASS" : "FAIL")}]");
        Console.WriteLine($"  - Gen2 GC 触发回收次数: {gen2Delta} 次  [硬指标: 0 次  -> {(gen2Delta == 0 ? "PASS" : "FAIL")}]");
        Console.WriteLine($"  - 热路径托管堆分配内存: {totalAllocated} 字节  [硬指标: 0 字节  -> {(totalAllocated == 0 ? "PASS" : "FAIL")}]");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine($"  - 状态校验 CheckSum:    Actions={dummyActionCountSum}, Positions={dummyPositionCountSum}, ScoreSum={dummyScoreSum:F2}");
        Console.WriteLine("================================================================================");
        Console.WriteLine("提示：可通过 `dotnet run -c Release -- --bdn` 启动完整的 BenchmarkDotNet 统计分析。");
        Console.WriteLine();

        bool pass = (avgMs <= 3.5) && (p99Ms <= 4.8) && (gen0Delta == 0) && (gen1Delta == 0) && (gen2Delta == 0) && (totalAllocated == 0);
        return pass ? 0 : 1;
    }
}
