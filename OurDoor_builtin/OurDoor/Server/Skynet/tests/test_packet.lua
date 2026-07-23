local script_path = arg and arg[0] or ""
local server_root = string.match(script_path, "^(.*)/tests/[^/]+$") or "."
package.path = server_root .. "/lualib/?.lua;" .. package.path

local packet = require "packet"
local protocol = require "protocol"

local function assert_equal(actual, expected, message)
    if actual ~= expected then
        error(string.format("%s: expected %s, got %s", message, tostring(expected), tostring(actual)))
    end
end

local function assert_error(action, message)
    local ok = pcall(action)
    if ok then
        error(message .. ": expected an error")
    end
end

local first_payload = packet.encode_envelope(
    protocol.MESSAGE_ID.HEARTBEAT,
    0x01020304,
    protocol.MESSAGE_TYPE.REQUEST,
    '{"sequence":1}'
)
local second_payload = packet.encode_envelope(
    protocol.MESSAGE_ID.HEARTBEAT,
    0x05060708,
    protocol.MESSAGE_TYPE.REQUEST,
    '{"sequence":2}'
)

local decoded = packet.decode_envelope(first_payload)
assert_equal(decoded.message_id, 1, "message id")
assert_equal(decoded.session, 0x01020304, "session")
assert_equal(decoded.message_type, protocol.MESSAGE_TYPE.REQUEST, "message type")
assert_equal(decoded.json_body, '{"sequence":1}', "json body")
assert_equal(protocol.MESSAGE_ID.GUEST_LOGIN, 100, "guest login message id")
assert_equal(protocol.MESSAGE_ID.CREATE_ROOM, 200, "create room message id")
assert_equal(protocol.MESSAGE_ID.JOIN_ROOM, 201, "join room message id")
assert_equal(protocol.MESSAGE_ID.ROOM_READY, 900, "room ready message id")
assert_equal(protocol.MESSAGE_ID.ROOM_SNAPSHOT, 901, "room snapshot message id")

local first_frame = packet.frame(first_payload)
local second_frame = packet.frame(second_payload)
local expected_first_frame = string.char(
    0x00, 0x15,
    0x00, 0x01,
    0x01, 0x02, 0x03, 0x04,
    0x00,
    0x7B, 0x22, 0x73, 0x65, 0x71, 0x75, 0x65, 0x6E, 0x63, 0x65, 0x22, 0x3A, 0x31, 0x7D
)
assert_equal(first_frame, expected_first_frame, "cross-language wire vector")

local decoder = packet.new_stream_decoder()
assert_equal(#decoder:feed(string.sub(first_frame, 1, 1)), 0, "split header part 1")
assert_equal(#decoder:feed(string.sub(first_frame, 2, 5)), 0, "split header part 2")
local completed = decoder:feed(string.sub(first_frame, 6))
assert_equal(#completed, 1, "split payload completion")
assert_equal(completed[1], first_payload, "split payload value")

decoder = packet.new_stream_decoder()
completed = decoder:feed(first_frame .. second_frame)
assert_equal(#completed, 2, "sticky packets")
assert_equal(completed[1], first_payload, "sticky packet 1")
assert_equal(completed[2], second_payload, "sticky packet 2")

decoder = packet.new_stream_decoder()
local combined = first_frame .. string.sub(second_frame, 1, 4)
completed = decoder:feed(combined)
assert_equal(#completed, 1, "full plus partial")
assert_equal(decoder:buffered_bytes(), 4, "partial bytes retained")
completed = decoder:feed(string.sub(second_frame, 5))
assert_equal(#completed, 1, "partial packet completion")
assert_equal(completed[1], second_payload, "partial packet value")

assert_error(function()
    packet.encode_envelope(
        0,
        1,
        protocol.MESSAGE_TYPE.REQUEST,
        "{}"
    )
end, "message id zero")

assert_error(function()
    packet.encode_envelope(
        protocol.MESSAGE_ID.HEARTBEAT,
        0,
        protocol.MESSAGE_TYPE.REQUEST,
        "{}"
    )
end, "request session zero")

assert_error(function()
    packet.encode_envelope(
        protocol.MESSAGE_ID.HEARTBEAT,
        1,
        protocol.MESSAGE_TYPE.PUSH,
        "{}"
    )
end, "push non-zero session")

assert_error(function()
    packet.decode_envelope(string.rep("\0", 6))
end, "payload below minimum")

assert_error(function()
    packet.read_length(string.char(0, 6))
end, "frame length below minimum")

print("LXY M1 Lua packet tests passed")
