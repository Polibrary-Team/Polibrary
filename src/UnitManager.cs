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
using PolytopiaBackendBase;


namespace Polibrary;

public static class UnitManager
{
    private static ManualLogSource jeremy;
    public static void Load(ManualLogSource logger)
    {
        // rest in peace steve, you had a good run, 2025-2025

        jeremy = logger; // f you jeremy
        //yeah jeremy go fuck yourself

        Harmony.CreateAndPatchAll(typeof(UnitManager));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetDefenceBonus))]
    public static void DefBonus(this UnitState unit, GameState gameState, ref int __result)
    {
        int defence = 10;
        bool change = false;
        TileData tile = gameState.Map.GetTile(unit.coordinates);

        if (tile.improvement == null) return;

        if (tile.owner == unit.owner && Parse.improvementDefenceBoost.TryGetValue(tile.improvement.type, out int i))
        {
            defence = i;
            change = true;
        }

        if (Parse.freelanceImprovementDefenceBoostDict.TryGetValue(tile.improvement.type, out int j))
        {
            defence = j;
            change = true;
        }

        if (tile.owner == unit.owner && tile.improvement.type == ImprovementData.Type.City && tile.improvement.rewards != null && unit.HasAbility(UnitAbility.Type.Fortify))
        {
            foreach (CityReward reward in tile.improvement.rewards)
            {
                if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
                {
                    if (cityRewardData.defenceBoost != -1)
                    {
                        defence = (defence < cityRewardData.defenceBoost) ? cityRewardData.defenceBoost : defence;
                        change = true;
                    }
                }
            }
        }

        if (change)
        {
            __result = defence;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetDefence))]
    public static void Defence(this UnitState unit, GameState state, ref int __result)
    {
        if (Main.polibGameState.rewardBoostDict.TryGetValue(unit.type, out int num))
        {
            foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unit.type))
            {
                if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
                {
                    __result += cityRewardData.boostDefenceOverSpawn * num;
                }
            }
        }
        
        foreach (UnitEffect effect in unit.effects)
        {
            if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                if (effectData.additives.TryGetValue("defence", out int add))
                {
                    __result += add;
                }
                if (effectData.multiplicatives.TryGetValue("defence", out int mult))
                {
                    __result *= mult;
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMovement))]
    public static void Movement(this UnitState unitState, GameState gameState, ref int __result)
    {
        if (Main.polibGameState.rewardBoostDict.TryGetValue(unitState.type, out int num))
        {
            foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitState.type))
            {
                if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
                {
                    __result += cityRewardData.boostMovementOverSpawn * num;
                }
            }
        }
        
        foreach (UnitEffect effect in unitState.effects)
        {
            if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                if (effectData.additives.TryGetValue("movement", out int add))
                {
                    __result += add;
                }
                if (effectData.multiplicatives.TryGetValue("movement", out int mult))
                {
                    __result *= mult;
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttack), typeof(UnitState), typeof(GameState))]
    public static void Attack(this UnitState unitState, GameState gameState, ref int __result)
    {
        if (Main.polibGameState.rewardBoostDict.TryGetValue(unitState.type, out int num))
        {
            foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitState.type))
            {
                if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
                {
                    __result += cityRewardData.boostAttackOverSpawn * num;
                }
            }
        }
        
        foreach (UnitEffect effect in unitState.effects)
        {
            if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                if (effectData.additives.TryGetValue("attack", out int add))
                {
                    __result += add;
                }
                if (effectData.multiplicatives.TryGetValue("attack", out int mult))
                {
                    __result *= mult;
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetRange), typeof(UnitState), typeof(GameState))]
    public static void Range(this UnitState unitState, GameState gameState, ref int __result)
    {   
        foreach (UnitEffect effect in unitState.effects)
        {
            if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                if (effectData.additives.TryGetValue("range", out int add))
                {
                    __result += add;
                }
                if (effectData.multiplicatives.TryGetValue("range", out int mult))
                {
                    __result *= mult;
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMaxHealth))]
    public static void GetMaxHealth(this UnitState unitState, GameState gameState, ref int __result) //man voidmongers using modularity will get me SOO much downloads!!
    {
        if (Main.polibGameState.rewardBoostDict.TryGetValue(unitState.type, out int num))
        {
            foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitState.type))
            {
                if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
                {
                    __result += cityRewardData.boostMaxHpOverSpawn * num;
                }
            }
        }
    }

    #region Agent

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetTrainableUnits))]
    public static void ExcludeAgentsFromCities(ref Il2Gen.List<TrainCommand> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
    {
        //Safety first
        if (player.Id != gameState.CurrentPlayer) //thats cap, if it crashes then why doesnt the user learn to code and fix it themselves, if they whine so much!
        {
            return;
        }
        if (tile.owner != player.Id)
        {
            return;
        }
        if (tile.unit != null)
        {
            return;
        }
        if (tile.improvement == null || tile.improvement.type != ImprovementData.Type.City)
        {
            return;
        }
        __result = new Il2Gen.List<TrainCommand>();
        foreach (UnitData unitData in gameState.GameLogicData.GetUnlockedUnits(player, gameState, false))
        {
            if (!unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_agent")) && CommandValidation.HasUnitTerrain(gameState, tile.coordinates, unitData))
            {
                TrainCommand trainCommand = new TrainCommand(player.Id, unitData.type, tile.coordinates);
                if (!player.blockTrainUnits && (includeUnavailable || trainCommand.IsValid(gameState)))
                {
                    __result.Add(trainCommand);
                }
            }
        }
    }

    public static bool CanBeAgented(WorldCoordinates coords, GameState gameState)
    {
        var tiles = gameState.Map.GetTileNeighbors(coords);
        bool flag = true;
        foreach (TileData tile in tiles)
        {
            if (tile != null && tile.improvement != null)
            {
                if (PolibUtils.DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_preventagent")))
                {
                    flag = false;
                    break;
                }
            }
        }
        return flag;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetTrainableUnits))]
    public static void IncludeAgents(ref Il2Gen.List<TrainCommand> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
    {
        if (player.Id != gameState.CurrentPlayer)
        {
            return;
        }
        if (tile.unit != null)
        {
            return;
        }
        if (tile.HasImprovement(ImprovementData.Type.City))
        {
            return;
        }
        if (tile.owner == player.Id || tile.owner == 0)
        {
            return;
        }
        __result = new Il2Gen.List<TrainCommand>();
        foreach (UnitData unitData in gameState.GameLogicData.GetUnlockedUnits(player, gameState, false))
        {
            if (unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_agent")) && CanBeAgented(tile.coordinates, gameState) && tile.CanBeAccessedByPlayer(gameState, player) && CommandValidation.HasUnitTerrain(gameState, tile.coordinates, unitData))
            {
                TrainCommand trainCommand = new TrainCommand(player.Id, unitData.type, tile.coordinates);
                string text;
                if (includeUnavailable || trainCommand.IsValid(gameState, out text))
                {
                    __result.Add(trainCommand);
                }
            }
        }
    }


    #endregion
    #region Loyal
    // This needs a rewrite so that the command itself isn't valid or the likes

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ConvertAction), nameof(ConvertAction.ExecuteDefault))]
    public static bool Loyal(ConvertAction __instance, GameState gameState)
    {
        TileData tile2 = gameState.Map.GetTile(__instance.Target);
        UnitState unit2 = tile2.unit;

        if (unit2.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_loyal")))
        {
            return false;
        }

        else return true;
    }

    #endregion

    #region Lazy & Demotivate
    [HarmonyPostfix] //literally me
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(BattleHelpers), nameof(BattleHelpers.GetBattleResults))]
    public static void LazyOrDemotivate(GameState gameState, UnitState attackingUnit, UnitState defendingUnit, ref BattleResults __result)
    {
        if (attackingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_lazy")))
        {
            __result.shouldMoveToDefeatedEnemyTile = false;
        }
        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_demotivate")))
        {
            __result.shouldMoveToDefeatedEnemyTile = false;
        }
    }


    #endregion

    #region Blind

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetSightRange))]
    public static void Blinding(this UnitData unitData, ref int __result)
    {
        if (unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_blind")))
        {
            __result = 0;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.ExploreFromTile))]
    public static bool Blinding2(GameState gameState, PlayerState playerState, TileData tile, int sightRange, bool shouldUseActions)
    {
        if (sightRange == 0) return false;
        return true;
    }

    #endregion

    #region Scary
    [HarmonyPostfix] //i genuinely cant remember if this was me or not -wasd_
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    public static void InciteFear(AttackCommand __instance, GameState gameState)
    {
        UnitState aggressor;
        gameState.TryGetUnit(__instance.UnitId, out aggressor);
        TileData tile = gameState.Map.GetTile(__instance.Target);
        if (tile.unit == null) //Flee!
        {
            return;
        }
        UnitState unit = tile.unit;
        PlayerState playerState;
        gameState.TryGetPlayer(__instance.PlayerId, out playerState);
        PlayerState defender;
        gameState.TryGetPlayer(unit.owner, out defender);
        BattleResults battleResults = BattleHelpers.GetBattleResults(gameState, aggressor, unit);

        if (battleResults.attackDamage < unit.health && aggressor.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_scary")))
        {
            unit.attacked = true;
        }
    }
    #endregion

    #region MOVEMENTS

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    public static void HealAll(MoveAction __instance, GameState gameState)
    {
        UnitState unitState;
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState);
        gameState.TryGetUnit(__instance.UnitId, out unitState);
        TileData tile2 = gameState.Map.GetTile(__instance.Path[0]);

        if (tile2 == null || tile2.improvement == null)
        {
            return;
        }
        else if (PolibUtils.DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healall")))
        {
            PolibUtils.HealUnit(gameState, unitState, 40);
        }
        else if (PolibUtils.DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healfriendly")) && tile2.owner == unitState.owner)
        {
            PolibUtils.HealUnit(gameState, unitState, 40);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    public static void CleanseImp(MoveAction __instance, GameState gameState)
    {
        UnitState unitState;
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState);
        gameState.TryGetUnit(__instance.UnitId, out unitState);
        TileData tile2 = gameState.Map.GetTile(__instance.Path[0]);

        if (tile2 == null || tile2.improvement == null)
        {
            return;
        }
        else if (PolibUtils.DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_cleanse")))
        {
            PolibUtils.CleanseUnit(unitState);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    public static void Crush(MoveAction __instance, GameState gameState)
    {

        UnitState unitState;
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState);
        gameState.TryGetUnit(__instance.UnitId, out unitState);
        TileData tile2 = gameState.Map.GetTile(__instance.Path[0]);

        if (unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_crush")) && CanDestroyDiNuovo(tile2, gameState))
        {
            gameState.ActionStack.Add(new DestroyImprovementAction(tile2.owner, tile2.coordinates));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.Execute))]
    public static void RuinDash(MoveCommand __instance, GameState gameState)
    {
        gameState.TryGetUnit(__instance.UnitId, out UnitState unit);
        TileData tile = gameState.Map.GetTile(__instance.To);

        if (unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_rummager")) && tile.HasImprovement(ImprovementData.Type.Ruin))
        {
            var command = new ExamineRuinsCommand(unit.owner, tile.coordinates);
            var action = new ExamineRuinsAction(unit.owner, command.GetRuinsReward(gameState, unit.owner, tile), tile.coordinates);
            gameState.ActionStack.Insert(0, action);
        }

    }

    public static bool CanDestroyDiNuovo(TileData tile, GameState gameState) //is that fucking italian? //Yes.
    {
        if (tile == null || tile.improvement == null)
        {
            return false;
        }
        if (tile.improvement.type == ImprovementData.Type.City || tile.improvement.type == ImprovementData.Type.Ruin || tile.improvement.type == ImprovementData.Type.LightHouse)
        {
            return false;
        }
        if (PolibUtils.DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_indestructible")))
        {
            return false;
        }
        return true;
    }



    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
    public static void ExcludeMoveOptions(this GameState gameState, WorldCoordinates start, int maxCost, UnitState unit, ref Il2Gen.List<WorldCoordinates> __result)
    {
        List<System.Func<TileData, UnitState, bool>> filters = new List<System.Func<TileData, UnitState, bool>>{
            PolibUtils.IsTileBlockedForUnit,
            PolibUtils.IsTileOutOfBounds,
            PolibUtils.IsTileWaterForHydrophobics
        };
        __result = PolibUtils.FilterMoveOptions(__result, unit, filters);
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetPath), typeof(MapData), typeof(WorldCoordinates), typeof(WorldCoordinates), typeof(int), typeof(PathFinderSettings))]
    public static void CantEmbarkExtraFix(this MapData map, WorldCoordinates start, WorldCoordinates destination, int maxCost, PathFinderSettings settings, ref Il2Gen.List<WorldCoordinates> __result)
    {
        TileData tile1 = map.GetTile(start);
        if (tile1 == null || tile1.unit == null || !tile1.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_cantembark"))) return;

        if (map.GetTile(destination).IsWater) __result = null;
    }

    #endregion
}