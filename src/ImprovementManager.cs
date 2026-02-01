using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Linq.Expressions.Interpreter;
using JetBrains.Annotations;
using Polytopia.Data;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;
using pbb = PolytopiaBackendBase.Common;
using Il2CppSystem.Xml;
using Polibrary.Parsing;
using Il2CppMono.Security.Protocol.Ntlm;


namespace Polibrary;

// imp = improvement

public static class ImprovementManager
{
    private static ManualLogSource GabrielLogOfHell;
    public static void Load(ManualLogSource logger)
    {
        GabrielLogOfHell = logger;
        Harmony.CreateAndPatchAll(typeof(ImprovementManager));

    }

    #region Can build imp?

    // check for every single prebuilt ability once to reduce number of patches, loops and LoC
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void OnePatchToRuleThemAll(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (!improvementFlag(gameState, tile, playerState, improvement))
        {
            __result = false;
        }
    }

    public static bool improvementFlag(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
    {

        if (tile.unit != null)
        {
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_woundedbuilder")) && tile.unit.health == tile.unit.GetMaxHealth(gameState))
                return false;
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_fullhealthbuilder")) && tile.unit.health != tile.unit.GetMaxHealth(gameState))
                return false;
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_veteranbuilder")) && tile.unit.promotionLevel < 1)
                return false;
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_nobuilder")))
                return false;
        }

        if(impNeighborCheckers(gameState, tile, playerState, improvement)) return false;

        if(PolibUtils.IsTileNative(playerState, tile, gameState))
        {
            if(improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_foreign")))
                return false;
        }
        else if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_native"))) return false;

        return true;
    }

    public static bool impNeighborCheckers(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement) // true iff imp fails
    {
        // Next the check-neighboring-tiles requirements
        // the flags indicate their success
        bool needsfriendly_success = false;
        bool needsenemy_success = false;
        bool isolated_success = true;

        foreach(var tile1 in gameState.Map.GetTileNeighbors(tile.coordinates))
        {
            if(tile1 != null && tile1.coordinates != WorldCoordinates.NULL_COORDINATES)
            {
                if(tile1.unit != null)
                {
                    if(tile1.unit.owner == playerState.Id) needsfriendly_success = true;
                    else needsenemy_success = true;
                }
                if(tile1.improvement != null)
                {
                    if(tile1.improvement.type == improvement.type) isolated_success = false;
                }
            }
        }

        return (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_needsfriendly")) && !needsfriendly_success) || (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_needsenemy")) && !needsenemy_success) || (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_isolated")) && !isolated_success);
    }
    #endregion



    #region GLD Builders

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltBySpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (Parsing.Parse.BuildersDict.TryGetValue(improvement.type, out string ability))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (!tile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType(ability)))
            {
                __result = false;
            }
        }
        if (Parsing.Parse.NoBuildersDict.TryGetValue(improvement.type, out string ability2))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (tile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType(ability2)))
            {
                __result = false;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void CheckImprovementPlaceability(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        Parsing.Parse.unitAbilityWhitelist.TryGetValue(improvement.type, out List<UnitAbility.Type> allowAbilityList);
        Parsing.Parse.unitWhitelist.TryGetValue(improvement.type, out List<UnitData.Type> allowList);
        Parsing.Parse.unitAbilityBlacklist.TryGetValue(improvement.type, out List<UnitAbility.Type> denyAbilityList);
        Parsing.Parse.unitBlacklist.TryGetValue(improvement.type, out List<UnitData.Type> denyList);

        if (allowAbilityList == null && denyAbilityList == null && allowList == null && denyList == null) return;
        if (tile.unit == null)
        {
            return;
        }

        bool value = false;

        foreach (UnitAbility.Type type in tile.unit.UnitData.unitAbilities)
        {
            if (allowAbilityList != null && allowAbilityList.Contains(type)) value = true;
            if (denyAbilityList != null && denyAbilityList.Contains(type)) value = false;
        }
        if (allowList != null && allowList.Contains(tile.unit.type)) value = true;
        if (denyList != null && denyList.Contains(tile.unit.type)) value = false;

        __result = value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltOnSpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        int idx = PolibData.FindData(Parse.polibImprovementDatas, improvement.type);
        if(idx >= 0)
        {
            string ability = Parse.polibImprovementDatas[idx].builtOnSpecific;
            if(ability == null) return;
            if (tile.improvement == null)
            {
                __result = false;
                return;
            }
            if (!PolibUtils.DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType(ability)))
            {
                __result = false;
                return;
            }
        }
    }

    #endregion

    #region Demolishable
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetImprovementAbilities))]
    public static void Can_Demolish_This_Actually(ref Il2CppSystem.Collections.Generic.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
    {
        if (player.Id != gameState.CurrentPlayer)
        {
            return;
        }
        if (tile == null || tile.improvement == null)
        {
            return;
        }

        if (tile.CanDestroy(gameState, player) && gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
        {
            if (!player.HasAbility(PlayerAbility.Type.Destroy, gameState) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_demolishable")) && player.tribe != pbb.TribeType.Cymanti)
            {
                __result.Add(new DestroyCommand(player.Id, tile.coordinates));
            }
            if (!tile.improvement.HasEffect(ImprovementEffect.decomposing) && !player.HasAbility(PlayerAbility.Type.Decompose, gameState) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_demolishable")) && player.tribe == pbb.TribeType.Cymanti)
            {
                __result.Add(new DecomposeCommand(player.Id, tile.coordinates));
            }
        }
    }
    #endregion

    #region OnBuild Actions

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void BuildActionnaire(BuildAction __instance, GameState gameState)
    {
        var data = PolibUtils.DataFromType(__instance.Type);
        TileData tile = gameState.Map.GetTile(__instance.Coordinates);
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState player);

        if (tile == null)
        {
            return;
        }
        if (data == null || player == null)
        {
            return;
        }

        if (tile.unit != null)
        {
            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healonce")))
            {
                PolibUtils.HealUnit(gameState, tile.unit, 40);
            }

            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_cleanseonce")))
            {
                PolibUtils.CleanseUnit(tile.unit);
            }

            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_gainxp")))
            {
                tile.unit.xp += 3;
            }

            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_killunit")))
            {
                gameState.ActionStack.Add(new KillUnitAction(tile.unit.owner, tile.coordinates));
            }
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_research")))
        {

            var unlockableTech = PolibUtils.polibGetUnlockableTech(player);
            if (unlockableTech != null && unlockableTech.Count != 0)
            {
                var tech = unlockableTech[gameState.RandomHash.Range(0, unlockableTech.Count, tile.coordinates.X, tile.coordinates.Y)];
                gameState.ActionStack.Add(new ResearchAction(player.Id, tech.type, 0));
            }
        }
        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_reveal")))
        {
            foreach (var tile2 in gameState.Map.GetArea(tile.coordinates, 2, true, true))
            {
                if (tile2 != null && tile2.coordinates != WorldCoordinates.NULL_COORDINATES)
                {
                    gameState.ActionStack.Add(new ExploreAction(player.Id, tile2.coordinates));
                }
            }
        }

    }

    #endregion

    #region UI

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.AddUnitActionButtons))]
    public static void TryShowCost(InteractionBar __instance, Il2Gen.List<CommandBase> availableActions)
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
    #endregion

    #region AI

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AI), nameof(AI.GetImprovementScore))]
    public static void AIBoost(ref float __result, GameState gameState, ImprovementData improvementData, TileData tileData, PlayerState player)
    {
        if (!gameState.GameLogicData.CanBuild(gameState, tileData, player, improvementData))
        {
            return;
        }
        int idx = PolibData.FindData(Parse.polibImprovementDatas, improvementData.type);
        if(idx >= 0)
        {
            float? value = Parse.polibImprovementDatas[idx].aiScore;
            if(value != null) __result += (float)value;
            Main.modLogger.LogMessage("Aiscore: " + (float)value);
        }
    }


    #endregion

    #region Custom Description

    /*[HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetInfo))]
    public static void SetImprovementInfo(ref string __result, pbb.SkinType skinOfCurrentLocalPlayer, ImprovementData improvementData, ImprovementState improvementState = null, PlayerState owner = null, TileData tileData = null)
    {
        if (Parsing.Parse.ImpCustomLocKey.TryGetValue(improvementData.type, out var key))
        {
            __result = Localization.GetSkinned(skinOfCurrentLocalPlayer, key, new Il2CppReferenceArray<Il2CppSystem.Object>(null));
        }
    }
    */
    #endregion
}