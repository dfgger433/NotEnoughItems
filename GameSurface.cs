using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NotEnoughItems;

internal sealed class GameSurface
{
    internal const string TerminalGuid = "com.unknown.inventoryterminal";
    internal enum Kind { Closed, Inventory, Terminal }
    private delegate bool ReadBounds(out Rect rect);
    private readonly Func<bool>? _terminalOpen;
    private readonly ReadBounds? _terminalBounds;
    private readonly Func<bool>? _closeTerminal;
    private readonly Func<bool>? _terminalTyping;
    private readonly Func<RectTransform?>? _terminalRoot;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly List<InvButton> _radialButtons = new();
    private RectTransform? _radialRoot, _containerRoot;
    private Transform? _wearablesRoot;
    private int _radialChildren, _wearableChildren;
    private Rect _radialOffsets;
    private Rect? _containerOffsets;
    private int _inventoryWidth, _inventoryHeight;
    private float _inventoryScale;
    private int _terminalWidth, _terminalHeight;
    private PlayerCamera? _terminalCamera;
    private int _detectedFrame = -1;
    private Kind _detected;
    private UnityEngine.Object? _detectedHost;
    private Rect _detectedBounds;
    private Canvas? _modalShield;
    internal bool TerminalTextInputFocused => _terminalTyping?.Invoke() == true;

    internal GameSurface(ManualLogSource log)
    {
        if (!Chainloader.PluginInfos.TryGetValue(TerminalGuid, out var terminal)) return;
        try
        {
            var type = terminal.Instance?.GetType();
            var api = type?.Assembly.GetType("UnknownInventoryTerminal.TerminalApi");
            _terminalTyping = (Func<bool>?)type?.GetProperty("SearchFocused", BindingFlags.NonPublic | BindingFlags.Static)?.GetGetMethod(true)?.CreateDelegate(typeof(Func<bool>));
            _terminalOpen = (Func<bool>?)api?.GetProperty("IsOpen")?.GetMethod?.CreateDelegate(typeof(Func<bool>));
            _terminalBounds = (ReadBounds?)api?.GetMethod("TryGetBounds")?.CreateDelegate(typeof(ReadBounds));
            _closeTerminal = (Func<bool>?)api?.GetMethod("TryClose")?.CreateDelegate(typeof(Func<bool>));
            _terminalRoot = (Func<RectTransform?>?)api?.GetProperty("ExtensionRoot")?.GetMethod?.CreateDelegate(typeof(Func<RectTransform?>));
            log.LogInfo(_terminalBounds != null ? "Unknown Inventory Terminal bounds API connected." :
                "Unknown Inventory Terminal is installed but does not expose the bounds API; update it to 0.4.4 or newer.");
        }
        catch (Exception error) when (error is ArgumentException || error is MemberAccessException)
        {
            log.LogWarning("Could not bind Unknown Inventory Terminal API: " + error.Message);
        }
    }

    internal Kind Detect(out Rect bounds, out UnityEngine.Object? host, bool refresh = false)
    {
        if (refresh || _detectedFrame != Time.frameCount)
        {
            _detected = DetectSurface(out var nextBounds, out _detectedHost);
            _detectedBounds = nextBounds;
            _detectedFrame = Time.frameCount;
        }
        bounds = _detectedBounds;
        host = _detectedHost;
        return _detected;
    }

    private Kind DetectSurface(out Rect bounds, out UnityEngine.Object? host)
    {
        bounds = default;
        host = null;
        var camera = PlayerCamera.main;
        if (!camera || !camera.body || !camera.body.alive || !camera.body.conscious || PauseHandler.paused ||
            (ConsoleScript.instance && ConsoleScript.instance.active) || Active(camera.craftingPanel) ||
            Active(camera.woundView) || Active(camera.tradeMenu) || Active(camera.itemCustomizer) ||
            (MinigameBase.main && MinigameBase.main.currentMinigame) || LiquidTransfer.instance)
            return Kind.Closed;
        if (_terminalOpen?.Invoke() == true)
        {
            if (_terminalBounds?.Invoke(out var terminal) != true || !ValidTerminalBounds(terminal)) return Kind.Closed;
            bounds = NeiLayout.FromScreen(terminal, Screen.height);
            if (_detected == Kind.Terminal && _terminalCamera == camera && _terminalWidth == Screen.width &&
                _terminalHeight == Screen.height && !TerminalBoundsChanged(_detectedBounds, bounds))
                bounds = _detectedBounds;
            _terminalCamera = camera;
            _terminalWidth = Screen.width;
            _terminalHeight = Screen.height;
            host = camera;
            return Kind.Terminal;
        }
        if (!camera.radialOpen || !camera.radialMenu || !camera.radialMenu.gameObject.activeInHierarchy)
            return Kind.Closed;

        bounds = InventoryBounds(camera);
        host = camera.radialMenu;
        return Kind.Inventory;
    }

