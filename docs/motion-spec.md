# 主题图标动效说明（motion spec）

本文件记录主题切换图标的动效决策。素材由 [`tools/gen-theme-lottie.mjs`](../tools/gen-theme-lottie.mjs)
生成（`src/TodoApp/Assets/lottie/theme-3state.json`），再用 LottieGen 编译成 C# 动画源。

## 1. 输入约束

| 项 | 取值 | 来源 |
|---|---|---|
| 状态数 | 3（跟随系统 / 日间 / 夜间） | 产品需求 |
| 控件类型 | 20×20 图标按钮，位于页面右上角 | UI |
| 播放方式 | 点击循环，任意两态之间可正向/反向播放 | 交互设计 |

## 2. 动效人格（Motion Personality）

按 `motion-design` 的四种原型，这里选 **Corporate**：

- 时长 200–400 ms，缓动 `cubic-bezier(0.2, 0, 0, 1)`，过冲 0–3 %
- 理由：这是设置类控件，不是营销插画；克制、可预测比“活泼”更重要
- 图标过渡的通用区间是 **150–250 ms**，本项目每条腿取上限 **250 ms**（15 帧 @60fps）

## 3. 时间线（fr 60，帧 0–45，总 750 ms，每条腿 250 ms）

`AnimatedVisualPlayer.PlayAsync(from, to)` **不能倒放**：传 `1 → 0` 会正向播完并停在进度 1，
这就是「夜间切回跟随系统后停在月亮」的原因。所以素材被排成**三段顺序播放的正向腿**，
应用侧永远只往前播：

| 腿 | 帧 | 进度 | 转换 |
|---|---|---|---|
| 1 | 0 → 15 | 0 → 1/3 | 跟随系统（显示器）→ 日间（太阳） |
| 2 | 15 → 30 | 1/3 → 2/3 | 日间（太阳）→ 夜间（月亮） |
| 3 | 30 → 45 | 2/3 → 1 | 夜间（月亮）→ 跟随系统（显示器） |

静态帧：`0` = 跟随系统、`1/3` = 日间、`2/3` = 夜间；第 45 帧与第 0 帧同画面，
所以「夜间 → 跟随系统」播到进度 1 时落到显示器，不会再停在月亮。

应用侧映射（`TodoScreen.Transition`）：

| 上一态 → 当前态 | 播放区间 |
|---|---|
| 系统 → 日间 | `0 → 1/3` |
| 日间 → 夜间 | `1/3 → 2/3` |
| 夜间 → 系统 | `2/3 → 1` |
| 冷启动落在夜间 | `0 → 2/3`（走完前两条腿） |
## 4. 属性与分层（避免“纯透明度”的廉价感）

`motion-design` 的硬性规则：重要状态变化不能只动透明度，必须叠加位置或缩放。因此：

| 层 | 属性 | 作用 |
|---|---|---|
| Primary | `position` + `opacity`（日出/日落，**左升右落**） | 新图标从左下 `(12, 84)` 沿弧线经 `(30, 62)` 升到中心 `(50, 50)`；旧图标从中心经 `(70, 62)` 落到右下 `(88, 84)` 并淡出 |
| Secondary | 出入错峰（先出后进，各 9 帧） | 旧图标完全消失后新图标才升到中央，两者不会在中途重叠 |
| Ambient | 无 | 20×20 的控件上加环境动效会变成噪音，按“premium = 做减法”主动省略 |\n\n三个状态（显示器 / 太阳 / 月亮）走**同一条“地平线履带”**，所以「跟随系统」也参与升降，而不是被硬切打断。

## 5. 缓动

所有关键帧使用同一组贝塞尔控制点 `o: (0.2, 0)`、`i: (0, 1)`，即 `cubic-bezier(0.2, 0, 0, 1)`：

- 入场 = 减速（快起、缓落）→ 符合 `entrance → decelerate`
- 出场同样使用该曲线，因为出场阶段与新入场阶段在同一条时间线上相邻，统一曲线更连贯
- 没有使用 linear（`motion-design` 明确禁止：空间/形状运动不能用线性）

