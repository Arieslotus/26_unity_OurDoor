/// <summary>
/// 实现功能：在第二关远端客户端直接呈现钥匙、纸箱和校门的确定性最终状态。
/// </summary>
using System;
using UnityEngine;

public static class OurDoorLevel2Presentation
{
    public static void ApplyKeyLandedForOuter()
    {
        SchoolKey key = UnityEngine.Object.FindObjectOfType<SchoolKey>();
        if (key == null)
            throw new InvalidOperationException("[M4 第二关表现] 场景中缺少 SchoolKey。");
        if (key.targetPoint == null)
            throw new InvalidOperationException("[M4 第二关表现] SchoolKey 缺少 targetPoint。");

        Rigidbody body = key.GetComponent<Rigidbody>();
        if (body == null)
            throw new InvalidOperationException("[M4 第二关表现] SchoolKey 缺少 Rigidbody。");

        key.transform.SetPositionAndRotation(
            key.targetPoint.position,
            key.targetPoint.rotation);
        body.isKinematic = true;
        body.useGravity = false;
        if (key.trail != null)
            key.trail.gameObject.SetActive(false);
        if (key.particles != null)
            key.particles.gameObject.SetActive(true);
    }

    public static void ApplyBoxBuiltForInner()
    {
        BoxManager manager = UnityEngine.Object.FindObjectOfType<BoxManager>();
        if (manager == null)
            throw new InvalidOperationException("[M4 第二关表现] 场景中缺少 BoxManager。");
        if (manager.slots == null || manager.slots.Count == 0)
            throw new InvalidOperationException("[M4 第二关表现] BoxManager 没有配置 slots。");

        foreach (BoxBuildSlot slot in manager.slots)
        {
            if (slot == null)
                throw new InvalidOperationException("[M4 第二关表现] slots 中存在空引用。");
            if (slot.builtObject == null)
            {
                throw new InvalidOperationException(
                    $"[M4 第二关表现] BoxBuildSlot {slot.name} 缺少 builtObject。");
            }

            slot.isBuilt = true;
            slot.builtObject.SetActive(true);
        }
        manager.SetClimbUI(true);
    }

    public static void ApplyDoorOpenedForInner()
    {
        SchoolDoorController door =
            UnityEngine.Object.FindObjectOfType<SchoolDoorController>();
        if (door == null)
            throw new InvalidOperationException("[M4 第二关表现] 场景中缺少 SchoolDoorController。");
        if (door.DoorL == null || door.DoorR == null)
            throw new InvalidOperationException("[M4 第二关表现] 校门缺少 DoorL 或 DoorR。");
        if (door.keyForAnima == null || door.particles == null)
            throw new InvalidOperationException("[M4 第二关表现] 校门缺少钥匙动画或粒子引用。");

        door.keyForAnima.gameObject.SetActive(true);
        door.particles.gameObject.SetActive(true);
        door.DoorL.localRotation *= Quaternion.Euler(0f, -70f, 0f);
        door.DoorR.localRotation *= Quaternion.Euler(0f, 70f, 0f);
    }
}
