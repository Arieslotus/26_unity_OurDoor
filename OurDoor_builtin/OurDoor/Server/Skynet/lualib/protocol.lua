local protocol = {}

protocol.MESSAGE_TYPE = {
    REQUEST = 0,
    RESPONSE = 1,
    PUSH = 2,
}

protocol.MESSAGE_ID = {
    HEARTBEAT = 1,
    GUEST_LOGIN = 100,
    CREATE_ROOM = 200,
    JOIN_ROOM = 201,
    ROOM_READY = 900,
    ROOM_SNAPSHOT = 901,
    SERVER_ERROR = 999,
}

protocol.MIN_PAYLOAD_LENGTH = 7
protocol.MAX_PAYLOAD_LENGTH = 32 * 1024

return protocol
