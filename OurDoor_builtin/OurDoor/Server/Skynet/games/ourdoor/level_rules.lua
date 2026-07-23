--- <summary>
--- 实现功能：校验 OurDoor 三关角色、前置条件和状态变化，并判断当前关卡是否完成。
--- </summary>
local error_code = require "error_code"
local room_state = require "ourdoor.room_state"

local level_rules = {}

local function rejected(code, message)
    return code, message, false
end

local function require_role(actual_role, expected_role, action)
    if actual_role == expected_role then
        return nil
    end
    return string.format(
        "角色无权执行操作，action=%s, required=%s, actual=%s",
        action,
        expected_role,
        tostring(actual_role)
    )
end

local function require_true_value(bool_value, action)
    if bool_value == true then
        return nil
    end
    return string.format(
        "操作只接受 boolValue=true，action=%s, actual=%s",
        action,
        tostring(bool_value)
    )
end

local function validate_irreversible_action(
    role,
    expected_role,
    action,
    bool_value
)
    local role_error = require_role(role, expected_role, action)
    if role_error then
        return error_code.ROLE_FORBIDDEN, role_error
    end
    local value_error = require_true_value(bool_value, action)
    if value_error then
        return error_code.INVALID_REQUEST, value_error
    end
    return error_code.OK, "OK"
end

local function apply_level1(state, role, action, bool_value)
    local level1 = state.level1
    if type(level1) ~= "table" then
        error("[M4 LevelRule] 第一关服务端状态缺失")
    end

    if action == "SET_POWER" then
        local role_error = require_role(role, "Outer", action)
        if role_error then
            return rejected(error_code.ROLE_FORBIDDEN, role_error)
        end
        if level1.powerOn == bool_value then
            return error_code.OK, "OK", false
        end
        level1.powerOn = bool_value
        return error_code.OK, "OK", true
    end

    if action == "PASSWORD_FOUND" then
        local code, message = validate_irreversible_action(
            role,
            "Inner",
            action,
            bool_value
        )
        if code ~= error_code.OK then
            return rejected(code, message)
        end
        if level1.passwordFound then
            return error_code.OK, "OK", false
        end
        if not level1.powerOn then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "PASSWORD_FOUND 要求 powerOn=true"
            )
        end
        level1.passwordFound = true
        return error_code.OK, "OK", true
    end

    if action == "LOCK_OPENED" then
        local code, message = validate_irreversible_action(
            role,
            "Outer",
            action,
            bool_value
        )
        if code ~= error_code.OK then
            return rejected(code, message)
        end
        if level1.lockOpened then
            return error_code.OK, "OK", false
        end
        if not level1.passwordFound then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "LOCK_OPENED 要求 passwordFound=true"
            )
        end
        level1.lockOpened = true
        return error_code.OK, "OK", true
    end

    return rejected(
        error_code.INVALID_ACTION,
        string.format("第一关未知操作：%s", action)
    )
end

local function apply_level2(state, role, action, bool_value)
    local level2 = state.level2
    if type(level2) ~= "table" then
        error("[M4 LevelRule] 第二关服务端状态缺失")
    end

    local expected_role
    if action == "KEY_FOUND" or action == "KEY_LANDED" then
        expected_role = "Inner"
    elseif action == "BOX_BUILT" or action == "DOOR_OPENED" then
        expected_role = "Outer"
    else
        return rejected(
            error_code.INVALID_ACTION,
            string.format("第二关未知操作：%s", action)
        )
    end

    local code, message = validate_irreversible_action(
        role,
        expected_role,
        action,
        bool_value
    )
    if code ~= error_code.OK then
        return rejected(code, message)
    end

    if action == "KEY_FOUND" then
        if level2.keyFound then
            return error_code.OK, "OK", false
        end
        level2.keyFound = true
    elseif action == "KEY_LANDED" then
        if level2.keyLanded then
            return error_code.OK, "OK", false
        end
        if not level2.keyFound then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "KEY_LANDED 要求 keyFound=true"
            )
        end
        level2.keyLanded = true
    elseif action == "BOX_BUILT" then
        if level2.boxBuilt then
            return error_code.OK, "OK", false
        end
        if not level2.keyLanded then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "BOX_BUILT 要求 keyLanded=true"
            )
        end
        level2.boxBuilt = true
    elseif action == "DOOR_OPENED" then
        if level2.doorOpened then
            return error_code.OK, "OK", false
        end
        if not level2.keyLanded or not level2.boxBuilt then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "第二关 DOOR_OPENED 要求 keyLanded=true 且 boxBuilt=true"
            )
        end
        level2.doorOpened = true
    end
    return error_code.OK, "OK", true
