document.addEventListener('DOMContentLoaded', () => {
  // 1. Initialize KaTeX auto-render
  if (typeof renderMathInElement === 'function') {
    renderMathInElement(document.body, {
      delimiters: [
        { left: '$$', right: '$$', display: true },
        { left: '$', right: '$', display: false }
      ],
      throwOnError: false
    });
  }

  // 2. Interactive Route Comparison Toggle
  const btnSchemeA = document.getElementById('btn-scheme-a');
  const btnSchemeB = document.getElementById('btn-scheme-b');
  const comparisonContent = document.getElementById('comparison-content');

  const schemeDetails = {
    schemeA: {
      title: '方案 A：C# HDT 插件方案 (首推 MVP)',
      badge: '开发周期：1 ~ 2 周 · 推荐指数 ★★★★★',
      summary: '直接编译为 <code>TavernOracle.dll</code> 放入 HDT 的 <code>Plugins/</code> 目录。借由 HDT 宿主现成的四项核心基建（Power.log 监听器、HearthDb 全卡牌库、Bob\'s Buddy 蒙特卡洛战力引擎、WPF 游戏吸附 Overlay），开发者 100% 聚焦于决策算法！',
      points: [
        '✅ <strong>零成本拥有高容错日志流</strong>：无需自行编写复杂的异步文件监听与日志截断恢复。',
        '✅ <strong>直接调用 Bob\'s Buddy 战力</strong>：直接从 HDT 获取下一战暴毙概率 $P_{\\text{lethal}}$，省去编写数万行战斗模拟器。',
        '✅ <strong>原生融入万千玩家工作流</strong>：天梯玩家日常本来就开着 HDT，用户无需安装新客户端，零心理门槛。',
        '⚠️ <strong>约束</strong>：强依赖 HDT 宿主进程；UI 偏向传统 WPF，界面定制上限受宿主限制。'
      ]
    },
    schemeB: {
      title: '方案 B：Rust + Tauri 独立客户端方案 (现代独立版)',
      badge: '开发周期：3 ~ 4 周 · 推荐指数 ★★★★☆',
      summary: '采用 Rust 独立进程监听 <code>Power.log</code>，前端由 Webview2 渲染现代暗夜毛玻璃 HUD。完全脱离 HDT 单独运行。',
      points: [
        '✅ <strong>独立品牌与超轻包体</strong>：安装包 &lt; 20MB，内存占用 &lt; 40MB，无需依赖第三方宿主。',
        '✅ <strong>极致现代 UI 视觉</strong>：可无拘束使用 Vue 3 / React / Tailwind 实现酷炫战术动效。',
        '⚠️ <strong>需重新造轮子</strong>：必须自行解析 <code>Power.log</code>、维护 <code>cards.json</code> 卡牌库，并自行移植或实现蒙特卡洛战力模拟。',
        '⚠️ <strong>窗口吸附复杂度高</strong>：需自行处理 Win32 跨进程窗口位置同步（<code>WM_MOVE</code> / <code>WM_SIZE</code>）与 DPI 缩放。'
      ]
    }
  };

  function renderScheme(key) {
    const data = schemeDetails[key];
    comparisonContent.innerHTML = `
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.8rem;">
        <h3 style="color: #fff; margin: 0;">${data.title}</h3>
        <span class="badge-metric">${data.badge}</span>
      </div>
      <p style="font-size: 0.95rem; color: #cbd5e1; margin-bottom: 1rem;">${data.summary}</p>
      <div style="display: flex; flex-direction: column; gap: 0.5rem;">
        ${data.points.map(p => `<div style="font-size: 0.9rem; color: #94a3b8;">${p}</div>`).join('')}
      </div>
    `;
    if (typeof renderMathInElement === 'function') {
      renderMathInElement(comparisonContent, { delimiters: [{ left: '$', right: '$', display: false }] });
    }
  }

  if (btnSchemeA && btnSchemeB && comparisonContent) {
    btnSchemeA.addEventListener('click', () => {
      btnSchemeA.classList.add('active');
      btnSchemeB.classList.remove('active');
      renderScheme('schemeA');
    });

    btnSchemeB.addEventListener('click', () => {
      btnSchemeB.classList.add('active');
      btnSchemeA.classList.remove('active');
      renderScheme('schemeB');
    });

    renderScheme('schemeA');
  }

  // 3. Interactive HDT Hook Event Simulator
  const btnTurnStart = document.getElementById('btn-turn-start');
  const btnBuyMinion = document.getElementById('btn-buy-minion');
  const btnReroll = document.getElementById('btn-reroll');
  const simOutput = document.getElementById('sim-output');

  const logs = [];
  function addLog(msg, color = '#38bdf8') {
    const time = new Date().toLocaleTimeString();
    logs.unshift(`<span style="color: #64748b;">[${time}]</span> <span style="color: ${color};">${msg}</span>`);
    if (logs.length > 5) logs.pop();
    simOutput.innerHTML = logs.join('<br>');
  }

  if (btnTurnStart) {
    btnTurnStart.addEventListener('click', () => {
      addLog('🔔 HDT 触发 GameEvents.OnTurnStart(Turn 6, Gold 8)', '#f59e0b');
      addLog('⚡ TavernOracle 读取 Core.Game.Player.Board (7随从) & Bob\'s Buddy P_lethal=12%', '#38bdf8');
      addLog('🧠 背包 DP 耗时 3.2ms 求解完成：推荐升 4 本并冻结跳蛙对子', '#10b981');
      addLog('🎨 Core.OverlayCanvas 挂载徽标：在酒馆第2格随从头顶高亮 🔥【三连对子】', '#c084fc');
    });
  }

  if (btnBuyMinion) {
    btnBuyMinion.addEventListener('click', () => {
      addLog('🔔 HDT 触发 GameEvents.OnEntityCreated (买入随从 CardID=BG20_100)', '#f59e0b');
      addLog('⚡ 增量更新状态机：可用铸币 8 -> 5，手牌实体数 +1', '#38bdf8');
      addLog('🧠 重新评估战力收益 ΔV(a) = +4.5，推荐将新随从打出至左侧第 2 站位', '#10b981');
    });
  }

  if (btnReroll) {
    btnReroll.addEventListener('click', () => {
      addLog('🔔 HDT 触发 GameEvents.OnZoneChange (酒馆区刷新，耗费 1 铸币)', '#f59e0b');
      addLog('⚡ 读取 4 张新随从属性，贝叶斯流派后验概率跳蛙野兽流维持 82%', '#38bdf8');
      addLog('🧠 发现核心组件【提塔斯·瑞文戴尔】，立即浮层置顶黄色高亮 ★【高协同质变怪】', '#10b981');
    });
  }
});
