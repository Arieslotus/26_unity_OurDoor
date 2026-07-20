# LXY 网络模块：Day 1

## 已实现

- C# TCP 异步连接、后台读取和串行发送。
- 两字节大端长度头及半包/粘包处理。
- `messageId + session + messageType + JSON` 编解码。
- Unity 主线程消息队列。
- session 请求/响应匹配和请求超时。
- Skynet HEARTBEAT 服务。
- C#、Lua 拆包测试和跨语言固定字节向量。

## 启动服务端

先准备并编译 Skynet，然后在 Unity 项目根目录执行：

```sh
SKYNET_ROOT=/Skynet的绝对路径 ./Assets/LXY/Server/run.sh
```

默认监听 `0.0.0.0:8888`。

## Unity 验收

1. 在任意测试场景创建空 GameObject。
2. 添加 `Day1HeartbeatRunner` 组件。
3. 保持 Host 为 `127.0.0.1`、Port 为 `8888`、Heartbeat Count 为 `20`。
4. 进入 Play Mode。
5. Console 出现 `PASS: 20/20 heartbeats succeeded` 即通过。

该组件会在运行时自动创建 `NetworkManager` 和 `MainThreadDispatcher`，无需修改现有场景或脚本。

## Unity 离线测试

在 Test Runner 的 EditMode 中运行 `LXY.Networking.EditModeTests`，覆盖：

- 单包。
- 包头和消息体分段到达。
- 多包粘连。
- 完整包加下一个半包。
- 非法长度。
- 大端消息头、协议往返和跨语言固定字节向量。
