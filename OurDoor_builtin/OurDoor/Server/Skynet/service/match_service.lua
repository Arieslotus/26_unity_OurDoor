--- <summary>
--- 实现功能：按关卡执行 FIFO 匹配，复用房间创建/加入流程并负责取消与掉线移除。
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

local function response(code, message, queued, level_id)
    return {
        code = code,
        message = message,
        queued = queued or false,
        levelId = level_id or 0,
    }
end

local function cancel_response(code, message, removed, level_id)
    return {
        code = code,
        message = message,
        removed = removed or false,
        levelId = level_id or 0,
    }
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

local function create_matched_room(first, second)
    local created = skynet.call(
        room_service,
        "lua",
        "CREATE_ROOM",
        first.account,
        first.agent,
        first.level_id
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
        or created.role ~= "Outer"
        or joined.role ~= "Inner"
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
    })
end

function command.REQUEST(account, agent, level_id)
    if not validate_account(account, agent) then
        return response(error_code.NOT_LOGGED_IN, "账号或连接身份无效")
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
    if waiting:contains(account.uid) then
        return response(
            error_code.ALREADY_MATCHING,
            string.format("账号已经在匹配队列中，uid=%s", account.uid),
            false,
            level_id
        )
    end

    local entry = {
        uid = account.uid,
        agent = agent,
        account = account,
        level_id = level_id,
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
        return response(error_code.OK, "OK", true, level_id)
    end

    local room, room_error =
        create_matched_room(result.first, result.second)
    if not room then
        return response(
            room_error.code,
            room_error.message,
            false,
            level_id
        )
    end

    notify_match_found(result.first, room, "Outer", true)
    notify_match_found(result.second, room, "Inner", false)
    skynet.error(string.format(
        "[M6 Match] FIFO 配对成功，roomId=%s, levelId=%d, "
            .. "outer=%s, inner=%s, totalWaiting=%d",
        room.room_id,
        room.level_id,
        result.first.uid,
        result.second.uid,
        waiting:count()
    ))
    return response(error_code.OK, "OK", false, level_id)
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
        removed.level_id
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
