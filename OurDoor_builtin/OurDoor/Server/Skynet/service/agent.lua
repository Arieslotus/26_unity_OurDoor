--- <summary>
--- 实现功能：管理单个 TCP 连接、匹配/房间请求、心跳、串行推送与统一退出清理。
--- </summary>
local skynet = require "skynet"
local socket = require "skynet.socket"
local queue = require "skynet.queue"
local packet = require "packet"
local protocol = require "protocol"
local error_code = require "error_code"
local json = require "json"

local fd_text, address = ...
local fd = assert(tonumber(fd_text), "[M2 Agent] 缺少有效 fd")
address = address or "unknown"

local account_service
local lobby_service
local account
local room_id
local matching_level_id
local write_lock = queue()
local closing = false
local close_after_response = false
local last_heartbeat_tick
local logged_uid

local HEARTBEAT_CHECK_TICKS = 100
local HEARTBEAT_TIMEOUT_TICKS = 30 * 100

local function send_envelope(message_id, session, message_type, body)
    local json_body
    if type(body) == "string" then
        json_body = body
    else
        json_body = json.encode(body)
    end

    local payload = packet.encode_envelope(
        message_id,
        session,
        message_type,
        json_body
    )
    local frame = packet.frame(payload)
    write_lock(function()
        socket.write(fd, frame)
    end)
end

local function send_response(message_id, session, body)
    send_envelope(
        message_id,
        session,
        protocol.MESSAGE_TYPE.RESPONSE,
        body
    )
end

local function send_business_error(message_id, session, code, message)
    send_response(message_id, session, {
        code = code,
        message = message,
    })
end

local function decode_request_body(envelope)
    if envelope.json_body == "" then
        error(string.format(
            "[M2 Agent] 请求缺少 JSON，fd=%d, messageId=%d, session=%d",
            fd,
            envelope.message_id,
            envelope.session
        ))
    end
    local body = json.decode(envelope.json_body)
    if type(body) ~= "table" or body == json.null then
        error(string.format(
            "[M2 Agent] 请求 JSON 根节点必须是对象，fd=%d, messageId=%d",
            fd,
            envelope.message_id
        ))
    end
    return body
end

local function require_login(envelope)
    if account then
        return true
    end

    send_business_error(
        envelope.message_id,
        envelope.session,
        error_code.NOT_LOGGED_IN,
        "当前连接尚未登录临时账号"
    )
    return false
end

