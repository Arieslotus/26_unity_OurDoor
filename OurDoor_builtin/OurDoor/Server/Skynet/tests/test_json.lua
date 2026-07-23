--- <summary>
--- 实现功能：验证 M2 JSON 编解码、Unicode、数组及非法输入拒绝行为。
--- </summary>
local script_path = arg and arg[0] or ""
local server_root = string.match(script_path, "^(.*)/tests/[^/]+$") or "."
package.path = server_root .. "/lualib/?.lua;" .. package.path

local json = require "json"

local function assert_equal(actual, expected, message)
    if actual ~= expected then
        error(string.format(
            "%s: expected %s, got %s",
            message,
            tostring(expected),
            tostring(actual)
        ))
    end
end

local function assert_error(action, message)
    local ok = pcall(action)
    if ok then
        error(message .. ": expected an error")
    end
end

local source = {
    code = 0,
    message = "门外玩家",
    roomId = "R00001",
    levelId = 3,
    revision = 1,
    players = {
        { uid = "guest-a", role = "Outer" },
        { uid = "guest-b", role = "Inner" },
    },
}

local encoded = json.encode(source)
local decoded = json.decode(encoded)
assert_equal(decoded.code, 0, "code")
assert_equal(decoded.message, "门外玩家", "utf8 text")
assert_equal(decoded.roomId, "R00001", "room id")
assert_equal(decoded.levelId, 3, "level id")
assert_equal(decoded.players[1].role, "Outer", "outer role")
assert_equal(decoded.players[2].role, "Inner", "inner role")

local unicode = json.decode('{"value":"\\u95E8\\u5185"}')
assert_equal(unicode.value, "门内", "unicode escape")

assert_error(function()
    json.decode("")
end, "empty json")

assert_error(function()
    json.decode('{"roomId":"R00001"} trailing')
end, "trailing data")

assert_error(function()
    json.decode('{"value":"\\uD800"}')
end, "unpaired surrogate")

assert_error(function()
    json.decode('{"value":01}')
end, "leading zero")

assert_error(function()
    json.decode('{"value":"' .. string.char(0xC3, 0x28) .. '"}')
end, "invalid utf8")

local cycle = {}
cycle.self = cycle
assert_error(function()
    json.encode(cycle)
end, "cyclic table")

print("LXY M2 Lua JSON tests passed")
