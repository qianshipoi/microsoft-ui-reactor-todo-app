# Reactor Todo

用 [microsoft/microsoft-ui-reactor](https://github.com/microsoft/microsoft-ui-reactor) 写的 WinUI 3 待办清单示例。

Reactor 是微软实验性的**声明式、组件化 C# UI 框架**——渲染出真正的 WinUI 控件，但界面用 C# 工厂方法 + Hooks
描述：**没有 XAML、没有 ViewModel、没有 code-behind**。这个项目把它的核心用法集中在一个可以直接运行的待办应用里。

A small WinUI 3 todo app built with Microsoft.UI.Reactor: declarative C# UI, zero XAML.

![运行截图](docs/screenshot.png)

## 功能

- **增删改查**：添加（按钮或 <kbd>Enter</kbd>）、行内重命名（<kbd>Enter</kbd> 保存 / <kbd>Esc</kbd> 取消）、单条删除
- **完成状态**：点复选框或**直接点标题文字**都能切换（已完成显示删除线 + 次级文字色）
- **二次确认**：删除与「清除已完成」都先弹确认框，红色危险按钮 + 后果说明，不会误删
- **过滤器**：全部 / 未完成 / 已完成，各自带实时计数；底部汇总「N 项未完成 · M 项已完成」
- **多语言**：简体中文 / English / 日本語 / 跟随系统，切换即时生效（含窗口标题与无障碍名称）
- **三态主题**：跟随系统 / 日间 / 夜间，点一下图标换一个模式；主题按钮用 **Lottie 动画**过渡
  （左升右落的日出/日落轨迹），图标颜色随明暗主题自适应
- **原生观感**：按钮按语义分色（主操作 / 普通 / 轻量 / 危险），全部用 WinUI 系统样式与主题 token
- **自动持久化**：待办与设置分别写入 `todos.json` / `settings.json`，重启后恢复

![删除前的二次确认](docs/screenshot-confirm.png)

## 环境要求

| 项 | 要求 |
|---|---|
| 系统 | Windows 10 1809（17763）及以上 |
| SDK | .NET 10 SDK（目标框架 `net10.0-windows10.0.26100.0`） |
| 包 | `Microsoft.UI.Reactor` **0.1.0-preview.16**（实验性预览版） |
| 其他 | 打包项目必须显式指定平台（`-p:Platform=x64`） |

## 快速开始

```powershell
# 构建并运行（打包形态）
dotnet run --project .\src\TodoApp\TodoApp.csproj -p:Platform=x64

# 或先构建，再直接双击 exe（已自带 Windows App SDK 运行时）
dotnet build .\src\TodoApp\TodoApp.csproj -p:Platform=x64
.\src\TodoApp\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TodoApp.exe
```

Visual Studio 打开 `ReactorTodo.slnx` 后，启动配置选「Reactor Todo (Package)」按 <kbd>F5</kbd> 即可。

## 项目结构

```
src/TodoApp/
  Program.cs / App.cs     # 入口与根组件（主题、语言、窗口 chrome）
  TodoScreen.cs           # 主界面：列表、页脚、设置按钮
  State/ Storage/         # 状态 reducer、JSON 持久化、系统主题
  Localization/           # 三种语言 + 回退链
  Components/             # 行、输入栏、过滤器、对话框、按钮语义、按下反馈
  Animations/ Assets/     # 主题图标的 Lottie 素材与生成代码
tools/                    # 动画生成脚本、UIA 冒烟测试、像素级校验工具
```

整个应用只有 17 个 `.cs` 文件，**没有一个 `.xaml`**。每个文件的职责见 [AGENTS.md](AGENTS.md)。

## 文档

| 文件 | 内容 |
|---|---|
| [AGENTS.md](AGENTS.md) | **工程说明**：目录职责、编码规范、设计决策、Reactor 踩坑记录、验证方法、**Git 提交规范** |
| [docs/motion-spec.md](docs/motion-spec.md) | 主题图标动效：时间线、缓动、参数依据、实测数据 |
| [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) | 第三方素材与工具许可 |

## 说明

- 本项目由官方模板 `dotnet new reactor` 脚手架改造而来，并开启 `WindowsAppSDKSelfContained=true`
  （双击 exe 即可运行，无需预装 Windows App Runtime）。
- Reactor 目前是实验性预览版，API 可能随版本变化。
- 官方仓库：<https://github.com/microsoft/microsoft-ui-reactor>
