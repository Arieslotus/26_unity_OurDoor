# 《OurDoor》Skynet 联网接入开发文档

> 开发周期：5 天  
> 范围：登录、双人房间、角色分配、三关关键事件同步、断线清理  
> PC 化由合作开发者负责；本文只定义其与网络层的接口。

## 1. 完成目标

最终必须完成：

1. 两个 Unity PC 客户端连接同一 Skynet 服务端。
2. 使用临时账号登录；创建房间或使用房间码加入。
3. 服务端分配 Outer、Inner，并让两端进入同一关卡。
4. 第一、二、三关的关键协作事件由服务端校验并同步。
5. 一端退出后，另一端收到通知；服务端清理账号和房间。

不做：数据库、正式账号、断线重连、位置/动作/物理连续同步、匹配、聊天、XLua、Sproto。

## 2. 技术方案

```text
PC/VR 交互
  → LevelNetworkSync 提交操作意图
  → NetworkManager
  → TCP：两字节大端长度头 + 固定消息头 + JSON
  → Skynet gate → agent → room_service
  → 校验角色和前置状态，更新 revision
  → 广播完整 RoomSnapshot
  → LevelNetworkSync 在 Unity 主线程应用快照
  → Level1Manager / Level2Manager / L3Manager
  → 现有关卡表现
```

固定决策：

| 项目 | 决策 |
| --- | --- |
| 客户端 | Unity 2022.3.62f3c1，纯 C# |
| 连接 | 单 TCP 长连接，登录和游戏共用一个端口 |
| 编码 | 固定二进制消息头 + UTF-8 JSON |
| JSON | Unity `JsonUtility`，使用明确 DTO，不使用字典 |
| 服务端 | Skynet，账号、房间、关卡状态全部保存在内存 |
| 房间 | 创建时选择关卡；第二人加入后自动开始 |
| 角色 | 创建者为 Outer，加入者为 Inner |
| 同步 | 只同步关键结果事件，不同步操作过程和物体轨迹 |
| 一致性 | 服务端权威；每次有效变化广播完整快照 |

## 3. UnityMMO 参考资料

