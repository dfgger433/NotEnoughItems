using System;
using System.Reflection;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Game-side implementations of the optional local sandbox actions.</summary>
internal static class NeiActions
{
    internal enum LocalMode
    {
        Recipe,
        Utility,
        Cheat
    }

    internal const int MaxGiveAmount = 64;
    private static WorldGeneration? _enabledWorld;
    private static Body? _enabledBody;
    private static LocalMode _mode;

    /// <summary>The three-state NEI mode, matching the original recipe/utility/cheat switch.</summary>
    internal static LocalMode Mode => _mode;

    /// <summary>Whether the optional local action panel is in utility or cheat mode.</summary>
    internal static bool Enabled
    {
        get => _mode != LocalMode.Recipe;
        set => SetEnabled(value);
    }

    /// <summary>Whether utility actions are visible for the current mode.</summary>
    internal static bool ShowsUtilities => _mode is LocalMode.Utility or LocalMode.Cheat;

    /// <summary>Whether item-giving shortcuts are active for the current mode.</summary>
    internal static bool CanGive => _mode == LocalMode.Cheat && CanUse;

    /// <summary>Whether the local action panel can be used in this active run.</summary>
    internal static bool CanUse => ShowsUtilities && GetContext(out _, out _, requireEnabled: true, requireOptIn: true) == null;

    /// <summary>Whether a local mode can be entered. This deliberately excludes opt-in so the UI can explain it.</summary>
    internal static bool CanEnter => GetContext(out _, out _, requireEnabled: false, requireOptIn: false) == null;

    /// <summary>Changes the mode for compatibility with existing reset paths.</summary>
    internal static bool SetEnabled(bool value)
    {
        if (!value)
        {
            SetMode(LocalMode.Recipe);
            return true;
        }

        if (_mode == LocalMode.Recipe && TrySetMode(LocalMode.Utility)) return true;
        return Enabled;
    }

    private const string TogetherGuid = "CasualtiesMP";
    private const string LegacyGuid = "KrokoshaCasualtiesMP";
    private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;

