# LXY M4 Skynet 服务端

## M3 必需配置

本项目不自动修改服务端配置。启动 M3 前，手动修改 `Server/Skynet/config`：

```lua
lxy_game_rule_module = "ourdoor.level_rules"
```

并在 `lua_path` 中加入 `games`：

```lua
lua_path = server_root .. "lualib/?.lua;"
    .. server_root .. "games/?.lua;"
    .. skynet_root .. "lualib/?.lua;"
    .. skynet_root .. "lualib/?/init.lua"
```

缺少任一项时服务端会明确报错，不会退回硬编码规则。

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
~/skynet/3rd/lua/lua Server/Skynet/tests/test_level_rules.lua
~/skynet/3rd/lua/lua Server/Skynet/tests/test_m4_level_rules.lua
```

通过标志：

```text
LXY M1 Lua packet tests passed
LXY M2 Lua JSON tests passed
LXY M3 level rule tests passed
LXY M4 level rule tests passed
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

## M3 行为

- `LEVEL_ACTION=300`，请求必须携带房间、关卡、action、boolValue 和
  `clientActionId`。
- 第一关角色规则为：Outer 通电、Inner 发现密码、Outer 开锁。
- 服务端拒绝错误成员、错误关卡、错误角色和不满足前置条件的操作。
- 有效状态变化才执行 `revision + 1` 并向两端广播完整快照。
- 合法但没有状态变化的操作不增加 revision。
- 每个房间缓存最近 64 个 `clientActionId`；重复 ID 返回成功和
  `duplicate=true`，不再次改变状态。
- 通用房间服务通过 `game_rule` 调用 `games/ourdoor`，其他项目可替换
  `lxy_game_rule_module` 使用自己的规则模块。

## M4 行为

- 第二关 4 个、第三关 5 个 action 均由 `games/ourdoor` 校验。
- 第三关 `METAL_RECEIVED` 由 Outer 把铁片递入门缝时提交。
- `READY_NEXT_LEVEL=301` 只接受已经完成且不是第三关的房间。
- 每个账号只占一个准备槽位；只有两名成员均准备后才切换。
- 切换时继续使用同一房间和角色，revision 增加一次，重建下一关状态。
- `LEVEL_CHANGED=904` 在一个推送中携带目标关卡和完整初始快照，避免
  客户端先后收到关卡号与快照造成中间状态。
- 选择第一关按 `1→2→3`，选择第二关按 `2→3`，选择第三关不再换关。