参考仓库：[liuhaopen/UnityMMO](https://github.com/liuhaopen/UnityMMO/tree/91890b5d40f37860e606660ac3bc0ddaaccdebb7)

| 文件 | 参考内容 |
| --- | --- |
| [NetworkManager.cs](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Assets/XLuaFramework/Scripts/Manager/NetworkManager.cs) | TCP 连接、两字节大端包长、半包/粘包缓存、主线程回调思路 |
| [NetMsgDispatcher.cs](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Assets/Scripts/Net/NetMsgDispatcher.cs) | session 匹配响应、服务端推送分发 |
| [gated.lua](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Server/service/gated.lua) | 连接接入、agent 绑定、断线通知 |
| [msgagent.lua](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Server/service/msgagent.lua) | 单连接会话、协议分发到业务服务 |
| [main.lua](https://github.com/liuhaopen/UnityMMO/blob/91890b5d40f37860e606660ac3bc0ddaaccdebb7/Server/service/main.lua) | Skynet 核心服务启动顺序 |

只参考分层和职责，不复制：

- XLua 和 Lua 客户端业务层。
- Sproto、协议生成工具。
- `snax.loginserver`、`snax.msgserver` 的握手和消息格式。
- MySQL、world、scene 等 MMO 服务。
- 原项目的 `BeginRead/BeginWrite` 代码实现。

OurDoor 使用自定义固定消息头，因此不能直接套用 UnityMMO 的 `snax.msgserver`。

## 4. 客户端结构

```text
Assets/XYT/_Script/Network/
├── Core/
│   ├── NetworkClient.cs
│   ├── PacketFramer.cs
│   └── MainThreadDispatcher.cs
├── Protocol/
│   ├── MessageIds.cs
│   ├── NetworkEnvelope.cs
│   ├── ProtocolCodec.cs
│   └── ProtocolDtos.cs
├── NetworkConfig.cs
├── NetworkManager.cs
├── Level1NetworkSync.cs
├── Level2NetworkSync.cs
└── Level3NetworkSync.cs
```

职责：

- `NetworkClient`：异步连接、唯一读循环、串行发送、关闭。
- `PacketFramer`：两字节大端长度头，处理半包和粘包。
- `ProtocolCodec`：固定消息头和 JSON DTO 编解码。
- `MainThreadDispatcher`：将业务回调切到 Unity 主线程。
- `NetworkManager`：连接状态、session、请求超时、心跳、消息订阅。
- `LevelXNetworkSync`：提交关卡动作，按快照调用对应 LevelManager。

要求：

- `NetworkManager` 使用 `DontDestroyOnLoad`，全局唯一。
- 网络线程不得访问 GameObject、UI、SceneManager。
- 多个发送必须串行，不能交叉写入同一 NetworkStream。
- 断开时只清理一次，并取消全部 pending 请求。
- 关卡同步组件在场景卸载时取消订阅。

## 5. 服务端结构

```text
server/
├── config
├── service/
│   ├── main.lua
│   ├── gate_service.lua
│   ├── agent.lua
│   ├── lobby_service.lua
│   └── room_service.lua
├── lualib/
│   ├── protocol.lua
│   ├── packet.lua
│   └── error_code.lua
└── run.sh
```

- `gate_service`：监听、收包、连接与 agent 绑定。
- `agent`：登录状态、session 响应、消息权限、断线入口。
- `lobby_service`：房间码、创建、加入和索引。
- `room_service`：房间成员、角色、关卡状态、revision、动作校验和广播。

五天版本使用一个 `room_service` 管理全部房间，不为每个房间创建独立服务。

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
- request 的 session 非零；response 沿用请求 session；push 的 session 为 0。
- 非法长度、未知消息类型直接断开并记录日志。

### 6.2 消息 ID

| ID | 名称 | 说明 |
| ---: | --- | --- |
| 1 | `HEARTBEAT` | 心跳请求/响应 |
| 100 | `LOGIN` | 临时账号登录 |
| 200 | `CREATE_ROOM` | 携带 levelId 创建房间 |
| 201 | `JOIN_ROOM` | 使用 roomId 加入 |
| 300 | `LEVEL_ACTION` | 提交关卡动作 |
| 900 | `ROOM_SNAPSHOT` | 服务端推送完整房间状态 |
| 901 | `PLAYER_LEFT` | 玩家离开通知 |
| 999 | `SERVER_ERROR` | 无法归属普通响应的错误 |

请求失败使用相同 messageId/session 返回 `code != 0`。

### 6.3 核心消息

创建房间：

```json
{"levelId":2}
```

关卡动作：

```json
{
  "roomId":"ABCD12",
  "levelId":2,
  "action":"KEY_FOUND",
  "boolValue":true,
  "clientActionId":"player_b-18"
}
```

房间快照：

```json
{
  "roomId":"ABCD12",
  "ownerUid":"player_a",
  "phase":"playing",
  "levelId":2,
  "revision":4,
  "players":[
    {"uid":"player_a","role":"outer"},
    {"uid":"player_b","role":"inner"}
  ],
  "level1":null,
  "level2":{"keyFound":true,"keyLanded":true,"boxBuilt":false,"doorOpened":false},
  "level3":null
}
```

- 客户端 revision 初始值为 `-1`，忽略小于或等于当前 revision 的快照。
- 有效状态变化后 `revision + 1`。
- 房间缓存最近 64 个 `clientActionId`；重复请求不重复改变状态。
- 进入房间、动作成功、场景恢复都广播完整快照，不实现增量补偿协议。

## 7. 三关事件映射

### 7.1 接入规则

现有 `Level1Manager`、`Level2Manager`、`L3Manager` 作为“应用权威结果”的入口。真实交互不能在在线模式下直接修改 Manager：

```text
真实交互 → LevelXNetworkSync.RequestAction
服务器校验并广播 → LevelXNetworkSync.ApplySnapshot
→ LevelXManager.Set... → 本地表现
```

离线模式继续直接调用 Manager。在线模式禁用 `Level1AutoControl`、`Level2AutoControl`、`L3AutoControl`。

### 7.2 第一关

| action | 角色 | 前置状态 | 真实入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `SET_POWER` | Outer | playing | `L1/ElectricBox/ElectricBox.cs` | `Level1Manager.SetPowerOn(value)` |
| `PASSWORD_FOUND` | Inner | powerOn | `L1/TV/TVScreen.cs` | `Level1Manager.SetPassWordFound()` |
| `LOCK_OPENED` | Outer | passwordFound | `CombinationPadLock/Script/PadLockPassword.cs` | `Level1Manager.SetLockOpened()` |

`Level1Manager` 需要补充 `PasswordFound` 状态，避免密码事件只能触发、不能进入快照。

### 7.3 第二关

| action | 角色 | 前置状态 | 真实入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `KEY_FOUND` | Inner | playing | `L2/Key/SchoolKey.cs` | `Level2Manager.SetKeyFound()` |
| `KEY_LANDED` | Inner | keyFound | `L2/Key/SchoolKey.cs` | `Level2Manager.SetKeyLandOnWall()` |
| `BOX_BUILT` | Outer | keyLanded | `L2/Box/BoxManager.cs` | `Level2Manager.SetBoxBuild()` |
| `DOOR_OPENED` | Outer | keyLanded + boxBuilt | `L2/Door/OpenSchoolDoorTrigger.cs` | `SchoolDoorController` 权威开门表现 + `Level2Manager.SetDoorOpen()` |

`Level2Manager.BoxBuild`、`DoorOpen` 需要改为可读取的只读属性。钥匙飞行和箱子搭建过程不联网；远端按快照设置确定的完成状态。

### 7.4 第三关

| action | 角色 | 前置状态 | 真实入口 | 权威应用 |
| --- | --- | --- | --- | --- |
| `PASSWORD_SUCCESS` | Inner | playing | `L3/Knock/DoorKnockManager.cs` | `L3Manager.SetPasswordSuccess()` |
| `METAL_FOUND` | Inner | passwordSuccess | `L3/MetalPiece/MetalPiece.cs` | `L3Manager.SetMetalPieceFound()` |
| `METAL_RECEIVED` | Inner | metalFound | `L3/MetalPiece/InsideMetalPiece.cs` | `L3Manager.SetMetalPieceReceived()` |
| `WIRE_FOUND` | Outer | metalFound | `L3/WireAndWall/WireExtractTrigger.cs` | `L3Manager.SetWireFound()` |
| `DOOR_OPENED` | Outer | wireFound | `L3/Door/HutongDoorTrigger.cs` | `HutongDoorController` 权威开门表现 + `L3Manager.SetDoorOpened()` |

铁片传递、铁丝拔出和门动画只同步最终结果。远端使用确定位置和动画，不同步刚体轨迹。

### 7.5 PC 化协作接口

PC 化不在本文展开，但合作代码必须满足：

- 角色由 `NetworkManager` 写入 `GameManager` 后再加载关卡。
- PC 交互与原 VR 交互调用同一个 `LevelXNetworkSync.RequestAction`。
- PC 场景包含对应 `LevelXNetworkSync`，不自行实现 Socket。
- 场景名称和 `levelId` 映射固定：1=Shop、2=School、3=Hutong。
- 网络层不得依赖 PCPlayerRig 或具体输入设备。

## 8. 五天开发路径

### Day 1：TCP 与协议

- 建立客户端和服务端目录。
- 完成 C#/Lua 帧编解码、半包/粘包处理。
- 完成连接、主线程队列、串行发送、session 和请求超时。
- 验收：Unity 与 Skynet 连续 20 次 HEARTBEAT 请求/响应成功。

### Day 2：登录、房间、场景

- 实现临时账号、重复登录限制。
- 实现创建/加入房间、关卡选择、角色分配和完整快照。
- 收到 playing 快照后写入角色并加载对应 PC 场景。
- 验收：两客户端进入同一关卡，roomId、levelId、角色、revision 一致。

### Day 3：通用关卡同步与第一关

- 实现 `LEVEL_ACTION`、幂等、角色/前置条件校验。
- 实现三个 `LevelXNetworkSync` 的共用快照框架。
- 接入第一关三个动作，禁用在线 AutoControl。
- 验收：第一关供电、密码、开锁在两端一致；非法角色和顺序被拒绝。

### Day 4：第二、三关

- 接入第二关四个动作和确定性结果表现。
- 接入第三关五个动作和确定性结果表现。
- 修复 Manager 缺少可读状态的问题。
- 验收：第二、三关各从初始状态完整执行一次，两端 revision 和全部布尔状态一致。

### Day 5：断线与回归

- 实现主动退出、异常断线、房间销毁和 `PLAYER_LEFT`。
- 登录后每 10 秒心跳；30 秒无活动清理连接。
- 分别对三关执行两轮完整双端流程。
- 测试错误房间码、第三人加入、重复动作、错误角色、错误顺序、服务端关闭。
- 输出启动说明、端口、双客户端验收步骤。

Day 5 只修复问题，不新增协议或功能。

## 9. 验收标准

- [ ] 两个 PC 客户端能够登录、创建/加入房间并获得不同角色。
- [ ] 创建房间时可选择三关之一，两端加载同一场景。
- [ ] 第一关 3 个、第二关 4 个、第三关 5 个关键事件全部由 Skynet 确认并同步。
- [ ] 所有关卡动作校验房间、levelId、角色、前置状态和 `clientActionId`。
- [ ] 半包、粘包、非法长度、乱序响应和请求超时处理正确。
- [ ] 网络线程不访问 Unity 场景对象。
- [ ] 在线模式禁用 AutoControl，离线模式保持原逻辑。
- [ ] 一端退出后另一端收到通知，服务端释放账号和房间。
- [ ] 三关各连续完成两轮，双方快照状态和 revision 一致。
