# TavernOracle (酒馆先知)

<p align="center">
  <strong>专为《炉石传说·酒馆战棋》打造的极低延迟、万分级实时战术参谋 HUD 辅助系统</strong><br>
  <em>100% 遵守暴雪“纸笔准则（Pencil & Paper Rule）” —— 零内存注入、零按键模拟、零封号风险。</em>
</p>

<p align="center">
  <a href="README.md"><strong>👉 English Version (英文原版)</strong></a> |
  <a href="README_CN.md"><strong>简体中文</strong></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/平台-Windows-0078D6?logo=windows&logoColor=white" alt="Platform">
  <img src="https://img.shields.io/badge/端到端延迟-%3C%2015ms-brightgreen" alt="Latency">
  <img src="https://img.shields.io/badge/反作弊合规-纸笔准则%20(100%25%20安全)-success" alt="Anti-Cheat">
  <img src="https://img.shields.io/badge/开源协议-MIT-blue" alt="License">
</p>

---

## 📖 项目简介

**TavernOracle（酒馆先知）** 是一款运行于 Windows 平台的《炉石传说·酒馆战棋》（Hearthstone Battlegrounds）实时“战术参谋长”HUD 辅助软件。

在每回合紧张的 30 秒烧绳时间内，TavernOracle 能够在 **15 毫秒以内**完成盘面状态重构、流派推断、生死线概率评估与最优动作序列规划。为玩家提供**对标天梯万分级选手水准（85%~90%+ 吻合度）**的操作决策建议（涵盖**买入、卖出、刷新、冻结、升本与战力站位排列**），助力稳步冲分。

---

## 🛡️ 反作弊与安全合规准则（纸笔准则）

TavernOracle 严格遵守暴雪前炉石总监 Ben Brode 提出的**“纸笔准则（Pencil & Paper Rule）”**：*凡是玩家用一张纸和一支笔能够手动推算与记录的信息，软件予以数字化呈现完全合法合规。*

| 行为分类 | TavernOracle 实现方式 | 暴雪官方态度 | 封号风险 |
| :--- | :--- | :--- | :--- |
| **数据采集** | 只读解析本地官方调试日志 `Power.log` | 官方原生支持的日志流（与 HDT、Firestone 机理完全一致） | **0% (100% 安全)** |
| **表现层 HUD** | 被动透明悬浮窗，激活 `WS_EX_TRANSPARENT` 点击穿透 | 仅呈现视图层，所有鼠标按键直接穿透到底层炉石客户端 | **0% (100% 安全)** |
| **玩家交互** | 仅展示决策建议微标与顶部横条 | 所有游戏操作 100% 由玩家手动执行 | **0% (100% 安全)** |
| **违规行为** | ❌ **绝不**进行 DLL 注入、**绝不**修改内存、**绝不**包含鼠标连点/按键模拟（`mouse_event`/`SendInput`） | 自动化无人值守脚本（Botting）与内存纂改会被 Warden 判定为外挂 | **永久封号 (100%)** |

---

## ⚡ 核心系统架构

```
+-----------------------------------------------------------------------------------+
|                              炉石传说客户端 (Unity / Windows)                     |
+-----------------------------------------------------------------------------------+
                                          │ (官方调试流输出, 0ms 延迟, 零注入)
                                          ▼
+-----------------------------------------------------------------------------------+
| 【数据层】Power.log 流式状态机                                                     |
|  - 实时监听 %LocalAppData%\Blizzard\Hearthstone\Logs\Power.log                    |
|  - 映射开源 HearthDb 官方元数据，还原 EntityID、随从属性、手牌、酒馆随从、金币、星级 |
+-----------------------------------------------------------------------------------+
                                          │ (盘面结构化张量 / JSON)
                                          ▼
+-----------------------------------------------------------------------------------+
| 【决策大脑】双模动态决策引擎 (Dual-Engine Decision Brain)                          |
|                                                                                   |
|  [模式 A · 生产级基石] 贝叶斯流派后验 + 蒙特卡洛生死线 + 组合背包动态规划 (DP)       |
|    - 耗时: < 5ms  |  对标万分准确率: 85%+  |  版本更新冷启动: 0 毫秒                |
|                                                                                   |
|  [模式 B · 进阶增强] Set Transformer 跨实体自注意力 + 高分段 Replay 模仿学习         |
|    - 耗时: 50~100ms | 对标万分准确率: 90%+  |  机制语义向量化 (Zero-Shot)          |
+-----------------------------------------------------------------------------------+
                                          │ (决策推荐 Action 序列)
                                          ▼
+-----------------------------------------------------------------------------------+
| 【表现层】Windows 透明点击穿透 HUD (Presentation Layer)                            |
|  - Win32 API (WS_EX_TRANSPARENT | WS_EX_LAYERED)，完全不干扰玩家鼠标出牌           |
|  - 随从卡面上浮动态推荐 Badge（高亮对子、核心怪、战力评分）                         |
|  - 顶部横条显示大局策略：本回合升本/冻结/刷新建议与避坑提示                        |
+-----------------------------------------------------------------------------------+
```

