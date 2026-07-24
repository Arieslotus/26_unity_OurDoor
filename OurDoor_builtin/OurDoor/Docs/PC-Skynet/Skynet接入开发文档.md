# 《OurDoor》Skynet 联网接入开发文档

> 客户端：Unity 2022.3.62f3c1，PC
>
> 服务端：Skynet，Linux
>
> Unity 客户端新增文件统一放在 `Assets/LXY`
>
> Skynet 服务端放在项目根目录 `Server/Skynet`

## 1. 开发目标

### 1.1 P0

1. 两个 Unity PC 客户端连接同一 Skynet 服务端。
2. 使用临时账号登录。
3. 创建房间或使用房间码加入房间。
4. 服务端分配 `Outer`、`Inner`，让两端进入同一关卡。
5. 第一、二、三关的关键协作事件由服务端校验并同步。
6. 客户端只提交操作意图，服务端是关卡状态的唯一权威。

### 1.2 P1

1. 一端主动退出、异常断线或心跳超时后，另一端收到通知。
2. 服务端清理连接、临时账号、房间和匹配队列。
3. 按关卡匹配，同一 `levelId` 的两名玩家自动组成房间。

### 1.3 不做

- 正式账号、注册、密码登录。
- 数据库及持久化业务数据。
- 断线重连和会话恢复。
- 位置、动作、动画参数、刚体和物理轨迹的连续同步。
- 聊天、好友、战绩、排行榜。
- XLua、Sproto、Protobuf。
- 多人房间、观战、中途加入。

## 2. 模块化原则

账号、房间、匹配、TCP 和协议与具体关卡解耦，分成三层：

```text
通用客户端层
  TCP / 协议 / 登录 / 房间 / 匹配 / 会话
          ↓ 通用事件和接口
OurDoor 适配层
  角色映射 / 场景映射 / 三关 action 与快照应用
          ↓
现有游戏代码
  GameManager / PlayersManager / LevelXManager / 关卡表现

通用 Skynet 服务端
  gate / agent / account / lobby / match / room
          ↓ 注入游戏规则
OurDoor 服务端规则
  levelId / action / 角色权限 / 前置状态 / 状态变更
```

约束：

- 通用层不得引用 `GameManager`、`PlayersManager`、`Level1Manager`、`Level2Manager`、`L3Manager`。
- 通用房间只管理成员、角色槽位、阶段和游戏状态容器，不硬编码三关 action。
- OurDoor 规则通过接口或规则表接入。
- UI 通过事件调用通用服务，不直接操作 Socket。
- 其他项目复用时，只替换游戏适配层和服务端规则层。
- 当前只要求源码级复用，不提前拆成 Unity Package，避免增加维护复杂度。

## 3. 目录规划

### 3.1 Unity 客户端

```text
Assets/LXY/
├── Network/
│   ├── Core/
│   │   ├── NetworkClient.cs
│   │   ├── PacketFramer.cs
│   │   └── MainThreadDispatcher.cs
│   ├── Protocol/
│   │   ├── MessageIds.cs
│   │   ├── ProtocolCodec.cs
│   │   ├── ProtocolDtos.cs
│   │   ├── LobbyDtos.cs
│   │   ├── LevelActionDtos.cs
│   │   ├── LevelFlowDtos.cs
│   │   └── RoomLifecycleDtos.cs
│   ├── Session/
│   │   ├── NetworkSession.cs
│   │   └── OnlineSessionState.cs
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── RoomService.cs
│   │   ├── LevelActionService.cs
│   │   ├── LevelFlowService.cs
│   │   ├── RoomLifecycleService.cs
│   │   └── HeartbeatService.cs
│   ├── NetworkManager.cs
│   └── NetworkConfig.cs
├── Online/
│   ├── Runtime/
│   │   ├── OnlineActionBridge.cs
│   │   └── OnlineActionIntent.cs
│   ├── OurDoor/
│   │   ├── OurDoorOnlineController.cs
│   │   ├── OurDoorSceneRouter.cs
│   │   ├── OurDoorRoleAdapter.cs
│   │   ├── OurDoorOnlineLevelSelectAdapter.cs
│   │   ├── OurDoorOnlineLevelFlow.cs
│   │   └── OurDoorLevelXSnapshotSynchronizer / Presentation
│   └── Debug/
│       ├── M2LobbyDebugPanel.cs
│       ├── M3Level1DebugPanel.cs
│       ├── M4LevelDebugPanel.cs
│       └── M5ExitDebugPanel.cs
└── Tests/
    └── EditMode/
```

