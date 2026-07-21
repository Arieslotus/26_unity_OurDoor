/// <summary>
/// PC 端可交互物体的统一接口。
/// 此接口不依赖 XR 或 PICO。
/// </summary>
public interface IPCInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(PCInteractor interactor);
    void Interact(PCInteractor interactor);
}
