using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制 PC 交互准星和提示文字。
/// </summary>
public class PCInteractionPrompt : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Image crosshair;
    [SerializeField] private TMP_Text promptText;

    [Header("显示设置")]
    [SerializeField] private string defaultPrompt = "按 E 交互";
    string currentPrompt = "";
    
    PCInteractor interactor;

    private void Awake()
    {
        HidePrompt();

        interactor = FindObjectOfType<PCInteractor>();
    }

    private void Update()
    {
        if (interactor == null) return;

        if (interactor.CanInteract)
        {
            currentPrompt = interactor.CurrentWorldInteractable.InteractionPrompt;
            ShowPrompt(currentPrompt);
        }
        else
        {
            currentPrompt = "";
            HidePrompt();
        }
    }
    public void ShowPrompt(string message)
    {
        //Debug.Log(message);

        if (crosshair != null)
            SetCrosshairAlpha(100f);

        if (promptText != null)
        {
            promptText.text = string.IsNullOrWhiteSpace(message)
                ? defaultPrompt
                : message;
            promptText.gameObject.SetActive(true);
        }
    }

    public void HidePrompt()
    {
        if (crosshair != null)
            SetCrosshairAlpha(10f); 

        if (promptText != null)
        {
            promptText.text = string.Empty;
            promptText.gameObject.SetActive(false);
        }
    }

    private void SetCrosshairAlpha(float alpha)
    {
        if (crosshair == null) return;

        Color color = crosshair.color;
        color.a = alpha / 100f;
        crosshair.color = color;
    }
}
