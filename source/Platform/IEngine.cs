namespace Burntime.Platform;

public interface IEngine
{
    void CenterMouse();
    void IncreaseLoadingCount();
    void DecreaseLoadingCount();
    void WaitForBlend();

    float Blend { get; set; }
    float BlendSpeed { get; set; }
    bool BlockBlend { get; set; }
    bool IsBlended { get; }

    Vector2 GameResolution { get; }
    Vector2 Resolution { get; set; }

    DeviceManager DeviceManager { get; set; }
}
