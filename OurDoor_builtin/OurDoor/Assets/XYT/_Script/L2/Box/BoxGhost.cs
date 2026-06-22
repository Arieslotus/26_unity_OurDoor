using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxGhost : MonoBehaviour
{
    public static List<BoxGhost> AllGhosts = new();

    public int boxID;

    Renderer[] renderers;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        AllGhosts.Add(this);
    }

    void OnDestroy()
    {
        AllGhosts.Remove(this);
    }

    // 根据目标box是否已搭建，决定ghost的渲染是否显示
    public void Refresh(BoxManager manager)
    {
        bool visible = !manager.IsBuilt(boxID);

        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }
}
