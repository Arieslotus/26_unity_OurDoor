/// <summary>
/// 实现功能：将服务端 levelId 严格映射到 OurDoor PC 关卡场景并执行加载。
/// </summary>
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OurDoorSceneRouter
{
    public static string GetSceneName(int levelId)
    {
        switch (levelId)
        {
            case 1:
                return "Shop_PC";
            case 2:
                return "School_PC";
            case 3:
                return "Hutong_PC";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(levelId),
                    $"[M2 场景路由] levelId 必须是 1、2、3，当前值={levelId}。");
        }
    }

    public static void LoadLevel(int levelId)
    {
        string sceneName = GetSceneName(levelId);
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            throw new InvalidOperationException(
                $"[M2 场景路由] 场景未加入 Build Settings 或名称错误，" +
                $"levelId={levelId}, sceneName={sceneName}。");
        }

        SceneManager.LoadScene(sceneName);
    }
}