职责：

- `Network/Core`：连接、收发、半包/粘包和主线程队列。
- `Network/Protocol`：通用消息头、消息 ID、错误码和 DTO。
- `Network/Session`：登录与房间会话状态。
- `Network/Services`：临时登录、房间、匹配和关卡操作用例。
- `NetworkManager`：连接状态、session、请求超时、心跳和推送分发。
- `Online/OurDoor`：唯一允许引用现有项目代码的适配层。
- `Tests`：帧解析、协议、状态机和关卡规则测试。

### 3.2 Skynet 服务端

```text
Server/Skynet/
├── config
├── service/
│   ├── main.lua
│   ├── gate_service.lua
│   ├── agent.lua
│   ├── account_service.lua
│   ├── lobby_service.lua
│   ├── room_service.lua
│   └── match_service.lua
├── lualib/
│   ├── protocol.lua
│   ├── packet.lua
│   ├── error_code.lua
│   ├── json.lua
│   ├── game_rule.lua（M3）
│   └── match_queue.lua
├── games/（M3-M4）
│   └── ourdoor/
│       ├── level_rules.lua
│       └── room_state.lua
├── tests/
└── run.sh
```

该目录位于 `Assets` 外，Unity 不会导入 Lua、Shell 和服务端配置。部署时可整体复制到 Linux，与 Unity 客户端无运行时依赖。

通用服务职责：

- `gate_service`：监听、收包、连接生命周期和 agent 绑定。
- `agent`：单连接登录态、请求分发、权限校验和断线入口。
- `account_service`：内存临时账号和在线占用。
- `lobby_service`：创建和加入房间的统一大厅入口。
- `match_service`：按 `levelId` 等待、取消、掉线移除和配对。
- `room_service`：房间码、账号到房间索引、成员、角色槽位、阶段、
  revision 和广播；M3 再增加 action 幂等。
- `game_rule`：通用规则接口，不包含 OurDoor action。
- `games/ourdoor`：三关角色权限、前置状态和状态变更。

## 4. 总体流程

```text
PC/VR 交互
  → LevelXNetworkSync.RequestAction
  → NetworkManager
  → TCP：两字节大端包长 + 固定消息头 + UTF-8 JSON
  → gate_service → agent
  → account / lobby / match / room
  → OurDoor level_rules 校验角色与前置状态
  → 更新状态与 revision
  → 广播完整 ROOM_SNAPSHOT
  → Unity 主线程
  → LevelXNetworkSync.ApplySnapshot
  → LevelXManager 与确定性表现
```

固定决策：

| 项目 | 决策 |
| --- | --- |
| 连接 | 单 TCP 长连接，登录和游戏共用一个端口 |
| 编码 | 固定二进制消息头 + UTF-8 JSON |
| JSON | Unity `JsonUtility` + 明确 DTO，不使用字典和多态 |
| 服务端状态 | 临时账号、队列、房间、关卡状态全部保存在内存 |
| 房间人数 | 固定 2 人 |
| 角色 | 创建者/匹配先入队者为 Outer，另一人为 Inner |
| 开始条件 | 两人齐后服务端推送 `ROOM_READY` |
| 场景映射 | 1=`Shop_PC`，2=`School_PC`，3=`Hutong_PC` |
| 一致性 | 服务端权威，每次有效变化广播完整快照 |
| 幂等 | 每个操作携带 `clientActionId` |

首版由一个 `room_service` 管理全部房间，不为每个房间创建独立 Skynet service。

## 5. 客户端设计

### 5.1 连接状态

```text
Disconnected
  → Connecting
  → Connected
  → Authenticating
  → Lobby
  → WaitingRoom / Matching
  → LoadingLevel
  → Playing
  → Disconnected
```

要求：

- 只有一个读循环。
- 多个发送请求必须串行写入 `NetworkStream`。
- 网络线程不得访问 GameObject、UI、`SceneManager` 或关卡 Manager。
- 所有 Unity 操作切回主线程。
- 断开只清理一次，并取消全部 pending 请求。
- 场景组件销毁时取消消息订阅。

### 5.2 临时登录

客户端生成本地 `guestId` 和显示昵称：

```json
{"guestId":"local-guid","displayName":"PlayerA","clientVersion":"0.1.0"}
```

