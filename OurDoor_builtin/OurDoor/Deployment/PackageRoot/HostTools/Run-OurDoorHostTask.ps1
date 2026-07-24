<#
实现功能：由高权限计划任务启动、复用或精确停止 OurDoor Skynet，并刷新 WSL NAT 转发和输出诊断结果。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Start', 'Stop')]
    [string]$Action
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'OurDoorHost.psm1') -Force

function Get-ServerContext {
    param([Parameter(Mandatory = $true)][object]$Settings)

    $packageRoot = [IO.Path]::GetFullPath([string]$Settings.PackageRoot)
    $serverRootWindows = Join-Path $packageRoot 'HostTools\Server\Skynet'
    $serverRootWsl = ConvertTo-OurDoorWslPath `
        -Distro $Settings.Distro `
        -WindowsPath $serverRootWindows
    $stateRootWsl = ConvertTo-OurDoorWslPath `
        -Distro $Settings.Distro `
        -WindowsPath (Get-OurDoorStateRoot)
    $serverLogWindows = Join-Path $packageRoot 'Logs\Server\skynet-latest.log'
    [IO.Directory]::CreateDirectory(
        (Split-Path -Parent $serverLogWindows)
    ) | Out-Null
    $serverLogWsl = ConvertTo-OurDoorWslPath `
        -Distro $Settings.Distro `
        -WindowsPath $serverLogWindows
    $skynetCandidate =
        ([string]$Settings.SkynetRoot).TrimEnd('/') + '/skynet'
    $skynetLiteral =
        ConvertTo-OurDoorBashLiteral -Value $skynetCandidate
    $skynetResolved = Invoke-OurDoorWsl `
        -Distro $Settings.Distro `
        -Command "readlink -f -- $skynetLiteral"
    if ([string]::IsNullOrWhiteSpace($skynetResolved.Output)) {
        throw "[M8 主机管理] 无法解析 Skynet 可执行文件：$skynetCandidate"
    }

    return [pscustomobject]@{
        PackageRoot = $packageRoot
        ServerRootWindows = $serverRootWindows
        ServerRootWsl = $serverRootWsl
        ConfigWsl = $serverRootWsl.TrimEnd('/') + '/config'
        RunScriptWsl = $serverRootWsl.TrimEnd('/') + '/run.sh'
        SkynetExecutableWsl = $skynetResolved.Output
        PidFileWindows = Join-Path (Get-OurDoorStateRoot) 'skynet.pid'
        PidFileWsl = $stateRootWsl.TrimEnd('/') + '/skynet.pid'
        LauncherWindows =
            Join-Path (Get-OurDoorStateRoot) 'start-skynet.sh'
        LauncherWsl = $stateRootWsl.TrimEnd('/') + '/start-skynet.sh'
        ServerLogWindows = $serverLogWindows
        ServerLogWsl = $serverLogWsl
    }
}

function Get-ServerProcessState {
    param(
        [Parameter(Mandatory = $true)][object]$Settings,
        [Parameter(Mandatory = $true)][object]$Context
    )

    if (-not (Test-Path -LiteralPath $Context.PidFileWindows -PathType Leaf)) {
        return [pscustomobject]@{
            Disposition = 'Missing'
            Pid = $null
            ProcessExists = $false
            IdentityMatches = $false
            Detail = 'PID 文件不存在'
        }
    }

    $pidText = (
        Get-Content -LiteralPath $Context.PidFileWindows -Raw -Encoding ASCII
    ).Trim()
    $serverPid = 0
    if (-not [int]::TryParse($pidText, [ref]$serverPid) -or
        $serverPid -le 0) {
        return [pscustomobject]@{
            Disposition = 'Stale'
            Pid = $null
            ProcessExists = $false
            IdentityMatches = $false
            Detail = "PID 文件内容非法：$pidText"
        }
    }

    $pidLiteral = [string]$serverPid
    $configLiteral = ConvertTo-OurDoorBashLiteral -Value $Context.ConfigWsl
    $command = @"
set -eu
pid=$pidLiteral
config=$configLiteral
if ! kill -0 "`$pid" 2>/dev/null; then
    exit 3
fi
exe=`$(readlink -f "/proc/`$pid/exe")
matches=0
while IFS= read -r arg; do
    if [ "`$arg" = "`$config" ]; then
        matches=1
    fi
done < <(tr '\0' '\n' < "/proc/`$pid/cmdline")
printf '%s\n%s' "`$exe" "`$matches"
"@
    $probe = Invoke-OurDoorWsl `
        -Distro $Settings.Distro `
        -Command $command `
        -AllowFailure
    if ($probe.ExitCode -ne 0) {
        return [pscustomobject]@{
            Disposition = 'Stale'
            Pid = $serverPid
            ProcessExists = $false
            IdentityMatches = $false
            Detail = "PID 对应进程不存在，exitCode=$($probe.ExitCode)"
        }
    }

    $lines = @($probe.Output -split "`r?`n")
    $actualExecutable = if ($lines.Count -ge 1) { $lines[0].Trim() } else { '' }
    $configMatches = $lines.Count -ge 2 -and $lines[1].Trim() -eq '1'
    $identityMatches =
        $actualExecutable -eq $Context.SkynetExecutableWsl -and
        $configMatches
    $disposition = Get-OurDoorPidDisposition `
        -PidFileExists $true `
        -ProcessExists $true `
        -IdentityMatches $identityMatches
    return [pscustomobject]@{
        Disposition = $disposition
        Pid = $serverPid
        ProcessExists = $true
        IdentityMatches = $identityMatches
        Detail = (
            'exe={0}, expected={1}, configMatches={2}' -f
            $actualExecutable,
            $Context.SkynetExecutableWsl,
            $configMatches
        )
    }
}

