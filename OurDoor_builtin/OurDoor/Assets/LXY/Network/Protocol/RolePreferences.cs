/// <summary>
/// 实现功能：集中定义创建房间和匹配请求使用的身份偏好及兼容性校验。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Protocol
{
    public static class RolePreferences
    {
        public const string Any = "Any";
        public const string Outer = "Outer";
        public const string Inner = "Inner";

        public static void Validate(string preference, string parameterName)
        {
            if (preference != Any &&
                preference != Outer &&
                preference != Inner)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"身份偏好必须是 Any、Outer 或 Inner，当前={preference ?? "null"}。");
            }
        }

        public static bool AcceptsRole(string preference, string actualRole)
        {
            Validate(preference, nameof(preference));
            if (actualRole != Outer && actualRole != Inner)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actualRole),
                    $"实际身份必须是 Outer 或 Inner，当前={actualRole ?? "null"}。");
            }

            return preference == Any || preference == actualRole;
        }
    }
}