服务端校验：

- 同一连接只能登录一次。
- 同一 `guestId` 同时只允许一个在线连接。
- 未登录不能创建房间、加入房间、匹配或提交关卡事件。
- 服务端重启后临时账号全部清空。

### 5.3 房间与场景

1. 玩家携带 `levelId` 创建房间，或使用 `roomId` 加入。
2. 第二人加入后，服务端分配角色并推送 `ROOM_READY`。
3. 客户端先保存 `roomId/role/levelId`。
4. `OurDoorRoleAdapter` 写入 `GameManager`。
5. `OurDoorSceneRouter` 在主线程加载对应 PC 场景。
6. 场景中的 `LevelXNetworkSync` 应用最新完整快照。

不能以“创建/加入请求成功”直接进入场景，必须等待 `ROOM_READY`。

### 5.4 匹配

`MatchService` 只提供：

- `RequestMatch(levelId)`
- `CancelMatch()`
- `OnMatchFound`

服务端按 `levelId` 使用 FIFO 队列。配对成功后复用房间创建、角色分配和 `ROOM_READY` 流程，不实现第二套房间逻辑。

客户端在发出请求前从 `Lobby` 进入 `Matching`。匹配响应只确认是否已经
入队；真正建立房间以 `MATCH_FOUND` 为准：

```json
{
  "roomId":"R00001",
  "levelId":2,
  "role":"Outer",
  "revision":1
}
```

收到 `MATCH_FOUND` 后进入 `WaitingRoom`。由于 Skynet 的房间推送和匹配
推送来自不同 service，客户端允许 `ROOM_SNAPSHOT`、`ROOM_READY` 先到达，
但只能暂存；必须先处理 `MATCH_FOUND` 建立房间身份，再按原顺序应用暂存
推送。缺失、重复或关卡不一致时直接报错。

匹配错误码：

| code | 含义 |
| ---: | --- |
| 4001 | 当前账号已经在匹配队列中 |
| 4002 | 当前账号不在匹配队列中 |

## 6. 协议

### 6.1 帧格式

```text
uint16 BE payloadLength
uint16 BE messageId
uint32 BE session
uint8      messageType   // 0=request, 1=response, 2=push
byte[]     jsonBody      // UTF-8，无 BOM
```

- `payloadLength` 只计算其后的 Payload。
- Payload 最小 7 字节，最大 32 KiB。
- request 的 session 非零。
- response 沿用请求 session。
- push 的 session 为 0。
- 非法长度和未知消息类型直接断开。
- 普通业务失败使用相同 `messageId/session` 返回 `code != 0`。

### 6.2 消息 ID

| ID | 名称 | 优先级 | 说明 |
| ---: | --- | --- | --- |
| 1 | `HEARTBEAT` | P0 | 心跳 |
| 100 | `GUEST_LOGIN` | P0 | 临时账号登录 |
| 200 | `CREATE_ROOM` | P0 | 创建房间 |
| 201 | `JOIN_ROOM` | P0 | 房间码加入 |
| 202 | `LEAVE_ROOM` | P1 | 主动离开 |
| 210 | `MATCH_REQUEST` | P1 | 按关卡匹配 |
| 211 | `MATCH_CANCEL` | P1 | 取消匹配 |
| 300 | `LEVEL_ACTION` | P0 | 提交关卡动作 |
| 301 | `READY_NEXT_LEVEL` | P0 | 本端已准备进入下一关 |
| 900 | `ROOM_READY` | P0 | 角色和场景已确定 |
| 901 | `ROOM_SNAPSHOT` | P0 | 完整房间状态 |
| 902 | `PLAYER_LEFT` | P1 | 对方离开 |
| 903 | `MATCH_FOUND` | P1 | 匹配成功 |
| 904 | `LEVEL_CHANGED` | P0 | 原子切换关卡并携带完整快照 |
| 999 | `SERVER_ERROR` | P0 | 服务端错误推送 |

### 6.3 关卡动作

```json
{
  "roomId":"ABCD12",
  "levelId":2,
  "action":"KEY_FOUND",
  "boolValue":true,
  "clientActionId":"uid-000018"
}
```

服务端必须校验：

- 当前连接已经登录。
- `uid` 属于该房间。
- `levelId` 与房间一致。
- 当前角色有权执行该 action。
- 前置状态满足。
- `clientActionId` 未重复处理。

