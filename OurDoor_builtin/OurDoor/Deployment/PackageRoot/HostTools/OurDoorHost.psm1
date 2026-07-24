<#
实现功能：提供 OurDoor 局域网主机安装、WSL 管理、端口转发、日志、PID 身份校验和任务结果工具。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StateRoot = Join-Path $env:ProgramData 'OurDoorHost'
$script:SettingsPath = Join-Path $script:StateRoot 'host-settings.json'
$script:StartResultPath = Join-Path $script:StateRoot 'last-start-result.json'
$script:StopResultPath = Join-Path $script:StateRoot 'last-stop-result.json'
$script:StartTaskName = 'OurDoorHost-StartServer'
$script:StopTaskName = 'OurDoorHost-StopServer'
$script:FirewallRuleName = 'OurDoor Skynet TCP 8888'

function Get-OurDoorStateRoot {
    return $script:StateRoot
}

function Get-OurDoorSettingsPath {
    return $script:SettingsPath
}

function Get-OurDoorResultPath {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Start', 'Stop')]
        [string]$Action
    )
    if ($Action -eq 'Start') {
        return $script:StartResultPath
    }
    return $script:StopResultPath
}

function Get-OurDoorTaskName {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Start', 'Stop')]
        [string]$Action
    )
    if ($Action -eq 'Start') {
        return $script:StartTaskName
    }
    return $script:StopTaskName
}

function Get-OurDoorFirewallRuleName {
    return $script:FirewallRuleName
}

function Test-OurDoorAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
}

function Assert-OurDoorAdministrator {
    if (-not (Test-OurDoorAdministrator)) {
        throw '[M8 主机管理] 当前进程没有管理员权限。'
    }
}

function Write-OurDoorUtf8NoBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $encoding = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Write-OurDoorLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    $logDirectory = Join-Path $PackageRoot 'Logs\Server'
    [IO.Directory]::CreateDirectory($logDirectory) | Out-Null
    $managementLog = Join-Path $logDirectory 'host-management.log'
    $line = '{0:yyyy-MM-dd HH:mm:ss.fff} {1}' -f (Get-Date), $Message
    [IO.File]::AppendAllText(
        $managementLog,
        $line + [Environment]::NewLine,
        (New-Object Text.UTF8Encoding($false))
    )
    Write-Host $line
}

function ConvertTo-OurDoorBashLiteral {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )
    return "'" + $Value.Replace("'", "'""'""'") + "'"
}

function Resolve-OurDoorWslCommandResult {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$StandardOutput,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$StandardError,
        [Parameter(Mandatory = $true)]
        [string]$Distro,
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [switch]$AllowFailure
    )
    $outputText = ($StandardOutput -replace "`0", '').Trim()
    $errorText = ($StandardError -replace "`0", '').Trim()
    if ($ExitCode -ne 0 -and -not $AllowFailure) {
        $diagnostic = if ([string]::IsNullOrWhiteSpace($errorText)) {
            $outputText
        }
        else {
            $errorText
        }
        throw (
            '[M8 WSL] 命令失败，distro={0}, exitCode={1}, command={2}, output={3}' -f
            $Distro,
            $ExitCode,
            $Command,
            $diagnostic
        )
    }
    return [pscustomobject]@{
        ExitCode = $ExitCode
        Output = $outputText
        Warning = $errorText
    }
}

function Assert-OurDoorSafeDistroName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Distro
    )
    if ($Distro -notmatch '^[A-Za-z0-9._-]+$') {
        throw (
            '[M8 WSL] 发行版名称包含不支持的字符；' +
            "只允许英文字母、数字、点、下划线和连字符，distro=$Distro"
        )
    }
    return $Distro
}