end

local function apply_level3(state, role, action, bool_value)
    local level3 = state.level3
    if type(level3) ~= "table" then
        error("[M4 LevelRule] 第三关服务端状态缺失")
    end

    local expected_role
    if action == "PASSWORD_SUCCESS"
        or action == "METAL_FOUND"
        or action == "METAL_RECEIVED" then
        expected_role = "Outer"
    elseif action == "WIRE_FOUND" or action == "DOOR_OPENED" then
        expected_role = "Inner"
    else
        return rejected(
            error_code.INVALID_ACTION,
            string.format("第三关未知操作：%s", action)
        )
    end

    local code, message = validate_irreversible_action(
        role,
        expected_role,
        action,
        bool_value
    )
    if code ~= error_code.OK then
        return rejected(code, message)
    end

    if action == "PASSWORD_SUCCESS" then
        if level3.passwordSuccess then
            return error_code.OK, "OK", false
        end
        level3.passwordSuccess = true
    elseif action == "METAL_FOUND" then
        if level3.metalFound then
            return error_code.OK, "OK", false
        end
        if not level3.passwordSuccess then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "METAL_FOUND 要求 passwordSuccess=true"
            )
        end
        level3.metalFound = true
    elseif action == "METAL_RECEIVED" then
        if level3.metalReceived then
            return error_code.OK, "OK", false
        end
        if not level3.metalFound then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "METAL_RECEIVED 要求 metalFound=true"
            )
        end
        level3.metalReceived = true
    elseif action == "WIRE_FOUND" then
        if level3.wireFound then
            return error_code.OK, "OK", false
        end
        if not level3.metalReceived then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "WIRE_FOUND 要求 metalReceived=true"
            )
        end
        level3.wireFound = true
    elseif action == "DOOR_OPENED" then
        if level3.doorOpened then
            return error_code.OK, "OK", false
        end
        if not level3.wireFound then
            return rejected(
                error_code.PRECONDITION_NOT_MET,
                "第三关 DOOR_OPENED 要求 wireFound=true"
            )
        end
        level3.doorOpened = true
    end
    return error_code.OK, "OK", true
end

function level_rules.create_state(level_id)
    return room_state.create(level_id)
end

function level_rules.build_snapshot(level_id, state)
    return room_state.build_snapshot(level_id, state)
end

function level_rules.apply_action(
    level_id,
    state,
    role,
    action,
    bool_value
)
    if type(action) ~= "string" or action == "" then
        return rejected(error_code.INVALID_ACTION, "action 不能为空")
    end
    if type(bool_value) ~= "boolean" then
        return rejected(
            error_code.INVALID_REQUEST,
            string.format(
                "boolValue 必须是 boolean，action=%s, actualType=%s",
                action,
                type(bool_value)
            )
        )
    end

    if level_id == 1 then
        return apply_level1(state, role, action, bool_value)
    elseif level_id == 2 then
        return apply_level2(state, role, action, bool_value)
    elseif level_id == 3 then
        return apply_level3(state, role, action, bool_value)
    end
    return rejected(
        error_code.INVALID_ACTION,
        string.format("未知关卡：%s", tostring(level_id))
    )
end

function level_rules.is_level_complete(level_id, state)
    if level_id == 1 then
        return type(state.level1) == "table"
            and state.level1.lockOpened == true
    elseif level_id == 2 then
        return type(state.level2) == "table"
            and state.level2.doorOpened == true
    elseif level_id == 3 then
        return type(state.level3) == "table"
            and state.level3.doorOpened == true
    end
    error(string.format(
        "[M4 LevelRule] 无法判断未知关卡完成状态：%s",
        tostring(level_id)
    ))
end

return level_rules