成功响应：

```json
{
  "code":0,
  "message":"OK",
  "clientActionId":"uid-000018",
  "revision":2,
  "duplicate":false,
  "changed":true
}
```

关卡操作错误码：

| code | 含义 |
| ---: | --- |
| 3001 | 当前账号不属于请求房间 |
| 3002 | 房间不在 `playing` 阶段 |
| 3003 | 请求关卡与房间关卡不一致 |
| 3004 | 角色无权执行该 action |
| 3005 | 前置状态不满足 |
| 3006 | 未知或当前里程碑未实现的 action |
| 3007 | 当前关卡尚未完成 |
| 3008 | 当前关卡没有下一关 |

### 6.4 连续换关

- 本关完成后，两端分别提交 `READY_NEXT_LEVEL`。
- 服务端不使用固定延时，只统计房间内两个账号的准备槽位。
- 两端均准备后，服务端执行一次 `revision + 1`，创建下一关初始状态。
- `LEVEL_CHANGED` 同时包含 `fromLevelId`、`toLevelId`、角色、revision
  和下一关完整快照。
- 客户端原子更新会话为 `LoadingLevel`，加载完成后应用快照并回到
  `Playing`。

### 6.5 房间快照

```json
{
  "roomId":"ABCD12",
  "phase":"playing",
  "levelId":2,
  "revision":4,
  "players":[
    {"uid":"guest_a","role":"outer"},
    {"uid":"guest_b","role":"inner"}
  ],
  "level1":null,
  "level2":{"keyFound":true,"keyLanded":true,"boxBuilt":false,"doorOpened":false},
  "level3":null
}
```

规则：

- 客户端 revision 初始为 `-1`。
- 只应用大于当前 revision 的快照。
- 有效变化后 `revision + 1`。
- 重复 action 不重复改变状态。
- 房间缓存最近 64 个 `clientActionId`。
- 不实现增量状态补偿。

## 7. OurDoor 关卡适配

### 7.1 接入规则

```text
真实交互
  → 在线：LevelXNetworkSync.RequestAction
  → 离线：直接调用 LevelXManager

服务端确认
  → LevelXNetworkSync.ApplySnapshot
  → LevelXManager / 表现接口
```

在线模式不得先修改 Manager 再发送。`ApplySnapshot` 必须幂等，重复快照不能重复播放一次性音效、动画或协程。

联网模式停用：

- `Level1AutoControl`
- `Level2AutoControl`
- `L3AutoControl`

### 7.2 第一关

| action | 角色 | 前置状态 | 当前交互入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `SET_POWER` | Outer | `playing` | `L1/ElectricBox/ElectricBox.cs` | `Level1Manager.SetPowerOn(value)` |
| `PASSWORD_FOUND` | Inner | `powerOn` | `L1/TV/TVScreen.cs` | `Level1Manager.SetPassWordFound()` |
| `LOCK_OPENED` | Outer | `passwordFound` | `CombinationPadLock/Script/PadLockPassword.cs` | `Level1Manager.SetLockOpened()` |

代码要求：

- `Level1Manager` 增加只读 `PasswordFound`。
- `SetPassWordFound` 改为幂等。
- `ElectricBox` 只在状态改变时提交 action。

### 7.3 第二关

| action | 角色 | 前置状态 | 当前交互入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `KEY_FOUND` | Inner | `playing` | `L2/Key/SchoolKey.cs` | `Level2Manager.SetKeyFound()` |
| `KEY_LANDED` | Inner | `keyFound` | `L2/Key/SchoolKey.cs` | `Level2Manager.SetKeyLandOnWall()` |
| `BOX_BUILT` | Outer | `keyLanded` | `L2/Box/BoxManager.cs` | `Level2Manager.SetBoxBuild()` |
| `DOOR_OPENED` | Outer | `keyLanded && boxBuilt` | `L2/Door/OpenSchoolDoorTrigger.cs` | 确定性开门 + `SetDoorOpen()` |

代码要求：

- `BoxBuild`、`DoorOpen` 提供只读状态。
- 将开门状态与开门表现拆开。
- 不同步钥匙轨迹和箱子搭建过程。

### 7.4 第三关

以下角色已按实际剧情和门缝递送行为确认：

