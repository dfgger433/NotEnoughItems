using System.Reflection;
using NotEnoughItems;
using UnityEngine;
using UnityEngine.EventSystems;

internal static class TooltipSmoke
{
    private const BindingFlags NonPublic = BindingFlags.Static | BindingFlags.NonPublic;

    internal static void DragEndpoints()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Type[] raycasts = [typeof(List<RaycastResult>)];
        Type[] withUsable = [typeof(List<RaycastResult>), typeof(UsableObject).MakeByRefType()];
        foreach (var (name, parameters) in new (string, Type[])[]
        {
            ("HandleInput", Type.EmptyTypes), ("HandleDragging", withUsable), ("HandleStartDragging", withUsable),
            ("HandleWhileDragging", raycasts), ("TryPerformWorldActions", Type.EmptyTypes)
        })
        {
            if (typeof(PlayerCamera).GetMethod(name, instance, null, parameters, null)?.ReturnType != typeof(void))
                throw new InvalidOperationException($"The installed game does not expose the expected drag hook: {name}.");
        }
    }

    internal static void BookmarkNativeEndpoints()
    {
        const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
        var craft = typeof(Catalog).Assembly.GetType("NotEnoughItems.BookmarkCrafting", true)!;
        var check = craft.GetMethod("CheckSelection", methods)!.GetParameters();
        if (typeof(global::Recipe).GetMethod("GetItemsForRecipe", Type.EmptyTypes)?.ReturnType != typeof(List<Item>) ||
            check[0].ParameterType != typeof(global::Recipe) || check[1].ParameterType != typeof(List<Item>).MakeByRefType() ||
            typeof(RecipeResult).GetMethod("SpawnResult", [typeof(int)])?.ReturnType != typeof(void) ||
            typeof(PlayerCamera).GetMethod("TryCraft", Type.EmptyTypes)?.ReturnType != typeof(void) ||
            typeof(Body).GetMethod("PickUpItem", [typeof(Item), typeof(int), typeof(bool)]) == null)
            throw new InvalidOperationException("The installed game no longer exposes the verified craft/transfer hooks.");
        var normalize = typeof(NotEnoughItems.Recipe).GetMethod("NativeOutput", NonPublic)!;
        var bottle = (RecipeOutput)normalize.Invoke(null, [new RecipeResult { id = "water", amount = 1, isLiquid = true, resultCondition = 50f }])!;
        var batch = (RecipeOutput)normalize.Invoke(null, [new RecipeResult { id = "stick", amount = 2, resultCondition = .75f }])!;
        if (bottle.Amount != 50 || batch.Amount != 2 || batch.Condition != .75f)
            throw new InvalidOperationException("Native liquid delivery volume and solid batch size use different fields.");
    }

    internal static void SupportedIngredientNames()
    {
        var listNames = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.NativeTooltip", true)!
            .GetMethod("SupportedNames", NonPublic)!;
        string[] Names(RecipeIngredient ingredient, params CatalogItem[] items) =>
            (string[])listNames.Invoke(null, [ingredient, items])!;

        CatalogItem Candidate(string id, string label, float amount, CatalogItemKind kind = CatalogItemKind.Item) =>
            new(id, label, kind: kind, tags: ["metal"], qualities: [new CraftingQuality("grip", amount)]);
        var strong = Candidate("pliers", "<b>Pliers</b>", 3);
        var weak = Candidate("tongs", "Weak tongs", 1);
        var ignored = Candidate("broken", "Broken pliers", 4);
        var second = Candidate("vise", "<sprite index=0>", 4);
        var wrongType = Candidate("fluid", "Metal liquid", 4, CatalogItemKind.Liquid);
        var requirement = new RecipeIngredient("", 2, isCatalyst: true, qualityId: "grip", ignoredItemId: "broken", requiredTag: "metal");
        if (!Names(requirement, strong, weak, ignored, second, wrongType).SequenceEqual(["Pliers", "vise"]))
            throw new InvalidOperationException("The hover list must include every eligible item, strip native markup, and reject weaker, ignored and wrong-type candidates.");
        if (Names(requirement, weak, ignored, wrongType).Length != 0 ||
            !Names(requirement, weak, strong).SequenceEqual(["Pliers"]))
            throw new InvalidOperationException("Zero and one eligible matches must not gain phantom alternatives.");
    }

    internal static void ItemContent()
    {
        var native = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.NativeTooltip", true)!;
        var forItem = native.GetMethod("ForItem", NonPublic)!;
        (string? Name, string? Description, string? Note, string? Id, string? IdText, string? ModText) Read(CatalogItem item, string? note = null,
            bool expanded = false, bool chinese = false, float? volume = null, bool showId = true)
        {
            var result = forItem.Invoke(null, [item.DisplayName, item, note, null, expanded, chinese, volume, showId])!;
            string? Get(string name) => (string?)result.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result);
            return (Get("Name"), Get("Description"), Get("Note"), Get("Id"), Get("IdText"), Get("ModText"));
        }

        var item = new CatalogItem("rope", "Rope", "<color=green>A coiled rope.");
        if (Read(item, "Quantity: 3") != ("Rope", "<color=green>A coiled rope.", "Quantity: 3", "rope", "Item ID: rope", "Casualties: Unknown") ||
            Read(item, "Quantity: 3", showId: false) != ("Rope", "<color=green>A coiled rope.", "Quantity: 3", null, null, "Casualties: Unknown"))
            throw new InvalidOperationException("The setting must hide the ID without affecting native markup, notes or the plain mod name.");
        var displayOnly = new CatalogItem("example:pliers", "Pliers", "Tool.", kind: CatalogItemKind.Virtual,
            source: "example.tools", qualities: [new CraftingQuality("grip", 2)]);
        if (Read(displayOnly, expanded: true, chinese: true) != ("Pliers", "Tool.", null, "example:pliers", "条目 ID：example:pliers", "example.tools"))
            throw new InvalidOperationException("A provider-only entry must preserve its own raw ID and declared owner without gaining a native detail block.");

        // Water uses the game's cleanwater locale alias; quality amounts come from LiquidType.
        var prior = Locale.currentLang;
        try
        {
            Locale.currentLang = new Language();
            Locale.currentLang.other["cleanwater"] = "Localized water";
            Locale.currentLang.other["cleanwaterdsc"] = "Native liquid description.";
            Locale.currentLang.other["itemqualities"] = "Qualities: ";
            Locale.currentLang.other["cqwater"] = "Wet";
            var water = new CatalogItem("water", "Water", "Catalog fallback.", kind: CatalogItemKind.Liquid, source: "example.fluids");
            var plain = native.GetMethod("PlainGameText", NonPublic)!;
            string Strip(string? input) => (string)plain.Invoke(null, [input])!;
            var collapsed = Read(water, chinese: true, volume: 12f);
            var expanded = Read(water, expanded: true, volume: 12f);
            if (Strip(collapsed.Name) != "Localized water" || Strip(collapsed.Description) != "Native liquid description." ||
                collapsed.Id != "water" || collapsed.IdText != "流体 ID：water" || collapsed.ModText != "example.fluids" ||
                Read(water, showId: false).Id != null ||
                Strip(expanded.Description) != "Native liquid description.\n\nQualities: Wet (12)" ||
                !expanded.Description!.Contains("<alpha=#FF>", StringComparison.Ordinal))
                throw new InvalidOperationException("Liquids must read their native locale alias and expand scaled qualities while retaining the raw fluid ID and owner.");
        }
        finally { Locale.currentLang = prior; }
    }

    internal static void IdSettingAndInput()
    {
        var window = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.NeiWindow", true)!;
        var bind = window.GetMethod("BindTooltipId", NonPublic)!;
        string path = Path.Combine(AppContext.BaseDirectory, $"nei-tooltip-{Guid.NewGuid():N}.cfg");
        BepInEx.Configuration.ConfigEntry<bool> Reload() => (BepInEx.Configuration.ConfigEntry<bool>)bind.Invoke(null,
            [new BepInEx.Configuration.ConfigFile(path, saveOnInit: false)])!;
        try
        {
            var setting = Reload();
            if (!setting.Value) throw new InvalidOperationException("Previously visible IDs must stay visible by default.");
            setting.Value = false;
            if (Reload().Value) throw new InvalidOperationException("Hiding IDs must survive reloading the config.");
            setting.Value = true;
            if (!Reload().Value) throw new InvalidOperationException("Turning IDs back on must survive reloading the config.");
        }
        finally { File.Delete(path); }

        var copy = window.GetMethod("CopyTooltipValueAt", NonPublic)!;
        var row = new Rect(100, 110, 200, 14);
        string? Copy(string? value, bool shift, bool requireShift = true, EventType type = EventType.MouseDown, int button = 0, Vector2? pointer = null) =>
            (string?)copy.Invoke(null, [value, row, pointer ?? row.center, type, button, shift, requireShift]);
        if (Copy("example:pliers", shift: true) != "example:pliers" ||
            Copy("example.tools", shift: false, requireShift: false) != "example.tools" ||
            Copy(null, shift: true) != null || Copy("water", shift: false) != null ||
            Copy("water", shift: true, button: 1) != null || Copy("water", shift: true, type: EventType.MouseUp) != null ||
            Copy("water", shift: true, pointer: row.center + new Vector2(0, row.height)) != null ||
            Copy("example.tools", shift: true, requireShift: false, button: 1) != null)
            throw new InvalidOperationException("IDs copy only on Shift-left-click; mod names copy on plain left-click without activating rows or other buttons.");
    }

    internal static void Colors()
    {
        var format = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.TooltipText", true)!.GetMethod("Format", NonPublic)!;
        (string Rich, string Shadow, string Plain) Format(string input) => ((string, string, string))format.Invoke(null, [input, "FFFFFF"])!;
        var stack = Format("<color=#f00><sprite index=2 tint=1><alpha=#88>A<br/><color=\"green\">B</color>C</color> <1% <>");
        if (stack.Rich != "<color=#FF000088>A\n</color><color=#00FF00FF>B</color><color=#FF0000FF>C</color> <1% <>" ||
            stack.Shadow != "<color=#19182788>A\n</color><color=#191827FF>B</color><color=#191827FF>C</color> <1% <>" ||
            stack.Plain != "A\nBC <1% <>")
            throw new InvalidOperationException("TMP color switching, alpha, line breaks, literal angle text and stack restoration must survive IMGUI adaptation.");
        var open = Format("<#1234>title\n<color=orange><i>hint</i>");
        if (open.Rich != "<color=#11223344>title\n</color><i><color=#FF8000FF>hint</color></i>")
            throw new InvalidOperationException("Unclosed game colors must terminate at the block boundary, including around italic spans.");
    }

    internal static void Run()
    {
        var assembly = typeof(CatalogItem).Assembly;
        var native = assembly.GetType("NotEnoughItems.NativeTooltip", throwOnError: true)!;
        var suppress = native.GetMethod("Suppress", NonPublic)!;
        bool Suppress(bool overlay, bool hit) => (bool)suppress.Invoke(null, [overlay, hit])!;
        if (!Suppress(true, true) || Suppress(true, false) || Suppress(false, true))
            throw new InvalidOperationException("Only a region hit on an active overlay may hide the stock tooltip.");

        // Captured pointer state or native dragging must never be part of the decision:
        // a hit outside every drawn NEI rectangle keeps the vanilla tooltip unchanged.
        var hit = native.GetMethod("Hit", NonPublic)!;
        var region = typeof((bool Visible, Rect Rect));
        var visible = Array.CreateInstance(region, 2);
        visible.SetValue((true, new Rect(10, 10, 100, 100)), 0);
        visible.SetValue((false, new Rect(0, 0, 500, 500)), 1);
        bool Hit(Vector2 point) => (bool)hit.Invoke(null, [point, visible])!;
        if (!Hit(new Vector2(20, 20)) || Hit(new Vector2(5, 5)))
            throw new InvalidOperationException("The visible NEI rectangle, not its exterior, must bound suppression.");
        var hidden = Array.CreateInstance(region, 1);
        hidden.SetValue((false, new Rect(0, 0, 500, 500)), 0);
        if ((bool)hit.Invoke(null, [new Vector2(50, 50), hidden])! ||
            (bool)hit.Invoke(null, [new Vector2(50, 50), new[] { (true, new Rect(0, 0, 0, 100)) }])!)
            throw new InvalidOperationException("Hidden or zero-area NEI controls cannot suppress the stock tooltip.");

        var layer = assembly.GetType("NotEnoughItems.NativeTooltipLayer", throwOnError: true)!;
        var hide = layer.GetMethod("Hide", NonPublic);
        if (hide == null || hide.GetParameters() is not [{ ParameterType: var hiddenType }] ||
            hiddenType != typeof(GlobalDark) || layer.GetMethod("Raise", NonPublic) != null)
            throw new InvalidOperationException("NEI must hide the stock root, not raise or composite it.");

        var surface = assembly.GetType("NotEnoughItems.GameSurface", throwOnError: true)!;
        foreach (string legacy in new[] { "FeedNeiTooltip", "UseOriginalItemTooltip", "PlaceNeiTooltip" })
            if (surface.GetMethod(legacy, NonPublic) != null)
                throw new InvalidOperationException($"Legacy native-tooltip injection still exists: {legacy}.");

        var plugin = assembly.GetType("NotEnoughItems.Plugin", throwOnError: true)!;
        var window = assembly.GetType("NotEnoughItems.NeiWindow", throwOnError: true)!;
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        if (plugin.GetMethod("TryGetItemTooltip", instance) != null ||
            window.GetMethod("TryGetItemTooltip", instance) != null ||
            plugin.GetProperty("SuppressNativeTooltip", instance) == null ||
            window.GetMethod("NativeTooltipHit", instance)?.GetParameters() is not [{ ParameterType: var pointerType }] ||
            pointerType != typeof(Vector2))
            throw new InvalidOperationException("NEI must hit-test its visible regions instead of injecting GlobalDark content.");

        var dark = typeof(GlobalDark);
        if (dark.GetMethod("HandleTooltip", Type.EmptyTypes)?.ReturnType != typeof(void) ||
            dark.GetField("tooltipRect") == null || dark.GetField("tooltipText") == null ||
            dark.GetMethod("SetTooltip", [typeof((string, string))]) == null)
            throw new InvalidOperationException("The installed GlobalDark still needs its stock tooltip state for a safe hide.");

        var endpoint = typeof(UIUtil).GetMethod("GetUITooltip", [typeof(List<RaycastResult>)]);
        if (endpoint == null || endpoint.ReturnType != typeof((string, string)) ||
            typeof(PlayerCamera).GetMethod("ItemHoverDescription", [typeof(Item)])?.ReturnType != typeof((string, string)) ||
            typeof(PlayerCamera).GetField("alwaysExpandDescriptions")?.FieldType != typeof(bool))
            throw new InvalidOperationException("The installed game must expose the native tooltip and expansion path.");
        foreach (Type component in new[] { typeof(Item), typeof(Container), typeof(BatteryItem), typeof(WaterContainerItem) })
            if (component.GetMethod("Awake", instance, null, Type.EmptyTypes, null)?.ReturnType != typeof(void))
                throw new InvalidOperationException($"The inactive catalog preview must be able to initialize {component.Name} for the native formatter.");
    }
}
