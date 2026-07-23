# LXY M2 Skynet 服务端

## 启动

先编译 Skynet，再从 Unity 项目根目录执行：

```sh
SKYNET_ROOT=/Skynet的绝对路径 ./Server/Skynet/run.sh
```

默认监听 `0.0.0.0:8888`。如需修改监听地址，由用户手动在 `config`
中配置：

```lua
lxy_host = "0.0.0.0"
lxy_port = 8888
```

服务端启动后应出现：

```text
[M1 Gate] 正在监听 0.0.0.0:8888
[M2] main 启动完成
```

## Lua 测试

使用 Skynet 自带 Lua：

```sh
~/skynet/3rd/lua/lua Server/Skynet/tests/test_packet.lua
~/skynet/3rd/lua/lua Server/Skynet/tests/test_json.lua
```

通过标志：

```text
LXY M1 Lua packet tests passed
LXY M2 Lua JSON tests passed
```

## M2 行为

- 同一连接只能临时登录一次。
- 同一 guestId 同时只能被一个连接占用。
- 未登录连接不能创建或加入房间。
- 房间固定两人。
- 创建者为 `Outer`，加入者为 `Inner`。
- 第二人加入后，先向两端推送相同初始快照，再分别推送
  `ROOM_READY`。
- 错误房间码、第三人加入和非法 levelId 返回明确业务错误。

退出清理和账号释放属于 M5。M2 期间重新运行相同测试时应使用新的
guestId，或者重启 Skynet 服务端。
