# OurDoor M8 局域网发布工具

## 源码目录

```text
Deployment/
├─ Package-OurDoor.ps1
├─ 生成完整安装包.cmd
└─ PackageRoot/
   ├─ ourdoor-network.json
   ├─ 安装局域网主机.cmd
   ├─ 启动主机并进入游戏.cmd
   ├─ 只启动服务端.cmd
   ├─ 停止服务端.cmd
   └─ HostTools/
      ├─ OurDoorHost.psm1
      ├─ Install-OurDoorHost.ps1
      ├─ Invoke-OurDoorHost.ps1
      ├─ Run-OurDoorHostTask.ps1
      └─ Test-OurDoorHost.ps1
```

这些是发布模板。`Package-OurDoor.ps1` 会另外把
`Server/Skynet` 复制到完整包的 `HostTools/Server/Skynet`。

## 生成完整包

先由用户在 Unity 中手动生成 Windows Build，输出必须包含：

```text
OurDoor.exe
OurDoor_Data/
UnityPlayer.dll
```

然后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Deployment\Package-OurDoor.ps1 `
  -UnityBuildPath "D:\Build\OurDoor" `
  -OutputPath "D:\Release\OurDoor-LAN"
```

也可以双击 `生成完整安装包.cmd` 并传入相同的两个路径。

为防止覆盖可用发布包，输出目录已存在时脚本会明确失败，不会自动删除。
脚本不会启动 Unity，也不会修改 Build Settings、场景或 PlayerSettings。

## 主机 A 前置条件

- Windows 10/11，WSL2 已启用。
- 至少一个名称以 `Ubuntu` 开头的 WSL 发行版。
- Ubuntu 已完成首次启动。
- Skynet 源码位于部署者选择的 Linux 路径。
- Skynet 已完成 `make linux`，`skynet` 可执行。
- Skynet 仓库当前 HEAD 必须精确位于 `v1.8.0` Git 标签。

缺少任何条件都会停止安装并给出错误；工具不会联网下载 WSL、Ubuntu 或
Skynet，也不会接受无法确认的 Skynet 版本。

## 部署顺序

1. 把完整包放到电脑 A 的最终目录。
2. A 运行 `安装局域网主机.cmd`，接受一次 UAC。
3. 根据提示选择 Ubuntu、Skynet 根目录和局域网 IPv4。
4. 安装完成后确认根目录的 `ourdoor-network.json` 已写入 A 的局域网 IP。
5. 再把整个完整包复制给 B/C。
6. A 使用“启动主机并进入游戏”或“只启动服务端”。
7. B/C 直接运行 `OurDoor.exe`。

移动 A 上已经安装的完整包后，必须在新路径重新运行安装程序。安装程序会
拒绝静默改绑到另一个路径。

## 系统变更

只有部署者实际运行 `安装局域网主机.cmd` 后才会发生：

- 创建 Windows Private Profile TCP 8888 入站规则。
- 创建或刷新 `0.0.0.0:8888 → WSL_IP:8888` 的 portproxy。
- 注册 `OurDoorHost-StartServer`、`OurDoorHost-StopServer` 两个最高
  权限、按需运行、禁止并行实例的计划任务。
- 在桌面创建启动和停服快捷方式。
- 在 `%ProgramData%\OurDoorHost` 写入主机设置、PID 和任务结果。

日常入口只触发已注册任务，不重复申请管理员权限。

## 生命周期与安全

- 关闭 A 的游戏不会停止 Skynet。
- 停服不会执行 `wsl --shutdown`。
- PID 对应进程的可执行文件和 config 参数都匹配时才允许停止。
- PID 失效会被清理；PID 指向其他进程时启动和停止都会明确拒绝。
- WSL 8888 已被未知进程监听时不会启动第二份 Skynet。
- Windows 8888 被其他程序或未知 portproxy 占用时明确失败。
- A 关机后 WSL 自然停止；下次启动会清理失效 PID 并刷新 WSL IP。

## 日志

```text
完整包/Logs/Server/
├─ host-management.log
└─ skynet-latest.log
```

管理日志包含 WSL 发行版、Skynet 路径、WSL IP、局域网 IP、防火墙、
portproxy、PID、启动/复用/停止和错误原因。

## 测试

PowerShell 策略测试不会修改防火墙、计划任务、portproxy 或 WSL：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Deployment\PackageRoot\HostTools\Test-OurDoorHost.ps1
```

Unity 配置测试位于 `M8DeploymentConfigTests`。Unity 编译和 EditMode 测试
由用户在里程碑验收时执行。
