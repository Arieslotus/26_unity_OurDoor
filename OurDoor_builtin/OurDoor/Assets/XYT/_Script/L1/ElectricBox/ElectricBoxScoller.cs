using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在每个滑条上，用于检测该滑条是否开启
/// </summary>
public class ElectricBoxScoller : MonoBehaviour
{
    ElectricScollerTrigger onTrigger;
    ElectricScollerTrigger offTrigger;

    [Header("状态")]
    public bool isCurrentOn = false;

    private void Start()
    {
        var tri = GetComponentsInChildren<ElectricScollerTrigger>();

        foreach(var t in tri)
        {
            if (t != null)
            {
                if (t.isOn) onTrigger = t;
                else offTrigger = t;
            }
        }
    }

    private void Update()
    {
        if(onTrigger != null && offTrigger != null)
        {
            if(onTrigger.hasTriggered && !offTrigger.hasTriggered)
            {
                isCurrentOn = true;
            }
            else
            {
                isCurrentOn = false;
            }
        }
    }
}
