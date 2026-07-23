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
│   │   └── LevelActionDtos.cs
│   ├── Session/
│   │   ├── NetworkSession.cs
│   │   └── OnlineSessionState.cs
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── RoomService.cs
│   │   └── LevelActionService.cs
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
│   │   └── OurDoorLevel1SnapshotSynchronizer.cs
│   └── Debug/
│       ├── M2LobbyDebugPanel.cs
│       └── M3Level1DebugPanel.cs
└── Tests/
    └── EditMode/
```

职责：

- `Network/Core`：连接、收发、半包/粘包和主线程队列。
- `Network/Protocol`：通用消息头、消息 ID、错误码和 DTO。
- `Network/Session`：登录与房间会话状态。
- `Network/Services`：临时登录、房间和关卡操作用例；M6 再增加匹配服务。
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
│   └── match_service.lua（M6）
├── lualib/
│   ├── protocol.lua
│   ├── packet.lua
│   ├── error_code.lua
│   ├── json.lua
│   └── game_rule.lua（M3）
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
| 900 | `ROOM_READY` | P0 | 角色和场景已确定 |
| 901 | `ROOM_SNAPSHOT` | P0 | 完整房间状态 |
| 902 | `PLAYER_LEFT` | P1 | 对方离开 |
| 903 | `MATCH_FOUND` | P1 | 匹配成功 |
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

### 6.4 房间快照

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

以下角色按当前 `L3AutoControl` 推导，实施前需要人工确认：

| action | 角色 | 前置状态 | 当前交互入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `PASSWORD_SUCCESS` | Outer | `playing` | `L3/Knock/DoorKnockManager.cs` | `L3Manager.SetPasswordSuccess()` |
| `METAL_FOUND` | Outer | `passwordSuccess` | `L3/MetalPiece/MetalPiece.cs` | `L3Manager.SetMetalPieceFound()` |
| `METAL_RECEIVED` | Inner | `metalFound` | `L3/MetalPiece/InsideMetalPiece.cs` | `L3Manager.SetMetalPieceReceived()` |
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

清理接口必须幂等。一人离开即结束本局，对端返回大厅，不等待重连。

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

状态：代码已完成，等待 Lua、Unity EditMode 与双客户端运行验收。

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

开发：

- 接入第二关 4 个、第三关 5 个 action。
- 补齐确定性结果表现。
- 完成连续关卡推进：选择第一关时按 `1→2→3`，选择第二关时按
  `2→3`，选择第三关时只运行第三关；每次换关仍由服务端确认并让
  两端进入相同场景。

验证：

- 两关分别完整完成两轮。
- 不同步物理轨迹仍能正确呈现最终状态。
- 两端快照一致。

### M5：退出与清理（P1）

开发：

- 统一主动退出、socket 异常和心跳超时清理。
- 完成 `PLAYER_LEFT`。

验证：

- 在等待房间、加载和游玩阶段分别关闭一端。
- 对端只收到一次通知。
- 服务端无账号、房间和 agent 残留。

### M6：匹配（P1）

开发：

- 完成 `MATCH_REQUEST`、`MATCH_CANCEL`。
- 按 `levelId` FIFO 配对。
- 复用 M2 房间创建流程。

验证：

- 同关卡两人成功匹配。
- 不同关卡不匹配。
- 取消、重复请求和掉线不残留队列项。
- 匹配与房间码加入产生相同房间结构。

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
- [ ] 复制通用客户端层与通用服务端层后，只替换适配层即可接入其他项目。

## 12. 后续待确认项

1. M5 对端退出后返回 `Start_PC`，还是停留当前场景显示结束面板。
2. 部署使用的 Skynet 地址、端口、防火墙和 Linux 启动方式。

以上配置不在代码中自动修改。
