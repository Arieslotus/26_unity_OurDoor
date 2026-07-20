local protocol = require "protocol"

local packet = {}

local function validate_payload_length(length)
    if length < protocol.MIN_PAYLOAD_LENGTH or length > protocol.MAX_PAYLOAD_LENGTH then
        error(string.format("invalid payload length: %d", length))
    end
end

local function validate_envelope(message_id, session, message_type)
    if message_id == 0 then
        error("message id 0 is reserved")
    end
    if message_type ~= protocol.MESSAGE_TYPE.REQUEST
        and message_type ~= protocol.MESSAGE_TYPE.RESPONSE
        and message_type ~= protocol.MESSAGE_TYPE.PUSH then
        error(string.format("unknown message type: %d", message_type))
    end
    if message_type == protocol.MESSAGE_TYPE.PUSH then
        if session ~= 0 then
            error("push requires session 0")
        end
    elseif session == 0 then
        error("request and response require a non-zero session")
    end
end

function packet.encode_envelope(message_id, session, message_type, json_body)
    json_body = json_body or ""
    validate_envelope(message_id, session, message_type)
    local payload = string.pack(">I2I4I1", message_id, session, message_type) .. json_body
    validate_payload_length(#payload)
    return payload
end

function packet.decode_envelope(payload)
    validate_payload_length(#payload)
    local message_id, session, message_type, body_offset = string.unpack(">I2I4I1", payload)
    validate_envelope(message_id, session, message_type)
    return {
        message_id = message_id,
        session = session,
        message_type = message_type,
        json_body = string.sub(payload, body_offset),
    }
end

function packet.frame(payload)
    validate_payload_length(#payload)
    return string.pack(">I2", #payload) .. payload
end

function packet.read_length(length_prefix)
    if #length_prefix ~= 2 then
        error("length prefix must contain exactly 2 bytes")
    end
    local length = string.unpack(">I2", length_prefix)
    validate_payload_length(length)
    return length
end

function packet.new_stream_decoder()
    local buffered = ""

    return {
        feed = function(_, chunk)
            if chunk == nil or #chunk == 0 then
                return {}
            end

            buffered = buffered .. chunk
            local payloads = {}

            while #buffered >= 2 do
                local length = string.unpack(">I2", buffered)
                validate_payload_length(length)
                if #buffered < length + 2 then
                    break
                end

                payloads[#payloads + 1] = string.sub(buffered, 3, length + 2)
                buffered = string.sub(buffered, length + 3)
            end

            return payloads
        end,
        buffered_bytes = function()
            return #buffered
        end,
    }
end

return packet
