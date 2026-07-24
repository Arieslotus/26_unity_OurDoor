--- <summary>
--- 实现功能：按关卡模式和身份偏好执行全局 FIFO 匹配，并负责房间创建、取消与掉线移除。
--- </summary>
local skynet = require "skynet"
local queue = require "skynet.queue"
local error_code = require "error_code"
local match_queue = require "match_queue"

local room_service
local waiting = match_queue.new()
local command_lock = queue()
local command = {}

local function validate_account(account, agent)
    return type(account) == "table"
        and type(account.uid) == "string"
        and account.uid ~= ""
        and account.agent == agent
end

local function response(code, message, queued, level_id, role_preference)
    return {
        code = code,
        message = message,
        queued = queued or false,
        levelId = level_id or 0,
        rolePreference = role_preference,
    }
end

local function cancel_response(
    code,
    message,
    removed,
    level_id,
    role_preference
)
    return {
        code = code,
        message = message,
        removed = removed or false,
        levelId = level_id or 0,
        rolePreference = role_preference,
    }
end

local function is_valid_role_preference(role_preference)
    return role_preference == "Any"
        or role_preference == "Outer"
        or role_preference == "Inner"
end

local function requeue_first_after_room_failure(first, failure_message)
    local requeue = waiting:request(first)
    if requeue.matched then
        error(string.format(
            "[M6 Match] 房间创建失败后的重入队意外触发配对，uid=%s",
            first.uid
        ))
    end
    skynet.error(string.format(
        "[M6 Match] 配对后的房间流程失败，先入队玩家已重新排队，"
            .. "uid=%s, levelId=%d, failure=%s, waiting=%d",
        first.uid,
        first.level_id,
        failure_message,
        waiting:count()
    ))
end

local function create_matched_room(
    first,
    second,
    matched_level_id,
    first_role,
    second_role
)
    local created = skynet.call(
        room_service,
        "lua",
        "CREATE_ROOM",
        first.account,
        first.agent,
        matched_level_id,
        first_role
    )
    if created.code ~= error_code.OK then
        requeue_first_after_room_failure(
            first,
            string.format(
                "CREATE_ROOM code=%s, message=%s",
                tostring(created.code),
                tostring(created.message)
            )
        )
        return nil, created
    end

    local joined = skynet.call(
        room_service,
        "lua",
        "JOIN_ROOM",
        second.account,
        second.agent,
        created.roomId
    )
    if joined.code ~= error_code.OK then
        local cleanup = skynet.call(
            room_service,
            "lua",
            "LEAVE_CONNECTION",
            first.account,
            first.agent,
            "match_join_failed"
        )
        if not cleanup.removed then
            error(string.format(
                "[M6 Match] JOIN_ROOM 失败后未能销毁临时房间，roomId=%s",
                created.roomId
            ))
        end
        requeue_first_after_room_failure(
            first,
            string.format(
                "JOIN_ROOM code=%s, message=%s",
                tostring(joined.code),
                tostring(joined.message)
            )
        )
        return nil, joined
    end

    if created.roomId ~= joined.roomId
        or created.levelId ~= joined.levelId
        or created.role ~= first_role
        or joined.role ~= second_role
        or joined.revision <= created.revision then
        error(string.format(
            "[M6 Match] 复用房间流程返回的数据不一致，"
                .. "createdRoom=%s, joinedRoom=%s, outerRole=%s, "
                .. "innerRole=%s, createRevision=%s, joinRevision=%s",
            tostring(created.roomId),
            tostring(joined.roomId),
            tostring(created.role),
            tostring(joined.role),
            tostring(created.revision),
            tostring(joined.revision)
        ))
    end

    return {
        room_id = joined.roomId,
        level_id = joined.levelId,
        revision = joined.revision,
    }
end

local function notify_match_found(entry, room, role, was_queued)
    skynet.send(entry.agent, "lua", "match_found", {
        roomId = room.room_id,
        levelId = room.level_id,
        role = role,
        revision = room.revision,
        wasQueued = was_queued,
        requestedLevelId = entry.level_id,
        rolePreference = entry.role_preference,
    })
end

