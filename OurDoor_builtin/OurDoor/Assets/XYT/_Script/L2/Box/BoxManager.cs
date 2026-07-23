using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class BoxManager : MonoBehaviour
{
    [Tooltip("抓取状态")]
    public bool isGrabbingBox => grabbingCount > 0;
    private int grabbingCount = 0;

    [Header("提示")]
    public Transform ghostParent; // 提示

    [Header("爬上")]
    public Transform climbUI;


    // ref
    PlayersManager playerManager;

    private void Awake()
    {
        playerManager = FindObjectOfType<PlayersManager>();
    }
    private void Start()
    {
        ghostParent.gameObject.SetActive(false);
        SetClimbUI(false);
    }

    private void Update()
    {

        UpdateGhost();
    }
    public void BeginGrab()
    {
        grabbingCount++;
    }

    public void EndGrab()
    {
        grabbingCount--;

        if (grabbingCount < 0)
            grabbingCount = 0;
    }

    // 在钥匙落墙后，根据玩家手里是否有物体，更新鬼影提示
    void UpdateGhost()
    {
        if (Level2Manager.Instance.KeyLandWall)
        {
            if(playerManager.currentPlayerType == PlayerType.Outer)
            {
                if (isGrabbingBox) ghostParent.gameObject.SetActive(true);
                else ghostParent.gameObject.SetActive(false);
            }
        }
    }

    // build
    public List<BoxBuildSlot> slots;

    public bool TryBuild(Box box, BoxBuildSlot slot)
    {
        if (slot.isBuilt)
            return false;

        // ID检查
        if (box.boxID != slot.requiredBoxID)
            return false;

        // 前置检查
        if (slot.prerequisiteSlotID >= 0)
        {
            BoxBuildSlot pre = GetSlotByID(slot.prerequisiteSlotID);

            if (!pre.isBuilt)
                return false;
        }

        // build
        slot.isBuilt = true;
        //if (slot.builtObject != null)
        //    slot.builtObject.SetActive(true);
        if (slot.builtObject != null)
        {
            StartCoroutine(PlayBuildAnim(slot.builtObject));
        }

        // delete box
        XRGrabInteractable grab = box.GetComponent<XRGrabInteractable>();
        if (grab.isSelected)
        {
            grab.interactionManager.SelectExit(
                grab.firstInteractorSelecting,
                grab);
        }
        Destroy(box.gameObject);

        // check all
        CheckAllBuilt();
        RefreshAllGhost();

        Debug.Log($"搭建物体{slot.requiredBoxID}成功");

        return true;
    }

    void CheckAllBuilt()
    {
        foreach (var slot in slots)
        {
            if (!slot.isBuilt)
                return;
        }

        Debug.Log("全部搭建完成");

        SetClimbUI(true); // 显示攀爬ui
        OurDoorLevelActionGateway.RequestLevel2BoxBuilt();
    }

    public void SetClimbUI(bool active)
    {
        if (climbUI != null) climbUI.gameObject.SetActive(active);
    }

    public BoxBuildSlot GetSlotByID(int boxID)
    {
        foreach (var slot in slots)
        {
            if (slot.requiredBoxID == boxID)
                return slot;
        }
        return null;
    }
    public bool IsBuilt(int boxID)
    {
        foreach (var slot in slots)
        {
            if (slot.requiredBoxID == boxID)
                return slot.isBuilt;
        }

        return false;
    }

    void RefreshAllGhost()
    {
        foreach (var ghost in BoxGhost.AllGhosts)
        {
            ghost.Refresh(this);
        }
    }

    IEnumerator PlayBuildAnim(GameObject obj)
    {
        obj.SetActive(true);

        Transform t = obj.transform;

        Vector3 targetPos = t.position;
        Vector3 startPos = targetPos + Vector3.up * 0.3f;

        Vector3 targetScale = t.localScale;
        Vector3 startScale = Vector3.zero;

        t.position = startPos;
        t.localScale = startScale;

        float duration = 1f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float p = timer / duration;

            // 缓动
            p = 1f - Mathf.Pow(1f - p, 3f);

            t.position = Vector3.Lerp(startPos, targetPos, p);
            t.localScale = Vector3.Lerp(startScale, targetScale, p);

            yield return null;
        }

        //while (timer < duration)
        //{
        //    timer += Time.deltaTime;

        //    float p = timer / duration;

        //    p = Mathf.Sin(p * Mathf.PI * 0.5f);

        //    t.position = Vector3.Lerp(startPos, targetPos, p);

        //    float scaleP = p;

        //    if (p > 0.8f)
        //    {
        //        scaleP += Mathf.Sin((p - 0.8f) * 15f) * 0.05f;
        //    }

        //    t.localScale = targetScale * scaleP;

        //    yield return null;
        //}

        t.position = targetPos;
        t.localScale = targetScale;
    }
}
