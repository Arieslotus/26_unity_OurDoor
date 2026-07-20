# LXY Day 1 Skynet Server

## Start

Build Skynet first, then run from the Unity project root:

```sh
SKYNET_ROOT=/absolute/path/to/skynet ./Assets/LXY/Server/run.sh
```

The server listens on `0.0.0.0:8888` and echoes valid `HEARTBEAT` JSON bodies using the request session.

To override the endpoint, add these values to `config`:

```lua
lxy_host = "0.0.0.0"
lxy_port = 8888
```

## Lua packet test

With a Lua 5.3+ interpreter:

```sh
lua Assets/LXY/Server/tests/test_packet.lua
```

## Unity verification

1. Start this server.
2. Add `Day1HeartbeatRunner` to an empty GameObject in any test scene.
3. Enter Play Mode.
4. Confirm the Console prints `PASS: 20/20 heartbeats succeeded`.
