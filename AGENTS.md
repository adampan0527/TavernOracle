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
| **原子任务与依赖 DAG** | [atomic_tasks_dag.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/atomic_tasks_dag.md) | 方案 A 全开发流程 23 个正交原子任务契约、开工状态推导与有向依赖拓扑 (DAG) |
| **开发规范与 UI 系统** | [dev_specifications.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/dev_specifications.md) | 代码与性能规范（零 GC、15ms 预算）、UI/UX 视觉系统、微标规范 |
| **立项说明书全本** | [PROJECT_STARTUP.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/PROJECT_STARTUP.md) | 项目立项原始全景规格说明书（全量归档） |

---

## 4. Agent 工作流协议 (Agent Protocol)

AI Agent 在本代码库工作时，必须严格执行以下工作流：

1. **查阅路由**：在接受用户任务后，首先查阅上表，定位相关的 [docs](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 文件并单点加载。
2. **合规自检**：涉及输入捕获或 UI 交互的代码变更，必须依据 [compliance.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/compliance.md) 校验是否触碰外挂/注入红线。
3. **渐进式沉淀**：新增架构设计、协议变更或核心模块规范时，应在 [docs/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) 下创建或更新对应专题文档，保持 `AGENTS.md` 作为简洁目录索引的纯粹性。

---

## 5. 可视化交互汇报规范 (HTML Interactive Reporting Protocol)

当用户询问“某个思路该怎么实现”或需要进行方案设计、算法演算、架构演示时，**AI Agent 必须通过交互式 HTML（结合图文、动效、流程演练等）进行直观汇报演示**。

汇报内容统一归档于根目录的 [report_html/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/report_html) 文件夹中，并严格遵守以下结构与规则：

### 5.1 目录组织与命名契约
- **根目录**：[report_html/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/report_html) 为汇报专用的根目录。
- **子目录命名**：每个独立汇报必须在 `report_html/` 下新建一个独立文件夹，统一命名为 **`3位有前导0的数字_简短名称`**（例如：`001_example`、`002_knapsack_dp_demo`、`003_hud_overlay_flow`），按序自增。
- **统一入口**：每个汇报子文件夹内**必须且只能以 `index.html` 作为统一入口文件**。
- **自包含与高内聚**：
  - 该汇报所需的子页面、样式表（CSS）、交互动效脚本（JS）、多媒体资源等，均必须整理归类在该子目录内部（如 `001_example/css/`、`001_example/js/`、`001_example/assets/`）。
  - 目录层次必须结构清晰、低耦合，便于用户查看与后续快速修改调整。

### 5.2 汇报产出标准
1. 生成完整的交互演示页面，确保可通过浏览器本地直接打开并顺畅交互。
2. 汇报完成后，在对话中向用户说明汇报核心摘要，并输出包含 `file://` 协议的可点击绝对路径（例如：`file:///Users/panyan/Desktop/coding_project/app/TavernOracle/report_html/001_example/index.html`）。

### 5.3 数学公式渲染标准与 CSS 避坑规范
**【历史教训与硬性底线】严禁在 HTML 页面中出现排版错乱、未编译源码或字符断裂：**
1. **公式渲染标准（自包含 KaTeX 引擎）**：
   - 涉及数学公式、变量推导（如 $P_{\text{lethal}}$、$w_{\text{tempo}}$、$\Delta V(a)$）时，汇报子目录必须就地引入自包含的 KaTeX 库（`lib/katex/`，包含 css、js 与 fonts），确保本地离线脱机时也能渲染出学术级数学排版。严禁在页面暴露未经编译的原始 `$` 源码。
2. **严防 CSS 破坏行内排版**：
   - 标题与文本容器（如 `h2`、`h3`、`<p>`）**严禁设置 `display: flex; gap: ...`**，否则会将行内的数学公式元素和普通文本节点拆解为独立 Flex 子项，造成灾难性的字符空格断裂（如出现 `( P lethal ...)` 错位）。
3. **离线可用与零失效依赖**：
   - 样式、脚本与字体必须完全自包含在汇报子目录内，避免依赖因网络波动或跨域问题导致加载失败的外部 CDN。

### 5.4 Playwright 自动化渲染验收协议 (Playwright Verification Protocol)
**【结束任务前的强制红线】所有 HTML 汇报在交付给用户前，必须通过 Playwright 自动化验证：**
1. 必须使用 Playwright 启动无头浏览器加载目标 `index.html`。
2. 自动化断言检验：
   - 确认 KaTeX 数学公式渲染元素（`.katex`）匹配存在，页面文本无裸露未编译字符。
   - 模拟执行关键交互逻辑（如滑块拖动、模式切换），确保 DOM 响应无异常。
   - 控制台零脚本错误（Zero Console Errors）。
3. 生成并视觉核验渲染截图无误后，方可向用户交付并输出汇报链接。
