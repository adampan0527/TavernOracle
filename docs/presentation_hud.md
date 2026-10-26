# 表现层：Windows 透明点击穿透 HUD (Presentation Layer & HUD)

## 1. 核心设计原则

1. **绝对穿透**：所有鼠标点击必须 100% 穿透至底层炉石传说客户端，绝不影响玩家选卡、出牌、刷新等动作。
2. **零闪烁与硬件加速**：透明悬浮窗需保持高帧率与极低渲染占用。
3. **坐标自适应吸附**：动态监听游戏窗口的 `WM_MOVE`、`WM_SIZE` 以及分辨率缩放，与游戏卡牌和酒馆区域保持像素级对齐。

---

## 2. Win32 API 点击穿透实现

在 Windows 平台通过组合 `WS_EX_TRANSPARENT` 与 `WS_EX_LAYERED` 样式实现点击穿透：

```csharp
// Windows Win32 扩展样式关键代码 (点击穿透与半透明分层)
public const int WS_EX_TRANSPARENT = 0x00000020;
public const int WS_EX_LAYERED     = 0x00000080;
public const int GWL_EXSTYLE       = -20;

[DllImport("user32.dll")]
public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

[DllImport("user32.dll")]
public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

// 初始化悬浮窗时调用，使所有鼠标操作穿透至底层炉石客户端
public void EnableClickThrough(IntPtr hWnd) {
    int extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
    SetWindowLong(hWnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
}
```

---

## 3. 核心 HUD UI 组件规范

1. **动态卡面头顶微标 (Minion Top Badges)**：
   - 🔥 `对子核心`：场上已有 1 张，购买即凑齐三连。
   - ★ `高协同质变怪`：大幅提升当前阵容核心流派战斗力。
   - 🛡️ `即时保命单卡`：高身材 / 圣盾 / 剧毒即插即用。
2. **顶部决策横条 (Top Recommendation Banner)**：
   - 【本回合推荐】：升至 4 本并冻结（当前对战胜率 78%，留对子为佳）。
   - 【避坑预警】：勿刷新，当前剩余 3 铸币无理财，刷新将浪费 1 费。
3. **战力与暴毙预警仪 (Combat Odds & Lethal Gauge)**：
   - 实时显示预测胜率、打平率、战败暴毙率。
