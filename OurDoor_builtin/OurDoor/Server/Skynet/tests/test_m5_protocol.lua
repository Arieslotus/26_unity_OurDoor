--- <summary>
--- 实现功能：验证 M5 主动退出和对端离开通知的跨端协议编号。
--- </summary>
local source = debug.getinfo(1, "S").source
local script_path = source:sub(1, 1) == "@" and source:sub(2) or source
local server_root = script_path:match("^(.*[/\\])tests[/\\][^/\\]+$")
assert(server_root, "无法从测试脚本路径解析 Server/Skynet 根目录")

package.path = server_root .. "lualib/?.lua;" .. package.path

local protocol = require "protocol"

assert(protocol.MESSAGE_ID.LEAVE_ROOM == 202, "LEAVE_ROOM 协议号必须为 202")
assert(protocol.MESSAGE_ID.PLAYER_LEFT == 902, "PLAYER_LEFT 协议号必须为 902")

print("LXY M5 lifecycle protocol tests passed")
