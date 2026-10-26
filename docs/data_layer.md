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
