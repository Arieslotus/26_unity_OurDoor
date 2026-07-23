--- <summary>
--- 实现功能：验证 M3 第一关角色权限、前置条件、合法流程和状态幂等。
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

local function apply(state, role, action, bool_value)
    return rules.apply_action(1, state, role, action, bool_value)
end

local state = rules.create_state(1)
assert_equal(state.level1.powerOn, false, "initial power")
assert_equal(state.level1.passwordFound, false, "initial password")
assert_equal(state.level1.lockOpened, false, "initial lock")

local code, _, changed = apply(
    state,
    "Inner",
    "SET_POWER",
    true
)
assert_equal(code, error_code.ROLE_FORBIDDEN, "wrong role")
assert_equal(changed, false, "wrong role changed")
assert_equal(state.level1.powerOn, false, "wrong role state")

code, _, changed = apply(
    state,
    "Inner",
    "PASSWORD_FOUND",
    true
)
assert_equal(code, error_code.PRECONDITION_NOT_MET, "password out of order")
assert_equal(changed, false, "password out of order changed")

code, _, changed = apply(
    state,
    "Outer",
    "LOCK_OPENED",
    true
)
assert_equal(code, error_code.PRECONDITION_NOT_MET, "lock out of order")
assert_equal(changed, false, "lock out of order changed")

code, _, changed = apply(state, "Outer", "SET_POWER", true)
assert_equal(code, error_code.OK, "set power")
assert_equal(changed, true, "set power changed")
assert_equal(state.level1.powerOn, true, "power state")

code, _, changed = apply(state, "Outer", "SET_POWER", true)
assert_equal(code, error_code.OK, "repeat power")
assert_equal(changed, false, "repeat power changed")

code, _, changed = apply(
    state,
    "Inner",
    "PASSWORD_FOUND",
    true
)
assert_equal(code, error_code.OK, "password found")
assert_equal(changed, true, "password changed")

code, _, changed = apply(
    state,
    "Outer",
    "LOCK_OPENED",
    true
)
assert_equal(code, error_code.OK, "lock opened")
assert_equal(changed, true, "lock changed")

code, _, changed = apply(state, "Outer", "SET_POWER", false)
assert_equal(code, error_code.OK, "power off after completion")
assert_equal(changed, true, "power off changed")
assert_equal(state.level1.passwordFound, true, "password remains irreversible")
assert_equal(state.level1.lockOpened, true, "lock remains irreversible")

code, _, changed = apply(
    state,
    "Inner",
    "PASSWORD_FOUND",
    true
)
assert_equal(code, error_code.OK, "repeat irreversible action")
assert_equal(changed, false, "repeat irreversible action changed")

local snapshot = rules.build_snapshot(1, state)
assert_equal(snapshot.level1.powerOn, false, "snapshot power")
assert_equal(snapshot.level1.passwordFound, true, "snapshot password")
assert_equal(snapshot.level1.lockOpened, true, "snapshot lock")

print("LXY M3 level rule tests passed")
