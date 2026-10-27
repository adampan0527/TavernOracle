const tasksData = [
  {
    id: 'T01',
    name: 'HDT 插件工程脚手架搭建',
    module: '基础工程',
    layer: 'P0 基础环境',
    prereqs: [],
    blocked: ['T03', 'T04', 'T05', 'T15'],
    isCritical: true,
    desc: '初始化 .NET 8 / .NET Framework 类库工程，配置对 HearthstoneDeckTracker.exe 与 HearthDb.dll 的引用；配置 Post-Build 自动拷贝 DLL 至 HDT Plugins/ 目录。',
    criteria: 'HDT 启动时能在“选项-插件”列表中识别到空白的 TavernOracle 插件条目，无报错加载。'
  },
  {
    id: 'T02',
    name: '静态合规审查规则器',
    module: '合规安全',
    layer: 'P0 基础环境',
    prereqs: [],
    blocked: ['T20', 'T23'],
    isCritical: false,
    desc: '编写 Roslyn Analyzer 或 CI 静态检查脚本，对工程代码进行 AST 扫描，禁止引用 user32.dll!SendInput、mouse_event、SendKeys、OpenProcess 等模拟操作与注入 API。',
    criteria: '尝试引入模拟点击或驱动调用时，构建立即失败（Build Failure）并给出合规警报。'
  },
  {
    id: 'T03',
    name: 'HearthDb 元数据适配封装',
    module: '数据层',
    layer: 'P1 数据摄取',
    prereqs: ['T01'],
    blocked: ['T04', 'T14'],
    isCritical: true,
    desc: '封装 HearthDb.Cards.All，提取酒馆战棋随从星级、种族、基础身材、嘲讽/圣盾/剧毒/复生/磁力机制标签，以及普通与金色卡牌的三连对应映射。',
    criteria: '单元测试能通过 CardID 准确检索全量战棋随从的属性与三连映射，查询耗时 ≤ 0.1ms。'
  },
  {
    id: 'T04',
    name: '盘面状态轻量 DTO 与快照定义',
    module: '数据层',
    layer: 'P1 数据摄取',
    prereqs: ['T01', 'T03'],
    blocked: ['T05', 'T06', 'T11', 'T14'],
    isCritical: true,
    desc: '定义 BoardSnapshot, MinionEntity, HandEntity, TavernMinion, TavernTier, GoldBudget 等只读数据结构，采用值类型（Struct）与内存复用设计。',
    criteria: '结构体无堆内存分配垃圾，支持深度拷贝与不可变快照隔离。'
  },
  {
    id: 'T05',
    name: 'HDT 游戏事件转换与状态机',
    module: '数据层',
    layer: 'P1 数据摄取',
    prereqs: ['T01', 'T04'],
    blocked: ['T20'],
    isCritical: false,
    desc: '监听 HDT GameEvents.OnGameStart, OnTurnStart, OnEntityCreated, OnZoneChange，将 HDT Core.Game 实体转换为内部统一的 BoardSnapshot。',
    criteria: '当局势发生变化（买怪、刷新、打出）时，状态机能在 ≤ 2ms 内产出当前盘面不可变快照。'
  },
  {
    id: 'T06',
    name: '离线盘面对局回放测试数据集',
    module: '数据测试',
    layer: 'P1 数据摄取',
    prereqs: ['T04'],
    blocked: ['T21'],
    isCritical: false,
    desc: '录制或手工编写典型战局样本集（如跳蛙野兽流转折点、残血紧急苟活、神仙决赛圈等至少 20 组盘面快照 JSON 文件），供算法离线脱机调试。',
    criteria: '单元测试工程可在完全不启动炉石和 HDT 的脱机环境下快速加载盘面快照。'
  },
  {
    id: 'T07',
    name: 'Bob\'s Buddy 战力模拟桥接适配器',
    module: '战力仿真',
    layer: 'P2 先验仿真',
    prereqs: [],
    blocked: ['T10'],
    isCritical: false,
    desc: '编写对 HDT 内置 BobsBuddyInvoker 的访问包装类，当战斗结束或回合切换时提取下一场预测暴毙率 P_lethal 与平局/胜率浮点值。',
    criteria: '在 HDT 运行环境下能正常接收 Bob\'s Buddy 的对战模拟结果；脱机模式下优雅降级使用默认安全值（P_lethal = 0.10）。'
  },
  {
    id: 'T08',
    name: '流派构筑与种族协同知识图谱',
    module: '先验知识',
    layer: 'P2 先验仿真',
    prereqs: [],
    blocked: ['T09'],
    isCritical: false,
    desc: '编写 archetypes.json 规则库，定义各大流派（跳蛙野兽、偏折机械、手牌鱼人、亡灵复生等）的核心发动机随从 ID、协同收益矩阵与种族位掩码（BitMask）。',
    criteria: '验证 JSON 格式合法，且各大主流流派的核心组件完备定义，无无效 CardID。'
  },
  {
    id: 'T09',
    name: '贝叶斯流派后验概率推导器',
    module: '算法层',
    layer: 'P2 先验仿真',
    prereqs: ['T08'],
    blocked: ['T12'],
    isCritical: false,
    desc: '输入当前英雄 H、禁选种族池 B 以及场上手牌已有组件 C，按公式 P(流派_k | H, C, B) ∝ P(C | 流派_k) * P(流派_k | H, B) 推导各流派概率分布。',
    criteria: '单元测试验证：场上有跳蛙与瑞文戴尔时，野兽流后验概率显著提升至 ≥ 80%；推导耗时 ≤ 0.5ms。'
  },
  {
    id: 'T10',
    name: '战力生死线风控调节阀',
    module: '算法层',
    layer: 'P3 决策算法',
    prereqs: ['T07'],
    blocked: ['T12'],
    isCritical: false,
    desc: '实现保命权重 w_tempo = 1.0 + 3.0 * (P_lethal)^2 与远期贪婪权重 w_scale = max(0, 1.0 - 0.9 * P_lethal) 动态调节；当 P_lethal > 30% 触发策略突变切断贪婪逻辑。',
    criteria: '单元测试通过边界值验证：在 P_lethal = 0.05 时保持贪婪升本；在 P_lethal = 0.40 时贪婪权重强制归零。'
  },
  {
    id: 'T11',
    name: '原子操作候选动作集生成器',
    module: '算法层',
    layer: 'P3 决策算法',
    prereqs: ['T04'],
    blocked: ['T12'],
    isCritical: true,
    desc: '根据当前酒馆在售随从、手牌与战场 7 个卡位，生成当前回合所有合法原子动作分支：BUY(id), SELL(slot), REROLL, FREEZE, UPGRADE_TIER, PLAY(slot, pos)。',
    criteria: '生成的候选动作不包含非法行为（如卡位已满仍生成买入非战吼怪、金币不足仍升本等），生成耗时 ≤ 0.5ms。'
  },
  {
    id: 'T12',
    name: '动作边际收益价值评估器',
    module: '算法层',
    layer: 'P3 决策算法',
    prereqs: ['T09', 'T10', 'T11'],
    blocked: ['T13'],
    isCritical: true,
    desc: '实现边际收益公式 ΔV(a) = w_tempo * Δ战力 + w_scale * Δ协同 + 对子加成，对单一动作增量打分。',
    criteria: '面对对子牌时能给出额外的对子加权分；面对高协同质变怪能给出协同加权；打分耗时 ≤ 1ms。'
  },
  {
    id: 'T13',
    name: '分支定界有界背包 DP 求解器',
    module: '算法层',
    layer: 'P3 决策算法',
    prereqs: ['T12'],
    blocked: ['T20'],
    isCritical: true,
    desc: '在可用铸币与 7 卡位双重约束下，对动作树执行分支定界剪枝搜索，输出全局评分最高的最优操作序列；设置 4.5ms 硬超时保底机制。',
    criteria: '在极端复杂局面（10 金币、满手牌、多对子）下搜索全流程耗时严格控制在 ≤ 5ms 内，且不产生堆内存 GC。'
  },
  {
    id: 'T14',
    name: '随从站位排列启发式排序器',
    module: '算法层',
    layer: 'P3 决策算法',
    prereqs: ['T03', 'T04'],
    blocked: ['T20'],
    isCritical: false,
    desc: '根据场上 7 个随从的嘲讽、风怒、圣盾、破盾怪、亡语触发顺序及防狂战斧规则，输出最优站位坐标序列。',
    criteria: '能够自动将圣盾狂战斧排在首位、嘲讽圣盾靠右、关键后排发动机（瑞文/铜须）置于最右侧防狂战斧波及。'
  },
  {
    id: 'T15',
    name: 'WPF 透明点击穿透覆盖层基类',
    module: '表现层',
    layer: 'P4 表现 HUD',
    prereqs: ['T01'],
    blocked: ['T16', 'T17', 'T18', 'T19'],
    isCritical: false,
    desc: '实现挂载至 HDT Core.OverlayCanvas 的自定义 WPF 控件；调用 Win32 API 设置 WS_EX_TRANSPARENT | WS_EX_LAYERED 样式。',
    criteria: '鼠标在悬浮窗区域进行高频拖拽、点击出牌，游戏原生点击操作 100% 畅通无阻，绝不丢牌卡手。'
  },
  {
    id: 'T16',
    name: '酒馆卡面头顶微标控件 (Badge)',
    module: '表现层',
    layer: 'P4 表现 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    desc: '开发卡面上浮动态推荐微标（🔥【三连对子】、★【流派质变怪】、🛡️【即时保命单卡】）；支持平滑浮现与呼吸动效。',
    criteria: '微标在随从正上方精准对齐，无文本溢出或视觉遮挡卡牌身材（攻防数值）。'
  },
  {
    id: 'T17',
    name: '顶部决策横条控件 (Banner)',
    module: '表现层',
    layer: 'P4 表现 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    desc: '开发屏幕顶部半透明大局策略横条，展示本回合核心战略（如“升至 4 本并冻结跳蛙”、“切勿刷新，存铸币防暴毙”）。',
    criteria: '文本自适应缩放排版，支持根据危险程度呈现绿、黄、红三种视觉警示状态。'
  },
  {
    id: 'T18',
    name: '胜率与暴毙概率仪表盘 (Gauge)',
    module: '表现层',
    layer: 'P4 表现 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    desc: '开发轻量级暴毙预警仪，实时渲染预测胜率、打平率与暴毙率百分比与动态颜色警示条。',
    criteria: '随战力模拟输出动态刷新，帧率稳定，GPU 绘制占用趋近于 0。'
  },
  {
    id: 'T19',
    name: '游戏窗口吸附与坐标变换对齐',
    module: '表现层',
    layer: 'P4 表现 HUD',
    prereqs: ['T15'],
    blocked: ['T20'],
    isCritical: false,
    desc: '监听 HDT 窗口吸附通知与 Win32 WM_MOVE、WM_SIZE，在窗口化、全屏及系统 DPI 缩放（100%、125%、150%）下保持卡面像素级对齐。',
    criteria: '在拖动炉石游戏窗口或改变分辨率时，微标与横条实时跟随吸附，偏移误差 < 2px。'
  },
  {
    id: 'T20',
    name: 'HDT IPlugin 主入口与生命周期装配',
    module: '系统集成',
    layer: 'P5 集成发布',
    prereqs: ['T02', 'T05', 'T13', 'T14', 'T16', 'T17', 'T18', 'T19'],
    blocked: ['T21', 'T22'],
    isCritical: true,
    desc: '实现 IPlugin 接口全部生命周期（OnLoad, OnUpdate, OnUnload），串联事件总线、算法引擎与表现层，组装核心链路。',
    criteria: '在完整对局中从选英雄、进酒馆、打对战直至游戏结束，全流程零异常崩溃（Zero Unhandled Exceptions）。'
  },
  {
    id: 'T21',
    name: '离线基准性能与零 GC 压测套件',
    module: '质量验证',
    layer: 'P5 集成发布',
    prereqs: ['T06', 'T20'],
    blocked: ['T23'],
    isCritical: true,
    desc: '基于 T06 的 20 组对局快照批量循环运行 10,000 次，利用 BenchmarkDotNet 统计 P99 决策耗时与 Gen0/1/2 垃圾回收次数。',
    criteria: '决策链路平均耗时 ≤ 3.5ms，P99 耗时 ≤ 4.8ms，每千次推理产生的 GC 内存分配为 0 字节（Zero GC Allocation）。'
  },
  {
    id: 'T22',
    name: '玩家个性化配置与快捷控制面板',
    module: '用户体验',
    layer: 'P5 集成发布',
    prereqs: ['T20'],
    blocked: ['T23'],
    isCritical: false,
    desc: '在 HDT 插件设置界面嵌入配置项：HUD 整体开关、推荐激进/保守度调节滑块、各组件可见性勾选。',
    criteria: '修改配置后实时生效，设置持久化存储于本地 XML/JSON 配置文件中。'
  },
  {
    id: 'T23',
    name: '自动化构建流水线与版本交付包',
    module: '工程发布',
    layer: 'P5 集成发布',
    prereqs: ['T02', 'T21', 'T22'],
    blocked: [],
    isCritical: true,
    desc: '编写 GitHub Actions / 本地 MSBuild 脚本，执行合规静态审查、跑通全部单测与压测，编译并压缩打包最终交付物 TavernOracle-v1.0.0.zip。',
    criteria: '一键构建生成符合 HDT 插件发布规范的 Zip 包，解压至 Plugins/ 目录即刻开箱即用。'
  }
];

