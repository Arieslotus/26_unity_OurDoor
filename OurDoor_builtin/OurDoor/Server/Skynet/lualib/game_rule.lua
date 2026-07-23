--- <summary>
--- 实现功能：定义可替换的游戏规则适配接口，并严格校验规则模块的输入输出。
--- </summary>
local game_rule = {}

local function require_function(adapter, function_name)
    local value = adapter[function_name]
    if type(value) ~= "function" then
        error(string.format(
            "[M3 GameRule] 规则模块缺少函数：%s",
            function_name
        ))
    end
    return value
end

function game_rule.new(adapter)
    if type(adapter) ~= "table" then
        error("[M3 GameRule] adapter 必须是 table")
    end

    local create_state = require_function(adapter, "create_state")
    local build_snapshot = require_function(adapter, "build_snapshot")
    local apply_action = require_function(adapter, "apply_action")
    local is_level_complete =
        require_function(adapter, "is_level_complete")
    local instance = {}

    function instance.create_state(level_id)
        local state = create_state(level_id)
        if type(state) ~= "table" then
            error(string.format(
                "[M3 GameRule] create_state 必须返回 table，levelId=%s",
                tostring(level_id)
            ))
        end
        return state
    end

    function instance.build_snapshot(level_id, state)
        local snapshot = build_snapshot(level_id, state)
        if type(snapshot) ~= "table" then
            error(string.format(
                "[M3 GameRule] build_snapshot 必须返回 table，levelId=%s",
                tostring(level_id)
            ))
        end
        return snapshot
    end

    function instance.apply_action(
        level_id,
        state,
        role,
        action,
        bool_value
    )
        local code, message, changed = apply_action(
            level_id,
            state,
            role,
            action,
            bool_value
        )
        if type(code) ~= "number"
            or type(message) ~= "string"
            or type(changed) ~= "boolean" then
            error(string.format(
                "[M3 GameRule] apply_action 返回值非法，"
                    .. "levelId=%s, action=%s, code=%s, messageType=%s, changed=%s",
                tostring(level_id),
                tostring(action),
                tostring(code),
                type(message),
                tostring(changed)
            ))
        end
        return code, message, changed
    end

    function instance.is_level_complete(level_id, state)
        local complete = is_level_complete(level_id, state)
        if type(complete) ~= "boolean" then
            error(string.format(
                "[M4 GameRule] is_level_complete 必须返回 boolean，levelId=%s",
                tostring(level_id)
            ))
        end
        return complete
    end

    return instance
end

return game_rule
