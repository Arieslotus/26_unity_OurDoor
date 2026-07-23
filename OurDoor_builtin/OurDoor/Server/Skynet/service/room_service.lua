--- <summary>
--- 实现功能：管理双人房间、房间码、角色槽位、初始快照与 ROOM_READY 广播。
--- </summary>
local skynet = require "skynet"
local error_code = require "error_code"
local protocol = require "protocol"

local rooms = {}
local room_by_uid = {}
local next_room_number = 0

local function response(code, message, room, role)
    local result = {
        code = code,
        message = message,
    }
    if room then
        result.roomId = room.room_id
        result.levelId = room.level_id
        result.role = role
        result.revision = room.revision
    end
    return result
end

local function next_room_id()
    for _ = 1, 99999 do
        next_room_number = next_room_number % 99999 + 1
        local room_id = string.format("R%05d", next_room_number)
        if not rooms[room_id] then
            return room_id
        end
    end
    error("[M2 Room] 房间码空间已耗尽")
end

local function player_snapshot(player)
    return {
        uid = player.uid,
        displayName = player.display_name,
        role = player.role,
    }
end

local function build_snapshot(room)
    local players = {}
    for index, player in ipairs(room.players) do
        players[index] = player_snapshot(player)
    end
    return {
        roomId = room.room_id,
        phase = room.phase,
        levelId = room.level_id,
        revision = room.revision,
        players = players,
    }
end

local function push_room_ready(room)
    local snapshot = build_snapshot(room)
    for _, player in ipairs(room.players) do
        skynet.send(
            player.agent,
            "lua",
            "push",
            protocol.MESSAGE_ID.ROOM_SNAPSHOT,
            snapshot
        )
        skynet.send(
            player.agent,
            "lua",
            "push",
            protocol.MESSAGE_ID.ROOM_READY,
            {
                roomId = room.room_id,
                levelId = room.level_id,
                role = player.role,
                revision = room.revision,
            }
        )
    end
end

local function validate_account(account, agent)
    if type(account) ~= "table"
        or type(account.uid) ~= "string"
        or account.uid == ""
        or account.agent ~= agent then
        return false
    end
    return true
end

local command = {}

function command.CREATE_ROOM(account, agent, level_id)
    if not validate_account(account, agent) then
        return response(error_code.NOT_LOGGED_IN, "账号或连接身份无效")
    end
    if room_by_uid[account.uid] then
        return response(
            error_code.ALREADY_IN_ROOM,
            string.format("账号已在房间中，uid=%s", account.uid)
        )
    end
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id < 1
        or level_id > 3 then
        return response(
            error_code.INVALID_LEVEL,
            string.format("levelId 必须是 1、2、3，当前=%s", tostring(level_id))
        )
    end

    local room_id = next_room_id()
    local room = {
        room_id = room_id,
        level_id = level_id,
        phase = "waiting",
        revision = 0,
        players = {
            {
                uid = account.uid,
                display_name = account.display_name,
                role = "Outer",
                agent = agent,
            },
        },
    }
    rooms[room_id] = room
    room_by_uid[account.uid] = room_id

    skynet.error(string.format(
        "[M2 Room] 房间已创建，roomId=%s, levelId=%d, outer=%s",
        room_id,
        level_id,
        account.uid
    ))
    return response(error_code.OK, "OK", room, "Outer")
end

function command.JOIN_ROOM(account, agent, room_id)
    if not validate_account(account, agent) then
        return response(error_code.NOT_LOGGED_IN, "账号或连接身份无效")
    end
    if room_by_uid[account.uid] then
        return response(
            error_code.ALREADY_IN_ROOM,
            string.format("账号已在房间中，uid=%s", account.uid)
        )
    end
    if type(room_id) ~= "string" or room_id == "" then
        return response(error_code.INVALID_REQUEST, "roomId 不能为空")
    end

    local room = rooms[room_id]
    if not room then
        return response(
            error_code.ROOM_NOT_FOUND,
            string.format("房间不存在，roomId=%s", room_id)
        )
    end
    if #room.players >= 2 then
        return response(
            error_code.ROOM_FULL,
            string.format("房间人数已满，roomId=%s", room_id)
        )
    end
    if room.phase ~= "waiting" then
        return response(
            error_code.ROOM_NOT_WAITING,
            string.format("房间不在等待阶段，roomId=%s, phase=%s", room_id, room.phase)
        )
    end

    room.players[2] = {
        uid = account.uid,
        display_name = account.display_name,
        role = "Inner",
        agent = agent,
    }
    room_by_uid[account.uid] = room_id
    room.phase = "playing"
    room.revision = room.revision + 1

    skynet.error(string.format(
        "[M2 Room] 玩家加入，roomId=%s, inner=%s, revision=%d",
        room_id,
        account.uid,
        room.revision
    ))
    push_room_ready(room)
    return response(error_code.OK, "OK", room, "Inner")
end

skynet.start(function()
    skynet.dispatch("lua", function(session, _, command_name, ...)
        local handler = command[command_name]
        if not handler then
            error(string.format("[M2 Room] 未知命令：%s", tostring(command_name)))
        end
        skynet.retpack(handler(...))
    end)
end)
