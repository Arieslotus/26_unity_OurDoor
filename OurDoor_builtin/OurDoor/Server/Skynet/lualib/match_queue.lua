--- <summary>
--- 实现功能：提供按 levelId 隔离、可取消且无残留索引的严格 FIFO 匹配队列。
--- </summary>
local match_queue = {}
match_queue.__index = match_queue

local function validate_level_id(level_id)
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id < 1
        or level_id > 3 then
        error(string.format(
            "[M6 MatchQueue] levelId 必须是 1、2、3，当前=%s",
            tostring(level_id)
        ))
    end
end

local function validate_entry(entry)
    if type(entry) ~= "table"
        or type(entry.uid) ~= "string"
        or entry.uid == ""
        or entry.agent == nil then
        error("[M6 MatchQueue] 匹配项缺少 uid 或 agent")
    end
    validate_level_id(entry.level_id)
end

function match_queue.new()
    return setmetatable({
        queues = {
            [1] = {},
            [2] = {},
            [3] = {},
        },
        by_uid = {},
        total = 0,
    }, match_queue)
end

function match_queue:contains(uid)
    return self.by_uid[uid] ~= nil
end

function match_queue:request(entry)
    validate_entry(entry)
    if self.by_uid[entry.uid] then
        error(string.format(
            "[M6 MatchQueue] uid 已在匹配队列中，uid=%s",
            entry.uid
        ))
    end

    local queue = self.queues[entry.level_id]
    if #queue == 0 then
        queue[1] = entry
        self.by_uid[entry.uid] = entry
        self.total = self.total + 1
        return {
            matched = false,
            waiting = entry,
        }
    end

    local first = table.remove(queue, 1)
    if self.by_uid[first.uid] ~= first then
        error(string.format(
            "[M6 MatchQueue] FIFO 与 uid 索引不一致，uid=%s, levelId=%d",
            first.uid,
            first.level_id
        ))
    end
    self.by_uid[first.uid] = nil
    self.total = self.total - 1
    return {
        matched = true,
        first = first,
        second = entry,
    }
end

function match_queue:cancel(uid, agent)
    if type(uid) ~= "string" or uid == "" then
        error("[M6 MatchQueue] 取消匹配时 uid 不能为空")
    end

    local entry = self.by_uid[uid]
    if not entry then
        return nil
    end
    if entry.agent ~= agent then
        error(string.format(
            "[M6 MatchQueue] 取消匹配的 agent 不一致，uid=%s",
            uid
        ))
    end

    local queue = self.queues[entry.level_id]
    local found_index
    for index, queued in ipairs(queue) do
        if queued == entry then
            found_index = index
            break
        end
    end
    if not found_index then
        error(string.format(
            "[M6 MatchQueue] uid 索引存在但 FIFO 中缺少匹配项，uid=%s",
            uid
        ))
    end

    table.remove(queue, found_index)
    self.by_uid[uid] = nil
    self.total = self.total - 1
    return entry
end

function match_queue:count(level_id)
    if level_id == nil then
        return self.total
    end
    validate_level_id(level_id)
    return #self.queues[level_id]
end

return match_queue