## 6. Lottie 结构要点（依据 `text-to-lottie`）

- 顶层字段齐全：`v / fr / ip / op / w / h / nm / assets / layers`，`op` 为独占值（31）
- **一个形状一个组**：LottieGen 在同一组里放多个形状会报 `LT0027` 并丢弃多余形状
- 单色（`[0, 0, 0, 1]`）→ LottieGen `-GenerateColorBindings` 暴露 `Color_000000`，
  运行时按主题改成白色（深色）或近黑（浅色）
- `markers` 标注三态（`system@0 / light@15 / dark@30`），便于以后做「跳到某状态」的调试

## 7. 两个已修复的实现 bug（附自检）

第一版存在两个缺陷，都出在生成脚本里，现在脚本自带断言防止回归：

| 现象 | 原因 | 修复 | 自检 |
|---|---|---|---|
| 夜间图标是一个圆圈，不是弯月 | 弯月取错了圆弧：应该用外圆的**左侧**弧 + 内圆的**左侧**弧（缺口在右侧），却取了右侧弧——那一侧正好被内圆覆盖，拼出来就是整圆 | 弧线方向改为左弧，并按相反方向闭合 | 断言路径 `max x < 15`（整圆会到 21，弯月停在交点 ~11–13） |
| 夜间切回「跟随系统」后仍显示夜间 | ① 月亮图层首个关键帧在 t=18，时间线开头没有关键帧，Lottie 会把首帧值回填到起点之前，progress 0 时月亮可见；② 更根本的是 `PlayAsync` 只会正向播，`1 → 0` 实际停在了进度 1（月亮） | ① 在 t=0 补 `opacity = 0`；② 素材排成三段正向腿，应用只做正向播放 | 断言 frame 0/15/30/45 的三层可见性，并断言所有转换 `from < to` |

## 8. 图标视觉尺寸对齐（与字体图标）

主题按钮是 Lottie（矢量），旁边的语言按钮是 Segoe MDL2 字形，两者天然不同尺寸。用像素实测校准，而不是凭感觉：

| 项 | 初始 | 校准后（浅色实测） |
|---|---|---|
| 主题按钮 / 语言按钮 | 44×33 / **40×29** | **44×33 / 44×33** |
| 主题墨迹 / 语言墨迹 | **11×11** / 16×16 | **16×16 / 16×16** |
| 墨迹偏移 | (17,11) / (12,6) | **(14,8) / (14,8)** |
| **笔画宽度**（水平连续段） | 中位 2px / 均值 3.74 | **中位 2px / 均值 2.73**（字形 2px / 2.42） |
| 墨迹像素数 | 41 / 108 | 82 / 116 |

三条措施：

1. **统一图标盒**：按钮内容都放进 `IconHost()` 的固定 `20×20` Border，两个按钮因此同尺寸、墨迹同偏移 `(14, 8)`。
2. **放大画布内图形**：Lottie 的 100 单位画布中图形只占约 52%（42–54 单位），在 20px 播放器里只渲染出 11px 墨迹。生成时加 `ART_SCALE = 145`，把图形放大到占满画布，墨迹变为 16–17px（与 16px 字形一致），且不影响布局。
3. **匹配笔画粗细（关键）**：这里不能只看墨迹总量——`墨迹像素数` 还取决于图形复杂度（地球字形线条多，显示器只是一个外框）。
   真正决定“粗细观感”的是**笔画宽度**，用「水平墨迹连续段长度」度量：描边 7 单位时中位 **2px**（均值 3.74），
   而 16px 字形是 **1–2px**（均值 2.25）——所以看起来偏粗。把描边调到 **4.2 单位**（≈1.22px 理论宽度）后，
   实测中位 2px、均值 2.73，与字形的 2.42 基本一致。

测量工具：[`tools/measure-icon-ink.ps1`](../tools/measure-icon-ink.ps1)——PrintWindow 截图后，按**按钮自身背景色**判定墨迹
（自适应深/浅主题，早期用固定亮度阈值时深色主题会把背景当成墨迹），输出墨迹包围盒、像素数与笔画宽度统计。

