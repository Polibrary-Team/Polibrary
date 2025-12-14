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
using PolytopiaBackendBase.Auth;
using PolytopiaBackendBase.Game;
using SevenZip.Compression.LZMA;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements.UIR;
using System.Reflection;
using UnityEngine.EventSystems;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;
using pbb = PolytopiaBackendBase.Common;
using Il2CppSystem.Xml;


namespace Polibrary;

public static class ImprovementManager
{
    private static ManualLogSource GabrielLogOfHell;
    public static void Load(ManualLogSource logger)
    {
        GabrielLogOfHell = logger;
        Harmony.CreateAndPatchAll(typeof(ImprovementManager));

    }

    #region Requirers
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void NewRequirers(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (!__result) return;
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_woundedbuilder")))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (tile.unit.health == tile.unit.GetMaxHealth(gameState))
            {
                __result = false;
                return;
            }
        }
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_fullhealthbuilder")))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (tile.unit.health != tile.unit.GetMaxHealth(gameState))
            {
                __result = false;
                return;
            }
        }
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_needsfriendly")))
        {
            bool f_success = false;
            foreach (var tile2 in gameState.Map.GetTileNeighbors(tile.coordinates))
            {
                if (tile2 != null || tile2.coordinates != WorldCoordinates.NULL_COORDINATES)
                {
                    if (tile2.unit != null && tile2.unit.owner == playerState.Id)
                    {
                        f_success = true;
                    }
                }
            }
            if (!f_success)
            {
                __result = false;
                return;
            }
        }
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_needsenemy")))
        {
            bool f_success = false;
            foreach (var tile2 in gameState.Map.GetTileNeighbors(tile.coordinates))
            {
                if (tile2 != null || tile2.coordinates != WorldCoordinates.NULL_COORDINATES)
                {
                    if (tile2.unit != null && tile2.unit.owner != playerState.Id)
                    {
                        f_success = true;
                    }
                }
            }
            if (!f_success)
            {
                __result = false;
                return;
            }
        }
    }
    #endregion

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void polib_nobuilder(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_nobuilder")))
        {
            if (tile.unit != null)
            {
                __result = false;
                return;
            }
        }
    }

    #region GLD Builders

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltBySpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (Parse.BuildersDict.TryGetValue(improvement.type, out string ability))
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
        if (Parse.NoBuildersDict.TryGetValue(improvement.type, out string ability2))
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
        Parse.unitAbilityWhitelist.TryGetValue(improvement.type, out List<UnitAbility.Type> allowAbilityList);
        Parse.unitWhitelist.TryGetValue(improvement.type, out List<UnitData.Type> allowList);
        Parse.unitAbilityBlacklist.TryGetValue(improvement.type, out List<UnitAbility.Type> denyAbilityList);
        Parse.unitBlacklist.TryGetValue(improvement.type, out List<UnitData.Type> denyList);

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
        if (Parse.ImpBuildersDict.TryGetValue(improvement.type, out string ability))
        {
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

    #region Actions

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void BuildActionnaire(BuildAction __instance, GameState gameState)
    {
        var data = gameState.GameLogicData.GetImprovementData(__instance.Type);
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

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healonce")) && tile.unit != null)
        {
            PolibUtils.HealUnit(gameState, tile.unit, 40);
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_cleanseonce")) && tile.unit != null)
        {
            PolibUtils.CleanseUnit(gameState, tile.unit);
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_gainxp")) && tile.unit != null)
        {
            tile.unit.xp += 3;
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_killunit")) && tile.unit != null)
        {
            gameState.ActionStack.Add(new KillUnitAction(tile.unit.owner, tile.coordinates));
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_research")))
        {

            var unlockableTech = PolibUtils.polibGetUnlockableTech(player);
            if (unlockableTech == null || unlockableTech.Count == 0)
            {
            }
            else
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

    #region ImpPlacements
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void Isolated(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;

        if (!improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_isolated"))) return;

        var list = gameState.Map.GetTileNeighbors(tile.coordinates);
        bool flag = false;

        foreach (var item in list)
        {
            if (item != null && item.improvement != null)
            {
                if (item.improvement.type == improvement.type)
                {
                    flag = true;
                    break;
                }
            }
        }

        if (flag)
        {
            __result = false;
        }
    }

    public static bool OnNative(PlayerState player, TileData tile, GameState gameState)
    {
        return tile.climate == gameState.GameLogicData.GetTribeData(player.tribe).climate;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void Native(ref bool __result, GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
    {
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_native")) && !OnNative(playerState, tile, gameState))
        {
            __result = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void Foreign(ref bool __result, GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
    {
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_foreign")) && OnNative(playerState, tile, gameState))
        {
            __result = false;
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
            if(buildCommand == null) continue;
            if(!GameManager.GameState.GameLogicData.TryGetData(buildCommand.Type, out ImprovementData improvementData)) continue;

            var refLoc = LocalizationUtils.CapitalizeString(Localization.Get(improvementData.displayName));
            var buttons = __instance.buttons.ToArray();
            foreach (var button in buttons)
            {
                Main.modLogger.LogMessage(button.text);
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
        if(Parse.AIScoreDict.TryGetValue(improvementData.type, out float value))
        {
            __result += value;
        }
    }


    #endregion

    #region Custom Description

    /*[HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetInfo))]
    public static void SetImprovementInfo(ref string __result, pbb.SkinType skinOfCurrentLocalPlayer, ImprovementData improvementData, ImprovementState improvementState = null, PlayerState owner = null, TileData tileData = null)
    {
        if (Parse.ImpCustomLocKey.TryGetValue(improvementData.type, out var key))
        {
            __result = Localization.GetSkinned(skinOfCurrentLocalPlayer, key, new Il2CppReferenceArray<Il2CppSystem.Object>(null));
        }
    }
    */
    #endregion
}