local function cleanup_connection(reason)
    if closing then
        skynet.error(string.format(
            "[M5 Agent] 清理入口已执行，忽略重复调用，"
                .. "fd=%d, uid=%s, reason=%s",
            fd,
            logged_uid or "未登录",
            reason
        ))
        return true
    end
    closing = true

    local cleanup_account = account
    local cleanup_room_id = room_id
    local cleanup_matching_level_id = matching_level_id
    local errors = {}
    local room_result
    local remaining_accounts

    if cleanup_account then
        local room_ok, room_result_or_error = pcall(
            skynet.call,
            lobby_service,
            "lua",
            "LEAVE_CONNECTION",
            cleanup_account,
            skynet.self(),
            reason
        )
        if room_ok then
            room_result = room_result_or_error
        else
            errors[#errors + 1] =
                "lobby_cleanup=" .. tostring(room_result_or_error)
        end

        local account_ok, account_removed_or_error, account_count = pcall(
            skynet.call,
            account_service,
            "lua",
            "LOGOUT",
            skynet.self(),
            cleanup_account.guest_id
        )
        if account_ok then
            remaining_accounts = account_count
        else
            errors[#errors + 1] =
                "account_service=" .. tostring(account_removed_or_error)
        end
    end

    account = nil
    room_id = nil
    matching_level_id = nil
    last_heartbeat_tick = nil

    if #errors > 0 then
        local error_text = table.concat(errors, " | ")
        skynet.error(string.format(
            "[M5 Agent] 连接清理失败，fd=%d, uid=%s, roomId=%s, "
                .. "reason=%s, errors=%s",
            fd,
            logged_uid or "未登录",
            cleanup_room_id or "无",
            reason,
            error_text
        ))
        return false, error_text
    end

    skynet.error(string.format(
        "[M6 Agent] 连接清理完成，fd=%d, uid=%s, roomId=%s, "
            .. "matchingLevelId=%s, reason=%s, matchRemoved=%s, "
            .. "remainingMatches=%s, roomRemoved=%s, remainingRooms=%s, "
            .. "remainingRoomIndexes=%s, remainingAccounts=%s",
        fd,
        logged_uid or "未登录",
        cleanup_room_id or "无",
        cleanup_matching_level_id and tostring(cleanup_matching_level_id) or "无",
        reason,
        room_result and tostring(room_result.matchRemoved) or "false",
        room_result and tostring(room_result.remainingMatches) or "未登录",
        room_result and tostring(room_result.removed) or "false",
        room_result and tostring(room_result.remainingRooms) or "未登录",
        room_result and tostring(room_result.remainingRoomIndexes) or "未登录",
        remaining_accounts ~= nil and tostring(remaining_accounts) or "未登录"
    ))
    return true
end

local function handle_heartbeat(envelope)
    local body = decode_request_body(envelope)
    if type(body.sequence) ~= "number"
        or body.sequence % 1 ~= 0
        or body.sequence < 0 then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.INVALID_REQUEST,
            "心跳 sequence 必须是非负整数"
        )
        return
    end
    if type(body.clientTimeUtcMs) ~= "number" then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.INVALID_REQUEST,
            "心跳 clientTimeUtcMs 必须是数字"
        )
        return
    end
    if account then
        last_heartbeat_tick = skynet.now()
    end

    send_response(
        protocol.MESSAGE_ID.HEARTBEAT,
        envelope.session,
        body
    )
end

local function handle_guest_login(envelope)
    if account then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_LOGGED_IN,
            string.format("同一连接不能重复登录，uid=%s", account.uid)
        )
        return
    end

    local body = decode_request_body(envelope)
    local code, message, logged_account = skynet.call(
        account_service,
        "lua",
        "LOGIN",
        skynet.self(),
        body.guestId,
        body.displayName,
        body.clientVersion
    )
    if code ~= error_code.OK then
        send_business_error(envelope.message_id, envelope.session, code, message)
        return
    end

    account = logged_account
    logged_uid = account.uid
    last_heartbeat_tick = skynet.now()
    send_response(envelope.message_id, envelope.session, {
        code = error_code.OK,
        message = "OK",
        uid = account.uid,
        displayName = account.display_name,
    })
end

local function handle_create_room(envelope)
    if not require_login(envelope) then
        return
    end
    if room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_IN_ROOM,
            string.format("当前连接已在房间中，roomId=%s", room_id)
        )
        return
    end
    if matching_level_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_MATCHING,
            string.format("当前连接正在匹配，levelId=%d", matching_level_id)
        )
        return
    end

    local body = decode_request_body(envelope)
    local result = skynet.call(
        lobby_service,
        "lua",
        "CREATE_ROOM",
        account,
        skynet.self(),
        body.levelId
    )
    if result.code == error_code.OK then
        room_id = result.roomId
    end
    send_response(envelope.message_id, envelope.session, result)
end

local function handle_join_room(envelope)
    if not require_login(envelope) then
        return
    end
    if room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_IN_ROOM,
            string.format("当前连接已在房间中，roomId=%s", room_id)
        )
        return
    end
    if matching_level_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_MATCHING,
            string.format("当前连接正在匹配，levelId=%d", matching_level_id)
        )
        return
    end

    local body = decode_request_body(envelope)
    local requested_room_id = body.roomId
    if type(requested_room_id) == "string" then
        requested_room_id = string.upper(requested_room_id)
    end
    local result = skynet.call(
        lobby_service,
        "lua",
        "JOIN_ROOM",
        account,
        skynet.self(),
        requested_room_id
    )
    if result.code == error_code.OK then
        room_id = result.roomId
    end
    send_response(envelope.message_id, envelope.session, result)
end

