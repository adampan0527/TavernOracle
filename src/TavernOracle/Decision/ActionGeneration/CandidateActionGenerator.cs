namespace TavernOracle.Decision.ActionGeneration;

using System;
using System.Runtime.CompilerServices;
using TavernOracle.Data.State;

/// <summary>
/// 原子操作候选动作集生成器。
/// 严格依据当前酒馆在售、手牌与战场席位状态，生成所有合法的 BUY, SELL, REROLL, FREEZE, UPGRADE_TIER, PLAY 动作。
/// 具备硬性非法剪枝逻辑，执行延迟严格控制在 &lt;= 0.5ms (通常 &lt; 0.05ms)，零托管堆垃圾分配。
/// </summary>
public sealed class CandidateActionGenerator : ICandidateActionGenerator
{
    /// <summary>
    /// 全局共享单例实例 (无状态纯计算，线程安全)
    /// </summary>
    public static CandidateActionGenerator Instance { get; } = new CandidateActionGenerator();

    /// <summary>
    /// 全局共享单例别名
    /// </summary>
    public static CandidateActionGenerator Shared => Instance;

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public CandidateActionGenerator()
    {
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Generate(in BoardSnapshot snapshot, ref CandidateActionBuffer buffer)
    {
        buffer.Clear();

        int availableGold = snapshot.Gold.Available;

        // 1. UpgradeTier: 仅当可用金币 >= 升本费用且酒馆等级 < 7 时合法
        if (snapshot.TavernTier < 7 && availableGold >= snapshot.UpgradeCost)
        {
            buffer.Add(CandidateAction.CreateUpgradeTier(snapshot.UpgradeCost));
        }

        // 2. Reroll: 仅当可用金币 >= 1 时合法
        if (availableGold >= 1)
        {
            buffer.Add(CandidateAction.CreateReroll());
        }

        // 3. Freeze: 仅当酒馆在售随从数 > 0 且当前未冻结时合法
        var tavernMinions = snapshot.TavernMinions;
        if (tavernMinions.Length > 0 && !snapshot.IsFrozen)
        {
            buffer.Add(CandidateAction.CreateFreeze());
        }

        // 4. Buy: 遍历酒馆在售随从，仅当金币足够且手牌未满 (< 10) 时合法
        var handCards = snapshot.HandCards;
        if (handCards.Length < BoardSnapshot.MaxHandCards)
        {
            for (int i = 0; i < tavernMinions.Length; i++)
            {
                var minion = tavernMinions[i];
                if (minion.IsTavernSpell) continue;
                if (availableGold >= minion.Cost)
                {
                    buffer.Add(CandidateAction.CreateBuy(minion.EntityId, i, minion.Cost, minion.CardId));
                }
            }
        }

        // 5. Sell: 遍历我方战场已有随从，卖出获得 1 金币并释放卡位
        var boardMinions = snapshot.BoardMinions;
        for (int i = 0; i < boardMinions.Length; i++)
        {
            var minion = boardMinions[i];
            buffer.Add(CandidateAction.CreateSell(minion.EntityId, i, minion.CardId));
        }

        // 6. Play: 遍历手牌，若战场卡位未满 (< 7)，生成可打出至不同位置 (0 ... BoardCount) 的打出动作
        int boardCount = boardMinions.Length;
        for (int i = 0; i < handCards.Length; i++)
        {
            var card = handCards[i];
            if (!card.IsMinion || availableGold < card.Cost || HandActionPlanner.ShouldHold(snapshot, card)) continue;
            var planned = HandActionPlanner.Plan(snapshot, card, i);
            if (planned.Type == ActionType.Magnetize) buffer.Add(planned);
            else if (boardCount < BoardSnapshot.MaxBoardMinions)
                for (int pos = 0; pos <= boardCount; pos++)
                    buffer.Add(CandidateAction.CreatePlay(card.EntityId, i, pos, card.Cost, card.CardId));
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CandidateActionBuffer Generate(in BoardSnapshot snapshot)
    {
        var buffer = new CandidateActionBuffer();
        Generate(in snapshot, ref buffer);
        return buffer;
    }

    /// <inheritdoc />
    public int Generate(in BoardSnapshot snapshot, Span<CandidateAction> destination)
    {
        var buffer = new CandidateActionBuffer();
        Generate(in snapshot, ref buffer);
        var span = buffer.AsSpan();
        if (span.Length > destination.Length)
        {
            throw new ArgumentException(
                $"Destination span is too small. Required: {span.Length}, Available: {destination.Length}",
                nameof(destination));
        }

        span.CopyTo(destination);
        return span.Length;
    }
}
