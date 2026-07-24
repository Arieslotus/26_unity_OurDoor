/// <summary>
/// 实现功能：在第三关门缝检测 Outer 递出铁片，并只在在线模式提交 METAL_RECEIVED。
/// </summary>
using System;
using UnityEngine;

[RequireComponent(typeof(DoorGapReceiver))]
public sealed class OurDoorDoorGapOnlineHook : MonoBehaviour
{
    private DoorGapReceiver receiver;
    private bool submitted;

    private void Awake()
    {
        receiver = GetComponent<DoorGapReceiver>();
        if (receiver == null)
        {
            throw new InvalidOperationException(
                $"[M4 门缝联网入口] 对象 {gameObject.name} 缺少 DoorGapReceiver。");
        }
        if (receiver.transferMetalPiece == null)
        {
            throw new InvalidOperationException(
                $"[M4 门缝联网入口] 对象 {gameObject.name} " +
                "的 DoorGapReceiver 缺少 transferMetalPiece。");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!OnlineActionBridge.IsOnline)
            return;
        if (submitted)
            return;

        MetalPiece metal = other.GetComponent<MetalPiece>();
        if (metal == null || !metal.isPicking)
            return;

        submitted = true;
        OurDoorLevelActionGateway.RequestLevel3MetalReceived();
        Debug.Log(
            $"[M4 门缝联网入口] 已提交 METAL_RECEIVED，" +
            $"object={gameObject.name}, metal={metal.gameObject.name}。");
    }
}
