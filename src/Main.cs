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

public static class Main
{


    private static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        new Harmony("com.polibraryteam.polibrary").PatchAll();
        modLogger = logger;
        logger.LogMessage("Polibrary.dll loaded.");
        PolyMod.Loader.AddPatchDataType("cityRewardData", typeof(CityReward)); //casual fapingvin carry
        PolyMod.Loader.AddPatchDataType("unitEffectData", typeof(UnitEffect)); //casual fapingvin carry... ...again
        PolyMod.Loader.AddPatchDataType("unitAbilityData", typeof(UnitAbility.Type)); //...casual...      ...fapingvin carry...       ...again
    }

    public static ImprovementData DataFromState(ImprovementState improvement, GameState state)
    {
        return state.GameLogicData.GetImprovementData(improvement.type);
    }

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
        else if (DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healall")))
        {
            PolibUtils.HealUnit(gameState, unitState, 40);
        }
        else if (DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healfriendly")) && tile2.owner == unitState.owner)
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
        else if (DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_cleanse")))
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
        if (DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_indestructible")))
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
                    ImprovementData data = Main.DataFromState(tile.improvement, gameState);

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
        modLogger.LogMessage("Bounded unit found");

        var homecity = unit.home;
        var citytile = gameState.Map.GetTile(homecity);
        var citytiles = ActionUtils.GetCityArea(gameState, citytile);
        modLogger.LogMessage("citytiles length " + citytiles.Count);
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
        modLogger.LogMessage("Homesick unit found");

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
    /*
    #region Intercept
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    public static bool Intercept(AttackCommand __instance, GameState gameState)
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

        if (battleResults.retaliationDamage > 0 && unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState))
        {

            if (battleResults.attackDamage > 0)
            {
                gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, __instance.Target, battleResults.attackDamage, battleResults.shouldMoveToDefeatedEnemyTile, AttackAction.AnimationType.Normal, 250));
            }
            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Target, __instance.Origin, battleResults.retaliationDamage, false, AttackAction.AnimationType.Normal, 100));

            if (battleResults.shouldMoveToDefeatedEnemyTile)
            {
                gameState.CommandStack.Add(new MoveCommand(__instance.PlayerId, aggressor, __instance.Target));
            }
            aggressor.moved = !aggressor.HasAbility(UnitAbility.Type.Escape, gameState);
            aggressor.attacked = true;

            return false;
        }

        return true;
    }

    [HarmonyPostfix] //retaliation calculation is BUUULLSHIT!
    [HarmonyPatch(typeof(BattleHelpers), nameof(BattleHelpers.GetBattleResults))] //yeah, it is, like many things in poly source code
    public static void InterceptRetaliationDamage(GameState gameState, UnitState attackingUnit, UnitState defendingUnit, ref BattleResults __result)
    {
        bool flag = (int)attackingUnit.health <= __result.retaliationDamage;

        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState) && flag)
        {
            __result.attackDamage = 0;
        }
        bool flag2 = (int)defendingUnit.health <= __result.attackDamage;
        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState) && flag2)
        {

            // FUUUUUUUUUUCK is this really the only way??
            // if damage calculator is ever changed then this is worthless!!!
            // But afaik only battlehelpers calculates damage, but it sets it to 0 if the unit would die
            long num = 45L; //o7 fap
            long num2 = (long)attackingUnit.GetMaxHealth(gameState);
            long num3 = (long)defendingUnit.GetMaxHealth(gameState);
            long num4 = (long)(attackingUnit.GetAttack(gameState) * (int)attackingUnit.health * 100) / num2;
            long num5 = (long)(defendingUnit.GetDefence(gameState) * (int)defendingUnit.health * 100) / num3;
            long num6 = num4 + num5;
            long num7 = (long)defendingUnit.GetDefenceBonus(gameState);

            __result.retaliationDamage = BattleHelpers.Round((int)(num5 * (long)defendingUnit.GetDefence(gameState) * num * 100L / (1000L * num6 * num7))); ;
        }

        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState) && flag)
        {
            __result.attackDamage = 0;
        }

        if (__result.attackDamage >= defendingUnit.health && attackingUnit.GetRange(gameState) == 1 && !attackingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_lazy")))
        {
            __result.shouldMoveToDefeatedEnemyTile = true;
        }
    }
    #endregion*/

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
            if (!player.HasAbility(PlayerAbility.Type.Destroy, gameState) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_demolishable")) && player.tribe != TribeData.Type.Cymanti)
            {
                __result.Add(new DestroyCommand(player.Id, tile.coordinates));
            }
            if (!tile.improvement.HasEffect(ImprovementEffect.decomposing) && !player.HasAbility(PlayerAbility.Type.Decompose, gameState) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_demolishable")) && player.tribe == TribeData.Type.Cymanti)
            {
                __result.Add(new DecomposeCommand(player.Id, tile.coordinates));
            }
        }
    }
    #endregion
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
                if (DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_preventagent")))
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
            if (unitData.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_agent")) && Main.CanBeAgented(tile.coordinates, gameState) && tile.CanBeAccessedByPlayer(gameState, player) && CommandValidation.HasUnitTerrain(gameState, tile.coordinates, unitData))
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


    


    


    //                    ██                   
    //                  ██████                 
    //                 ███  ███                
    //               ███      ███              
    //              ███   ██   ███             
    //            ███     ██     ███           
    //           ███      ██      ███          
    //          ██        ██        ██         
    //        ███         ██         ███       
    //       ██           ██           ██      
    //     ███            ██            ███    
    //    ██                              ██   
    //  ███               ██               ███ 
    // ██                                    ██
    // ████████████████████████████████████████
    //                                         
    // ░█░█░░░█░█░█▀█░█▀▄░█▀█░▀█▀░█▀█░█▀▀░░░█░█
    // ░▀░▀░░░█▄█░█▀█░█▀▄░█░█░░█░░█░█░█░█░░░▀░▀
    // ░▀░▀░░░▀░▀░▀░▀░▀░▀░▀░▀░▀▀▀░▀░▀░▀▀▀░░░▀░▀
    //
    // Beyond lies the code graveyard
    // Enter with caution!



    

    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStateUtils), nameof(GameStateUtils.SetPlayerNames))]
    public static void GameStateUtils_SetPlayerNames(GameState gameState)
    {
        foreach (PlayerState playerState in gameState.PlayerStates)
        {
            TribeData tribeData;
            gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData);
            if ((playerState.GetNameInternal == null || playerState.GetNameInternal() == "") && leaderNameDict.TryGetValue(tribeData.type, out var name))
            {
                playerState.UserName = name;
                LogMan1997?.LogInfo($"Named {tribeData.type} as {name}, their current name: {playerState.UserName}");
            }
        }
        LogMan1997?.LogInfo($"Tried naming tribes");
    }*/

    

    
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetSightRange))]
    public static void UnitDataExtensions_GetSightRange(this UnitData unitData, ref int __result)
    {
        int num = 1;
        foreach (UnitAbility.Type abilityType in unitData.unitAbilities)
        {
            if (unitAbilityDataDict.TryGetValue(abilityType, out var data))
            {
                num = (data.visionRadius <= num) ? data.visionRadius : num;
            }
        }
        __result = num;
    } 

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.CheckStepOnPoison))]
    public static void ActionUtils_CheckStepOnPoison(TileData targetTile, UnitState unit, GameState gameState)
    {
        ImprovementData improvementData;
        PlayerState player;
        bool b = false;
        foreach (UnitAbility.Type abilityType in gameState.GameLogicData.GetUnitData(unit.type).unitAbilities)
        {
            if (unitAbilityDataDict.TryGetValue(abilityType, out var data))
            {
                b = data.allowsFly ? true : b; //FUCKING FUCK FUCK THIS SHIT FUCKING FUCK SHIT FUCK
            }
        }
        if (targetTile.improvement != null && gameState.GameLogicData.TryGetData(targetTile.improvement.type, out improvementData) && improvementData != null && improvementData.HasAbility(ImprovementAbility.Type.Poison) && gameState.TryGetPlayer(unit.owner, out player) && !player.HasTribeAbility(TribeAbility.Type.PoisonResist, gameState) && !b)
            {
                gameState.ActionStack.Add(new PoisonUnitAction(unit.owner, targetTile.coordinates, targetTile.coordinates));
                if (gameState.Version < 83)
                {
                    gameState.ActionStack.Add(new AttackAction(unit.owner, targetTile.coordinates, targetTile.coordinates, 20, false, AttackAction.AnimationType.None, 0));
                }
            }
    } */
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.PerformAttackDefault))]
    private static void ActionUtils_PerformAttackDefault(GameState gameState, byte playerId, WorldCoordinates origin, WorldCoordinates target, int damage)
    {
        TileData tile = gameState.Map.GetTile(origin);
        TileData tile2 = gameState.Map.GetTile(target);
        UnitState? usAttacker = (tile != null) ? tile.unit : null;
        UnitState usDefender = tile2.unit;
        if (usAttacker != null)
        {
            usAttacker.SetUnitDirection(origin, target);
        }
        usDefender.SetUnitDirection(target, origin);
        UnitState usDefender2 = usDefender;
        usDefender2.health -= (ushort)Math.Min(damage, usDefender.health);
        if (usAttacker != null)
        {
            foreach (UnitEffect effect in usAttacker.effects)
            {
                if (unitEffectDataDict.TryGetValue(effect, out var effectData) && effectData != null && effectData.removal != null)
                {
                    foreach (string str in effectData.removal)
                    {
                        if (str == "attack")
                        {
                            usAttacker.RemoveEffect(effect);
                        }
                    }
                }
            }
        }
        foreach (UnitEffect effect in usDefender.effects)
        {
            if (unitEffectDataDict.TryGetValue(effect, out var effectData) && effectData != null && effectData.removal != null)
            {
                foreach (string str in effectData.removal)
                {
                    if (str == "hurt")
                    {
                        usDefender.RemoveEffect(effect);
                    }
                }
            }
        }
        byte playerId2 = (origin == target || usAttacker == null) ? playerId : usAttacker.owner;
        PlayerState playerState;
        gameState.TryGetPlayer(playerId2, out playerState);
        if (usDefender.health == 0)
        {
            if (usDefender.owner != 255)
            {
                playerState.kills += 1U;
                if (origin != WorldCoordinates.NULL_COORDINATES && usAttacker != null)
                {
                    UnitData unitData;
                    gameState.GameLogicData.TryGetData(usAttacker.type, out unitData);
                    if (!unitData.IsVehicle() && !unitData.hidden)
                    {
                        UnitState unitState3 = usAttacker;
                        unitState3.xp += 1;
                    }
                }
                ActionUtils.EnableTask(gameState, playerState, TaskData.Type.Killer);
                TaskBase taskBase;
                if (gameState.TryGetTask(playerState, TaskData.Type.Killer, out taskBase) && taskBase.Bump(gameState, 1))
                {
                    gameState.CheckTask(playerState, taskBase);
                }
            }
            PlayerState playerState2;
            gameState.TryGetPlayer(usDefender.owner, out playerState2);
            playerState2.casualities += 1U;
            gameState.ActionStack.Add(new KillUnitAction(playerId, target));
            return;
        }
        if (usAttacker != null && usAttacker.HasAbility(UnitAbility.Type.Poison, gameState))
        {
            gameState.ActionStack.Add(new PoisonUnitAction(playerId, origin, target));
        }
    } */

    /* i cant keep developing this forever, gotta get it out to get feedback on shit
    //melee splash
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    public static bool AttackCommand_ExecuteDefault(GameState gameState, AttackCommand __instance)
    {
        bool poisonSplashDealsDamage = false;
        bool freezeSplashDealsDamage = true;
        bool freezeSplashFreezesTile = true;
        bool drenchSplashFloodsTiles = true;
        bool drenchSplashDealsDamage = true;
        double splashDamageMultiplier = 0.5;

        UnitState unitState = gameState.Map.GetTile(__instance.Origin).unit;
        if (!gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState))
        {
            playerState = new PlayerState();
        }

        if (unitState.HasAbility(UnitAbility.Type.Splash, gameState))
        {
            foreach (TileData tileData in gameState.Map.GetTileNeighbors(__instance.Target))
            {
                if (tileData.unit != null && !tileData.unit.HasActivePeaceTreaty(gameState, playerState))
                {
                    if (unitState.HasAbility(UnitAbility.Type.Poison, gameState))
                    {
                        gameState.ActionStack.Add(new PoisonUnitAction(__instance.PlayerId, __instance.Origin, tileData.coordinates));
                        if (poisonSplashDealsDamage)
                        {
                            BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                        }
                    }
                    else if (unitState.HasAbility(UnitAbility.Type.Freeze, gameState))
                    {
                        gameState.ActionStack.Add(new FreezeUnitAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, 0));
                        if (freezeSplashFreezesTile)
                        {
                            gameState.ActionStack.Add(new FreezeTileAction(__instance.PlayerId, tileData.coordinates));
                        }
                        if (freezeSplashDealsDamage)
                        {
                            BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                        }
                    }
                    else if (unitState.HasAbility(UnitAbility.Type.Drench, gameState))
                    {
                        if (drenchSplashFloodsTiles)
                        {
                            gameState.ActionStack.Add(new FloodTileAction(__instance.PlayerId, tileData.coordinates));
                        }
                        if (drenchSplashDealsDamage)
                        {
                            BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                        }
                    }
                    else
                    {
                        BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                        gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                    }
                }
            }
        }
        return false;
    }*/

    













    

    

    
    