function Invoke-OurDoorWsl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Distro,
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [switch]$AllowFailure
    )
    $safeDistro = Assert-OurDoorSafeDistroName -Distro $Distro
    $utf8 = New-Object Text.UTF8Encoding($false)
    $processInfo = New-Object Diagnostics.ProcessStartInfo
    $processInfo.FileName = (Get-Command wsl.exe -ErrorAction Stop).Source
    # Windows PowerShell 5.1 的 ProcessStartInfo 会把这里的双引号原样交给
    # wsl.exe，使其查找名为 "Ubuntu"（含引号）的发行版。
    $processInfo.Arguments = "-d $safeDistro -- bash -s"
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.RedirectStandardInput = $true
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $processInfo.StandardOutputEncoding = $utf8
    $processInfo.StandardErrorEncoding = $utf8

    $process = New-Object Diagnostics.Process
    $process.StartInfo = $processInfo
    try {
        if (-not $process.Start()) {
            throw '[M8 WSL] 无法启动 wsl.exe 进程。'
        }
        $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
        $standardErrorTask = $process.StandardError.ReadToEndAsync()
        # 直接写 UTF-8 字节而不是 PowerShell 管道：避免 Windows PowerShell
        # 自动附加 CRLF，导致 Linux 将 uname -r 解析为带回车的非法参数。
        $commandBytes = $utf8.GetBytes($Command.Replace("`r", ''))
        $process.StandardInput.BaseStream.Write(
            $commandBytes,
            0,
            $commandBytes.Length
        )
        $process.StandardInput.BaseStream.Close()
        $process.WaitForExit()
        $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
        $standardError = $standardErrorTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
    return Resolve-OurDoorWslCommandResult `
        -ExitCode $exitCode `
        -StandardOutput ([string]$standardOutput) `
        -StandardError ([string]$standardError) `
        -Distro $Distro `
        -Command $Command `
        -AllowFailure:$AllowFailure
}

function ConvertTo-OurDoorWslPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Distro,
        [Parameter(Mandatory = $true)]
        [string]$WindowsPath
    )
    $literal = ConvertTo-OurDoorBashLiteral -Value $WindowsPath
    $result = Invoke-OurDoorWsl `
        -Distro $Distro `
        -Command "wslpath -a -u -- $literal"
    if ([string]::IsNullOrWhiteSpace($result.Output)) {
        throw "[M8 WSL] Windows 路径转换结果为空：$WindowsPath"
    }
    return $result.Output
}

function Get-OurDoorWslIp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Distro
    )
    $command = @'
set -eu
iface=$(ip -4 route show default | awk 'NR==1 {print $5}')
if [ -z "$iface" ]; then
    echo "WSL default route interface is missing" >&2
    exit 11
fi
ip -4 -o addr show dev "$iface" scope global |
    awk 'NR==1 {split($4,a,"/"); print a[1]}'
'@
    $result = Invoke-OurDoorWsl -Distro $Distro -Command $command
    $ip = $result.Output.Trim()
    $parsed = $null
    if (-not [Net.IPAddress]::TryParse($ip, [ref]$parsed) -or
        $parsed.AddressFamily -ne
            [Net.Sockets.AddressFamily]::InterNetwork) {
        throw "[M8 WSL] 无法取得默认路由网卡的 IPv4，result=$ip"
    }
    return $ip
}

function Test-OurDoorSkynetVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Distro,
        [Parameter(Mandatory = $true)]
        [string]$SkynetRoot,
        [string]$ExpectedVersion = 'v1.8.0'
    )
    $root = ConvertTo-OurDoorBashLiteral -Value $SkynetRoot
    $expected = ConvertTo-OurDoorBashLiteral -Value $ExpectedVersion
    $command = @"
