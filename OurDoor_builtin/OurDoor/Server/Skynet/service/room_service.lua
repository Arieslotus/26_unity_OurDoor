--- <summary>
--- 实现功能：管理双人房间、权威状态、幂等 action、完整快照和房间销毁。
--- </summary>
local skynet = require "skynet"
local error_code = require "error_code"
local protocol = require "protocol"
local json = require "json"
local game_rule_factory = require "game_rule"

local rooms = {}
local room_by_uid = {}
local next_room_number = 0
local game_rules

local function is_blank(value)
    return type(value) ~= "string"
        or string.find(value, "%S") == nil
end

local function resolve_creator_roles(role_preference)
    if role_preference == "Any" or role_preference == "Outer" then
        return "Outer", "Inner"
    end
    if role_preference == "Inner" then
        return "Inner", "Outer"
    end
    return nil, nil
end

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
    local game_snapshot =
        game_rules.build_snapshot(room.level_id, room.game_state)
    return {
        roomId = room.room_id,
        phase = room.phase,
        levelId = room.level_id,
        revision = room.revision,
        players = players,
        level1 = game_snapshot.level1 or json.null,
        level2 = game_snapshot.level2 or json.null,
        level3 = game_snapshot.level3 or json.null,
    }
end

local function push_snapshot(room)
    local snapshot = build_snapshot(room)
    for _, player in ipairs(room.players) do
        skynet.send(
            player.agent,
            "lua",
            "push",
            protocol.MESSAGE_ID.ROOM_SNAPSHOT,
            snapshot
        )
    end
end

local function push_room_ready(room)
    push_snapshot(room)
    for _, player in ipairs(room.players) do
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

