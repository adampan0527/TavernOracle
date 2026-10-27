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

  // 2. Risk Aversion Simulator (P_lethal Slider)
  const slider = document.getElementById('lethal-slider');
  const lethalValElem = document.getElementById('lethal-value');
  const tempoWeightElem = document.getElementById('tempo-weight');
  const scaleWeightElem = document.getElementById('scale-weight');
  const statusIndicator = document.getElementById('status-indicator');
  const strategyTitle = document.getElementById('strategy-title');
  const strategyDesc = document.getElementById('strategy-desc');

  function updateRiskSimulation() {
    if (!slider) return;
    const p = parseFloat(slider.value) / 100.0;
    const pPercent = Math.round(p * 100);
    lethalValElem.textContent = `${pPercent}%`;

    // Formulas:
    // w_tempo = 1.0 + 3.0 * (p)^2
    // w_scale = max(0, 1.0 - 0.9 * p) (when p > 0.3, scale drops drastically)
    const wTempo = 1.0 + 3.0 * Math.pow(p, 2);
    let wScale = Math.max(0.0, 1.0 - 0.9 * p);

    tempoWeightElem.textContent = wTempo.toFixed(2);
    scaleWeightElem.textContent = (p > 0.30 ? Math.max(0.05, wScale * 0.3) : wScale).toFixed(2);

    if (p <= 0.15) {
      statusIndicator.style.background = 'rgba(16, 185, 129, 0.12)';
      statusIndicator.style.borderColor = 'rgba(16, 185, 129, 0.4)';
      strategyTitle.style.color = '#10b981';
      strategyTitle.textContent = '🛡️ 稳态贪婪模式 (Greedy / Scaling Mode)';
      strategyDesc.textContent = '暴毙概率极低 (≤ 15%)。算法优先升本、寻找理财三连、锁核心高星对子，积累远期成长复利。';
    } else if (p <= 0.30) {
      statusIndicator.style.background = 'rgba(245, 158, 11, 0.12)';
      statusIndicator.style.borderColor = 'rgba(245, 158, 11, 0.4)';
      strategyTitle.style.color = '#f59e0b';
      strategyTitle.textContent = '⚖️ 均衡防守模式 (Balanced Tempo Mode)';
      strategyDesc.textContent = '存在一定血量被蚕食风险 (15% ~ 30%)。算法平衡战力与资源，暂缓贪心升本，补齐场上残缺站位。';
    } else {
      statusIndicator.style.background = 'rgba(239, 68, 68, 0.15)';
      statusIndicator.style.borderColor = 'rgba(239, 68, 68, 0.5)';
      strategyTitle.style.color = '#ef4444';
      strategyTitle.textContent = '🚨 策略突变：紧急苟活保命 (Survival Emergency)';
      strategyDesc.textContent = '生死线告警 (> 30%)！成长权重被强制归零，切断一切升本与保留对子动作，100% 预算买入即插即用战力怪（圣盾/剧毒/高攻防）。';
    }
  }

  if (slider) {
    slider.addEventListener('input', updateRiskSimulation);
    updateRiskSimulation();
  }

  // 3. Archetype Selector Interactive Demo
  const archetypeSelect = document.getElementById('archetype-select');
  const archetypeInfo = document.getElementById('archetype-info');
  const archetypeCards = document.getElementById('archetype-cards');

  const archetypeData = {
    beasts: {
      name: '跳蛙/狂战野兽流',
      posterior: '82\\%',
      coreCards: ['跳蛙骑士 (1星)', '巨狼先祖 (6星)', '提塔斯·瑞文戴尔 (5星)', '下水道老鼠 (2星)'],
      strategy: '亡语复生频次堆叠 + 瑞文戴尔倍乘增益。战力计算重点评估亡语触发链条数。'
    },
    mechs: {
      name: '圣盾偏折机械流',
      posterior: '78\\%',
      coreCards: ['偏折机器人 (3星)', '自动化打击装置 (4星)', '回收机器人 (4星)', '吵吵模组 (4星磁力)'],
      strategy: '刷新圣盾制造白嫖优势 + 磁力保值回收。DP 预算应优先保障磁力贴合。'
    },
    murlocs: {
      name: '手牌剧毒鱼人流',
      posterior: '85\\%',
      coreCards: ['拜戈尔格国王 (5星)', '合唱鱼人 (6星)', '蛮鱼斥候 (5星)', '爆裂邦戈七重奏 (5星)'],
      strategy: '点杀剧毒 + 手牌身材爆发。重点在阶段中期快速升本寻找 5/6 星核心发动机。'
    },
    undead: {
      name: '全局攻击力亡语流',
      posterior: '74\\%',
      coreCards: ['永恒骑士 (2星)', '饥饿的憎恶 (5星)', '克苏恩教徒 (3星)', '永恒召唤者 (6星)'],
      strategy: '利用“本局对战中亡灵死亡数”全局成长，前中期不断卖血保频次，后期大怪连环生生不息。'
    }
  };

  if (archetypeSelect && archetypeInfo && archetypeCards) {
    archetypeSelect.addEventListener('change', (e) => {
      const data = archetypeData[e.target.value] || archetypeData.beasts;
      archetypeInfo.innerHTML = `<strong>后验推断概率 $P(\\text{流派} \\mid H, C, B) \\approx ${data.posterior}$</strong>：${data.strategy}`;
      archetypeCards.innerHTML = data.coreCards.map(c => `<span class="badge-metric" style="margin-right: 0.5rem; margin-bottom: 0.4rem;">${c}</span>`).join('');
      if (typeof renderMathInElement === 'function') {
        renderMathInElement(archetypeInfo, { delimiters: [{ left: '$', right: '$', display: false }] });
      }
    });
  }

  // 4. Roadmap Accordion Toggle
  const weekCards = document.querySelectorAll('.week-card');
  weekCards.forEach(card => {
    card.addEventListener('click', () => {
      weekCards.forEach(c => c.classList.remove('active'));
      card.classList.add('active');
    });
  });
});
