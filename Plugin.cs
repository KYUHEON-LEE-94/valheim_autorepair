using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace UnifiedAutoRepair;

/// <summary>
/// Repairs every eligible item as soon as the player uses a crafting station.
///
/// Split out of Valheim Unified so it can be installed on its own. Behaviour is
/// unchanged from the module it replaces.
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.xman0922.unifiedautorepair";
    public const string PluginName = "Unified Auto Repair";
    public const string PluginVersion = "1.0.1";

    internal static ManualLogSource Log = null!;
    private static ConfigEntry<bool> repairEnabled = null!;

    private static readonly MethodInfo? HaveRepairableItems =
        AccessTools.Method(typeof(InventoryGui), "HaveRepairableItems");
    private static readonly MethodInfo? RepairOneItem =
        AccessTools.Method(typeof(InventoryGui), "RepairOneItem");

    private Harmony? harmony;

    private void Awake()
    {
        Log = Logger;
        repairEnabled = Config.Bind("Automatic repair", "Enabled", true,
            "Repair every eligible equipped item when using a crafting station.");

        harmony = new Harmony(PluginGuid);
        var interact = AccessTools.Method(typeof(CraftingStation), "Interact");
        var setStation = AccessTools.Method(typeof(Player), nameof(Player.SetCraftingStation));
        if (interact == null || HaveRepairableItems == null || RepairOneItem == null)
        {
            // Fail loudly and harmlessly instead of patching a moved API.
            Logger.LogError("CraftingStation/InventoryGui repair methods were not found; automatic repair is inactive.");
            return;
        }

        harmony.Patch(interact, postfix: new HarmonyMethod(typeof(Plugin), nameof(CraftingStationInteractPostfix)));
        // Every station type ends up here when it becomes the active one, so
        // this also covers a station opened through any other path.
        if (setStation != null)
        {
            harmony.Patch(setStation, postfix: new HarmonyMethod(typeof(Plugin), nameof(SetCraftingStationPostfix)));
        }
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private static int lastRunFrame = -1;

    private static void CraftingStationInteractPostfix(CraftingStation __instance, Humanoid user, bool repeat)
    {
        // Valheim 1.0 returns false from CraftingStation.Interact even after it
        // successfully opens the station, so a __result check would prevent every
        // repair. Trigger on the postfix, guarded against failed interactions.
        if (repeat || user != Player.m_localPlayer)
        {
            return;
        }
        TryRepair(__instance, "interact");
    }

    private static void SetCraftingStationPostfix(Player __instance, CraftingStation station)
    {
        if (station != null && __instance == Player.m_localPlayer)
        {
            TryRepair(station, "station");
        }
    }

    private static void TryRepair(CraftingStation station, string trigger)
    {
        var player = Player.m_localPlayer;
        var inventoryGui = InventoryGui.instance;
        if (!repairEnabled.Value || player == null || inventoryGui == null)
        {
            return;
        }
        if (player.GetCurrentCraftingStation() != station)
        {
            Log.LogInfo($"Auto repair skipped at {station.m_name} ({trigger}): it is not the player's active station.");
            return;
        }
        // Both hooks fire for one interaction; repair once.
        if (lastRunFrame == Time.frameCount)
        {
            return;
        }
        lastRunFrame = Time.frameCount;

        var repaired = 0;
        while (repaired < 256 && HaveRepairableItems!.Invoke(inventoryGui, null) is bool canRepair && canRepair)
        {
            RepairOneItem!.Invoke(inventoryGui, null);
            repaired++;
        }

        if (repaired > 0)
        {
            Log.LogInfo($"Automatically repaired {repaired} item(s) at {station.m_name} (level {station.GetLevel()}).");
            player.Message(MessageHud.MessageType.Center,
                IsKorean ? $"장비와 도구 {repaired}개 수리 완료" : $"Repaired {repaired} equipped item(s)");
            return;
        }

        Diagnose(player, station);
    }

    /// <summary>
    /// Nothing was repaired. Log, per damaged item, the reason Valheim's own
    /// repair rules gave, so a report like "it does not work at the forge"
    /// can be answered from the log alone.
    /// </summary>
    private static void Diagnose(Player player, CraftingStation station)
    {
        var worn = new List<ItemDrop.ItemData>();
        player.GetInventory().GetWornItems(worn);
        if (worn.Count == 0)
        {
            return;
        }

        var level = Mathf.Min(station.GetLevel(), 4);
        var usable = station.CheckUsable(player, showMessage: false);
        Log.LogInfo($"Auto repair at {station.m_name}: level {station.GetLevel()}, usable {usable}, " +
                    $"canRepair {station.m_canRepair}, {worn.Count} damaged item(s) but none repairable here.");
        foreach (var item in worn)
        {
            string reason;
            var recipe = ObjectDB.instance != null ? ObjectDB.instance.GetRecipe(item) : null;
            if (!item.m_shared.m_canBeReparied)
            {
                reason = "the item cannot be repaired";
            }
            else if (recipe == null)
            {
                reason = "no recipe";
            }
            else
            {
                var repairName = recipe.m_repairStation != null ? recipe.m_repairStation.m_name : null;
                var craftName = recipe.m_craftingStation != null ? recipe.m_craftingStation.m_name : null;
                if (repairName != station.m_name && craftName != station.m_name && item.m_worldLevel >= Game.m_worldLevel)
                {
                    reason = $"repaired at {repairName ?? craftName ?? "(none)"}";
                }
                else if (level < recipe.m_minStationLevel)
                {
                    reason = $"needs station level {recipe.m_minStationLevel}, this one is {level}";
                }
                else
                {
                    reason = usable ? "unknown" : "station not usable here (roof or fire)";
                }
            }
            Log.LogInfo($"  {item.m_shared.m_name} {item.m_durability:0}/{item.GetMaxDurability():0}: {reason}");
        }
    }

    private static bool IsKorean
    {
        get
        {
            var localization = Localization.instance;
            return localization != null &&
                   string.Equals(localization.GetSelectedLanguage(), "Korean", StringComparison.OrdinalIgnoreCase);
        }
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
