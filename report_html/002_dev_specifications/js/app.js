document.addEventListener('DOMContentLoaded', () => {
  // Initialize KaTeX
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

  initMath();
  window.addEventListener('load', initMath);

  // Badge Simulator Interactive Logic
  const badgeTarget = document.getElementById('demo-badge');
  const cardName = document.getElementById('demo-card-name');
  const cardAtk = document.getElementById('demo-card-atk');
  const cardHp = document.getElementById('demo-card-hp');
  const badgeDesc = document.getElementById('badge-desc-text');
  const buttons = document.querySelectorAll('.spec-btn');

  const badgeConfigs = {
    triple: {
      text: "🔥 凑齐三连 (Triple +1)",
      className: "badge-triple",
      card: { name: "机械木马", atk: "4", hp: "4" },
      desc: "【传奇金色微标】场上已存在该随从 2 张或手牌有对子，购买即刻合成金色随从并发现高一本奖励卡。触发金光呼吸特效。"
    },
    synergy: {
      text: "★ 核心质变 (Engine +85)",
      className: "badge-synergy",
      card: { name: "熊妈妈", atk: "5", hp: "5" },
      desc: "【魔能紫色微标】当前流派后验概率高（野兽流 78%），购买后全队召唤野兽获得巨大数值提升，判定为流派发动机。"
    },
    tempo: {
      text: "🛡️ 即时保命 (Tempo Fix)",
      className: "badge-tempo",
      card: { name: "正义使者", atk: "7", hp: "4" },
      desc: "【翡翠绿色微标】下回合暴毙率预警（$P_{\\text{lethal}} > 30\\%$），该卡具备圣盾/剧毒即插即用属性，优先买入护脸苟活。"
    },
    trap: {
      text: "⛔ 避坑预警 (Low Value)",
      className: "badge-trap",
      card: { name: "中立白板怪", atk: "2", hp: "3" },
      desc: "【警戒红色微标】边际价值 $\\Delta V(a) < 0$，与当前阵容协同度极低，属于浪费 3 铸币的陷阱卡，建议跳过。"
    }
  };

  buttons.forEach(btn => {
    btn.addEventListener('click', () => {
      buttons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      const type = btn.getAttribute('data-type');
      const cfg = badgeConfigs[type];
      if (cfg) {
        badgeTarget.className = `hud-badge-floating ${cfg.className}`;
        badgeTarget.textContent = cfg.text;
        cardName.textContent = cfg.card.name;
        cardAtk.textContent = cfg.card.atk;
        cardHp.textContent = cfg.card.hp;
        badgeDesc.innerHTML = cfg.desc;
        // re-render math if any
        initMath();
      }
    });
  });
});
