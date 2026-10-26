# TavernOracle 开发规范指南 (Development Specifications & Design System)

本文档制定了 **TavernOracle** 项目的代码架构规范、技术栈选择标准、UI/UX 视觉系统规范以及不可逾越的工程合规红线。

---

## 1. 技术栈选型规范 (Tech Stack Architecture)

系统根据模块职责划分为三个技术梯队：

| 领域 | 推荐语言 / 框架 | 职责与定位 | 选用依据与标准 |
| :--- | :--- | :--- | :--- |
| **主运行时 (首选路线 A)** | **C# (.NET 8 / Framework 4.8)** | HDT 插件宿主、日志解析、DP 背包搜索、WPF HUD | 完美复用开源生态：`HearthDb` (全卡牌元数据)、`Bob's Buddy` (蒙特卡洛模拟)、`LogWatcher` (稳定日志流)。1~2 周即可交付 MVP。 |
| **主运行时 (备选路线 B)** | **Rust + Tauri + TypeScript / React** | 独立轻量客户端、Webview2 透明置顶 HUD | 不依赖 HDT 宿主，极低内存开销（< 40MB），高颜值跨平台前端 UI。 |
| **离线 AI 研究与训练** | **Python 3.10+ (PyTorch / PyG)** | 高分局录像回放挖掘、Set Transformer 模仿学习 | 快速离线训练，导出为 ONNX 模型供 C# / Rust 运行时以低时延加载推理。 |

---

## 2. 代码与性能工程规范 (Coding & Performance Standards)

### 2.1 极低时延与零 GC 优化（15ms 闭环预算）
- **禁止高频堆分配**：在每回合 30 秒倒计时内的密集计算中，算法核心代码（如背包 DP 递归、打分评估）禁止在循环体内使用 `new` 分配短期对象。
- **复用对象池与结构体**：优先使用 `readonly struct` 与内存池（`ArrayPool<T>`），禁止在计算链路中高频调用 LINQ（如 `.Where().Select().ToList()` 会产生大量垃圾回收毛刺）。
- **位掩码与状态压缩**：流派、种族（Beast, Mech, Murloc 等）及关键词（圣盾、剧毒、复生）使用整型位运算（BitMask）进行 $O(1)$ 协同计算。

### 2.2 异常与降级兜底规范
- **日志断流降级**：若 `Power.log` 产生短暂延迟或格式异常，状态机必须保持上一帧稳定快照，并向 HUD 输出“数据同步中”微标，严禁抛出未捕获异常导致客户端崩溃。
- **超时强行剪枝**：动态规划背包搜索必须设定 5ms 硬超时阈值。若未完成全量剪枝，必须立即返回当前最高分的贪心局部解。

---

## 3. UI/UX 风格设计规范 (Design System)

TavernOracle HUD 的视觉设计哲学定位于：**“暗夜魔幻酒馆 (Dark Fantasy Tavern) + 现代战术电竞 HUD (Tactical Esports Overlay)”**。

### 3.1 颜色系统 (Color Tokens)

| 色彩角色 | 色值 (Hex) | 语义与应用场景 |
| :--- | :--- | :--- |
| **背景深空 (Bg Canvas)** | `#0d1117` / `rgba(22, 27, 34, 0.88)` | 半透明磨砂亚克力背景，配合 `backdrop-filter: blur(10px)` |
| **传奇金 (Legendary Gold)**| `#f59e0b` / `#fbbf24` | 三连对子提示、升本时机推荐、核心主 C 随从徽标 |
| **战术蓝 (Tactical Blue)** | `#38bdf8` | 状态机监控、金币收益模型、中立节奏提示 |
| **安全翠绿 (Safe / Tempo)**| `#10b981` | 即时战力加成、安全对战胜率、推荐买入单卡 |
| **暴毙警报 (Lethal Red)**  | `#ef4444` | 生死线突变（$P_{\text{lethal}} > 30\%$）、血量危机、避坑警告 |
| **魔能协同 (Arcane Purple)**| `#a855f7` | 跨随从强联动、种族核心光环、亡语滚雪球机制 |

### 3.2 随从卡面微标系统 (Minion Badge System)
- **微标位置**：锚定于酒馆区随从卡面上方居中 15px 处。
- **徽标分类**：
  - 🔥 **三连对子 (Triple)**：金底渐变外框，附带缓动发光呼吸动效（Glow Pulse）。
  - ★ **核心质变 (Key Engine)**：魔能紫菱形徽标，标注流派协同得分。
  - 🛡️ **即时战力 (Tempo Fix)**：翡翠绿盾牌，高亮圣盾/剧毒保命单卡。
  - ⛔ **陷阱预警 (Trap Warning)**：半透明灰色下沉或红色细斜杠，警示低收益陷阱卡。

### 3.3 交互体验红线 (HUD Interaction Rules)
1. **穿透至高无上**：窗口必须挂载 `WS_EX_TRANSPARENT | WS_EX_LAYERED`，HUD 绝不允许拦截玩家原生的鼠标滑过、拖拽与点击。
2. **零遮挡视觉侵入**：信息采用微标化、胶囊化（Pill UI）呈现，禁止大面积实心弹窗遮挡随从原画与酒馆界面。
3. **坐标自适应吸附**：实时监听底层游戏窗口的 `WM_MOVE`、`WM_SIZE`，在 1080P、2K、4K 及不同高 DPI 缩放下严格等比缩放。
