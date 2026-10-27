# AGENTS.md

面向 AI 助手与本项目贡献者的**工程说明**。README 只讲「这是什么、怎么跑」；设计取舍、Reactor 踩坑、
验证方法、提交规范都放在这里。

---

## 1. 项目是什么

用 [`microsoft/microsoft-ui-reactor`](https://github.com/microsoft/microsoft-ui-reactor) 写的 WinUI 3 待办清单示例。
Reactor 是微软实验性的**声明式、组件化 C# UI 框架**：渲染出真正的 WinUI 控件，但界面用 C# 工厂方法 + Hooks 描述
——**没有 XAML、没有 ViewModel、没有 code-behind**。

目标是「一个能直接跑的小应用，把 Reactor 的核心用法讲清楚」，所以优先保证：**原生观感（Win10/Win11 一致）**、
**构建 0 警告**、**每条结论都有实测数据支撑**。

## 2. 技术栈与版本约束

| 项 | 值 | 约束原因 |
|---|---|---|
| TFM | `net10.0-windows10.0.26100.0` | 跟模板一致 |
| `Microsoft.UI.Reactor` | `0.1.0-preview.16` | 实验性预览，API 会变；升级前先读包内 `agentkit/reactor.api.txt` |
| `Microsoft.Graphics.Win2D` | `1.4.0` | LottieGen 产物需要 ≥1.4.0；写 1.3.2 会触发 NU1605 降级错误 |
| 打包形态 | 单项目 MSIX（Packaged），同时 `WindowsAppSDKSelfContained=true` | 双击 exe 也能直接跑，不必预装 Windows App Runtime |
| 平台 | 必须显式 `-p:Platform=x64`（或 arm64） | 打包项目不支持 AnyCPU |
| 目标系统 | Windows 10 1809+ | Win10 只有 `Segoe MDL2 Assets`，**没有** `Segoe Fluent Icons` |

## 3. 目录结构

```
ReactorTodo.slnx
AGENTS.md                     # 本文件：工程规范 / 设计决策 / 踩坑 / 提交规范
README.md                     # 面向使用者：功能、环境、快速开始
THIRD-PARTY-NOTICES.md        # 第三方素材与工具许可
docs/
  screenshot.png              # README 用截图
  screenshot-confirm.png      # 二次确认对话框截图
  motion-spec.md              # 主题图标动效的完整决策记录与实测数据
src/TodoApp/
  Program.cs                  # 入口：一行 ReactorApp.Run<App>(...)
  App.cs                      # 根组件：主题/语言偏好、LocaleProvider、系统主题订阅、标题栏配色
  TodoScreen.cs               # 主界面（在 LocaleProvider 内，UseIntl 取文案）+ 主题图标
  Models/TodoItem.cs          # record 数据模型
  State/TodoState.cs          # 状态 / 动作 / 纯函数 reducer / 过滤器
  State/AppSettings.cs        # 主题与语言偏好、locale 解析
  State/SystemTheme.cs        # UISettings 判定系统深浅色 + 变更订阅
  Storage/TodoStore.cs        # 待办 JSON 持久化（源生成序列化）
  Storage/SettingsStore.cs    # 偏好 JSON 持久化
  Localization/
    StringKeys.cs             # 文案 key 常量（写错编译期报错）
    AppStringProvider.cs      # IStringResourceProvider：中/英/日 + 四级回退链
    IntlExtensions.cs         # intl.T(key, args)
  Components/
    AddTodoBar.cs             # 输入框 + Enter 提交
    TodoRow.cs                # 单行卡片：勾选、行内编辑、删除（走二次确认）
    FilterBar.cs              # SelectorBar 过滤器
    TodoCommands.cs           # dispatch 的稳定门面（UseMemo 缓存）
    Buttons.cs                # 按钮语义：主 / 普通 / 轻量 / 危险
    ConfirmDialog.cs          # 状态驱动的二次确认对话框
    PressFeedback.cs          # 图标按钮按下缩放（合成器 Visual.Scale）
  Animations/Theme_3state.cs  # LottieGen 生成的三态动画源（勿手改）
  Assets/lottie/theme-3state.json  # 动画素材（由 tools/gen-theme-lottie.mjs 生成）
tools/
  gen-theme-lottie.mjs        # 生成三态 Lottie（含不变量自检），改动画就改这里
  ui-smoke-test.ps1           # UIA 端到端冒烟测试（44 项）
  measure-icon-ink.ps1        # 测量图标墨迹尺寸 / 笔画宽度（深浅主题自适应）
  inspect-button-style.ps1    # 采样按钮边框与底色像素，核对样式是否被冲掉
  test-icon-press.ps1         # 注入真实鼠标输入验证按下反馈
```

整个应用 17 个 `.cs` 文件，**0 个 `.xaml`**。

## 4. 常用命令

```powershell
# 构建（必须指定平台）
dotnet build src\TodoApp\TodoApp.csproj -p:Platform=x64

# 运行（打包形态）
dotnet run --project .\src\TodoApp\TodoApp.csproj -p:Platform=x64

# 直接跑 exe（自包含运行时）
.\src\TodoApp\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TodoApp.exe

# 端到端冒烟测试（会先备份再还原你的 todos.json / settings.json）
pwsh -File tools\ui-smoke-test.ps1

# 重新生成主题图标动画（改完 tools/gen-theme-lottie.mjs 后）
node tools\gen-theme-lottie.mjs
.\.tools\lottiegen.exe -InputFile src\TodoApp\Assets\lottie\theme-3state.json `
    -Language cs -Namespace ReactorTodo.Animations -Public `
    -WinUIVersion 3.0 -GenerateColorBindings -OutputFolder src\TodoApp\Animations
```

**验证强度按改动类型决定**（重要）：

| 改动类型 | 必须做 |
|---|---|
| 改 UI / 动效 / 布局 | 构建 0 警告 + 启动看效果 |
| 新增或修改功能 | 构建 0 警告 + `tools/ui-smoke-test.ps1` 全绿 |
| 只改文档 / 注释 | 不需要跑测试 |

## 5. 编码规范

- **纯 Reactor 声明式**：不引入 XAML、不用 code-behind、不用 ViewModel；状态用 `UseState` / `UseReducer`。
- **状态不可变**：`TodoState` 是 `record`，变更通过 `with` 产生新实例；列表状态必须 `UseReducer`
  （`UseState` + `List<T>.Add()` 不会触发重渲染）。
- **配色只走 `Theme.*` 语义 token**（`Theme.CardBackground` / `Theme.SecondaryText` / `Theme.SystemCritical` …），
  不写死颜色字面量，保证明暗主题与高对比度都正确。
- **文案只走 `StringKeys` + `intl.T(...)`**，不写中文字面量；新增文案要同时补 3 种语言。
- **控件要有 `AutomationId` + `AutomationName`**（分析器 `REACTOR_A11Y_003/004` 会强制），`AutomationId` 用英文且不随语言变化。
- **构建必须 0 警告**：Reactor 自带分析器（`REACTOR_HOOKS_011`、`REACTOR_A11Y_003`、`REACTOR_MOD_003` …）报的都要修，不要 Suppress。
- 代码注释用中文解释**为什么**（尤其是绕坑的地方），不要复述代码在做什么。

### React → Reactor 心智模型

| React | 本项目中的写法 |
|---|---|
| `function App()` | `class App : Component { override Element Render() }` |
| `useState` | `var (draft, setDraft) = UseState("")` |
| `useReducer` | `UseReducer<TodoState, TodoAction>(TodoState.Reduce, TodoState.Initial)` |
| `useEffect` | `UseEffect(() => TodoStore.Save(state.Items), state.Items)` |
| `useMemo` | `UseMemo(() => new TodoCommands(dispatch), dispatch)` |
| `key={id}` | `Component<TodoRow, TodoRowProps>(props).WithKey(item.Id)` |
| `children` | 工厂方法可变参数，`null` 子元素自动过滤 |
| `className` / `style` | `.Padding()` `.Background(...)` `.Flex(grow: 1, basis: 0)` 等流式修饰符 |

## 6. 设计决策

### 6.1 按钮语义

WinUI 内置样式与 Reactor 修饰符的对应：

| WinUI 样式 | Reactor | 观感 | 用在哪 |
|---|---|---|---|
| `DefaultButtonStyle` | 默认 | 中性浅底 + 边框 | 对话框「取消」 |
| `AccentButtonStyle` | `.AccentButton()` | 实心强调色 | 「添加」「保存」 |
| `SubtleButtonStyle` | `.SubtleButton()` | 无边框，悬停才有底色 | 「编辑」「取消」 |
| — | `.TextLink()` | 文字链接 | 未使用 |

WinUI **没有**危险/破坏性按钮样式，`Theme.Error` 这类 token 也不存在，所以在 `Components/Buttons.cs` 自己拼：

| 语义 | 方法 | 实现 |
|---|---|---|
| 主操作 | `Buttons.Primary` | `.AccentButton()` |
| 普通操作 | `Buttons.Normal` | 默认样式 |
| 轻量操作 | `Buttons.Subtle` | `.SubtleButton()` |
| 危险（可重复） | `Buttons.Danger` | `.SubtleButton()` + `Foreground(Theme.SystemCritical)` + 悬停淡红底 |
| 危险（终态） | `Buttons.DangerOutlined` | 默认按钮形状 + 红字 + 红描边 + 悬停淡红底 |

换色用 **lightweight styling**（覆盖 `ButtonBackground*` / `ButtonForeground*` / `ButtonBorderBrush*` 等资源键），
不自定义 `Style`、不写 XAML。

实测：默认 / 强调 / 轻量三种样式的几何**完全一致**——圆角 `4`、内边距 `11,5,11,6`、高度 `32`。
所以「行内删除是红字无底色、对话框删除是红字 + 红描边」只靠颜色与描边表达强度差异，共用系统几何。

### 6.2 二次确认是状态，不是命令

```
点「删除」          → RequestDelete(id)  → PendingDeleteId = id   → 对话框 IsOpen
点「取消」/Esc      → CancelDelete      → PendingDeleteId = null → 关闭，列表不变
点对话框「删除」     → ConfirmDelete     → 真正移除
```

- `TodoState` 里放 `PendingDeleteId` / `PendingClearCompleted`，渲染与开关全走 reducer，没有命令式 `ShowAsync()`。
- 每条关闭路径（Esc、标题栏关闭、按钮关闭）都由 `ContentDialogElement.OnClosed` 统一派发 cancel，cancel 幂等。
- WinUI 的 `primaryButtonText` 只能传文本、样式固定，所以把自绘按钮放进对话框 `Content`，
  `primaryButtonText` 传**空串**隐藏内置按钮。
- 无事可做时禁用按钮（`CompletedCount == 0` 时 `.IsEnabled(false)`），而不是弹空对话框。

### 6.3 多语言

- 用 Reactor 自带本地化栈：`LocaleProvider(locale, content, provider, defaultLocale)` 包住整屏，
  后代组件 `UseIntl()` 直接取文案，不层层传参。
- **`LocaleProvider` 不做回退**（未支持 locale 会拿到空串），回退逻辑写在 `AppStringProvider`：
  `精确 locale → 语言 → 默认 → key`。
- 文案表写在代码里；换 `.resw` 只需实现 `IStringResourceProvider`（Reactor 自带 `ReswResourceProvider`）。
- 窗口标题与无障碍名称也参与本地化，所以 UIA 测试要按**窗口句柄**找窗口，不能按标题。

### 6.4 主题

- `ThemeMode.System / Light / Dark` → `ElementTheme.Default / Light / Dark`，立即生效并写入 `settings.json`。
- **主题不能只切 `RequestedTheme`**：`.RequestedTheme()` 加在组件 `Render()` 返回的根元素上不生效（实测控件仍是
  `Default`），要用 `.Set(panel => panel.RequestedTheme = …)` 直接写属性。
- **`Theme.*` 画刷在元素创建时解析**，所以主题变化要让子树重建——`App` 把屏幕按
  `.WithKey($"theme-{theme}-{resolvedTheme}")` 重新挂载。
- **背景必须自铺**：`BackdropKind.Mica` 只跟**系统**主题走，应用内切深色会出现「白底白字」。
  现在**始终**铺 `Theme.SolidBackground`（深色 `#FF202020`、浅色 `#FFF3F3F3`）。
- **「跟随系统」= 跟随深浅色**：`UISettings.GetColorValue(UIColorType.Background)` 亮度判定 +
  订阅 `ColorValuesChanged`（回调在后台线程，需 `DispatcherQueue.TryEnqueue`）。不取系统强调色。
- 标题栏的最小化/最大化/关闭由系统绘制、只跟应用主题，所以按解析后的主题显式设置 `AppWindow.TitleBar` 的按钮色，
  避免「深色内容 + 系统绿色按钮」。

### 6.5 主题图标动效（Lottie）

一个素材覆盖三态：`Assets/lottie/theme-3state.json` → LottieGen → `Animations/Theme_3state.cs` →
`AnimatedVisualPlayer`。完整决策与实测数据见 [`docs/motion-spec.md`](docs/motion-spec.md)。

关键约定：

- **时间线三段顺序正向**（fr 60、帧 0–45、每段 250 ms）：`0→1/3` 系统→日间、`1/3→2/3` 日间→夜间、
  `2/3→1` 夜间→系统。**`PlayAsync(from, to)` 不能倒放**：传 `1→0` 会正向播完停在进度 1。
- **轨迹是「左升右落」**：新图标从左下 `(12,84)` 沿弧线经 `(30,62)` 升到中心 `(50,50)`；
  旧图标从中心经 `(70,62)` 落到右下 `(88,84)`。出入错峰 9 帧，两者不重叠。
- **缓动统一** `cubic-bezier(0.2, 0, 0, 1)`（Corporate 人格），不加环境动效（20px 控件加了只是噪音）。
- **颜色**：素材单色 → 生成时 `-GenerateColorBindings` 暴露 `Color_000000`，运行时按主题改
  （深色 `#FFFFFFFF`、浅色 `#E5000000`）。**必须在赋 `player.Source` 之前设置**。
- 改动画只改 `tools/gen-theme-lottie.mjs`（含不变量自检），然后跑第 4 节的生成命令，**不要手改** 生成的 `.cs`。

### 6.6 图标视觉对齐

字体图标（16px 字形）与矢量图标（Lottie）在同一行，必须做**视觉尺寸与笔画对齐**：

- 统一 `20×20` 图标盒（`IconHost`），两个按钮因此同尺寸、墨迹同偏移 `(14,8)`。
- Lottie 画布内图形放大 `ART_SCALE = 145`（原图形只占画布 ~52%，20px 播放器只渲染出 11px 墨迹）。
- 描边 `STROKE = 4.2` 单位：7 单位时笔画中位 2px（比字形粗一倍），4.2 单位后中位 2px / 均值 2.73，
  与字形（2px / 2.42）一致。
- 校验工具：`tools/measure-icon-ink.ps1`（按按钮自身背景色判定墨迹，深浅主题自适应）。

### 6.7 按下反馈

只有**语言按钮**有按下缩放（主题按钮本身已有过渡动画）。按下图标缩到 `0.8`、松开回 `1.0`
（按 90 ms / 松 160 ms）。实现见 `Components/PressFeedback.cs`，三个必须知道的点：

1. `ButtonElement.Set(...)` 在内容赋值**之前**执行，此时 `Content` 为 `null` → 必须等 `Loaded` 再接线。
2. `ButtonBase` 会把 `PointerPressed` 标记为已处理 → 必须
   `AddHandler(UIElement.PointerPressedEvent, handler, handledEventsToo: true)`。
3. 用 `Set(...)` 而不是 `OnMount(...)`：`OnMount` 会冲掉按钮的 subtle 样式（链条上游 `WithFlyout` 已把静态类型
   退化为 `Element`，`with` 表达式切掉派生记录字段）。
4. 缩放作用于 `Button.Content` 的 `Visual.Scale`（合成器，不重新栅格化字形），`CenterPoint` 取图标中心。

## 7. 踩坑记录（Reactor 0.1.0-preview.16 + .NET 10 实测）

### 布局

1. **FlexPanel 不能嵌套 FlexPanel**：`FlexRow` 里再放 `FlexRow` 会让内层塌陷成 0 尺寸，外层兄弟元素跟着不参与布局。
   按钮组改用 `HStack`（StackPanel）。
2. **`.Padding()` 对 Flex 容器无效**（用 `.FlexPadding()`），分析器给 `REACTOR_MOD_003`。
3. **在根组件外包一层布局容器会切断高度链**：包 `FlexColumn(content)` 后 `Flex(grow: 1)` 全部失效
   （列表只拿到 150px）。要刷新主题用 `.WithKey(...)` 重挂载，别包容器。
4. **行内文本垂直居中不能只靠 flex 拉伸**：`.Flex(grow: 1)` 会把文本框拉满行高、文字顶端对齐；
   还要 `.Flex(..., alignSelf: FlexAlign.Center)`。
5. **WinUI `CheckBox` 自带 120px `MinWidth`**：加 `.MinWidth(0)` 收掉多余空白。

### 事件与状态

6. **`UseState` + `List<T>.Add()` 不重渲染**，列表状态用 `UseReducer`。
7. **按钮点击是构造参数** `Button("添加", onClick)`，没有 `.OnClick()`。
8. **`CheckBox` 回调参数名是 `onIsCheckedChanged`**（文档散文里写的 `onChanged` 是错的）。
9. **`.WithTransitions(...)` 在本版本不生效**（实测 `Transitions.Count == 0`），
   要进入动画用 `.OnMount(...)` 自己播 Storyboard。
10. **给单个子元素换 `.WithKey(...)` 不会重建控件**（UIA RuntimeId 不变）；要重挂载，key 加在**组件**元素上。

### 样式

11. **同名资源键只对同一套样式生效**：`AccentButtonStyle` 读 `AccentButtonBackground*`，覆盖通用
    `ButtonBackground*` 无效。改色前先确认目标样式模板读哪组键。
12. **`.Foreground()` 会把按钮圆角清零**（`4,4,4,4` → `0,0,0,0`，样式被切成显式 `Button`）。
    显式补 `.CornerRadius(ThemeResource.CornerRadius("ControlCornerRadius"))`。
13. **`ContentDialog` 的 `primaryButtonText` 传空串才隐藏内置主按钮**，自绘按钮放进 `Content`。
14. **`OnMount` 会冲掉按钮样式**（见 6.7 第 3 点），配置控件优先用 `.Set(...)`。

### 主题

15. **`.RequestedTheme()` 放在组件返回的根元素上不生效**，要 `.Set(panel => panel.RequestedTheme = …)`。
16. **`Theme.*` 画刷在元素创建时解析**，改主题要重建子树（按主题 key 重挂载）。
17. **`BackdropKind.Mica` 只跟系统主题**，应用内切深色会出现白底白字；自铺 `Theme.SolidBackground`。
18. **`ComboBox` 会回调 `SelectedIndexChanged(-1)`**，直接 `(ThemeMode)index` 会写坏设置文件
    （`"theme": "-1"`）→ 过滤 `index >= 0`，读取时 `Enum.IsDefined` 兜底。

### 本地化

19. **`LocaleProvider` 不做语言回退**，回退逻辑要写在 `IStringResourceProvider` 里。
20. **主题变化会重挂载界面，缓存的 `AutomationElement` 会失效**：自动化测试每次点击前重新查找控件。
21. **`RadioMenuFlyoutItem` 在 UIA 里是 Toggle 模式**（不是 SelectionItem/Invoke），用 `TogglePattern`。

### 字体与图标

22. **图标字体要挑系统真有的**：`Segoe Fluent Icons` 只在 Win11，Win10 会渲染成空白；用 `Segoe MDL2 Assets`。
23. **缩放字形会发糊**：`ScaleTransform` 让字形按分数比例重新栅格化；图标动效用矢量素材或合成器 `Visual.Scale`。

### Lottie

24. **`Microsoft.Toolkit.Uwp.UI.Lottie`（Lottie-Windows 7.x）只面向 UAP**，WinUI 3 用不了；
    正确路径是 **LottieGen 生成源码**再编进项目。
25. **LottieGen 必须显式 `-WinUIVersion 3.0`**：默认生成 `Windows.UI.Composition` 版本，编译报 `CS0738`。
26. **生成代码还需要 Win2D ≥ 1.4.0**（`Microsoft.Graphics.Canvas.Geometry`）。
27. **同一组里放多个形状不被支持**：LottieGen 报 `LT0027` 并丢形状 → 一个形状一个组。
28. **Lottie 颜色是烘焙进素材的**：用 `-GenerateColorBindings` + `SetColorProperty`，且在赋 `player.Source` 之前设置。
29. **`PlayAsync(from, to)` 只能正向**：`1 → 0` 会正向播完停在 1；所有过渡都要排成正向时间线。
30. **`Visual.Scale` 在 WinUI 3 是 `Vector3`**：用 `Vector2` 关键帧抛
    `ArgumentException: expression output does not match animating property type`。

## 8. 验证方法与实测数据

WinUI 没有无头宿主，所有验证都跑真窗口。

| 手段 | 用途 | 工具 |
|---|---|---|
| 构建 | 0 错误；Debug 0 警告（Release 有 2 条来自 Windows SDK 的 `IL2104` 裁剪告警，与项目无关） | `dotnet build -p:Platform=x64` |
| UIA 端到端 | 44 项检查：CRUD、过滤器、二次确认三条路径、重启恢复、点标题切完成、键盘 Enter、语言切换、主题切换与持久化、列表占满中部且页脚贴底 | `tools/ui-smoke-test.ps1` |
| 控件属性读取 | 主题、画刷、圆角、内边距、`AnimatedVisualPlayer` 加载状态 | `.Set(c => …)` / `.OnMount(e => …)` 里读属性 |
| 像素采样 | 按钮配色、圆角起始位置、图标墨迹与笔画宽度 | `tools/measure-icon-ink.ps1`、`tools/inspect-button-style.ps1` |
| 真实输入 | 按下缩放、hover 态（`Cursor.Position` 不触发指针事件，要 `mouse_event`） | `tools/test-icon-press.ps1` |

代表性实测数据：

- 主题：同一张卡浅色 `#B3FFFFFF` / 深色 `#0DFFFFFF`；深色根背景 `#FF202020` + 文字 `#FFFFFFFF`。
- 布局（UIA 几何）：列表视口 458px 并占满中部，汇总/清除距窗口底边 32px；20 条数据时 11 条被裁在滚动区。
- 动画：`IsAnimatedVisualLoaded=True`、`Duration=517ms`，三段过渡均 `error=none`。
- 图标：两按钮均 `44×33`、墨迹 `16×16`、偏移 `(14,8)`；笔画中位 2px（均值 2.73 vs 字形 2.42）。
- 按下反馈：真实鼠标注入 → `handler pressed` / `handler released`，图标 `scale=<0.8, 0.8, 1>` → `<1, 1, 1>`。

注意事项：键盘输入需要窗口拿到前台焦点，抢不到时冒烟测试输出 `[SKIP]` 而不是伪装通过。

## 9. Git 提交规范

采用 [Conventional Commits](https://www.conventionalcommits.org/)，**中文正文、英文 type/scope**。

### 格式

```
<type>(<scope>): <subject>

<body：为什么这么改、影响了什么>

<footer：关联 issue，可选>
```

### type

| type | 用途 |
|---|---|
| `feat` | 新功能（用户可见的行为变化） |
| `fix` | 修 bug |
| `docs` | 只改文档（README、AGENTS.md、注释） |
| `refactor` | 重构，不改变外部行为 |
| `style` | 格式、空白、命名（不含逻辑变化） |
| `test` | 测试与验证脚本 |
| `build` | 构建系统、依赖、项目文件 |
| `chore` | 杂项（.gitignore、工具配置） |
| `perf` | 性能优化 |
| `revert` | 回滚 |

### scope

`theme`、`i18n`、`ui`、`state`、`storage`、`lottie`、`icons`、`motion`、`build`、`tools`、`docs`、`agents`。

### 规则

- **subject 用中文、不超过 50 字、句末不加句号**；动词开头，说清「做了什么」。
- **一个提交只做一件事**：功能改动与文档改动分开；重构与行为修改分开。
- **body 说明为什么**，尤其是绕坑的实现（例如「`PlayAsync` 不能倒放，所以时间线排成三段正向」）。
- **不提交** `bin/`、`obj/`、`.tools/`、`.ink/`、本地 `settings.json` / `todos.json`。
- 提交前确认 `dotnet build -p:Platform=x64` 通过；**功能改动**还要 `ui-smoke-test.ps1` 全绿。
- 提交人邮箱必须是 `43244500+qianshipoi@users.noreply.github.com`（仓库本地配置已设置）。

### 示例

```
feat(theme): 三态主题切换与 Lottie 图标动画

- 主题按钮按 跟随系统 → 日间 → 夜间 循环，图标由 LottieGen 生成的矢量动画驱动
- PlayAsync 不能倒放，所以时间线排成三段正向：0→1/3→2/3→1
- 动画颜色按主题改写（-GenerateColorBindings），深色下为白色
```

```
fix(motion): 夜间切回跟随系统后图标停在月亮

PlayAsync(1, 0) 不会倒放，而是正向播完停在进度 1。
改为把循环拆成三段正向腿，夜间→系统走 2/3→1，落到第 45 帧（显示器）。
```

## 10. 给 AI 的协作约定

- **先读本文件再动手**；改主题/动效前先看 `docs/motion-spec.md`。
- 排查 Reactor API 时优先查包内文档 `~/.nuget/packages/microsoft.ui.reactor/0.1.0-preview.16/agentkit/`
  （`reactor.api.txt` 是全量 API 索引，比翻网页快且准）。
- **不要手改** `src/TodoApp/Animations/*.cs`（LottieGen 产物）；改动画改生成脚本。
- **不要引入 XAML**、不要用 code-behind、不要加 ViewModel —— 破坏项目定位。
- 改完必须能 `dotnet build` 0 警告；功能改动跑冒烟测试。
- 结论要有数据：涉及观感/尺寸/颜色的判断，用上面第 8 节的工具量出来再下结论，别凭感觉。
