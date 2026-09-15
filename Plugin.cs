using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

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
    public const string PluginVersion = "1.0.0";

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
        if (interact == null || HaveRepairableItems == null || RepairOneItem == null)
        {
            // Fail loudly and harmlessly instead of patching a moved API.
            Logger.LogError("CraftingStation/InventoryGui repair methods were not found; automatic repair is inactive.");
            return;
        }

        harmony.Patch(interact, postfix: new HarmonyMethod(typeof(Plugin), nameof(CraftingStationInteractPostfix)));
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private static void CraftingStationInteractPostfix(CraftingStation __instance, Humanoid user, bool repeat)
    {
        // Valheim 1.0 returns false from CraftingStation.Interact even after it
        // successfully opens the station, so a __result check would prevent every
        // repair. Trigger on the postfix, guarded against failed interactions.
        var player = Player.m_localPlayer;
        if (repeat || !repairEnabled.Value || player == null || user != player ||
            player.GetCurrentCraftingStation() != __instance)
        {
            return;
        }

        var inventoryGui = InventoryGui.instance;
        if (inventoryGui == null)
        {
            return;
        }

        var repaired = 0;
        while (repaired < 256 && HaveRepairableItems!.Invoke(inventoryGui, null) is bool canRepair && canRepair)
        {
            RepairOneItem!.Invoke(inventoryGui, null);
            repaired++;
        }

        if (repaired > 0)
        {
            Log.LogInfo($"Automatically repaired {repaired} item(s).");
            player.Message(MessageHud.MessageType.Center,
                IsKorean ? $"장비와 도구 {repaired}개 수리 완료" : $"Repaired {repaired} equipped item(s)");
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
