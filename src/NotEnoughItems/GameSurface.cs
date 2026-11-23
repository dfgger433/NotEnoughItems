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

internal sealed class NeiRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    internal NeiWindow? Window { get; set; }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        Plugin.Instance?.CanShieldPointer == true && Window?.Hit(new Vector2(screenPoint.x, Screen.height - screenPoint.y)) == true;
}

internal sealed class GameSurface
{
    internal const string TerminalGuid = "com.unknown.inventoryterminal";
    internal enum Kind { Closed, Inventory, Terminal, Crafting }
    private delegate bool ReadBounds(out Rect rect);
    private readonly Func<bool>? _terminalOpen;
    private readonly ReadBounds? _terminalBounds;
    private readonly Func<bool>? _closeTerminal;
    private readonly Func<bool>? _terminalTyping;
    private readonly Func<RectTransform?>? _terminalRoot;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly List<InvButton> _radialButtons = new();
    private RectTransform? _radialRoot;
    private Transform? _wearablesRoot;
    private int _radialChildren, _wearableChildren;
    private Rect _radialOffsets;
    private int _inventoryWidth, _inventoryHeight;
    private float _inventoryScale;
    private int _terminalWidth, _terminalHeight;
    private int _detectedFrame = -1;
    private Kind _detected;
    private UnityEngine.Object? _detectedHost;
    private Rect _detectedBounds;
    private Canvas? _modalShield;
    private NeiRaycastFilter? _inputFilter;
    internal bool TerminalTextInputFocused => _terminalTyping?.Invoke() == true;

    internal GameSurface(ManualLogSource log)
    {
        if (!Chainloader.PluginInfos.TryGetValue(TerminalGuid, out var terminal)) return;
        try
        {
            var type = terminal.Instance?.GetType();
            var api = type?.Assembly.GetType("UnknownInventoryTerminal.TerminalApi");
            _terminalTyping = (Func<bool>?)type?.GetProperty("SearchFocused", BindingFlags.NonPublic | BindingFlags.Static)?.GetGetMethod(true)?.CreateDelegate(typeof(Func<bool>));
            var terminalOpen = api?.GetProperty("IsOpen")?.GetMethod ??
                type?.GetProperty("IsVisible", BindingFlags.NonPublic | BindingFlags.Static)?.GetGetMethod(true);
            _terminalOpen = (Func<bool>?)terminalOpen?.CreateDelegate(typeof(Func<bool>));
            _terminalBounds = (ReadBounds?)api?.GetMethod("TryGetBounds")?.CreateDelegate(typeof(ReadBounds));
            _closeTerminal = (Func<bool>?)api?.GetMethod("TryClose")?.CreateDelegate(typeof(Func<bool>));
            _terminalRoot = (Func<RectTransform?>?)api?.GetProperty("ExtensionRoot")?.GetMethod?.CreateDelegate(typeof(Func<RectTransform?>));
            if (_terminalOpen == null) log.LogWarning("Unknown Inventory Terminal does not expose its visibility state.");
            else log.LogInfo(_terminalBounds != null ? "Unknown Inventory Terminal bounds API connected." :
                "Unknown Inventory Terminal bounds API unavailable. Using fallback bounds when the terminal is open.");
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
            _detected = DetectSurface(out var nextBounds, out var nextHost);
            _detectedBounds = nextBounds;
            _detectedHost = nextHost;
            _detectedFrame = Time.frameCount;
        }
        bounds = _detectedBounds;
        host = _detectedHost;
        return _detected;
    }

    // Check current game state without remeasuring layout. Native UI can open after our Update.
    internal bool IsCurrent(Kind kind, UnityEngine.Object? host) =>
        kind != Kind.Closed && DetectKind(PlayerCamera.main, out var currentHost) == kind && currentHost == host;

