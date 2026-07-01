using TMPro;
using UnityEngine;

public class PlayerSelector : MonoBehaviour
{
    public TMP_Text playerText;

    readonly string[] names =
    {
        "门外",
        "门内"
    };

    void Start()
    {
        Refresh();
    }

    public void Left()
    {
        int value = (int)GameManager.Instance.CurrentPlayerRole;

        value--;
        if (value < 0)
            value = names.Length - 1;

        GameManager.Instance.SetPlayerRole((GameManager.PlayerRole)value);

        Refresh();
    }

    public void Right()
    {
        int value = (int)GameManager.Instance.CurrentPlayerRole;

        value++;
        if (value >= names.Length)
            value = 0;

        GameManager.Instance.SetPlayerRole((GameManager.PlayerRole)value);

        Refresh();
    }

    void Refresh()
    {
        playerText.text =
            names[(int)GameManager.Instance.CurrentPlayerRole];
    }
}