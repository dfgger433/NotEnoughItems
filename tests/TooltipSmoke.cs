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
            typeof(PlayerCamera).GetMethod("ItemHoverDescription", [typeof(Item)])?.ReturnType != typeof((string, string)))
            throw new InvalidOperationException("The installed game must expose the native tooltip path.");
    }
}
