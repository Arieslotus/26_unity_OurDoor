--- <summary>
--- 实现功能：验证 M7 全局 FIFO 的任意关卡、身份偏好、取消与索引清理。
--- </summary>
local source = debug.getinfo(1, "S").source
local script_path = source:sub(1, 1) == "@" and source:sub(2) or source
local server_root = script_path:match("^(.*[/\\])tests[/\\][^/\\]+$")
assert(server_root, "无法从测试脚本路径解析 Server/Skynet 根目录")

package.path = server_root .. "lualib/?.lua;" .. package.path

local match_queue = require "match_queue"

local function entry(uid, level_id, role_preference)
    return {
        uid = uid,
        agent = "agent-" .. uid,
        level_id = level_id,
        role_preference = role_preference,
    }
end

local function assert_error(action, message)
    local ok = pcall(action)
    assert(not ok, message)
end

local queue = match_queue.new()
local level_two_outer = entry("a", 2, "Outer")
local level_two_outer_again = entry("b", 2, "Outer")
local level_three_inner = entry("c", 3, "Inner")

local result = queue:request(level_two_outer)
assert(not result.matched, "第一个指定关卡玩家应进入等待")
result = queue:request(level_two_outer_again)
assert(not result.matched, "相同固定身份不能匹配")
result = queue:request(level_three_inner)
assert(not result.matched, "不同指定关卡不能匹配")
assert(queue:count() == 3, "此时应有三个不兼容玩家等待")

local any_level_inner = entry("d", 0, "Inner")
result = queue:request(any_level_inner)
assert(result.matched, "任意关卡 Inner 应匹配最早兼容的 Outer")
assert(result.first.uid == "a", "必须从全局队列选择最早兼容玩家")
assert(result.level_id == 2, "任意关卡应采用对方指定的第二关")
assert(result.first_role == "Outer", "固定 Outer 必须得到 Outer")
assert(result.second_role == "Inner", "固定 Inner 必须得到 Inner")

local any_level_any = entry("e", 0, "Any")
result = queue:request(any_level_any)
assert(result.matched, "任意身份应匹配当前最早兼容玩家")
assert(result.first.uid == "b", "不能越过更早且兼容的第二关玩家")
assert(result.level_id == 2, "任意关卡应采用对方指定关卡")
assert(result.first_role == "Outer", "固定 Outer 必须保持身份")
assert(result.second_role == "Inner", "Any 应取得相反身份")

local level_three_outer = entry("f", 3, "Outer")
result = queue:request(level_three_outer)
assert(result.matched, "第三关 Outer 应匹配已等待的第三关 Inner")
assert(result.first.uid == "c", "第三关匹配应取最早兼容玩家")
assert(result.first_role == "Inner", "先入队固定 Inner 必须保持身份")
assert(result.second_role == "Outer", "后入队固定 Outer 必须保持身份")

local both_any_first = entry("g", 0, "Any")
local both_any_second = entry("h", 0, "Any")
queue:request(both_any_first)
result = queue:request(both_any_second)
assert(result.matched, "两个任意玩家应完成匹配")
assert(result.level_id == 1, "两个任意关卡玩家必须进入第一关")
assert(result.first_role == "Outer", "两个任意身份时先入队玩家必须为 Outer")
assert(result.second_role == "Inner", "两个任意身份时后入队玩家必须为 Inner")

local cancel_entry = entry("cancel", 1, "Any")
queue:request(cancel_entry)
local removed = queue:cancel("cancel", cancel_entry.agent)
assert(removed == cancel_entry, "取消应返回被移除的原匹配项")
assert(queue:count() == 0, "取消后队列和 uid 索引均应清空")
assert(not queue:contains("cancel"), "取消后 uid 索引不应残留")

local duplicate = entry("duplicate", 3, "Any")
queue:request(duplicate)
assert_error(function()
    queue:request(entry("duplicate", 3, "Any"))
end, "重复 uid 请求必须报错")
assert_error(function()
    queue:cancel("duplicate", "wrong-agent")
end, "错误 agent 取消必须报错")
queue:cancel("duplicate", duplicate.agent)

assert_error(function()
    queue:request(entry("bad-level", 4, "Any"))
end, "非法关卡必须报错")
assert_error(function()
    queue:request(entry("bad-role", 1, "Unknown"))
end, "非法身份偏好必须报错")
assert(queue:count() == 0, "最终队列必须无残留")

print("LXY M7 role and any-level match queue tests passed")