local function handle_match_request(envelope)
    if not require_login(envelope) then
        return
    end
    if room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_IN_ROOM,
            string.format("当前连接已在房间中，roomId=%s", room_id)
        )
        return
    end
    if matching_level_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_MATCHING,
            string.format("当前连接正在匹配，levelId=%d", matching_level_id)
        )
        return
    end

    local body = decode_request_body(envelope)
    local result = skynet.call(
        lobby_service,
        "lua",
        "MATCH_REQUEST",
        account,
        skynet.self(),
        body.levelId
    )
    if result.code == error_code.OK and result.queued then
        matching_level_id = result.levelId
    end
    send_response(envelope.message_id, envelope.session, result)
end

local function handle_match_cancel(envelope)
    if not require_login(envelope) then
        return
    end
    decode_request_body(envelope)
    if room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.ALREADY_IN_ROOM,
            string.format("匹配已经形成房间，roomId=%s", room_id)
        )
        return
    end
    if not matching_level_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.NOT_MATCHING,
            "当前连接不在匹配队列中"
        )
        return
    end

    local result = skynet.call(
        lobby_service,
        "lua",
        "MATCH_CANCEL",
        account,
        skynet.self()
    )
    if result.code == error_code.OK then
        matching_level_id = nil
    end
    send_response(envelope.message_id, envelope.session, result)
end

local function handle_leave_room(envelope)
    if not require_login(envelope) then
        return
    end

    local body = decode_request_body(envelope)
    if type(body.roomId) ~= "string" or body.roomId == "" then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.INVALID_REQUEST,
            "LEAVE_ROOM 的 roomId 不能为空"
        )
        return
    end
    if room_id and body.roomId ~= room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.NOT_IN_ROOM,
            string.format(
                "退出房间与当前房间不一致，request=%s, current=%s",
                body.roomId,
                room_id
            )
        )
        return
    end

    local response_room_id = body.roomId
    local cleanup_ok, cleanup_error = cleanup_connection("client_leave")
    if not cleanup_ok then
        error(string.format(
            "[M5 Agent] 主动退出清理失败，fd=%d, roomId=%s, error=%s",
            fd,
            response_room_id,
            cleanup_error
        ))
    end

    send_response(envelope.message_id, envelope.session, {
        code = error_code.OK,
        message = "OK",
        roomId = response_room_id,
    })
    close_after_response = true
end

local function handle_level_action(envelope)
    if not require_login(envelope) then
        return
    end
    if not room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.NOT_IN_ROOM,
            "当前连接尚未加入房间"
        )
        return
    end

    local body = decode_request_body(envelope)
    local result = skynet.call(
        lobby_service,
        "lua",
        "SUBMIT_LEVEL_ACTION",
        account,
        skynet.self(),
        body.roomId,
        body.levelId,
        body.action,
        body.boolValue,
        body.clientActionId
    )
    send_response(envelope.message_id, envelope.session, result)
end

local function handle_ready_next_level(envelope)
    if not require_login(envelope) then
        return
    end
    if not room_id then
        send_business_error(
            envelope.message_id,
            envelope.session,
            error_code.NOT_IN_ROOM,
            "当前连接尚未加入房间"
        )
        return
    end

    local body = decode_request_body(envelope)
    local result = skynet.call(
        lobby_service,
        "lua",
        "READY_NEXT_LEVEL",
        account,
        skynet.self(),
        body.roomId,
        body.levelId
    )
    send_response(envelope.message_id, envelope.session, result)
end

local request_handlers = {
    [protocol.MESSAGE_ID.HEARTBEAT] = handle_heartbeat,
    [protocol.MESSAGE_ID.GUEST_LOGIN] = handle_guest_login,
    [protocol.MESSAGE_ID.CREATE_ROOM] = handle_create_room,
    [protocol.MESSAGE_ID.JOIN_ROOM] = handle_join_room,
    [protocol.MESSAGE_ID.LEAVE_ROOM] = handle_leave_room,
    [protocol.MESSAGE_ID.MATCH_REQUEST] = handle_match_request,
    [protocol.MESSAGE_ID.MATCH_CANCEL] = handle_match_cancel,
    [protocol.MESSAGE_ID.LEVEL_ACTION] = handle_level_action,
    [protocol.MESSAGE_ID.READY_NEXT_LEVEL] = handle_ready_next_level,
}

