# AGENTS.md - TavernOracle (酒馆先知)

> **致所有在此项目工作的 AI Agent**：  
> 本文件为 **TavernOracle** 代码库的根导航索引。  
> 本项目严格遵循**渐进式披露原则（Progressive Disclosure Principle）**：`AGENTS.md` 仅包含高层项目概述与文档路由索引。在执行具体开发或审查任务时，**请勿盲目将全量文档载入上下文**，请根据当前任务主题，按需读取 [docs](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 目录中的对应指导文件。

---

## 1. 项目定位与核心使命 (Project Overview)

- **项目名称**：TavernOracle（酒馆先知）
- **一句话定义**：运行于 Windows 平台的《炉石传说·酒馆战棋》（Hearthstone Battlegrounds）实时“战术参谋长”HUD 辅助系统。
- **核心目标**：在 30 秒烧绳时间内以极低延迟（$< 15\text{ ms}$）分析实时局势，提供对标天梯万分级选手水准（85%~90%+ 吻合度）的操作决策推荐（包括买入、卖出、刷新、冻结、升本与战力站位排列）。
- **合规底线**：100% 遵守暴雪“纸笔准则（Pencil & Paper Rule）”，仅以透明穿透 HUD 呈现被动信息，零内存注入、零按键/鼠标模拟，确保账号 0 封号风险。

---

## 2. 核心设计原则与绝对红线 (Core Rules)

1. **绝对安全红线**：
   - 严禁引入任何模拟鼠标点击（`mouse_event` / `SendInput`）或键盘按键代码。
   - 严禁向炉石主进程进行 DLL 注入、内存篡改或数据包拦截。
2. **极低时延预算**：
   - 全链路处理（日志读取 -> 状态更新 -> 算法求解 -> HUD 呈现）必须控制在 $15\text{ ms}$ 以内。
3. **版本解耦（Zero Cold-Start）**：
   - 游戏补丁更新（随从身材/费用微调）不得要求重新训练主干模型，支持基于元数据配置文件的秒级冷启动。

---

## 3. 指导文档索引路由 (Progressive Documentation Index)

所有项目的详细技术规范与指导文件均存放在 [docs/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 目录下。请根据具体任务阅读对应文档：

| 关注领域 / 任务类型 | 指导文档 | 核心内容摘要 |
| :--- | :--- | :--- |
| **合规审计与安全规范** | [compliance.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/compliance.md) | 暴雪“纸笔准则”、反作弊边界、AI Agent 编码审查红线清单 |
| **总体架构与数据流** | [architecture.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/architecture.md) | 三层分层拓扑架构、15ms 时延预算分配、版本更迭解耦策略 |
| **日志摄取与状态机** | [data_layer.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/data_layer.md) | `Power.log` 官方日志配置、TagChange 核心标签解析规范、生态库推荐 |
| **决策算法与数学建模** | [decision_engine.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/decision_engine.md) | 模式 A（贝叶斯后验 + 战力生死线 + 组合背包 DP）与模式 B（Set Transformer 模仿学习）数学公式与推理拓扑 |
| **表现层与 HUD 悬浮窗** | [presentation_hud.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/presentation_hud.md) | Win32 API 点击穿透实现（`WS_EX_TRANSPARENT`）、卡面微标规范、窗口自适应吸附 |
| **技术选型与迭代规划** | [roadmap_and_tech_stack.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/roadmap_and_tech_stack.md) | HDT 插件方案 vs Tauri 独立版选型对比、4 周敏捷路线图、开源生态参考 |
| **立项说明书全本** | [PROJECT_STARTUP.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/PROJECT_STARTUP.md) | 项目立项原始全景规格说明书（全量归档） |

---

## 4. Agent 工作流协议 (Agent Protocol)

AI Agent 在本代码库工作时，必须严格执行以下工作流：

1. **查阅路由**：在接受用户任务后，首先查阅上表，定位相关的 [docs](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 文件并单点加载。
2. **合规自检**：涉及输入捕获或 UI 交互的代码变更，必须依据 [compliance.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/compliance.md) 校验是否触碰外挂/注入红线。
3. **渐进式沉淀**：新增架构设计、协议变更或核心模块规范时，应在 [docs/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 下创建或更新对应专题文档，保持 `AGENTS.md` 作为简洁目录索引的纯粹性。
