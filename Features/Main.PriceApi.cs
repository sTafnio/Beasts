using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using ExileCore;
using Newtonsoft.Json;

namespace Beasts;

public partial class Main
{
    private static readonly Vector4 EnabledBeastTextColor = new(0.4f, 1f, 0.4f, 1f);
    private static readonly HttpClient HttpClient = new();
    private const string PoeNinjaItemOverviewEndpoint = "economy/stash/current/item/overview";
    private const string PoeNinjaExchangeOverviewEndpoint = "economy/exchange/current/overview";
    private static readonly string[] PoeNinjaItemOverviewTypes =
    [
        "Scarab",
        "Map",
        "Fragment",
        "Currency",
        "Invitation",
    ];
    private static readonly Dictionary<string, string> PoeNinjaOverviewEndpointByType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Beast"] = PoeNinjaItemOverviewEndpoint,
        ["Scarab"] = PoeNinjaExchangeOverviewEndpoint,
        ["Map"] = PoeNinjaItemOverviewEndpoint,
        ["Fragment"] = PoeNinjaExchangeOverviewEndpoint,
        ["Currency"] = PoeNinjaExchangeOverviewEndpoint,
        ["Invitation"] = PoeNinjaItemOverviewEndpoint,
    };

    private Dictionary<string, float> _beastPrices = AllRedBeasts.ToDictionary(x => x.Name, _ => -1f);
    private Dictionary<string, float> _marketItemPrices = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, float> _mapTierAveragePrices = new();
    private Dictionary<string, string> _beastPriceTexts = new(StringComparer.OrdinalIgnoreCase);
    private TrackedBeast[] _sortedBeastsByPrice = AllRedBeasts;
    private bool _isFetchingPrices;
    private DateTime _lastPriceFetchAttempt = DateTime.MinValue;

    private const ImGuiColorEditFlags StyleSwatchFlags =
        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoOptions |
        ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf;

    private void DrawBeastPickerPanel()
    {
        ImGui.TextUnformatted($"Prices as of: {Settings.BeastPrices.LastUpdated}");
        ImGui.TextDisabled("Text / Size / Circle / Radius are per-beast overrides: click or drag to change.");
        ImGui.TextDisabled("Right-click a cell, or press x, to go back to the global setting.");
        ImGui.Separator();

        if (!ImGui.BeginTable("##BeastPickerTable", 8,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV |
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
                new Vector2(0, 400)))
            return;

        // The HUD's ImGui has asserts on: a skipped EndTable() aborts the whole HUD.
        try
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 24);
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthFixed, 34);
            ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 66);
            ImGui.TableSetupColumn("Circle", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Radius", ImGuiTableColumnFlags.WidthFixed, 66);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 22);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var enabledBeasts = Settings.BeastPrices.EnabledBeasts;
            var colors = Settings.MapRender.Colors;

            foreach (var beast in _sortedBeastsByPrice)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var isEnabled = enabledBeasts.Contains(beast.Name);
                if (ImGui.Checkbox($"##{beast.Name}_chk", ref isEnabled))
                {
                    if (isEnabled) enabledBeasts.Add(beast.Name);
                    else enabledBeasts.Remove(beast.Name);

                    SavePersistedBeastPriceSettings();
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(TryGetBeastPriceText(beast.Name, out var priceText) ? priceText : "?");

                ImGui.TableNextColumn();
                if (isEnabled)
                    ImGui.TextColored(EnabledBeastTextColor, beast.Name);
                else
                    ImGui.TextDisabled(beast.Name);

                var style = GetBeastStyleOrNull(beast.Name);

                ImGui.TableNextColumn();
                DrawBeastStyleColorCell(
                    $"##text_{beast.Name}",
                    beast.Name,
                    style?.TextColor,
                    colors.WorldBeastColor.Value,
                    "Text color",
                    "world labels, large map labels and the tracked beasts window",
                    (s, value) => s.TextColor = value);

                ImGui.TableNextColumn();
                DrawBeastStyleFontSizeCell(beast.Name, style);

                ImGui.TableNextColumn();
                DrawBeastStyleColorCell(
                    $"##circle_{beast.Name}",
                    beast.Name,
                    style?.CircleColor,
                    colors.WorldBeastCircleColor.Value,
                    "Circle color",
                    "the ground circle (the capture circle colors still show while it is netted)",
                    (s, value) => s.CircleColor = value);

                ImGui.TableNextColumn();
                DrawBeastStyleRadiusCell(beast.Name, style);

                ImGui.TableNextColumn();
                if (style is { IsEmpty: false })
                {
                    if (ImGui.SmallButton($"x##reset_{beast.Name}"))
                    {
                        UpdateBeastStyle(beast.Name, s =>
                        {
                            s.TextColor = null;
                            s.FontSize = null;
                            s.CircleColor = null;
                            s.CircleRadius = null;
                        });
                    }

                    DrawItemTooltip("Reset this beast to the global settings.");
                }
            }
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private void DrawBeastStyleColorCell(
        string id,
        string beastName,
        string overrideColor,
        SharpDX.Color globalColor,
        string what,
        string appliesTo,
        Action<BeastStyle, string> setOverride)
    {
        var hasOverride = TryParseStyleColor(overrideColor, out var customColor);
        var value = BeastsHelpers.ToImGuiColor(hasOverride ? customColor : globalColor);
        if (ImGui.ColorEdit4(id, ref value, StyleSwatchFlags))
        {
            var picked = FormatStyleColor(new SharpDX.Color(value.X, value.Y, value.Z, value.W));
            UpdateBeastStyle(beastName, s => setOverride(s, picked));
        }

        if (hasOverride && ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            UpdateBeastStyle(beastName, s => setOverride(s, null));
        }

        DrawItemTooltip(hasOverride
            ? $"{what} for {appliesTo}: custom {FormatStyleColor(customColor)}.\nRight-click to use the global color again."
            : $"{what} for {appliesTo}: global setting.\nClick to pick a color for this beast only.");
    }

    private void DrawBeastStyleFontSizeCell(string beastName, BeastStyle style)
    {
        var hasOverride = style?.FontSize != null;
        var value = hasOverride ? ClampFontSize(style.FontSize.Value) : Settings.MapRender.Layout.WorldLabelFontSize.Value;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt($"##size_{beastName}", ref value, 0.2f, MinFontSize, MaxFontSize,
                hasOverride ? "%d px" : "%d (def)", ImGuiSliderFlags.AlwaysClamp))
        {
            var picked = ClampFontSize(value);
            UpdateBeastStyle(beastName, s => s.FontSize = picked);
        }

        if (hasOverride && ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            UpdateBeastStyle(beastName, s => s.FontSize = null);
        }

        var layout = Settings.MapRender.Layout;
        var mapLabels = Settings.MapRender.LargeMapLabels;
        DrawItemTooltip(hasOverride
            ? $"Font size for world and large map labels: custom {value} px.\nRight-click to use the global sizes again."
            : $"Font size: global setting (world labels {layout.WorldLabelFontSize.Value} px, large map labels {mapLabels.FontSize.Value} px).\nDrag sideways, or Ctrl+click to type, to set a size for this beast only.");
    }

    private void DrawBeastStyleRadiusCell(string beastName, BeastStyle style)
    {
        var hasOverride = style?.CircleRadius != null;
        var value = hasOverride ? ClampCircleRadius(style.CircleRadius.Value) : Settings.MapRender.Layout.WorldBeastCircleRadius.Value;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragFloat($"##radius_{beastName}", ref value, 2f, MinCircleRadius, MapRenderLayoutSettings.MaxCircleRadius,
                hasOverride ? "%.0f" : "%.0f (def)", ImGuiSliderFlags.AlwaysClamp))
        {
            var picked = ClampCircleRadius(value);
            UpdateBeastStyle(beastName, s => s.CircleRadius = picked);
        }

        if (hasOverride && ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            UpdateBeastStyle(beastName, s => s.CircleRadius = null);
        }

        DrawItemTooltip(hasOverride
            ? $"Ground circle radius: custom {value:0} world units.\nRight-click to use the global radius again."
            : $"Ground circle radius: global setting ({Settings.MapRender.Layout.WorldBeastCircleRadius.Value:0} world units).\nDrag sideways, or Ctrl+click to type, to set a size for this beast only.");
    }

    private static void DrawItemTooltip(string text)
    {
        if (!ImGui.IsItemHovered() || !ImGui.BeginTooltip())
        {
            return;
        }

        try
        {
            ImGui.TextUnformatted(text);
        }
        finally
        {
            ImGui.EndTooltip();
        }
    }

    private void SelectAllPriceDataBeasts()
    {
        SetAllPriceDataBeastsEnabled(true);
    }

    private void DeselectAllPriceDataBeasts()
    {
        SetAllPriceDataBeastsEnabled(false);
    }

    private void SelectPriceDataBeastsWorth15ChaosOrMore()
    {
        SetEnabledPriceDataBeasts(beast =>
            _beastPrices.TryGetValue(beast.Name, out var price) && price >= 15f);
    }

    private void SetAllPriceDataBeastsEnabled(bool isEnabled)
    {
        SetEnabledPriceDataBeasts(beast => isEnabled);
    }

    private void SetEnabledPriceDataBeasts(Func<TrackedBeast, bool> predicate)
    {
        var enabledBeasts = Settings.BeastPrices.EnabledBeasts;
        enabledBeasts.Clear();

        if (predicate != null)
        {
            enabledBeasts.UnionWith(AllRedBeasts.Where(predicate).Select(x => x.Name));
        }

        SavePersistedBeastPriceSettings();
    }

    private async Task FetchBeastPricesAsync()
    {
        if (_isFetchingPrices) return;
        _isFetchingPrices = true;
        _lastPriceFetchAttempt = DateTime.UtcNow;
        try
        {
            Log("Fetching beast prices from poe.ninja...");
            var league = Uri.EscapeDataString(Settings.BeastPrices.League.Value?.Trim() ?? "Mirage");

            var beastUrl = BuildPoeNinjaOverviewUrl(league, "Beast");

            var beastJson = await HttpClient.GetStringAsync(beastUrl);
            var beastResponse = JsonConvert.DeserializeObject<PoeNinjaOverviewResponse>(beastJson);
            if (beastResponse?.Lines == null) return;

            var lookup = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var beastNamesById = BuildPoeNinjaItemNameById(beastResponse);
            foreach (var line in beastResponse.Lines)
            {
                var lineName = BeastData.CurrentName(GetPoeNinjaLineName(line, beastNamesById));
                var chaosValue = GetPoeNinjaLineChaosValue(line);
                if (string.IsNullOrWhiteSpace(lineName) || chaosValue <= 0)
                {
                    continue;
                }

                if (!lookup.TryGetValue(lineName, out var existingPrice) || chaosValue > existingPrice)
                {
                    lookup[lineName] = chaosValue;
                }
            }

            var updated = AllRedBeasts.ToDictionary(
                b => b.Name,
                b => lookup.TryGetValue(b.Name, out var price) ? price : -1f,
                StringComparer.OrdinalIgnoreCase);

            _beastPrices = updated;
            RebuildPriceCaches(updated);

            var marketItemPrices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var mapTierBuckets = new Dictionary<int, List<float>>();

            foreach (var type in PoeNinjaItemOverviewTypes)
            {
                try
                {
                    var url = BuildPoeNinjaOverviewUrl(league, type);
                    var json = await HttpClient.GetStringAsync(url);
                    var response = JsonConvert.DeserializeObject<PoeNinjaOverviewResponse>(json);
                    if (response?.Lines == null)
                    {
                        continue;
                    }

                    var namesById = BuildPoeNinjaItemNameById(response);

                    foreach (var line in response.Lines)
                    {
                        var lineName = GetPoeNinjaLineName(line, namesById);
                        var chaosValue = GetPoeNinjaLineChaosValue(line);
                        if (string.IsNullOrWhiteSpace(lineName) || chaosValue <= 0)
                        {
                            continue;
                        }

                        marketItemPrices[lineName] = chaosValue;

                        var mapTier = GetPoeNinjaLineMapTier(line, lineName);
                        if (mapTier.HasValue)
                        {
                            if (!mapTierBuckets.TryGetValue(mapTier.Value, out var bucket))
                            {
                                bucket = new List<float>();
                                mapTierBuckets[mapTier.Value] = bucket;
                            }

                            bucket.Add(chaosValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Skipping poe.ninja {type} prices. {ex.GetType().Name}: {ex.Message}");
                }
            }

            _marketItemPrices = marketItemPrices;
            _mapTierAveragePrices = mapTierBuckets.ToDictionary(
                x => x.Key,
                x => x.Value.Count > 0 ? x.Value.Average() : 0f);

            Settings.BeastPrices.LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            SavePersistedBeastPriceSettings();
            Log($"Beast + item prices updated ({Settings.BeastPrices.LastUpdated}).");
        }
        catch (Exception ex)
        {
            LogError("Failed to fetch beast prices", ex);
        }
        finally
        {
            _isFetchingPrices = false;
        }
    }

    private static string BuildPoeNinjaOverviewUrl(string escapedLeague, string type)
    {
        if (!PoeNinjaOverviewEndpointByType.TryGetValue(type ?? string.Empty, out var endpoint))
        {
            endpoint = PoeNinjaItemOverviewEndpoint;
        }

        return $"https://poe.ninja/poe1/api/{endpoint}?league={escapedLeague}&type={Uri.EscapeDataString(type ?? string.Empty)}";
    }

    private static Dictionary<string, string> BuildPoeNinjaItemNameById(PoeNinjaOverviewResponse response)
    {
        var namesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response?.Items == null)
        {
            return namesById;
        }

        foreach (var item in response.Items)
        {
            if (string.IsNullOrWhiteSpace(item?.Id) || string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            namesById[item.Id] = item.Name;
        }

        return namesById;
    }

    private static string GetPoeNinjaLineName(PoeNinjaOverviewLine line, IReadOnlyDictionary<string, string> namesById)
    {
        if (!string.IsNullOrWhiteSpace(line?.Id) && namesById != null && namesById.TryGetValue(line.Id, out var nameById))
        {
            return nameById;
        }

        if (!string.IsNullOrWhiteSpace(line?.Name))
        {
            return line.Name;
        }

        return !string.IsNullOrWhiteSpace(line?.CurrencyTypeName)
            ? line.CurrencyTypeName
            : string.Empty;
    }

    private static float GetPoeNinjaLineChaosValue(PoeNinjaOverviewLine line)
    {
        if (line == null)
        {
            return -1f;
        }

        if (line.PrimaryValue > 0)
        {
            return line.PrimaryValue.Value;
        }

        if (line.ChaosValue > 0)
        {
            return line.ChaosValue.Value;
        }

        if (line.ChaosEquivalent > 0)
        {
            return line.ChaosEquivalent.Value;
        }

        return -1f;
    }

    private static int? GetPoeNinjaLineMapTier(PoeNinjaOverviewLine line, string lineName)
    {
        if (line?.MapTier > 0)
        {
            return line.MapTier;
        }

        if (string.IsNullOrWhiteSpace(lineName))
        {
            return null;
        }

        var tierMatch = System.Text.RegularExpressions.Regex.Match(
            lineName,
            @"\(\s*Tier\s*(\d+)\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!tierMatch.Success || !int.TryParse(tierMatch.Groups[1].Value, out var parsedTier) || parsedTier <= 0)
        {
            return null;
        }

        return parsedTier;
    }

    private void RebuildPriceCaches(Dictionary<string, float> prices)
    {
        _beastPriceTexts = AllRedBeasts
            .Where(b => prices.TryGetValue(b.Name, out var p) && p >= 0)
            .ToDictionary(b => b.Name, b => $"{prices[b.Name]:0}c", StringComparer.OrdinalIgnoreCase);

        _sortedBeastsByPrice = AllRedBeasts
            .OrderByDescending(b => prices.TryGetValue(b.Name, out var price) ? price : -1f)
            .ToArray();
    }

    private bool TryGetConfiguredItemPriceChaos(string configuredName, out double chaosValue)
    {
        chaosValue = 0d;
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            return false;
        }

        var normalized = configuredName.Trim();
        if (_marketItemPrices.TryGetValue(normalized, out var directPrice) && directPrice > 0)
        {
            chaosValue = directPrice;
            return true;
        }

        var mapTierMatch = System.Text.RegularExpressions.Regex.Match(normalized, @"^Map \(Tier\s*(\d+)\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (mapTierMatch.Success && int.TryParse(mapTierMatch.Groups[1].Value, out var tier) &&
            _mapTierAveragePrices.TryGetValue(tier, out var tierAvg) && tierAvg > 0)
        {
            chaosValue = tierAvg;
            return true;
        }

        return false;
    }

    private class PoeNinjaOverviewResponse
    {
        [JsonProperty("items")] public List<PoeNinjaOverviewItem> Items { get; set; }
        [JsonProperty("lines")] public List<PoeNinjaOverviewLine> Lines { get; set; }
    }

    private class PoeNinjaOverviewItem
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }

    private class PoeNinjaOverviewLine
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("currencyTypeName")] public string CurrencyTypeName { get; set; }
        [JsonProperty("primaryValue")] public float? PrimaryValue { get; set; }
        [JsonProperty("chaosValue")] public float? ChaosValue { get; set; }
        [JsonProperty("chaosEquivalent")] public float? ChaosEquivalent { get; set; }
        [JsonProperty("mapTier")] public int? MapTier { get; set; }
    }
}