function Remove-StalePidFile {
    param([Parameter(Mandatory = $true)][object]$Context)
    if (Test-Path -LiteralPath $Context.PidFileWindows -PathType Leaf) {
        Remove-Item -LiteralPath $Context.PidFileWindows -Force
    }
}

function Test-WslPortListening {
    param(
        [Parameter(Mandatory = $true)][string]$Distro,
        [Parameter(Mandatory = $true)][int]$Port
    )
    $command = (
        "ss -ltnH | awk '{{print `$4}}' | grep -Eq ':{0}$'" -f $Port
    )
    $result = Invoke-OurDoorWsl `
        -Distro $Distro `
        -Command $command `
        -AllowFailure
    return $result.ExitCode -eq 0
}

function Wait-OurDoorEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$TargetHost,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutSeconds = 20
    )
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-OurDoorTcpEndpoint `
                -Host $TargetHost `
                -Port $Port `
                -TimeoutMilliseconds 800) {
            return
        }
        Start-Sleep -Milliseconds 250
    }
    throw "[M8 主机启动] 等待局域网端点超时：$TargetHost`:$Port"
}

function Stop-ValidatedServer {
    param(
        [Parameter(Mandatory = $true)][object]$Settings,
        [Parameter(Mandatory = $true)][object]$Context,
        [Parameter(Mandatory = $true)][int]$ServerPid
    )
    $command = @"
set -eu
pid=$ServerPid
start_time=`$(awk '{print `$22}' "/proc/`$pid/stat")
kill -TERM "`$pid"
count=0
while kill -0 "`$pid" 2>/dev/null; do
    current_start=`$(awk '{print `$22}' "/proc/`$pid/stat")
    if [ "`$current_start" != "`$start_time" ]; then
        echo "PID was reused while stopping: `$pid" >&2
        exit 12
    fi
    count=`$((count + 1))
    if [ "`$count" -ge 100 ]; then
        kill -KILL "`$pid"
        break
    fi
    sleep 0.1
done
"@
    Invoke-OurDoorWsl `
        -Distro $Settings.Distro `
        -Command $command | Out-Null
    Remove-StalePidFile -Context $Context
}