function command.REQUEST(account, agent, level_id, role_preference)
    if not validate_account(account, agent) then
        return response(error_code.NOT_LOGGED_IN, "账号或连接身份无效")
    end
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id < 0
        or level_id > 3 then
        return response(
            error_code.INVALID_LEVEL,
            string.format(
                "levelId 必须是 0、1、2、3，当前=%s",
                tostring(level_id)
            )
        )
    end
    if not is_valid_role_preference(role_preference) then
        return response(
            error_code.INVALID_ROLE_PREFERENCE,
            string.format(
                "rolePreference 必须是 Any、Outer、Inner，当前=%s",
                tostring(role_preference)
            ),
            false,
            level_id,
            role_preference
        )
    end
    if waiting:contains(account.uid) then
        return response(
            error_code.ALREADY_MATCHING,
            string.format("账号已经在匹配队列中，uid=%s", account.uid),
            false,
            level_id,
            role_preference
        )
    end

    local entry = {
        uid = account.uid,
        agent = agent,
        account = account,
        level_id = level_id,
        role_preference = role_preference,
    }
    local result = waiting:request(entry)
    if not result.matched then
        skynet.error(string.format(
            "[M6 Match] 玩家进入队列，uid=%s, levelId=%d, "
                .. "levelWaiting=%d, totalWaiting=%d",
            account.uid,
            level_id,
            waiting:count(level_id),
            waiting:count()
        ))
        return response(
            error_code.OK,
            "OK",
            true,
            level_id,
            role_preference
        )
    end

    local room, room_error =
        create_matched_room(
            result.first,
            result.second,
            result.level_id,
            result.first_role,
            result.second_role
        )
    if not room then
        return response(
            room_error.code,
            room_error.message,
            false,
            level_id,
            role_preference
        )
    end

    notify_match_found(
        result.first,
        room,
        result.first_role,
        true
    )
    notify_match_found(
        result.second,
        room,
        result.second_role,
        false
    )
    skynet.error(string.format(
        "[M7 Match] FIFO 配对成功，roomId=%s, levelId=%d, "
            .. "first=%s(%s), second=%s(%s), totalWaiting=%d",
        room.room_id,
        room.level_id,
        result.first.uid,
        result.first_role,
        result.second.uid,
        result.second_role,
        waiting:count()
    ))
    return response(
        error_code.OK,
        "OK",
        false,
        level_id,
        role_preference
    )
end

function command.CANCEL(account, agent)
    if not validate_account(account, agent) then
        return cancel_response(error_code.NOT_LOGGED_IN, "账号或连接身份无效")
    end

    local removed = waiting:cancel(account.uid, agent)
    if not removed then
        return cancel_response(
            error_code.NOT_MATCHING,
            string.format("账号当前不在匹配队列中，uid=%s", account.uid)
        )
    end

    skynet.error(string.format(
        "[M6 Match] 玩家取消匹配，uid=%s, levelId=%d, totalWaiting=%d",
        removed.uid,
        removed.level_id,
        waiting:count()
    ))
    return cancel_response(
        error_code.OK,
        "OK",
        true,
        removed.level_id,
        removed.role_preference
    )
end

function command.LEAVE_CONNECTION(account, agent, reason)
    if not validate_account(account, agent) then
        error("[M6 Match] 清理连接时账号或 agent 身份无效")
    end
    if type(reason) ~= "string" or reason == "" then
        error("[M6 Match] 清理连接时 reason 不能为空")
    end

    local removed = waiting:cancel(account.uid, agent)
    skynet.error(string.format(
        "[M6 Match] 连接清理匹配项，uid=%s, reason=%s, "
            .. "removed=%s, levelId=%s, totalWaiting=%d",
        account.uid,
        reason,
        tostring(removed ~= nil),
        removed and tostring(removed.level_id) or "无",
        waiting:count()
    ))
    return {
        removed = removed ~= nil,
        levelId = removed and removed.level_id or nil,
        remainingMatches = waiting:count(),
    }
end

skynet.start(function()
    room_service = skynet.uniqueservice("room_service")
    skynet.dispatch("lua", function(_, _, command_name, ...)
        local handler = command[command_name]
        if not handler then
            error(string.format("[M6 Match] 未知命令：%s", tostring(command_name)))
        end
        skynet.retpack(command_lock(handler, ...))
    end)
end)