local function handle_payload(payload)
    local envelope = packet.decode_envelope(payload)

    if envelope.message_type ~= protocol.MESSAGE_TYPE.REQUEST then
        error(string.format(
            "[M2 Agent] 客户端消息类型非法，fd=%d, messageType=%d",
            fd,
            envelope.message_type
        ))
    end

    local handler = request_handlers[envelope.message_id]
    if not handler then
        error(string.format(
            "[M2 Agent] 未知消息，fd=%d, messageId=%d",
            fd,
            envelope.message_id
        ))
    end
    handler(envelope)
end

local function run()
    socket.start(fd)
    skynet.error(string.format(
        "[M2 Agent] 客户端已连接，fd=%d, address=%s",
        fd,
        address
    ))

    while true do
        local length_prefix = socket.read(fd, 2)
        if not length_prefix then
            return
        end

        local payload_length = packet.read_length(length_prefix)
        local payload = socket.read(fd, payload_length)
        if not payload then
            error(string.format(
                "[M2 Agent] 消息体未完整到达，fd=%d, expected=%d",
                fd,
                payload_length
            ))
        end

        handle_payload(payload)
        if close_after_response then
            return
        end
    end
end

local function heartbeat_watchdog()
    while not closing do
        skynet.sleep(HEARTBEAT_CHECK_TICKS)
        if account
            and last_heartbeat_tick
            and skynet.now() - last_heartbeat_tick
                >= HEARTBEAT_TIMEOUT_TICKS then
            local elapsed_ticks = skynet.now() - last_heartbeat_tick
            skynet.error(string.format(
                "[M5 Agent] 登录后心跳超时，fd=%d, uid=%s, "
                    .. "elapsedSeconds=%.2f",
                fd,
                logged_uid or "未知",
                elapsed_ticks / 100
            ))
            local cleanup_ok, cleanup_error =
                cleanup_connection("heartbeat_timeout")
            if not cleanup_ok then
                skynet.error(string.format(
                    "[M5 Agent] 心跳超时后的清理存在错误，fd=%d, error=%s",
                    fd,
                    cleanup_error
                ))
            end
            socket.shutdown(fd)
            return
        end
    end
end

