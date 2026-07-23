--- <summary>
--- 实现功能：提供无外部依赖的严格 JSON 编解码，供 Unity 明确 DTO 与 Lua table 通信。
--- </summary>
local json = {}
json.null = {}

local escape_map = {
    ['"'] = '\\"',
    ['\\'] = '\\\\',
    ['\b'] = '\\b',
    ['\f'] = '\\f',
    ['\n'] = '\\n',
    ['\r'] = '\\r',
    ['\t'] = '\\t',
}

local function encode_string(value)
    return '"' .. value:gsub('[%z\1-\31"\\]', function(character)
        return escape_map[character]
            or string.format("\\u%04X", string.byte(character))
    end) .. '"'
end

local function is_array(value)
    local count = 0
    local maximum = 0
    for key in pairs(value) do
        if type(key) ~= "number" or key < 1 or key % 1 ~= 0 then
            return false, 0
        end
        count = count + 1
        if key > maximum then
            maximum = key
        end
    end
    return count > 0 and maximum == count, maximum
end

local function encode_value(value, stack)
    local value_type = type(value)
    if value == json.null or value_type == "nil" then
        return "null"
    end
    if value_type == "boolean" then
        return value and "true" or "false"
    end
    if value_type == "number" then
        if value ~= value or value == math.huge or value == -math.huge then
            error("[JSON] 不能编码非有限数字")
        end
        return tostring(value)
    end
    if value_type == "string" then
        return encode_string(value)
    end
    if value_type ~= "table" then
        error(string.format("[JSON] 不支持编码类型：%s", value_type))
    end
    if stack[value] then
        error("[JSON] 不能编码循环引用")
    end

    stack[value] = true
    local array, length = is_array(value)
    local parts = {}
    if array then
        for index = 1, length do
            parts[index] = encode_value(value[index], stack)
        end
        stack[value] = nil
        return "[" .. table.concat(parts, ",") .. "]"
    end

    local keys = {}
    for key in pairs(value) do
        if type(key) ~= "string" then
            error("[JSON] 对象键必须是字符串")
        end
        keys[#keys + 1] = key
    end
    table.sort(keys)
    for index, key in ipairs(keys) do
        parts[index] = encode_string(key) .. ":" .. encode_value(value[key], stack)
    end
    stack[value] = nil
    return "{" .. table.concat(parts, ",") .. "}"
end

function json.encode(value)
    return encode_value(value, {})
end

local function utf8_character(codepoint)
    if codepoint <= 0x7F then
        return string.char(codepoint)
    end
    if codepoint <= 0x7FF then
        return string.char(
            0xC0 + math.floor(codepoint / 0x40),
            0x80 + codepoint % 0x40
        )
    end
    if codepoint <= 0xFFFF then
        return string.char(
            0xE0 + math.floor(codepoint / 0x1000),
            0x80 + math.floor(codepoint / 0x40) % 0x40,
            0x80 + codepoint % 0x40
        )
    end
    if codepoint <= 0x10FFFF then
        return string.char(
            0xF0 + math.floor(codepoint / 0x40000),
            0x80 + math.floor(codepoint / 0x1000) % 0x40,
            0x80 + math.floor(codepoint / 0x40) % 0x40,
            0x80 + codepoint % 0x40
        )
    end
    error(string.format("[JSON] Unicode 码点越界：%X", codepoint))
end

local function decoder(source)
    local position = 1
    local length = #source
    local parse_value

    local function fail(message)
        error(string.format("[JSON] %s，位置=%d", message, position))
    end

    local function skip_whitespace()
        local _, finish = source:find("^[ \t\r\n]*", position)
        position = (finish or position - 1) + 1
    end

    local function parse_hex4()
        local value = source:sub(position, position + 3)
        if #value ~= 4 or not value:match("^%x%x%x%x$") then
            fail("无效的 Unicode 转义")
        end
        position = position + 4
        return tonumber(value, 16)
    end

    local function parse_string()
        position = position + 1
        local result = {}
        local segment_start = position

        while position <= length do
            local byte = source:byte(position)
            if byte == 34 then
                result[#result + 1] = source:sub(segment_start, position - 1)
                position = position + 1
                return table.concat(result)
            end
            if byte == 92 then
                result[#result + 1] = source:sub(segment_start, position - 1)
                position = position + 1
                local escape = source:sub(position, position)
                local simple = {
                    ['"'] = '"',
                    ['\\'] = '\\',
                    ['/'] = '/',
                    b = '\b',
                    f = '\f',
                    n = '\n',
                    r = '\r',
                    t = '\t',
                }
                if simple[escape] then
                    result[#result + 1] = simple[escape]
                    position = position + 1
                elseif escape == "u" then
                    position = position + 1
                    local codepoint = parse_hex4()
                    if codepoint >= 0xD800 and codepoint <= 0xDBFF then
                        if source:sub(position, position + 1) ~= "\\u" then
                            fail("高代理项后缺少低代理项")
                        end
                        position = position + 2
                        local low = parse_hex4()
                        if low < 0xDC00 or low > 0xDFFF then
                            fail("无效的低代理项")
                        end
                        codepoint = 0x10000
                            + (codepoint - 0xD800) * 0x400
                            + (low - 0xDC00)
                    elseif codepoint >= 0xDC00 and codepoint <= 0xDFFF then
                        fail("孤立的低代理项")
                    end
                    result[#result + 1] = utf8_character(codepoint)
                else
                    fail("无效的字符串转义")
                end
                segment_start = position
            elseif byte < 32 then
                fail("字符串包含控制字符")
            else
                position = position + 1
            end
        end
        fail("字符串没有结束引号")
    end

    local function parse_number()
        local start = position
        if source:sub(position, position) == "-" then
            position = position + 1
        end

        local first_digit = source:sub(position, position)
        if first_digit == "0" then
            position = position + 1
            if source:sub(position, position):match("%d") then
                fail("数字不能包含前导零")
            end
        elseif first_digit:match("[1-9]") then
            repeat
                position = position + 1
            until not source:sub(position, position):match("%d")
        else
            fail("无效数字整数部分")
        end

        if source:sub(position, position) == "." then
            position = position + 1
            if not source:sub(position, position):match("%d") then
                fail("小数点后必须包含数字")
            end
            repeat
                position = position + 1
            until not source:sub(position, position):match("%d")
        end

        local exponent = source:sub(position, position)
        if exponent == "e" or exponent == "E" then
            position = position + 1
            local sign = source:sub(position, position)
            if sign == "+" or sign == "-" then
                position = position + 1
            end
            if not source:sub(position, position):match("%d") then
                fail("指数部分必须包含数字")
            end
            repeat
                position = position + 1
            until not source:sub(position, position):match("%d")
        end

        local text = source:sub(start, position - 1)
        local value = tonumber(text)
        if not value then
            fail("数字无法解析")
        end
        if value == math.huge or value == -math.huge then
            fail("数字超出有限范围")
        end
        return value
    end

    local function parse_array()
        position = position + 1
        skip_whitespace()
        local result = {}
        if source:sub(position, position) == "]" then
            position = position + 1
            return result
        end

        while true do
            result[#result + 1] = parse_value()
            skip_whitespace()
            local character = source:sub(position, position)
            if character == "]" then
                position = position + 1
                return result
            end
            if character ~= "," then
                fail("数组元素之间缺少逗号")
            end
            position = position + 1
            skip_whitespace()
        end
    end

    local function parse_object()
        position = position + 1
        skip_whitespace()
        local result = {}
        if source:sub(position, position) == "}" then
            position = position + 1
            return result
        end

        while true do
            if source:sub(position, position) ~= '"' then
                fail("对象键必须是字符串")
            end
            local key = parse_string()
            skip_whitespace()
            if source:sub(position, position) ~= ":" then
                fail("对象键后缺少冒号")
            end
            position = position + 1
            skip_whitespace()
            result[key] = parse_value()
            skip_whitespace()
            local character = source:sub(position, position)
            if character == "}" then
                position = position + 1
                return result
            end
            if character ~= "," then
                fail("对象成员之间缺少逗号")
            end
            position = position + 1
            skip_whitespace()
        end
    end

    parse_value = function()
        skip_whitespace()
        local character = source:sub(position, position)
        if character == '"' then
            return parse_string()
        end
        if character == "{" then
            return parse_object()
        end
        if character == "[" then
            return parse_array()
        end
        if character == "-" or character:match("%d") then
            return parse_number()
        end
        if source:sub(position, position + 3) == "true" then
            position = position + 4
            return true
        end
        if source:sub(position, position + 4) == "false" then
            position = position + 5
            return false
        end
        if source:sub(position, position + 3) == "null" then
            position = position + 4
            return json.null
        end
        fail("未知 JSON 值")
    end

    local result = parse_value()
    skip_whitespace()
    if position <= length then
        fail("JSON 末尾存在多余内容")
    end
    return result
end

function json.decode(source)
    if type(source) ~= "string" or source == "" then
        error("[JSON] 输入必须是非空字符串")
    end
    local _, invalid_position = utf8.len(source)
    if invalid_position then
        error(string.format("[JSON] 输入不是有效 UTF-8，位置=%d", invalid_position))
    end
    return decoder(source)
end

return json
