using TMPro;
using UnityEngine;

public class ModeSelector : MonoBehaviour
{
    public TMP_Text modeText;

    readonly string[] names =
    {
        "模拟双人",
        //"双人模式",
        //"单人模式"
    };

    void Start()
    {
        Refresh();
    }

    public void Left()
    {
        int value = (int)GameManager.Instance.CurrentGameMode;

        value--;
        if (value < 0)
            value = names.Length - 1;

        GameManager.Instance.SetGameMode((GameManager.GameMode)value);

        Refresh();
    }

    public void Right()
    {
        int value = (int)GameManager.Instance.CurrentGameMode;

        value++;
        if (value >= names.Length)
            value = 0;

        GameManager.Instance.SetGameMode((GameManager.GameMode)value);

        Refresh();
    }

    void Refresh()
    {
        modeText.text =
            names[(int)GameManager.Instance.CurrentGameMode];
    }
}