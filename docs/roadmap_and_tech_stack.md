# 技术选型、4周开发路线图与生态参考 (Tech Stack, Roadmap & Ecosystem)

## 1. 技术路线选型对比

| 方案 | 技术栈 | 优势 | 劣势 | 推荐指数 |
| :--- | :--- | :--- | :--- | :--- |
| **路线 A (首选推荐)** | **C# (.NET) + HDT 插件** | 借助 HDT / HearthDb / Bob's Buddy 现成生态，仅需编写核心决策算法，1~2 周即可交付 MVP | 依赖 HDT 作为宿主进程 | ★★★★★ |
| **路线 B (现代独立版)** | **Tauri (Rust + React / Vue)** | 独立进程运行，Webview2 渲染现代高颜值 UI，安装包轻量（< 20MB） | 需自行维护日志解析与卡牌库，周期 3~4 周 | ★★★★☆ |
| **路线 C (算法研究原型)** | **Python + PyQt / DirectML** | 数据分析与神经网络离线验证便捷 | 资源占用略大，发版体积较大 | ★★★☆☆ |

---

## 2. 4 周敏捷开发与落地路线图

```
 Week 1: 数据链路打通 (Data Ingestion)
 ├─ 编写/引入 log.config，验证 Power.log 流式捕获
 ├─ 接入 HearthDb，测试随从 CardID 与属性映射
 └─ 交付物: 控制台实时打印当前酒馆随从、金币、英雄血量

 Week 2: 决策算法与背包求解注入 (Brain Core)
 ├─ 编写卡牌属性 JSON 知识图谱与种族协同算子
 ├─ 实现蒙特卡洛战力模拟生死线 (P_lethal 调节阀)
 ├─ 编写分支定界有界背包求解器 (Knapsack DP)
 └─ 交付物: 控制台实时输出当前回合最优买/卖/刷/升操作序列

 Week 3: 透明穿透 HUD 与游戏吸附 (Presentation Layer)
 ├─ 实现 Win32 WS_EX_TRANSPARENT 点击穿透悬浮窗
 ├─ 实现随从坐标锚定高亮徽章与顶部推荐横条
 └─ 交付物: 游戏内半透明 HUD 渲染与手动拖拽测试

 Week 4: 天梯对局微调与双模增强 (Tuning & Deployment)
 ├─ 爬取天梯单卡胜率注入贝叶斯先验矩阵
 ├─ 对齐英雄技能节奏线（拉法姆、阿凯、加里维克斯等专属逻辑）
 └─ 交付物: 封装发布可执行安装包，开启天梯实战冲分
```

---

## 3. 核心生态开源资源

- **HearthDb (.NET C#)**: [https://github.com/HearthSim/HearthDb](https://github.com/HearthSim/HearthDb) —— 暴雪全卡牌元数据官方结构化定义库。
- **Hearthstone-Deck-Tracker (HDT)**: [https://github.com/HearthSim/Hearthstone-Deck-Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker) —— 权威开源 C# 炉石客户端宿主与日志监听系统。
- **python-hslog**: [https://github.com/HearthSim/python-hslog](https://github.com/HearthSim/python-hslog) —— Python 流式解析 Power.log 生成语法树。
- **Bob's Buddy**: 开源的酒馆战棋千次随机战力与胜率蒙特卡洛模拟器源码（内置于 HDT）。
