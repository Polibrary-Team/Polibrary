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


namespace Polibrary;

public static class UnitManager
{
    private static ManualLogSource steve;
    public static void Load(ManualLogSource logger)
    {
        steve = logger;
        steve.LogInfo("I");
        steve.LogInfo("am Steve");
        // I'm keeping this logger alive cuz its the funniest -fapingvin
        Harmony.CreateAndPatchAll(typeof(UnitManager));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetDefenceBonus))]
    public static bool UnitDataExtensions_GetDefenceBonus(this UnitState unit, GameState gameState, ref int __result)
    {
        TileData tile = gameState.Map.GetTile(unit.coordinates);
        byte playerId = gameState.CurrentPlayer;
        if (tile != null)
        {
            int finaldef = 10;
            int effectMultiplicative = 0;
            foreach (UnitEffect effect in unit.effects)
            {
                if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
                {
                    effectMultiplicative = (effectData.defenceMult != 0) ? effectMultiplicative + (effectData.defenceMult / 10) : effectMultiplicative;
                }
            }
            if (tile.owner == unit.owner && tile.improvement != null && Parse.defenceBoostDict.TryGetValue(tile.improvement.type, out int def))
            {
                finaldef = def; //python users got triggered here
            }
            else if (tile.owner == unit.owner && tile.improvement != null && tile.improvement.type == ImprovementData.Type.City && tile.improvement.rewards != null)
            {
                int def2 = 0;
                //steve!.LogInfo(tile.improvement.rewards.Count);
                foreach (CityReward reward in tile.improvement.rewards)
                {
                    if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
                    {
                        def2 = def2 + cityRewardData.defenceBoostReward;
                    }
                }
                if (def2 != 0)
                {
                    finaldef = def2 - 1;
                }
            }
            if (finaldef == 10)
            {
                return true;
            }
            else
            {
                effectMultiplicative = (effectMultiplicative == 0) ? 1 : effectMultiplicative;
                __result = finaldef + effectMultiplicative;
                return false;
            }
        }
        else { return true; }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetDefence))]
    public static void UnitDataExtensions_GetDefence(this UnitState unit, GameState state, ref int __result) //banan sugeston
    {
        UnitData unitData;
        state.GameLogicData.TryGetData(unit.type, out unitData);
        int boostDefenceOverSpawn = 0;
        foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitData.type))
        {
            boostDefenceOverSpawn += PolibUtils.GetRewardData(reward).boostDefenceOverSpawn;
        }
        __result = (unitData.defence + boostDefenceOverSpawn * PolibUtils.GetRewardCountForPlayer(unit.owner, PolibUtils.GetSpawningRewardsForUnit(unitData.type))) * unit.GetDefenceBonus(state);
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMovement), new System.Type[] { typeof(UnitState), typeof(GameState) })]
    public static void UnitDataExtensions_GetMovement(this UnitState unitState, GameState gameState, ref int __result)
    {
        UnitData unitData;
        gameState.GameLogicData.TryGetData(unitState.type, out unitData);
        int effectAdditive = 0;
        int effectMultiplicative = 1;
        foreach (UnitEffect effect in unitState.effects)
        {
            if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                effectAdditive = effectAdditive + effectData.movementAdd;
                effectMultiplicative = (effectData.movementMult != 0) ? effectMultiplicative * (effectData.movementMult / 10) : effectMultiplicative;
            }
        }
        int boostMovementOverSpawn = 0;
        foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitData.type))
        {
            boostMovementOverSpawn += PolibUtils.GetRewardData(reward).boostMovementOverSpawn;
        }
        __result = ((unitData.GetMovement() + boostMovementOverSpawn * PolibUtils.GetRewardCountForPlayer(unitState.owner, PolibUtils.GetSpawningRewardsForUnit(unitData.type))) * effectMultiplicative) + effectAdditive;
    }
    

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttack), new System.Type[] { typeof(UnitState), typeof(GameState) })]
    public static void UnitDataExtensions_GetAttack(this UnitState unitState, GameState gameState, ref int __result)
    {
        UnitData unitData;
        gameState.GameLogicData.TryGetData(unitState.type, out unitData);
        int effectAdditive = 0;
        int effectMultiplicative = 1;
        foreach (UnitEffect effect in unitState.effects)
        {
            if (Parse.unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                effectAdditive = effectAdditive + effectData.attackAdd;
                effectMultiplicative = (effectData.attackMult != 0) ? effectMultiplicative * (effectData.attackMult / 10) : effectMultiplicative;
            }
        }
        int boostAttackOverSpawn = 0;
        foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitData.type))
        {
            boostAttackOverSpawn += PolibUtils.GetRewardData(reward).boostAttackOverSpawn;
        }
        __result = ((unitData.GetAttack() + boostAttackOverSpawn * PolibUtils.GetRewardCountForPlayer(unitState.owner, PolibUtils.GetSpawningRewardsForUnit(unitData.type)) * 10) * effectMultiplicative) + effectAdditive * 10;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMaxHealth))]
    public static void GetMaxHealth(this UnitState unitState, GameState gameState, ref int __result) //man voidmongers using modularity will get me SOO much downloads!!
    {
        if (unitState.passengerUnit != null && !unitState.HasAbility(UnitAbility.Type.Protect, gameState))
        {
            unitState = unitState.passengerUnit;
        }
        UnitData unitData;
        gameState.GameLogicData.TryGetData(unitState.type, out unitData);
        int boostMaxHpOverSpawn = 0;
        foreach (CityReward reward in PolibUtils.GetSpawningRewardsForUnit(unitData.type))
        {
            boostMaxHpOverSpawn += PolibUtils.GetRewardData(reward).boostMaxHpOverSpawn;
        }
        __result = unitData.health + (unitState.promotionLevel * 50) + (boostMaxHpOverSpawn * PolibUtils.GetRewardCountForPlayer(unitState.owner, PolibUtils.GetSpawningRewardsForUnit(unitData.type)));
    }

    #region Agent
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetTrainableUnits))]
    public static void ExcludeAgentsFromCities(ref Il2CppSystem.Collections.Generic.List<TrainCommand> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
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
        __result = new Il2CppSystem.Collections.Generic.List<TrainCommand>();
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
    public static void IncludeAgents(ref Il2CppSystem.Collections.Generic.List<TrainCommand> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
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
        __result = new Il2CppSystem.Collections.Generic.List<TrainCommand>();
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

    #region Lazy
    [HarmonyPostfix] //literally me
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(BattleHelpers), nameof(BattleHelpers.GetBattleResults))]
    public static void Lazy(GameState gameState, UnitState attackingUnit, UnitState defendingUnit, ref BattleResults __result)
    {
        if (attackingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_lazy")))
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

    #endregion

    #region InciteFear
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    public static void InciteFear(AttackCommand __instance, GameState gameState)
    {
        UnitState aggressor;
        gameState.TryGetUnit(__instance.UnitId, out aggressor);
        TileData tile = gameState.Map.GetTile(__instance.Target);
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

    #region Movements

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
            PolibUtils.CleanseUnit(gameState, unitState);
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
            gameState.CommandStack.Add(new ExamineRuinsCommand(unit.owner, tile.coordinates));
        }

    }


    /* We can't use TileData.CanDestroy since it won't destroy enemy improvements so messy
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TileData), nameof(TileData.CanDestroy))]
    public static void Indestructible(ref bool __result, TileData __instance, GameState gameState, PlayerState player)
    {
        if (__instance.improvement != null)
        {
            var data = gameState.GameLogicData.GetImprovementData(__instance.improvement.type);
            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_indestructible")))
            {
                __result = false;
            }
        }
    }*/

    public static bool CanDestroyDiNuovo(TileData tile, GameState gameState) //is that fucking italian?
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
    public static void PolyBlock(this GameState gameState, WorldCoordinates start, int maxCost, UnitState unit, ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result)
    {
        Il2CppSystem.Collections.Generic.List<WorldCoordinates> newlist = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();

        for (int i = 0; i < __result.Count; i++)
        {
            bool flag = false;
            if (__result[i] != WorldCoordinates.NULL_COORDINATES)
            {
                TileData tile = gameState.Map.GetTile(__result[i]);
                if (tile != null && tile.improvement != null)
                {
                    ImprovementData data = PolibUtils.DataFromState(tile.improvement, gameState);

                    bool flag2 = true;
                    if (Parse.UnblockDict.TryGetValue(data.type, out string value))
                    {
                        if (unit.HasAbility(EnumCache<UnitAbility.Type>.GetType(value)))
                        {
                            flag2 = false;
                        }
                    }

                    if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_block")) && flag2)
                    {
                        flag = true;
                    }
                }
            }
            if (!flag)
            {
                newlist.Add(__result[i]);
            }
        }

        __result = newlist;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
    public static void Bounded(this GameState gameState, WorldCoordinates start, int maxCost, UnitState unit, ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result)
    {

        if (!unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_bounded")))
        {
            return;
        }
        steve.LogMessage("Bounded unit found");

        var homecity = unit.home;
        var citytile = gameState.Map.GetTile(homecity);
        var citytiles = ActionUtils.GetCityArea(gameState, citytile);
        steve.LogMessage("citytiles length " + citytiles.Count);
        Il2CppSystem.Collections.Generic.List<WorldCoordinates> newlist = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();

        for (int i = 0; i < __result.Count; i++)
        {
            bool flag = false;
            if (__result[i] != WorldCoordinates.NULL_COORDINATES)
            {
                TileData tile = gameState.Map.GetTile(__result[i]);
                if (tile != null)
                {
                    bool currentTileOutofBounds = false;
                    foreach (var item in citytiles)
                    {
                        if (item == tile)
                        {
                            currentTileOutofBounds = true;
                            break;
                        }
                    }
                    if (!currentTileOutofBounds)
                    {
                        flag = true;
                    }
                }
            }
            if (!flag)
            {
                newlist.Add(__result[i]);
            }
        }

        __result = newlist;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
    public static void TrueBounded(this GameState gameState, WorldCoordinates start, int maxCost, UnitState unit, ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result)
    {

        if (!unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_homesick")))
        {
            return;
        }
        steve.LogMessage("Homesick unit found");

        var owner = unit.owner;
        Il2CppSystem.Collections.Generic.List<WorldCoordinates> newlist = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();

        for (int i = 0; i < __result.Count; i++)
        {
            bool flag = false;
            if (__result[i] != WorldCoordinates.NULL_COORDINATES)
            {
                TileData tile = gameState.Map.GetTile(__result[i]);
                if (tile != null)
                {
                    if (tile.owner != owner)
                    {
                        flag = true;
                    }
                }
            }
            if (!flag)
            {
                newlist.Add(__result[i]);
            }
        }

        __result = newlist;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.GetMoveOptions))]
    public static void CantEmbark(this GameState gameState, WorldCoordinates start, int maxCost, UnitState unit, ref Il2CppSystem.Collections.Generic.List<WorldCoordinates> __result)
    {

        if (!unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_cantembark")))
        {
            return;
        }

        Il2CppSystem.Collections.Generic.List<WorldCoordinates> newlist = new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();

        for (int i = 0; i < __result.Count; i++)
        {
            bool flag = false;
            if (__result[i] != WorldCoordinates.NULL_COORDINATES)
            {
                TileData tile = gameState.Map.GetTile(__result[i]);
                if (tile != null)
                {
                    if (tile.terrain == Polytopia.Data.TerrainData.Type.Water || tile.terrain == Polytopia.Data.TerrainData.Type.Ocean)
                    {
                        flag = true;
                    }
                }
            }
            if (!flag)
            {
                newlist.Add(__result[i]);
            }
        }

        __result = newlist;
    }
    #endregion
}