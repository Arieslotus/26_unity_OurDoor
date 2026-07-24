# LXY 联网模块

## 当前里程碑

- M0：离线/在线操作接缝，已验收。
- M1：TCP、协议、session、心跳，已验收。
- M2：临时账号、双人房间、角色和场景，已验收。
- M3：第一关服务端权威同步，已验收。
- M4：三关同步、连续换关和第二关真实拿钥匙均已验收。
- M5：退出、对端通知、账号/房间清理和生产心跳，已验收。
- M6：按关卡 FIFO 匹配、取消和掉线清理，已验收。
- M7：最终运行时联网 UI、任意关卡匹配和身份偏好，代码完成，等待验收。
- M8：局域网部署配置、一键主机管理与完整包组装，代码完成，等待验收。

## M2 手动配置

在开始界面的常驻网络 GameObject 上依次添加：

1. `MainThreadDispatcher`
2. `NetworkManager`
3. `OurDoorOnlineController`
4. `M2LobbyDebugPanel`

`M2LobbyDebugPanel` 的 `Online Controller` 必须指向同一对象上的
`OurDoorOnlineController`。调试面板用于 M2 验收，不是正式 UI。

复用现有选关按钮时，每个按钮：

1. 添加 `OurDoorOnlineLevelSelectAdapter`。
2. `Original Level Button` 指向该按钮原有的 `LevelSelectButton`。
3. `Online Controller` 指向常驻网络对象。
4. 将 Button 的 OnClick 从 `LevelSelectButton.OnClick` 改绑为
   `OurDoorOnlineLevelSelectAdapter.OnClick`。

`SimulateTwoPlayer` 仍执行原离线选关逻辑；`TwoPlayer` 只选择房间
`levelId`，收到服务端 `ROOM_READY` 后才加载场景。

场景名固定映射：

| levelId | PC 场景 |
| ---: | --- |
| 1 | `Shop_PC` |
| 2 | `School_PC` |
| 3 | `Hutong_PC` |

以上三个场景必须由用户手动加入 Build Settings。缺少组件、引用、场景或
非法状态时会明确报错，不会自动创建或改正。

## M2 双客户端流程

两个客户端分别执行：

1. 连接同一 Host、Port。
2. 登录前可点击“未登录创建房间”探针，确认服务端返回 `1004`。
3. 使用不同自动生成的 guestId 临时登录。
4. 创建端选择关卡并创建房间，房间码自动复制到剪贴板。
5. 加入端输入房间码并加入。
6. 两端收到相同房间快照和 `ROOM_READY` 后进入相同场景。

M7 最终界面要求创建者明确选择 `Outer` 或 `Inner`，加入者取得相反身份。

## M3 手动配置

在开始界面的常驻网络 GameObject 上增加 `M3Level1DebugPanel`，并将
`Online Controller` 指向同一对象上的 `OurDoorOnlineController`。
该面板随常驻对象进入 `Shop_PC`，用于：

- 单独发送 `SET_POWER`、`PASSWORD_FOUND`、`LOCK_OPENED`。
- 验证错误角色与错误顺序返回明确错误。
- 连续发送两次相同 `clientActionId`，验证 revision 不增加。
- 查看当前第一关完整快照。

第一关正式交互仍使用 M0 已接好的原入口，不需要修改或重新绑定 XYT
组件。客户端只提交意图；`Level1Manager` 只在收到更高 revision 的服务端
快照后更新。

## M4 手动配置

在开始界面的常驻网络 GameObject 上增加 `M4LevelDebugPanel`，并将
`Online Controller` 指向同一对象上的 `OurDoorOnlineController`。

在 `Hutong_PC` 中找到挂有 `DoorGapReceiver` 的门缝触发器物体，手动增加
`OurDoorDoorGapOnlineHook`。该组件只在在线模式提交铁片递送事件，不改变
离线流程。

M4 不要求给第二、三关额外挂同步组件；`OurDoorOnlineController` 会在
服务端确认场景切换后创建对应纯 C# 同步器。第二关远端只应用钥匙落点、
纸箱完成和校门打开的最终状态；第三关远端只应用门缝铁片、铁丝和大门的
最终状态，不同步轨迹。

本关完成后，两端结束剧情会自动分别提交换关准备。调试面板的“本端已准备
进入下一关”按钮可以在验收时跳过剧情等待，但服务端仍会拒绝尚未完成的
关卡。

