using System;
using UnityEngine;

/// <summary>
/// 实现功能：在游戏交互与未来网络发送器之间提供可复用的操作意图接缝。
/// </summary>
public static class OnlineActionBridge
{
    public static bool IsOnline { get; private set; }

    private static Func<OnlineActionIntent, bool> submitHandler;

    public static void EnterOnline(Func<OnlineActionIntent, bool> handler)
    {
        if (handler == null)
            throw new ArgumentNullException("handler");

        submitHandler = handler;
        IsOnline = true;
    }

    public static void ExitOnline()
    {
        IsOnline = false;
        submitHandler = null;
    }

    public static bool TrySubmit(OnlineActionIntent intent)
    {
        if (!IsOnline)
            return false;

        if (intent == null)
            throw new ArgumentNullException("intent");

        if (submitHandler == null)
        {
            throw new InvalidOperationException(
                "[联网操作] 当前处于在线模式，但没有注册操作发送器。");
        }

        return submitHandler(intent);
    }
}
