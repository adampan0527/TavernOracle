# TavernOracle (酒馆先知)

<p align="center">
  <strong>An ultra-low latency, 10k+ MMR tactical advisor HUD for Hearthstone Battlegrounds</strong><br>
  <em>100% compliant with Blizzard's "Pencil & Paper Rule" — zero injection, zero botting, zero ban risk.</em>
</p>

<p align="center">
  <a href="README_CN.md"><strong>👉 简体中文版本 (Chinese Version)</strong></a> |
  <a href="README.md"><strong>English</strong></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Platform">
  <img src="https://img.shields.io/badge/Latency-%3C%2015ms-brightgreen" alt="Latency">
  <img src="https://img.shields.io/badge/Anti--Cheat-Pencil%20%26%20Paper%20Rule%20(100%25%20Safe)-success" alt="Anti-Cheat">
  <img src="https://img.shields.io/badge/License-MIT-blue" alt="License">
</p>

---

## 📖 Overview

**TavernOracle** is a real-time tactical assistant HUD designed for *Hearthstone Battlegrounds*.

During the intense 30-second turn phase, TavernOracle analyzes board state, gold curves, tavern tiers, and combat win probabilities in **under 15 ms**. It delivers **10,000+ MMR-caliber decisions** (buy, sell, roll, freeze, level up, and positioning) with an expected 85%~90%+ alignment with high-ranking professional play.

---

## 🛡️ Anti-Cheat & Compliance (Pencil & Paper Rule)

TavernOracle strictly abides by the **Pencil & Paper Rule** established by former Hearthstone Game Director Ben Brode: *anything a player could track with a pencil and paper is fair game for companion software.*

| Feature | TavernOracle Implementation | Blizzard Policy | Ban Risk |
| :--- | :--- | :--- | :--- |
| **Data Ingestion** | Read-only parsing of local `Power.log` | Official diagnostic log stream (Identical to HDT / Firestone) | **0% (100% Safe)** |
| **Overlay HUD** | Passive, transparent click-through (`WS_EX_TRANSPARENT`) | Visual overlay only; all mouse clicks pass through to Hearthstone | **0% (100% Safe)** |
| **Player Action** | Decision recommendation badges & banners only | All in-game inputs are executed manually by the player | **0% (100% Safe)** |
| **Prohibited Behaviors** | ❌ **No** DLL injection, **No** memory editing, **No** automated mouse clicks (`mouse_event`/`SendInput`) | Automated botting or memory tampering is strictly prohibited | **Permanently Banned by Warden** |

---

## ⚡ Core Architecture

```
+-----------------------------------------------------------------------------------+
|                           Hearthstone Client (Unity / Windows)                    |
+-----------------------------------------------------------------------------------+
                                          │ (Official log stream, 0ms, zero-injection)
                                          ▼
+-----------------------------------------------------------------------------------+
| 【Data Layer】Power.log Stream Parser & State Machine                             |
|  - Real-time tailing of %LocalAppData%\Blizzard\Hearthstone\Logs\Power.log         |
|  - Mapped via HearthDb metadata: Entity IDs, buffs, tavern minions, gold, tavern tier|
+-----------------------------------------------------------------------------------+
                                          │ (Structured board state JSON / Tensor)
                                          ▼
+-----------------------------------------------------------------------------------+
| 【Decision Brain】Dual-Engine Architecture                                         |
|                                                                                   |
|  [Mode A · Production Baseline] Bayesian Posterior + Monte Carlo + Knapsack DP    |
|    - Latency: < 5 ms  |  10k MMR Alignment: 85%+  |  Zero Cold-Start on Patches   |
|                                                                                   |
|  [Mode B · Advanced Ceiling] Set Transformer Attention + High-MMR Replay Cloning  |
|    - Latency: 50~100 ms | 10k MMR Alignment: 90%+ |  Zero-Shot Semantic Embeddings|
+-----------------------------------------------------------------------------------+
                                          │ (Recommended action sequence)
                                          ▼
+-----------------------------------------------------------------------------------+
| 【Presentation Layer】Windows Transparent Click-Through HUD                        |
|  - Win32 API (WS_EX_TRANSPARENT | WS_EX_LAYERED) zero input interference          |
|  - Real-time minion badges (Triples, Core Synergies, Tempo/Lethal prevention)    |
|  - Macro strategy banner: Level-up timing, roll efficiency, and trap warnings    |
+-----------------------------------------------------------------------------------+
```

