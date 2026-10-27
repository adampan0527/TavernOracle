# 数据层：Power.log 日志摄取与状态机 (Data Layer & State Machine)

## 1. 为什么坚决不用“纯截图 + YOLO/OCR 识别”

1. **动画与动态特效干扰**：金卡动态晃动、购买打出飞行动画、BUFF 浮层提示会导致 CV 漏识率高达 15%~30%。
2. **严重计算延迟与资源消耗**：截屏 + 图像模型推理有 300~800ms 延迟，且高负荷占用玩家显卡资源，导致炉石本身掉帧。
3. **Power.log 的优势**：官方原生支持输出，零延迟（0ms），100% 结构化准确度，无显卡开销。

---

## 2. 官方调试日志配置

在玩家本地目录创建或修改配置文件以激活无延迟数据流：  
文件路径：`%LocalAppData%\Blizzard\Hearthstone\log.config`

```ini
[Power]
LogLevel=1
FilePrinting=true
ConsolePrinting=false
ScreenPrinting=false
Verbose=true
```

---

## 3. 核心日志语法与 TagChange 解析规范

`Power.log` 以行级文本输出游戏引擎的实体变更事件。核心状态转换包括：

### 3.1 资源与技术等级
- `TAG_CHANGE Entity=[...] tag=RESOURCES value=X`：当前可用铸币数。
- `TAG_CHANGE Entity=[...] tag=RESOURCES_USED value=X`：已消耗铸币数。
- `TAG_CHANGE Entity=[...] tag=PLAYER_TECH_LEVEL value=X`：当前酒馆星级。
- `TAG_CHANGE Entity=[...] tag=HERO_POWER_ACTIVATED value=X`：英雄技能是否已激活。

### 3.2 随从生成与属性变更
- `FULL_ENTITY - Updating [entityName=... id=X zone=PLAY zonePos=Y cardId=BG_...]`：
  - 新建或更新随从实体信息。
  - 核心属性：`ATK` (攻击力), `HEALTH` (生命值), `TECH_LEVEL` (随从星级), `TAUNT` (嘲讽), `DIVINE_SHIELD` (圣盾), `POISONOUS` / `VENOMOUS` (剧毒), `REBORN` (复生)。
- `SHOW_ENTITY - Updating Entity=[id=X cardId=BG_...] CardID=BG_...`：揭示酒馆区（`zone=SETASIDE` 或 `zone=PLAY`）的新卡牌。

### 3.3 区域流转 (Zone Transition)
- `zone=HAND`：手牌区。
- `zone=PLAY`：战场区（区分我方战场与敌方战场）。
- `zone=GRAVEYARD`：墓地/弃牌区。
- `zone=SETASIDE`：酒馆待选区或衍生缓冲区。

---

## 4. 推荐开源组件

- **HearthDb (.NET C#)**: [https://github.com/HearthSim/HearthDb](https://github.com/HearthSim/HearthDb)
- **LogWatcher (内置于 HDT)**: [https://github.com/HearthSim/Hearthstone-Deck-Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker)
- **python-hslog**: [https://github.com/HearthSim/python-hslog](https://github.com/HearthSim/python-hslog)

## 5. HDT 插件实时接线与验收（2026-10-27）

默认插件在 `OnUpdate` 中读取 HDT 公共 `API.Core.Game`，通过
`IGameStateWatcher.AttachHdtGame` 同步真实招募盘面并驱动决策与 HUD。
此输入是 HDT 从日志重建的托管对象，不读取炉石进程内存。注入离线状态机的测试不启用宿主同步。

- 按已安装 HDT 1.59.4 的实际契约读取 `PlayerEntity.GetTag`、`PlayerTechLevel`、
  `GetTurnNumber()`、`Player.Board/Hand`、`Opponent.Board`。
- `RESOURCES_USED` 标签编号为 **25**，`PLAYER_TECH_LEVEL` 为 **1377**；
  可用金币取资源加临时资源减已用资源，零金币必须保留，禁止按回合补出虚假金币。
- 升本费用来自我方 `PlayerEntities` 中处于 PLAY 的 `TB_BaconShopTechUp*_Button`。
  未找到时禁止生成升本建议。已购随从打出费用为零。
- 商品按 `ZonePosition` 排序，横条以“从左数第 N 张”与卡名指明首选。
  酒馆法术保留物理位置用于对齐，当前未实现法术价值评估，不作为随从推荐买入。
- 战斗、菜单阶段清除购牌建议；数据未连接或商店未识别时显示等待提示。
  尚无真实战力模拟结果时不展示默认先验为实测胜率。
- HUD 根 Canvas 不得在零尺寸下裁剪子控件；所有新增控件保持 `IsHitTestVisible=false`。

回归入口：`LiveHdtRecommendationTests`。先用旧代码复现零金币误判与零尺寸裁剪两个失败，
修复后覆盖实时消费/换店、可见推荐与首选微标、阶段切换、等待提示和法术位置。
这是宿主对象契约及 WPF 渲染验收，不能替代真实对局验收，也不能证明端到端 15ms 指标。

构建且暂不替换正在运行的插件：
`dotnet build src/TavernOracle/TavernOracle.csproj -c Release -f net472 -p:DeployToHdt=false`。
退出 HDT 后执行 `tools/install_plugin.ps1`，将已构建 DLL 同步到安装目录和 AppData 插件目录，
保留备份并校验哈希，然后重新启动 HDT、确认 TavernOracle 已启用。
