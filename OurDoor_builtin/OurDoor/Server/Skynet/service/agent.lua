--- <summary>
--- 实现功能：管理单个 TCP 连接，处理心跳、临时登录、房间请求与串行服务端推送。
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
local write_lock = queue()

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

local function handle_heartbeat(envelope)
    send_response(
        protocol.MESSAGE_ID.HEARTBEAT,
        envelope.session,
        envelope.json_body
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

local request_handlers = {
    [protocol.MESSAGE_ID.HEARTBEAT] = handle_heartbeat,
    [protocol.MESSAGE_ID.GUEST_LOGIN] = handle_guest_login,
    [protocol.MESSAGE_ID.CREATE_ROOM] = handle_create_room,
    [protocol.MESSAGE_ID.JOIN_ROOM] = handle_join_room,
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
    end
end

skynet.start(function()
    account_service = skynet.uniqueservice("account_service")
    lobby_service = skynet.uniqueservice("lobby_service")

    skynet.dispatch("lua", function(_, _, command, ...)
        if command ~= "push" then
            error(string.format("[M2 Agent] 未知服务消息：%s", tostring(command)))
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

    socket.close(fd)
    skynet.error(string.format(
        "[M2 Agent] 客户端已断开，fd=%d, address=%s, uid=%s, roomId=%s",
        fd,
        address,
        account and account.uid or "未登录",
        room_id or "无"
    ))
    skynet.exit()
end)