set -eu
root=$root
expected=$expected
test -x "`$root/skynet"
command -v git >/dev/null 2>&1
tag=`$(git -C "`$root" describe --tags --exact-match HEAD 2>/dev/null)
test "`$tag" = "`$expected"
printf '%s' "`$tag"
"@
    $result = Invoke-OurDoorWsl `
        -Distro $Distro `
        -Command $command `
        -AllowFailure
    return [pscustomobject]@{
        Valid = $result.ExitCode -eq 0
        DetectedVersion = $result.Output
        ExpectedVersion = $ExpectedVersion
        SkynetRoot = $SkynetRoot
    }
}

function Get-OurDoorWslDistros {
    if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
        throw (
            '[M8 主机安装] 未找到 wsl.exe。请先启用 WSL2，安装步骤：' +
            'https://learn.microsoft.com/windows/wsl/install'
        )
    }
    $standardErrorPath = [IO.Path]::GetTempFileName()
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & wsl.exe --list --quiet 2> $standardErrorPath
        $exitCode = $LASTEXITCODE
        $standardError = Get-Content `
            -LiteralPath $standardErrorPath `
            -Raw `
            -ErrorAction SilentlyContinue
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Remove-Item -LiteralPath $standardErrorPath -Force -ErrorAction SilentlyContinue
    }
    if ($exitCode -ne 0) {
        $diagnostic = if ([string]::IsNullOrWhiteSpace($standardError)) {
            $output | Out-String
        }
        else {
            $standardError
        }
        throw "[M8 主机安装] 无法列出 WSL 发行版：$diagnostic"
    }
    return @(
        $output |
            ForEach-Object { ($_ -replace "`0", '').Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

function Select-OurDoorCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Candidates,
        [Parameter(Mandatory = $true)]
        [string]$Title,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Formatter
    )
    if ($Candidates.Count -eq 0) {
        throw "[M8 主机安装] 没有可用候选项：$Title"
    }
    if ($Candidates.Count -eq 1) {
        return $Candidates[0]
    }

    Write-Host $Title
    for ($index = 0; $index -lt $Candidates.Count; $index++) {
        $description = & $Formatter $Candidates[$index]
        Write-Host ('  [{0}] {1}' -f ($index + 1), $description)
    }
    while ($true) {
        $inputValue = Read-Host "请输入序号 1-$($Candidates.Count)"
        $selected = 0
        if ([int]::TryParse($inputValue, [ref]$selected) -and
            $selected -ge 1 -and
            $selected -le $Candidates.Count) {
            return $Candidates[$selected - 1]
        }
        Write-Host '输入无效，请重新选择。' -ForegroundColor Yellow
    }
}

function Get-OurDoorLanCandidates {
    $candidates = New-Object Collections.Generic.List[object]
    $configurations = Get-NetIPConfiguration |
        Where-Object {
            $_.NetAdapter.Status -eq 'Up' -and
            $_.InterfaceAlias -notlike 'vEthernet*' -and
            $_.InterfaceAlias -notlike '*Loopback*' -and
            $_.InterfaceAlias -notlike 'WSL*'
        }
    foreach ($configuration in $configurations) {
        foreach ($address in @($configuration.IPv4Address)) {
            if ($null -eq $address) {
                continue
            }
            $ip = [string]$address.IPAddress
            if ($ip -eq '127.0.0.1' -or $ip.StartsWith('169.254.')) {
                continue
            }
            $candidates.Add([pscustomobject]@{
                InterfaceAlias = $configuration.InterfaceAlias
                InterfaceDescription = $configuration.InterfaceDescription
                Address = $ip
            })
        }
    }
    return $candidates.ToArray()
}

function Get-OurDoorPidDisposition {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$PidFileExists,
        [Parameter(Mandatory = $true)]
        [bool]$ProcessExists,
        [Parameter(Mandatory = $true)]
        [bool]$IdentityMatches
    )
    if (-not $PidFileExists) {
        return 'Missing'
    }
    if (-not $ProcessExists) {
        return 'Stale'
    }
    if (-not $IdentityMatches) {
        return 'Foreign'
    }
    return 'Running'
}

function Get-OurDoorStartDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Missing', 'Stale', 'Foreign', 'Running')]
        [string]$PidDisposition,
        [Parameter(Mandatory = $true)]
        [bool]$ForeignPortOwner
    )
    if ($PidDisposition -eq 'Foreign') {
        throw '[M8 主机启动] PID 文件指向非 OurDoor 进程，拒绝启动。'
    }
    if ($ForeignPortOwner) {
        throw '[M8 主机启动] TCP 8888 已被其他程序占用，拒绝启动。'
    }
    if ($PidDisposition -eq 'Running') {
        return 'Reuse'
    }
    return 'Start'
}

