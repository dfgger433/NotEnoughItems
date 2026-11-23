using System;
using System.Reflection;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NotEnoughItems;

/// <summary>Supplies a read-only catalog preview to the stock item tooltip generator.</summary>
internal static class NativeTooltipPreview
{
    private static readonly MethodInfo? ItemAwake = AccessTools.DeclaredMethod(typeof(Item), "Awake");
    private static readonly MethodInfo? BatteryAwake = AccessTools.DeclaredMethod(typeof(BatteryItem), "Awake");
    private static readonly MethodInfo? ContainerAwake = AccessTools.DeclaredMethod(typeof(Container), "Awake");
    private static readonly MethodInfo? LiquidAwake = AccessTools.DeclaredMethod(typeof(WaterContainerItem), "Awake");
    private static GameObject? _root;
    private static CatalogItem? _entry;
    private static Item? _preview;

    internal static Item? Get(CatalogItem entry)
    {
        if (ReferenceEquals(entry, _entry) && (_root is null || _root)) return _preview;
        Release();
        if (entry.NativeItemInfo == null || Item.GlobalItems?.ContainsKey(entry.Id) != true || !PlayerCamera.main)
            return null;

        _entry = entry;
        try
        {
            var prefab = Resources.Load<GameObject>(entry.Id);
            if (!prefab && ItemRegistry.TryGetCustomInfo(entry.Id, out _)) prefab = CustomInstantiate.ResolvePrefab(entry.Id);
            if (prefab && !prefab.GetComponent<Item>()) prefab = null;

            // The parent is inactive before we instantiate the clone. It never reaches
            // Start, which would register it as an actual game item.
            _root = new GameObject("NEI Tooltip Preview") { hideFlags = HideFlags.HideAndDontSave };
            _root.SetActive(false);
            Item preview;
            if (prefab)
                preview = Object.Instantiate(prefab!, _root.transform, false).GetComponent<Item>();
            else
            {
                var definition = new GameObject("Item Definition");
                definition.transform.SetParent(_root.transform, false);
                preview = definition.AddComponent<Item>();
            }
            preview.id = entry.Id;
            // The tooltip reads component state populated by these native Awake methods.
            // Call only those initializers, not arbitrary scripts on the prefab.
            ItemAwake?.Invoke(preview, null);
            if (preview.TryGetComponent<Container>(out var container)) ContainerAwake?.Invoke(container, null);
            if (preview.TryGetComponent<BatteryItem>(out var battery))
            {
                BatteryAwake?.Invoke(battery, null);
                if (!battery.hasBattery) preview.condition = 0f;
            }
            if (preview.TryGetComponent<WaterContainerItem>(out var liquid))
            {
                LiquidAwake?.Invoke(liquid, null);
                if (liquid.Capacity > 0f) liquid.UpdateCondition();
            }
            return _preview = preview;
        }
        catch (Exception)
        {
            // Do not retry a broken preview on every repaint of the same slot.
            Release();
            _entry = entry;
            return null;
        }
    }

    internal static void Release()
    {
        if (_root != null && _root) Object.Destroy(_root);
        _root = null;
        _entry = null;
        _preview = null;
    }
}
