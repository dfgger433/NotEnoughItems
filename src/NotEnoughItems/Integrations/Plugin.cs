using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NotEnoughItems;

/// <summary>BepInEx entry point for the Casualties: Unknown NEI port.</summary>
[BepInPlugin(Guid, "Not Enough Items", BuildVersion.Value)]
[BepInDependency("net.cucorelib", "1.1")]
[BepInDependency(GameSurface.TerminalGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(PinyinSearch.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(NativePlaySession.TogetherGuid, BepInDependency.DependencyFlags.SoftDependency)]
[DefaultExecutionOrder(-300)]
public sealed class Plugin : BaseUnityPlugin
{
    /// <summary>The stable plug-in identifier to use in a BepInDependency attribute.</summary>
    public const string Guid = "casualtiesunknown.notenoughitems";
    // Keep the existing BepInEx key, but use a distinct ID so CCL does not automatically
    // link its saved game-setting value back into the config before our callback runs.
    private const string OverlaySetting = "NotEnoughItems.ShowOverlay";
    internal static Plugin? Instance { get; private set; }
    internal PinyinSearch? Pinyin { get; private set; }
    private GameSurface _surface = null!;
    private NeiWindow _window = null!;
    private Harmony? _harmony;
    private ConfigEntry<bool> _enabled = null!;
    private ConfigEntry<bool> _allowCheats = null!;
    private ConfigEntry<bool> _debugSpawnMissingMaterials = null!;
    private ConfigEntry<SpawnMode> _spawnMode = null!;
    private ConfigEntry<float> _scale = null!;
    private ConfigEntry<KeyCode> _toggle = null!;
    private ConfigEntry<KeyCode> _recipes = null!;
    private ConfigEntry<KeyCode> _usages = null!;
    private ConfigEntry<KeyCode> _bookmark = null!;
    private ConfigEntry<KeyCode> _toggleBookmarks = null!;
    private ConfigEntry<string> _language = null!;
    private GameSurface.Kind _active;
    private UnityEngine.Object? _host;
    private Rect _bounds;
    private int _layoutWidth, _layoutHeight;
    private float _layoutScale;
    private (int Bookmark, int Catalog, int Y, int Icon, bool History, int LeftX, int LeftY, int RightX, int RightY) _layoutOptions;
    private bool _positioned;
    private Toggle? _visibilityToggle;
    private int _capturedFrame = -1;
    private GameSurface.Kind _capturedSurface;
    private UnityEngine.Object? _capturedHost;
    private int _nativePressFrame = -1;
    private int _nativeCancelFrame = -1;
    private bool _pointerCapture;
    private int _ownedHotControl;
    private PlayerCamera? _observedDragCamera;
    private bool _clearFocus;
    private bool _escapeDown, _backspaceDown;
    private float _nextRefresh;
    private int _updatedFrame = -1;
    private int _bindingFrame = -1;
    internal IReadOnlyList<(ConfigEntry<KeyCode> Entry, string LabelKey)> ShortcutBindings { get; private set; } = null!;

    internal bool SuppressNativeTooltip
    {
        get
        {
            if (_window == null || PauseHandler.paused || SettingsMenu.instance ||
                (ConsoleScript.instance && ConsoleScript.instance.active) || !OverlayActive) return false;
            var mouse = Input.mousePosition;
            var pointer = new Vector2(mouse.x, Screen.height - mouse.y);
            // The recipe header is an active control strip, not an underlying item slot.
            // A native tooltip drawn above IMGUI can conceal both its label and buttons.
            if (_window.RecipeNavigationHit(pointer)) return true;
            if (!_window.NativeTooltipHit(pointer)) return false;
            bool nativeUiTooltip = !string.IsNullOrEmpty(UIUtil.GetUITooltip(UIUtil.GetEventSystemRaycastResults()).Item1);
            return NativeTooltip.Suppress(overlayActive: true, pointerOverVisibleRegion: true, nativeUiTooltipVisible: nativeUiTooltip);
        }
    }

    private bool OverlayActive => isActiveAndEnabled && _enabled.Value && _surface.IsCurrent(_active, _host);
    internal bool NativePointerActive => _nativePressFrame == Time.frameCount ||
        (PlayerCamera.main && PlayerCamera.main.dragItem);
    internal bool CanShieldPointer => OverlayActive && !NativePointerActive;
    internal bool OwnsPointer => CanShieldPointer &&
        (_pointerCapture || _window.Hit(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)));
    internal bool OwnsKeyboard => OverlayActive && !NativePointerActive &&
        (_window.SearchFocused || _capturedFrame == Time.frameCount);
    internal bool IsNativeInventory => _active == GameSurface.Kind.Inventory;
    internal bool IsCrafting => _active == GameSurface.Kind.Crafting;
    internal bool NativeDragCancelledThisFrame => _nativeCancelFrame == Time.frameCount;
    internal bool TracksNativeDrag(PlayerCamera camera) => OverlayActive || _observedDragCamera == camera;
    internal bool PointerOverOverlay
    {
        get
        {
            if (!OverlayActive) return false;
            var point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return _window.OptionsHit(point) || _window.RecipeNavigationHit(point) || _window.CandidatePopupHit(point) ||
                _window.ItemTooltipPopupHit(point) || _window.Hit(point) && (_active != GameSurface.Kind.Terminal || !_bounds.Contains(point));
        }
    }

    internal void ObserveNativeDrag(PlayerCamera camera)
    {
        if (!OverlayActive || !camera.dragItem) return;
        _observedDragCamera = camera;
        YieldToNativePointer();
    }

    internal void CancelNativeDrag(PlayerCamera camera)
    {
        YieldToNativePointer();
        _nativeCancelFrame = Time.frameCount;
        GameSurface.ClearNativeDrag(camera);
        _observedDragCamera = null;
    }
    internal bool ShouldBlockInput => isActiveAndEnabled && !NativePointerActive &&
        (_capturedFrame == Time.frameCount && _surface.IsCurrent(_capturedSurface, _capturedHost) ||
         _active == GameSurface.Kind.Inventory && (OwnsKeyboard || OwnsPointer && AnyInteractTransition()));

    internal void YieldToNativePointer()
    {
        _nativePressFrame = Time.frameCount;
        _pointerCapture = false;
        _clearFocus = true;
        _window.SearchFocused = false;
    }
    internal string RecipeKey => _recipes.Value.ToString();
    internal string UsageKey => _usages.Value.ToString();
    internal string BookmarkKey => _bookmark.Value.ToString();
    internal bool BookmarkKeyBound => _bookmark.Value != KeyCode.None;
    internal bool Chinese => _language.Value == "zh-CN" || (_language.Value == "auto" &&
        ((Locale.currentLangName?.IndexOf("zh", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
        (Locale.currentLangName?.Contains("中文") ?? false) || SystemUsesChinese()));

    private static bool SystemUsesChinese() => Application.systemLanguage is SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional;

    internal bool AllowCheats => _allowCheats.Value;
    internal bool DebugSpawnMissingMaterials
    {
        get => _debugSpawnMissingMaterials.Value;
        set => _debugSpawnMissingMaterials.Value = value;
    }
    internal SpawnMode ItemSpawnMode
    {
        get => _spawnMode.Value == SpawnMode.SpawnToPlayer ? SpawnMode.SpawnToPlayer : SpawnMode.Nei;
        set => _spawnMode.Value = value;
    }
    internal float Scale { get => _scale.Value; set => _scale.Value = value; }
    internal string Language
    {
        get => _language.Value;
        set
        {
            bool wasChinese = Chinese;
            _language.Value = value;
            if (Chinese != wasChinese) Catalog.Shared.Invalidate();
        }
    }

    /// <summary>Updates the explicit local-cheat opt-in from NEI's own settings screen.</summary>
    internal void SetAllowCheats(bool value)
    {
        _allowCheats.Value = value;
        if (!value) NeiActions.DisableOfflineCheats();
    }

    /// <summary>Cycles Recipe → Cheat and returns a localized notice for the UI.</summary>
    internal bool TryCycleMode(out string notice) => NeiActions.TryCycleMode(out notice);

    /// <summary>Compatibility overload for icon handlers that only need to know whether cycling succeeded.</summary>
    internal bool TryCycleMode() => NeiActions.TryCycleMode(out _);

    /// <summary>Retries the catalog scan from the settings diagnostics page.</summary>
    internal void RetryCatalogRefresh() => _window.RetryCatalogRefresh(Logger);

    internal void RefreshBookmarkStock() => _window.RefreshBookmarkStock();

    internal static ConfigEntry<SpawnMode> BindSpawnMode(ConfigFile config) => config.Bind("NotEnoughItems", "SpawnMode",
        SpawnMode.Nei, "Single-player cheat delivery. Nei stows without replacing gear; SpawnToPlayer uses vanilla automatic pickup.");

    internal static ConfigEntry<int> BindSpawnAmount(ConfigFile config) => config.Bind("NotEnoughItems", "SpawnAmount", 1,
        new ConfigDescription("Shared quantity for left-click item giving in Cheat mode and bookmark quantity shortcuts.",
            new AcceptableValueRange<int>(1, NeiActions.MaxGiveAmount)));

    internal static ConfigEntry<bool> BindDebugSpawnMissingMaterials(ConfigFile config) => config.Bind(
        "NotEnoughItems", "DebugSpawnMissingMaterials", false,
        "Enable the bookmark-chain missing-material button and shortcut. Still requires Cheat mode and authorized permissions.");

    private void Awake()
    {
        NeiText.Initialize(System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location), Logger.LogWarning);
        _enabled = Config.Bind("NotEnoughItems", "Enabled", true, "Show NEI beside inventory and the optional Inventory Terminal. Also changed by Game Settings and the toggle key.");
        _allowCheats = Config.Bind("NotEnoughItems", "AllowLocalCheats", false, "Opt in to cheats in single-player. Multiplayer mode uses the server's Together permissions instead.");
        _debugSpawnMissingMaterials = BindDebugSpawnMissingMaterials(Config);
        _spawnMode = BindSpawnMode(Config);
        _scale = Config.Bind("NotEnoughItems", "Scale", 1f, new ConfigDescription("Sidebar scale multiplier.", new AcceptableValueRange<float>(0.7f, 1.4f)));
        _toggle = Config.Bind("NotEnoughItems", "Toggle", KeyCode.O, "Show or hide the NEI overlay on the open surface.");
        _recipes = Config.Bind("NotEnoughItems", "RecipesKey", KeyCode.R, "Look up recipes for the hovered item.");
        _usages = Config.Bind("NotEnoughItems", "UsagesKey", KeyCode.U, "Look up uses for the hovered item.");
        _bookmark = Config.Bind("NotEnoughItems", "BookmarkKey", KeyCode.A, "Bookmark the hovered item.");
        _toggleBookmarks = Config.Bind("NotEnoughItems", "ToggleBookmarks", KeyCode.B, "Show or hide the bookmark panel.");
        _language = Config.Bind("NotEnoughItems", "Language", "auto", new ConfigDescription("Interface language.", new AcceptableValueList<string>("auto", "en", "zh-CN")));
        Pinyin = PinyinSearch.Connect(Chainloader.PluginInfos.TryGetValue(PinyinSearch.PluginGuid, out var pinyinPlugin)
            ? pinyinPlugin.Instance?.GetType().Assembly : null);
        ShortcutBindings = new[]
        {
            (_toggle, "options.keys.toggle_nei"), (_recipes, "options.keys.recipes"),
            (_usages, "options.keys.usages"), (_bookmark, "options.keys.bookmark_item"),
            (_toggleBookmarks, "options.keys.toggle_bookmarks")
        };
        _surface = new GameSurface(Logger);
        _window = new NeiWindow(this, Config);
        Instance = this;
        _harmony = new Harmony(Guid);
        InstallVisibilityPatch(_harmony);
        RegisterOptions();
        _enabled.SettingChanged += OnVisibilityChanged;
        MirrorVisibility();
        GameSurface.InstallPatches(_harmony, Logger);
        Logger.LogInfo("Not Enough Items loaded; CUCoreLib settings registered.");
    }

    private void RegisterOptions()
    {
        const Setting.SettingCategory category = Setting.SettingCategory.Game;
        ModOptionsRegistry.Register(ModOptionDefinition.Bool(OverlaySetting,
            NeiText.Get("options.overlay.label", Chinese),
            NeiText.Get("options.overlay.description", Chinese),
            category, _enabled.Value, _ => MirrorVisibility()));
    }

    private static void InstallVisibilityPatch(Harmony harmony) =>
        harmony.Patch(AccessTools.Method(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab), new[] { typeof(Setting.SettingCategory) }),
            postfix: new HarmonyMethod(typeof(Plugin), nameof(FindVisibilityToggle)));

    private static void FindVisibilityToggle(Setting.SettingCategory category, List<GameObject> ___spawnedSettings)
    {
        if (Instance is not { } plugin) return;
        plugin._visibilityToggle = null;
        if (category != Setting.SettingCategory.Game || Settings.settings == null || ___spawnedSettings == null) return;

        int row = 0;
        foreach (var setting in Settings.settings)
        {
            if (setting == null || setting.category != category) continue;
            if (setting is SettingBool && setting.name == OverlaySetting)
            {
                if (row < ___spawnedSettings.Count && ___spawnedSettings[row] && ___spawnedSettings[row].transform.childCount > 1)
                {
                    var toggle = ___spawnedSettings[row].transform.GetChild(1).GetComponent<Toggle>();
                    plugin._visibilityToggle = toggle;
                    if (toggle)
                    {
                        // The stock listener only calls Setting.Apply, which is also
                        // invoked for loads and network snapshots. Accept an explicit
                        // toggle change instead, so those callbacks cannot write config.
                        toggle.onValueChanged.RemoveAllListeners();
                        toggle.onValueChanged.AddListener(plugin.SetOverlayVisible);
                    }
                }
                plugin.MirrorVisibility();
                return;
            }
            row++;
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        // Both the native setting and the toggle key share this persistent value.
        // Never save settings.json to change unrelated game rows.
        _enabled.Value = visible;
    }

    private void MirrorVisibility()
    {
        if (Settings.settings?.Find(static setting => setting?.name == OverlaySetting) is SettingBool row)
            row.value = _enabled.Value;
        if (_visibilityToggle) _visibilityToggle!.SetIsOnWithoutNotify(_enabled.Value);
    }

    private void OnVisibilityChanged(object sender, EventArgs args)
    {
        MirrorVisibility();
        _clearFocus = true;
        _pointerCapture = false;
        if (!_enabled.Value)
        {
            _active = GameSurface.Kind.Closed;
            _host = null;
            _window.ResetView();
            _window.Hide();
            _positioned = false;
            _surface.SetInputShield(transform, false);
        }
    }

    private void Update()
    {
        if (_harmony is { } harmony) TogetherCheats.Update(harmony, Logger);
        ReadInput();
    }

    internal void ReadInput()
    {
        if (_window == null || !isActiveAndEnabled || _updatedFrame == Time.frameCount) return;
        _updatedFrame = Time.frameCount;
        _escapeDown = _backspaceDown = false;
        bool togglePressed = Pressed(_toggle) && _toggle.Value != KeyBinds.GetBind("toggleinventory");
        if (!_enabled.Value && !togglePressed) return;
        DetectSurface();
        if (_active == GameSurface.Kind.Closed) return;
        // Read a new binding before its old or new action (including the overlay toggle) can run.
        if (_window.CaptureShortcutBinding())
        {
            _bindingFrame = Time.frameCount;
            CaptureKey();
            return;
        }
        bool typing = _window.SearchFocused || GameSurface.OtherTextInputFocused() || _surface.TerminalTextInputFocused;
        if (!typing && togglePressed)
        {
            CaptureKey();
            SetOverlayVisible(!_enabled.Value);
            if (_enabled.Value) DetectSurface();
        }
        if (!_enabled.Value) return;
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 1.3f;
            _window.RefreshCatalog(Logger);
        }
        if (_window.LayoutEditing) return;
        if (NativePointerActive || NativeSlotPress())
        {
            var camera = PlayerCamera.main;
            if (camera && camera.dragItem) ObserveNativeDrag(camera);
            else YieldToNativePointer();
            return;
        }
        var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) ||
            Input.GetKeyDown(KeyBinds.GetBind("iteminteract")))
        {
            if (_window.Hit(mouse))
            {
                _pointerCapture = true;
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            }
            if (!_window.TextInputHit(mouse) && _window.SearchFocused) _clearFocus = true;
        }
        bool pausePressed = Input.GetKeyDown(KeyBinds.GetBind("pause"));
        bool dismissLanguageMenu = _window.LanguageMenuOpen && (pausePressed || Input.GetKeyDown(KeyCode.Escape));
        _escapeDown = dismissLanguageMenu || _active == GameSurface.Kind.Inventory && pausePressed &&
            (_window.ModalOpen || _window.SearchFocused);
        if (_escapeDown)
        {
            CaptureKey();
            if (dismissLanguageMenu) _window.CloseLanguageMenu();
            else if (_window.SearchFocused) _clearFocus = true;
            else _window.Back();
            return;
        }
        if (typing) return;
        if (Input.GetKeyDown(KeyCode.Backspace) && _window.ModalOpen)
        {
            _backspaceDown = true;
            CaptureKey();
            _window.NavigateBack();
            return;
        }
        if (Pressed(_toggleBookmarks))
        {
            _window.ToggleBookmarks();
            CaptureKey();
            return;
        }
        if (!IsCrafting && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F))
        {
            if (_window.RequestSearchFocus()) CaptureKey();
            return;
        }
        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (_window.ItemTooltipPopupHit(mouse)) return;
        bool candidatePopup = _window.CandidatePopupHit(mouse);
        if (!candidatePopup && TryBookmarkKey(mouse, control, shift))
        {
            CaptureKey();
            return;
        }
        if (IsCrafting)
        {
            if (Pressed(_bookmark) && _window.Hit(mouse) && _window.HandleBookmarkShortcut(mouse, _window.ItemAt(mouse), control, shift)) CaptureKey();
            return;
        }
        bool recipes = Pressed(_recipes), usages = Pressed(_usages), bookmark = Pressed(_bookmark);
        if (!recipes && !usages && !bookmark) return;
        if (recipes && !usages && !bookmark && _window.TryLookupPropertyAt(mouse))
        {
            CaptureKey();
            return;
        }
        var id = _window.ItemAt(mouse);
        string[]? contents = null;
        if (id == null && !_window.Hit(mouse) && _surface.HoveredItem(_active) is { } native)
        {
            id = "item:" + native.id;
            if (!bookmark && native.TryGetComponent<WaterContainerItem>(out var container) && container.stack != null)
                contents = container.stack.Where(stack => stack != null && stack.amount > 0 && !string.IsNullOrWhiteSpace(stack.liquidId))
                    .Select(stack => "liquid:" + stack.liquidId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        if (bookmark)
        {
            if (!candidatePopup && _window.HandleBookmarkShortcut(mouse, id, control, shift)) CaptureKey();
            return;
        }
        if (string.IsNullOrEmpty(id)) return;
        _window.Lookup(id, usages, contents);
        CaptureKey();
    }

    private static readonly KeyCode[] BookmarkActionKeys = { KeyCode.T, KeyCode.F, KeyCode.V, KeyCode.C, KeyCode.M, KeyCode.L, KeyCode.S };

    private bool TryBookmarkKey(Vector2 mouse, bool control, bool shift)
    {
        foreach (var key in BookmarkActionKeys)
        {
            if (!Input.GetKeyDown(key)) continue;
            var action = BookmarkKeys.Resolve(key, control, shift);
            if (IsCrafting && action is BookmarkShortcut.Tree or BookmarkShortcut.OrderTree or BookmarkShortcut.OrderFavorite or BookmarkShortcut.Pull or BookmarkShortcut.PullMissing or BookmarkShortcut.SpawnMissing) continue;
            bool handled = action switch
            {
                BookmarkShortcut.Tree => _window.OpenBookmarkTree(mouse),
                BookmarkShortcut.OrderTree => _window.OrderAndInspectAt(mouse),
                BookmarkShortcut.OrderFavorite => _window.OrderFavoriteAt(mouse),
                BookmarkShortcut.Pull or BookmarkShortcut.PullMissing => _window.HandleBookmarkPull(mouse, shift),
                BookmarkShortcut.Craft or BookmarkShortcut.CraftMissing => _window.HandleBookmarkCraft(mouse, control),
                BookmarkShortcut.SpawnMissing => _window.HandleBookmarkSpawnMissing(mouse),
                BookmarkShortcut.Share or BookmarkShortcut.ShareRecipe => _window.ShareBookmark(mouse, shift),
                BookmarkShortcut.Copy => _window.HandleBookmarkClipboard(mouse, copy: true),
                BookmarkShortcut.Paste => _window.HandleBookmarkClipboard(mouse, copy: false),
                BookmarkShortcut.OpenNative => _window.OpenBookmarkInCrafting(mouse),
                _ => false
            };
            if (handled) return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        if (_window == null) return;
        if (_enabled.Value) DetectSurface(refresh: true);
        var dragCamera = _observedDragCamera;
        if (!dragCamera || dragCamera != PlayerCamera.main || !GameSurface.NativeDragCleanupAllowed(dragCamera!) ||
            (!dragCamera!.dragItem && (!dragCamera.dragImage || !dragCamera.dragImage.enabled)))
            _observedDragCamera = null;
        else
        {
            var interact = KeyBinds.GetBind("iteminteract");
            // Let native/terminal/multiplayer consumers finish on KeyUp. A remaining
            // preview on a subsequent unheld frame is stale, not a pending drop.
            if (DragInputPolicy.IsStale(Input.GetKey(interact), Input.GetKeyUp(interact))) CancelNativeDrag(dragCamera);
        }
        _surface.SetInputShield(transform, OverlayActive, _window);
        if (_pointerCapture && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2) &&
            !Input.GetKey(KeyBinds.GetBind("iteminteract"))) _pointerCapture = false;
    }

    private void DetectSurface(bool refresh = false)
    {
        var detected = _surface.Detect(out var bounds, out var host, refresh);
        bool switched = detected != _active || _host != host;
        if (switched)
        {
            _clearFocus = true;
            _host = host;
            _window.ResetView();
            _window.Hide();
            _positioned = false;
            _nextRefresh = 0;
            _pointerCapture = false;
        }
        _active = detected;
        if (!_enabled.Value || _active == GameSurface.Kind.Closed) return;

        float scale = Scale;
        if (!switched && _positioned && _bounds.Equals(bounds) && _layoutWidth == Screen.width && _layoutHeight == Screen.height &&
            _layoutScale == scale && _layoutOptions == _window.LayoutOptions)
            return;
        _positioned = true;
        _bounds = bounds;
        _layoutWidth = Screen.width;
        _layoutHeight = Screen.height;
        _layoutScale = scale;
        _layoutOptions = _window.LayoutOptions;
        _window.Position(bounds, scale);
    }

    private void OnGUI()
    {
        if (_window == null) return;
        // Recover releases controls could not see if a modal closed mid-press.
        if (Event.current.type == EventType.Repaint && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
            NeiSkin.CancelButtonPress();
        if (_clearFocus)
        {
            if (NeiWindow.IsTextControl(GUI.GetNameOfFocusedControl())) GUI.FocusControl(null);
            if (_ownedHotControl != 0 && GUIUtility.hotControl == _ownedHotControl) GUIUtility.hotControl = 0;
            _ownedHotControl = 0;
            _window.SearchFocused = false;
            _clearFocus = false;
        }
        if (!OverlayActive) return;
        if (Event.current.type == EventType.KeyDown &&
            ((_escapeDown && (Event.current.keyCode == KeyCode.Escape || Event.current.keyCode == KeyBinds.GetBind("pause"))) ||
             (_backspaceDown && Event.current.keyCode == KeyCode.Backspace))) Event.current.Use();
        var guiEvent = Event.current;
        if (_bindingFrame == Time.frameCount && guiEvent.type is EventType.KeyDown or EventType.MouseDown)
        { guiEvent.Use(); return; }
        // Both inventory and terminal hosts draw the same editors. Stop IMGUI traversal without
        // capturing the gameplay key, which is polled separately (for example, to close inventory).
        if (TabFocusPolicy.Consume(guiEvent.type == EventType.KeyDown, guiEvent.keyCode, guiEvent.character))
        {
            guiEvent.Use();
            return;
        }
        if (NativePointerActive && guiEvent.type is not (EventType.Layout or EventType.Repaint)) return;
        int originalDepth = GUI.depth;
        int previousControl = GUIUtility.hotControl;
        bool mouseDown = guiEvent.type == EventType.MouseDown;
        // Unity renders smaller IMGUI depths on top. Keep NEI behind the stock
        // UGUI tooltip and other native IMGUI surfaces instead of covering them.
        GUI.depth = 100;
        try
        {
            _window.Draw();
        }
        finally
        {
            GUI.depth = originalDepth;
            int control = GUIUtility.hotControl;
            if (mouseDown && control != 0 && control != previousControl) _ownedHotControl = control;
            else if (_ownedHotControl != control) _ownedHotControl = 0;
        }
    }

    private bool NativeSlotPress()
    {
        if (_active == GameSurface.Kind.Terminal || !Input.GetKeyDown(KeyBinds.GetBind("iteminteract"))) return false;
        var pointer = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        return !_window.OptionsHit(pointer) && !_window.RecipeNavigationHit(pointer) && !_window.CandidatePopupHit(pointer) &&
            !_window.ItemTooltipPopupHit(pointer) && _surface.HoveredItem(_active, forDrag: true) != null;
    }

    private static bool Pressed(ConfigEntry<KeyCode> key) => key.Value != KeyCode.None && Input.GetKeyDown(key.Value);
    private static bool AnyInteractTransition() => Input.GetKeyDown(KeyBinds.GetBind("iteminteract")) ||
        Input.GetKeyUp(KeyBinds.GetBind("iteminteract")) || Input.GetKey(KeyBinds.GetBind("attack")) || Input.mouseScrollDelta.y != 0;
    private void CaptureKey()
    {
        _capturedFrame = Time.frameCount;
        _capturedSurface = _active;
        _capturedHost = _host;
    }
    internal bool OpenCrafting(global::Recipe recipe) => _surface.OpenCrafting(_active, recipe);
    internal bool CanOpenCrafting(global::Recipe recipe) => GameSurface.CanOpenCrafting(recipe);
    internal void LogCraftFailure(Exception error) => Logger.LogError("NEI bookmark crafting stopped: " + error);

    private void OnDisable()
    {
        TogetherCheats.Suspend();
        _surface?.SetInputShield(transform, false);
        _window?.ResetView();
        _window?.Hide();
        _positioned = false;
        _active = GameSurface.Kind.Closed;
        _host = null;
        _pointerCapture = false;
        _clearFocus = true;
    }

    private void OnDestroy()
    {
        if (_enabled != null) _enabled.SettingChanged -= OnVisibilityChanged;
        BookmarkCrafting.Release();
        NativeTooltipPreview.Release();
        TogetherCheats.Release();
        _harmony?.UnpatchSelf();
        NeiSkin.Release();
        NeiActions.Enabled = false;
        if (Instance == this) Instance = null;
    }
}
