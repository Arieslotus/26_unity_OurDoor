local skynet = require "skynet"
local socket = require "skynet.socket"
local packet = require "packet"
local protocol = require "protocol"

local HOST = skynet.getenv("lxy_host") or "0.0.0.0"
local PORT = tonumber(skynet.getenv("lxy_port")) or 8888

local function send_envelope(fd, message_id, session, message_type, json_body)
    local payload = packet.encode_envelope(message_id, session, message_type, json_body)
    socket.write(fd, packet.frame(payload))
end

local function handle_payload(fd, payload)
    local envelope = packet.decode_envelope(payload)

    if envelope.message_type ~= protocol.MESSAGE_TYPE.REQUEST then
        error("Day 1 server accepts request messages only")
    end
    if envelope.message_id ~= protocol.MESSAGE_ID.HEARTBEAT then
        error(string.format("unknown Day 1 message id: %d", envelope.message_id))
    end

    send_envelope(
        fd,
        protocol.MESSAGE_ID.HEARTBEAT,
        envelope.session,
        protocol.MESSAGE_TYPE.RESPONSE,
        envelope.json_body
    )
end

local function serve_client(fd, address)
    socket.start(fd)
    skynet.error(string.format("[LXY Day1] client connected: fd=%d address=%s", fd, address or "unknown"))

    local ok, reason = pcall(function()
        while true do
            local length_prefix = socket.read(fd, 2)
            if not length_prefix then
                return
            end

            local payload_length = packet.read_length(length_prefix)
            local payload = socket.read(fd, payload_length)
            if not payload then
                return
            end

            handle_payload(fd, payload)
        end
    end)

    if not ok then
        skynet.error(string.format("[LXY Day1] client protocol error: fd=%d reason=%s", fd, tostring(reason)))
    end
    socket.close(fd)
    skynet.error(string.format("[LXY Day1] client disconnected: fd=%d", fd))
end

skynet.start(function()
    local listen_fd = assert(socket.listen(HOST, PORT))
    skynet.error(string.format("[LXY Day1] HEARTBEAT server listening on %s:%d", HOST, PORT))

    socket.start(listen_fd, function(fd, address)
        skynet.fork(serve_client, fd, address)
    end)
end)
