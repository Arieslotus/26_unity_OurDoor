--- <summary>
--- 实现功能：提供房间及关卡操作的通用大厅入口，并统一转发给房间服务。
--- </summary>
local skynet = require "skynet"

local room_service
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
    skynet.dispatch("lua", function(session, _, command_name, ...)
        local handler = command[command_name]
        if not handler then
            error(string.format("[M2 Lobby] 未知命令：%s", tostring(command_name)))
        end
        skynet.retpack(handler(...))
    end)
end)