---

## 🚀 核心特性

- **极致低时延预算（全链路 $< 15\text{ ms}$）**：
  - 日志解析 $\le 2\text{ ms}$
  - 状态机还原 $\le 3\text{ ms}$
  - 决策求解 $\le 5\text{ ms}$
  - HUD 渲染 $\le 5\text{ ms}$
- **动态战力生死线风控（$P_{lethal}$ 调节阀）**：
  - 通过蒙特卡洛千次对局模拟评估下一场暴毙概率 $P_{lethal}$。
  - 当 $P_{lethal} > 30\%$ 时，决策引擎自动切断贪心升本与留对子逻辑，强制切换为“保命战力最大化”，优先购买即插即用的身材与剧毒随从。
- **应对版本更迭零冷启动（Zero Cold-Start）**：
  - 模式 A 采用无需训练的参数化图模型与背包 DP，暴雪更新身材费用时仅需修改卡牌 JSON 属性配置，0 秒完成冷启动适配。
  - 模式 B 将卡牌投影至机制语义向量空间（原子关键词编码），新卡上线亦具备零样本（Zero-Shot）评分能力。
- **无感透明 HUD 交互**：
  - Win32 扩展样式确保鼠标原生操作毫秒不卡顿，随游戏窗口移动或缩放自适应像素吸附。

---

## 🛠️ 快速上手

### 1. 开启炉石官方日志输出

在你的 Windows 本地目录创建或修改 `log.config`：  
文件路径：`%LocalAppData%\Blizzard\Hearthstone\log.config`

```ini
[Power]
LogLevel=1
FilePrinting=true
ConsolePrinting=false
ScreenPrinting=false
Verbose=true
```

### 2. 验证数据流

启动炉石传说并进入任意酒馆战棋对局，游戏引擎将在以下路径生成实时行级事件：
```
%LocalAppData%\Blizzard\Hearthstone\Logs\Power.log
```

---

## 📚 项目指导文档索引

本项目严格遵循**渐进式披露原则**，所有技术规范与开发指南均存放在 [docs/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 目录：

- [AGENTS.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/AGENTS.md) — 根目录索引与 AI Agent 工作流准则。
- [docs/compliance.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/compliance.md) — 暴雪反作弊边界、合规审计与代码审查红线清单。
- [docs/architecture.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/architecture.md) — 三层架构拓扑、时延预算分配与版本解耦体系。
- [docs/data_layer.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/data_layer.md) — Power.log 流式数据摄取、状态机还原与开源库推荐。
- [docs/decision_engine.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/decision_engine.md) — 决策大脑：贝叶斯后验、背包 DP 与 Set Transformer 数学建模。
- [docs/presentation_hud.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/presentation_hud.md) — Windows 透明穿透悬浮窗、Win32 API 调用与卡面微标规范。
- [docs/roadmap_and_tech_stack.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/roadmap_and_tech_stack.md) — 技术路线选型（HDT 插件 vs Tauri 独立版）、4 周路线图。
- [docs/PROJECT_STARTUP.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/PROJECT_STARTUP.md) — 原始全景立项规格说明书全本。

---

## 🗺️ 4 周敏捷路线图

- **Week 1：数据链路打通** —— 编写 log.config、Power.log 流式捕获、HearthDb 随从元数据映射。
- **Week 2：决策大脑求解** —— 种族协同算子、蒙特卡洛战力模拟生死线、分支定界背包求解器。
- **Week 3：透明穿透 HUD** —— Win32 WS_EX_TRANSPARENT 悬浮窗、随从高亮徽章与顶部横条。
- **Week 4：天梯对局微调** —— 注入天梯胜率先验矩阵、英雄专属节奏线对齐、打包独立安装程序。

---

## ⚖️ 免责声明 (Disclaimer)

TavernOracle 是一款非官方的辅助工具，用于策略研究与游戏辅助。本项目与暴雪娱乐（Blizzard Entertainment）没有任何从属、赞助或官方合作关系。游戏资源与商标均归其各自所有者所有。

## 📄 开源许可 (License)

本项目基于 [MIT License](LICENSE) 开源。
