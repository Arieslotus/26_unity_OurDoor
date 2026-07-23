/// <summary>
/// 实现功能：定义与具体游戏无关的联网操作意图数据。
/// </summary>
public sealed class OnlineActionIntent
{
    public int LevelId { get; private set; }
    public string Action { get; private set; }
    public bool BoolValue { get; private set; }

    public OnlineActionIntent(int levelId, string action, bool boolValue)
    {
        LevelId = levelId;
        Action = action;
        BoolValue = boolValue;
    }
}
