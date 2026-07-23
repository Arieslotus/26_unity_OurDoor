--- <summary>
--- 实现功能：监听 TCP 端口，并为每个客户端连接创建独立 agent。
--- </summary>
local skynet = require "skynet"
local socket = require "skynet.socket"

local host = skynet.getenv("lxy_host") or "0.0.0.0"
local port = tonumber(skynet.getenv("lxy_port")) or 8888

local function accept_client(fd, address)
    local ok, agent_or_error = pcall(
        skynet.newservice,
        "agent",
        tostring(fd),
        address or "unknown"
    )

    if not ok then
        socket.close(fd)
        skynet.error(string.format(
            "[M1 Gate] 创建 agent 失败，fd=%d, address=%s, error=%s",
            fd,
            address or "unknown",
            tostring(agent_or_error)
        ))
        return
    end

    skynet.error(string.format(
        "[M1 Gate] 客户端已接入，fd=%d, address=%s, agent=%s",
        fd,
        address or "unknown",
        skynet.address(agent_or_error)
    ))
end

skynet.start(function()
    local listen_fd = assert(
        socket.listen(host, port),
        string.format("[M1 Gate] 监听失败，host=%s, port=%d", host, port)
    )

    skynet.error(string.format("[M1 Gate] 正在监听 %s:%d", host, port))
    socket.start(listen_fd, accept_client)
end)
