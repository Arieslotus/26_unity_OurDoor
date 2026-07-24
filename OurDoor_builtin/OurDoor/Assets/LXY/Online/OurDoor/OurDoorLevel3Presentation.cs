/// <summary>
/// 实现功能：在第三关远端客户端直接呈现门缝铁片、铁丝和大门的确定性最终状态。
/// </summary>
using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public static class OurDoorLevel3Presentation
{
    public static void ApplyMetalReceivedForInner()
    {
        DoorGapReceiver receiver =
            UnityEngine.Object.FindObjectOfType<DoorGapReceiver>();
        if (receiver == null)
            throw new InvalidOperationException("[M4 第三关表现] 场景中缺少 DoorGapReceiver。");
        if (receiver.transferMetalPiece == null)
            throw new InvalidOperationException("[M4 第三关表现] 门缝接收器缺少 transferMetalPiece。");

        receiver.transferMetalPiece.SetActive(true);
    }

    public static void ApplyWireFoundForOuter()
    {
        WireExtractTrigger trigger =
            UnityEngine.Object.FindObjectOfType<WireExtractTrigger>();
        if (trigger == null)
            throw new InvalidOperationException("[M4 第三关表现] 场景中缺少 WireExtractTrigger。");
        if (trigger.wireTransform == null || trigger.wireEndPoint == null)
            throw new InvalidOperationException("[M4 第三关表现] 铁丝缺少 Transform 或终点引用。");

        trigger.wireTransform.SetPositionAndRotation(
            trigger.wireEndPoint.position,
            trigger.wireEndPoint.rotation);

        Rigidbody body = trigger.wireTransform.GetComponent<Rigidbody>();
        if (body == null)
            throw new InvalidOperationException("[M4 第三关表现] 铁丝缺少 Rigidbody。");
        body.isKinematic = false;
        body.useGravity = true;

        XRGrabInteractable grab =
            trigger.wireTransform.GetComponent<XRGrabInteractable>();
        PCPickupInteractable pickup =
            trigger.wireTransform.GetComponent<PCPickupInteractable>();
        if (grab == null && pickup == null)
            throw new InvalidOperationException("[M4 第三关表现] 铁丝缺少可拾取组件。");
        if (grab != null)
            grab.enabled = true;
        if (pickup != null)
            pickup.enabled = true;
    }

    public static void ApplyDoorOpenedForOuter()
    {
        HutongDoorController door =
            UnityEngine.Object.FindObjectOfType<HutongDoorController>();
        if (door == null)
            throw new InvalidOperationException("[M4 第三关表现] 场景中缺少 HutongDoorController。");
        if (door.doorL == null || door.doorR == null)
            throw new InvalidOperationException("[M4 第三关表现] 胡同门缺少左右门引用。");

        door.doorL.localRotation *=
            Quaternion.Euler(0f, door.leftOpenAngle, 0f);
        door.doorR.localRotation *=
            Quaternion.Euler(0f, door.rightOpenAngle, 0f);
        if (door.ParticleRoot != null)
            door.ParticleRoot.SetActive(true);
        if (door.lockRig != null)
            door.lockRig.isKinematic = false;
        if (door.lacthAnimator != null)
            door.lacthAnimator.SetTrigger("Open");
    }
}
