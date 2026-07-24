--- <summary>
--- 实现功能：按全局进入顺序匹配关卡和身份偏好兼容的玩家，并保持可取消的严格索引。
--- </summary>
local match_queue = {}
match_queue.__index = match_queue

local function validate_level_id(level_id)
    if type(level_id) ~= "number"
        or level_id % 1 ~= 0
        or level_id < 0
        or level_id > 3 then
        error(string.format(
            "[M7 MatchQueue] levelId 必须是 0、1、2、3，当前=%s",
            tostring(level_id)
        ))
    end
end

local function validate_role_preference(role_preference)
    if role_preference ~= "Any"
        and role_preference ~= "Outer"
        and role_preference ~= "Inner" then
        error(string.format(
            "[M7 MatchQueue] rolePreference 必须是 Any、Outer 或 Inner，当前=%s",
            tostring(role_preference)
        ))
    end
end

local function validate_entry(entry)
    if type(entry) ~= "table"
        or type(entry.uid) ~= "string"
        or entry.uid == ""
        or entry.agent == nil then
        error("[M7 MatchQueue] 匹配项缺少 uid 或 agent")
    end
    validate_level_id(entry.level_id)
    validate_role_preference(entry.role_preference)
end

local function levels_are_compatible(first, second)
    return first.level_id == 0
        or second.level_id == 0
        or first.level_id == second.level_id
end

local function roles_are_compatible(first, second)
    return first.role_preference == "Any"
        or second.role_preference == "Any"
        or first.role_preference ~= second.role_preference
end

local function resolve_level(first, second)
    if first.level_id ~= 0 then
        return first.level_id
    end
    if second.level_id ~= 0 then
        return second.level_id
    end
    return 1
end

local function resolve_roles(first, second)
    if first.role_preference == "Outer" then
        return "Outer", "Inner"
    end
    if first.role_preference == "Inner" then
        return "Inner", "Outer"
    end
    if second.role_preference == "Outer" then
        return "Inner", "Outer"
    end
    if second.role_preference == "Inner" then
        return "Outer", "Inner"
    end
    return "Outer", "Inner"
end

function match_queue.new()
    return setmetatable({
        entries = {},
        by_uid = {},
    }, match_queue)
end

function match_queue:contains(uid)
    return self.by_uid[uid] ~= nil
end

function match_queue:request(entry)
    validate_entry(entry)
    if self.by_uid[entry.uid] then
        error(string.format(
            "[M7 MatchQueue] uid 已在匹配队列中，uid=%s",
            entry.uid
        ))
    end

    local compatible_index
    for index, waiting in ipairs(self.entries) do
        if levels_are_compatible(waiting, entry)
            and roles_are_compatible(waiting, entry) then
            compatible_index = index
            break
        end
    end

    if not compatible_index then
        self.entries[#self.entries + 1] = entry
        self.by_uid[entry.uid] = entry
        return {
            matched = false,
            waiting = entry,
        }
    end

    local first = table.remove(self.entries, compatible_index)
    if self.by_uid[first.uid] ~= first then
        error(string.format(
            "[M7 MatchQueue] FIFO 与 uid 索引不一致，uid=%s",
            first.uid
        ))
    end
    self.by_uid[first.uid] = nil

    local first_role, second_role = resolve_roles(first, entry)
    return {
        matched = true,
        first = first,
        second = entry,
        level_id = resolve_level(first, entry),
        first_role = first_role,
        second_role = second_role,
    }
end

function match_queue:cancel(uid, agent)
    if type(uid) ~= "string" or uid == "" then
        error("[M7 MatchQueue] 取消匹配时 uid 不能为空")
    end

    local entry = self.by_uid[uid]
    if not entry then
        return nil
    end
    if entry.agent ~= agent then
        error(string.format(
            "[M7 MatchQueue] 取消匹配的 agent 不一致，uid=%s",
            uid
        ))
    end

    local found_index
    for index, queued in ipairs(self.entries) do
        if queued == entry then
            found_index = index
            break
        end
    end
    if not found_index then
        error(string.format(
            "[M7 MatchQueue] uid 索引存在但 FIFO 中缺少匹配项，uid=%s",
            uid
        ))
    end

    table.remove(self.entries, found_index)
    self.by_uid[uid] = nil
    return entry
end

function match_queue:count(level_id)
    if level_id == nil then
        return #self.entries
    end
    validate_level_id(level_id)

    local count = 0
    for _, entry in ipairs(self.entries) do
        if entry.level_id == level_id then
            count = count + 1
        end
    end
    return count
end

return match_queue