    private Kind DetectSurface(out Rect bounds, out UnityEngine.Object? host)
    {
        bounds = default;
        var camera = PlayerCamera.main;
        var kind = DetectKind(camera, out host);
        if (kind == Kind.Inventory)
        {
            bounds = InventoryBounds(camera);
        }
        else if (kind == Kind.Crafting)
        {
            bounds = PanelBounds(camera.craftingPanel);
        }
        else if (kind == Kind.Terminal)
        {
            if (_terminalBounds?.Invoke(out var terminal) == true && TryClipScreenBounds(terminal, out var visible))
                bounds = NeiLayout.FromScreen(visible, Screen.height);
            else
                bounds = PanelBounds(_terminalRoot?.Invoke()?.parent?.gameObject);
            if (_detected == Kind.Terminal && _detectedHost == host && _terminalWidth == Screen.width &&
                _terminalHeight == Screen.height && !TerminalBoundsChanged(_detectedBounds, bounds))
                bounds = _detectedBounds;
            _terminalWidth = Screen.width;
            _terminalHeight = Screen.height;
        }
        return kind;
    }

    private Kind DetectKind(PlayerCamera camera, out UnityEngine.Object? host)
    {
        host = null;
        var world = WorldGeneration.world;
        if (!camera || !camera.isActiveAndEnabled || !camera.body || !camera.body.alive || !camera.body.conscious ||
            !world || world.cutsceneActive)
            return Kind.Closed;

        var kind = ResolveKind(HasBlockingScreen(camera), HasOpenContainer(camera), _terminalOpen,
            camera.radialOpen && camera.radialMenu && Active(camera.radialMenu.gameObject), Open(camera.craftingPanel));
        host = kind switch
        {
            Kind.Crafting => camera.craftingPanel,
            Kind.Terminal => camera,
            Kind.Inventory => camera.radialMenu,
            _ => null
        };
        return kind;
    }

    internal static Kind ResolveKind(bool blockingScreen, bool containerOpen, Func<bool>? terminalOpen, bool inventoryOpen, bool craftingOpen)
    {
        if (blockingScreen) return Kind.Closed;
        if (craftingOpen) return containerOpen ? Kind.Closed : Kind.Crafting;
        // The terminal hides and disables the native container without clearing its
        // open flag. Only treat that flag as a blocker for the native backpack.
        if (terminalOpen?.Invoke() == true) return Kind.Terminal;
        return inventoryOpen && !containerOpen ? Kind.Inventory : Kind.Closed;
    }

    private static bool HasBlockingScreen(PlayerCamera camera)
    {
        var transfer = LiquidTransfer.instance;
        return PauseHandler.paused || SettingsMenu.instance || ScrollableText.textExists ||
            (ConsoleScript.instance && ConsoleScript.instance.active) || (MinigameBase.main && MinigameBase.main.currentMinigame) ||
            Open(camera.woundView) || Open(camera.tradeMenu) || Open(camera.itemCustomizer) ||
            (transfer && transfer.isActiveAndEnabled);
    }

    // The game keeps the radial open for containers, and the terminal can hide the native hierarchy.
    private static bool HasOpenContainer(PlayerCamera camera) => camera.currentContainer || Open(camera.containerMenu);

    internal static bool NativeDragCleanupAllowed(PlayerCamera camera) =>
        !HasOpenContainer(camera) && !HasBlockingScreen(camera) && !Open(camera.craftingPanel);

    private Rect PanelBounds(GameObject? panel)
    {
        if (Active(panel) && panel!.transform is RectTransform rect && TryProjectPanel(rect, out var screen))
        {
            var title = rect.Find("Top") as RectTransform;
            if (title && Active(title!.gameObject) && TryProjectPanel(title, out var header))
                screen = Union(screen, header);
            return NeiLayout.FromScreen(screen, Screen.height);
        }

        // Keep a usable center reservation if a modal is opening or its geometry is unavailable.
        float width = Mathf.Min(640f, Screen.width * .62f);
        float height = Mathf.Min(560f, Screen.height * .72f);
        return new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
    }

    private bool TryProjectPanel(RectTransform rect, out Rect screen)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        var rootCanvas = canvas ? canvas.rootCanvas : null;
        Camera? renderCamera = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            renderCamera = rootCanvas.worldCamera ? rootCanvas.worldCamera : Camera.main;

        rect.GetWorldCorners(_corners);
        var min = RectTransformUtility.WorldToScreenPoint(renderCamera, _corners[0]);
        var max = min;
        for (int i = 1; i < _corners.Length; i++)
        {
            var corner = RectTransformUtility.WorldToScreenPoint(renderCamera, _corners[i]);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        return TryClipScreenBounds(Rect.MinMaxRect(min.x, min.y, max.x, max.y), out screen);
    }