// State
let selectedTaskId = 'T01';
let currentFilter = 'all';
let completedTasks = new Set();

document.addEventListener('DOMContentLoaded', () => {
  // 1. Initialize KaTeX
  if (typeof renderMathInElement === 'function') {
    renderMathInElement(document.body, {
      delimiters: [
        { left: '$$', right: '$$', display: true },
        { left: '$', right: '$', display: false }
      ],
      throwOnError: false
    });
  }

  const matrixElem = document.getElementById('dag-matrix');
  const filterBtns = document.querySelectorAll('.filter-btn');

  function isTaskReady(task) {
    if (task.prereqs.length === 0) return true;
    return task.prereqs.every(p => completedTasks.has(p));
  }

  function renderMatrix() {
    if (!matrixElem) return;
    matrixElem.innerHTML = '';

    const selectedTask = tasksData.find(t => t.id === selectedTaskId);
    const upstreamIds = new Set(selectedTask ? selectedTask.prereqs : []);
    const downstreamIds = new Set(selectedTask ? selectedTask.blocked : []);

    tasksData.forEach(task => {
      const ready = isTaskReady(task);
      const isDone = completedTasks.has(task.id);

      // Filtering
      if (currentFilter === 'ready' && !ready) return;
      if (currentFilter === 'blocked' && ready) return;
      if (currentFilter === 'critical' && !task.isCritical) return;

      const node = document.createElement('div');
      node.className = `task-node ${task.id === selectedTaskId ? 'selected' : ''}`;
      if (task.prereqs.length === 0) node.classList.add('is-root');
      if (task.isCritical) node.classList.add('is-critical');
      if (ready && !isDone) node.classList.add('is-ready');
      if (upstreamIds.has(task.id)) node.classList.add('is-upstream');
      if (downstreamIds.has(task.id)) node.classList.add('is-downstream');

      let badgeClass = 'badge-blocked';
      let badgeText = '锁定中';
      if (isDone) {
        badgeClass = 'badge-done';
        badgeText = '✓ 已完成';
      } else if (ready) {
        badgeClass = 'badge-ready';
        badgeText = '🟢 可开工';
      }

      node.innerHTML = `
        <div class="task-header">
          <span class="task-id">${task.id}</span>
          <span class="task-status-badge ${badgeClass}">${badgeText}</span>
        </div>
        <div class="task-name">${task.name}</div>
        <div class="task-module">${task.module} · ${task.layer}</div>
      `;

      node.addEventListener('click', () => {
        selectedTaskId = task.id;
        renderMatrix();
        renderInspector();
      });

      matrixElem.appendChild(node);
    });
  }

  function renderInspector() {
    const task = tasksData.find(t => t.id === selectedTaskId) || tasksData[0];
    const isDone = completedTasks.has(task.id);
    const ready = isTaskReady(task);

    const inspector = document.getElementById('task-inspector');
    if (!inspector) return;

    const prereqBadges = task.prereqs.length > 0
      ? task.prereqs.map(p => `<span class="badge-metric" style="margin-right: 0.4rem; background: rgba(56, 189, 248, 0.2); color: #38bdf8;">${p}</span>`).join('')
      : '<span style="color: #34d399; font-size: 0.85rem;">无 (根任务，入度为 0)</span>';

    const blockedBadges = task.blocked.length > 0
      ? task.blocked.map(b => `<span class="badge-metric" style="margin-right: 0.4rem; background: rgba(192, 132, 252, 0.2); color: #c084fc;">${b}</span>`).join('')
      : '<span style="color: #94a3b8; font-size: 0.85rem;">无 (终点交付物)</span>';

    inspector.innerHTML = `
      <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-color); padding-bottom: 0.8rem; margin-bottom: 1rem;">
        <div>
          <span class="task-id" style="font-size: 1.1rem; margin-right: 0.6rem;">${task.id}</span>
          <strong style="font-size: 1.2rem; color: #fff;">${task.name}</strong>
          ${task.isCritical ? '<span class="badge-metric" style="margin-left: 0.6rem; background: rgba(239, 68, 68, 0.2); color: #f87171;">🔥 关键路径</span>' : ''}
        </div>
        <button id="toggle-done-btn" class="filter-btn" style="${isDone ? 'background: #059669; color: #fff;' : 'background: #182238; color: #38bdf8; border-color: #38bdf8;'}">
          ${isDone ? '✓ 标记为未完成' : '☑ 模拟标记完成 (解锁后置任务)'}
        </button>
      </div>

      <div class="inspector-grid">
        <div class="inspector-card">
          <div class="inspector-label">前置依赖 (Prerequisites)</div>
          <div class="inspector-value">${prereqBadges}</div>
        </div>

        <div class="inspector-card">
          <div class="inspector-label">解锁后置 (Direct Dependents)</div>
          <div class="inspector-value">${blockedBadges}</div>
        </div>

        <div class="inspector-card" style="grid-column: 1 / -1;">
          <div class="inspector-label">任务职责与输入输出契约</div>
          <div class="inspector-value" style="line-height: 1.6;">${task.desc}</div>
        </div>

        <div class="inspector-card" style="grid-column: 1 / -1; border-left: 3px solid #10b981;">
          <div class="inspector-label" style="color: #10b981;">验收标准 (Acceptance Criteria)</div>
          <div class="inspector-value" style="color: #cbd5e1;">${task.criteria}</div>
        </div>
      </div>
    `;

    const toggleBtn = document.getElementById('toggle-done-btn');
    if (toggleBtn) {
      toggleBtn.addEventListener('click', () => {
        if (completedTasks.has(task.id)) {
          completedTasks.delete(task.id);
        } else {
          completedTasks.add(task.id);
        }
        renderMatrix();
        renderInspector();
      });
    }

    if (typeof renderMathInElement === 'function') {
      renderMathInElement(inspector, { delimiters: [{ left: '$', right: '$', display: false }] });
    }
  }

  filterBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      filterBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentFilter = btn.dataset.filter;
      renderMatrix();
    });
  });

  renderMatrix();
  renderInspector();
});
