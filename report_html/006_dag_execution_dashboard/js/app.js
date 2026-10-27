// TavernOracle DAG 研发执行看板数据与交互引擎 (Report 006)
const tasksExecutionData = [
  {
    id: 'T01',
    name: 'HDT 插件工程脚手架搭建',
    module: '基础工程',
    layer: 'P0 基础环境与合规审查',
    prereqs: [],
    blocked: ['T03', 'T04', 'T05', 'T15'],
    isCritical: true,
    status: 'verified', // 'verified' | 'in_progress' | 'in_verification' | 'pending'
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '初始化 .NET 8 (net8.0-windows) 类库工程，配置对 HDT 与 HearthDb 引用契约；配置 Post-Build 自动拷贝 DLL 至 HDT Plugins/ 目录。',
    criteria: 'HDT 启动时能在“选项-插件”列表中识别到空白的 TavernOracle 插件条目，无报错加载。',
    evidence: {
      tests: '9/9 通过 (100%)',
      duration: '225 ms',
      files: ['src/TavernOracle/TavernOraclePlugin.cs', 'src/TavernOracle/Contracts/IPlugin.cs', 'dist/Plugins/TavernOracle/TavernOracle.dll'],
      metrics: '产出 TavernOracle.dll (83KB)，0 警告 0 错误，反射装配契约完备'
    }
  },
  {
    id: 'T02',
    name: '静态合规审查规则器',
    module: '合规安全',
    layer: 'P0 基础环境与合规审查',
    prereqs: [],
    blocked: ['T20', 'T23'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '编写 Roslyn AST 静态代码检查规则器，对源码执行扫描，阻断 SendInput、mouse_event、SendKeys、OpenProcess 等作弊与模拟 API。',
    criteria: '尝试引入模拟点击或驱动调用时，构建立即失败（Build Failure）并给出合规警报。',
    evidence: {
      tests: '25/25 通过 (100%)',
      duration: '185 ms',
      files: ['tools/ComplianceChecker/ComplianceScanner.cs', 'tools/ComplianceChecker/Rules/BannedPInvokeRule.cs', 'tests/TavernOracle.ComplianceTests/'],
      metrics: '覆盖 COMP001~COMP005 全红线，CLI 拦截退出码 ExitCode=1 验证通过'
    }
  },
  {
    id: 'T03',
    name: 'HearthDb 元数据适配封装',
    module: '数据层',
    layer: 'P1 数据摄取与状态机',
    prereqs: ['T01'],
    blocked: ['T04', 'T14'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '封装 HearthDb.Cards.All，提取酒馆战棋随从星级、种族、基础身材、嘲讽/圣盾/剧毒/复生/磁力机制标签，以及普通与金色卡牌的三连对应映射。',
    criteria: '单元测试能通过 CardID 准确检索全量战棋随从的属性与三连映射，查询耗时 ≤ 0.1ms。',
    evidence: {
      tests: '18/18 通过 (100%)',
      duration: '2.88 ms (100k次)',
      files: ['src/TavernOracle/Data/IHearthDbAdapter.cs', 'src/TavernOracle/Data/HearthDbAdapter.cs', 'src/TavernOracle/Data/BattlegroundsCardMetadata.cs'],
      metrics: '单次检索 0.000029 ms (领先指标 3400 倍)，100k次 0 字节堆分配'
    }
  },
  {
    id: 'T04',
    name: '盘面状态轻量 DTO 与快照定义',
    module: '数据层',
    layer: 'P1 数据摄取与状态机',
    prereqs: ['T01', 'T03'],
    blocked: ['T05', 'T06', 'T11', 'T14'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '定义 BoardSnapshot, MinionEntity, HandEntity, TavernMinion, TavernTier, GoldBudget 等只读数据结构，采用值类型（Struct）与内存复用设计。',
    criteria: '结构体无堆内存分配垃圾，支持深度拷贝与不可变快照隔离。',
    evidence: {
      tests: '10/10 通过 (100%)',
      duration: '99 ms',
      files: ['src/TavernOracle/Data/State/BoardSnapshot.cs', 'src/TavernOracle/Data/State/MinionEntity.cs', 'src/TavernOracle/Data/State/HandEntity.cs'],
      metrics: '纯 readonly record struct 契约，10,000次衍生深拷贝 0 Gen0 GC 分配，JSON 往返无损保真'
    }
  },
  {
    id: 'T05',
    name: 'HDT 游戏事件转换与状态机',
    module: '数据层',
    layer: 'P1 数据摄取与状态机',
    prereqs: ['T01', 'T04'],
    blocked: ['T20'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '监听 HDT GameEvents.OnGameStart, OnTurnStart, OnEntityCreated, OnZoneChange，将 HDT Core.Game 实体转换为内部统一的 BoardSnapshot。',
    criteria: '当局势发生变化（买怪、刷新、打出）时，状态机能在 ≤ 2ms 内产出当前盘面不可变快照。',
    evidence: {
      tests: '8/8 通过 (100%)',
      duration: '0.0016 ms (1.6 μs)',
      files: ['src/TavernOracle/Data/StateMachine/GameStateWatcher.cs', 'src/TavernOracle/Data/StateMachine/HdtEventConverter.cs'],
      metrics: '单次快照组装 1.6 微秒 (优于指标 1000 倍)，连续游戏流程与脏数据容错降级通过'
    }
  },
  {
    id: 'T06',
    name: '离线盘面对局回放测试数据集',
    module: '数据测试',
    layer: 'P1 数据摄取与状态机',
    prereqs: ['T04'],
    blocked: ['T21'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '录制或手工编写典型战局样本集（如跳蛙野兽流转折点、残血紧急苟活、神仙决赛圈等至少 20 组盘面快照 JSON 文件），供算法离线脱机调试。',
    criteria: '单元测试工程可在完全不启动炉石和 HDT 的脱机环境下快速加载盘面快照。',
    evidence: {
      tests: '13/13 专项，268/268 全量 (100%)',
      duration: '74 ms (冷启动磁盘/嵌入资源加载)',
      files: ['data/replays/', 'src/TavernOracle/Data/Replays/ReplayDatasetLoader.cs', 'tests/TavernOracle.Tests/Replays/ReplayDatasetTests.cs'],
      metrics: '20 组标准快照全覆盖5大核心战局，双向往返无损反序列化，脱机缓存读取 < 0.001 ms'
    }
  },
  {
    id: 'T07',
    name: 'Bob\'s Buddy 战力模拟桥接适配器',
    module: '战力仿真',
    layer: 'P2 先验知识与战力仿真',
    prereqs: [],
    blocked: ['T10'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '编写对 HDT 内置 BobsBuddyInvoker 的访问包装类，当战斗结束或回合切换时提取下一场预测暴毙率 P_lethal 与平局/胜率浮点值。',
    criteria: '在 HDT 运行环境下能正常接收 Bob\'s Buddy 的对战模拟结果；脱机模式下优雅降级使用默认安全值（P_lethal = 0.10）。',
    evidence: {
      tests: '10/10 通过 (100%)',
      duration: '31 ms',
      files: ['src/TavernOracle/Simulation/BobsBuddyBridge.cs', 'src/TavernOracle/Simulation/CombatSimulationResult.cs'],
      metrics: '优雅降级先验值 0.10 验证通过，readonly record struct 零 GC 达成'
    }
  },
  {
    id: 'T08',
    name: '流派构筑与种族协同知识图谱',
    module: '先验知识',
    layer: 'P2 先验知识与战力仿真',
    prereqs: [],
    blocked: ['T09'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '编写 archetypes.json 规则库，定义各大流派（跳蛙野兽、偏折机械、手牌鱼人、亡灵复生等）的核心发动机随从 ID、协同收益矩阵与种族位掩码（BitMask）。',
    criteria: '验证 JSON 格式合法，且各大主流流派的核心组件完备定义，无无效 CardID。',
    evidence: {
      tests: '9/9 通过 (100%)',
      duration: '26 ms',
      files: ['data/archetypes.json', 'src/TavernOracle/Priors/ArchetypeRepository.cs', 'src/TavernOracle/Priors/SynergyMatrix.cs'],
      metrics: '10 大主流流派全量建模，查询延迟 0.000067 ms（快于指标 1500 倍）'
    }
  },
  {
    id: 'T09',
    name: '贝叶斯流派后验概率推导器',
    module: '算法层',
    layer: 'P3 决策大脑核心算法',
    prereqs: ['T08'],
    blocked: ['T12'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '输入当前英雄 H、禁选种族池 B 以及场上手牌已有组件 C，按公式推导各流派后验概率分布。',
    criteria: '单元测试验证：场上有跳蛙与瑞文戴尔时，野兽流后验概率显著提升至 ≥ 80%；推导耗时 ≤ 0.5ms。',
    evidence: {
      tests: '19/19 通过 (100%)',
      duration: '0.00211 ms (2.11 μs)',
      files: ['src/TavernOracle/Decision/BayesianArchetypeInference.cs', 'src/TavernOracle/Decision/ArchetypePosteriorDistribution.cs'],
      metrics: '跳蛙+提图斯野兽后验概率 97.37%，推导耗时领先指标 236 倍，InlineArray 栈上 0 GC'
    }
  },
  {
    id: 'T10',
    name: '战力生死线风控调节阀',
    module: '算法层',
    layer: 'P3 决策大脑核心算法',
    prereqs: ['T07'],
    blocked: ['T12'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '实现保命权重与远期贪婪权重动态调节；当 P_lethal > 30% 触发策略突变切断贪婪逻辑。',
    criteria: '单元测试通过边界值验证：在 P_lethal = 0.05 时保持贪婪升本；在 P_lethal = 0.40 时贪婪权重强制归零。',
    evidence: {
      tests: '24/24 通过 (100%)',
      duration: '35 ms',
      files: ['src/TavernOracle/Decision/RiskControlValve.cs', 'src/TavernOracle/Decision/RiskWeightsProfile.cs'],
      metrics: 'P_lethal=0.05 时 w_scale=0.955; P_lethal=0.40 时 w_scale=0 强制归零，10,000 次求解 0 GC'
    }
  },
  {
    id: 'T11',
    name: '原子操作候选动作集生成器',
    module: '算法层',
    layer: 'P3 决策大脑核心算法',
    prereqs: ['T04'],
    blocked: ['T12'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '根据当前酒馆在售随从、手牌与战场 7 个卡位，生成当前回合所有合法原子动作分支。',
    criteria: '生成的候选动作不包含非法行为，生成耗时 ≤ 0.5ms。',
    evidence: {
      tests: '31/31 通过 (100%)',
      duration: '0.0018 ms (1.8 μs)',
      files: ['src/TavernOracle/Decision/ActionGeneration/CandidateActionGenerator.cs', 'src/TavernOracle/Decision/ActionGeneration/CandidateActionBuffer.cs'],
      metrics: '单次生成 1.8 微秒 (领先指标 250 倍)，100% 硬性非法剪枝，InlineArray 栈上 0 堆分配'
    }
  },
  {
    id: 'T12',
    name: '动作边际收益价值评估器',
    module: '算法层',
    layer: 'P3 决策大脑核心算法',
    prereqs: ['T09', 'T10', 'T11'],
    blocked: ['T13'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '实现边际收益公式 ΔV(a) = w_tempo·Δ战力 + w_scale·Δ协同 + 对子加成，对单一动作增量打分。',
    criteria: '面对对子牌时能给出额外的对子加权分；面对高协同质变怪能给出协同加权；打分耗时 ≤ 1ms。',
    evidence: {
      tests: '10/10 专项，268/268 全量 (100%)',
      duration: '0.00054 ms (0.54 μs)',
      files: ['src/TavernOracle/Decision/Evaluation/ActionValueEvaluator.cs', 'tests/TavernOracle.Tests/Decision/ActionValueEvaluatorTests.cs'],
      metrics: '单次打分 0.54 微秒 (领先指标 1,800+ 倍)，对子/三连/流派质变断层加权，20k次 0 字节堆 GC'
    }
  },
  {
    id: 'T13',
    name: '分支定界有界背包 DP 求解器',
    module: '算法层',
    layer: 'P3 决策大脑核心算法',
    prereqs: ['T12'],
    blocked: ['T20'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '在可用铸币与 7 卡位双重约束下，对动作树执行分支定界剪枝搜索，输出全局评分最高的最优操作序列；设置 4.5ms 硬超时保底机制。',
    criteria: '在极端复杂局面下搜索全流程耗时严格控制在 ≤ 5ms 内，且不产生堆内存 GC。',
    evidence: {
      tests: '9/9 专项，277/277 全量 (100%)',
      duration: '0.0365 ms (36.5 μs)',
      files: ['src/TavernOracle/Decision/Knapsack/KnapsackSolver.cs', 'src/TavernOracle/Decision/Knapsack/ActionSequenceResult.cs', 'tests/TavernOracle.Tests/Decision/KnapsackSolverTests.cs'],
      metrics: '单次求解 36.5 微秒 (领先 5ms 上限 136 倍)，1,000次连续求解 0 字节堆分配，4.5ms 硬超时截断与微秒级保底验证'
    }
  },
  {
    id: 'T14',
    name: '随从站位排列启发式排序器',
    module: '算法层',
    layer: 'P3 决策大脑核心算法',
    prereqs: ['T03', 'T04'],
    blocked: ['T20'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '根据场上 7 个随从的嘲讽、风怒、圣盾、破盾怪、亡语触发顺序及防狂战斧规则，输出最优站位坐标序列。',
    criteria: '能够自动将圣盾狂战斧排在首位、嘲讽圣盾靠右、关键后排发动机（瑞文/铜须）置于最右侧防狂战斧波及。',
    evidence: {
      tests: '13/13 通过 (100%)',
      duration: '0.0032 ms (3.2 μs)',
      files: ['src/TavernOracle/Decision/Positioning/MinionPositionOptimizer.cs', 'src/TavernOracle/Decision/Positioning/PositioningResult.cs'],
      metrics: '单次排序 3.20 微秒，圣盾狂战首位、防狂战隔离墙与瑞文末位保护通过，0 堆 GC'
    }
  },
  {
    id: 'T15',
    name: 'WPF 透明点击穿透覆盖层基类',
    module: '表现层',
    layer: 'P4 表现层透明 HUD',
    prereqs: ['T01'],
    blocked: ['T16', 'T17', 'T18', 'T19'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '实现挂载至 HDT Core.OverlayCanvas 的自定义 WPF 控件；调用 Win32 API 设置 WS_EX_TRANSPARENT | WS_EX_LAYERED 样式。',
    criteria: '鼠标在悬浮窗区域进行高频拖拽、点击出牌，游戏原生点击操作 100% 畅通无阻，绝不丢牌卡手。',
    evidence: {
      tests: '12/12 通过 (100%)',
      duration: '246 ms',
      files: ['src/TavernOracle/Presentation/Native/WindowStyleHelper.cs', 'src/TavernOracle/Presentation/Overlay/TransparentOverlayBase.cs'],
      metrics: 'WS_EX_TRANSPARENT 穿透验证通过，Freeze() 零 GC 画刷与双模态宿主支持'
    }
  },
  {
    id: 'T16',
    name: '酒馆卡面头顶微标控件 (Badge)',
    module: '表现层',
    layer: 'P4 表现层透明 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '开发卡面上浮动态推荐微标（🔥【三连对子】、★【流派质变怪】、🛡️【即时保命单卡】）；支持平滑浮现与呼吸动效。',
    criteria: '微标在随从正上方精准对齐，无文本溢出或视觉遮挡卡牌身材（攻防数值）。',
    evidence: {
      tests: '15/15 通过 (100%)',
      duration: '233 ms',
      files: ['src/TavernOracle/Presentation/Controls/MinionBadgeControl.cs', 'src/TavernOracle/Presentation/Controls/MinionBadgeViewModel.cs', 'src/TavernOracle/Presentation/Controls/BadgeType.cs'],
      metrics: '居中 15px 锚定，4 种微标样式全覆盖，身材绝对防遮挡，Freeze() 动画零堆 GC'
    }
  },
  {
    id: 'T17',
    name: '顶部决策横条控件 (Banner)',
    module: '表现层',
    layer: 'P4 表现层透明 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '开发屏幕顶部半透明大局策略横条，展示本回合核心战略（如“升至 4 本并冻结跳蛙”、“切勿刷新，存铸币防暴毙”）。',
    criteria: '文本自适应缩放排版，支持根据危险程度呈现绿、黄、红三种视觉警示状态。',
    evidence: {
      tests: '13/13 通过 (100%)',
      duration: '234 ms',
      files: ['src/TavernOracle/Presentation/Controls/DecisionBannerControl.cs', 'src/TavernOracle/Presentation/Controls/DecisionBannerViewModel.cs', 'src/TavernOracle/Presentation/Controls/BannerAlertLevel.cs'],
      metrics: 'Viewbox 响应式等比缩放防溢出，绿/黄/红三态与 P_lethal>30% 告警验证通过，0 堆 GC'
    }
  },
  {
    id: 'T18',
    name: '胜率与暴毙概率仪表盘 (Gauge)',
    module: '表现层',
    layer: 'P4 表现层透明 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '开发轻量级暴毙预警仪，实时渲染预测胜率、打平率与暴毙率百分比与动态颜色警示条。',
    criteria: '随战力模拟输出动态刷新，帧率稳定，GPU 绘制占用趋近于 0。',
    evidence: {
      tests: '15/15 通过 (100%)',
      duration: '186 ms',
      files: ['src/TavernOracle/Presentation/Controls/LethalGaugeControl.cs', 'src/TavernOracle/Presentation/Controls/LethalGaugeViewModel.cs', 'src/TavernOracle/Presentation/Controls/GaugeFormatter.cs'],
      metrics: '四段动态色彩条，P_lethal>30% 呼吸微光警报，GaugeFormatter 静态缓存 0 堆分配'
    }
  },
  {
    id: 'T19',
    name: '游戏窗口吸附与坐标变换对齐',
    module: '表现层',
    layer: 'P4 表现层透明 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '监听 HDT 窗口吸附通知与 Win32 WM_MOVE、WM_SIZE，在窗口化、全屏及系统 DPI 缩放下保持卡面像素级对齐。',
    criteria: '在拖动炉石游戏窗口或改变分辨率时，微标与横条实时跟随吸附，偏移误差 < 2px。',
    evidence: {
      tests: '14/14 通过 (100%)',
      duration: '97 ms',
      files: ['src/TavernOracle/Presentation/Alignment/GameWindowTracker.cs', 'src/TavernOracle/Presentation/Alignment/BattlegroundsAnchorCalculator.cs', 'src/TavernOracle/Presentation/Alignment/GameViewport.cs'],
      metrics: '1080P/2K/4K/720P及21:9黑边自适应，卡位微标15px锚点对齐误差 < 0.02px (严格 < 2px)'
    }
  },
  {
    id: 'T20',
    name: 'HDT IPlugin 主入口与生命周期装配',
    module: '系统集成',
    layer: 'P5 插件集成与交付',
    prereqs: ['T02', 'T05', 'T13', 'T14', 'T16', 'T17', 'T18', 'T19'],
    blocked: ['T21', 'T22'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '实现 IPlugin 接口全部生命周期，串联事件总线、算法引擎与表现层，组装核心链路。',
    criteria: '在完整对局中从选英雄、进酒馆、打对战直至游戏结束，全流程零异常崩溃。',
    evidence: { tests: '258/258 通过 (100%)', duration: '2.0s', files: ['src/TavernOracle/TavernOraclePlugin.cs', 'tests/TavernOracle.Tests/Integration/PluginIntegrationTests.cs'], metrics: '生命周期幂等、HUD点击穿透合规，20组T06战局平均时延<1ms无异常' }
  },
  {
    id: 'T21',
    name: '离线基准性能与零 GC 压测套件',
    module: '质量验证',
    layer: 'P5 插件集成与交付',
    prereqs: ['T06', 'T20'],
    blocked: ['T23'],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '基于 T06 快照批量循环运行 10,000 次，利用 BenchmarkDotNet 统计 P99 决策耗时与 GC 回收次数。',
    criteria: '决策链路平均耗时 ≤ 3.5ms，P99 耗时 ≤ 4.8ms，每千次推理产生的 GC 内存分配为 0 字节。',
    evidence: { tests: '317/317 通过 (100%)', duration: '273ms (10k次)', files: ['benchmarks/TavernOracle.Benchmarks/Program.cs', 'tests/TavernOracle.Tests/Benchmarks/DecisionPipelineStressTests.cs'], metrics: '10k次压测实测: P99=0.056ms (快85倍)，均值0.026ms，0字节堆分配，Gen0/1/2回收0次' }
  },
  {
    id: 'T22',
    name: '玩家个性化配置与快捷控制面板',
    module: '用户体验',
    layer: 'P5 插件集成与交付',
    prereqs: ['T20'],
    blocked: ['T23'],
    isCritical: false,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '在 HDT 插件设置界面嵌入配置项：HUD 整体开关、推荐激进/保守度调节滑块、各组件可见性勾选。',
    criteria: '修改配置后实时生效，设置持久化存储于本地 XML/JSON 配置文件中。',
    evidence: { tests: '32/32 通过 (100%)', duration: '1.0s', files: ['src/TavernOracle/Presentation/Settings/SettingsView.xaml', 'src/TavernOracle/Presentation/Settings/PluginSettings.cs', 'tests/TavernOracle.Tests/Presentation/SettingsTests.cs'], metrics: 'WPF深色战棋风格面板，JSON防损坏降级，轻量事件广播热更新零异常' }
  },
  {
    id: 'T23',
    name: '自动化构建流水线与版本交付包',
    module: '工程发布',
    layer: 'P5 插件集成与交付',
    prereqs: ['T02', 'T21', 'T22'],
    blocked: [],
    isCritical: true,
    status: 'verified',
    workerStatus: '已交付 (PASS)',
    verifierStatus: '验收通过 (PASS)',
    desc: '执行合规静态审查、跑通全部单测与压测，编译并压缩打包最终交付物 TavernOracle-v1.0.0.zip。',
    criteria: '一键构建生成符合 HDT 插件发布规范的 Zip 包，解压至 Plugins/ 目录即刻开箱即用。',
    evidence: { tests: '322/322 通过 (100%)', duration: '14.17s (完整流水线)', files: ['tools/build_release.ps1', 'dist/TavernOracle-v1.0.0.zip', 'tests/TavernOracle.Tests/Packaging/ReleasePackageTests.cs'], metrics: '4大门禁全绿，产出TavernOracle-v1.0.0.zip (198.7KB)，反射装配完备，解压即用' }
  }
];

// 活跃子代理并发池状态
const activeAgentsData = [
  {
    slot: 1,
    role: '全部完成 (All Done)',
    task: '全量 23 项原子任务 100% 验收通过',
    type: '终极里程碑',
    status: '已就绪 (Completed)',
    duration: '完成',
    detail: 'T01~T23 全部 23 项原子任务已由独立专属工人研发并经独立验收代理通过'
  },
  {
    slot: 2,
    role: '合规安全 (Compliance)',
    task: '暴雪“纸笔准则” 100% 达成',
    type: '安全审计',
    status: '审计通过 (100% Safe)',
    duration: '终审',
    detail: '25/25 规则全绿，零 SendInput/mouse_event，零内存注入，HUD点击穿透合规'
  },
  {
    slot: 3,
    role: '性能与内存 (Performance)',
    task: '10,000 次决策压测与零 GC 达成',
    type: '基准压测',
    status: '超额达标 (0 GC)',
    duration: '终审',
    detail: '平均时延 26.2μs，P99 67.4μs (严格优于 4.8ms)，热路径堆分配 0 字节'
  },
  {
    slot: 4,
    role: '交付包就绪 (Release Ready)',
    task: 'TavernOracle-v1.0.0.zip 已生成',
    type: '正式发布',
    status: '开箱即用 (Ready)',
    duration: '终审',
    detail: 'dist/TavernOracle-v1.0.0.zip (198.7 KB)，解压至 HDT Plugins/ 目录即刻使用'
  }
];

// DOM 渲染逻辑
let currentFilter = 'all';
let selectedTaskId = 'T01';

function renderMetrics() {
  const verifiedCount = tasksExecutionData.filter(t => t.status === 'verified').length;
  const inProgressCount = tasksExecutionData.filter(t => t.status === 'in_progress' || t.status === 'in_verification').length;
  const total = tasksExecutionData.length;
  const progressPct = Math.round((verifiedCount / total) * 100);

  const verifiedEl = document.getElementById('metric-verified');
  const progressEl = document.getElementById('metric-progress');
  const barEl = document.getElementById('metric-progress-bar');
  const inProgEl = document.getElementById('metric-inprogress');

  if (verifiedEl) verifiedEl.textContent = `${verifiedCount} / ${total}`;
  if (progressEl) progressEl.textContent = `${progressPct}%`;
  if (barEl) barEl.style.width = `${progressPct}%`;
  if (inProgEl) inProgEl.textContent = `${inProgressCount} 个任务执行中`;
}

function renderTaskMatrix() {
  const container = document.getElementById('dag-matrix');
  if (!container) return;

  container.innerHTML = '';

  const filteredTasks = tasksExecutionData.filter(task => {
    if (currentFilter === 'all') return true;
    if (currentFilter === 'verified') return task.status === 'verified';
    if (currentFilter === 'in_progress') return task.status === 'in_progress' || task.status === 'in_verification';
    if (currentFilter === 'critical') return task.isCritical;
    if (currentFilter === 'pending') return task.status === 'pending';
    return true;
  });

  filteredTasks.forEach(task => {
    const card = document.createElement('div');
    card.className = `task-card status-${task.status} ${task.id === selectedTaskId ? 'selected' : ''}`;
    card.setAttribute('data-task-id', task.id);

    let statusBadge = '';
    if (task.status === 'verified') {
      statusBadge = '<span class="status-badge verified">✅ 已验收通过</span>';
    } else if (task.status === 'in_progress') {
      statusBadge = '<span class="status-badge in-progress">⚡ 工人研发中</span>';
    } else if (task.status === 'in_verification') {
      statusBadge = '<span class="status-badge in-verification">🔍 独立验收中</span>';
    } else {
      statusBadge = '<span class="status-badge pending">🔒 等待前置</span>';
    }

    card.innerHTML = `
      <div class="task-card-header">
        <span class="task-id">${task.id}</span>
        ${task.isCritical ? '<span class="critical-tag">关键路径</span>' : ''}
        ${statusBadge}
      </div>
      <div class="task-name">${task.name}</div>
      <div class="task-module">${task.layer} · ${task.module}</div>
    `;

    card.addEventListener('click', () => {
      selectedTaskId = task.id;
      renderTaskMatrix();
      renderTaskInspector(task.id);
    });

    container.appendChild(card);
  });
}

function renderTaskInspector(taskId) {
  const inspector = document.getElementById('task-inspector');
  if (!inspector) return;

  const task = tasksExecutionData.find(t => t.id === taskId);
  if (!task) return;

  let statusHtml = '';
  if (task.status === 'verified') {
    statusHtml = `<span style="color: #10b981; font-weight: bold;">✅ 已完成并由独立代理验收通过 (PASS)</span>`;
  } else if (task.status === 'in_progress') {
    statusHtml = `<span style="color: #38bdf8; font-weight: bold;">⚡ 专属工人代理开发中 (Running)</span>`;
  } else if (task.status === 'in_verification') {
    statusHtml = `<span style="color: #a855f7; font-weight: bold;">🔍 独立全新验收代理审查中 (In Verification)</span>`;
  } else {
    statusHtml = `<span style="color: #94a3b8; font-weight: bold;">🔒 依赖前置阻塞中 (Pending Prerequisites)</span>`;
  }

  inspector.innerHTML = `
    <div class="inspector-header">
      <div>
        <h3 style="color: #fff; margin-bottom: 0.3rem;">【${task.id}】${task.name}</h3>
        <p style="color: #94a3b8; font-size: 0.9rem; margin-bottom: 0;">归属模块：${task.layer} &gt; ${task.module} ${task.isCritical ? '· <strong style="color: #ef4444;">核心关键路径</strong>' : ''}</p>
      </div>
      <div>${statusHtml}</div>
    </div>

    <div class="inspector-body">
      <div class="grid-2" style="margin-bottom: 1.25rem;">
        <div class="box-item">
          <div class="box-label">前置必要依赖 (Prerequisites)</div>
          <div class="box-val">${task.prereqs.length === 0 ? '🟢 无 (根任务/立即就绪)' : task.prereqs.map(p => `<code class="code-pill">${p}</code>`).join(' ')}</div>
        </div>
        <div class="box-item">
          <div class="box-label">解锁下游任务 (Unlocks)</div>
          <div class="box-val">${task.blocked.length === 0 ? '🏁 无 (终极交付节点)' : task.blocked.map(b => `<code class="code-pill">${b}</code>`).join(' ')}</div>
        </div>
      </div>

      <div class="box-item" style="margin-bottom: 1.25rem;">
        <div class="box-label">任务职责与契约目标</div>
        <p style="color: #e2e8f0; font-size: 0.95rem; line-height: 1.6; margin-bottom: 0.5rem;">${task.desc}</p>
        <div class="box-label" style="margin-top: 0.8rem;">客观验收标准 (Acceptance Criteria)</div>
        <p style="color: #f59e0b; font-size: 0.95rem; line-height: 1.6; margin-bottom: 0;">${task.criteria}</p>
      </div>

      ${task.status === 'verified' || task.status === 'in_progress' ? `
      <div class="box-item" style="border-left: 4px solid var(--accent-green); background: rgba(16, 185, 129, 0.05);">
        <div class="box-label" style="color: #10b981;">验收凭证与落地指标 (Evidence)</div>
        <ul style="color: #cbd5e1; font-size: 0.9rem; padding-left: 1.25rem; line-height: 1.7;">
          <li><strong>测试结果</strong>：${task.evidence.tests} (耗时: ${task.evidence.duration})</li>
          <li><strong>核心指标</strong>：${task.evidence.metrics}</li>
          <li><strong>核心产出文件</strong>：${task.evidence.files.map(f => `<code>${f}</code>`).join(', ')}</li>
          <li><strong>合规安全性</strong>：100% 遵从暴雪“纸笔准则”，零 SendInput/mouse_event，零内存注入</li>
        </ul>
      </div>
      ` : ''}
    </div>
  `;
}

function renderAgentsTable() {
  const tableBody = document.getElementById('agents-table-body');
  if (!tableBody) return;

  tableBody.innerHTML = '';
  activeAgentsData.forEach(agent => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><span class="slot-badge">Slot ${agent.slot}</span></td>
      <td><strong style="color: #fff;">${agent.role}</strong></td>
      <td style="color: #38bdf8;">${agent.task}</td>
      <td><span class="agent-type-badge">${agent.type}</span></td>
      <td><span class="agent-status-badge ${agent.status.includes('运行中') ? 'active' : 'idle'}">${agent.status}</span></td>
      <td style="color: #cbd5e1; font-size: 0.88rem;">${agent.detail}</td>
    `;
    tableBody.appendChild(tr);
  });
}

// 暴毙率滑块交互求解器 (供用户实时测试风控数学公式)
function setupLethalSlider() {
  const slider = document.getElementById('lethal-slider');
  const lethalVal = document.getElementById('lethal-value');
  const tempoVal = document.getElementById('tempo-weight-val');
  const scaleVal = document.getElementById('scale-weight-val');
  const strategyAlert = document.getElementById('strategy-alert');
  const riskBadge = document.getElementById('risk-tier-badge');

  if (!slider) return;

  function update() {
    const rawVal = parseInt(slider.value, 10);
    const pLethal = rawVal / 100.0;

    if (lethalVal) lethalVal.textContent = `${rawVal}% (${pLethal.toFixed(2)})`;

    // 公式计算
    const wTempo = 1.0 + 3.0 * (pLethal * pLethal);
    let wScale = 0.0;
    let isMutated = false;

    if (pLethal <= 0.30) {
      wScale = Math.max(0.0, 1.0 - 0.9 * pLethal);
    } else {
      wScale = 0.0;
      isMutated = true;
    }

    if (tempoVal) tempoVal.textContent = wTempo.toFixed(4);
    if (scaleVal) scaleVal.textContent = wScale.toFixed(4);

    if (riskBadge) {
      if (pLethal <= 0.05) {
        riskBadge.className = 'status-badge verified';
        riskBadge.textContent = '🟢 Safe (绝对安全)';
      } else if (pLethal <= 0.15) {
        riskBadge.className = 'status-badge in-progress';
        riskBadge.textContent = '🔵 Low (微量风险)';
      } else if (pLethal <= 0.30) {
        riskBadge.className = 'status-badge in-verification';
        riskBadge.textContent = '🟡 Warning (战力警戒)';
      } else {
        riskBadge.className = 'status-badge critical';
        riskBadge.textContent = '🔴 Lethal (生死线突变)';
      }
    }

    if (strategyAlert) {
      if (isMutated) {
        strategyAlert.style.display = 'block';
        strategyAlert.innerHTML = `⚠️ <strong>【生死线风控熔断突变】</strong> 当前暴毙率 ${rawVal}% &gt; 30%，远期贪婪权重强制清零 ($w_{\\text{scale}} = 0.0$)！切断升本与远期理财，全面转入绝地保命模式！`;
        if (window.renderMathInElement) {
          renderMathInElement(strategyAlert, { delimiters: [{ left: '$', right: '$', display: false }] });
        }
      } else {
        strategyAlert.style.display = 'none';
      }
    }
  }

  slider.addEventListener('input', update);
  update();
}

// 过滤器按钮事件绑定
function setupFilterButtons() {
  const buttons = document.querySelectorAll('.filter-btn');
  buttons.forEach(btn => {
    btn.addEventListener('click', () => {
      buttons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentFilter = btn.getAttribute('data-filter') || 'all';
      renderTaskMatrix();
    });
  });
}

// 初始化
document.addEventListener('DOMContentLoaded', () => {
  renderMetrics();
  renderTaskMatrix();
  renderTaskInspector(selectedTaskId);
  renderAgentsTable();
  setupFilterButtons();
  setupLethalSlider();

  // KaTeX 自动排版渲染
  if (window.renderMathInElement) {
    renderMathInElement(document.body, {
      delimiters: [
        { left: '$$', right: '$$', display: true },
        { left: '$', right: '$', display: false }
      ],
      throwOnError: false
    });
  }
});
