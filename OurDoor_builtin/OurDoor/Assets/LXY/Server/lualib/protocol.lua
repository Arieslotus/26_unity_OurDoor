local protocol = {}

protocol.MESSAGE_TYPE = {
    REQUEST = 0,
    RESPONSE = 1,
    PUSH = 2,
}

protocol.MESSAGE_ID = {
    HEARTBEAT = 1,
}

protocol.MIN_PAYLOAD_LENGTH = 7
protocol.MAX_PAYLOAD_LENGTH = 32 * 1024

return protocol
