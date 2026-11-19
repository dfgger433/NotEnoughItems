using System;
using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NotEnoughItems;

/// <summary>BepInEx entry point for the Casualties: Unknown NEI port.</summary>
[BepInPlugin(Guid, "Not Enough Items", "1.0.0")]
[BepInDependency("net.cucorelib", "1.1")]
[BepInDependency(GameSurface.TerminalGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("casualtiesunknown.uei", BepInDependency.DependencyFlags.SoftDependency)]
[DefaultExecutionOrder(-300)]
public sealed class Plugin : BaseUnityPlugin
{
    /// <summary>The stable plug-in identifier to use in a BepInDependency attribute.</summary>
    public const string Guid = "casualtiesunknown.notenoughitems";
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
    private Rect _bounds;
    private int _capturedFrame = -1;
    private bool _pointerCapture;
    private bool _clearFocus;
    private bool _escapeDown;
    private float _nextRefresh;
    private int _updatedFrame = -1;

    internal bool SuppressDuplicate => isActiveAndEnabled && _enabled.Value && _replaceUei.Value && _active != GameSurface.Kind.Closed;
    internal bool OwnsPointer => isActiveAndEnabled && _enabled.Value && _active != GameSurface.Kind.Closed &&
        (_pointerCapture || _window.Hit(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)));
    internal bool OwnsKeyboard => isActiveAndEnabled && _enabled.Value && _active != GameSurface.Kind.Closed &&
        (_window.SearchFocused || _window.ModalOpen || _capturedFrame == Time.frameCount);
    internal bool IsNativeInventory => _active == GameSurface.Kind.Inventory;
    internal bool CoversTerminal => isActiveAndEnabled && _enabled.Value && _active == GameSurface.Kind.Terminal && _window.ModalOpen;
    internal bool ShouldBlockInput => isActiveAndEnabled && (_capturedFrame == Time.frameCount ||
        (_active == GameSurface.Kind.Inventory && (OwnsKeyboard || OwnsPointer && AnyInteractTransition())));
    internal string RecipeKey => _recipes.Value.ToString();
    internal string UsageKey => _usages.Value.ToString();
    internal string BookmarkKey => _bookmark.Value.ToString();
    internal bool Chinese => _language.Value == "zh-CN" || (_language.Value == "auto" &&
        ((Locale.currentLangName?.IndexOf("zh", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
        (Locale.currentLangName?.Contains("中文") ?? false) || Application.systemLanguage == SystemLanguage.ChineseSimplified ||
        Application.systemLanguage == SystemLanguage.ChineseTraditional));
    internal bool AllowCheats => _allowCheats.Value;
    internal float Scale => Mathf.Clamp(_scale.Value, 0.7f, 1.4f);

    private void Awake()
    {
        _enabled = Config.Bind("NotEnoughItems", "Enabled", true, "Show NEI beside inventory and the optional Inventory Terminal.");
        _replaceUei = Config.Bind("NotEnoughItems", "ReplaceUEIPanel", true, "Hide the legacy UEI panel while NEI is visible; other UEI functions are untouched.");
        _allowCheats = Config.Bind("NotEnoughItems", "AllowLocalCheats", false, "Allow entering the optional single-player cheat mode. Never applies to a multiplayer game.");
        _scale = Config.Bind("NotEnoughItems", "Scale", 1f, new ConfigDescription("Sidebar scale multiplier.", new AcceptableValueRange<float>(0.7f, 1.4f)));
        _toggle = Config.Bind("NotEnoughItems", "Toggle", KeyCode.O, "Show or hide the NEI overlay on the open surface.");
        _recipes = Config.Bind("NotEnoughItems", "RecipesKey", KeyCode.R, "Look up recipes for the hovered item.");
        _usages = Config.Bind("NotEnoughItems", "UsagesKey", KeyCode.U, "Look up uses for the hovered item.");
        _bookmark = Config.Bind("NotEnoughItems", "BookmarkKey", KeyCode.A, "Bookmark the hovered item.");
        _toggleBookmarks = Config.Bind("NotEnoughItems", "ToggleBookmarks", KeyCode.B, "Show or hide the bookmark panel.");
        _language = Config.Bind("NotEnoughItems", "Language", "auto", new ConfigDescription("Interface language.", new AcceptableValueList<string>("auto", "en", "zh-CN")));
        RegisterOptions();
        _surface = new GameSurface(Logger);
        _window = new NeiWindow(this, Config);
        Instance = this;
        _harmony = new Harmony(Guid);
        GameSurface.InstallPatches(_harmony, Logger);
        Logger.LogInfo("Not Enough Items loaded; CUCoreLib settings registered.");
    }

    private void RegisterOptions()
    {
        const Setting.SettingCategory category = Setting.SettingCategory.Game;
        ModOptionsRegistry.Register(ModOptionDefinition.Bool("NotEnoughItems.Enabled", "NEI · Enabled", "Show the item and bookmark panels on inventory screens.", category, _enabled.Value, v => _enabled.Value = v));
        ModOptionsRegistry.Register(ModOptionDefinition.Float("NotEnoughItems.Scale", "NEI · Sidebar scale", "Change the size of the item and bookmark panels.", category, _scale.Value, 0.7f, 1.4f, v => _scale.Value = v));
        foreach (var bind in new[] { _toggle, _recipes, _usages, _bookmark, _toggleBookmarks })
            ModOptionsRegistry.Register(ModOptionDefinition.Keybind("NotEnoughItems." + bind.Definition.Key,
                "NEI · " + bind.Definition.Key, bind.Description.Description, category, bind.Value, v => bind.Value = v));
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
        if (!_enabled.Value && !Pressed(_toggle)) return;
        DetectSurface();
        if (_active == GameSurface.Kind.Closed) return;
        bool typing = _window.SearchFocused || GameSurface.OtherTextInputFocused() || _surface.TerminalTextInputFocused;
        if (!typing && Pressed(_toggle) && _toggle.Value != KeyBinds.GetBind("toggleinventory"))
        {
            _enabled.Value = !_enabled.Value;
            CaptureKey();
            DetectSurface();
            if (_active == GameSurface.Kind.Closed) return;
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
        _escapeDown = _active == GameSurface.Kind.Inventory && Input.GetKeyDown(KeyBinds.GetBind("pause")) &&
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
            _window.RequestSearchFocus();
            CaptureKey();
            return;
        }
        bool recipes = Pressed(_recipes), usages = Pressed(_usages), bookmark = Pressed(_bookmark);
        if (!recipes && !usages && !bookmark) return;
        var id = _window.ItemAt(mouse);
        if (id == null && !_window.Hit(mouse) && _surface.HoveredItem(_active) is { } native) id = "item:" + native.id;
        if (string.IsNullOrEmpty(id)) return;
        if (bookmark) _window.ToggleBookmark(id);
        else _window.Lookup(id, usages);
        CaptureKey();
    }

    private void LateUpdate()
    {
        if (_window == null) return;
        DetectSurface(refresh: true);
        _surface.SetModalShield(transform, _enabled.Value && _active != GameSurface.Kind.Closed && _window.ModalOpen);
        if (_pointerCapture && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2) &&
            !Input.GetKey(KeyBinds.GetBind("iteminteract"))) _pointerCapture = false;
    }

    private void DetectSurface(bool refresh = false)
    {
        var detected = _surface.Detect(out _bounds, refresh);
        if (detected != _active)
        {
            _clearFocus = true;
            _window.ResetView();
            _nextRefresh = 0;
            _pointerCapture = false;
        }
        _active = detected;
        if (_enabled.Value && _active != GameSurface.Kind.Closed) _window.Position(_bounds, Scale);
        else _window.Hide();
    }

    private void OnGUI()
    {
        if (_window == null) return;
        if (_clearFocus)
        {
            if (GUI.GetNameOfFocusedControl() is NeiWindow.SearchControl or NeiWindow.GroupControl) GUI.FocusControl(null);
            _window.SearchFocused = false;
            _clearFocus = false;
        }
        if (_escapeDown && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyBinds.GetBind("pause")) Event.current.Use();
        if (!_enabled.Value || _active == GameSurface.Kind.Closed) return;
        int originalDepth = GUI.depth;
        GUI.depth = -100;
        try { _window.Draw(); }
        finally { GUI.depth = originalDepth; }
    }

    private static bool Pressed(ConfigEntry<KeyCode> key) => key.Value != KeyCode.None && Input.GetKeyDown(key.Value);
    private static bool AnyInteractTransition() => Input.GetKeyDown(KeyBinds.GetBind("iteminteract")) ||
        Input.GetKeyUp(KeyBinds.GetBind("iteminteract")) || Input.GetKey(KeyBinds.GetBind("attack")) || Input.mouseScrollDelta.y != 0;
    private void CaptureKey() => _capturedFrame = Time.frameCount;
    internal bool OpenCrafting(global::Recipe recipe) => _surface.OpenCrafting(_active, recipe);
    internal bool CanOpenCrafting(global::Recipe recipe) => GameSurface.CanOpenCrafting(recipe);
    internal void HideOverlay() { _enabled.Value = false; _clearFocus = true; _window.Hide(); }

    private void OnDisable()
    {
        _surface?.SetModalShield(transform, false);
        _window?.ResetView();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        NeiSkin.Release();
        NeiActions.Enabled = false;
        if (Instance == this) Instance = null;
    }
}