    private static bool TryClipScreenBounds(Rect bounds, out Rect visible)
    {
        visible = default;
        if (!ValidBounds(bounds)) return false;
        visible = Rect.MinMaxRect(Mathf.Max(0, bounds.xMin), Mathf.Max(0, bounds.yMin),
            Mathf.Min(Screen.width, bounds.xMax), Mathf.Min(Screen.height, bounds.yMax));
        return ValidBounds(visible);
    }

    private Rect InventoryBounds(PlayerCamera camera)
    {
        var radial = camera.radialMenu;
        var wearables = camera.wearPopulate ? camera.wearPopulate.transform : null;
        int wearableChildren = wearables ? wearables!.childCount : 0;
        float scale = camera.mainCanvas ? Mathf.Max(.2f, PlayerCamera.uiScale) : 1f;

        // Measure the local slot layout because the native camera moves the radial every Update.
        if (_detected != Kind.Inventory || _radialRoot != radial || _wearablesRoot != wearables ||
            _radialChildren != radial.childCount || _wearableChildren != wearableChildren ||
            _inventoryWidth != Screen.width || _inventoryHeight != Screen.height || _inventoryScale != scale)
        {
            _radialRoot = radial;
            _wearablesRoot = wearables;
            _radialChildren = radial.childCount;
            _wearableChildren = wearableChildren;
            _inventoryWidth = Screen.width;
            _inventoryHeight = Screen.height;
            _inventoryScale = scale;

            // Only measure visible inventory slots. The root also owns invisible drag zones and transient actions.
            _radialOffsets = new Rect(-297, -292, 594, 601);
            radial.GetComponentsInChildren(false, _radialButtons);
            foreach (var button in _radialButtons)
            {
                if (button.transform is RectTransform rect && TryMeasureLocal(rect, radial, out var slot))
                    _radialOffsets = Union(_radialOffsets, slot);
            }
            _radialButtons.Clear();
        }
        return AnchorInventory(_radialOffsets, _inventoryWidth, _inventoryHeight, _inventoryScale);
    }