//I snuck this in, and he didn't even realize! I'm so sneaky! He'll never figure it out!
    /*



                                                                                ███████        ████████                                                                                       
                                                                           ████                   ██████                                                                                      
                                                                       ████                   █████                                                                                           
                                                                     ███                   ███                                                                                                
                                                                   ███                  ████                                                                                                  
                                                                 ███                  ███                                                                                                     
                                                               ███                 ████                                                                                                       
                                                              ███                 ██                                                                                                          
                                                             ███                 ██                                                                                                           
                                                            ██                  ██                                                                                                            
                                                           ██                   ██               █████                                                                                        
                                                          ██                      ███   ████████████████████                                                                                  
                                                         ███                        █████████████████████████████                                                                             
                                                         ██                     ███████████      █████████████████████                              ██                                        
                                                         ██                █████████████                ████████████████████                       ███                                        
                                                         ██            █████████████                        ████████████████████████  ██████     ███ ██                                       
                                                          ██        ██████████████                              ████████████████████████████   ███   ██                                       
                                                        ████    █████████████████                    ██            ██████████████████████   ████     ██                                       
                                                       ███████████████████████                         ██            ███████████████████████         ██                                       
                                                       ██████████████████████        ███████             ██           █████████████████              ██                                       
                                                       █████████████████████            ██████            ██            ████████████████             ██                                       
                                                       ████████████████████                ████            ██            ███████████████            ██                                        
                                                       ██████████████████                    ███            ████          ██████████████           ██                                         
                                                     ███████████████████              ███████████                          █████████████          ██                                          
                                                  █████████████████████            ██ █  ██████████                        ██████████████       ██                                            
                                                 █████████████████████                  █████    ███                        █████████████      ██                                             
                                               ██████████████████████                          █   ██       █                ███████████     ███                                              
                                               █████████████████████                                     ███        ███████  ███████████   ███                                                
                                              █████████████████████         ██                              ███████████      ██████████████                                                   
                                             █████████████████████          ███                                  ████ ██     ████████████                                                     
                                             ████████████████████          █████                                         █  ████████████                                                      
                                            █████████████████████          ███████                                          █████████                                                         
                                            ████████████████████          ███████████          ███    ██                   █████████                                                          
                                            ███████████████████        ███████████████████               ██                ████████                                                           
                                    ███████████████████████████       █████████████████████                  ██           ████████                                                            
                      █████████████████     ██████████████████        ████████████████████████                            ████████                                                            
              ████████████                 ██████████████████        █████████    ████████████                            ████████                                                            
        █████████                         ███████████████████       █████████    ██       ████   ██                       ████████                                                            
                                        ██  ████████████████        █████████     ██         ███  ██              ███     █████████                                                           
                                      ███   ███   ██████████       ██████████      █           ███                 ████   █████████                                                           
                                     ██             ████████      █████████         █           █████      █        ████  █████████                                                           
                                    ██               ███████      ████████           ███          ██████          █████████████████   ██                                                      
                                  ███                ████████    ███████                ███           ███████████       ████████████████                                                      
                                 ██                  █████████   ██████     █              ██                              ███████████                                                        
                                ██                  ███████████████████ █████          █      ████                          ██████████                                                        
                               ██                ██████████████████████████             ███       █████                   █  ███████████                                                      
                             ███             █████████████████████████████                 ███           █████        █████   ███████████                                                     
                             ██               ███████████████████████████                      █                               ██████████                                                     
                           ███                 ██████████████████████████                                                       █████████                                                     
                          ██                     ████████████████████████                                                       ███   ██                                                      
                          ██                       ████████████████████████                                         ██         ███     ██                                                     
                         ██                          ██████████████████████                                      ███           ██      ██                                                     
                        ██                             █████████████████████                                  ███              ██       ██                                                    
                        ██                               ███████████████████                             █████                ██        ██                                                    
                       ██                                  ██████████████████                                                ██          █                                                    
                       ██                                   █████████████████                                               ██           ██                                                   
                      ██                                  ██ ██████████████████                                            ██             ██                                                  
                     ██                                  ███  ████████████████████            ██                          █               ███                                                 
                     ██                                 ██     █████████████████████         ██                          ██               █████                                               
                     ██                                ██       ███████████████████████    █████     ██                 ██                ██  ███                                             
                     ██                               ██         ████████████████████████████████   ████     ███       ██                 █     ███                                           
                     █                              ██           ████████████████████████████████████████████████    ███                 ██       ██                                          
                    ██                            ███             █████████████████████████████████████████████████████                  █         ███                                        
                    ██                           ███               ███████████████████████████████████████████████████                  ██          ███                                       
                     █                          ██                  ████████████████████████████████████████████████                   ██             ██                                      
                     ██                        ██                   ███████████████████████████████████████████████                  ██                ███                                    
                     ██                      ██                      ████████████████████████████████████████████████             ███                   ███                                   
                      ██                   ███                        █████████████████████████████████████████      ███████  █████                      ███                                  
                       ██                 ██                           ███████████████████████████████████████                                            ███                                 
        */
}