| action | 角色 | 前置状态 | 当前交互入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `PASSWORD_SUCCESS` | Outer | `playing` | `L3/Knock/DoorKnockManager.cs` | `L3Manager.SetPasswordSuccess()` |
| `METAL_FOUND` | Outer | `passwordSuccess` | `L3/MetalPiece/MetalPiece.cs` | `L3Manager.SetMetalPieceFound()` |
| `METAL_RECEIVED` | Outer | `metalFound` | `OurDoorDoorGapOnlineHook.cs` | Inner 端激活门缝铁片 + `L3Manager.SetMetalPieceReceived()` |
| `WIRE_FOUND` | Inner | `metalReceived` | `L3/WireAndWall/WireExtractTrigger.cs` | `L3Manager.SetWireFound()` |
| `DOOR_OPENED` | Inner | `wireFound` | `L3/Door/HutongDoorTrigger.cs` | 确定性开门 + `SetDoorOpened()` |

代码要求：

- 为铁片最终位置、铁丝完成位置和门状态提供独立表现入口。
- 远端应用快照时不重新执行本地交互的完整延时协程。
- 不同步铁片和铁丝的刚体轨迹。

## 8. 退出与清理

触发来源：

- 客户端发送 `LEAVE_ROOM`。
- TCP 正常关闭。
- socket 异常。
- 登录后连续 30 秒无有效心跳。

清理顺序：

```text
agent 标记 closing
  → 从 match_service 移除
  → 从 room_service 移除
  → 向对端推送 PLAYER_LEFT
  → 销毁房间及关卡状态
  → 删除账号/房间索引
  → 释放临时账号在线占用
  → 关闭连接并退出 agent
```

清理接口必须幂等。一人离开即结束本局，对端保留登录和 TCP 连接，
清除房间态后返回 `Start_PC`，不等待重连。主动退出者释放临时账号并
关闭 TCP。

## 9. UnityMMO 参考项目

