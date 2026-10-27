using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TavernOracle.Data.Replays;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Decision.Positioning;
using Xunit;
using Xunit.Abstractions;

namespace TavernOracle.Tests.Benchmarks;

/// <summary>
/// 离线基准性能与零 GC 压测套件 (T21)。
/// 循环载入全部 20 组复杂战局录像快照，连续执行 10,000 次决策求解 (DP 求解 + 站位排序 + 价值评估)。
/// 精确度量 P50、P90、P99 时延与托管堆 GC 分配，严格校验性能与零 GC 契约：
/// 1. 平均时延 &lt;= 3.5ms (实测力争 &lt; 1.0ms);
/// 2. P99 时延 &lt;= 4.8ms (远低于 15ms 战术决策时延上限);
/// 3. 每千次推理产生的 Gen0/1/2 GC 回收次数为 0，求解热路径总分配内存为 0 字节 (Zero GC Allocation)。
/// </summary>
public sealed class DecisionPipelineStressTests
{
    private readonly ITestOutputHelper _output;
    private readonly IDecisionPipeline _pipeline;
    private readonly BoardSnapshot[] _allReplaySnapshots;

    public DecisionPipelineStressTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = DecisionPipeline.Instance;

        // 通过 T06 离线回放数据加载服务获取全部 20 组标准录像快照
        var loader = ReplayDatasetLoader.Instance;
        var replaysMap = loader.LoadAllReplays();
        if (replaysMap.Count < 20)
        {
            replaysMap = ReplayDatasetFactory.CreateAll();
        }