    private Rect InventoryBounds(PlayerCamera camera)
    {
        var radial = camera.radialMenu;
        var container = Active(camera.containerMenu) ? camera.containerMenu.transform as RectTransform : null;
        var wearables = camera.wearPopulate ? camera.wearPopulate.transform : null;
        int wearableChildren = wearables ? wearables!.childCount : 0;
        float scale = camera.mainCanvas ? Mathf.Max(.2f, PlayerCamera.uiScale) : 1f;

        // The native camera moves the entire radial in screen space every Update. Only
        // measure local layout, never the radial's translated/animated world rectangle.
        if (_detected != Kind.Inventory || _radialRoot != radial || _containerRoot != container || _wearablesRoot != wearables ||
            _radialChildren != radial.childCount || _wearableChildren != wearableChildren ||
            _inventoryWidth != Screen.width || _inventoryHeight != Screen.height || _inventoryScale != scale)
        {
            _radialRoot = radial;
            _containerRoot = container;
            _wearablesRoot = wearables;
            _radialChildren = radial.childCount;
            _wearableChildren = wearableChildren;
            _inventoryWidth = Screen.width;
            _inventoryHeight = Screen.height;
            _inventoryScale = scale;

            // The radial's root also owns invisible drag zones and transient actions.
            // Measure the permanent inventory slots and the visible container frame instead.
            _radialOffsets = new Rect(-297, -292, 594, 601);
            radial.GetComponentsInChildren(false, _radialButtons);
            foreach (var button in _radialButtons)
            {
                if (button.transform is not RectTransform rect || (container && rect.IsChildOf(container))) continue;
                if (TryMeasureLocal(rect, radial, null, out var slot)) _radialOffsets = Union(_radialOffsets, slot);
            }
            _radialButtons.Clear();
            _containerOffsets = null;
            if (container && TryMeasureLocal(container!, radial, container, out var frame))
            {
                var title = container!.Find("Top") as RectTransform;
                if (title && title!.gameObject.activeInHierarchy && TryMeasureLocal(title, radial, container, out var header))
                    frame = Union(frame, header);
                _containerOffsets = frame;
            }
        }
        return AnchorInventory(_radialOffsets, _containerOffsets, _inventoryWidth, _inventoryHeight, _inventoryScale);
    }

    private bool TryMeasureLocal(RectTransform rect, RectTransform radial, RectTransform? animatedContainer, out Rect bounds)
    {
        bounds = default;
        var relative = Matrix4x4.identity;
        for (Transform current = rect; current != radial; current = current.parent)
        {
            if (!current) return false;
            // The container animates from 0.01 to 1 when opened. Reserve its full size
            // immediately, or the panels will slide during the animation.
            var scale = current == animatedContainer ? Vector3.one : current.localScale;
            relative = Matrix4x4.TRS(current.localPosition, current.localRotation, scale) * relative;
        }

        rect.GetLocalCorners(_corners);
        var first = relative.MultiplyPoint3x4(_corners[0]);
        float left = first.x, top = -first.y, right = first.x, bottom = -first.y;
        for (int i = 1; i < _corners.Length; i++)
        {
            var p = relative.MultiplyPoint3x4(_corners[i]);
            left = Mathf.Min(left, p.x);
            top = Mathf.Min(top, -p.y);
            right = Mathf.Max(right, p.x);
            bottom = Mathf.Max(bottom, -p.y);
        }
        bounds = Rect.MinMaxRect(left, top, right, bottom);
        return true;
    }

