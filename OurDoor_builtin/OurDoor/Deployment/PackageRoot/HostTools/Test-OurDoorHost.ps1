<#
实现功能：不修改系统配置，验证重复启动、失效 PID、精确停服、依赖缺失和端口冲突策略。
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'OurDoorHost.psm1') -Force

$passed = 0
$failed = 0

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)][string]$Name
    )
    if ($Expected -ne $Actual) {
        throw "$Name 失败：expected=$Expected, actual=$Actual"
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$ExpectedText,
        [Parameter(Mandatory = $true)][string]$Name
    )
    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedText*") {
            throw (
                "$Name 抛出了错误，但消息不匹配：" +
                $_.Exception.Message
            )
        }
        return
    }
    throw "$Name 失败：预期抛出错误。"
}

function Run-Test {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Test
    )
    try {
        & $Test
        $script:passed++
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    catch {
        $script:failed++
        Write-Host "[FAIL] $Name：$($_.Exception.Message)" -ForegroundColor Red
    }
}

Run-Test '重复启动复用原 Skynet' {
    $pidState = Get-OurDoorPidDisposition `
        -PidFileExists $true `
        -ProcessExists $true `
        -IdentityMatches $true
    Assert-Equal 'Running' $pidState 'PID 状态'
    Assert-Equal `
        'Reuse' `
        (Get-OurDoorStartDecision `
            -PidDisposition $pidState `
            -ForeignPortOwner $false) `
        '启动决策'
}

Run-Test '失效 PID 被识别并允许重新启动' {
    $pidState = Get-OurDoorPidDisposition `
        -PidFileExists $true `
        -ProcessExists $false `
        -IdentityMatches $false
    Assert-Equal 'Stale' $pidState 'PID 状态'
    Assert-Equal `
        'Start' `
        (Get-OurDoorStartDecision `
            -PidDisposition $pidState `
            -ForeignPortOwner $false) `
        '启动决策'
}

Run-Test '停止命令只停止身份匹配的目标' {
    Assert-Equal `
        'StopTarget' `
        (Get-OurDoorStopDecision -PidDisposition Running) `
        '目标进程停止决策'
    Assert-Equal `
        'RefuseForeign' `
        (Get-OurDoorStopDecision -PidDisposition Foreign) `
        '外部进程停止决策'
    Assert-Equal `
        'CleanStaleState' `
        (Get-OurDoorStopDecision -PidDisposition Stale) `
        '失效 PID 停止决策'
}

Run-Test 'PID 指向其他进程时拒绝启动第二份服务端' {
    Assert-Throws `
        -Action {
            Get-OurDoorStartDecision `
                -PidDisposition Foreign `
                -ForeignPortOwner $false
        } `
        -ExpectedText 'PID 文件指向非 OurDoor 进程' `
        -Name '外部 PID'
}

Run-Test '端口被其他程序占用时明确失败' {
    Assert-Throws `
        -Action {
            Get-OurDoorStartDecision `
                -PidDisposition Missing `
                -ForeignPortOwner $true
        } `
        -ExpectedText 'TCP 8888 已被其他程序占用' `
        -Name '端口冲突'
}

Run-Test '缺少 WSL 时明确失败' {
    Assert-Throws `
        -Action {
            Assert-OurDoorDependencyState `
                -WslAvailable $false `
                -UbuntuAvailable $true `
                -SkynetAvailable $true `
                -ServerFilesAvailable $true
        } `
        -ExpectedText '缺少 WSL2' `
        -Name 'WSL 依赖'
}

Run-Test '缺少 Ubuntu 时明确失败' {
    Assert-Throws `
        -Action {
            Assert-OurDoorDependencyState `
                -WslAvailable $true `
                -UbuntuAvailable $false `
                -SkynetAvailable $true `
                -ServerFilesAvailable $true
        } `
        -ExpectedText '缺少 Ubuntu' `
        -Name 'Ubuntu 依赖'
}

Run-Test '缺少 Skynet 时明确失败' {
    Assert-Throws `
        -Action {
            Assert-OurDoorDependencyState `
                -WslAvailable $true `
                -UbuntuAvailable $true `
                -SkynetAvailable $false `
                -ServerFilesAvailable $true
        } `
        -ExpectedText '缺少已验证的 Skynet' `
        -Name 'Skynet 依赖'
}

Run-Test '缺少服务端文件时明确失败' {
    Assert-Throws `
        -Action {
            Assert-OurDoorDependencyState `
                -WslAvailable $true `
                -UbuntuAvailable $true `
                -SkynetAvailable $true `
                -ServerFilesAvailable $false
        } `
        -ExpectedText '缺少 OurDoor Skynet 服务端文件' `
        -Name '服务端文件依赖'
}

Run-Test 'WSL 成功命令忽略代理警告且不污染标准输出' {
    $result = Resolve-OurDoorWslCommandResult `
        -ExitCode 0 `
        -StandardOutput '6.6.114.1-microsoft-standard-WSL2' `
        -StandardError 'wsl: 检测到 localhost 代理配置，但未镜像到 WSL。' `
        -Distro 'Ubuntu' `
        -Command 'uname -r'
    Assert-Equal `
        '6.6.114.1-microsoft-standard-WSL2' `
        $result.Output `
        'WSL 标准输出'
    Assert-Equal `
        'wsl: 检测到 localhost 代理配置，但未镜像到 WSL。' `
        $result.Warning `
        'WSL 警告'
}

