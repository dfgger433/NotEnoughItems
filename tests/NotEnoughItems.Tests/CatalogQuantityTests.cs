using System.Reflection;
using BepInEx.Configuration;
using NotEnoughItems;

internal static class CatalogQuantityTests
{
    private const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Type Actions = typeof(Catalog).Assembly.GetType("NotEnoughItems.NeiActions", throwOnError: true)!;

    internal static void Run()
    {
        EditorAndStepper();
        ConfigAndDelivery();
        CompactLayout();
    }

    private static void EditorAndStepper()
    {
        var parse = Actions.GetMethod("TryReadGiveAmount", InternalStatic)!;
        (bool Valid, int? Amount) Read(string text)
        {
            object?[] args = [text, null];
            return ((bool)parse.Invoke(null, args)!, (int?)args[1]);
        }

        Check(Read("1") == (true, 1) && Read("12") == (true, 12) && Read("10") == (true, 10),
            "Successive digits extend the value, including a zero after a nonzero digit.");
        Check(Read(" \t064\r\n") == (true, 64) && Read("109") == (true, 64) && Read(new string('9', 1024)) == (true, 64),
            "Clipboard amounts are read as complete numbers and oversize pastes saturate without overflow.");
        Check(Read("") == (true, null) && Read("00") == (true, null), "Blank and zero drafts do not change the saved quantity.");
        foreach (string invalid in new[] { "-1", "+12", "1.5", "1 2", "3rope", "100a" })
            Check(Read(invalid) == (false, null), $"Reject nonnumeric text without accepting a partial amount: '{invalid}'.");

        var clamp = Actions.GetMethod("ClampGiveAmount", InternalStatic)!;
        int Clamp(int value) => (int)clamp.Invoke(null, [value])!;
        Check(Clamp(9 + 1) == 10 && Clamp(10 - 1) == 9 && Clamp(64 + 1) == 64 && Clamp(1 - 1) == 1,
            "The stepper crosses nine in either direction and respects both limits.");
    }

    private static void ConfigAndDelivery()
    {
        var plugin = typeof(Catalog).Assembly.GetType("NotEnoughItems.Plugin", throwOnError: true)!;
        var bind = plugin.GetMethod("BindSpawnAmount", InternalStatic)!;
        var slotAmount = Actions.GetMethod("SlotGiveAmount", InternalStatic)!;
        int FromSlot(int button, int value) => (int)slotAmount.Invoke(null, [button, value])!;

        string path = Path.Combine(AppContext.BaseDirectory, $"nei-catalog-quantity-{Guid.NewGuid():N}.cfg");
        ConfigEntry<int> Reload() => (ConfigEntry<int>)bind.Invoke(null, [new ConfigFile(path, saveOnInit: false)])!;
        try
        {
            var setting = Reload();
            Check(setting.Definition.Section == "NotEnoughItems" && setting.Definition.Key == "SpawnAmount" && setting.Value == 1,
                "The persisted key, section and default quantity remain compatible.");
            setting.Value = 37;
            setting = Reload();
            Check(setting.Value == 37 && FromSlot(0, setting.Value) == 37 && FromSlot(1, setting.Value) == 1,
                "A two-digit quantity survives reload, left click uses it and right click stays at one.");
            setting.Value = int.MaxValue;
            Check(Reload().Value == 64 && FromSlot(0, int.MaxValue) == 64, "Config and slot delivery cap invalid large values.");
            setting.Value = int.MinValue;
            Check(Reload().Value == 1 && FromSlot(0, int.MinValue) == 1, "Config and slot delivery clamp negative values to one.");
        }
        finally { File.Delete(path); }

    }

    private static void CompactLayout()
    {
        const int width = 360, height = 640, panelWidth = 65;
        float scale = NeiLayout.GuiScale(width, height, 1.4f, panelWidth, panelWidth);
        var (_, catalog, _, footer) = NeiLayout.Overlay(width / scale, height / scale, panelWidth, panelWidth);
        var layout = new NeiLayout.CatalogPanel(catalog, footer);
        Check(layout.Quantity.width >= 30f && layout.Search.width >= 22f && layout.Search.xMax < layout.Quantity.xMin &&
            layout.Quantity.xMax < layout.Increase.xMin && layout.Increase.width > 0f && layout.Decrease.width > 0f,
            "At minimum sidebar width the two digits fit and search and both step buttons remain available.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