## M5 手动配置

在开始界面的常驻网络 GameObject 上增加 `M5ExitDebugPanel`，并将
`Online Controller` 指向同一对象上的 `OurDoorOnlineController`。
该面板用于主动退出和显示 `PLAYER_LEFT`，不是正式 UI。

`Start_PC` 必须由用户手动加入 Build Settings。对端退出后：

- 当前客户端保留临时登录与 TCP 连接。
- 会话清除房间、角色、关卡、revision 和快照，回到 `Lobby`。
- 若当前不在 `Start_PC`，代码加载 `Start_PC`。
- 可直接重新选择关卡并创建或加入新房间。

主动退出客户端会释放服务端临时账号、断开 TCP，并进入
`Disconnected`。登录成功后生产心跳自动启动，不需要额外挂组件。

## M6 手动配置

不需要新增场景组件。现有 `M2LobbyDebugPanel` 已增加：

- “匹配所选关卡”
- “取消匹配”
- 当前匹配关卡显示

先使用已有 Level 1/2/3 选关按钮或调试选关按钮设置 `levelId`，再开始
匹配。先入队者为 `Outer`，后入队者为 `Inner`。匹配成功后仍由
`ROOM_SNAPSHOT` 和 `ROOM_READY` 驱动角色写入及场景加载。

## M7 手动配置

不需要 Canvas、EventSystem 或手工创建按钮、文本。

在开始界面的常驻网络 GameObject 上：

1. 增加 `OurDoorOnlineRuntimePanel`。
2. 将 `Online Controller` 指向同一对象的
   `OurDoorOnlineController`。
3. 禁用 `M2LobbyDebugPanel`、`M3Level1DebugPanel`、
   `M4LevelDebugPanel`、`M5ExitDebugPanel`，不要删除源码。

最终面板默认关闭，主键盘或小键盘 Enter 打开/关闭。打开时释放光标并
暂停本地第一人称操作，关闭后恢复；面板跨关卡保持当前开关状态。重要通知
固定显示在左下角，可点 `×` 关闭；锁定光标时可按 Backspace 关闭。

运行后按 M8 的 `ourdoor-network.json` 自动连接服务端。配置无效或连接失败
时显示“重新读取配置并连接”。临时 ID 是每次启动随机生成的 4 位小写字母/
数字组合，不提供手工输入。

登录后的一级页面提供“创建房间、加入房间、匹配”三个入口：

- 创建房间：必须选择第 1/2/3 关以及 `Outer/Inner`，不提供任意身份。
- 加入房间：只输入房间码；关卡和身份由创建者选择。
- 匹配：选择第 1/2/3 关或任意关卡，以及
  `Outer/Inner/任意身份`。
- 三个二级页面均可返回一级页面；返回会把页面重置为初始状态。
- 匹配成功前可以取消匹配。
- 查看房间码、关卡和实际身份，复制房间码或主动退出。

匹配规则：

- 任意关卡与指定关卡相遇时使用指定关卡。
- 两个任意关卡相遇时使用第一关。
- `Outer` 与 `Inner/任意身份` 兼容，`Inner` 与
  `Outer/任意身份` 兼容；相同固定身份不兼容。
- 两个任意身份相遇时先入队者为 `Outer`。

## 测试

Unity Test Runner 的 EditMode 中运行 `LXY.Networking.EditModeTests`。
服务端 Lua 测试见 `Server/Skynet/README.md`。

## M8 客户端部署配置

最终面板不再读取 Inspector 中的 Host/Port。Editor 使用项目根目录、
Windows Build 使用 `OurDoor.exe` 同目录的：

```text
ourdoor-network.json
```

严格格式：

```json
{
  "host": "192.168.1.100",
  "port": 8888
}
```

只允许 `host`、`port` 两个字段。文件缺失、非 UTF-8、非法 JSON、重复
字段、缺失字段、未知字段、空 Host、非法 IPv4/主机名或越界 Port 都会停止
自动连接，并在通知和日志中显示配置路径及原因。不会回退到
`127.0.0.1`。

项目根目录当前配置用于 Editor 本机调试。发布包内的配置先保持无效，电脑 A
运行主机安装程序后写入所选局域网 IPv4，再把同一完整包复制给 B/C。

主机管理和打包方式见 `Deployment/README.md`。
