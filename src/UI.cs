using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;

using Il2Gen = Il2CppSystem.Collections.Generic;
using Polibrary.Parsing;

namespace Polibrary;

public static class UI
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(UI));
        modLogger = logger;
    }

    #region BuildingUI

    
    /// Tries showing the cost of manual improvements in the unit interactionbar
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.AddUnitActionButtons))]
    private static void TryShowCost(InteractionBar __instance, Il2Gen.List<CommandBase> availableActions)
    {
        if (__instance == null) return;
        if (GameManager.LocalPlayer == null || GameManager.LocalPlayer.AutoPlay) return;
        if (availableActions == null || availableActions.Count == 0) return;

        var commands = availableActions.ToArray();
        foreach (var command in commands)
        {
            BuildCommand buildCommand = command.TryCast<BuildCommand>();
            if (buildCommand == null) continue;
            if (!GameManager.GameState.GameLogicData.TryGetData(buildCommand.Type, out ImprovementData improvementData)) continue;

            var refLoc = LocalizationUtils.CapitalizeString(Localization.Get(improvementData.displayName));
            var buttons = __instance.buttons.ToArray();
            foreach (var button in buttons)
            {
                if (button.text == refLoc)
                {

                    if (buildCommand != null && improvementData.cost > 0)
                    {
                        button.Cost = improvementData.cost;
                        button.ShowLabel = true;
                        button.m_showLabelBackground = true;
                        button.UpdateLabelVisibility();
                    }
                }
            }
        }
    }

    /// Ability to override entire ability text with custom
    /// klipi mentioned wanting to have a similar thing in polymod when that happens delete this ig
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetInfo))]
    private static void InfoOverride(ref string __result, PolytopiaBackendBase.Common.SkinType skinOfCurrentLocalPlayer, ImprovementData improvementData, ImprovementState improvementState = null, PlayerState owner = null, TileData tileData = null)
    {
        if (PolibData.TryGetValue(Parse.polibImprovementDatas, improvementData.type, nameof(PolibImprovementData.infoOverride), out string result))
            __result = Localization.Get(result);
    }
    #endregion

    #region DiploUI
    private static bool isInDiploHell = false; // Needed for prefix-postfix

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerInfoPopup), nameof(PlayerInfoPopup.UpdateDiplomacyActionButtons))]
    private static void PlayerInfoPopup_Prefix(PlayerState player, PlayerInfoPopup __instance)
    {
        isInDiploHell = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerInfoPopup), nameof(PlayerInfoPopup.UpdateDiplomacyActionButtons))]
    private static void PlayerInfoPopup_Postfix(PlayerState player, PlayerInfoPopup __instance)
    {
        isInDiploHell = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerExtensions), nameof(PlayerExtensions.HasTech))]
    static bool HasTechPrefix_DiplomacyDehardcoded(this PlayerState player, TechData.Type tech, ref bool __result)
    {
        if (isInDiploHell)
        {
            if (tech == TechData.Type.Shields && GameManager.GameState.GameLogicData.IsUnlocked(PlayerAbility.Type.PeaceTreaty, player))
            {
                __result = true;
                return false;
            }

            if (tech == TechData.Type.Diplomacy && GameManager.GameState.GameLogicData.IsUnlocked(PlayerAbility.Type.Embassy, player))
            {
                __result = true;
                return false;
            }
        }
        
        return true; 
    }


    #endregion

    #region TechUI

    /// HIDDENITEM - with prefixpostfix
    /// 
    
    private class HiddenState
    {
        public List<UnitData> HiddenUnits = new List<UnitData>();
        public List<ImprovementData> HiddenImprovements = new List<ImprovementData>();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechPopup), nameof(TechPopup.SetTechData))]
    private static void HiddenItemPrefix2(TechData data, ref HiddenState __state)
    {
        if(data == null) return;
        __state = new HiddenState();

        if(data.improvementUnlocks != null && data.improvementUnlocks.Count > 0)
        {
            List<ImprovementData> impsToRemove = new List<ImprovementData>();

            foreach(ImprovementData imp in data.improvementUnlocks)
            {
                bool success = PolibData.TryGetValue(Parse.polibImprovementDatas, imp.type, nameof(PolibImprovementData.hiddenItem), out bool isHidden);
                if (success & isHidden)
                {
                    impsToRemove.Add(imp);
                }
            }
            foreach(ImprovementData imp in impsToRemove)
            {
                data.improvementUnlocks.Remove(imp);
                __state.HiddenImprovements.Add(imp);
            }
        }
        if(data.unitUnlocks != null && data.unitUnlocks.Count > 0)
        {
            List<UnitData> unitsToRemove = new List<UnitData>();
            foreach(UnitData unit in data.unitUnlocks)
            {
                bool success = PolibData.TryGetValue(Parse.polibUnitDatas, unit.type, nameof(PolibUnitData.hiddenItem), out bool isHidden);
                if(success & isHidden)
                {
                    unitsToRemove.Add(unit);
                }
            }
            foreach(UnitData unit in unitsToRemove)
            {
                data.unitUnlocks.Remove(unit);
                __state.HiddenUnits.Add(unit);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TechPopup), nameof(TechPopup.SetTechData))]
    private static void HiddenItemPostfix2(TechData data, ref HiddenState __state)
    {
        if(__state == null || data == null) return;
        foreach(var imp in __state.HiddenImprovements)
        {
            data.improvementUnlocks.Add(imp);
        }
        foreach(var unit in __state.HiddenUnits)
        {
            data.unitUnlocks.Add(unit);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechItem), nameof(TechItem.GetUnlockItems))]
    private static void HiddenItemPrefix(TechData techData, PlayerState playerState, bool onlyPickFirstItem, ref HiddenState __state)
    {
        if(techData == null) return;
        __state = new HiddenState();

        if(techData.improvementUnlocks != null && techData.improvementUnlocks.Count > 0)
        {
            List<ImprovementData> impsToRemove = new List<ImprovementData>();

            foreach(ImprovementData imp in techData.improvementUnlocks)
            {
                bool success = PolibData.TryGetValue(Parse.polibImprovementDatas, imp.type, nameof(PolibImprovementData.hiddenItem), out bool isHidden);
                if (success & isHidden)
                {
                    impsToRemove.Add(imp);
                }
            }
            foreach(ImprovementData imp in impsToRemove)
            {
                techData.improvementUnlocks.Remove(imp);
                __state.HiddenImprovements.Add(imp);
            }
        }
        if(techData.unitUnlocks != null && techData.unitUnlocks.Count > 0)
        {
            List<UnitData> unitsToRemove = new List<UnitData>();
            foreach(UnitData unit in techData.unitUnlocks)
            {
                bool success = PolibData.TryGetValue(Parse.polibUnitDatas, unit.type, nameof(PolibUnitData.hiddenItem), out bool isHidden);
                if(success & isHidden)
                {
                    unitsToRemove.Add(unit);
                }
            }
            foreach(UnitData unit in unitsToRemove)
            {
                techData.unitUnlocks.Remove(unit);
                __state.HiddenUnits.Add(unit);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TechItem), nameof(TechItem.GetUnlockItems))]
    private static void HiddenItemPostfix(TechData techData, PlayerState playerState, bool onlyPickFirstItem, ref HiddenState __state)
    {
        if(__state == null || techData == null) return;
        foreach(var imp in __state.HiddenImprovements)
        {
            techData.improvementUnlocks.Add(imp);
        }
        foreach(var unit in __state.HiddenUnits)
        {
            techData.unitUnlocks.Add(unit);
        }
    }


    #endregion
}