using Godot;

namespace MatchThree.View;

public static class SafeArea
{
    public const float MarginPx = 12f;

    public const float HudBarHeightPx = 56f;

    public const float HudGapPx = 16f;

    private static float? _topInsetPx;

    public static float TopInsetPx
    {
        get
        {
            _topInsetPx ??= ComputeTopInsetPx();
            return _topInsetPx.Value;
        }
    }

    private static float ComputeTopInsetPx()
    {
        if (OS.GetName() != "Android") return 0f;

        var safe = DisplayServer.GetDisplaySafeArea();
        if (safe.Position.Y <= 0 || safe.Size.Y <= 0) return 0f;

        if (Godot.Engine.GetMainLoop() is not SceneTree tree) return 0f;
        var screenHeightPx = Mathf.Max(1f, DisplayServer.ScreenGetSize().Y);
        var visibleHeight = Mathf.Max(1f, tree.Root.GetVisibleRect().Size.Y);
        var pxPerCanvasUnit = screenHeightPx / visibleHeight;
        return safe.Position.Y / pxPerCanvasUnit;
    }
}