using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 搭建检测区域
/// </summary>
public class BoxBuildSlot : MonoBehaviour
{
    public int requiredBoxID = 999; // 正确的 box id

    public int prerequisiteSlotID = -1; // -1 默认无前置搭建 box

    public GameObject builtObject; // 预设搭建成功的展示物体

    [HideInInspector]
    public bool isBuilt;

    // ref
    BoxManager boxManager;

    private void Awake()
    {
        boxManager = FindObjectOfType<BoxManager>();
    }
    private void Start()
    {
        builtObject.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        Box box = other.GetComponent<Box>();

        if (box == null)
            return;

        boxManager.TryBuild(box, this);
    }
}
