# 东方夜雀食堂 Mod 管理器

![License](https://img.shields.io/badge/license-GPLv3-red.svg?style=flat-square)
![Language](https://img.shields.io/badge/Language-C%23-cf6fff?style=flat-square)
![Framework](https://img.shields.io/badge/Framework-.NET%2010%20WPF-9fa0db?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-00b600?style=flat-square)

**本项目使用 GNU 通用公共许可证第三版**

> [!WARNING]
> **免责声明**：本软件按"原样"提供，不提供任何明示或暗示的保证，包括但不限于对适销性、特定用途适用性和非侵权性的保证。在任何情况下，作者或版权持有人均不对因合同、侵权或其他原因而产生的任何索赔、损害或其他责任负责。本项目与原始游戏开发商（Dichroic Purplion/二色幽紫蝶）没有任何关联，也未获得其认可。
>
> **免费发布**：本项目免费提供。如果你是付费获得本软件，请立即申请退款并给卖家差评。本软件是一个独立的第三方工具。
>
> 本 Mod 管理器仅限与正版《东方夜雀食堂》配合使用。使用本软件即表示你同意仅将其与正版游戏配合使用，并尊重知识产权。

专为《东方夜雀食堂》游戏打造的 Windows 原生桌面 Mod 管理器，基于 .NET 10 构建的 WPF 应用程序。

## 功能特性

> [!NOTE]
> 此为早期开发版本，功能可能随时调整，使用需谨慎。

- 🖥️ **原生 WPF 桌面界面** - 基于 .NET 10 与 WPF-UI 4.3.0（Fluent 风格），全本地运行，无浏览器、无 Web 服务
- 🎮 **游戏启动器** - 通过 Steam URL 协议（`steam://rungameid/1584090`）或外部程序启动游戏，实时进程监控与会话计时
- 📦 **Mod 管理** - 从 `BepInEx/plugins` 目录浏览、启用、禁用、删除与安装 Mod（支持 ZIP 一键安装）
- ⚠️ **冲突检测** - 基于 `Manifest.toml` 的 `UniqueID` / `IncompatibleWith` 字段，启用冲突 Mod 时提示并支持强制启用
- 🌐 **多语言支持** - 基于 INI 文件的本地化系统，附带各语言的 Home/About Markdown 资源
- 🎨 **主题切换** - 浅色 / 深色 / 跟随系统，主题色独立可自定义
- 📝 **日志查看** - 内置查看器展示 `Logs/Latest.Log`；未处理异常自动转储 KernelPanic 日志
- ⚙️ **BepInEx 配置** - 直接在界面中编辑 `BepInEx.cfg` 常用项
- 🔄 **更新检查** - 应用更新基于 GitHub API，Mod 更新基于在线版本比对
- 💾 **设置热重载** - 设置保存至 `AppConfig.Schale`（INI 格式）并立即生效

## 系统要求

- .NET 10.0 SDK（仅编译需要；运行时需 .NET 10 桌面运行时）
- Windows 10 x64 22H2 及以上版本（更旧系统可能兼容性不佳）
- 正版游戏《东方夜雀食堂》本体
- 稳定的网络连接用于下载 Mod（可选）

**如果您执意要在 Windows 10 x64 以下版本上运行，您必须在系统环境变量内添加 `DOTNET_EnableWriteXorExecute=0` 和 `DOTNET_GCName=clrgc.dll`，然后重新启动。详见[此 Issue](https://github.com/dotnet/runtime/issues/79469#issuecomment-1371202114)。**

## 源码编译

### 准备工作

1. 安装 .NET 10.0 SDK

- 手动下载：[Microsoft .NET Download Page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- 或使用 WinGet：

```bash
winget install --id Microsoft.DotNet.SDK.10 --source winget
```

> 需要管理员权限的 PowerShell，或使用 `sudo winget install ...`（若已开启 Windows sudo 功能）。

2. 安装 Git：[Git SCM](https://git-scm.com/)

```bash
winget install --id Git.Git --source winget
```

3. 配置开发环境

- **Visual Studio 2022**：勾选 ".NET 桌面开发" 工作负载
- **VS Code / Trae**：安装 "C# Dev Kit"、"C#"、"IntelliCode for C# Dev Kit" 与 ".NET Install Tool" 扩展

### 编译步骤

1. 克隆仓库并进入项目目录：

```bash
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
cd './THMI-Mod-Manager/THMI Mod Manager'
```

2. 编译应用：

```bash
dotnet build --configuration Release --no-incremental
```

构建到游戏目录（通过 `-p:GameDir="路径"` 指定游戏安装目录；或使用 `-p:BuildToGameDir=true` 使用 csproj 中配置的默认游戏目录）：

```bash
dotnet build --configuration Release --no-incremental -p:BuildToGameDir=true
```

3. 发布优化版本：

```bash
dotnet publish --configuration Release
```

从以下目录启动生成的 WPF 桌面程序：

```powershell
& ".\bin\Release\net10.0-windows\publish\THMI Mod Manager.exe"
```

*注意：可使用 `--output <path>` 指定输出目录。使用 `-p:SelfContained=true` 可包含 .NET 运行时（无需安装运行时，但输出体积更大）。编译过程会自动将本地化文件、主题资源和配置文件复制到输出目录。*

## 使用说明

### 主窗口

应用采用侧边栏导航：**主菜单**（首页 / 模组 / 探索）与 **管理**（设置 / 关于）。侧边栏底部显示 Steam 状态与快速启动按钮；内容区底部状态栏显示当前操作信息。

### 首页

显示三个指标卡片（游戏状态、游戏文件、本次计时）与启动器卡片（启动 / 停止）。若应用目录下未找到 `Touhou Mystia Izakaya.exe`，会提示部署到游戏目录或在设置中配置外部启动程序。

### 模组管理

- 工具栏：**检查更新**、**安装 ZIP**、**刷新** 与排序下拉框（名称 / 安装时间）
- 每个 Mod 卡片可直接**启用 / 禁用 / 删除**（游戏运行时禁用修改）
- 冲突检测读取 `Manifest.toml`；启用冲突 Mod 时提示并支持强制启用
- 点击 Mod 卡片可展开详情（ID、版本、不兼容列表）

### 探索

计划中的 Mod 浏览 / 下载站（开发中，占位页面）。

### 设置

- **语言与区域**：切换界面语言（即时生效）
- **外观**：浅色 / 深色 / 跟随系统主题
- **启动设置**：Steam 启动或外部程序（需指定可执行文件）
- **更新**：自动检查更新开关与检查频率（启动时 / 每周 / 每月）
- **通知**：启用或禁用更新与事件通知
- **窗口标题**：为游戏窗口标题添加 "Modded" 前缀
- **BepInEx 配置**：选择 `BepInEx.cfg` 后可直接编辑常用配置项

设置保存后立即生效（热重载），配置写入应用目录下的 `AppConfig.Schale`。

### 日志

内置日志查看器展示 `Logs/Latest.Log`；未处理异常自动转储 `KernelPanic_{yyyyMMdd_HHmmss}.log`。右键点击"关于"导航项可打开 Windows 系统关于对话框。
