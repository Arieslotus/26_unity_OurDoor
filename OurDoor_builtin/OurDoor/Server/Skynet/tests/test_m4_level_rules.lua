--- <summary>
--- 实现功能：验证 M4 第二、三关角色权限、前置条件、合法流程和完成判断。
--- </summary>
local script_path = arg and arg[0] or ""
local server_root = string.match(script_path, "^(.*)/tests/[^/]+$") or "."
package.path = server_root .. "/lualib/?.lua;"
    .. server_root .. "/games/?.lua;"
    .. package.path

local error_code = require "error_code"
local game_rule = require "game_rule"
local rules = game_rule.new(require "ourdoor.level_rules")

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

local function apply(level_id, state, role, action)
    return rules.apply_action(level_id, state, role, action, true)
end

local level2 = rules.create_state(2)
local code, _, changed = apply(2, level2, "Outer", "KEY_FOUND")
assert_equal(code, error_code.ROLE_FORBIDDEN, "L2 wrong role")
assert_equal(changed, false, "L2 wrong role changed")

code, _, changed = apply(2, level2, "Inner", "KEY_LANDED")
assert_equal(code, error_code.PRECONDITION_NOT_MET, "L2 out of order")
assert_equal(changed, false, "L2 out of order changed")

code, _, changed = apply(2, level2, "Inner", "KEY_FOUND")
assert_equal(code, error_code.OK, "L2 key found")
assert_equal(changed, true, "L2 key found changed")
code, _, changed = apply(2, level2, "Inner", "KEY_LANDED")
assert_equal(code, error_code.OK, "L2 key landed")
code, _, changed = apply(2, level2, "Outer", "BOX_BUILT")
assert_equal(code, error_code.OK, "L2 box built")
code, _, changed = apply(2, level2, "Outer", "DOOR_OPENED")
assert_equal(code, error_code.OK, "L2 door opened")
assert_equal(rules.is_level_complete(2, level2), true, "L2 complete")

local level3 = rules.create_state(3)
code, _, changed = apply(3, level3, "Inner", "PASSWORD_SUCCESS")
assert_equal(code, error_code.ROLE_FORBIDDEN, "L3 wrong role")
code, _, changed = apply(3, level3, "Outer", "METAL_FOUND")
assert_equal(code, error_code.PRECONDITION_NOT_MET, "L3 out of order")

code, _, changed = apply(3, level3, "Outer", "PASSWORD_SUCCESS")
assert_equal(code, error_code.OK, "L3 password")
code, _, changed = apply(3, level3, "Outer", "METAL_FOUND")
assert_equal(code, error_code.OK, "L3 metal found")
code, _, changed = apply(3, level3, "Inner", "METAL_RECEIVED")
assert_equal(code, error_code.ROLE_FORBIDDEN, "L3 metal received wrong role")
code, _, changed = apply(3, level3, "Outer", "METAL_RECEIVED")
assert_equal(code, error_code.OK, "L3 metal received")
code, _, changed = apply(3, level3, "Inner", "WIRE_FOUND")
assert_equal(code, error_code.OK, "L3 wire found")
code, _, changed = apply(3, level3, "Inner", "DOOR_OPENED")
assert_equal(code, error_code.OK, "L3 door opened")
assert_equal(rules.is_level_complete(3, level3), true, "L3 complete")

local snapshot2 = rules.build_snapshot(2, level2)
assert_equal(snapshot2.level2.doorOpened, true, "L2 snapshot")
local snapshot3 = rules.build_snapshot(3, level3)
assert_equal(snapshot3.level3.metalReceived, true, "L3 snapshot")

print("LXY M4 level rule tests passed")
