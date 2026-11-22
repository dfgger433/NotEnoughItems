using System.Reflection;
using NotEnoughItems;
using UnityEngine.EventSystems;

internal static class TooltipSmoke
{
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
        var resolver = assembly.GetType("NotEnoughItems.NativeTooltip", throwOnError: true)!
            .GetMethod("ForItem", BindingFlags.NonPublic | BindingFlags.Static)!;
        var item = new CatalogItem("test_virtual", "<size=200>Virtual", "<sprite index=4>Body", kind: CatalogItemKind.Virtual);
        var result = ((string, string))resolver.Invoke(null, [item.DisplayName, item, "<color=red>NEI note", null])!;
        if (result.Item1 != "＜size=200＞Virtual" || result.Item2 != "＜sprite index=4＞Body\n\n＜color=red＞NEI note")
            throw new InvalidOperationException("Provider and NEI-only text must remain literal in the native rich text renderer.");
        var vanilla = new CatalogItem("test", "Name", "<color=#fff><sprite index=4>Typed description", kind: CatalogItemKind.Liquid);
        result = ((string, string))resolver.Invoke(null, [vanilla.DisplayName, vanilla, null, null])!;
        if (result.Item2 != vanilla.Description) throw new InvalidOperationException("Original formatting must pass through untouched.");
        var endpoint = typeof(UIUtil).GetMethod("GetUITooltip", [typeof(List<RaycastResult>)])!;
        if (endpoint.ReturnType != typeof((string, string)) || typeof(GlobalDark).GetMethod("HandleTooltip") == null ||
            typeof(PlayerCamera).GetMethod("ItemHoverDescription", [typeof(Item)])?.ReturnType != typeof((string, string)))
            throw new InvalidOperationException("The installed game must expose the native tooltip path.");
    }
}