## 9. 语言按钮的按下反馈

只有**多语言切换按钮**有该效果（主题按钮不加，它本身已有 Lottie 过渡）。按下时**图标**缩到 **0.8**、
松开回 **1.0**（按 90 ms / 松 160 ms，`cubic-bezier(0.2,0,0,1)`）——按钮底色不动，只有图标缩，
所以读起来是“某个东西被按进去了”。

实现：`Components/PressFeedback.cs`，调用 `.WithIconPressScale()`。

### 三个必须踩过的坑

1. **`ButtonElement.Set(...)` 在内容赋值前执行**
   `Set(Action<Button>)` 的时机在元素构建过程中，此时 `button.Content` 还是 `null`，直接取图标会拿到空。
   必须等 `Loaded` 之后再接线（`OnCreated` → `Loaded` → `Wire`）。

2. **`ButtonBase` 会把 `PointerPressed` 标记为已处理**
   元素级 `button.PointerPressed += ...` 永远收不到，必须
   `button.AddHandler(UIElement.PointerPressedEvent, handler, handledEventsToo: true)`。
   （这也是“看起来接好了但完全没反应”的原因。）

3. **`OnMount` 会把这个按钮的样式冲掉**
   用 `OnMount` 挂回调后，语言按钮从 subtle（无底色）变成了默认带边框的按钮：实测按钮内部像素
   `#FBFBFB` + 边框 `#E5E5E5`，而正常应该是页面底色 `#F3F3F3`。改用 `Set(...)` 后样式正常。
   （附带发现：`WithFlyout(...)` 返回的是基类 `Element`，在其之后接任何泛型扩展都会把 `T` 推断成
   `Element`，`with` 表达式因此切掉派生记录的字段。）

### 其它要点

- 缩放对象是 `Button.Content`（图标宿主）的 `Visual.Scale`，`CenterPoint` 取图标自身尺寸的一半
  ——实测缩放中心 `(10, 10)`（20×20 图标），不是按钮的 `(22, 16.5)`。
- `Visual.Scale` 在 WinUI 3 里是 **Vector3**，用 `Vector2` 关键帧会抛
  `ArgumentException: The expression output does not match animating property type`。
- 用合成器而不用 XAML `RenderTransform`：后者会按分数比例重新栅格化字形，动画期间发糊。
- `PointerExited` / `PointerCaptureLost` 也复位，指针拖出按钮不会卡在缩小状态。

### 验证方式

`tools/test-icon-press.ps1`：UIA 定位按钮 → `SetForegroundWindow` + `SetCursorPos` + `mouse_event`
**注入真实鼠标按下/抬起**（UIA 的 `InvokePattern` 不产生 pointer-down，测不出来），
再读应用写出的探针日志。实测输出：

```
created isLoaded=False content=<null>       <- 印证坑 1
loaded  content=Border
wire ok icon=Border
handler pressed
handler released                             <- 按住 220ms
handler released scale=<0.8, 0.8, 1>         <- 图标缩到 0.8
```

同时用 `tools/inspect-button-style.ps1` 核对按钮样式：两个按钮的底色/边框像素都等于页面底色 `#F3F3F3`。
## 10. 验证

| 检查 | 结果 |
|---|---|
| JSON 可解析 | `node -e "JSON.parse(...)"` 通过 |
| LottieGen 编译 | 0 warning（`-WinUIVersion 3.0 -GenerateColorBindings`） |
| 运行期加载 | `IsAnimatedVisualLoaded=True`，`Duration=517 ms` |
| 三段过渡 | `0→1/3`、`1/3→2/3`、`2/3→1`，全部正向；每次切换后 `error=none` |

> 说明：`text-to-lottie` 默认要求在它自带的 Skia Skottie 播放器里逐帧核对；本项目没有该播放器，
> 改用 **LottieGen 编译 + WinUI `AnimatedVisualPlayer` 运行期状态**验证，这是与实际交付目标一致的检查。










