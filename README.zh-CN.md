# <ruby title="你知道的太多了">东<rt>T</rt>方<rt>H</rt>夜雀<rt>M</rt>食堂<rt>I</rt></ruby> Mod 管理器

**本项目使用 GNU 通用公共许可证第三版或更高版本**

专为《东方夜雀食堂》游戏打造的 Mod 管理器。

## 功能特性

> [!NOTE]
> 此为早期开发版本，功能可能随时调整，使用需谨慎。

- 基于 ASP.NET Core 的网页界面
- 🎮 **一键启动** - 游戏进程监控，随开随关
- 📦 **Mod管理**（开发中） - 一键安装卸载Mod
- 🔧 **兼容性检测**（开发中） - 检查Mod兼容性与冲突
- 🌐 **多语言支持** - 本地化系统，支持多种语言包
- 支持 **.ini** 和 **.toml** 两种本地化文件格式
- 自动识别文件格式并解析

## 系统要求

- .NET 10.0 SDK
- Windows 10及以上版本（Win10以下可能兼容性不佳）
- 正版游戏《东方夜雀食堂》本体
- 支持现代网页标准的浏览器（Chrome、Edge、Firefox 等）
- 稳定的网络连接用于下载Mod（可选）*（注：<ruby>部分学校<rt><small>https://www.gfxy.com</small></rt></ruby>的校园网可能阻断 Github，可尝试使用代理下载）*

## 源码编译

### 准备工作：

1. 安装.NET 10.0 SDK
 - 手动下载：[微软.NET下载页面](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
 - 使用 WinGet 命令（Windows 包管理器）
```bash
winget install --id Microsoft.DotNet.SDK.10 --source winget
```
- *注意：使用 WinGet 安装需管理员权限。若已开启 Windows sudo 功能，可在普通权限下执行*
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
 - （Visual Studio 2022 用户）安装 VS2022 并勾选 ".NET 桌面开发"、"Node.js 开发"（可选）和 "ASP.NET 与网页开发" 工作负载
 - （VS Code 用户）安装 VS Code 并添加 "C# 开发工具包"、"C#"、"C# 开发工具包智能代码"、".NET 安装工具" 扩展
 - （Trae 用户）安装 Trae 后打开设置 → "常规" 标签页 → 偏好设置 → "前往设置"，搜索 "Market"，在 "应用扩展市场地址" 填入 `https://marketplace.visualstudio.com/`，重启 Trae。随后安装 "C# 开发工具包"、"C#"、"C# 开发工具包智能代码"、".NET 安装工具" 扩展。
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

命令提示符或PowerShell：
```bash
dotnet build --configuration Release --no-incremental
```

编译过程会自动将本地化文件、网页资源和配置文件复制到输出目录。

## 使用说明

### 游戏启动功能

Mod管理器现已集成游戏启动器，功能如下：

- **启动游戏**：点击"启动"按钮，通过Steam URL协议启动游戏
- **进程监控**：自动检测游戏运行状态
- **关闭游戏**：需要时可安全终止游戏进程
- **状态显示**：实时显示游戏进程状态（运行中/已停止）

启动器使用Steam URL协议（`steam://rungameid/1584090`）启动游戏，确保与Steam完美集成。