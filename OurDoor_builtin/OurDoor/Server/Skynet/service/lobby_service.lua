--- <summary>
--- 实现功能：提供匹配、房间、关卡操作及连接退出的通用大厅入口。
--- </summary>
local skynet = require "skynet"

local room_service
local match_service
local command = {}

function command.CREATE_ROOM(account, agent, level_id)
    return skynet.call(
        room_service,
        "lua",
        "CREATE_ROOM",
        account,
        agent,
        level_id
    )
end

function command.JOIN_ROOM(account, agent, room_id)
    return skynet.call(
        room_service,
        "lua",
        "JOIN_ROOM",
        account,
        agent,
        room_id
    )
end

function command.MATCH_REQUEST(account, agent, level_id)
    return skynet.call(
        match_service,
        "lua",
        "REQUEST",
        account,
        agent,
        level_id
    )
end

function command.MATCH_CANCEL(account, agent)
    return skynet.call(
        match_service,
        "lua",
        "CANCEL",
        account,
        agent
    )
end

function command.LEAVE_CONNECTION(account, agent, reason)
    local match_ok, match_result = pcall(
        skynet.call,
        match_service,
        "lua",
        "LEAVE_CONNECTION",
        account,
        agent,
        reason
    )
    local room_ok, room_result = pcall(
        skynet.call,
        room_service,
        "lua",
        "LEAVE_CONNECTION",
        account,
        agent,
        reason
    )
    if not match_ok or not room_ok then
        error(string.format(
            "[M6 Lobby] 连接清理不完整，match=%s, room=%s",
            match_ok and "OK" or tostring(match_result),
            room_ok and "OK" or tostring(room_result)
        ))
    end

    room_result.matchRemoved = match_result.removed
    room_result.remainingMatches = match_result.remainingMatches
    return room_result
end

function command.SUBMIT_LEVEL_ACTION(
    account,
    agent,
    room_id,
    level_id,
    action,
    bool_value,
    client_action_id
)
    return skynet.call(
        room_service,
        "lua",
        "SUBMIT_LEVEL_ACTION",
        account,
        agent,
        room_id,
        level_id,
        action,
        bool_value,
        client_action_id
    )
end

function command.READY_NEXT_LEVEL(account, agent, room_id, level_id)
    return skynet.call(
        room_service,
        "lua",
        "READY_NEXT_LEVEL",
        account,
        agent,
        room_id,
        level_id
    )
end

skynet.start(function()
    room_service = skynet.uniqueservice("room_service")
    match_service = skynet.uniqueservice("match_service")
    skynet.dispatch("lua", function(session, _, command_name, ...)
        local handler = command[command_name]
        if not handler then
            error(string.format("[M2 Lobby] 未知命令：%s", tostring(command_name)))
        end
        skynet.retpack(handler(...))
    end)
end)