    private bool TryMeasureLocal(RectTransform rect, RectTransform radial, out Rect bounds)
    {
        bounds = default;
        var relative = Matrix4x4.identity;
        for (Transform current = rect; current != radial; current = current.parent)
        {
            if (!current) return false;
            relative = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * relative;
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

    internal static Rect AnchorInventory(Rect offsets, float width, float height, float unit) =>
        new Rect(width * .5f + offsets.x * unit, height * .5f + offsets.y * unit, offsets.width * unit, offsets.height * unit);

    internal static bool TerminalBoundsChanged(Rect current, Rect next) =>
        Mathf.Abs(next.xMin - current.xMin) >= 1f || Mathf.Abs(next.yMin - current.yMin) >= 1f ||
        Mathf.Abs(next.xMax - current.xMax) >= 1f || Mathf.Abs(next.yMax - current.yMax) >= 1f;

    private static bool ValidBounds(Rect bounds) => bounds.width >= 1f && bounds.height >= 1f &&
        float.IsFinite(bounds.xMin) && float.IsFinite(bounds.yMin) && float.IsFinite(bounds.xMax) && float.IsFinite(bounds.yMax);

    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    internal void SetInputShield(Transform parent, bool enabled, NeiWindow? window = null)
    {
        enabled = enabled && window != null;
        if (!enabled && !_modalShield) return;
        if (!_modalShield)
        {
            var root = new GameObject("NEI.ModalInput", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            _modalShield = root.GetComponent<Canvas>();
            _modalShield.renderMode = RenderMode.ScreenSpaceOverlay;
            _modalShield.sortingOrder = short.MaxValue;
            var target = new GameObject("NEI.InputTarget", typeof(RectTransform), typeof(Image), typeof(NeiRaycastFilter));
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) target.layer = uiLayer;
            var rect = (RectTransform)target.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = target.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            _inputFilter = target.GetComponent<NeiRaycastFilter>();
        }
        _inputFilter!.Window = enabled ? window : null;
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

    internal Item? HoveredItem(Kind kind, bool forDrag = false)
    {
        var camera = PlayerCamera.main;
        if (kind is Kind.Closed or Kind.Crafting || !EventSystem.current || DetectKind(camera, out _) != kind) return null;
        Transform? root = kind == Kind.Terminal ? _terminalRoot?.Invoke()?.parent : camera.radialMenu;
        if (!root) return null;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Input.mousePosition }, hits);
        foreach (var hit in hits)
        {
            if (!hit.gameObject || !hit.gameObject.transform.IsChildOf(root)) continue;
            var label = forDrag ? hit.gameObject.GetComponent<ItemLabel>() : hit.gameObject.GetComponentInParent<ItemLabel>();
            if (label && label.refItem) return label.refItem;
            var slot = forDrag ? hit.gameObject.GetComponent<InvButton>() : hit.gameObject.GetComponentInParent<InvButton>();
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
        var camera = PlayerCamera.main;
        if (kind == Kind.Closed || DetectKind(camera, out _) != kind || !CanOpenCrafting(recipe) ||
            (kind == Kind.Terminal && _closeTerminal?.Invoke() != true)) return false;
        if (!Active(camera.craftingPanel)) camera.OpenCraftScreen();
        if (!Active(camera.craftingPanel)) return false;
        camera.ClearRecipeFilter();
        camera.SelectRecipe(recipe.index);
        return true;
    }

    private static bool Active(GameObject? go) => go != null && go.activeInHierarchy;
    private static bool Open(GameObject? go) => go != null && go.activeSelf;

    internal static void InstallPatches(Harmony harmony, ManualLogSource log)
    {
        Patch(harmony, log, typeof(PlayerCamera), "HandleInput", nameof(SkipInput), nameof(CancelOnInventoryClose));
        Patch(harmony, log, typeof(PlayerCamera), "HandleDragging", nameof(FilterNativeDrag), priority: Priority.First);
        Patch(harmony, log, typeof(PlayerCamera), "HandleStartDragging", nameof(SkipPointer), nameof(RecordNativeDrag));
        Patch(harmony, log, typeof(PlayerCamera), "HandleWhileDragging", nameof(SkipWhileDragging));
        Patch(harmony, log, typeof(PlayerCamera), "TryPerformWorldActions", nameof(SkipWorldDropThroughOverlay));
        Patch(harmony, log, typeof(PlayerCamera), "HandleMouseOver", nameof(SkipHover));
        var darkMethod = AccessTools.Method(typeof(GlobalDark), nameof(GlobalDark.HandleTooltip));
        if (darkMethod != null)
            harmony.Patch(darkMethod,
                prefix: new HarmonyMethod(typeof(GameSurface), nameof(SuppressNeiTooltip)) { priority = Priority.First });
        var uei = AccessTools.TypeByName("UeiRuntime");
        if (Chainloader.PluginInfos.ContainsKey("casualtiesunknown.uei") && uei != null)
        {
            var shouldShow = AccessTools.Method(uei, "ShouldShow", new[] { typeof(PlayerCamera), typeof(string).MakeByRefType() });
            if (shouldShow != null)
                harmony.Patch(shouldShow, postfix: new HarmonyMethod(typeof(GameSurface), nameof(HideDuplicateIndex)));
        }
    }

    private static void Patch(Harmony harmony, ManualLogSource log, Type type, string target, string prefix,
        string? postfix = null, int priority = Priority.Normal)
    {
        var method = AccessTools.Method(type, target);
        if (method == null) { log.LogWarning($"Optional input patch not found: {type.Name}.{target}"); return; }
        try
        {
            var before = priority == Priority.First ? new[] { TerminalGuid } : null;
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(GameSurface), prefix) { priority = priority, before = before },
                postfix: postfix == null ? null : new HarmonyMethod(typeof(GameSurface), postfix));
        }
        catch (Exception error) { log.LogWarning($"Could not patch {type.Name}.{target}: {error.Message}"); }
    }

    // Hide only the stock item tooltip whose pointer is over a NEI-drawn region.
    private static bool SuppressNeiTooltip(GlobalDark __instance)
    {
        if (Plugin.Instance?.SuppressNativeTooltip != true) return true;
        // The stock hidden branch owns the root state; apply it now because the original
        // method (and its Update-time tooltipText reset) will not run for this frame.
        NativeTooltipLayer.Hide(__instance);
        return false;
    }

