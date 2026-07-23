--- <summary>
--- 实现功能：按顺序启动 M2 账号、房间、大厅和 TCP 接入服务。
--- </summary>
local skynet = require "skynet"

skynet.start(function()
    local account = skynet.uniqueservice("account_service")
    local room = skynet.uniqueservice("room_service")
    local lobby = skynet.uniqueservice("lobby_service")
    local gate = skynet.newservice("gate_service")
    if not gate then
        error("[M2] gate_service 启动失败")
    end

    skynet.error(string.format(
        "[M2] main 启动完成，account=%s, room=%s, lobby=%s, gate=%s",
        skynet.address(account),
        skynet.address(room),
        skynet.address(lobby),
        skynet.address(gate)
    ))
    skynet.exit()
end)
