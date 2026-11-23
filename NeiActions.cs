using System;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

namespace NotEnoughItems;

internal enum SpawnMode { Nei, SpawnToPlayer }

/// <summary>Optional sandbox actions routed to the local game or the authorized Together command dispatcher.</summary>
internal static class NeiActions
{
    internal enum LocalMode
    {
        Recipe,
        Cheat
    }

    // Ordinary NEI item counts use 64 per stack. Keep at most one such batch per click:
    // the native spawn loop and Together's give command each create one real object per item.
    internal const int MaxGiveAmount = 64;
    private const string TemporaryBottleId = "craftingbottle";
    private const float TemporaryBottleLifetime = 300f; // Same scaled lifetime as the native crafting output.

    internal static int ClampGiveAmount(int amount) => Math.Clamp(amount, 1, MaxGiveAmount);
    internal static int SlotGiveAmount(int button, int configuredAmount) => button == 1 ? 1 : ClampGiveAmount(configuredAmount);
    internal static string? MultiplayerGiveCommand(string itemId, int amount) => CheatPermission.GiveCommand(itemId, amount, MaxGiveAmount);

    // A blank or zero draft doesn't replace the current setting. Saturate oversized
    // digit-only pastes while parsing, without overflowing even for very long input.
    internal static bool TryReadGiveAmount(string text, out int? amount)
    {
        amount = null;
        int value = 0;
        foreach (char digit in text.AsSpan().Trim())
        {
            if (digit is < '0' or > '9') return false;
            value = Math.Min(MaxGiveAmount, value * 10 + (digit - '0'));
        }
        if (value > 0) amount = value;
        return true;
    }

    private static WorldGeneration? _enabledWorld;
    private static Body? _enabledBody;
    private static CheatSession _enabledSession;
    private static LocalMode _mode;

    /// <summary>The two-state NEI mode: recipe browsing or cheat actions.</summary>
    internal static LocalMode Mode => _mode;

    /// <summary>Whether item-giving shortcuts are active for the current mode.</summary>
    internal static bool CanGive => _mode == LocalMode.Cheat && GetContext(out _, out var session) == null && session.Supports("give");

    /// <summary>Whether a cheat mode can be entered. This deliberately excludes opt-in so the UI can explain it.</summary>
    internal static bool CanEnter => GetContext(out _, out _, requireEnabled: false, requireOptIn: false) == null;

    /// <summary>Whether cheat mode is currently enabled.</summary>
    internal static bool Enabled
    {
        get => _mode == LocalMode.Cheat;
        set => SetEnabled(value);
    }

    internal static void DisableOfflineCheats()
    {
        if (_enabledSession.Access == CheatAccess.Offline) SetMode(LocalMode.Recipe);
    }

    /// <summary>Changes cheat mode for compatibility with existing shutdown/reset paths.</summary>
    internal static bool SetEnabled(bool value)
    {
        if (!value)
        {
            SetMode(LocalMode.Recipe);
            return true;
        }

        if (_mode == LocalMode.Recipe && TrySetMode(LocalMode.Cheat)) return true;
        return Enabled;
    }