    internal static Rect AnchorInventory(Rect radialOffsets, Rect? containerOffsets, float width, float height, float unit)
    {
        var offsets = containerOffsets.HasValue ? Union(radialOffsets, containerOffsets.Value) : radialOffsets;
        return new Rect(width * .5f + offsets.x * unit, height * .5f + offsets.y * unit, offsets.width * unit, offsets.height * unit);
    }

    internal static bool TerminalBoundsChanged(Rect current, Rect next) =>
        Mathf.Abs(next.xMin - current.xMin) >= 1f || Mathf.Abs(next.yMin - current.yMin) >= 1f ||
        Mathf.Abs(next.xMax - current.xMax) >= 1f || Mathf.Abs(next.yMax - current.yMax) >= 1f;

    private static bool ValidTerminalBounds(Rect bounds) => bounds.width >= 1f && bounds.height >= 1f &&
        float.IsFinite(bounds.xMin) && float.IsFinite(bounds.yMin) && float.IsFinite(bounds.xMax) && float.IsFinite(bounds.yMax);

    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    internal void SetModalShield(Transform parent, bool enabled)
    {
        if (!enabled && !_modalShield) return;
        if (!_modalShield)
        {
            var root = new GameObject("NEI.ModalInput", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            _modalShield = root.GetComponent<Canvas>();
            _modalShield.renderMode = RenderMode.ScreenSpaceOverlay;
            _modalShield.sortingOrder = short.MaxValue;
            var target = new GameObject("NEI.ModalInputTarget", typeof(RectTransform), typeof(Image));
            target.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)target.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = target.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
        }
        _modalShield!.gameObject.SetActive(enabled);
    }

    internal static bool OtherTextInputFocused()
    {
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        var input = selected.GetComponentInParent<InputField>();
        var tmp = selected.GetComponentInParent<TMP_InputField>();
        return (input && input.isFocused) || (tmp && tmp.isFocused);
    }

