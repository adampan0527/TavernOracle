namespace TavernOracle.Benchmarks;

using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using TavernOracle.Data.Replays;
using TavernOracle.Data.State;
using TavernOracle.Decision;

/// <summary>
/// 离线基准性能与零 GC 压测套件基准测试 (T21 BenchmarkDotNet Suite)。
/// 循环遍历全部 20 组真实复杂录像快照，度量 DP 求解、站位重排与边际价值评估的全链路延迟分布与内存分配。
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class DecisionPipelineBenchmark
{
    private IDecisionPipeline _pipeline = null!;
    private BoardSnapshot[] _allSnapshots = null!;
    private int _index;

    private BoardSnapshot _s01Leapfrogger;
    private BoardSnapshot _s05LethalCrisis;
    private BoardSnapshot _s18TripleHunt;
    private BoardSnapshot _s20Complex10Gold;

    /// <summary>
    /// 初始化测试环境与 20 组对局快照
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _pipeline = DecisionPipeline.Instance;

        var replays = ReplayDatasetLoader.Instance.LoadAllReplays();
        if (replays.Count < 20)
        {
            replays = ReplayDatasetFactory.CreateAll();
        }

        _allSnapshots = replays.Values.ToArray();

        _s01Leapfrogger = replays.TryGetValue(ReplayDatasetFactory.S01_LeapfroggerEarlyTransition, out var s1)
            ? s1 : ReplayDatasetFactory.Create01LeapfroggerEarlyTransition();

        _s05LethalCrisis = replays.TryGetValue(ReplayDatasetFactory.S05_LethalEmergencyHp7Turn8, out var s5)
            ? s5 : ReplayDatasetFactory.Create05LethalEmergencyHp7Turn8();

        _s18TripleHunt = replays.TryGetValue(ReplayDatasetFactory.S18_MultiPairTripleHunt, out var s18)
            ? s18 : ReplayDatasetFactory.Create18MultiPairTripleHunt();

        _s20Complex10Gold = replays.TryGetValue(ReplayDatasetFactory.S20_Complex10GoldBranchingBudget, out var s20)
            ? s20 : ReplayDatasetFactory.Create20Complex10GoldBranchingBudget();
    }

    /// <summary>
    /// 全链路求解：20 组真实录像轮询压测 (DP 求解 + 站位排序 + 价值评估)
    /// </summary>
    [Benchmark(Baseline = true, Description = "全链路求解: 20 组录像循环轮询 (DP + 站位 + 价值)")]
    public int FullPipeline_All20Replays_RoundRobin()
    {
        ref readonly var snapshot = ref _allSnapshots[_index++ % _allSnapshots.Length];
        var result = _pipeline.Execute(in snapshot);
        return result.ActionResult.Count + result.PositioningResult.Count;
    }

    /// <summary>
    /// 典型战局 1: 跳蛙转折 (S01 - Leapfrogger Early Transition)
    /// </summary>
    [Benchmark(Description = "场景 1: 跳蛙转折 (S01 - Leapfrogger Transition)")]
    public int Solve_Scenario01_Leapfrogger()
    {
        var result = _pipeline.Execute(in _s01Leapfrogger);
        return result.ActionResult.Count + result.PositioningResult.Count;
    }

    /// <summary>
    /// 典型战局 2: 绝地保命 (S05 - Lethal Emergency HP 7)
    /// </summary>
    [Benchmark(Description = "场景 5: 绝地保命 (S05 - Lethal Crisis HP 7)")]
    public int Solve_Scenario05_LethalCrisis()
    {
        var result = _pipeline.Execute(in _s05LethalCrisis);
        return result.ActionResult.Count + result.PositioningResult.Count;
    }

    /// <summary>
    /// 典型战局 3: 多对子三连搜寻 (S18 - Multi-Pair Triple Hunt)
    /// </summary>
    [Benchmark(Description = "场景 18: 多对子三连 (S18 - MultiPair Triple Hunt)")]
    public int Solve_Scenario18_TripleHunt()
    {
        var result = _pipeline.Execute(in _s18TripleHunt);
        return result.ActionResult.Count + result.PositioningResult.Count;
    }

    /// <summary>
    /// 典型战局 4: 10 铸币复杂分支 (S20 - 10 Gold Budget)
    /// </summary>
    [Benchmark(Description = "场景 20: 10 铸币复杂分支 (S20 - 10 Gold Budget)")]
    public int Solve_Scenario20_Complex10Gold()
    {
        var result = _pipeline.Execute(in _s20Complex10Gold);
        return result.ActionResult.Count + result.PositioningResult.Count;
    }

    /// <summary>
    /// 单点组件：启发式随从站位重排 (7 随从)
    /// </summary>
    [Benchmark(Description = "单点组件: 启发式随从站位重排 (7 随从)")]
    public int Component_PositionOptimizer_Only()
    {
        var res = _pipeline.PositionOptimizer.OptimizePosition(in _s20Complex10Gold);
        return res.Count;
    }

    /// <summary>
    /// 单点组件：有界背包 DP 剪枝求解器 (S20 复杂局)
    /// </summary>
    [Benchmark(Description = "单点组件: 有界背包 DP 剪枝求解器 (S20 复杂局)")]
    public int Component_KnapsackSolver_Only()
    {
        var res = _pipeline.KnapsackSolver.Solve(in _s20Complex10Gold);
        return res.Count;
    }
}