    private static bool SkipInput(PlayerCamera __instance, out bool __state)
    {
        __state = __instance == PlayerCamera.main && __instance.radialOpen && __instance.dragItem &&
            (Input.GetKeyDown(KeyBinds.GetBind("toggleinventory")) || Input.GetKeyDown(KeyBinds.GetBind("pause")));
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

    private static void CancelOnInventoryClose(PlayerCamera __instance, bool __state)
    {
        if (DragInputPolicy.CancelOnClose(__state, __instance.radialOpen) && NativeDragCleanupAllowed(__instance) &&
            Plugin.Instance is { } plugin && plugin.TracksNativeDrag(__instance))
            plugin.CancelNativeDrag(__instance);
    }

    private static bool SkipPointer(PlayerCamera __instance) =>
        __instance != PlayerCamera.main || Plugin.Instance?.OwnsPointer != true;

    private static void RecordNativeDrag(PlayerCamera __instance)
    {
        if (__instance == PlayerCamera.main) Plugin.Instance?.ObserveNativeDrag(__instance);
    }

    private static bool FilterNativeDrag(PlayerCamera __instance, List<RaycastResult> uiCasts)
    {
        if (__instance != PlayerCamera.main || Plugin.Instance is not { } plugin) return true;
        if (plugin.NativeDragCancelledThisFrame) return false;
        if (!Input.GetKeyUp(KeyBinds.GetBind("iteminteract")) || !plugin.PointerOverOverlay) return true;

        // Real inventory slots are still valid where the layout overlaps. Over a
        // NEI-only control, do not call native release: it can fall back to dropping.
        bool dragging = __instance.dragItem;
        var release = DragInputPolicy.ReleaseOverOverlay(dragging, plugin.OwnsPointer,
            dragging && HasNativeDropTarget(__instance, uiCasts));
        if (release == DragInputPolicy.Release.CancelDrag) plugin.CancelNativeDrag(__instance);
        return release == DragInputPolicy.Release.Native;
    }

    private static bool HasNativeDropTarget(PlayerCamera camera, List<RaycastResult> hits)
    {
        foreach (var hit in hits)
        {
            var target = hit.gameObject;
            if (!Active(target)) continue;
            if (target.TryGetComponent<InvButton>(out var slot) && slot.Overlaps(hits) ||
                target == camera.craftButton || target == camera.customizeOpenObject || target.GetComponent<WoundViewLimb>() ||
                (camera.currentContainer && target.CompareTag("ContainerBack")) || (camera.currentTrader && target.CompareTag("TradeThing")))
                return true;
            if (target.CompareTag("RadialCenter") && camera.radialOpen && camera.radialMenu.localScale.x > PlayerCamera.inventoryUseLeniency &&
                Vector2.Distance(Input.mousePosition, camera.radialCircle.transform.position) / PlayerCamera.uiScale < camera.radialCircle.rectTransform.sizeDelta.x * .5f)
                return true;
        }
        return false;
    }

    private static bool SkipWhileDragging(PlayerCamera __instance, List<RaycastResult> uiCasts)
    {
        if (__instance != PlayerCamera.main || Plugin.Instance is not { } plugin) return true;
        if (plugin.OwnsPointer || plugin.OwnsKeyboard) return false;
        if (!__instance.dragItem || !plugin.PointerOverOverlay || HasNativeDropTarget(__instance, uiCasts)) return true;
        // Keep the drag preview moving without interacting with the world behind NEI.
        if (__instance.dragImage)
        {
            var preview = __instance.dragImage.rectTransform;
            preview.position = Input.mousePosition;
            preview.localScale = Vector2.Lerp(preview.localScale, Vector2.one, Time.unscaledDeltaTime * 10f);
        }
        return false;
    }

    // Some real slots only accept certain items. If the native handler rejects a
    // slot below NEI, it must not turn that rejection into a world drop instead.
    private static bool SkipWorldDropThroughOverlay(PlayerCamera __instance) =>
        __instance != PlayerCamera.main || Plugin.Instance?.PointerOverOverlay != true;

    internal static void ClearNativeDrag(PlayerCamera camera)
    {
        // Beginning a drag reads the original slot, it does not detach the item.
        camera.dragItem = null!;
        if (camera.dragImage) camera.dragImage.enabled = false;
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
