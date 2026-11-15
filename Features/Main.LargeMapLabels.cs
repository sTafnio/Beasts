using System;
using System.Collections.Generic;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using MapElement = ExileCore.PoEMemory.Elements.Map;
using Vector2 = System.Numerics.Vector2;

namespace Beasts;

// Beast names on the large map overlay (Tab), positioned the same way MinimapIcons positions its icons.
public partial class Main
{
    // Some HUD builds read IngameUi.Map from a stale offset after a game patch (the 2026-11-14 build read +0x708 while
    // the map element sat at +0x718), which leaves LargeMap unreadable. The HUD's own Map is used whenever it is
    // valid, so this fallback does nothing on a HUD that reads it correctly.
    private const int FallbackMapElementOffset = 0x718;

    private void DrawLargeMapLabels(IReadOnlyList<TrackedBeastMapMarkerInfo> beasts)
    {
        if (beasts.Count == 0)
        {
            return;
        }

        var largeMap = TryGetVisibleLargeMap();
        if (largeMap == null)
        {
            return;
        }

        var playerRender = GameController?.Player?.GetComponent<Render>();
        var ingameData = GameController?.IngameState?.Data;
        if (playerRender == null || ingameData == null)
        {
            return;
        }

        var labelSettings = Settings.MapRender.LargeMapLabels;
        var replaceWithStatus = Settings.MapRender.CapturedText.ReplaceNameAndPriceWithStatusText.Value;
        var includePrice = labelSettings.ShowPrice.Value;
        var showBackground = labelSettings.ShowBackground.Value;
        var backgroundColor = labelSettings.BackgroundColor.Value;
        var padding = new Vector2(labelSettings.PaddingX.Value, labelSettings.PaddingY.Value);
        var labelOffset = new Vector2(0, labelSettings.VerticalOffset.Value);

        var playerGridPos = playerRender.PosNum.WorldToGrid();
        var playerHeight = -playerRender.UnclampedHeight;
        var mapCenter = largeMap.MapCenter;
        var mapScale = largeMap.MapScale;

        foreach (var beast in beasts)
        {
            var gridPos = GetCurrentGridPos(beast);
            var delta = gridPos - playerGridPos;
            var deltaZ = (playerHeight + ingameData.GetTerrainHeightAt(gridPos)) * PoeMapExtension.WorldToGridConversion;
            var mapPos = mapCenter + mapScale * new Vector2(
                (delta.X - delta.Y) * CameraAngleCos,
                (deltaZ - (delta.X + delta.Y)) * CameraAngleSin);

            BuildMapMarkerTexts(beast.BeastName, beast.CaptureState, includePrice, out var primaryText, out var secondaryText);
            var statusColor = GetDisplayedCaptureStatusColor(beast.CaptureState);
            var primaryColor = beast.CaptureState != BeastCaptureState.None && replaceWithStatus
                ? statusColor
                : GetBeastMapTextColor(beast.BeastName);

            MapRenderDrawingPrimitives.DrawMapLabel(
                primaryText,
                secondaryText,
                mapPos + labelOffset,
                primaryColor,
                statusColor,
                GetBeastMapFontSize(beast.BeastName),
                showBackground,
                backgroundColor,
                padding);
        }
    }

    // The overlay list refreshes every 75 ms; read live beasts' position directly so moving labels don't stutter.
    // Beasts out of network range keep their last known position.
    private Vector2 GetCurrentGridPos(TrackedBeastMapMarkerInfo beast)
    {
        if (beast.IsLive &&
            _trackedBeastEntities.TryGetValue(beast.EntityId, out var entity) &&
            entity?.IsValid == true &&
            entity.GetComponent<Positioned>() is { } positioned)
        {
            return positioned.GridPosNum;
        }

        return beast.GridPos;
    }

    private SubMap TryGetVisibleLargeMap()
    {
        var ingameUi = GameController?.IngameState?.IngameUi;
        if (ingameUi == null)
        {
            return null;
        }

        var map = ResolveMapElement(ingameUi);
        if (map == null)
        {
            return null;
        }

        // Same order as MinimapIcons: while the corner minimap is up, the large map is not what the player sees.
        if (map.SmallMiniMap is { IsValid: true, IsVisibleLocal: true })
        {
            return null;
        }

        return map.LargeMap is { IsVisibleLocal: true } largeMap ? largeMap : null;
    }

    private MapElement ResolveMapElement(IngameUIElements ingameUi)
    {
        var hudMap = ingameUi.Map;
        if (IsValidMapElement(hudMap))
        {
            return hudMap;
        }

        try
        {
            var candidate = GameController.Game.GetObject<MapElement>(
                GameController.Memory.Read<long>(ingameUi.Address + FallbackMapElementOffset));
            return IsValidMapElement(candidate) ? candidate : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsValidMapElement(MapElement map) =>
        map is { Address: not 0 } &&
        map.LargeMap is { Address: not 0 } large && large.Parent?.Address == map.Address &&
        map.SmallMiniMap is { Address: not 0 } small && small.Parent?.Address == map.Address;
}
