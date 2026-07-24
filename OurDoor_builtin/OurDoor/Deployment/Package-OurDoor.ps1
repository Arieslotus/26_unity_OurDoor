<#
实现功能：把用户手动生成的 Unity Windows Build、客户端配置模板、主机管理工具和 Skynet Lua 服务端组装为统一完整包。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityBuildPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$deploymentRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $deploymentRoot
$templateRoot = Join-Path $deploymentRoot 'PackageRoot'
$serverSource = Join-Path $projectRoot 'Server\Skynet'
$UnityBuildPath = [IO.Path]::GetFullPath($UnityBuildPath)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

if (-not (Test-Path -LiteralPath $UnityBuildPath -PathType Container)) {
    throw "[M8 打包] Unity Build 目录不存在：$UnityBuildPath"
}
if (Test-Path -LiteralPath $OutputPath) {
    throw (
        "[M8 打包] 输出目录已经存在，拒绝覆盖：$OutputPath。" +
        '请指定一个新的空路径，或由你人工确认后删除旧包。'
    )
}
if (-not (Test-Path -LiteralPath $templateRoot -PathType Container)) {
    throw "[M8 打包] 缺少发布模板：$templateRoot"
}
if (-not (Test-Path -LiteralPath $serverSource -PathType Container)) {
    throw "[M8 打包] 缺少 Skynet 服务端源码：$serverSource"
}

$gameExe = Join-Path $UnityBuildPath 'OurDoor.exe'
$gameData = Join-Path $UnityBuildPath 'OurDoor_Data'
$unityPlayer = Join-Path $UnityBuildPath 'UnityPlayer.dll'
foreach ($required in @($gameExe, $gameData, $unityPlayer)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "[M8 打包] Unity Windows Build 缺少文件：$required"
    }
}

[IO.Directory]::CreateDirectory($OutputPath) | Out-Null
Get-ChildItem -LiteralPath $UnityBuildPath -Force |
    Copy-Item -Destination $OutputPath -Recurse -Force

Get-ChildItem -LiteralPath $templateRoot -Force |
    Copy-Item -Destination $OutputPath -Recurse -Force

# Windows cmd.exe 对仅含 LF 的批处理文件解析不稳定。发布时统一输出 CRLF，
# 并使用无 BOM UTF-8；入口文件自身仅包含 ASCII 命令和提示文本。
Get-ChildItem -LiteralPath $OutputPath -Filter '*.cmd' -File |
    ForEach-Object {
        $cmdContent = [IO.File]::ReadAllText($_.FullName)
        $cmdContent = $cmdContent.Replace("`r`n", "`n").Replace("`r", "`n")
        $cmdContent = $cmdContent.Replace("`n", "`r`n")
        [IO.File]::WriteAllText(
            $_.FullName,
            $cmdContent,
            (New-Object Text.UTF8Encoding($false))
        )
    }

$serverDestination = Join-Path $OutputPath 'HostTools\Server\Skynet'
[IO.Directory]::CreateDirectory(
    (Split-Path -Parent $serverDestination)
) | Out-Null
Copy-Item `
    -LiteralPath $serverSource `
    -Destination $serverDestination `
    -Recurse `
    -Force

$runScript = Join-Path $serverDestination 'run.sh'
$runContent = [IO.File]::ReadAllText($runScript)
$runContent = $runContent.Replace("`r`n", "`n").Replace("`r", "`n")
[IO.File]::WriteAllText(
    $runScript,
    $runContent,
    (New-Object Text.UTF8Encoding($false))
)

$logsDirectory = Join-Path $OutputPath 'Logs\Server'
[IO.Directory]::CreateDirectory($logsDirectory) | Out-Null

$manifestEntries = Get-ChildItem `
    -LiteralPath $OutputPath `
    -Recurse `
    -File |
    Sort-Object FullName |
    ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($OutputPath.Length).TrimStart('\')
            length = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
$manifest = [ordered]@{
    schemaVersion = 1
    createdAtUtc = [DateTime]::UtcNow.ToString('o')
    unityBuildSource = $UnityBuildPath
    serverSource = $serverSource
    files = @($manifestEntries)
} | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText(
    (Join-Path $OutputPath 'package-manifest.json'),
    $manifest,
    (New-Object Text.UTF8Encoding($false))
)

Write-Host "[M8 打包] 完整包生成成功：$OutputPath" -ForegroundColor Green
Write-Host '[M8 打包] 主机 A 必须先运行“安装局域网主机.cmd”，再把完整包复制给 B/C。'