function Remove-KnownPortProxy {
    param(
        [Parameter(Mandatory = $true)][object]$Settings,
        [AllowNull()][string]$ExpectedWslIp
    )
    $entries = @(Get-OurDoorPortProxy -Port ([int]$Settings.Port))
    if ($entries.Count -eq 0) {
        return
    }
    if ($entries.Count -ne 1) {
        throw (
            "[M8 停服] TCP $($Settings.Port) 存在多个 portproxy 规则，" +
            '拒绝自动删除。'
        )
    }
    $entry = $entries[0]
    $lastWslIp = [string]$Settings.LastWslIp
    $knownAddresses = @(
        $lastWslIp,
        $ExpectedWslIp
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $isOwned = $entry.ListenAddress -eq '0.0.0.0' -and
        $entry.ConnectPort -eq [int]$Settings.Port -and
        $entry.ConnectAddress -in $knownAddresses
    if (-not $isOwned) {
        throw (
            '[M8 停服] 发现非本工具记录的 portproxy，拒绝删除：' +
            "$($entry.ListenAddress):$($entry.ListenPort) -> " +
            "$($entry.ConnectAddress):$($entry.ConnectPort)"
        )
    }
    Remove-OurDoorPortProxy -Port ([int]$Settings.Port)
}

function Start-Server {
    param(
        [Parameter(Mandatory = $true)][object]$Settings,
        [Parameter(Mandatory = $true)][object]$Context
    )
    if (-not (Test-OurDoorFirewallRule -Port ([int]$Settings.Port))) {
        throw (
            '[M8 主机启动] OurDoor 防火墙规则缺失或被修改。' +
            '请重新运行“安装局域网主机.cmd”。'
        )
    }

    $version = Test-OurDoorSkynetVersion `
        -Distro $Settings.Distro `
        -SkynetRoot $Settings.SkynetRoot `
        -ExpectedVersion $Settings.ExpectedSkynetVersion
    if (-not $version.Valid) {
        throw (
            "[M8 主机启动] Skynet 版本或可执行文件校验失败，" +
            "root=$($Settings.SkynetRoot), detected=$($version.DetectedVersion)。"
        )
    }

    $processState = Get-ServerProcessState `
        -Settings $Settings `
        -Context $Context
    if ($processState.Disposition -eq 'Stale') {
        Write-OurDoorLog `
            -PackageRoot $Context.PackageRoot `
            -Message "[M8 启动] 清理失效 PID：$($processState.Detail)"
        Remove-StalePidFile -Context $Context
    }
    $startDecision = Get-OurDoorStartDecision `
        -PidDisposition $processState.Disposition `
        -ForeignPortOwner $false

    $wslIp = Get-OurDoorWslIp -Distro $Settings.Distro
    if ($startDecision -eq 'Start') {
        if (Test-WslPortListening `
                -Distro $Settings.Distro `
                -Port ([int]$Settings.Port)) {
            throw (
                "[M8 主机启动] WSL TCP $($Settings.Port) 已被未知进程监听，" +
                '且没有可验证的 OurDoor PID，拒绝启动第二份服务端。'
            )
        }
    }

    Set-OurDoorPortProxy `
        -Port ([int]$Settings.Port) `
        -WslIp $wslIp `
        -PreviousWslIp ([string]$Settings.LastWslIp)

    if ($startDecision -eq 'Start') {
        $runLiteral = ConvertTo-OurDoorBashLiteral -Value $Context.RunScriptWsl
        $skynetRootLiteral =
            ConvertTo-OurDoorBashLiteral -Value $Settings.SkynetRoot
        $pidLiteral =
            ConvertTo-OurDoorBashLiteral -Value $Context.PidFileWsl
        $logLiteral =
            ConvertTo-OurDoorBashLiteral -Value $Context.ServerLogWsl
        $launcherContent = @"
#!/usr/bin/env bash
set -eu
run=$runLiteral
skynet_root=$skynetRootLiteral
pid_file=$pidLiteral
log_file=$logLiteral
mkdir -p "`$(dirname "`$pid_file")" "`$(dirname "`$log_file")"
exec >>"`$log_file" 2>&1
: > "`$log_file"
printf '%s' "`$`$" > "`$pid_file"
exec env SKYNET_ROOT="`$skynet_root" sh "`$run"
"@
        Write-OurDoorUtf8NoBom `
            -Path $Context.LauncherWindows `
            -Content $launcherContent.Replace("`r", '')

        # 让隐藏的 wsl.exe 作为 Skynet 的 Windows 侧宿主持续运行。Linux
        # 启动脚本通过 exec 替换为 Skynet，因此写入的 $$ 始终是真实 PID。
        $wslExecutable = (Get-Command wsl.exe -ErrorAction Stop).Source
        Start-Process `
            -FilePath $wslExecutable `
            -ArgumentList @(
                '-d',
                [string]$Settings.Distro,
                '--',
                'bash',
                $Context.LauncherWsl
            ) `
            -WindowStyle Hidden | Out-Null

        $identityDeadline = [DateTime]::UtcNow.AddSeconds(5)
        do {
            Start-Sleep -Milliseconds 100
            $processState = Get-ServerProcessState `
                -Settings $Settings `
                -Context $Context
            if ($processState.Disposition -eq 'Running') {
                break
            }
        } while ([DateTime]::UtcNow -lt $identityDeadline)
        if ($processState.Disposition -ne 'Running') {
            Remove-KnownPortProxy `
                -Settings $Settings `
                -ExpectedWslIp $wslIp
            throw (
                '[M8 主机启动] Skynet 启动后 PID 身份校验失败：' +
                $processState.Detail
            )
        }
    }

    try {
        Wait-OurDoorEndpoint `
            -TargetHost $Settings.LanIp `
            -Port ([int]$Settings.Port)
    }
    catch {
        if ($startDecision -eq 'Start' -and
            $processState.Disposition -eq 'Running') {
            Stop-ValidatedServer `
                -Settings $Settings `
                -Context $Context `
                -ServerPid $processState.Pid
            Remove-KnownPortProxy `
                -Settings $Settings `
                -ExpectedWslIp $wslIp
        }
        throw
    }

    $Settings.LastWslIp = $wslIp
    $Settings | Add-Member `
        -MemberType NoteProperty `
        -Name LastStartedAtUtc `
        -Value ([DateTime]::UtcNow.ToString('o')) `
        -Force
    Write-OurDoorSettings -Settings $Settings
    $mode = if ($startDecision -eq 'Reuse') { '复用' } else { '启动' }
    Write-OurDoorLog `
        -PackageRoot $Context.PackageRoot `
        -Message (
            '[M8 启动] 服务端{0}成功，distro={1}, skynet={2}, WSL={3}, LAN={4}:{5}, PID={6}, log={7}。' -f
            $mode,
            $Settings.Distro,
            $Settings.SkynetRoot,
            $wslIp,
            $Settings.LanIp,
            $Settings.Port,
            $processState.Pid,
            $Context.ServerLogWindows
        )
    return @{
        mode = $mode
        distro = [string]$Settings.Distro
        skynetRoot = [string]$Settings.SkynetRoot
        wslIp = $wslIp
        lanIp = [string]$Settings.LanIp
        port = [int]$Settings.Port
        pid = [int]$processState.Pid
        log = $Context.ServerLogWindows
    }
}

