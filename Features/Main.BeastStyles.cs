using System;
using System.Globalization;
using SharpDX;
using Color = SharpDX.Color;

namespace Beasts;

// Per-beast style overrides (Price Data -> Tracked Beasts). Every getter falls back to the global setting when the
// beast has no override for that field.
public partial class Main
{
    internal const int MinFontSize = 8;
    internal const int MaxFontSize = 64;
    internal const float MinCircleRadius = 20f;
    private const float DefaultBaseFontHeight = 16f;
    private static readonly TimeSpan BeastStyleSaveDelay = TimeSpan.FromSeconds(1);

    // Height of the HUD font at text scale 1; font sizes in pixels are drawn as SetTextScale(size / this).
    private float _baseFontHeight = DefaultBaseFontHeight;
    private DateTime? _beastStylesChangedUtc;

    private BeastStyle GetBeastStyleOrNull(string beastName) =>
        beastName != null && Settings.BeastPrices.BeastStyles.TryGetValue(beastName, out var style) ? style : null;

    // While a net is on the beast the capture colors take over, so the capture state stays readable.
    private Color GetBeastWorldNameColor(string beastName, BeastCaptureState captureState) =>
        captureState == BeastCaptureState.None && TryParseStyleColor(GetBeastStyleOrNull(beastName)?.TextColor, out var color)
            ? color
            : GetWorldBeastColor(captureState);

    private Color GetBeastMapTextColor(string beastName) =>
        TryParseStyleColor(GetBeastStyleOrNull(beastName)?.TextColor, out var color)
            ? color
            : Settings.MapRender.LargeMapLabels.TextColor.Value;

    private Color GetBeastTrackedWindowColor(string beastName) =>
        TryParseStyleColor(GetBeastStyleOrNull(beastName)?.TextColor, out var color)
            ? color
            : GetTrackedWindowBeastColor();

    private Color GetBeastCircleColor(string beastName, BeastCaptureState captureState) =>
        captureState == BeastCaptureState.None && TryParseStyleColor(GetBeastStyleOrNull(beastName)?.CircleColor, out var color)
            ? color
            : GetWorldBeastCircleColor(captureState);

    private int GetBeastWorldFontSize(string beastName) =>
        ClampFontSize(GetBeastStyleOrNull(beastName)?.FontSize ?? Settings.MapRender.Layout.WorldLabelFontSize.Value);

    private int GetBeastMapFontSize(string beastName) =>
        ClampFontSize(GetBeastStyleOrNull(beastName)?.FontSize ?? Settings.MapRender.LargeMapLabels.FontSize.Value);

    private float GetBeastCircleRadius(string beastName) =>
        ClampCircleRadius(GetBeastStyleOrNull(beastName)?.CircleRadius ?? Settings.MapRender.Layout.WorldBeastCircleRadius.Value);

    internal static int ClampFontSize(int size) => Math.Clamp(size, MinFontSize, MaxFontSize);

    internal static float ClampCircleRadius(float radius) => Math.Clamp(radius, MinCircleRadius, MapRenderLayoutSettings.MaxCircleRadius);

    internal static bool TryParseStyleColor(string text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex += "FF";
        }

        if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
        {
            return false;
        }

        color = new Color((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
        return true;
    }

    internal static string FormatStyleColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    private void UpdateBeastStyle(string beastName, Action<BeastStyle> update)
    {
        var styles = Settings.BeastPrices.BeastStyles;
        if (!styles.TryGetValue(beastName, out var style))
        {
            style = new BeastStyle();
        }

        update(style);

        if (style.IsEmpty)
        {
            styles.Remove(beastName);
        }
        else
        {
            styles[beastName] = style;
        }

        _beastStylesChangedUtc = DateTime.UtcNow;
    }

    // Dragging a size or color fires every frame; write the settings file once things settle instead.
    private void FlushBeastStylesIfDue(DateTime now)
    {
        if (_beastStylesChangedUtc is not { } changedUtc || now - changedUtc < BeastStyleSaveDelay)
        {
            return;
        }

        _beastStylesChangedUtc = null;
        SavePersistedBeastPriceSettings();
    }

    private void RefreshBaseFontHeight()
    {
        var height = Graphics.MeasureText("Ag").Y;
        _baseFontHeight = height > 1f ? height : DefaultBaseFontHeight;
    }
}
