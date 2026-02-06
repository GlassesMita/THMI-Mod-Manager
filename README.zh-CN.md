# <ruby title="你知道的太多了">东<rt>T</rt>方<rt>H</rt>夜雀<rt>M</rt>食堂<rt>I</rt></ruby> Mod 管理器

![License](https://img.shields.io/badge/license-GPLv3-red.svg?style=flat-square)
![Language](https://img.shields.io/badge/Language-C%23-cf6fff?style=flat-square)
![Framework](https://img.shields.io/badge/Framework-ASP.NET-9fa0db?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-00b600?style=flat-square)
![Editor](https://img.shields.io/badge/Editor-Trae-1af000?style=flat-square)

**本项目使用 GNU 通用公共许可证第三版**

专为《东方夜雀食堂》游戏打造的 Mod 管理器。

[DeepWiki 文档 (仅英文)](https://deepwiki.com/GlassesMita/THMI-Mod-Manager/)

## 功能特性

> [!NOTE]
> 此为早期开发版本，功能可能随时调整，使用需谨慎。

- 基于 ASP.NET Core 的网页界面
- 🎮 **一键启动** - 游戏进程监控，随开随关
- 📦 **Mod 管理**（开发中） - 一键安装卸载 Mod
- 🔧 **兼容性检测**（开发中） - 检查 Mod 兼容性与冲突
- 🌐 **多语言支持** - 本地化系统，支持多种语言包
- 支持 **.ini** 和 **.toml** 两种本地化文件格式
- 自动识别文件格式并解析

## 系统要求

- .NET 10.0 SDK
- Windows 10 x64 22H2 及以上版本（Windows 10 以下可能兼容性不佳）
- 正版游戏《东方夜雀食堂》本体
- 支持现代网页标准的浏览器（Chrome、Edge、Firefox 等）
- 稳定的网络连接用于下载Mod（可选）*（注：<ruby>部分学校<rt><small><del>https://www.gfxy.com</del></small></rt></ruby>的校园网可能阻断 Github，可尝试使用代理下载）*

## 源码编译

### 准备工作：

1. 安装.NET 10.0 SDK

- 手动下载：[Microsoft .NET Download Page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- 使用 WinGet（Windows 包管理器）命令

```bash
winget install --id Microsoft.DotNet.SDK.10 --source winget
```

- *注意：使用 WinGet 安装需管理员权限。如果没有管理员权限，会在安装过程中请求一次。若已开启 Windows sudo 功能，可在普通权限下执行，但是会在刚开始运行命令的时候请求一次权限。这个功能在使用 WinGet 一次性安装多个应用的时候非常方便：*

```bash
sudo winget install --id Microsoft.DotNet.SDK.10 --source winget
```

2. 安装 Git

- 手动下载：[Git 官网](https://git-scm.com/)
- 使用 WinGet 命令

```bash
winget install --id Git.Git --source winget
```

- *同上，需管理员权限或 sudo 功能*

```bash
sudo winget install --id Git.Git --source winget
```

3. 配置开发环境

- （Visual Studio 2022 用户）安装 VS2022 并勾选 ".NET 桌面开发"、"Node.js 开发"（可选）和 "ASP.NET 和 Web 开发" 工作负载
- （VS Code 用户）安装 VS Code 并添加 "C# Dev Kit"、"C#"、"IntelliCode for C#" 和 ".NET Install Tool" 扩展
- （Trae 用户）安装 Trae 后打开设置 → "常规" 标签页 → 偏好设置 → "去设置"，搜索 "Market"，在 "应用扩展市场地址" 填入 `https://marketplace.visualstudio.com/`，重启 Trae。随后安装 "C# Dev Kit"、"C#"、"IntelliCode for C#" 和 ".NET Install Tool" 扩展。

### 编译步骤：

1. 克隆仓库：

命令提示符：

```bash
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
cd './THMI-Mod-Manager/THMI Mod Manager'
```

PowerShell：

```bash
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
Set-Location -Path './THMI-Mod-Manager/THMI Mod Manager'
```

2. 打开 `THMI Mod Manager.csproj`，将 `<OutDir>` 属性修改为你的正版《东方夜雀食堂》游戏安装目录。

3. 使用 DotNet CLI 编译：

- 使用此方法，程序依赖于 .NET 10 SDK 或 Runtime 运行。

命令提示符或 PowerShell：

```bash
dotnet build --configuration Release --no-incremental
```

- 使用此方法，将程序构建到游戏目录（即 `<OutDir>` 中配置的路径，确保路径为游戏安装目录，否则会报错。）

```bash
dotnet build --configuration Release --no-incremental -p:BuildToGameDir=true
```

- 使用此方法，程序将被最大程度优化代码。

命令提示符或 PowerShell：

```bash
dotnet publish --configuration Release
```

*注意：如果需要编译到其他目录，可使用 `--output <path>` 指定输出目录。如果使用 `-p:SelfContained=true` 参数，输出目录将包含所有依赖项，无需额外安装 .NET 运行时，但这会导致编译后的文件（夹）大小增加。*

编译过程会自动将本地化文件、网页资源和配置文件复制到输出目录。

## 使用说明

### 游戏启动功能

Mod 管理器现已集成游戏启动器，功能如下：

- **启动游戏**：点击"启动"按钮，通过 Steam URL 协议启动游戏
- **进程监控**：自动检测游戏运行状态
- **关闭游戏**：需要时可安全终止游戏进程
- **状态显示**：实时显示游戏进程状态（运行中/已停止）

启动器使用 Steam URL 协议（`steam://rungameid/1584090`）启动游戏，确保与 Steam 完美集成。
