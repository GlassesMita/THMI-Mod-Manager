# THMI Mod Manager 权限问题故障排除指南

## 问题描述
Discord显示"由于系统权限问题无法显示叠加面板"，且程序无法修改游戏窗口标题。

## 根本原因
1. **权限不足**：当前程序以标准用户权限运行，无法修改其他进程的窗口属性
2. **UAC限制**：Windows用户帐户控制阻止跨进程窗口操作
3. **完整性级别**：目标游戏进程可能运行在更高的完整性级别

## 解决方案

### 方法1：使用管理员权限启动（推荐）

#### 选项A：使用PowerShell脚本（推荐）
```powershell
# 在PowerShell中运行（以管理员身份）
.\run-as-admin.ps1
```

#### 选项B：使用批处理文件
```cmd
# 双击运行或以管理员身份运行
run-as-admin.bat
```

#### 选项C：手动以管理员身份运行
1. 右键点击项目文件夹
2. 选择"以管理员身份运行"
3. 在UAC提示时点击"是"

### 方法2：使用API检查权限状态

程序现在提供了权限检查API：

```http
GET /api/launcher/permissions
```

返回示例：
```json
{
  "success": true,
  "isAdministrator": false,
  "permissionStatus": "当前用户: USERNAME\\mila\n权限级别: 标准用户\n建议: 以管理员身份运行程序以获得最佳兼容性",
  "currentUser": "mila",
  "processId": 12345,
  "processName": "dotnet",
  "workingDirectory": "C:\\Users\\Mila\\source\\repos\\THMI Mod Manager",
  "operatingSystem": "Microsoft Windows 10.0.19045",
  "osArchitecture": "X64",
  "processArchitecture": "X64"
}
```

### 方法3：请求权限提升

```http
POST /api/launcher/elevate
```

此API将尝试重新启动程序以管理员权限运行。

## 故障排除步骤

### 步骤1：验证当前权限
运行以下命令检查当前权限：
```powershell
whoami /groups | findstr S-1-16-12288
```
如果有输出，表示已具有管理员权限。

### 步骤2：检查UAC设置
1. 打开控制面板 → 用户帐户 → 用户帐户
2. 点击"更改用户帐户控制设置"
3. 确保未设置为"从不通知"（最低级别可能影响某些功能）

### 步骤3：验证游戏进程权限
程序现在会自动检查目标进程的权限：
```
找到游戏进程: TouhouMystiaIzakaya (PID: 12345)
无法修改目标游戏进程 - 权限不足
建议：以管理员身份重新运行此程序
```

### 步骤4：检查Windows Defender/杀毒软件
某些安全软件可能阻止跨进程操作：
1. 临时禁用实时保护
2. 将程序添加到白名单
3. 检查Windows Defender的"受控文件夹访问"设置

## 增强的日志信息

程序现在提供更详细的权限相关日志：

### 权限检查日志
```
当前程序没有管理员权限，可能导致窗口标题修改失败
建议以管理员身份运行此程序
Discord叠加面板错误也表明存在权限问题
权限状态: 当前用户: USERNAME\mila
权限级别: 标准用户
建议: 以管理员身份运行程序以获得最佳兼容性
```

### 详细的错误信息
```
SetWindowText 失败 (错误代码: 5): 访问被拒绝 (Access is denied) - 需要管理员权限
连续 5 次修改失败
可能的原因：
1. 程序没有管理员权限
2. 目标窗口属于更高权限的进程
3. 目标窗口被其他程序保护
4. 游戏使用了自定义窗口管理器
```

## 代码增强

### 新增功能
1. **权限检查API**：提供详细的权限状态信息
2. **权限提升API**：尝试重新启动为管理员权限
3. **进程权限验证**：检查是否可以修改目标进程
4. **详细的错误代码**：提供Windows API错误的具体描述

### 新增文件
- `app.manifest`：应用程序清单，要求管理员权限
- `PermissionHelper.cs`：权限检查和提升的帮助类
- `run-as-admin.ps1`：PowerShell管理员权限启动脚本
- `run-as-admin.bat`：批处理管理员权限启动脚本

## 验证成功

成功运行时，您应该看到：
```
程序以管理员权限运行，应该可以正常修改窗口标题
找到游戏进程: TouhouMystiaIzakaya (PID: 12345)
游戏窗口标题已修改: 'Touhou Mystia Izakaya' -> 'Modded Touhou Mystia Izakaya'
```

## 如果仍然无法工作

1. **检查游戏完整性**：在Steam中验证游戏文件
2. **关闭叠加软件**：Discord、GeForce Experience等
3. **检查游戏版本**：确保游戏版本与预期匹配
4. **查看事件日志**：检查Windows事件查看器中的应用程序日志
5. **联系支持**：提供完整的日志文件进行进一步分析