using System;
using System.Collections.Generic;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Beasts.Runtime.Features;

internal sealed record MapRenderDrawingPrimitivesCallbacks(
    Func<Vector3, Vector2> WorldToScreen,
    Func<float> GetWorldBeastCircleFillOpacityPercent,
    Func<float> GetWorldBeastCircleOutlineThickness,
    Func<Color> GetWorldTextOutlineColor,
    Func<Vector2[]> GetWorldCircleScreenPoints,
    Func<Vector2[]> GetWorldCircleOutlinePoints,
    IReadOnlyList<Vector2> WorldCirclePoints,
    Action<string, Vector2, Color> DrawCenteredText,
    Action<Vector2[], Color> DrawConvexPolyFilled,
    Action<Vector2[], Color, float> DrawPolyLine,
    Action<RectangleF, Color> DrawBox,
    Func<string, Vector2> MeasureText,
    Func<float, IDisposable> SetTextScale,
    Func<float> GetBaseFontHeight);

internal sealed class MapRenderDrawingPrimitivesService
{
    private readonly MapRenderDrawingPrimitivesCallbacks _callbacks;

    public MapRenderDrawingPrimitivesService(MapRenderDrawingPrimitivesCallbacks callbacks)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public void DrawCenteredText(string text, Vector2 position, Color color)
    {
        _callbacks.DrawCenteredText(text, position, color);
    }

    public void DrawOutlinedText(string text, Vector2 position, Color color)
    {
        DrawOutlinedTextAtCurrentScale(text, position, color, 1f);
    }

    /// <summary>Outlined, horizontally centered text whose glyphs are <paramref name="fontSize"/> pixels tall.</summary>
    public void DrawOutlinedText(string text, Vector2 position, Color color, float fontSize)
    {
        var scale = GetTextScale(fontSize);
        using (_callbacks.SetTextScale(scale))
        {
            DrawOutlinedTextAtCurrentScale(text, position, color, GetOutlineOffset(scale));
        }
    }

    /// <summary>The HUD text scale that makes text <paramref name="fontSize"/> pixels tall.</summary>
    public float GetTextScale(float fontSize)
    {
        var baseHeight = _callbacks.GetBaseFontHeight();
        return baseHeight > 0f && fontSize > 0f ? fontSize / baseHeight : 1f;
    }

    /// <summary>
    /// A one- or two-line label centered on <paramref name="center"/>, optionally on a filled box. Without the box the
    /// text is outlined so it stays readable on the map.
    /// </summary>
    public void DrawMapLabel(
        string primaryText,
        string secondaryText,
        Vector2 center,
        Color primaryColor,
        Color secondaryColor,
        float fontSize,
        bool showBackground,
        Color backgroundColor,
        Vector2 padding)
    {
        if (string.IsNullOrEmpty(primaryText))
        {
            return;
        }

        var scale = GetTextScale(fontSize);
        using (_callbacks.SetTextScale(scale))
        {
            var hasSecondaryText = !string.IsNullOrEmpty(secondaryText);
            var primarySize = _callbacks.MeasureText(primaryText);
            var secondarySize = hasSecondaryText ? _callbacks.MeasureText(secondaryText) : Vector2.Zero;
            var width = Math.Max(primarySize.X, secondarySize.X);
            var height = primarySize.Y + secondarySize.Y;
            var top = center.Y - height / 2f;

            if (showBackground)
            {
                _callbacks.DrawBox(
                    new RectangleF(center.X - width / 2f - padding.X, top - padding.Y, width + padding.X * 2f, height + padding.Y * 2f),
                    backgroundColor);
            }

            var outlineOffset = GetOutlineOffset(scale);
            DrawLabelLine(primaryText, new Vector2(center.X, top), primaryColor, showBackground, outlineOffset);
            if (hasSecondaryText)
            {
                DrawLabelLine(secondaryText, new Vector2(center.X, top + primarySize.Y), secondaryColor, showBackground, outlineOffset);
            }
        }
    }

    public void DrawFilledCircleInWorld(Vector3 position, float radius, Color color)
    {
        var screenPoints = _callbacks.GetWorldCircleScreenPoints();
        var outlinePoints = _callbacks.GetWorldCircleOutlinePoints();
        for (var i = 0; i < _callbacks.WorldCirclePoints.Count; i++)
        {
            var point = _callbacks.WorldCirclePoints[i];
            screenPoints[i] = _callbacks.WorldToScreen(position + new Vector3(point.X * radius, point.Y * radius, 0));
            outlinePoints[i] = screenPoints[i];
        }

        outlinePoints[^1] = screenPoints[0];

        var fillOpacity = _callbacks.GetWorldBeastCircleFillOpacityPercent() / 100f;
        _callbacks.DrawConvexPolyFilled(screenPoints, color with { A = Color.ToByte((int)(fillOpacity * 255)) });
        _callbacks.DrawPolyLine(outlinePoints, color, _callbacks.GetWorldBeastCircleOutlineThickness());
    }

    private void DrawLabelLine(string text, Vector2 position, Color color, bool plain, float outlineOffset)
    {
        if (plain)
        {
            _callbacks.DrawCenteredText(text, position, color);
        }
        else
        {
            DrawOutlinedTextAtCurrentScale(text, position, color, outlineOffset);
        }
    }

    private void DrawOutlinedTextAtCurrentScale(string text, Vector2 position, Color color, float offset)
    {
        var outlineColor = _callbacks.GetWorldTextOutlineColor();
        _callbacks.DrawCenteredText(text, position + new Vector2(-offset, -offset), outlineColor);
        _callbacks.DrawCenteredText(text, position + new Vector2(-offset, offset), outlineColor);
        _callbacks.DrawCenteredText(text, position + new Vector2(offset, -offset), outlineColor);
        _callbacks.DrawCenteredText(text, position + new Vector2(offset, offset), outlineColor);
        _callbacks.DrawCenteredText(text, position, color);
    }

    // A 1 px outline disappears on big text; grow it with the scale (1 px at 16 px, 2 px at 32 px, ...).
    private static float GetOutlineOffset(float scale) => MathF.Max(1f, MathF.Round(scale));
}