参考固定提交：[liuhaopen/UnityMMO@91890b5](https://github.com/liuhaopen/UnityMMO/tree/91890b5d40f37860e606660ac3bc0ddaaccdebb7)。

| 文件 | 参考内容 |
| --- | --- |
| [NetworkManager.cs](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Assets/XLuaFramework/Scripts/Manager/NetworkManager.cs) | TCP 连接、两字节大端包长、半包/粘包缓存、主线程回调思路 |
| [NetMsgDispatcher.cs](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Assets/Scripts/Net/NetMsgDispatcher.cs) | session 匹配响应与服务端推送分发 |
| [gated.lua](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Server/service/gated.lua) | 连接接入、agent 绑定和断线通知 |
| [msgagent.lua](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Server/service/msgagent.lua) | 单连接会话和协议分发 |
| [main.lua](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Server/service/main.lua) | Skynet 核心服务启动顺序 |

只参考：

- 客户端连接、消息分发和业务层分层。
- gate、agent、共享业务 service 的职责划分。
- session 响应与服务端 push 分流。
- TCP 半包/粘包和主线程消费思路。

不复制：

- XLua 客户端业务层。
- Sproto 和协议生成工具。
- `snax.loginserver`、`snax.msgserver` 的握手与消息格式。
- MySQL、world、scene 等 MMO 服务。
- 旧式 `BeginRead/BeginWrite` 实现。

OurDoor 使用纯 C# 客户端和自定义固定消息头，不能直接套用 UnityMMO 的协议与业务代码。

## 10. 里程碑

### M0：冻结规则与拆分接缝

开发：

- 确认三关角色分工，重点确认第三关。
- 只补齐快照需要读取但当前缺失的 Manager 状态，不改变原事件语义。
- 拆分“提交意图”和“应用结果”。
- 保持离线模式可用，联网模式停用 AutoControl。

验证：

- 离线模式三关仍可完成。
- 关键交互在离线模式仍调用原 Manager，在联网模式只提交操作意图。
- Manager、发送器或 action 配置错误时明确报错，不静默兜底。
- 快照 revision 去重在 M3 的快照应用层实现，不侵入原 Manager 逻辑。

### M1：通用 TCP 与协议

状态：已完成并通过双客户端运行验收。

开发：

- 完成 C#/Lua 帧编解码。
- 完成半包、粘包、唯一读循环、串行发送。
- 完成 session、请求超时、主线程队列和心跳。
- 完成最小 gate/agent 链路。

验证：

- 两个客户端同时连接。
- 每端连续 100 次心跳正确匹配响应。
- 半包、粘包、连续包、非法长度测试通过。

### M2：临时账号、房间与场景

状态：已完成并通过 Lua、Unity EditMode 与双客户端运行验收。

开发：

- 完成临时账号、创建/加入房间。
- 完成角色分配、`ROOM_READY` 和初始快照。
- 完成 OurDoor 角色及场景适配。
- 复用现有选关界面：离线模式保持原按钮行为，联网模式由按钮选择
  `levelId`，收到 `ROOM_READY` 后才加载关卡。

验证：

- 两个 PC Build 通过房间码进入同一关卡。
- `roomId/levelId/revision` 一致，角色互补。
- 错误房间码、第三人加入和未登录操作被拒绝。

### M3：第一关权威同步

状态：已完成并通过 Lua、Unity EditMode 与双客户端运行验收。

开发：

- 完成 `LEVEL_ACTION`、幂等、角色与前置状态校验。
- 接入第一关 3 个 action。
- 客户端串行发送操作；服务端确认后广播完整快照。
- 第一关快照应用层拒绝重复 revision、非法回退和缺失状态。
- 通用 `game_rule` 与 `games/ourdoor` 规则实现分离。

手动配置：

- `Server/Skynet/config` 设置
  `lxy_game_rule_module = "ourdoor.level_rules"`。
- `lua_path` 增加 `server_root .. "games/?.lua;"`。
- 常驻网络对象增加 `M3Level1DebugPanel`，并绑定
  `OurDoorOnlineController`。

验证：

- 合法流程可完成。
- 错误角色、越序和重复请求不改变状态。
- 两端 revision 和状态一致。
- 初始 revision 为 1；三个首次有效变化后依次为 2、3、4。

### M4：第二、三关权威同步

状态：已完成并通过双客户端完整游戏交互验收。

开发：

- 接入第二关 4 个、第三关 5 个 action。
- 补齐确定性结果表现。
- 铁片递送由 Outer 的门缝联网入口确认，Inner 端只呈现最终铁片。
- 完成连续关卡推进：选择第一关时按 `1→2→3`，选择第二关时按
  `2→3`，选择第三关时只运行第三关；每次换关仍由服务端确认并让
  两端进入相同场景。
- 换关采用双方显式准备，不使用固定秒数；验收面板可以在通关后手动
  提交准备以跳过剧情等待。

验证：

- 两关分别完整完成两轮。
- 不同步物理轨迹仍能正确呈现最终状态。
- 两端快照一致。

### M5：退出与清理（P1）

状态：已完成并通过 Lua、Unity EditMode 与双客户端运行验收。

开发：

- `LEAVE_ROOM=202`；`PLAYER_LEFT=902`。
- 客户端登录成功后立即发送心跳，之后每 10 秒发送一次。
- 服务端登录后连续 30 秒没有有效心跳时执行超时清理。
- 主动退出、TCP 正常关闭、socket 异常和心跳超时使用同一幂等清理入口。
- 一人离开即销毁房间、双方房间索引和关卡状态，只释放离开者临时账号。
- 对端只收到一次 `PLAYER_LEFT`，保留登录和连接并返回 `Start_PC`。
- 主动退出者收到响应后断开连接，会话进入 `Disconnected`。

验证：

- 在等待房间、加载和游玩阶段分别主动退出或关闭一端。
- 对端只收到一次通知。
- 对端进入 `Lobby`、返回 `Start_PC`，并可直接重新创建房间。
- 原房间码不可加入，退出者 guestId 可以重新登录。
- 服务端日志中的房间、索引和账号计数正确；双方最终退出后均为 0。
- 每个关闭连接对应的 agent 输出断开日志并退出。

### M6：匹配（P1）

状态：已完成并通过 Lua、Unity EditMode 与双客户端运行验收。

开发：

- 完成 `MATCH_REQUEST`、`MATCH_CANCEL`。
- 按 `levelId` FIFO 配对。
- 复用 M2 房间创建流程。

验证：

- 同关卡两人成功匹配。
- 不同关卡不匹配。
- 取消、重复请求和掉线不残留队列项。
- 匹配与房间码加入产生相同房间结构。

实现边界：

- 使用现有选关结果作为 `MATCH_REQUEST.levelId`。
- 先入队者为 `Outer`，后入队者为 `Inner`。
- 匹配队列、uid 索引和房间创建由服务端串行处理。
- 取消或连接清理同时删除 FIFO 项与 uid 索引。
- 不实现匹配评分、匹配超时、跨关卡匹配或正式账号。

### M7：最终联网界面、任意关卡与身份偏好

状态：代码完成，等待手动配置和双客户端验收。

开发：

- 新增基于 `OnGUI` 的最终运行时联网面板，不依赖 Canvas 或
  EventSystem。
- 游戏启动后自动连接服务端；M8 将端点来源收敛为严格部署配置，失败时显示
  配置路径、最终端点、异常原因和重试入口。
- 每次启动生成 4 位小写字母/数字临时 ID，不允许手工输入。
- Enter 打开/关闭主面板；打开时释放光标并停止本地第一人称控制，关闭时
  恢复，跨场景保持开关状态。
- 登录后先显示创建房间、加入房间、匹配三个一级入口，各自进入独立二级
  页面并支持返回。
- 创建页必须选择第 1/2/3 关和 `Outer/Inner`；加入页只输入房间码并
  明确提示关卡、身份由创建者选择；匹配页提供指定/任意关卡及三种身份
  偏好。
- 左下角独立显示最近一条重要错误或玩家退出通知；支持 `×` 和
  Backspace 关闭。
- 最终 UI 创建房间只允许 `Outer/Inner`；匹配同时按关卡模式、身份偏好
  和全局 FIFO 选择最早兼容玩家。

匹配规则：

- `levelId=0` 表示任意关卡；任意与指定相遇时使用指定关卡，两个任意相遇
  时使用第一关。
- 不同指定关卡不匹配。
- 固定 `Outer` 与 `Inner/Any` 兼容，固定 `Inner` 与 `Outer/Any`
  兼容；相同固定身份不匹配。
- 固定身份与 `Any` 相遇时尊重固定身份；两个 `Any` 相遇时先入队者为
  `Outer`。
- 房间创建者必须明确选择 `Outer` 或 `Inner`，房间码加入者取得相反
  身份。

手动配置：

- 在常驻网络对象添加 `OurDoorOnlineRuntimePanel` 并绑定
  `OurDoorOnlineController`。
- 禁用 M2、M3、M4、M5 调试面板组件；保留源码。
- 不修改场景内 Canvas、EventSystem 和原游戏交互物体。

验证：

- 主面板默认隐藏，Enter 开关、光标和玩家控制恢复正确，跨三关不丢失
  当前开关状态。
- 自动连接、随机临时 ID 登录、房间创建/加入和主动退出正常。
- 创建页未同时选择关卡和固定身份时不能提交；Outer、Inner 两种创建
  结果均得到正确的互补身份。
- 加入页不显示关卡和身份选择，只显示说明、房间码输入、加入与返回。
- 三个二级页面返回一级页面后，重新进入时恢复各页面初始状态。
- 覆盖指定+指定、任意+指定、任意+任意及不兼容身份的匹配组合。
- 通知不依赖主面板显示，新通知会重新出现并可关闭。

### M8：局域网一键部署与服务端自动管理

状态：代码完成，等待 Unity、打包和多机手动验收。

架构：

- 指定一台电脑 A 运行 WSL2、Ubuntu、Skynet 和可选 Unity 客户端。
- B/C 只运行同一完整包中的 Unity 客户端。
- 不做首台启动者自动成为服务器、主机迁移、自动接管和公网部署。
- 工具只保证电脑 A 不重复启动 OurDoor Skynet；局域网服务器选择由部署者
  统一管理。

客户端配置：

- RuntimePanel 启动时严格读取可执行文件同目录的
  `ourdoor-network.json`；Editor 对应项目根目录。
- 配置只允许 `host`、`port`，支持合法 IPv4 和 ASCII 主机名。
- 文件缺失、非法 UTF-8/JSON、字段重复/缺失/未知、空 Host 或非法 Port
  时停止自动连接，不回退到 Inspector 或 `127.0.0.1`。
- 错误通知和日志包含配置绝对路径、最终端点及连接异常。

主机安装：

- 一次管理员授权完成 Ubuntu/WSL2、Skynet v1.8.0、服务端文件检查。
- 多 Ubuntu、网卡或地址候选必须由部署者选择，不静默猜测。
- 生成局域网配置，创建 Private Profile TCP 8888 防火墙规则和 WSL
  portproxy。
- 注册启动、停止两个最高权限按需任务，禁止并行实例，并创建桌面快捷方式。
- 状态、PID 和任务结果保存到 `%ProgramData%\OurDoorHost`。

启动和停止：

- 每次启动取得默认路由对应的 WSL IPv4，刷新已知旧 portproxy。
- PID、Skynet 可执行文件和 config 参数一致时复用；失效 PID 清理；身份
  不匹配或端口被未知进程占用时明确失败。
- 等待局域网端点可访问后才允许启动游戏。
- 关闭 A 的游戏不停止服务端；显式停服只停止已验证的 OurDoor Skynet。
- 停服不执行 `wsl --shutdown`，并在等待期间持续校验 PID 启动时间，避免
  PID 复用后误杀其他进程。

打包：

- 用户手动生成 Unity Windows Build。
- `Deployment/Package-OurDoor.ps1` 把 Unity Build、HostTools、服务端
  Lua、四个双击入口和配置模板组装为统一包。
- 输出目录已存在时拒绝覆盖，不修改 Unity 场景、Build Settings 或
  PlayerSettings。

自动测试：

- `M8DeploymentConfigTests` 覆盖合法端点、缺失配置、空 Host、非法端口、
  非法 JSON、重复/缺失/未知字段及端点应用。
- `Test-OurDoorHost.ps1` 在不修改系统的情况下覆盖重复启动、失效 PID、
  精确停服、依赖缺失、端口冲突和禁止关闭整个 WSL。

手动验收：

- A 完成一次安装后可一键启动服务端及游戏，不打开 Ubuntu 窗口。
- B/C 直接启动游戏，无需输入地址，可完成登录、房间和匹配。
- A 关闭游戏后服务端继续；A 不运行游戏时 B/C 仍可游玩。
- 重复启动不产生第二份 Skynet；显式停服让客户端收到断线。
- A 重启后能清理失效状态、刷新 WSL IP 并再次一键启动。
- 最终无 session 不匹配、Agent 异常、未知端口占用或错误进程残留。

## 11. 最终验收

- [ ] Unity 客户端新增源码全部位于 `Assets/LXY`。
- [ ] Skynet 服务端源码全部位于 `Server/Skynet`。
- [ ] 不在 `Assets/XYT` 中新增联网文件。
- [ ] 通用层不引用 OurDoor 的 Manager、场景或交互类。
- [ ] 两个 PC 客户端连接同一 Skynet 服务端并完成临时登录。
- [ ] 创建/加入房间成功，角色分别为 Outer/Inner。
- [ ] 两端只在收到 `ROOM_READY` 后进入相同关卡。
- [ ] 第一关 3 个、第二关 4 个、第三关 5 个关键事件均由服务端确认。
- [ ] 错误角色、错误顺序、错误房间码和第三人加入被拒绝。
- [ ] 在线模式不运行 AutoControl，离线模式保持原流程。
- [ ] 三关各连续完成两轮，双方 revision 和状态一致。
- [ ] 一端退出后另一端收到一次通知，服务端无残留。
- [ ] 匹配支持按关卡配对、取消和断线清理。
- [ ] 任意关卡匹配按“指定优先、两个任意进入第一关”选择关卡。
- [ ] 创建房间强制选择 Outer/Inner；匹配正确执行三种身份偏好。
- [ ] 最终联网面板默认隐藏、Enter 开关、自动连接与独立通知均正常。
- [ ] 客户端只使用严格部署配置，不存在 Inspector/localhost 静默回退。
- [ ] A 可一键安装、启动、复用和精确停止 OurDoor Skynet。
- [ ] B/C 使用同一完整包直接连接，A 关闭游戏后服务端继续运行。
- [ ] 重启 A 后失效 PID、WSL IP 和 portproxy 能被正确处理。
- [ ] 复制通用客户端层与通用服务端层后，只替换适配层即可接入其他项目。

## 12. 后续待确认项

1. 正式局域网建议在路由器中为电脑 A 设置 DHCP 地址保留。
2. 如果 A 的局域网 IPv4 变化，重新运行主机安装生成配置，再把新的
   `ourdoor-network.json` 复制到 B/C。
3. 如果移动电脑 A 上的完整包目录，需要在新路径重新运行主机安装。

开发过程不会自动修改 Unity 场景、Prefab、Build Settings、PlayerSettings
或当前电脑系统配置。只有部署者主动运行 M8 安装/管理入口后，才会按文档
创建防火墙、portproxy、计划任务和快捷方式。