function Get-OurDoorStopDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Missing', 'Stale', 'Foreign', 'Running')]
        [string]$PidDisposition
    )
    if ($PidDisposition -eq 'Foreign') {
        return 'RefuseForeign'
    }
    if ($PidDisposition -eq 'Running') {
        return 'StopTarget'
    }
    return 'CleanStaleState'
}

function Assert-OurDoorDependencyState {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$WslAvailable,
        [Parameter(Mandatory = $true)]
        [bool]$UbuntuAvailable,
        [Parameter(Mandatory = $true)]
        [bool]$SkynetAvailable,
        [Parameter(Mandatory = $true)]
        [bool]$ServerFilesAvailable
    )
    if (-not $WslAvailable) {
        throw '[M8 主机安装] 缺少 WSL2。'
    }
    if (-not $UbuntuAvailable) {
        throw '[M8 主机安装] 缺少 Ubuntu WSL 发行版。'
    }
    if (-not $SkynetAvailable) {
        throw '[M8 主机安装] 缺少已验证的 Skynet v1.8.0。'
    }
    if (-not $ServerFilesAvailable) {
        throw '[M8 主机安装] 缺少 OurDoor Skynet 服务端文件。'
    }
}

function Read-OurDoorSettings {
    if (-not (Test-Path -LiteralPath $script:SettingsPath -PathType Leaf)) {
        throw (
            '[M8 主机管理] 尚未安装局域网主机。请先运行“安装局域网主机.cmd”，' +
            "settings=$script:SettingsPath"
        )
    }
    $settings = Get-Content `
        -LiteralPath $script:SettingsPath `
        -Raw `
        -Encoding UTF8 | ConvertFrom-Json
    $required = @(
        'SchemaVersion',
        'PackageRoot',
        'Distro',
        'SkynetRoot',
        'LanIp',
        'Port',
        'ExpectedSkynetVersion',
        'LastWslIp'
    )
    foreach ($name in $required) {
        if ($null -eq $settings.$name -or
            [string]::IsNullOrWhiteSpace([string]$settings.$name)) {
            throw "[M8 主机管理] 主机设置缺少字段：$name"
        }
    }
    if ([int]$settings.SchemaVersion -ne 1) {
        throw (
            '[M8 主机管理] 不支持的主机设置版本：' +
            $settings.SchemaVersion
        )
    }
    if ([int]$settings.Port -ne 8888) {
        throw "[M8 主机管理] 主机设置端口必须为 8888：$($settings.Port)"
    }
    if ([string]$settings.ExpectedSkynetVersion -ne 'v1.8.0') {
        throw (
            '[M8 主机管理] 主机设置中的 Skynet 版本必须为 v1.8.0：' +
            $settings.ExpectedSkynetVersion
        )
    }
    foreach ($addressName in @('LanIp', 'LastWslIp')) {
        $parsedAddress = $null
        if (-not [Net.IPAddress]::TryParse(
                [string]$settings.$addressName,
                [ref]$parsedAddress) -or
            $parsedAddress.AddressFamily -ne
                [Net.Sockets.AddressFamily]::InterNetwork) {
            throw (
                "[M8 主机管理] 主机设置中的 $addressName 不是合法 IPv4：" +
                $settings.$addressName
            )
        }
    }
    return $settings
}

function Write-OurDoorSettings {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Settings
    )
    [IO.Directory]::CreateDirectory($script:StateRoot) | Out-Null
    $json = $Settings | ConvertTo-Json -Depth 8
    Write-OurDoorUtf8NoBom -Path $script:SettingsPath -Content $json
}

function Write-OurDoorTaskResult {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Start', 'Stop')]
        [string]$Action,
        [Parameter(Mandatory = $true)]
        [bool]$Success,
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [hashtable]$Details
    )
    $result = [ordered]@{
        action = $Action
        success = $Success
        timestampUtc = [DateTime]::UtcNow.ToString('o')
        message = $Message
        details = $Details
    }
    Write-OurDoorUtf8NoBom `
        -Path (Get-OurDoorResultPath -Action $Action) `
        -Content ($result | ConvertTo-Json -Depth 8)
}

function Test-OurDoorTcpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Host,
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [int]$TimeoutMilliseconds = 1000
    )
    $client = New-Object Net.Sockets.TcpClient
    try {
        $task = $client.ConnectAsync($Host, $Port)
        if (-not $task.Wait($TimeoutMilliseconds)) {
            return $false
        }
        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Get-OurDoorPortProxy {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )
    $output = & netsh.exe interface portproxy show v4tov4 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "[M8 端口转发] 无法读取 portproxy：$($output | Out-String)"
    }
    $entries = New-Object Collections.Generic.List[object]
    foreach ($line in $output) {
        $text = [string]$line
        if ($text -match '^\s*(\S+)\s+(\d+)\s+(\S+)\s+(\d+)\s*$') {
            if ([int]$Matches[2] -eq $Port) {
                $entries.Add([pscustomobject]@{
                    ListenAddress = $Matches[1]
                    ListenPort = [int]$Matches[2]
                    ConnectAddress = $Matches[3]
                    ConnectPort = [int]$Matches[4]
                })
            }
        }
    }
    return $entries.ToArray()
}

function Remove-OurDoorPortProxy {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )
    $output = & netsh.exe interface portproxy delete v4tov4 `
        listenaddress=0.0.0.0 `
        listenport=$Port 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "[M8 端口转发] 删除 TCP $Port 转发失败：$($output | Out-String)"
    }
}

function Set-OurDoorPortProxy {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [Parameter(Mandatory = $true)]
        [string]$WslIp,
        [AllowNull()]
        [string]$PreviousWslIp
    )
    $entries = @(Get-OurDoorPortProxy -Port $Port)
    if ($entries.Count -gt 1) {
        throw "[M8 端口转发] TCP $Port 存在多个 portproxy 规则，拒绝猜测。"
    }
    if ($entries.Count -eq 1) {
        $entry = $entries[0]
        $isCurrent = $entry.ListenAddress -eq '0.0.0.0' -and
            $entry.ConnectAddress -eq $WslIp -and
            $entry.ConnectPort -eq $Port
        if ($isCurrent) {
            return
        }
        $isKnownOldRule = $entry.ListenAddress -eq '0.0.0.0' -and
            -not [string]::IsNullOrWhiteSpace($PreviousWslIp) -and
            $entry.ConnectAddress -eq $PreviousWslIp -and
            $entry.ConnectPort -eq $Port
        if (-not $isKnownOldRule) {
            throw (
                "[M8 端口转发] TCP $Port 已存在非本工具规则：" +
                "$($entry.ListenAddress):$($entry.ListenPort) -> " +
                "$($entry.ConnectAddress):$($entry.ConnectPort)"
            )
        }
        Remove-OurDoorPortProxy -Port $Port
    }

    $listeners = @(
        Get-NetTCPConnection `
            -State Listen `
            -LocalPort $Port `
            -ErrorAction SilentlyContinue
    )
    if ($listeners.Count -gt 0) {
        $owners = ($listeners | Select-Object -ExpandProperty OwningProcess -Unique) -join ','
        throw "[M8 端口转发] TCP $Port 已被其他程序监听，PID=$owners。"
    }

    $output = & netsh.exe interface portproxy add v4tov4 `
        listenaddress=0.0.0.0 `
        listenport=$Port `
        connectaddress=$WslIp `
        connectport=$Port 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "[M8 端口转发] 创建 TCP $Port 转发失败：$($output | Out-String)"
    }
}

function Test-OurDoorFirewallRule {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )
    $rules = @(
        Get-NetFirewallRule `
            -DisplayName $script:FirewallRuleName `
            -ErrorAction SilentlyContinue
    )
    if ($rules.Count -ne 1) {
        return $false
    }
    $rule = $rules[0]
    if ($rule.Enabled -ne 'True' -or
        $rule.Direction -ne 'Inbound' -or
        $rule.Action -ne 'Allow' -or
        [string]$rule.Profile -ne 'Private') {
        return $false
    }
    $filters = @($rule | Get-NetFirewallPortFilter)
    return $filters.Count -eq 1 -and
        $filters[0].Protocol -eq 'TCP' -and
        [string]$filters[0].LocalPort -eq [string]$Port
}

Export-ModuleMember -Function *
