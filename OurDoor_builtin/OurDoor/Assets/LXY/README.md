# LXY 联网模块

## 当前里程碑

- M0：离线/在线操作接缝，已验收。
- M1：TCP、协议、session、心跳，已验收。
- M2：临时账号、双人房间、角色和场景，已验收。
- M3：第一关服务端权威同步，已验收。
- M4：联网、换关和第三关流程已通过；第二关真实拿钥匙留待单独补测。
- M5：退出、对端通知、账号/房间清理和生产心跳，代码完成，等待验收。

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

创建者固定为 `Outer`，加入者固定为 `Inner`。

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

## 测试

Unity Test Runner 的 EditMode 中运行 `LXY.Networking.EditModeTests`。
服务端 Lua 测试见 `Server/Skynet/README.md`。
