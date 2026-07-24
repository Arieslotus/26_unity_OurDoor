--- <summary>
--- 实现功能：登记与释放内存临时账号，并拒绝同一 guestId 被多个连接占用。
--- </summary>
local skynet = require "skynet"
local error_code = require "error_code"

local accounts_by_guest = {}

local function account_count()
    local count = 0
    for _ in pairs(accounts_by_guest) do
        count = count + 1
    end
    return count
end

local function validate_text(value, field_name, maximum_length)
    if type(value) ~= "string" or value:match("^%s*$") then
        return false, field_name .. " 不能为空"
    end
    local character_count = utf8.len(value)
    if not character_count then
        return false, field_name .. " 不是有效 UTF-8"
    end
    if character_count > maximum_length then
        return false, string.format(
            "%s 长度不能超过 %d，当前=%d",
            field_name,
            maximum_length,
            character_count
        )
    end
    if value:find("[%z\1-\31]") then
        return false, field_name .. " 不能包含控制字符"
    end
    return true
end

local command = {}

function command.LOGIN(agent, guest_id, display_name, client_version)
    if type(agent) ~= "number" then
        return error_code.INVALID_REQUEST, "agent 必须是有效服务地址"
    end
    local valid, reason = validate_text(guest_id, "guestId", 64)
    if not valid then
        return error_code.INVALID_REQUEST, reason
    end
    if not guest_id:match("^[%w_-]+$") then
        return error_code.INVALID_REQUEST,
            "guestId 只能包含字母、数字、下划线和连字符"
    end
    valid, reason = validate_text(display_name, "displayName", 32)
    if not valid then
        return error_code.INVALID_REQUEST, reason
    end
    valid, reason = validate_text(client_version, "clientVersion", 32)
    if not valid then
        return error_code.INVALID_REQUEST, reason
    end

    local occupied = accounts_by_guest[guest_id]
    if occupied then
        return error_code.GUEST_ALREADY_ONLINE,
            string.format("临时账号已在线，guestId=%s", guest_id)
    end

    local account = {
        uid = guest_id,
        guest_id = guest_id,
        display_name = display_name,
        client_version = client_version,
        agent = agent,
    }
    accounts_by_guest[guest_id] = account

    skynet.error(string.format(
        "[M2 Account] 临时账号登录成功，uid=%s, displayName=%s, agent=%s",
        account.uid,
        account.display_name,
        skynet.address(agent)
    ))
    return error_code.OK, "OK", account
end

function command.LOGOUT(agent, guest_id)
    if type(agent) ~= "number" then
        error("[M5 Account] LOGOUT 的 agent 必须是有效服务地址")
    end
    if type(guest_id) ~= "string" or guest_id == "" then
        error("[M5 Account] LOGOUT 的 guestId 不能为空")
    end

    local occupied = accounts_by_guest[guest_id]
    if not occupied then
        skynet.error(string.format(
            "[M5 Account] 临时账号已释放，重复清理不再修改状态，guestId=%s, agent=%s",
            guest_id,
            skynet.address(agent)
        ))
        return false, account_count()
    end
    if occupied.agent ~= agent then
        error(string.format(
            "[M5 Account] 释放账号的 agent 不匹配，guestId=%s, expected=%s, actual=%s",
            guest_id,
            skynet.address(occupied.agent),
            skynet.address(agent)
        ))
    end

    accounts_by_guest[guest_id] = nil
    local remaining_count = account_count()
    skynet.error(string.format(
        "[M5 Account] 临时账号在线占用已释放，guestId=%s, remainingAccounts=%d",
        guest_id,
        remaining_count
    ))
    return true, remaining_count
end

skynet.start(function()
    skynet.dispatch("lua", function(session, _, command_name, ...)
        local handler = command[command_name]
        if not handler then
            error(string.format("[M2 Account] 未知命令：%s", tostring(command_name)))
        end
        skynet.retpack(handler(...))
    end)
end)
