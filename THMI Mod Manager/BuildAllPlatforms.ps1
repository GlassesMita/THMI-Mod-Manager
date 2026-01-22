<#
.SYNOPSIS
一键构建THMI Mod Manager全平台发布包（自动从csproj读取版本号）

.PARAMETER BuildPath
构建输出的基础路径（必填），例如：D:\Build

.PARAMETER ProjectPath
可选：csproj文件路径（默认读取当前目录下的THMI Mod Manager.csproj）

.EXAMPLE
.\BuildAllPlatforms.ps1 -BuildPath D:\Build
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$BuildPath,
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = ".\THMI Mod Manager.csproj"
)

# ===================== 核心：从csproj自动读取版本号 =====================
if (-not (Test-Path $ProjectPath)) {
    Write-Host "❌ 找不到csproj文件：$ProjectPath" -ForegroundColor Red
    exit 1
}

# 读取csproj并提取<Version>节点值
try {
    $csprojContent = Get-Content -Path $ProjectPath -Raw
    $versionMatch = [regex]::Match($csprojContent, '<Version>(.*?)</Version>')
    if (-not $versionMatch.Success) {
        throw "csproj文件中未找到<Version>节点"
    }
    $projectVersion = $versionMatch.Groups[1].Value
    Write-Host "✅ 从csproj读取到版本号：$projectVersion" -ForegroundColor Green
}
catch {
    Write-Host "❌ 读取版本号失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ===================== 固定配置（无需依赖csproj） =====================
$projectName = "THMI Mod Manager"          # 项目名称
$targetFramework = "net10.0"               # 目标框架
$targetRuntimes = @(
    "win-x86",      # Windows 32位
    "win-x64",      # Windows 64位
    "win-arm64",    # Windows ARM64
    "linux-x64",    # Linux 64位
    "linux-arm",    # Linux ARM 32位
    "linux-arm64",  # Linux ARM 64位
    "osx-x64",      # macOS Intel 64位
    "osx-arm64"     # macOS Apple Silicon
) # 目标平台
$excludeFiles = @(                         # 需要剔除的文件
    "appsettings.Development.json",
    "package.json",
    "web.config"
)

# ===================== 构建逻辑 =====================
# 标准化基础路径（避免路径格式问题）
$BuildPath = [System.IO.Path]::GetFullPath($BuildPath)
# 检查基础路径，不存在则创建
if (-not (Test-Path $BuildPath)) {
    New-Item -ItemType Directory -Path $BuildPath | Out-Null
    Write-Host "✅ 创建基础构建目录：$BuildPath" -ForegroundColor Green
}

# 遍历所有平台逐个构建
foreach ($runtime in $targetRuntimes) {
    Write-Host "`n=====================================" -ForegroundColor Cyan
    Write-Host "开始构建 $runtime 平台..." -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan

    # 正确拼接平台输出路径（核心修复：避免路径重复）
    $runtimeOutputPath = Join-Path -Path $BuildPath -ChildPath $runtime
    # 定义ZIP包名称（<项目名>_<架构>_<版本>.zip）
    # 将空格替换为半角句点，GitHub会自动转换
    $zipFileName = "$($projectName -replace ' ','.')_$($runtime)_$($projectVersion).zip"
    $zipFilePath = Join-Path -Path $BuildPath -ChildPath $zipFileName

    try {
        # 1. 清理旧构建文件（避免缓存干扰）
        dotnet clean -c Release --nologo | Out-Null
        
        # 2. 发布当前平台（直接使用 -o 参数指定完整输出路径）
        # 添加 -p:BuildAll=true 来禁用 csproj 中的 ZIP 打包目标
        $publishArgs = @(
            "publish",
            "-c", "Release",
            "-r", $runtime,
            "-f", $targetFramework,
            "--self-contained", "false",
            "-o", "`"$runtimeOutputPath`"",  # 直接指定完整输出路径
            "-p:BuildAll=true",  # 禁用 csproj 中的 ZIP 打包逻辑
            "--nologo"
        )
        # 执行发布命令
        $process = Start-Process -FilePath "dotnet" -ArgumentList $publishArgs -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            throw "发布 $runtime 平台失败，退出码：$($process.ExitCode)"
        }
        Write-Host "✅ $runtime 平台发布成功" -ForegroundColor Green

        # 3. 剔除不需要的文件
        foreach ($file in $excludeFiles) {
            $excludePath = Join-Path -Path $runtimeOutputPath -ChildPath $file
            Get-ChildItem -Path $excludePath -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse
        }
        Write-Host "✅ 已剔除无用文件（$($excludeFiles -join ', ')）" -ForegroundColor Green

        # 4. 生成ZIP包（覆盖已存在的包）
        if (Test-Path $zipFilePath) {
            Remove-Item -Path $zipFilePath -Force
        }
        Compress-Archive -Path "$runtimeOutputPath\*" -DestinationPath $zipFilePath -Force
        Write-Host "✅ 已生成ZIP包：$zipFilePath" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ 构建 $runtime 平台失败：$($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# 构建完成汇总
Write-Host "`n=====================================" -ForegroundColor Green
Write-Host "🎉 全平台构建完成！" -ForegroundColor Green
Write-Host "📌 版本号：$projectVersion" -ForegroundColor Green
Write-Host "📂 产物路径：$BuildPath" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green