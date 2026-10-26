# 决策大脑：双模算法架构与数学建模 (Decision Engine & Modeling)

酒馆战棋要求在 30 秒绳子时间内对数万（$> 10^5$）个动作分支进行快速评估与剪枝。本项目采用分层混合双模架构：

---

## 1. 模式 A · 生产级基石：贝叶斯后验 + 战力生死线 + 组合背包动态规划 (DP)

模式 A 耗时 $< 5\text{ ms}$，对标天梯万分准确率 $85\%+$，版本更新零毫秒冷启动。

### 阶段一：贝叶斯流派推断 (Bayesian Archetype Posterior)
根据当前英雄 $H$、可用种族池 $B$ 以及场上核心组件 $C = \{c_1, c_2, ...\}$，计算当前应走向哪个终局构筑：

$$P(\text{流派}_k \mid H, C, B) \propto P(C \mid \text{流派}_k) \times P(\text{流派}_k \mid H, B)$$

若野兽流派后验概率达到 75%，则酒馆中所有野兽发动机组件的价值加成系数立即提高。

### 阶段二：蒙特卡洛战力生死线模拟 (Dynamic Risk-Aversion)
调用千次蒙特卡洛随机对战模拟（参考 Bob's Buddy），评估下一场暴毙概率 $P_{lethal}$：
- **即时战力保命权重**：$w_{tempo} = 1.0 + 3.0 \times (P_{lethal})^2$
- **远期成长贪婪权重**：$w_{scale} = 1.0 - 0.9 \times P_{lethal}$

> **策略突变**：当 $P_{lethal} > 30\%$ 时，$w_{scale} \to 0$。算法自动切断升本与留对子逻辑，强制进入“战力最大化苟活”，所有金币买即插即用的身材怪与剧毒。

### 阶段三：有界背包动态规划 (Bounded Knapsack DP)
定义动作边际收益：

$$\Delta V(a) = w_{tempo} \cdot \Delta\text{战力} + w_{scale} \cdot \Delta\text{协同} + \text{对子加成}$$

在金币预算 $\sum \text{Cost}(a_i) \le \text{Gold} + \text{Refund}$ 与 7 个随从卡槽约束下，利用**分支定界背包算法**在 **5 毫秒内**完成全局剪枝，输出全局最高分的操作序列。

---

## 2. 模式 B · 进阶增强：Set Transformer 离线行为克隆 (Imitation Learning)

模式 B 耗时 $50 \sim 100\text{ ms}$，对标职业选手操作吻合度 $90\%+$，具备微操（如防狂战斧站位、故意空过锁对子等）理解能力。

```
[7张在场随从 + 10张手牌 + 酒馆区随从]
                  │
                  ▼
[实体 Tokenizer 编码 (128维向量)]
  - CardID 机制 Embedding + 攻血标量 + 关键词 One-Hot (圣盾/剧毒/复生) + 槽位 pos
                  │
                  ▼
[Set Transformer 跨实体自注意力编码器]
  - 随从间自注意力 (Self-Attention) 提取场面构筑特征
  - 酒馆随从与场面做交叉注意力 (Cross-Attention) 评估加入后的全队质变增量
                  │
                  ▼
[双头网络输出 Dual Heads]
  ├─ 策略头 π(a|s): 输出原子动作概率分布 (买/卖/刷/升本)
  └─ 价值头 V(s): 输出预期终局排名回归值 (1.0 ~ 8.0)
```

- **数据来源**：抓取 HDT / Firestone 社区前 100 名顶尖职业选手（MMR > 10,000）的数百万局实战录像。
- **机制语义向量化 (Zero-Shot)**：卡牌统一映射到机制空间（攻血比、成长频次、增益范围等），新卡上线无需重新训练即可打分。
