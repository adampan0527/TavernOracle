document.addEventListener('DOMContentLoaded', () => {
  const lethalSlider = document.getElementById('lethal-slider');
  const lethalValue = document.getElementById('lethal-value');
  const statusBox = document.getElementById('status-indicator');
  const strategyTitle = document.getElementById('strategy-title');
  const strategyDesc = document.getElementById('strategy-desc');
  const tempoWeight = document.getElementById('tempo-weight');
  const scaleWeight = document.getElementById('scale-weight');

  function updateSimulation() {
    const pLethal = parseFloat(lethalSlider.value) / 100;
    lethalValue.textContent = `${Math.round(pLethal * 100)}%`;

    // Mathematical formula from docs/decision_engine.md
    // w_tempo = 1.0 + 3.0 * (P_lethal)^2
    // w_scale = 1.0 - 0.9 * P_lethal
    const wTempo = (1.0 + 3.0 * Math.pow(pLethal, 2)).toFixed(2);
    let wScale = (1.0 - 0.9 * pLethal).toFixed(2);
    if (pLethal > 0.3) {
      wScale = "0.00 (强制切断)";
    }

    tempoWeight.textContent = wTempo;
    scaleWeight.textContent = wScale;

    if (pLethal > 0.3) {
      statusBox.classList.add('lethal-warning');
      strategyTitle.textContent = "⚠️ 触发保命生死线模式 (Survival Mode)";
      strategyTitle.style.color = "#ef4444";
      strategyDesc.textContent = "暴毙风险高于 30%，系统立即切断升本与远期留对子，全部金币买即战力身材怪与剧毒。";
    } else {
      statusBox.classList.remove('lethal-warning');
      strategyTitle.textContent = "🛡️ 稳态发展模式 (Greedy / Scaling Mode)";
      strategyTitle.style.color = "#10b981";
      strategyDesc.textContent = "暴毙风险处于安全区间，建议升至 4 本并保留核心对子，争取后期成型。";
    }
  }

  function initMath() {
    if (typeof renderMathInElement === 'function') {
      renderMathInElement(document.body, {
        delimiters: [
          { left: '$$', right: '$$', display: true },
          { left: '$', right: '$', display: false },
          { left: '\\(', right: '\\)', display: false },
          { left: '\\[', right: '\\]', display: true }
        ],
        throwOnError: false
      });
    }
  }

  // Trigger KaTeX render
  initMath();
  window.addEventListener('load', initMath);

  if (lethalSlider) {
    lethalSlider.addEventListener('input', updateSimulation);
    updateSimulation();
  }
});