function Stop-Server {
    param(
        [Parameter(Mandatory = $true)][object]$Settings,
        [Parameter(Mandatory = $true)][object]$Context
    )
    $processState = Get-ServerProcessState `
        -Settings $Settings `
        -Context $Context
    $decision = Get-OurDoorStopDecision `
        -PidDisposition $processState.Disposition
    if ($decision -eq 'RefuseForeign') {
        throw (
            '[M8 停服] PID 文件指向非 OurDoor Skynet，拒绝终止任何进程：' +
            $processState.Detail
        )
    }
    if ($decision -eq 'StopTarget') {
        Stop-ValidatedServer `
            -Settings $Settings `
            -Context $Context `
            -ServerPid $processState.Pid
    }
    else {
        Remove-StalePidFile -Context $Context
    }

    Remove-KnownPortProxy -Settings $Settings
    Write-OurDoorLog `
        -PackageRoot $Context.PackageRoot `
        -Message (
            '[M8 停服] 完成，decision={0}, pid={1}, detail={2}。未执行 wsl --shutdown。' -f
            $decision,
            ($processState.Pid -as [string]),
            $processState.Detail
        )
    return @{
        decision = $decision
        pid = $processState.Pid
        wslShutdown = $false
    }
}

try {
    Assert-OurDoorAdministrator
    $settings = Read-OurDoorSettings
    $context = Get-ServerContext -Settings $settings
    $details = if ($Action -eq 'Start') {
        Start-Server -Settings $settings -Context $context
    }
    else {
        Stop-Server -Settings $settings -Context $context
    }
    Write-OurDoorTaskResult `
        -Action $Action `
        -Success $true `
        -Message "$Action 操作成功" `
        -Details $details
    exit 0
}
catch {
    $message = $_.Exception.Message
    try {
        $settingsForLog = Read-OurDoorSettings
        Write-OurDoorLog `
            -PackageRoot $settingsForLog.PackageRoot `
            -Message "[M8 $Action] 失败：$message"
    }
    catch {
        Write-Warning (
            '[M8 主机任务] 主错误发生后写入管理日志也失败：' +
            $_.Exception.Message
        )
    }
    Write-OurDoorTaskResult `
        -Action $Action `
        -Success $false `
        -Message $message `
        -Details @{ exception = [string]$_ }
    Write-Error $_
    exit 1
}
