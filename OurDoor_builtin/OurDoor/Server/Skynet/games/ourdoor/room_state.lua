--- <summary>
--- 实现功能：创建 OurDoor 各关卡房间状态，并生成完整关卡状态快照数据。
--- </summary>
local room_state = {}

function room_state.create(level_id)
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id < 1
        or level_id > 3 then
        error(string.format(
            "[M3 OurDoorState] levelId 非法：%s",
            tostring(level_id)
        ))
    end

    local state = {}
    if level_id == 1 then
        state.level1 = {
            powerOn = false,
            passwordFound = false,
            lockOpened = false,
        }
    elseif level_id == 2 then
        state.level2 = {
            keyFound = false,
            keyLanded = false,
            boxBuilt = false,
            doorOpened = false,
        }
    elseif level_id == 3 then
        state.level3 = {
            passwordSuccess = false,
            metalFound = false,
            metalReceived = false,
            wireFound = false,
            doorOpened = false,
        }
    end
    return state
end

function room_state.build_snapshot(level_id, state)
    if type(state) ~= "table" then
        error("[M3 OurDoorState] state 必须是 table")
    end
    if level_id == 1 and type(state.level1) ~= "table" then
        error("[M3 OurDoorState] 第一关状态缺少 level1")
    end
    if level_id == 2 and type(state.level2) ~= "table" then
        error("[M4 OurDoorState] 第二关状态缺少 level2")
    end
    if level_id == 3 and type(state.level3) ~= "table" then
        error("[M4 OurDoorState] 第三关状态缺少 level3")
    end

    return {
        level1 = state.level1,
        level2 = state.level2,
        level3 = state.level3,
    }
end

return room_state
