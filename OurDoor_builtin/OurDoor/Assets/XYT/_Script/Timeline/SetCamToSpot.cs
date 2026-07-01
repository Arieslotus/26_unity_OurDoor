using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetCamToSpot : MonoBehaviour
{
    public Transform CameraParent;

    public Transform AimSpot;

    [ContextMenu("设置相机位置")]
    public void SetCmaSpot()
    {
        if (CameraParent == null || AimSpot == null) return;
        CameraParent.position = AimSpot.position;
        CameraParent.rotation = AimSpot.rotation;
    }
}
