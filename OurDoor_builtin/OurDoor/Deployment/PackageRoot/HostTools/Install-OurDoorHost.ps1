<#
实现功能：一次性验证 WSL2、Ubuntu、Skynet v1.8.0 和服务端文件，配置防火墙、端口转发、计划任务、客户端端点及快捷方式。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$modulePath = Join-Path $PSScriptRoot 'OurDoorHost.psm1'
if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
    throw "[M8 主机安装] 缺少管理模块：$modulePath"
}
Import-Module $modulePath -Force

if (-not (Test-OurDoorAdministrator)) {
    $argumentLine = (
        '-NoProfile -ExecutionPolicy Bypass -File "{0}" -PackageRoot "{1}"' -f
        $PSCommandPath.Replace('"', '""'),
        $PackageRoot.Replace('"', '""')
    )
    $process = Start-Process `
        -FilePath 'powershell.exe' `
        -ArgumentList $argumentLine `
        -Verb RunAs `
        -Wait `
        -PassThru
    exit $process.ExitCode
}

$expectedVersion = 'v1.8.0'
$port = 8888

try {
    Assert-OurDoorAdministrator
    Write-OurDoorLog `
        -PackageRoot $PackageRoot `
        -Message '[M8 安装] 开始安装局域网主机。'

    $gamePath = Join-Path $PackageRoot 'OurDoor.exe'
    $serverRoot = Join-Path $PackageRoot 'HostTools\Server\Skynet'
    $requiredServerFiles = @(
        (Join-Path $serverRoot 'config'),
        (Join-Path $serverRoot 'run.sh'),
        (Join-Path $serverRoot 'service\main.lua'),
        (Join-Path $serverRoot 'service\gate_service.lua')
    )
    if (-not (Test-Path -LiteralPath $gamePath -PathType Leaf)) {
        throw "[M8 主机安装] 完整包缺少 OurDoor.exe：$gamePath"
    }
    foreach ($requiredFile in $requiredServerFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "[M8 主机安装] 缺少服务端文件：$requiredFile"
        }
    }

    $distros = @(Get-OurDoorWslDistros)
    $ubuntuDistros = @($distros | Where-Object { $_ -like 'Ubuntu*' })
    if ($ubuntuDistros.Count -eq 0) {
        throw (
            '[M8 主机安装] 未找到 Ubuntu WSL 发行版。请先执行 ' +
            'wsl --install -d Ubuntu，完成首次启动后再运行本安装程序。'
        )
    }
    $distro = Select-OurDoorCandidate `
        -Candidates $ubuntuDistros `
        -Title '检测到多个 Ubuntu 发行版，请选择 OurDoor 使用的发行版：' `
        -Formatter { param($item) [string]$item }

    $kernel = Invoke-OurDoorWsl -Distro $distro -Command 'uname -r'
    if ($kernel.Output -notmatch '(?i)WSL2|microsoft-standard') {
        throw (
            "[M8 主机安装] 发行版不是可确认的 WSL2，" +
            "distro=$distro, kernel=$($kernel.Output)。" +
            "请执行 wsl --set-version `"$distro`" 2。"
        )
    }

    $homeResult = Invoke-OurDoorWsl `
        -Distro $distro `
        -Command 'printf %s "$HOME"'
    $defaultSkynetRoot = $homeResult.Output.TrimEnd('/') + '/skynet'
    $enteredRoot = Read-Host "请输入 Skynet v1.8.0 根目录 [$defaultSkynetRoot]"
    $skynetRoot = if ([string]::IsNullOrWhiteSpace($enteredRoot)) {
        $defaultSkynetRoot
    }
    else {
        $enteredRoot.TrimEnd('/')
    }

    $versionCheck = Test-OurDoorSkynetVersion `
        -Distro $distro `
        -SkynetRoot $skynetRoot `
        -ExpectedVersion $expectedVersion
    if (-not $versionCheck.Valid) {
        throw (
            "[M8 主机安装] 无法确认 Skynet $expectedVersion。" +
            "要求 $skynetRoot/skynet 可执行，且源码仓库当前 HEAD 必须精确位于 " +
            "$expectedVersion 标签；detected=$($versionCheck.DetectedVersion)。" +
            '本工具不会自动下载或接受未知版本。请手动取得官方 Skynet ' +
            "$expectedVersion 源码并在 Ubuntu 中完成 make linux。"
        )
    }

    $lanCandidates = @(Get-OurDoorLanCandidates)
    if ($lanCandidates.Count -eq 0) {
        throw '[M8 主机安装] 未检测到有效局域网 IPv4。请先连接 Wi-Fi 或以太网。'
    }
    $selectedLan = Select-OurDoorCandidate `
        -Candidates $lanCandidates `
        -Title '请选择其他玩家访问电脑 A 时使用的局域网地址：' `
        -Formatter {
            param($item)
            '{0} / {1} / {2}' -f
                $item.Address,
                $item.InterfaceAlias,
                $item.InterfaceDescription
        }

    $networkConfigPath = Join-Path $PackageRoot 'ourdoor-network.json'
    $networkConfig = [ordered]@{
        host = $selectedLan.Address
        port = $port
    } | ConvertTo-Json
    Write-OurDoorUtf8NoBom `
        -Path $networkConfigPath `
        -Content $networkConfig

    $existingFirewallRules = @(
        Get-NetFirewallRule `
            -DisplayName (Get-OurDoorFirewallRuleName) `
            -ErrorAction SilentlyContinue
    )
    if ($existingFirewallRules.Count -eq 0) {
        New-NetFirewallRule `
            -DisplayName (Get-OurDoorFirewallRuleName) `
            -Direction Inbound `
            -Action Allow `
            -Protocol TCP `
            -LocalPort $port `
            -Profile Private | Out-Null
    }
    elseif (-not (Test-OurDoorFirewallRule -Port $port)) {
        throw (
            '[M8 主机安装] 已存在同名但配置不正确的防火墙规则。' +
            '请人工检查后重试，工具不会静默覆盖。'
        )
    }
    Write-OurDoorLog `
        -PackageRoot $PackageRoot `
        -Message (
            '[M8 安装] 防火墙检查通过，rule={0}, protocol=TCP, port={1}, profile=Private。' -f
            (Get-OurDoorFirewallRuleName),
            $port
        )

    $wslIp = Get-OurDoorWslIp -Distro $distro
    $previousWslIp = $null
    if (Test-Path -LiteralPath (Get-OurDoorSettingsPath) -PathType Leaf) {
        $previousSettings = Read-OurDoorSettings
        $previousRoot =
            [IO.Path]::GetFullPath([string]$previousSettings.PackageRoot).TrimEnd('\')
        if ([string]::Equals(
                $previousRoot,
                $PackageRoot.TrimEnd('\'),
                [StringComparison]::OrdinalIgnoreCase)) {
            $previousWslIp = [string]$previousSettings.LastWslIp
        }
        else {
            throw (
                '[M8 主机安装] 当前电脑已经安装了另一个路径的 OurDoor 主机。' +
                "installed=$previousRoot, current=$PackageRoot。" +
                '请确认目标后再处理，工具不会静默改绑。'
            )
        }
    }
    Set-OurDoorPortProxy `
        -Port $port `
        -WslIp $wslIp `
        -PreviousWslIp $previousWslIp
    Write-OurDoorLog `
        -PackageRoot $PackageRoot `
        -Message (
            '[M8 安装] 端口转发检查通过，0.0.0.0:{0} -> {1}:{0}。' -f
            $port,
            $wslIp
        )

    $settings = [ordered]@{
        SchemaVersion = 1
        PackageRoot = $PackageRoot.TrimEnd('\')
        Distro = [string]$distro
        SkynetRoot = $skynetRoot
        LanIp = [string]$selectedLan.Address
        Port = $port
        ExpectedSkynetVersion = $expectedVersion
        LastWslIp = $wslIp
        InstalledAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    Write-OurDoorSettings -Settings $settings

    $taskScript = Join-Path $PSScriptRoot 'Run-OurDoorHostTask.ps1'
    if (-not (Test-Path -LiteralPath $taskScript -PathType Leaf)) {
        throw "[M8 主机安装] 缺少高权限任务脚本：$taskScript"
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $principal = New-ScheduledTaskPrincipal `
        -UserId $identity `
        -LogonType Interactive `
        -RunLevel Highest
    $taskSettings = New-ScheduledTaskSettingsSet `
        -MultipleInstances IgnoreNew `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 5) `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -Hidden

    foreach ($actionName in @('Start', 'Stop')) {
        $taskArguments = (
            '-NoProfile -ExecutionPolicy Bypass -File "{0}" -Action {1}' -f
            $taskScript,
            $actionName
        )
        $taskAction = New-ScheduledTaskAction `
            -Execute 'powershell.exe' `
            -Argument $taskArguments
        Register-ScheduledTask `
            -TaskName (Get-OurDoorTaskName -Action $actionName) `
            -Action $taskAction `
            -Principal $principal `
            -Settings $taskSettings `
            -Force | Out-Null
    }

    $desktop = [Environment]::GetFolderPath('Desktop')
    $shell = New-Object -ComObject WScript.Shell
    $shortcutTargets = [ordered]@{
        'OurDoor 启动主机并进入游戏.lnk' =
            (Join-Path $PackageRoot '启动主机并进入游戏.cmd')
        'OurDoor 只启动服务端.lnk' =
            (Join-Path $PackageRoot '只启动服务端.cmd')
        'OurDoor 停止服务端.lnk' =
            (Join-Path $PackageRoot '停止服务端.cmd')
    }
    foreach ($shortcutName in $shortcutTargets.Keys) {
        $shortcut = $shell.CreateShortcut(
            (Join-Path $desktop $shortcutName)
        )
        $shortcut.TargetPath = $shortcutTargets[$shortcutName]
        $shortcut.WorkingDirectory = $PackageRoot
        $shortcut.Save()
    }

    Write-OurDoorLog `
        -PackageRoot $PackageRoot `
        -Message (
            '[M8 安装] 安装完成，distro={0}, skynet={1}, WSL={2}, LAN={3}:{4}, config={5}。' -f
            $distro,
            $skynetRoot,
            $wslIp,
            $selectedLan.Address,
            $port,
            $networkConfigPath
        )
    Write-Host 'OurDoor 局域网主机安装完成。' -ForegroundColor Green
    exit 0
}
catch {
    try {
        Write-OurDoorLog `
            -PackageRoot $PackageRoot `
            -Message "[M8 安装] 安装失败：$($_.Exception.Message)"
    }
    catch {
        Write-Error "[M8 安装] 写入失败日志也失败：$($_.Exception.Message)"
    }
    Write-Error $_
    exit 1
}
