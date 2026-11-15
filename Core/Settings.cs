using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace Beasts;

public class Settings : ISettings
{
    [Menu("Enabled", "Enable or disable the Beasts plugin.")]
    public ToggleNode Enable { get; set; } = new(false);

    [Menu("Debug Logs", "Write detailed logs to aid troubleshooting.")]
    public ToggleNode DebugLogging { get; set; } = new(false);

    [Menu("Auto-Hide Overlays", "Hide all beast overlays automatically when you are in hideout/town or when any panel (inventory, stash, Atlas, Bestiary) is open.")]
    public ToggleNode AutoHideOverlays { get; set; } = new(true);

    [Menu("Refresh Prices", "Fetch the latest beast prices from poe.ninja for the configured league.")]
    public ButtonNode RefreshPrices { get; set; } = new();

    [Menu("Overview", "Current league, tracked-beast count, and price feed status.")]
    [JsonIgnore]
    public CustomNode OverviewPanel { get; set; } = new();

    [Menu("Configuration")]
    [JsonIgnore]
    public CustomNode ConfigurationHeader { get; set; } = new();

    [Menu("Price Data", "Fetch beast prices from poe.ninja and choose which beasts are tracked as valuable.")]
    public BeastPricesSettings BeastPrices { get; set; } = new();

    [Menu("Markers & Prices", "Configure in-world beast labels and price overlays on inventory, stash, and Bestiary panels.")]
    public MapRenderSettings MapRender { get; set; } = new();