        _allReplaySnapshots = replaysMap.Values.ToArray();
        Assert.True(_allReplaySnapshots.Length >= 20, $"Expected at least 20 snapshots, but found {_allReplaySnapshots.Length}");
    }

    /// <summary>
    /// 验证全部 20 组录像场景在独立循环求解下 100% 保持 0 堆内存分配。
    /// </summary>
    [Fact]
    public void StressTest_All20Replays_IndividualZeroGCVerification()
    {
        for (int idx = 0; idx < _allReplaySnapshots.Length; idx++)
        {
            var snapshot = _allReplaySnapshots[idx];

            // 预热
            for (int w = 0; w < 50; w++)
            {
                var r = _pipeline.Execute(in snapshot);
                _ = r.ActionResult.Count;
                _ = r.PositioningResult.Count;
            }

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            const int iter = 100;
            for (int i = 0; i < iter; i++)
            {
                var r = _pipeline.Execute(in snapshot);
                _ = r.ActionResult.Count;
                _ = r.PositioningResult.Count;
            }

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            long delta = allocatedAfter - allocatedBefore;

            _output.WriteLine($"Replay #{idx + 1:D2} [{snapshot.HeroCardId}, Turn {snapshot.Turn}]: Allocated = {delta} bytes for {iter} iterations");
            Assert.Equal(0, delta);
        }
    }

    /// <summary>
    /// 核心压测标准：
    /// 1. 遍历并循环载入全部 20 组复杂录像快照；
    /// 2. 连续执行 10,000 次决策求解 (DP 求解 + 站位排序 + 价值评估)；
    /// 3. 精确度量 P50、P90、P99 时延与内存指标；
    /// 4. 硬性验收线：
    ///    - 平均时延 &lt;= 3.5ms
    ///    - P99 时延 &lt;= 4.8ms
    ///    - 每千次推理产生的 Gen0/Gen1/Gen2 GC 回收次数为 0，求解热路径总分配内存为 0 字节。
    /// </summary>
    [Fact]
    public void StressTest_10000Iterations_MustMeetLatencyAndZeroGCAllocationCriteria()
    {
        const int iterations = 10_000;
        int replayCount = _allReplaySnapshots.Length;

        // 1. 预热 JIT 编译与分支预测 (执行 3,000 次跨场景预热以触发并稳定 Tier-1 JIT 优化)
        for (int w = 0; w < 3_000; w++)
        {
            ref readonly var warmSnap = ref _allReplaySnapshots[w % replayCount];
            var warmRes = _pipeline.Execute(in warmSnap);
            _ = warmRes.ActionResult.Count;
            _ = warmRes.PositioningResult.Count;
        }

        // 等待后台 JIT Tiering 线程完成代码生成
        System.Threading.Thread.Sleep(100);

        // 2. 预先分配采样缓冲区，严防测试框架自身的数组分配污染压测区间的线程内存度量
        long[] latenciesTicks = new long[iterations];

        // 3. 强制清理 GC 基线
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        int gen0Start = GC.CollectionCount(0);
        int gen1Start = GC.CollectionCount(1);
        int gen2Start = GC.CollectionCount(2);

        // 4. 开始 10,000 次严苛压测
        var totalSw = Stopwatch.StartNew();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        int dummyActionCountSum = 0;
        double dummyScoreSum = 0;
        int dummyPositionCountSum = 0;

        for (int i = 0; i < iterations; i++)
        {
            ref readonly var snapshot = ref _allReplaySnapshots[i % replayCount];

            long start = Stopwatch.GetTimestamp();
            var result = _pipeline.Execute(in snapshot);
            long end = Stopwatch.GetTimestamp();

            latenciesTicks[i] = end - start;

            dummyActionCountSum += result.ActionResult.Count;
            dummyScoreSum += result.ActionResult.TotalScore;
            dummyPositionCountSum += result.PositioningResult.Count;
        }

        totalSw.Stop();
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        // 5. 垃圾回收与堆内存统计
        int gen0Delta = GC.CollectionCount(0) - gen0Start;
        int gen1Delta = GC.CollectionCount(1) - gen1Start;
        int gen2Delta = GC.CollectionCount(2) - gen2Start;
        long totalAllocated = allocatedAfter - allocatedBefore;

        // 6. 统计时延分布指标 (P50, P90, P95, P99, Max, Avg)
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

        // 7. 输出详细工程基准性能与 GC 报告
        _output.WriteLine("================================================================================");
        _output.WriteLine("       TavernOracle T21 Decision Pipeline 10,000 次连续求解压测报告");
        _output.WriteLine("================================================================================");
        _output.WriteLine($"  - 覆盖录像快照集:       {replayCount} 组 (包含跳蛙转折、绝地保命、多对子三连、APM极限等)");
        _output.WriteLine($"  - 连续求解迭代总数:     {iterations:N0} 次");
        _output.WriteLine($"  - 求解总耗时:           {totalExecutionMs:F2} ms");
        _output.WriteLine($"  - 平均求解时延 (Avg):   {avgMs:F4} ms ({avgMs * 1000.0:F1} μs)  [硬指标: <= 3.5 ms]");
        _output.WriteLine($"  - 最小时延 (Min):       {minMs:F4} ms ({minMs * 1000.0:F1} μs)");
        _output.WriteLine($"  - 中位数时延 (P50):     {p50Ms:F4} ms ({p50Ms * 1000.0:F1} μs)");
        _output.WriteLine($"  - 90分位时延 (P90):     {p90Ms:F4} ms ({p90Ms * 1000.0:F1} μs)");
        _output.WriteLine($"  - 95分位时延 (P95):     {p95Ms:F4} ms ({p95Ms * 1000.0:F1} μs)");
        _output.WriteLine($"  - 99分位时延 (P99):     {p99Ms:F4} ms ({p99Ms * 1000.0:F1} μs)  [硬指标: <= 4.8 ms]");
        _output.WriteLine($"  - 最大时延 (Max):       {maxMs:F4} ms ({maxMs * 1000.0:F1} μs)");
        _output.WriteLine("--------------------------------------------------------------------------------");
        _output.WriteLine("       垃圾回收与热路径堆内存分配度量 (Zero GC Contract)");
        _output.WriteLine("--------------------------------------------------------------------------------");
        _output.WriteLine($"  - Gen0 GC 触发回收次数: {gen0Delta} 次  [硬指标: 0 次]");
        _output.WriteLine($"  - Gen1 GC 触发回收次数: {gen1Delta} 次  [硬指标: 0 次]");
        _output.WriteLine($"  - Gen2 GC 触发回收次数: {gen2Delta} 次  [硬指标: 0 次]");
        _output.WriteLine($"  - 热路径托管堆分配内存: {totalAllocated} 字节  [硬指标: 0 字节]");
        _output.WriteLine("--------------------------------------------------------------------------------");
        _output.WriteLine($"  - 状态校验 CheckSum:    Actions={dummyActionCountSum}, Positions={dummyPositionCountSum}, ScoreSum={dummyScoreSum:F2}");
        _output.WriteLine("================================================================================");

        // 8. 校验计算有效性 (防止编译器消除死代码)
        Assert.True(dummyActionCountSum > 0, "决策流水线必须生成有效推荐动作");
        Assert.True(dummyPositionCountSum > 0, "决策流水线必须生成有效随从站位");
        Assert.True(dummyScoreSum > 0, "推荐动作序列总得分必须为正");

        // 9. 核心验收断言 (DAG 契约)
        // 验收项 1: 平均耗时 <= 3.5ms
        Assert.True(avgMs <= 3.5, $"决策流水线平均耗时 {avgMs:F4} ms 超出 3.5 ms 硬指标！");

        // 验收项 2: P99 耗时 <= 4.8ms
        Assert.True(p99Ms <= 4.8, $"决策流水线 P99 耗时 {p99Ms:F4} ms 超出 4.8 ms 硬指标！");

        // 验收项 3: GC 回收次数均为 0
        Assert.Equal(0, gen0Delta);
        Assert.Equal(0, gen1Delta);
        Assert.Equal(0, gen2Delta);

        // 验收项 4: 10,000 次热路径求解堆内存总分配严格为 0 字节
        Assert.Equal(0, totalAllocated);
    }
}
