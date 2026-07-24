--- <summary>
--- 实现功能：验证 M6 匹配消息及错误码与 Unity 客户端保持一致。
--- </summary>
local source = debug.getinfo(1, "S").source
local script_path = source:sub(1, 1) == "@" and source:sub(2) or source
local server_root = script_path:match("^(.*[/\\])tests[/\\][^/\\]+$")
assert(server_root, "无法从测试脚本路径解析 Server/Skynet 根目录")

package.path = server_root .. "lualib/?.lua;" .. package.path

local protocol = require "protocol"
local error_code = require "error_code"

assert(protocol.MESSAGE_ID.MATCH_REQUEST == 210, "MATCH_REQUEST 协议号必须为 210")
assert(protocol.MESSAGE_ID.MATCH_CANCEL == 211, "MATCH_CANCEL 协议号必须为 211")
assert(protocol.MESSAGE_ID.MATCH_FOUND == 903, "MATCH_FOUND 协议号必须为 903")
assert(error_code.ALREADY_MATCHING == 4001, "ALREADY_MATCHING 错误码必须为 4001")
assert(error_code.NOT_MATCHING == 4002, "NOT_MATCHING 错误码必须为 4002")
assert(
    error_code.INVALID_ROLE_PREFERENCE == 4003,
    "INVALID_ROLE_PREFERENCE 错误码必须为 4003"
)

print("LXY M6 match protocol tests passed")