local function push_level_changed(room, from_level_id)
    local snapshot = build_snapshot(room)
    for _, player in ipairs(room.players) do
        skynet.send(
            player.agent,
            "lua",
            "push",
            protocol.MESSAGE_ID.LEVEL_CHANGED,
            {
                roomId = room.room_id,
                fromLevelId = from_level_id,
                toLevelId = room.level_id,
                role = player.role,
                revision = room.revision,
                snapshot = snapshot,
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

local function find_player(room, account, agent)
    for _, player in ipairs(room.players) do
        if player.uid == account.uid and player.agent == agent then
            return player
        end
    end
    return nil
end

local function table_count(values)
    local count = 0
    for _ in pairs(values) do
        count = count + 1
    end
    return count
end

local function action_response(
    code,
    message,
    client_action_id,
    revision,
    duplicate,
    changed
)
    return {
        code = code,
        message = message,
        clientActionId = client_action_id,
        revision = revision or 0,
        duplicate = duplicate or false,
        changed = changed or false,
    }
end

local function remember_action_id(
    room,
    client_action_id,
    uid,
    level_id,
    action,
    bool_value,
    revision
)
    if room.processed_action_ids[client_action_id] then
        error(string.format(
            "[M3 Room] 重复 actionId 被再次写入缓存，roomId=%s, clientActionId=%s",
            room.room_id,
            client_action_id
        ))
    end

    room.processed_action_ids[client_action_id] = {
        uid = uid,
        level_id = level_id,
        action = action,
        bool_value = bool_value,
        revision = revision,
    }
    room.processed_action_order[#room.processed_action_order + 1] =
        client_action_id
    if #room.processed_action_order > 64 then
        local expired_action_id = table.remove(
            room.processed_action_order,
            1
        )
        room.processed_action_ids[expired_action_id] = nil
    end
end

local command = {}

function command.CREATE_ROOM(
    account,
    agent,
    level_id,
    role_preference
)
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
    local creator_role, joiner_role =
        resolve_creator_roles(role_preference)
    if not creator_role then
        return response(
            error_code.INVALID_ROLE_PREFERENCE,
            string.format(
                "rolePreference 必须是 Any、Outer 或 Inner，当前=%s",
                tostring(role_preference)
            )
        )
    end

    local room_id = next_room_id()
    local room = {
        room_id = room_id,
        level_id = level_id,
        phase = "waiting",
        revision = 0,
        game_state = game_rules.create_state(level_id),
        processed_action_ids = {},
        processed_action_order = {},
        ready_next_by_uid = {},
        joiner_role = joiner_role,
        players = {
            {
                uid = account.uid,
                display_name = account.display_name,
                role = creator_role,
                agent = agent,
            },
        },
    }
    rooms[room_id] = room
    room_by_uid[account.uid] = room_id

    skynet.error(string.format(
        "[M7 Room] 房间已创建，roomId=%s, levelId=%d, "
            .. "creator=%s, creatorRole=%s, joinerRole=%s",
        room_id,
        level_id,
        account.uid,
        creator_role,
        joiner_role
    ))
    return response(error_code.OK, "OK", room, creator_role)
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
    if is_blank(room_id) then
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
        role = room.joiner_role,
        agent = agent,
    }
    room_by_uid[account.uid] = room_id
    room.phase = "playing"
    room.revision = room.revision + 1

    skynet.error(string.format(
        "[M7 Room] 玩家加入，roomId=%s, joiner=%s, role=%s, revision=%d",
        room_id,
        account.uid,
        room.joiner_role,
        room.revision
    ))
    push_room_ready(room)
    return response(error_code.OK, "OK", room, room.joiner_role)
end

function command.LEAVE_CONNECTION(account, agent, reason)
    if not validate_account(account, agent) then
        error("[M5 Room] 清理连接时账号或 agent 身份无效")
    end
    if is_blank(reason) then
        error("[M5 Room] 清理连接时 reason 不能为空")
    end

    local room_id = room_by_uid[account.uid]
    if not room_id then
        local remaining_rooms = table_count(rooms)
        local remaining_indexes = table_count(room_by_uid)
        skynet.error(string.format(
            "[M5 Room] 账号当前不在房间，重复清理不再修改状态，"
                .. "uid=%s, reason=%s, remainingRooms=%d, remainingRoomIndexes=%d",
            account.uid,
            reason,
            remaining_rooms,
            remaining_indexes
        ))
        return {
            removed = false,
            roomId = nil,
            notifiedCount = 0,
            remainingRooms = remaining_rooms,
            remainingRoomIndexes = remaining_indexes,
        }
    end

    local room = rooms[room_id]
    if not room then
        error(string.format(
            "[M5 Room] room_by_uid 指向不存在的房间，"
                .. "uid=%s, roomId=%s, reason=%s",
            account.uid,
            room_id,
            reason
        ))
    end
    if not find_player(room, account, agent) then
        error(string.format(
            "[M5 Room] 房间成员与退出连接不匹配，"
                .. "uid=%s, roomId=%s, agent=%s",
            account.uid,
            room_id,
            skynet.address(agent)
        ))
    end

    local survivors = {}
    for _, player in ipairs(room.players) do
        room_by_uid[player.uid] = nil
        if player.uid ~= account.uid then
            survivors[#survivors + 1] = player
        end
    end
    rooms[room_id] = nil

    for _, survivor in ipairs(survivors) do
        skynet.send(
            survivor.agent,
            "lua",
            "room_closed",
            {
                roomId = room_id,
                leftUid = account.uid,
                reason = reason,
            }
        )
    end

    local remaining_rooms = table_count(rooms)
    local remaining_indexes = table_count(room_by_uid)
    skynet.error(string.format(
        "[M5 Room] 房间已销毁，roomId=%s, leftUid=%s, reason=%s, "
            .. "notified=%d, remainingRooms=%d, remainingRoomIndexes=%d",
        room_id,
        account.uid,
        reason,
        #survivors,
        remaining_rooms,
        remaining_indexes
    ))
    return {
        removed = true,
        roomId = room_id,
        notifiedCount = #survivors,
        remainingRooms = remaining_rooms,
        remainingRoomIndexes = remaining_indexes,
    }
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
    if not validate_account(account, agent) then
        return action_response(
            error_code.NOT_LOGGED_IN,
            "账号或连接身份无效",
            client_action_id
        )
    end
    if is_blank(room_id) then
        return action_response(
            error_code.NOT_IN_ROOM,
            "LEVEL_ACTION 缺少 roomId",
            client_action_id
        )
    end
    if room_by_uid[account.uid] ~= room_id then
        return action_response(
            error_code.NOT_IN_ROOM,
            string.format(
                "账号不属于请求房间，uid=%s, roomId=%s",
                account.uid,
                room_id
            ),
            client_action_id
        )
    end

    local room = rooms[room_id]
    if not room then
        error(string.format(
            "[M3 Room] room_by_uid 指向不存在的房间，uid=%s, roomId=%s",
            account.uid,
            room_id
        ))
    end
    local player = find_player(room, account, agent)
    if not player then
        return action_response(
            error_code.NOT_IN_ROOM,
            string.format(
                "房间成员或连接身份不匹配，uid=%s, roomId=%s",
                account.uid,
                room_id
            ),
            client_action_id,
            room.revision
        )
    end
    if room.phase ~= "playing" then
        return action_response(
            error_code.ROOM_NOT_PLAYING,
            string.format(
                "房间不在 playing 阶段，roomId=%s, phase=%s",
                room_id,
                room.phase
            ),
            client_action_id,
            room.revision
        )
    end
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id ~= room.level_id then
        return action_response(
            error_code.LEVEL_MISMATCH,
            string.format(
                "操作关卡与房间不一致，request=%s, room=%d",
                tostring(level_id),
                room.level_id
            ),
            client_action_id,
            room.revision
        )
    end
    if is_blank(action) then
        return action_response(
            error_code.INVALID_ACTION,
            "action 不能为空",
            client_action_id,
            room.revision
        )
    end
    if is_blank(client_action_id) then
        return action_response(
            error_code.INVALID_REQUEST,
            "clientActionId 不能为空",
            client_action_id,
            room.revision
        )
    end

    local processed_action =
        room.processed_action_ids[client_action_id]
    if processed_action then
        if processed_action.uid ~= account.uid
            or processed_action.level_id ~= room.level_id
            or processed_action.action ~= action
            or processed_action.bool_value ~= bool_value then
            return action_response(
                error_code.INVALID_REQUEST,
                string.format(
                    "clientActionId 被不同请求重复使用，clientActionId=%s",
                    client_action_id
                ),
                client_action_id,
                room.revision
            )
        end
        return action_response(
            error_code.OK,
            "OK",
            client_action_id,
            processed_action.revision,
            true,
            false
        )
    end

    local code, message, changed = game_rules.apply_action(
        room.level_id,
        room.game_state,
        player.role,
        action,
        bool_value
    )
    if code ~= error_code.OK then
        skynet.error(string.format(
            "[M3 Room] 操作被拒绝，roomId=%s, uid=%s, role=%s, "
                .. "action=%s, clientActionId=%s, code=%d, message=%s",
            room_id,
            account.uid,
            player.role,
            action,
            client_action_id,
            code,
            message
        ))
        return action_response(
            code,
            message,
            client_action_id,
            room.revision
        )
    end

    if changed then
        room.revision = room.revision + 1
        push_snapshot(room)
    end
    remember_action_id(
        room,
        client_action_id,
        account.uid,
        room.level_id,
        action,
        bool_value,
        room.revision
    )
    skynet.error(string.format(
        "[M3 Room] 操作已处理，roomId=%s, uid=%s, role=%s, "
            .. "action=%s, clientActionId=%s, changed=%s, revision=%d",
        room_id,
        account.uid,
        player.role,
        action,
        client_action_id,
        tostring(changed),
        room.revision
    ))
    return action_response(
        error_code.OK,
        "OK",
        client_action_id,
        room.revision,
        false,
        changed
    )
end

local function ready_next_response(
    code,
    message,
    room,
    requested_level_id,
    ready_count
)
    return {
        code = code,
        message = message,
        roomId = room and room.room_id or nil,
        levelId = requested_level_id or 0,
        readyCount = ready_count or 0,
        revision = room and room.revision or 0,
    }
end

local function count_ready_players(room)
    local count = 0
    for _, player in ipairs(room.players) do
        if room.ready_next_by_uid[player.uid] then
            count = count + 1
        end
    end
    return count
end

function command.READY_NEXT_LEVEL(
    account,
    agent,
    room_id,
    level_id
)
    if not validate_account(account, agent) then
        return ready_next_response(
            error_code.NOT_LOGGED_IN,
            "账号或连接身份无效",
            nil,
            level_id
        )
    end
    if is_blank(room_id) or room_by_uid[account.uid] ~= room_id then
        return ready_next_response(
            error_code.NOT_IN_ROOM,
            string.format(
                "账号不属于请求房间，uid=%s, roomId=%s",
                account.uid,
                tostring(room_id)
            ),
            nil,
            level_id
        )
    end

    local room = rooms[room_id]
    if not room then
        error(string.format(
            "[M4 Room] room_by_uid 指向不存在的房间，uid=%s, roomId=%s",
            account.uid,
            room_id
        ))
    end
    if not find_player(room, account, agent) then
        return ready_next_response(
            error_code.NOT_IN_ROOM,
            "房间成员或连接身份不匹配",
            room,
            level_id
        )
    end
    if room.phase ~= "playing" then
        return ready_next_response(
            error_code.ROOM_NOT_PLAYING,
            string.format("房间阶段不是 playing：%s", room.phase),
            room,
            level_id
        )
    end
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id ~= room.level_id then
        return ready_next_response(
            error_code.LEVEL_MISMATCH,
            string.format(
                "换关请求与当前房间关卡不一致，request=%s, room=%d",
                tostring(level_id),
                room.level_id
            ),
            room,
            level_id
        )
    end
    if room.level_id >= 3 then
        return ready_next_response(
            error_code.NO_NEXT_LEVEL,
            "第三关已经是最终关卡",
            room,
            level_id
        )
    end
    if not game_rules.is_level_complete(
        room.level_id,
        room.game_state
    ) then
        return ready_next_response(
            error_code.LEVEL_NOT_COMPLETE,
            string.format("当前关卡尚未完成，levelId=%d", room.level_id),
            room,
            level_id
        )
    end

    room.ready_next_by_uid[account.uid] = true
    local ready_count = count_ready_players(room)
    if ready_count == #room.players then
        local from_level_id = room.level_id
        room.level_id = from_level_id + 1
        room.game_state = game_rules.create_state(room.level_id)
        room.ready_next_by_uid = {}
        room.revision = room.revision + 1
        push_level_changed(room, from_level_id)
        skynet.error(string.format(
            "[M4 Room] 双方已准备并切换关卡，roomId=%s, from=%d, to=%d, revision=%d",
            room.room_id,
            from_level_id,
            room.level_id,
            room.revision
        ))
    else
        skynet.error(string.format(
            "[M4 Room] 玩家已准备换关，roomId=%s, uid=%s, levelId=%d, ready=%d/2",
            room.room_id,
            account.uid,
            room.level_id,
            ready_count
        ))
    end

    return ready_next_response(
        error_code.OK,
        "OK",
        room,
        level_id,
        ready_count
    )
end

skynet.start(function()
    local rule_module_name = skynet.getenv("lxy_game_rule_module")
    if type(rule_module_name) ~= "string" or rule_module_name == "" then
        error(
            "[M3 Room] config 缺少 lxy_game_rule_module，"
                .. "应配置为 ourdoor.level_rules"
        )
    end
    game_rules = game_rule_factory.new(require(rule_module_name))

    skynet.dispatch("lua", function(session, _, command_name, ...)
        local handler = command[command_name]
        if not handler then
            error(string.format("[M2 Room] 未知命令：%s", tostring(command_name)))
        end
        skynet.retpack(handler(...))
    end)
end)