    internal Item? HoveredItem(Kind kind)
    {
        var camera = PlayerCamera.main;
        if (!EventSystem.current || !camera || !camera.body) return null;
        Transform? root = kind == Kind.Terminal ? _terminalRoot?.Invoke()?.parent : camera.radialMenu;
        if (!root) return null;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Input.mousePosition }, hits);
        foreach (var hit in hits)
        {
            if (!hit.gameObject || !hit.gameObject.transform.IsChildOf(root)) continue;
            var label = hit.gameObject.GetComponentInParent<ItemLabel>();
            if (label && label.refItem) return label.refItem;
            var slot = hit.gameObject.GetComponentInParent<InvButton>();
            if (!slot || !slot.Overlaps(hits)) continue;
            if (slot.isBody && (camera.body.slots == null || slot.slot < 0 || slot.slot >= camera.body.slots.Length || !camera.body.slots[slot.slot])) continue;
            if (slot.TryGetItem(out var item) && item) return item;
        }
        return null;
    }

    internal static bool CanOpenCrafting(global::Recipe recipe)
    {
        var camera = PlayerCamera.main;
        int index = recipe.index;
        return camera && camera.body && !camera.dragItem && WorldGeneration.world &&
            !WorldGeneration.world.cutsceneActive && Recipes.recipes != null && index >= 0 && index < Recipes.recipes.Count &&
            ReferenceEquals(Recipes.recipes[index], recipe) && Recipes.GetVisibleRecipes(null).Contains(recipe);
    }

    internal bool OpenCrafting(Kind kind, global::Recipe recipe)
    {
        if (!CanOpenCrafting(recipe) || (kind == Kind.Terminal && _closeTerminal?.Invoke() != true)) return false;
        var camera = PlayerCamera.main;
        if (!Active(camera.craftingPanel)) camera.OpenCraftScreen();
        if (!Active(camera.craftingPanel)) return false;
        camera.ClearRecipeFilter();
        camera.SelectRecipe(recipe.index);
        return true;
    }

    private static bool Active(GameObject? go) => go != null && go.activeInHierarchy;

    internal static void InstallPatches(Harmony harmony, ManualLogSource log)
    {
        Patch(harmony, log, typeof(PlayerCamera), "HandleInput", nameof(SkipInput));
        Patch(harmony, log, typeof(PlayerCamera), "HandleStartDragging", nameof(SkipPointer));
        Patch(harmony, log, typeof(PlayerCamera), "HandleReleaseDragging", nameof(SkipRelease));
        Patch(harmony, log, typeof(PlayerCamera), "HandleWhileDragging", nameof(SkipWhileDragging));
        Patch(harmony, log, typeof(PlayerCamera), "HandleMouseOver", nameof(SkipHover));
        harmony.Patch(AccessTools.Method(typeof(UIUtil), nameof(UIUtil.GetUITooltip)),
            postfix: new HarmonyMethod(typeof(GameSurface), nameof(UseOriginalItemTooltip)) { priority = Priority.Last });
        if (Chainloader.PluginInfos.TryGetValue(TerminalGuid, out var terminal) && terminal.Instance != null)
            Patch(harmony, log, terminal.Instance.GetType(), "OnGUI", nameof(ShowTerminalChrome));
        var uei = AccessTools.TypeByName("UeiRuntime");
        if (Chainloader.PluginInfos.ContainsKey("casualtiesunknown.uei") && uei != null)
        {
            var shouldShow = AccessTools.Method(uei, "ShouldShow", new[] { typeof(PlayerCamera), typeof(string).MakeByRefType() });
            if (shouldShow != null)
                harmony.Patch(shouldShow, postfix: new HarmonyMethod(typeof(GameSurface), nameof(HideDuplicateIndex)));
        }
    }

    private static void Patch(Harmony harmony, ManualLogSource log, Type type, string target, string prefix)
    {
        var method = AccessTools.Method(type, target);
        if (method == null) { log.LogWarning($"Optional input patch not found: {type.Name}.{target}"); return; }
        try { harmony.Patch(method, prefix: new HarmonyMethod(typeof(GameSurface), prefix)); }
        catch (Exception error) { log.LogWarning($"Could not patch {type.Name}.{target}: {error.Message}"); }
    }

    // GlobalDark calls this while choosing which tooltip to show, before the stock renderer lays it out.
    private static void UseOriginalItemTooltip(ref (string, string) __result)
    {
        if (Plugin.Instance?.TryGetItemTooltip(out var itemText) == true) __result = itemText;
    }

    private static bool SkipInput(PlayerCamera __instance)
    {
        if (__instance != PlayerCamera.main) return true;
        var plugin = Plugin.Instance;
        plugin?.ReadInput();
        if (plugin?.ShouldBlockInput != true) return true;
        if (__instance.body)
        {
            __instance.body.moveDir = Vector2.zero;
            __instance.body.crouching = false;
        }
        __instance.wantsJumpInput = false;
        return false;
    }

    // Unity GUI.Window chrome is composed above ordinary IMGUI regardless of GUI.depth.
    private static bool ShowTerminalChrome() => Plugin.Instance?.CoversTerminal != true;

    private static bool SkipPointer(PlayerCamera __instance) =>
        __instance != PlayerCamera.main || Plugin.Instance?.OwnsPointer != true;

    private static bool SkipWhileDragging(PlayerCamera __instance)
    {
        if (__instance != PlayerCamera.main) return true;
        var plugin = Plugin.Instance;
        // A native drag may continue outside NEI, but NEI owns the frame while its
        // sidebar or text editor is under the pointer so favourite/customize input
        // cannot leak through to the game.
        return plugin?.OwnsPointer != true && plugin?.OwnsKeyboard != true;
    }

    private static bool SkipRelease(PlayerCamera __instance)
    {
        if (SkipPointer(__instance)) return true;
        // Cancel a native drag released over NEI without moving or destroying its item.
        __instance.dragItem = null;
        if (__instance.dragImage) __instance.dragImage.enabled = false;
        return false;
    }

    private static bool SkipHover(PlayerCamera __instance)
    {
        if (__instance != PlayerCamera.main || Plugin.Instance?.OwnsPointer != true) return true;
        if (__instance.hoverSquare) __instance.hoverSquare.enabled = false;
        return false;
    }

    private static void HideDuplicateIndex(ref bool __result, ref string hideReason)
    {
        if (!__result || Plugin.Instance?.SuppressDuplicate != true) return;
        __result = false;
        hideReason = "the Not Enough Items overlay is enabled";
    }
}