    /// <summary>Compatibility property used by existing shutdown/reset callers.</summary>
    internal static bool LegacyEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }

    /// <summary>Cycles Recipe → Cheat, after the explicit local-cheat opt-in check.</summary>
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

        EnterMode(LocalMode.Cheat, camera, session);
        notice = Text("NEI mode: Cheat.", "NEI 模式：作弊。");
        return true;
    }

    internal static string ModeName(LocalMode mode) => mode == LocalMode.Cheat
        ? Text("Cheat", "作弊") : Text("Recipe", "配方");

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

    // Vanilla has no filled-liquid spawn; preserve NEI's existing bottled-liquid delivery.
    internal static bool UsesVanillaPickup(SpawnMode mode, CatalogItemKind kind) =>
        mode == SpawnMode.SpawnToPlayer && kind == CatalogItemKind.Item;

    internal static Vector2 SpawnPosition(Vector2 player, int index, bool vanilla) => vanilla
        ? player : player + new Vector2((index % 7 - 3) * .10f, .4f);

    /// <summary>Gives registered items or full temporary bottles to the local player. Returns null on success.</summary>
    private static string? Give(CatalogItem entry, int amount, out int dropped, out bool submitted)
    {
        dropped = 0;
        submitted = false;
        if (_mode != LocalMode.Cheat)
            return Text("Switch NEI to Cheat mode first.", "请先将 NEI 切换到作弊模式。");
        var blocked = GetContext(out var camera, out var session);
        if (blocked != null) return blocked;
        if (camera.dragItem) return Text("Finish dragging the current item first.", "请先结束当前物品的拖拽。");
        if (entry.IsVirtual || entry.Kind is not (CatalogItemKind.Item or CatalogItemKind.Liquid))
            return Text("This entry cannot be given.", "此条目无法刷取。");
        if (amount < 1 || amount > MaxGiveAmount)
            return Text($"Quantity must be 1–{MaxGiveAmount}.", $"数量必须在 1–{MaxGiveAmount} 之间。");

        string itemId = entry.Id;
        if (session.Access != CheatAccess.Offline)
        {
            if (entry.IsLiquid)
                return Text("Together cannot give filled liquid bottles. Liquid giving is single-player only.",
                    "Together 无法发放装满液体的瓶子；液体仅支持单机刷取。");
            var command = MultiplayerGiveCommand(itemId, amount);
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
            // The catalog distinguishes item and liquid IDs, but the game registries decide what can be spawned.
            bool liquid = entry.IsLiquid;
            if (liquid && (Liquids.Registry == null || !Liquids.Registry.TryGetValue(itemId, out var type) || type == null))
                return Text($"Liquid '{itemId}' is not registered in the game.", $"游戏中没有注册液体“{itemId}”。");

            string prefabId = liquid ? TemporaryBottleId : itemId;
            var prefab = Resources.Load<GameObject>(prefabId);
            bool custom = !liquid && !prefab && ItemRegistry.TryGetCustomInfo(itemId, out _);
            if (custom) prefab = CustomInstantiate.ResolvePrefab(itemId);
            if (liquid && (!prefab || !prefab.TryGetComponent<WaterContainerItem>(out _)))
                return Text("The game's temporary bottle is unavailable.", "游戏的临时瓶不可用。");
            if (!prefab || !prefab.TryGetComponent<Item>(out _))
                return Text($"No spawnable item prefab for '{prefabId}'.", $"“{prefabId}”没有可生成的物品预制件。");

            // Follow `spawn … player` in single-player without opening or writing to the console.
            bool vanilla = UsesVanillaPickup(Plugin.Instance?.ItemSpawnMode ?? SpawnMode.Nei, entry.Kind);
            Vector2 playerPosition = vanilla ? (Vector2)body.transform.position : default;
            MarkCheat(world);
            for (; delivered < amount; delivered++)
            {
                var position = SpawnPosition(vanilla ? playerPosition : (Vector2)body.transform.position, delivered, vanilla);
                instance = custom
                    ? CustomInstantiate.InstantiateReturn(itemId, position, Quaternion.identity)
                    : Utils.Create(prefabId, position, 0f);
                if (!instance || !instance.TryGetComponent<Item>(out var item))
                    throw new InvalidOperationException("The item factory did not return an item.");

                if (liquid)
                {
                    // Fill before checking carry capacity so stowing sees the full bottle's weight.
                    if (!instance.TryGetComponent<WaterContainerItem>(out var bottle))
                        throw new InvalidOperationException("The temporary bottle has no liquid container.");
                    FillTemporaryBottle(bottle, itemId);
                    UnityEngine.Object.Destroy(instance, TemporaryBottleLifetime);
                }
                else
                {
                    // Match the native spawn command's default condition and magazine setup.
                    item.SetCondition(1f);
                    if (instance.TryGetComponent<AmmoScript>(out var ammo) && ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                        ammo.rounds = ammo.maxRounds;
                }

                if (!(vanilla ? TryVanillaPickup(body, item) : TryStow(body, item))) dropped++;
                instance = null; // This instance is now with the player or deliberately left nearby.
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

    private static void FillTemporaryBottle(WaterContainerItem bottle, string liquidId)
    {
        float capacity = bottle.Capacity;
        if (!float.IsFinite(capacity) || capacity <= 0f)
            throw new InvalidOperationException("The temporary bottle has no usable capacity.");

        bottle.DrainAll();
        if (bottle.AddLiquid(liquidId, capacity) != capacity)
            throw new InvalidOperationException("The temporary bottle could not be filled.");
    }

    /// <summary>
    /// Handles the Cheat-mode click contract used by the item grid without making ItemSlot know game APIs.
    /// Left click uses the configured quantity, right click gives one. Shift deliberately returns false,
    /// preserving the original lookup gesture instead of silently giving items.
    /// </summary>
    internal static bool TryHandleGiveFromSlot(CatalogItem entry, int button, bool shift, int configuredQuantity,
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

        int amount = SlotGiveAmount(button, configuredQuantity);
        error = Give(entry, amount, out dropped, out submitted);
        return true;
    }

    private static bool TryVanillaPickup(Body body, Item item)
    {
        if (Vector2.Distance(item.transform.position, body.transform.position) < 2f)
            body.AutoPickUpItem(item);
        return item.transform.IsChildOf(body.transform);
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
        if (requireEnabled && _mode != LocalMode.Cheat)
            return Text("Select Cheat mode first.", "请先选择作弊模式。");
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
            return Text("Re-enter Cheat mode for this session.", "请在当前会话重新进入作弊模式。");
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
