--- <summary>
--- 实现功能：验证 M6 匹配队列的同关卡 FIFO、跨关卡隔离、取消和索引清理。
--- </summary>
local source = debug.getinfo(1, "S").source
local script_path = source:sub(1, 1) == "@" and source:sub(2) or source
local server_root = script_path:match("^(.*[/\\])tests[/\\][^/\\]+$")
assert(server_root, "无法从测试脚本路径解析 Server/Skynet 根目录")

package.path = server_root .. "lualib/?.lua;" .. package.path

local match_queue = require "match_queue"

local function entry(uid, level_id)
    return {
        uid = uid,
        agent = "agent-" .. uid,
        level_id = level_id,
    }
end

local function assert_error(action, message)
    local ok = pcall(action)
    assert(not ok, message)
end

local queue = match_queue.new()
local first_level_one = entry("a", 1)
local first_level_two = entry("b", 2)
local second_level_one = entry("c", 1)

local result = queue:request(first_level_one)
assert(not result.matched, "第一个 Level 1 玩家应进入等待")
result = queue:request(first_level_two)
assert(not result.matched, "Level 2 玩家不能与 Level 1 玩家匹配")
assert(queue:count(1) == 1, "Level 1 等待人数应为 1")
assert(queue:count(2) == 1, "Level 2 等待人数应为 1")

result = queue:request(second_level_one)
assert(result.matched, "第二个 Level 1 玩家应完成匹配")
assert(result.first.uid == "a", "同关卡必须按 FIFO 取出最早玩家")
assert(result.second.uid == "c", "后请求玩家应成为第二名玩家")
assert(queue:count(1) == 0, "配对后 Level 1 队列应为空")
assert(queue:count() == 1, "此时只应剩余 Level 2 玩家")

local removed = queue:cancel("b", first_level_two.agent)
assert(removed == first_level_two, "取消应返回被移除的原匹配项")
assert(queue:count() == 0, "取消后队列和 uid 索引均应清空")
assert(not queue:contains("b"), "取消后 uid 索引不应残留")
assert(queue:cancel("missing", "agent-missing") == nil, "未入队账号取消应返回 nil")

local duplicate = entry("duplicate", 3)
queue:request(duplicate)
assert_error(function()
    queue:request(entry("duplicate", 3))
end, "重复 uid 请求必须报错")
assert_error(function()
    queue:cancel("duplicate", "wrong-agent")
end, "错误 agent 取消必须报错")
queue:cancel("duplicate", duplicate.agent)
assert(queue:count() == 0, "最终队列必须无残留")

print("LXY M6 FIFO match queue tests passed")
