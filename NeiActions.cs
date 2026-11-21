using System;
using System.Globalization;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Optional sandbox actions routed to the local game or the authorized Together command dispatcher.</summary>
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
    private static CheatSession _enabledSession;
    private static LocalMode _mode;

    /// <summary>The three-state NEI mode, matching the original recipe/utility/cheat switch.</summary>
    internal static LocalMode Mode => _mode;

    /// <summary>Whether the optional local action panel is in utility or cheat mode.</summary>
    internal static bool Enabled
    {
        get => _mode != LocalMode.Recipe;
        set => SetEnabled(value);
    }

    internal static void DisableOfflineCheats()
    {
        if (_enabledSession.Access == CheatAccess.Offline) SetMode(LocalMode.Recipe);
    }

    /// <summary>Whether utility actions are visible for the current mode.</summary>
    internal static bool ShowsUtilities => _mode is LocalMode.Utility or LocalMode.Cheat;

    /// <summary>Whether item-giving shortcuts are active for the current mode.</summary>
    internal static bool CanGive => _mode == LocalMode.Cheat && GetContext(out _, out var session) == null && session.Supports("give");

    /// <summary>Whether a local mode can be entered. This deliberately excludes opt-in so the UI can explain it.</summary>
    internal static bool CanEnter => GetContext(out _, out _, requireEnabled: false, requireOptIn: false) == null;
    internal static bool CanHeal => GetContext(out _, out var session) == null && session.Supports("heal");
    internal static bool CanSetSpeed => GetContext(out _, out var session) == null &&
        (session.Access == CheatAccess.Offline || session.TimeManipulation && session.Supports("timescale"));

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

    /// <summary>Compatibility property used by existing shutdown/reset callers.</summary>
    internal static bool LegacyEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }

    /// <summary>Cycles Recipe → Utility → Cheat, after the explicit local-cheat opt-in check.</summary>
    internal static bool TryCycleMode(out string notice)
    {
        if (_mode == LocalMode.Cheat)
        {
            SetMode(LocalMode.Recipe);
            notice = Text("NEI mode: Recipe.", "NEI 模式：配方。");
            return true;
        }
        var blocked = GetContext(out var camera, out var session, requireEnabled: false);
        if (blocked != null)
        {
            SetMode(LocalMode.Recipe);
            notice = blocked;
            return false;
        }

        var next = _mode == LocalMode.Utility && SessionMatches(camera, session) ? LocalMode.Cheat : LocalMode.Utility;
        EnterMode(next, camera, session);
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
            _enabledSession = default;
            return;
        }

        if (!TrySetMode(mode))
        {
            _mode = LocalMode.Recipe;
            _enabledWorld = null;
            _enabledBody = null;
            _enabledSession = default;
        }
    }

    private static bool TrySetMode(LocalMode mode)
    {
        if (GetContext(out var camera, out var session, requireEnabled: false) != null) return false;
        EnterMode(mode, camera, session);
        return true;
    }

    private static void EnterMode(LocalMode mode, PlayerCamera camera, CheatSession session)
    {
        _mode = mode;
        _enabledWorld = WorldGeneration.world;
        _enabledBody = camera.body;
        _enabledSession = session;
    }

    private static bool SessionMatches(PlayerCamera camera, CheatSession session) =>
        _enabledWorld == WorldGeneration.world && _enabledBody == camera.body && _enabledSession.Access == session.Access &&
        ReferenceEquals(_enabledSession.Transport, session.Transport);

    /// <summary>Gives registered items to the local player. Returns null on success.</summary>
    internal static string? Give(string itemId, int amount, out int dropped, out bool submitted)
    {
        dropped = 0;
        submitted = false;
        if (_mode != LocalMode.Cheat)
            return Text("Switch NEI to Cheat mode first.", "请先将 NEI 切换到作弊模式。");
        var blocked = GetContext(out var camera, out var session);
        if (blocked != null) return blocked;
        if (camera.dragItem) return Text("Finish dragging the current item first.", "请先结束当前物品的拖拽。");
        if (string.IsNullOrWhiteSpace(itemId)) return Text("No item ID was supplied.", "未提供物品 ID。");
        if (amount < 1 || amount > MaxGiveAmount)
            return Text($"Quantity must be 1–{MaxGiveAmount}.", $"数量必须在 1–{MaxGiveAmount} 之间。");

        itemId = itemId.Trim();
        if (session.Access != CheatAccess.Offline)
        {
            var command = CheatPermission.GiveCommand(itemId, amount, MaxGiveAmount);
            if (command == null) return Text("Not a valid multiplayer item ID or quantity.", "联机物品 ID 或数量无效。");
            var prefab = Resources.Load<GameObject>(itemId);
            if (!prefab || !prefab.TryGetComponent<Item>(out _))
                return Text("Together can only give game resource items through this command.", "Together 的此命令仅支持游戏资源中的物品。");
            return Submit(session, "give", command, out submitted);
        }
        var world = WorldGeneration.world;
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
        out string? error, out int dropped, out bool submitted)
    {
        error = null;
        dropped = 0;
        submitted = false;
        if (button != 0 && button != 1) return false;
        if (shift) return false;

        if (_mode != LocalMode.Cheat)
        {
            error = Text("Switch NEI to Cheat mode first.", "请先将 NEI 切换到作弊模式。");
            return true;
        }

        int amount = button == 1 ? 1 : Mathf.Clamp(configuredQuantity, 1, MaxGiveAmount);
        error = Give(itemId, amount, out dropped, out submitted);
        return true;
    }

    /// <summary>Uses the vanilla heal action without reattaching dismembered limbs.</summary>
    internal static string? Heal(out bool submitted)
    {
        submitted = false;
        var blocked = GetContext(out var camera, out var session);
        if (blocked != null) return blocked;
        if (session.Access != CheatAccess.Offline) return Submit(session, "heal", "heal @m", out submitted);
        try
        {
            MarkCheat(WorldGeneration.world);
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
    internal static string? SetSpeed(int multiplier, out bool submitted)
    {
        submitted = false;
        var blocked = GetContext(out var camera, out var session);
        if (blocked != null) return blocked;
        if (WorldGeneration.world.biomeDepth == 11 || camera.body.forceNoMovement)
            return Text("Speed controls are unavailable here.", "当前位置不允许切换游戏速度。");
        var speed = multiplier switch
        {
            1 => PlayerCamera.SpeedType.Normal,
            5 => PlayerCamera.SpeedType.Fast,
            20 => PlayerCamera.SpeedType.SuperFast,
            _ => (PlayerCamera.SpeedType?)null
        };
        if (!speed.HasValue) return Text("Choose 1×, 5× or 20×.", "请选择 1×、5× 或 20×。");
        if (session.Access != CheatAccess.Offline)
        {
            if (!session.TimeManipulation) return Text("The server does not allow changing game speed.", "服务器未允许改变游戏速度。");
            return Submit(session, "timescale", "timescale " + multiplier.ToString(CultureInfo.InvariantCulture), out submitted);
        }
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

    private static string? GetContext(out PlayerCamera camera, out CheatSession session,
        bool requireEnabled = true, bool requireOptIn = true)
    {
        camera = PlayerCamera.main;
        var world = WorldGeneration.world;
        session = default;
        if (requireEnabled && _mode == LocalMode.Recipe)
            return Text("Select Utility or Cheat mode first.", "请先选择实用或作弊模式。");
        if (!camera || !world || world.generatingWorld || world.cutsceneActive || PauseHandler.paused ||
            camera.didDeathScreen || camera.selfDestructActive || !camera.body || !camera.body.alive || !camera.body.conscious ||
            camera.gameObject.scene != world.gameObject.scene || camera.body.gameObject.scene != world.gameObject.scene)
            return Text("An active run is required.", "需要正在进行的游戏。");
        try { session = CheatSession.Detect(camera.body); }
        catch (Exception) { /* Missing or incompatible integration: deny, never fall back to offline. */ }
        if (session.Access == CheatAccess.Unavailable)
            return Text("Cheats are not permitted or the current session could not be verified.", "当前会话未授予作弊权限，或暂时无法验证权限。");
        if (requireOptIn && session.Access == CheatAccess.Offline && Plugin.Instance?.AllowCheats != true)
            return Text("Enable single-player cheats in NEI settings first.", "请先在 NEI 设置中允许单机作弊。");
        if (requireEnabled && !SessionMatches(camera, session))
            return Text("Re-enter Utility or Cheat mode for this session.", "请在当前会话重新进入实用或作弊模式。");
        return null;
    }

    private static string? Submit(CheatSession session, string name, string command, out bool submitted)
    {
        submitted = false;
        try
        {
            if (!session.Supports(name)) return Text("Together does not expose this command for your role.", "Together 未向当前角色开放此命令。");
            session.Dispatch(command);
            submitted = true;
            return null;
        }
        catch (Exception error)
        {
            LogFailure(name, error);
            return Text("Could not submit the command to Together.", "无法向 Together 提交命令。");
        }
    }

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