Run-Test 'WSL 非零退出码仍明确失败' {
    Assert-Throws `
        -Action {
            Resolve-OurDoorWslCommandResult `
                -ExitCode 9 `
                -StandardOutput '' `
                -StandardError '真实 WSL 错误' `
                -Distro 'Ubuntu' `
                -Command 'false'
        } `
        -ExpectedText 'exitCode=9' `
        -Name 'WSL 非零退出码'
}

Run-Test 'WSL 发行版名称允许标准 Ubuntu 名称' {
    Assert-Equal `
        'Ubuntu-24.04' `
        (Assert-OurDoorSafeDistroName -Distro 'Ubuntu-24.04') `
        '安全发行版名称'
}

Run-Test 'WSL 发行版名称拒绝空格和参数字符' {
    Assert-Throws `
        -Action {
            Assert-OurDoorSafeDistroName -Distro 'Ubuntu --exec'
        } `
        -ExpectedText '发行版名称包含不支持的字符' `
        -Name '危险发行版名称'
}

$moduleSource = Get-Content `
    -LiteralPath (Join-Path $PSScriptRoot 'OurDoorHost.psm1') `
    -Raw `
    -Encoding UTF8
Run-Test 'WSL 多行 Bash 命令通过标准输入传递' {
    if ($moduleSource -notmatch
        '(?s)RedirectStandardInput\s*=\s*\$true.+?StandardInput\.BaseStream\.Write\(') {
        throw '未找到通过进程标准输入精确写入 Bash 脚本的实现。'
    }
    if ($moduleSource -match
        '(?s)&\s*wsl\.exe.+?bash\s+-lc\s+\$Command') {
        throw '仍存在容易破坏多行脚本的 bash -lc 参数传递。'
    }
}

Run-Test '泛型列表显式转换为数组以兼容 Windows PowerShell 5.1' {
    if ($moduleSource -match
        'return\s+@\(\$(?:candidates|entries)\)') {
        throw '仍存在会触发参数类型不匹配的泛型列表数组转换。'
    }
    if ($moduleSource -notmatch
        'return\s+\$candidates\.ToArray\(\)' -or
        $moduleSource -notmatch
        'return\s+\$entries\.ToArray\(\)') {
        throw '网卡或端口转发列表未显式转换为数组。'
    }
}

$taskSource = Get-Content `
    -LiteralPath (Join-Path $PSScriptRoot 'Run-OurDoorHostTask.ps1') `
    -Raw `
    -Encoding UTF8
Run-Test '停服脚本不关闭整个 WSL' {
    if ($taskSource -match '(?im)^\s*wsl(?:\.exe)?\s+--shutdown') {
        throw '发现禁止的 wsl --shutdown 命令。'
    }
}

Run-Test '服务端由持久 WSL 宿主承载且记录 exec 后真实 PID' {
    if ($taskSource -match '(?m)^\s*nohup\b') {
        throw '仍使用会随短命 WSL 会话结束的 nohup 后台启动。'
    }
    if ($taskSource -notmatch
        '(?s)Start-Process.+?-FilePath\s+\$wslExecutable.+?-WindowStyle\s+Hidden') {
        throw '未找到隐藏的持久 wsl.exe 宿主启动。'
    }
    if ($taskSource -notmatch
        '(?m)^\s*printf.+`\$`\$.*pid_file') {
        throw 'Linux 启动脚本未在 exec 前记录真实 PID。'
    }
    if ($taskSource -notmatch
        '(?m)^\s*exec env SKYNET_ROOT=') {
        throw 'Linux 启动脚本未通过 exec 替换为 Skynet。'
    }
}

Run-Test '端点等待参数不覆盖 PowerShell 只读 Host 变量' {
    if ($taskSource -match
        '(?s)function\s+Wait-OurDoorEndpoint\s*\{.+?\[string\]\$Host\b') {
        throw 'Wait-OurDoorEndpoint 仍声明了与只读变量冲突的 Host 参数。'
    }
    if ($taskSource -notmatch
        '(?s)function\s+Wait-OurDoorEndpoint\s*\{.+?\[string\]\$TargetHost\b') {
        throw 'Wait-OurDoorEndpoint 缺少 TargetHost 参数。'
    }
}

Write-Host "M8 PowerShell tests completed: passed=$passed, failed=$failed"
if ($failed -ne 0) {
    exit 1
}
exit 0
