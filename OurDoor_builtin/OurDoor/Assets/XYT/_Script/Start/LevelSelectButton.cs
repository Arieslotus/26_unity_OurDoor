using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class LevelSelectButton : MonoBehaviour
{
    public int levelID;

    public string sceneName;

    public TMP_Text titleText;
    UIStartController uiController;

    bool unlocked;

    void Start()
    {
        uiController = GameObject.FindObjectOfType<UIStartController>();

        unlocked = GameManager.Instance.IsLevelUnlocked(levelID);

        titleText.color = unlocked ? Color.white : Color.gray;
    }

    public void OnClick()
    {
        if (!unlocked)
            return;

        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {

        if (uiController != null)
        {
            uiController.FadeIn();
        }

        yield return new WaitForSeconds(1);

        if(levelID == 1 || levelID == 2)
            GameManager.Instance.UnlockLevel(levelID + 1);
        SceneManager.LoadScene(sceneName);
    }
}