---

## 🚀 Key Features

- **Blazing Fast (< 15ms Latency Budget)**:
  - Log Parsing $\le 2\text{ ms}$
  - State Machine Assembly $\le 3\text{ ms}$
  - Decision Optimization $\le 5\text{ ms}$
  - HUD Refresh $\le 5\text{ ms}$
- **Dynamic Risk Aversion ($P_{lethal}$)**:
  - Real-time Monte Carlo combat simulation computes the probability of dying next turn ($P_{lethal}$).
  - When $P_{lethal} > 30\%$, the system automatically sheds greedy greed/scaling targets and locks into maximum immediate tempo to survive.
- **Zero Cold-Start Across Balance Patches**:
  - Training-free graph model driven by card metadata JSON.
  - When Blizzard drops a balance update, simply update the metadata file — zero downtime, zero retraining.
- **Unobtrusive Transparent HUD**:
  - Completely click-through overlay automatically anchors to the game viewport across resolutions and window moves.

---

## 🛠️ Quick Start

### 1. Enable Hearthstone Log Output

Create or edit `log.config` in your Hearthstone local directory:  
Path: `%LocalAppData%\Blizzard\Hearthstone\log.config`

```ini
[Power]
LogLevel=1
FilePrinting=true
ConsolePrinting=false
ScreenPrinting=false
Verbose=true
```

### 2. Verify Data Stream

Launch Hearthstone and play a Battlegrounds match. The game will automatically output real-time match events to:
```
%LocalAppData%\Blizzard\Hearthstone\Logs\Power.log
```

---

## 📚 Documentation Index

All technical design documents are organized under the [docs/](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs) directory:

- [AGENTS.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/AGENTS.md) — Root documentation router and AI Agent workflow protocol.
- [docs/compliance.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/compliance.md) — Blizzard anti-cheat boundary and safety audit checklist.
- [docs/architecture.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/architecture.md) — 3-tier system architecture, latency budget, and lifecycle management.
- [docs/data_layer.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/data_layer.md) — Power.log streaming parser specification and TagChange bindings.
- [docs/decision_engine.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/decision_engine.md) — Mathematical modeling: Bayesian inference, Knapsack DP, and Set Transformer.
- [docs/presentation_hud.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/presentation_hud.md) — Win32 click-through HUD, UI badges, and viewport docking.
- [docs/roadmap_and_tech_stack.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/roadmap_and_tech_stack.md) — HDT plugin vs. standalone Tauri evaluation & 4-week roadmap.
- [docs/PROJECT_STARTUP.md](file:///Users/panyan/Desktop/coding_project/app/TavernOracle/docs/PROJECT_STARTUP.md) — Complete original project specification document.

---

## 🗺️ 4-Week Roadmap

- **Week 1: Data Ingestion** — Log watcher, Power.log streaming, HearthDb metadata mapping.
- **Week 2: Decision Brain** — Dynamic archetype weighting, Monte Carlo lethal threshold, Knapsack DP solver.
- **Week 3: Presentation Layer** — Win32 click-through overlay, minion badges, top recommendation banner.
- **Week 4: Optimization & Release** — High-MMR ladder tuning, hero curve alignments, package installer.

---

## ⚖️ Disclaimer

TavernOracle is an unofficial companion tool developed for research and gameplay assistance. It is not affiliated with, endorsed by, or sponsored by Blizzard Entertainment. All game assets and trademarks are the property of their respective owners.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