    /// <summary>Compatibility property used by existing shutdown/reset callers.</summary>
    internal static bool LegacyEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }

    /// <summary>Cycles Recipe → Utility → Cheat, after the explicit local-cheat opt-in check.</summary>
    internal static bool TryCycleMode(out string notice)
    {
        var blocked = GetContext(out _, out _, requireEnabled: false, requireOptIn: false);
        if (blocked != null)
        {
            notice = blocked;
            return false;
        }

        if (Plugin.Instance?.AllowCheats != true)
        {
            notice = Text("Enable AllowLocalCheats first (this is an explicit opt-in).", "请先启用 AllowLocalCheats（这是明确的单机作弊 opt-in）。");
            return false;
        }

        var next = (LocalMode)(((int)_mode + 1) % 3);
        SetMode(next);
        notice = Text($"NEI mode: {ModeName(next)}.", $"NEI 模式：{ModeName(next)}。");
        return true;
    }

    internal static string ModeName(LocalMode mode) => mode switch
    {
        LocalMode.Utility => Text("Utility", "实用"),
        LocalMode.Cheat => Text("Cheat", "作弊"),
        _ => Text("Recipe", "配方")
    };

    private static void SetMode(LocalMode mode)
    {
        if (mode == LocalMode.Recipe)
        {
            _mode = mode;
            _enabledWorld = null;
            _enabledBody = null;
            return;
        }

        if (!TrySetMode(mode))
        {
            _mode = LocalMode.Recipe;
            _enabledWorld = null;
            _enabledBody = null;
        }
    }

    private static bool TrySetMode(LocalMode mode)
    {
        // The mode switch itself is also an explicit opt-in boundary; legacy callers cannot
        // bypass the OptionsScreen toggle by assigning Enabled=true.
        if (Plugin.Instance?.AllowCheats != true ||
            GetContext(out var camera, out var world, requireEnabled: false, requireOptIn: false) != null)
            return false;
        _mode = mode;
        _enabledWorld = world;
        _enabledBody = camera.body;
        return true;
    }

    /// <summary>Gives registered items to the local player. Returns null on success.</summary>
    internal static string? Give(string itemId, int amount) => Give(itemId, amount, out _);

    /// <summary>Also reports how many newly created items could not be put into inventory and were left nearby.</summary>
    internal static string? Give(string itemId, int amount, out int dropped)
    {
        dropped = 0;
        if (_mode != LocalMode.Cheat)
            return Text("Switch NEI to Cheat mode first.", "请先将 NEI 切换到作弊模式。");
        var blocked = GetContext(out var camera, out var world, requireEnabled: true, requireOptIn: true);
        if (blocked != null) return blocked;
        if (camera.dragItem) return Text("Finish dragging the current item first.", "请先结束当前物品的拖拽。");
        if (string.IsNullOrWhiteSpace(itemId)) return Text("No item ID was supplied.", "未提供物品 ID。");
        if (amount < 1 || amount > MaxGiveAmount)
            return Text($"Quantity must be 1–{MaxGiveAmount}.", $"数量必须在 1–{MaxGiveAmount} 之间。");

        itemId = itemId.Trim();
        var body = camera.body;
        GameObject? instance = null;
        int delivered = 0;
        try
        {
            // The base-game resource and CUCoreLib registry are game-side sources of truth.
            // Do not send a catalog object or a raw command, or instantiate a non-item prefab.
            var prefab = Resources.Load<GameObject>(itemId);
            bool custom = !prefab && ItemRegistry.TryGetCustomInfo(itemId, out _);
            if (custom) prefab = CustomInstantiate.ResolvePrefab(itemId);
            if (!prefab || !prefab.TryGetComponent<Item>(out _))
                return Text($"No spawnable item prefab for '{itemId}'.", $"“{itemId}”没有可生成的物品预制件。");

            MarkCheat(world);
            for (; delivered < amount; delivered++)
            {
                var position = (Vector2)body.transform.position + new Vector2(0f, 0.4f);
                position.x += (delivered % 7 - 3) * 0.10f;
                instance = custom
                    ? CustomInstantiate.InstantiateReturn(itemId, position, Quaternion.identity)
                    : Utils.Create(itemId, position, 0f);
                if (!instance || !instance.TryGetComponent<Item>(out var item))
                    throw new InvalidOperationException("The item factory did not return an item.");

                // Match the native spawn command's default condition and magazine setup.
                item.SetCondition(1f);
                if (instance.TryGetComponent<AmmoScript>(out var ammo) && ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                    ammo.rounds = ammo.maxRounds;

                if (!TryStow(body, item)) dropped++;
                instance = null; // This instance is now in inventory or deliberately left nearby.
            }
            return null;
        }
        catch (Exception error)
        {
            if (instance) UnityEngine.Object.Destroy(instance);
            LogFailure("Give", error);
            return Text($"Could only give {delivered}/{amount} × {itemId}.", $"仅成功发放 {delivered}/{amount} × {itemId}。");
        }
    }

    /// <summary>
    /// Handles the Cheat-mode click contract used by the item grid without making ItemSlot know game APIs.
    /// Left click uses the configured quantity, right click gives one. Shift deliberately returns false,
    /// preserving the original lookup gesture instead of silently giving items.
    /// </summary>
    internal static bool TryHandleGiveFromSlot(string itemId, int button, bool shift, int configuredQuantity,
        out string? error, out int dropped)
    {
        error = null;
        dropped = 0;
        if (button != 0 && button != 1) return false;
        if (shift) return false;

        if (_mode != LocalMode.Cheat)
        {
            error = Text("Switch NEI to Cheat mode first.", "请先将 NEI 切换到作弊模式。");
            return true;
        }

        int amount = shift ? 16 : button == 1 ? 1 : Mathf.Clamp(configuredQuantity, 1, MaxGiveAmount);
        error = Give(itemId, amount, out dropped);
        return true;
    }

    /// <summary>Uses the vanilla heal action without reattaching dismembered limbs.</summary>
    internal static string? Heal()
    {
        var blocked = GetContext(out var camera, out var world, requireEnabled: true, requireOptIn: true);
        if (blocked != null) return blocked;
        try
        {
            MarkCheat(world);
            camera.body.Heal(ignoreDismembered: true);
            return null;
        }
        catch (Exception error)
        {
            LogFailure("Heal", error);
            return Text("The game could not complete healing.", "游戏未能完成治疗。");
        }
    }

    /// <summary>Selects a native simulation speed: 1×, 5× or 20×, not a world-clock setting.</summary>
    internal static string? SetSpeed(int multiplier)
    {
        var blocked = GetContext(out var camera, out var world, requireEnabled: true, requireOptIn: true);
        if (blocked != null) return blocked;
        if (world.biomeDepth == 11 || camera.body.forceNoMovement)
            return Text("Speed controls are unavailable here.", "当前位置不允许切换游戏速度。");
        var speed = multiplier switch
        {
            1 => PlayerCamera.SpeedType.Normal,
            5 => PlayerCamera.SpeedType.Fast,
            20 => PlayerCamera.SpeedType.SuperFast,
            _ => (PlayerCamera.SpeedType?)null
        };
        if (!speed.HasValue) return Text("Choose 1×, 5× or 20×.", "请选择 1×、5× 或 20×。");
        try
        {
            camera.SetTimeScale(speed.Value);
            return camera.curTimeScale == speed.Value && Mathf.Approximately(Time.timeScale, multiplier)
                ? null : Text("The game did not change speed.", "游戏未切换速度。");
        }
        catch (Exception error)
        {
            LogFailure("SetSpeed", error);
            return Text("The game could not change speed.", "游戏未能切换速度。");
        }
    }

    private static bool TryStow(Body body, Item item)
    {
        var stats = item.Stats;
        if (stats.HasTag("noautopickup")) return false;

        // Only touch player-owned surface containers; Container.LoadItem checks capacity and tags.
        // Body.AutoPickUpItem can drop an already-equipped item in its wearable branch.
        if (!stats.onlyHoldInHands)
        {
            foreach (var owned in body.GetSurfaceInventoryItems())
            {
                if (!owned) continue;
                var container = owned.container;
                if (!container || !container.CanHoldItem(item)) continue;
                container.LoadItem(item);
                if (item.transform.parent == container.transform) return true;
            }
        }

        for (int i = 0; i < body.slots.Length; i++)
        {
            var slot = body.slots[i];
            if (!slot || !slot.canPickUp || body.HoldingItem(i) || (stats.onlyHoldInHands && !slot.isHand)) continue;
            if (body.DoPickupCheck(item, noAlerts: true))
            {
                // The empty/eligible slot and reach checks above have already succeeded.
                body.PickUpItem(item, i, force: true);
                if (item.transform.parent == slot.transform) return true;
            }
        }
        return false;
    }

    private static string? GetContext(out PlayerCamera camera, out WorldGeneration world,
        bool requireEnabled = true, bool requireOptIn = true)
    {
        camera = PlayerCamera.main;
        world = WorldGeneration.world;
        if (requireOptIn && Plugin.Instance?.AllowCheats != true)
            return Text("Local cheat mode is not enabled.", "未启用单机作弊模式。");
        if (requireEnabled && _mode == LocalMode.Recipe)
            return Text("Select Utility or Cheat mode first.", "请先选择实用或作弊模式。");
        if (!camera || !world || world.generatingWorld || world.cutsceneActive || PauseHandler.paused ||
            camera.didDeathScreen || camera.selfDestructActive || !camera.body || !camera.body.alive || !camera.body.conscious ||
            camera.gameObject.scene != world.gameObject.scene || camera.body.gameObject.scene != world.gameObject.scene)
            return Text("An active local run is required.", "需要正在进行的本地游戏。");
        if (requireEnabled && (_enabledWorld != world || _enabledBody != camera.body))
            return Text("Re-enter local mode for this run.", "请为当前运行重新进入单机模式。");
        if (!IsSinglePlayer())
            return Text("Sandbox tools require a confirmed single-player session.", "只有确认处于单人游戏时才能使用沙盒工具。");
        return null;
    }

    private static bool IsSinglePlayer()
    {
        try
        {
            var plugins = Chainloader.PluginInfos;
            if (plugins == null) return false;
            // These are the installed Together GUID and the legacy Krokosha GUID. In a patched
            // game, never infer single-player from permissions, host status, or missing bindings.
            if (plugins.TryGetValue(TogetherGuid, out var together))
            {
                var net = together.Instance?.GetType().Assembly.GetType("Together.Net");
                var transport = net?.GetProperty("Transport", StaticPublic);
                if (ReadStaticBool(net, "IsRunning") != false || transport == null || transport.GetValue(null) != null)
                    return false;
            }
            if (plugins.TryGetValue(LegacyGuid, out var legacy))
            {
                var assembly = legacy.Instance?.GetType().Assembly;
                var mp = assembly?.GetType("KrokoshaCasualtiesMP.KrokoshaScavMultiplayer");
                var transport = assembly?.GetType("KrokoshaCasualtiesMP.Net")?.GetProperty("TRANSPORT", StaticPublic);
                if (ReadStaticBool(mp, "network_system_is_running") != false ||
                    ReadStaticBool(mp, "network_system_is_client_and_waiting_connecting") != false ||
                    transport == null || transport.GetValue(null) != null) return false;
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                // A known multiplayer module loaded outside the recognized plugin registration
                // is an unknown state, not positive evidence that the session is offline.
                if ((name == TogetherGuid && !plugins.ContainsKey(TogetherGuid)) ||
                    (name == LegacyGuid && !plugins.ContainsKey(LegacyGuid))) return false;
            }
            return true;
        }
        catch (Exception error)
        {
            LogFailure("Session check", error);
            return false;
        }
    }

    private static bool? ReadStaticBool(Type? type, string name) =>
        type?.GetProperty(name, StaticPublic)?.GetValue(null) is bool value ? value : null;

    private static void MarkCheat(WorldGeneration world)
    {
        // Vanilla CheckForCheats disables achievements for the process outside Tutorial.
        // Unlike invoking console commands, this opens no console and writes no files.
        if (world.biomeOverride != WorldGeneration.OverrideSceneType.Tutorial)
            SteamManager.disableAchievements = true;
    }

    private static string Text(string english, string chinese) => Plugin.Instance?.Chinese == true ? chinese : english;

    private static void LogFailure(string action, Exception error) =>
        Debug.LogWarning($"[Not Enough Items] {action} failed: {error.Message}");
}