    [Menu("Bestiary Clipboard", "Auto-copy (and optionally auto-paste) a search regex into the Bestiary panel when it opens.")]
    public BestiaryClipboardSettings BestiaryClipboard { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class BeastPricesSettings
{
    [Menu("League", "The league name used for poe.ninja price lookups. Must match your current league exactly, for example Mirage.")]
    public TextNode League { get; set; } = new("Mirage");

    [Menu("Auto Refresh (min)", "Automatically fetch updated beast prices from poe.ninja at this interval in minutes. Set to 0 to only refresh manually.")]
    public RangeNode<int> AutoRefreshMinutes { get; set; } = new(10, 0, 60);

    [Menu("Actions", "Refresh prices from poe.ninja and bulk-select which beasts are tracked.")]
    [JsonIgnore]
    public CustomNode ActionsRow { get; set; } = new();

    [JsonIgnore]
    internal string LastUpdated { get; set; } = "never";

    [JsonIgnore]
    internal HashSet<string> EnabledBeasts { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

    [JsonProperty("LastUpdated")]
    public string SavedLastUpdated
    {
        get => LastUpdated;
        set => LastUpdated = value ?? "never";
    }

    [JsonProperty("EnabledBeasts")]
    public List<string> SavedEnabledBeasts
    {
        get => new(EnabledBeasts);
        set => EnabledBeasts = value != null
            ? new HashSet<string>(value, System.StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    }

    // Per-beast style overrides (text color, font size, circle color, circle size), keyed by beast name.
    // A beast without an entry, or a null field, uses the global setting.
    [IgnoreMenu]
    public Dictionary<string, BeastStyle> BeastStyles { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

    [Menu("Tracked Beasts", "Pick which beasts are considered valuable, and optionally give each one its own text color, font size, circle color and circle size. Enabled beasts appear in map markers, are counted in analytics, and are included in the auto-generated Bestiary search regex.")]
    [JsonIgnore] public CustomNode BeastPickerPanel { get; set; } = new();
}

/// <summary>Per-beast overrides. Null means "use the global setting".</summary>
public sealed class BeastStyle
{
    /// <summary>Name text color for world and large-map labels, as #RRGGBBAA.</summary>
    public string TextColor { get; set; }

    /// <summary>Font size in pixels for world and large-map labels.</summary>
    public int? FontSize { get; set; }

    /// <summary>Ground circle color while the beast is not being captured, as #RRGGBBAA.</summary>
    public string CircleColor { get; set; }

    /// <summary>Ground circle radius in world units.</summary>
    public float? CircleRadius { get; set; }

    [JsonIgnore]
    public bool IsEmpty => TextColor == null && FontSize == null && CircleColor == null && CircleRadius == null;
}

[Submenu(CollapsedByDefault = true)]
public class BestiaryClipboardSettings
{
    [Menu("Enable Auto Copy", "Automatically copy a search regex to the clipboard the moment the Bestiary captured-beasts panel opens.")]
    public ToggleNode EnableAutoCopy { get; set; } = new(true);

    [Menu("Auto Paste After Copy", "After copying the regex, also automatically paste it into the Bestiary search field and press Enter so matching beasts are filtered immediately.")]
    public ToggleNode AutoPasteAfterCopy { get; set; } = new(true);

    [Menu("Generate Regex From Enabled Beasts", "Automatically build the search regex from your Enabled Beasts list in Price Data. When disabled, the Manual Regex field below is used instead.")]
    public ToggleNode UseAutoRegex { get; set; } = new(true);

    [Menu("Manual Regex", "A custom search regex copied to the clipboard when automatic regex generation is turned off. Edit this to match specific beasts by name fragments separated by |.")]
    public TextNode BeastRegex { get; set; } = new("id v|le m|ld h|s ho|k m|an fi|ul, f|cic c|nd sc|s, f|d bra|l pla|n, f|l cru|l cy");
}

[Submenu(CollapsedByDefault = true)]
public class MapRenderSettings
{
    [Menu("Show World Labels", "Draw floating name labels and ground circles on tracked beasts directly in the 3D game world.")]
    public ToggleNode ShowBeastLabelsInWorld { get; set; } = new(true);

    [Menu("Large Map Labels", "Beast names on the large map overlay (the Tab map): on/off, price, background, color, font size and position.")]
    public LargeMapLabelSettings LargeMapLabels { get; set; } = new();

    [Menu("Show Tracked Beasts Window", "Show a small floating window that lists all currently alive tracked beasts in the area with their names, prices, and capture status.")]
    public ToggleNode ShowTrackedBeastsWindow { get; set; } = new(true);

    [Menu("Show Prices On Captured Beasts", "Show the poe.ninja price on captured beast items in your inventory, stash, and the Bestiary captured-beasts panel.")]
    public ToggleNode ShowPricesOnCapturedBeasts { get; set; } = new(true);

    [Menu("Only Show Enabled Beasts", "When on, only beasts you have checked in Price Data → Enabled Beasts are shown on markers, overlays, and the tracked beasts window. When off, every recognised beast (rare or unique) is shown.")]
    public ToggleNode ShowEnabledOnly { get; set; } = new(true);

    [Menu("Captured Status Text", "Customize the text and colors shown on beast labels during the two capture stages: the initial capture-in-progress and the final safe-to-leave captured state.")]
    public CapturedTextDisplaySettings CapturedText { get; set; } = new();

    [Menu("Colors", "Customize the colors used by in-world beast labels, ground circles, and the tracked beasts window. Large map label colors are under Large Map Labels.")]
    public MapRenderColorSettings Colors { get; set; } = new();

    [Menu("Layout", "Adjust font size, spacing, and circle size, thickness and fill for in-world beast labels and ground circles.")]
    public MapRenderLayoutSettings Layout { get; set; } = new();

}

[Submenu(CollapsedByDefault = true)]
public class LargeMapLabelSettings
{
    [Menu("Show Large Map Labels", "Draw tracked beast names on the large map overlay (Tab), the way MinimapIcons draws unique monster names. Beasts that walked out of range stay labelled at their last known position.")]
    public ToggleNode Show { get; set; } = new(true);

    [Menu("Show Price", "Add the poe.ninja price after the beast name on large map labels.")]
    public ToggleNode ShowPrice { get; set; } = new(true);

    [Menu("Show Background", "Draw a filled box behind large map labels. When off, the text gets an outline instead (World Text Outline Color).")]
    public ToggleNode ShowBackground { get; set; } = new(false);

    [Menu("Text Color", "Name color on large map labels. A beast with its own text color in Price Data → Tracked Beasts uses that instead.")]
    public ColorNode TextColor { get; set; } = new(Color.White);

    [Menu("Background Color", "Color of the box behind large map labels when Show Background is on.")]
    public ColorNode BackgroundColor { get; set; } = new(new Color(0, 0, 0, 200));

    [Menu("Font Size", "Font size in pixels for large map labels. A beast with its own font size in Price Data → Tracked Beasts uses that instead.")]
    public RangeNode<int> FontSize { get; set; } = new(16, 8, 64);

    [Menu("Height Offset", "Moves the label up (negative) or down (positive) from the beast's position on the map, in pixels.")]
    public RangeNode<float> VerticalOffset { get; set; } = new(-14f, -80f, 80f);

    [Menu("Background Padding X", "Horizontal padding in pixels between the text and the edge of the background box.")]
    public RangeNode<float> PaddingX { get; set; } = new(4f, 0f, 20f);

    [Menu("Background Padding Y", "Vertical padding in pixels between the text and the edge of the background box.")]
    public RangeNode<float> PaddingY { get; set; } = new(2f, 0f, 20f);
}

[Submenu(CollapsedByDefault = true)]
public class CapturedTextDisplaySettings
{
    [Menu("Capture Text Only", "When on, a beast being captured shows only the status text (e.g. 'Capturing') with no name or price. When off, the name and price remain visible with the status text added below.")]
    public ToggleNode ReplaceNameAndPriceWithStatusText { get; set; } = new(false);

    [Menu("Capturing Text", "Text shown on the label while a beast is being captured (first stage, net has been thrown). Change this to any text you prefer.")]
    public TextNode StatusText { get; set; } = new("Capturing");

    [Menu("Captured Text", "Text shown on the label after a beast is fully captured (second stage, safe to leave the map). Change this to any text you prefer.")]
    public TextNode CapturedStatusText { get; set; } = new("Caught");

    [Menu("Capture Text Color", "Color of the capturing status text (first stage) shown in world labels, map labels, and the tracked beasts window.")]
    public ColorNode CaptureTextColor { get; set; } = new(new Color(57, 255, 20, 255));

    [Menu("Captured Text Color", "Color of the captured status text (second stage, safe to leave) shown in world labels, map labels, and the tracked beasts window.")]
    public ColorNode CapturedTextColor { get; set; } = new(new Color(120, 220, 255, 255));
}

[Submenu(CollapsedByDefault = true)]
public class MapRenderColorSettings
{
    [Menu("World Beast Text Color", "Name text color for tracked beasts in the 3D world that have not been captured yet. A beast with its own text color in Price Data → Tracked Beasts uses that instead.")]
    public ColorNode WorldBeastColor { get; set; } = new(new Color(180, 20, 20, 255));

    [Menu("World Captured Beast Text Color", "Name text color for tracked beasts in the 3D world that are currently being captured or have already been safely captured.")]
    public ColorNode WorldCapturedBeastColor { get; set; } = new(new Color(255, 40, 40, 255));

    [Menu("World Price Text Color", "Color of the price text shown below beast names on in-world labels.")]
    public ColorNode WorldPriceTextColor { get; set; } = new(new Color(255, 235, 120, 255));

    [Menu("World Text Outline Color", "Color of the outline drawn behind in-world label text (and large map labels without a background) to keep it readable against bright or busy backgrounds.")]
    public ColorNode WorldTextOutlineColor { get; set; } = new(Color.Black);

    [Menu("World Beast Circle Color", "Color of the ground circle drawn around tracked beasts that have not been captured yet. A beast with its own circle color in Price Data → Tracked Beasts uses that instead.")]
    public ColorNode WorldBeastCircleColor { get; set; } = new(new Color(180, 20, 20, 255));

    [Menu("World Capture Circle Color", "Color of the ground circle while a beast is actively being captured (first stage).")]
    public ColorNode WorldCaptureRingColor { get; set; } = new(Color.White);

    [Menu("World Catched Circle Color", "Color of the ground circle after a beast is fully captured and it is safe to leave the map (second stage).")]
    public ColorNode WorldCapturedCircleColor { get; set; } = new(new Color(120, 220, 255, 255));

    [Menu("Tracked Window Beast Color", "Text color for beast names in the floating tracked beasts window. A beast with its own text color in Price Data → Tracked Beasts uses that instead.")]
    public ColorNode TrackedWindowBeastColor { get; set; } = new(new Color(180, 20, 20, 255));
}

[Submenu(CollapsedByDefault = true)]
public class MapRenderLayoutSettings
{
    [Menu("World Label Font Size", "Font size in pixels for in-world beast labels (name, price, capture status). A beast with its own font size in Price Data → Tracked Beasts uses that instead.")]
    public RangeNode<int> WorldLabelFontSize { get; set; } = new(16, 8, 64);

    [Menu("World Label Line Spacing", "Vertical spacing in pixels between lines on in-world beast labels (name, price, capture status), at the 16 px font size. It grows and shrinks with the font size.")]
    public RangeNode<float> WorldTextLineSpacing { get; set; } = new(18f, 8f, 40f);

    [Menu("World Beast Circle Radius", "Radius of the ground circle drawn around tracked beasts, in game world units (it scales with the camera like the game's own ground effects). A beast with its own circle size in Price Data → Tracked Beasts uses that instead.")]
    public RangeNode<float> WorldBeastCircleRadius { get; set; } = new(80f, 20f, MaxCircleRadius);

    internal const float MaxCircleRadius = 800f;

    [Menu("World Circle Outline Thickness", "Thickness of the ground circle outline in pixels.")]
    public RangeNode<float> WorldBeastCircleOutlineThickness { get; set; } = new(2f, 1f, 8f);

    [Menu("World Circle Fill Opacity (%)", "How opaque the filled area inside the ground circle is. 0 = fully transparent, 100 = fully solid.")]
    public RangeNode<int> WorldBeastCircleFillOpacityPercent { get; set; } = new(20, 0, 100);
}
