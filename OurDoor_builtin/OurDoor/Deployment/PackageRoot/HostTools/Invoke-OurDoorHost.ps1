<#
实现功能：以普通用户权限调用已注册的高权限启动/停止任务，等待明确结果，并在服务端可用后按需启动游戏。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Start', 'Stop')]
    [string]$Action,
    [switch]$LaunchGame,
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'OurDoorHost.psm1') -Force

try {
    $PackageRoot = [IO.Path]::GetFullPath($PackageRoot).TrimEnd('\')
    $settings = Read-OurDoorSettings
    $installedRoot =
        [IO.Path]::GetFullPath([string]$settings.PackageRoot).TrimEnd('\')
    if (-not [string]::Equals(
            $PackageRoot,
            $installedRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw (
            '[M8 主机入口] 当前完整包路径与已安装路径不一致。' +
            "installed=$installedRoot, current=$PackageRoot。" +
            '移动完整包后必须重新运行“安装局域网主机.cmd”。'
        )
    }
    if ($LaunchGame -and $Action -ne 'Start') {
        throw '[M8 主机入口] 只有启动服务端时可以同时启动游戏。'
    }

    $taskName = Get-OurDoorTaskName -Action $Action
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction Stop
    if ($task.Principal.RunLevel -ne 'Highest') {
        throw "[M8 主机入口] 计划任务没有最高权限：$taskName"
    }

    $resultPath = Get-OurDoorResultPath -Action $Action
    $previousWriteTime = [DateTime]::MinValue
    if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
        $previousWriteTime =
            (Get-Item -LiteralPath $resultPath).LastWriteTimeUtc
    }
    $requestTime = [DateTime]::UtcNow
    Start-ScheduledTask -TaskName $taskName

    $deadline = $requestTime.AddSeconds(90)
    $result = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 200
        if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
            continue
        }
        $file = Get-Item -LiteralPath $resultPath
        if ($file.LastWriteTimeUtc -le $previousWriteTime) {
            continue
        }
        $candidate = Get-Content `
            -LiteralPath $resultPath `
            -Raw `
            -Encoding UTF8 | ConvertFrom-Json
        if ($candidate.action -ne $Action) {
            continue
        }
        $result = $candidate
        break
    }
    if ($null -eq $result) {
        throw (
            "[M8 主机入口] 等待高权限任务结果超时，task=$taskName, " +
            "result=$resultPath。"
        )
    }
    if (-not [bool]$result.success) {
        throw "[M8 主机入口] $Action 失败：$($result.message)"
    }

    Write-Host "[M8 主机入口] $($result.message)" -ForegroundColor Green
    if ($LaunchGame) {
        $gamePath = Join-Path $PackageRoot 'OurDoor.exe'
        if (-not (Test-Path -LiteralPath $gamePath -PathType Leaf)) {
            throw "[M8 主机入口] 服务端已启动，但缺少游戏：$gamePath"
        }
        Start-Process `
            -FilePath $gamePath `
            -WorkingDirectory $PackageRoot
    }
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
