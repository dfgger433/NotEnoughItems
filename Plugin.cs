using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NotEnoughItems;

/// <summary>BepInEx entry point for the Casualties: Unknown NEI port.</summary>
[BepInPlugin(Guid, "Not Enough Items", "1.2.7")]
[BepInDependency("net.cucorelib", "1.1")]
[BepInDependency(GameSurface.TerminalGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("casualtiesunknown.uei", BepInDependency.DependencyFlags.SoftDependency)]
[DefaultExecutionOrder(-300)]
public sealed class Plugin : BaseUnityPlugin
{
    /// <summary>The stable plug-in identifier to use in a BepInDependency attribute.</summary>
    public const string Guid = "casualtiesunknown.notenoughitems";
    // Keep the existing BepInEx key, but use a distinct ID so CCL does not automatically
    // link its saved game-setting value back into the config before our callback runs.
    private const string OverlaySetting = "NotEnoughItems.ShowOverlay";
    internal static Plugin? Instance { get; private set; }
    private GameSurface _surface = null!;
    private NeiWindow _window = null!;
    private Harmony? _harmony;
    private ConfigEntry<bool> _enabled = null!;
    private ConfigEntry<bool> _replaceUei = null!;
    private ConfigEntry<bool> _allowCheats = null!;
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
    private bool _positioned;
    private Toggle? _visibilityToggle;
    private int _capturedFrame = -1;
    private bool _pointerCapture;
    private bool _clearFocus;
    private bool _escapeDown;
    private float _nextRefresh;
    private int _updatedFrame = -1;

    internal bool TryGetItemTooltip(out (string, string) tooltip)
    {
        tooltip = default;
        if (!isActiveAndEnabled || !_enabled.Value || _active == GameSurface.Kind.Closed || PauseHandler.paused ||
            SettingsMenu.instance || (ConsoleScript.instance && ConsoleScript.instance.active)) return false;
        var mouse = Input.mousePosition;
        return _window.TryGetItemTooltip(new Vector2(mouse.x, Screen.height - mouse.y), out tooltip);
    }

    internal bool SuppressDuplicate => isActiveAndEnabled && _enabled.Value && _replaceUei.Value && _active != GameSurface.Kind.Closed;
    internal bool OwnsPointer => isActiveAndEnabled && _enabled.Value && _active != GameSurface.Kind.Closed &&
        (_pointerCapture || _window.Hit(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)));
    internal bool OwnsKeyboard => isActiveAndEnabled && _enabled.Value && _active != GameSurface.Kind.Closed &&
        (_window.SearchFocused || _capturedFrame == Time.frameCount);
    internal bool IsNativeInventory => _active == GameSurface.Kind.Inventory;
    internal bool ShouldBlockInput => isActiveAndEnabled && (_capturedFrame == Time.frameCount ||
        (_active is GameSurface.Kind.Inventory or GameSurface.Kind.NativePanel && (OwnsKeyboard || OwnsPointer && AnyInteractTransition())));
    internal string RecipeKey => _recipes.Value.ToString();
    internal string UsageKey => _usages.Value.ToString();
    internal string BookmarkKey => _bookmark.Value.ToString();
    internal bool Chinese => _language.Value == "zh-CN" || (_language.Value == "auto" &&
        ((Locale.currentLangName?.IndexOf("zh", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
        (Locale.currentLangName?.Contains("中文") ?? false) || Application.systemLanguage == SystemLanguage.ChineseSimplified ||
        Application.systemLanguage == SystemLanguage.ChineseTraditional));
    internal bool AllowCheats => _allowCheats.Value;
    internal float Scale => Mathf.Clamp(_scale.Value, 0.7f, 1.4f);

    /// <summary>Updates the explicit local-cheat opt-in from NEI's own settings screen.</summary>
    internal void SetAllowCheats(bool value)
    {
        _allowCheats.Value = value;
        if (!value) NeiActions.Enabled = false;
    }

    /// <summary>Cycles Recipe → Utility → Cheat and returns a localized notice for the UI.</summary>
    internal bool TryCycleMode(out string notice) => NeiActions.TryCycleMode(out notice);

    /// <summary>Compatibility overload for icon handlers that only need to know whether cycling succeeded.</summary>
    internal bool TryCycleMode() => NeiActions.TryCycleMode(out _);

    private void Awake()
    {
        _enabled = Config.Bind("NotEnoughItems", "Enabled", true, "Show NEI beside inventory and the optional Inventory Terminal. Also changed by Game Settings, the toggle key, and Hide overlay.");
        _replaceUei = Config.Bind("NotEnoughItems", "ReplaceUEIPanel", true, "Hide the legacy UEI panel while NEI is visible; other UEI functions are untouched.");
        _allowCheats = Config.Bind("NotEnoughItems", "AllowLocalCheats", false, "Allow entering the optional single-player cheat mode. Never applies to a multiplayer game.");
        _scale = Config.Bind("NotEnoughItems", "Scale", 1f, new ConfigDescription("Sidebar scale multiplier.", new AcceptableValueRange<float>(0.7f, 1.4f)));
        _toggle = Config.Bind("NotEnoughItems", "Toggle", KeyCode.O, "Show or hide the NEI overlay on the open surface.");
        _recipes = Config.Bind("NotEnoughItems", "RecipesKey", KeyCode.R, "Look up recipes for the hovered item.");
        _usages = Config.Bind("NotEnoughItems", "UsagesKey", KeyCode.U, "Look up uses for the hovered item.");
        _bookmark = Config.Bind("NotEnoughItems", "BookmarkKey", KeyCode.A, "Bookmark the hovered item.");
        _toggleBookmarks = Config.Bind("NotEnoughItems", "ToggleBookmarks", KeyCode.B, "Show or hide the bookmark panel.");
        _language = Config.Bind("NotEnoughItems", "Language", "auto", new ConfigDescription("Interface language.", new AcceptableValueList<string>("auto", "en", "zh-CN")));
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
            Chinese ? "NEI · 显示侧栏" : "NEI · Show overlay",
            Chinese ? "在背包和库存终端显示 NEI；隐藏后可在这里重新开启。" : "Show NEI in inventory and the Inventory Terminal. You can re-enable it here after hiding it.",
            category, _enabled.Value, _ => MirrorVisibility()));
        ModOptionsRegistry.Register(ModOptionDefinition.Bool("NotEnoughItems.AllowLocalCheats",
            Chinese ? "NEI · 允许单机作弊" : "NEI · Allow local cheats",
            Chinese ? "明确启用后，NEI 才能在确认的单机运行中进入实用／作弊模式；多人或未知状态始终拒绝。" : "Explicit opt-in. NEI can enter Utility/Cheat mode only in a confirmed single-player run; multiplayer and unknown states are always refused.",
            category, _allowCheats.Value, value =>
            {
                _allowCheats.Value = value;
                if (!value) NeiActions.Enabled = false;
            }));
        ModOptionsRegistry.Register(ModOptionDefinition.Float("NotEnoughItems.Scale", "NEI · Sidebar scale", "Change the size of the item and bookmark panels.", category, _scale.Value, 0.7f, 1.4f, v => _scale.Value = v));
        foreach (var bind in new[] { _toggle, _recipes, _usages, _bookmark, _toggleBookmarks })
            ModOptionsRegistry.Register(ModOptionDefinition.Keybind("NotEnoughItems." + bind.Definition.Key,
                "NEI · " + bind.Definition.Key, bind.Description.Description, category, bind.Value, v => bind.Value = v));
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
        // ConfigEntry persists this value; every writer, including O and Hide overlay,
        // goes through it. Never save settings.json to change unrelated game rows.
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
        ReadInput();
    }

    internal void ReadInput()
    {
        if (_window == null || !isActiveAndEnabled || _updatedFrame == Time.frameCount) return;
        _updatedFrame = Time.frameCount;
        _escapeDown = false;
        bool togglePressed = Pressed(_toggle) && _toggle.Value != KeyBinds.GetBind("toggleinventory");
        if (!_enabled.Value && !togglePressed) return;
        DetectSurface();
        if (_active == GameSurface.Kind.Closed) return;
        bool typing = _window.SearchFocused || GameSurface.OtherTextInputFocused() || _surface.TerminalTextInputFocused;
        if (!typing && togglePressed)
        {
            SetOverlayVisible(!_enabled.Value);
            CaptureKey();
            if (_enabled.Value) DetectSurface();
        }
        if (!_enabled.Value) return;
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 1.3f;
            _window.RefreshCatalog(Logger);
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
            if (!_window.SearchHit(mouse) && !_window.GroupHit(mouse) && _window.SearchFocused) _clearFocus = true;
        }
        _escapeDown = _active is GameSurface.Kind.Inventory or GameSurface.Kind.NativePanel && Input.GetKeyDown(KeyBinds.GetBind("pause")) &&
            (_window.ModalOpen || _window.SearchFocused);
        if (_escapeDown)
        {
            CaptureKey();
            if (_window.SearchFocused) _clearFocus = true;
            else _window.Back();
            return;
        }
        if (typing) return;
        if (Pressed(_toggleBookmarks))
        {
            _window.ToggleBookmarks();
            CaptureKey();
            return;
        }
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F))
        {
            if (_window.RequestSearchFocus()) CaptureKey();
            return;
        }
        bool recipes = Pressed(_recipes), usages = Pressed(_usages), bookmark = Pressed(_bookmark);
        if (!recipes && !usages && !bookmark) return;
        var id = _window.ItemAt(mouse);
        string[]? contents = null;
        Item? lookupItem = null;
        if (id == null && !_window.Hit(mouse) && _surface.HoveredItem(_active) is { } native)
        {
            lookupItem = native;
            id = "item:" + native.id;
            if (!bookmark && native.TryGetComponent<WaterContainerItem>(out var container) && container.stack != null)
                contents = container.stack.Where(stack => stack != null && stack.amount > 0 && !string.IsNullOrWhiteSpace(stack.liquidId))
                    .Select(stack => "liquid:" + stack.liquidId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        if (bookmark)
        {
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (_window.HandleBookmarkShortcut(mouse, id, control, shift)) CaptureKey();
            return;
        }
        if (string.IsNullOrEmpty(id)) return;
        _window.Lookup(id, usages, contents, lookupItem);
        CaptureKey();
    }

    private void LateUpdate()
    {
        if (_window == null) return;
        if (_enabled.Value) DetectSurface(refresh: true);
        _surface.SetInputShield(transform, _enabled.Value && _active != GameSurface.Kind.Closed, _window);
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
        if (!switched && _positioned && _bounds.Equals(bounds) && _layoutWidth == Screen.width && _layoutHeight == Screen.height && _layoutScale == scale)
            return;
        _positioned = true;
        _bounds = bounds;
        _layoutWidth = Screen.width;
        _layoutHeight = Screen.height;
        _layoutScale = scale;
        _window.Position(bounds, scale);
    }

    private void OnGUI()
    {
        if (_window == null) return;
        if (_clearFocus)
        {
            if (NeiWindow.IsTextControl(GUI.GetNameOfFocusedControl())) GUI.FocusControl(null);
            _window.SearchFocused = false;
            _clearFocus = false;
        }
        if (_escapeDown && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyBinds.GetBind("pause")) Event.current.Use();
        if (!_enabled.Value || _active == GameSurface.Kind.Closed) return;
        int originalDepth = GUI.depth;
        GUI.depth = -100;
        try
        {
            _window.Draw();
        }
        finally { GUI.depth = originalDepth; }
    }

    private static bool Pressed(ConfigEntry<KeyCode> key) => key.Value != KeyCode.None && Input.GetKeyDown(key.Value);
    private static bool AnyInteractTransition() => Input.GetKeyDown(KeyBinds.GetBind("iteminteract")) ||
        Input.GetKeyUp(KeyBinds.GetBind("iteminteract")) || Input.GetKey(KeyBinds.GetBind("attack")) || Input.mouseScrollDelta.y != 0;
    private void CaptureKey() => _capturedFrame = Time.frameCount;
    internal bool OpenCrafting(global::Recipe recipe) => _surface.OpenCrafting(_active, recipe);
    internal bool CanOpenCrafting(global::Recipe recipe) => GameSurface.CanOpenCrafting(recipe);
    internal void HideOverlay() => SetOverlayVisible(false);

    private void OnDisable()
    {
        NativeTooltipLayer.Restore();
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
        _harmony?.UnpatchSelf();
        NativeTooltipLayer.Release();
        NeiSkin.Release();
        NeiActions.Enabled = false;
        if (Instance == this) Instance = null;
    }
}