skynet.start(function()
    account_service = skynet.uniqueservice("account_service")
    lobby_service = skynet.uniqueservice("lobby_service")

    skynet.dispatch("lua", function(_, _, command, ...)
        if command == "match_found" then
            local found = ...
            if type(found) ~= "table"
                or type(found.roomId) ~= "string"
                or found.roomId == ""
                or type(found.levelId) ~= "number"
                or found.levelId % 1 ~= 0
                or found.levelId < 1
                or found.levelId > 3
                or (found.role ~= "Outer" and found.role ~= "Inner")
                or type(found.revision) ~= "number"
                or found.revision % 1 ~= 0
                or found.revision < 0
                or type(found.wasQueued) ~= "boolean" then
                error("[M6 Agent] match_found 缺少合法匹配数据")
            end
            if closing then
                skynet.error(string.format(
                    "[M6 Agent] 连接清理期间不再发送 MATCH_FOUND，"
                        .. "fd=%d, uid=%s, roomId=%s",
                    fd,
                    logged_uid or "未登录",
                    found.roomId
                ))
                return
            end
            if not account then
                error("[M6 Agent] 未登录连接收到 match_found")
            end
            if room_id then
                error(string.format(
                    "[M6 Agent] 已在房间时收到 match_found，"
                        .. "current=%s, found=%s, uid=%s",
                    room_id,
                    found.roomId,
                    account.uid
                ))
            end
            if found.wasQueued then
                if matching_level_id ~= found.levelId then
                    error(string.format(
                        "[M6 Agent] 先入队玩家的匹配关卡不一致，"
                            .. "local=%s, found=%d, uid=%s",
                        tostring(matching_level_id),
                        found.levelId,
                        account.uid
                    ))
                end
            elseif matching_level_id ~= nil then
                error(string.format(
                    "[M6 Agent] 后入队玩家意外存在旧匹配关卡，"
                        .. "local=%d, found=%d, uid=%s",
                    matching_level_id,
                    found.levelId,
                    account.uid
                ))
            end

            matching_level_id = nil
            room_id = found.roomId
            send_envelope(
                protocol.MESSAGE_ID.MATCH_FOUND,
                0,
                protocol.MESSAGE_TYPE.PUSH,
                {
                    roomId = found.roomId,
                    levelId = found.levelId,
                    role = found.role,
                    revision = found.revision,
                }
            )
            skynet.error(string.format(
                "[M6 Agent] 已发送 MATCH_FOUND，"
                    .. "uid=%s, roomId=%s, levelId=%d, role=%s",
                account.uid,
                found.roomId,
                found.levelId,
                found.role
            ))
            return
        end

        if command == "room_closed" then
            local player_left = ...
            if type(player_left) ~= "table"
                or type(player_left.roomId) ~= "string"
                or type(player_left.leftUid) ~= "string"
                or type(player_left.reason) ~= "string" then
                error("[M5 Agent] room_closed 缺少合法 PLAYER_LEFT 数据")
            end
            if closing then
                skynet.error(string.format(
                    "[M5 Agent] 连接清理期间不再发送 PLAYER_LEFT，"
                        .. "fd=%d, roomId=%s, leftUid=%s",
                    fd,
                    player_left.roomId,
                    player_left.leftUid
                ))
                return
            end
            if room_id ~= player_left.roomId then
                error(string.format(
                    "[M5 Agent] room_closed 与本地房间不一致，"
                        .. "local=%s, notified=%s, uid=%s",
                    room_id or "无",
                    player_left.roomId,
                    logged_uid or "未登录"
                ))
            end

            room_id = nil
            send_envelope(
                protocol.MESSAGE_ID.PLAYER_LEFT,
                0,
                protocol.MESSAGE_TYPE.PUSH,
                player_left
            )
            skynet.error(string.format(
                "[M5 Agent] 已向对端发送 PLAYER_LEFT，"
                    .. "uid=%s, roomId=%s, leftUid=%s, reason=%s",
                logged_uid or "未登录",
                player_left.roomId,
                player_left.leftUid,
                player_left.reason
            ))
            return
        end

        if command ~= "push" then
            error(string.format("[M5 Agent] 未知服务消息：%s", tostring(command)))
        end
        if closing then
            local message_id = ...
            skynet.error(string.format(
                "[M5 Agent] 连接清理期间丢弃已无接收方的推送，"
                    .. "fd=%d, uid=%s, messageId=%s",
                fd,
                logged_uid or "未登录",
                tostring(message_id)
            ))
            return
        end
        if not account then
            error(string.format(
                "[M2 Agent] 未登录连接收到业务推送，fd=%d",
                fd
            ))
        end

        local message_id, body = ...
        send_envelope(
            message_id,
            0,
            protocol.MESSAGE_TYPE.PUSH,
            body
        )
    end)

    skynet.fork(heartbeat_watchdog)
    local ok, reason = xpcall(run, debug.traceback)
    if not ok then
        skynet.error(string.format(
            "[M2 Agent] 连接异常，fd=%d, address=%s, uid=%s, roomId=%s, error=%s",
            fd,
            address,
            account and account.uid or "未登录",
            room_id or "无",
            tostring(reason)
        ))
    end

    if not closing then
        local disconnect_reason = ok and "tcp_closed" or "socket_error"
        local cleanup_ok, cleanup_error =
            cleanup_connection(disconnect_reason)
        if not cleanup_ok then
            skynet.error(string.format(
                "[M5 Agent] 断线清理存在错误，fd=%d, error=%s",
                fd,
                cleanup_error
            ))
        end
    end
    socket.close(fd)
    skynet.error(string.format(
        "[M2 Agent] 客户端已断开，fd=%d, address=%s, uid=%s, roomId=%s",
        fd,
        address,
        logged_uid or "未登录",
        room_id or "已清理"
    ))
    skynet.exit()
end)
