using Godot;

namespace MatchThree.View;

/// <summary>
/// Device display safe-area helpers: converts the Android notch/cutout inset
/// from screen pixels into viewport pixels so the HUD can clear the camera
/// cutout on real phones while leaving desktop/editor layouts alone. Also
/// holds the shared HUD-bar geometry (height + gap) that Hud and BoardView
/// both read, so the bar's strip and the board's reserved space always agree.
///
/// Godot's DisplayServer.GetDisplaySafeArea() is Android-only and reports the
/// safe rectangle in screen pixels (empty everywhere else). Layout code works
/// in the stretched 540x960 design canvas (stretch/mode = canvas_items), so
/// the inset is scaled by the "window pixels per canvas unit" ratio before it
/// is exposed.
/// </summary>
public static class SafeArea
{
    /// <summary>
    /// Baseline clearance below the cutout / screen top edge for the
    /// bar+board block (in viewport px): the block is centered below this
    /// line and never sits flush against the screen top on notch-less
    /// devices. Shared by BoardView and Hud via SafeArea.
    /// </summary>
    public const float MarginPx = 12f;

    /// <summary>
    /// Height of the HUD bar in viewport px — matches the HUD Menu button's
    /// custom_minimum_size (120x56 in Game.tscn) so the content fills the
    /// bar exactly. Lives here so BoardView can reserve the bar's space
    /// without knowing Hud's internals.
    /// </summary>
    public const float HudBarHeightPx = 56f;

    /// <summary>Clearance between the HUD bar's bottom edge and the board's
    /// top edge, in viewport px.</summary>
    public const float HudGapPx = 16f;

    private static float? _topInsetPx;

    /// <summary>
    /// Top safe-area inset (camera-cutout clearance) in viewport px: 0 on
    /// desktop/editor runs and on devices that report no cutout. Lazily
    /// computed once on first access and cached, so Hud.cs and BoardView.cs
    /// read the same value independently.
    /// </summary>
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
        // GetDisplaySafeArea() is Android-only; everywhere else it reports no
        // cutout. Guarding on the OS keeps editor/desktop runs at 0.
        if (OS.GetName() != "Android") return 0f;

        var safe = DisplayServer.GetDisplaySafeArea();
        // A zero/negative rect means "no cutout reported" (e.g. devices
        // without a notch) — treat it like a normal screen.
        if (safe.Position.Y <= 0 || safe.Size.Y <= 0) return 0f;

        // The rect is in screen pixels and the game runs fullscreen, so the
        // inset converts to canvas units by the screen-height-to-visible-
        // viewport-height ratio (the stretch scale factor). The root
        // window's visible rect is the same coordinate space
        // GetViewportRect() reports to Node2Ds (540x960 canvas, expanded in
        // height on tall phones by stretch/aspect = expand).
        if (Godot.Engine.GetMainLoop() is not SceneTree tree) return 0f;
        var screenHeightPx = Mathf.Max(1f, DisplayServer.ScreenGetSize().Y);
        var visibleHeight = Mathf.Max(1f, tree.Root.GetVisibleRect().Size.Y);
        var pxPerCanvasUnit = screenHeightPx / visibleHeight;
        return safe.Position.Y / pxPerCanvasUnit;
    }
}