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
        Harmony.CreateAndPatchAll(typeof(Main));
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
            Main.HealUnit(gameState, unitState, 40);
        }
        else if (DataFromState(tile2.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healfriendly")) && tile2.owner == unitState.owner)
        {
            Main.HealUnit(gameState, unitState, 40);
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
            Main.CleanseUnit(gameState, unitState);
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

    public static bool CanDestroyDiNuovo(TileData tile, GameState gameState)
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
                    if (UnblockDict.TryGetValue(data.type, out string value))
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
    [HarmonyPatch(typeof(BattleHelpers), nameof(BattleHelpers.GetBattleResults))]
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
            long num = 45L;
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
    [HarmonyPostfix]
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
    #region GLD Parsing
    public static Dictionary<ImprovementData.Type, string> BuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> NoBuildersDict = new Dictionary<ImprovementData.Type, string>();

    public static Dictionary<ImprovementData.Type, string> ImpBuildersDict = new Dictionary<ImprovementData.Type, string>();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();

            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["BuiltBySpecific"] != null ? "BuiltBySpecific" : "builtBySpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        BuildersDict[impType] = ability;
                        token.Remove(key);
                        modLogger.LogInfo($"Added {ability} ability to {impType} in BuildersDict");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse5(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["BuiltOnSpecific"] != null ? "BuiltOnSpecific" : "builtOnSpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        ImpBuildersDict[impType] = ability;
                        token.Remove(key);
                        modLogger.LogInfo($"Added {ability} ability to {impType} in ImpBuildersDict");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse4(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["NotBuiltBySpecific"] != null ? "NotBuiltBySpecific" : "notBuiltBySpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        NoBuildersDict[impType] = ability;
                        token.Remove(key);
                        modLogger.LogInfo($"Added {ability} ability to {impType} in NoBuildersDict");
                    }
                }
            }
        }
    }

    public static Dictionary<ImprovementData.Type, string> UnblockDict = new Dictionary<ImprovementData.Type, string>();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse3(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["Unblock"] != null ? "Unblock" : "unblock";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        UnblockDict[impType] = ability;
                        token.Remove(key);
                        modLogger.LogInfo($"Added {ability} ability to {impType} in UnblockDict");
                    }
                }
            }
        }
    }

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
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltBySpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (BuildersDict.TryGetValue(improvement.type, out string ability))
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
        if (NoBuildersDict.TryGetValue(improvement.type, out string ability2))
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
    public static void mBuiltOnSpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (ImpBuildersDict.TryGetValue(improvement.type, out string ability))
        {
            if (tile.improvement == null)
            {
                __result = false;
                return;
            }
            if (!Main.DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType(ability)))
            {
                __result = false;
                return;
            }
        }
    }

    #endregion

    public static void CleanseUnit(GameState gameState, UnitState unit)
    {
        unit.effects = new Il2CppSystem.Collections.Generic.List<UnitEffect>();
    }

    public static void HealUnit(GameState gameState, UnitState unit, int amount)
    {
        var maxhp = unit.GetMaxHealth(gameState);
        var currhp = unit.health;
        if (currhp >= maxhp)
        {
            return;
        }
        var diff = maxhp - currhp;
        if (diff < amount)
        {
            amount = diff;
        }
        if (unit.HasEffect(UnitEffect.Poisoned))
        {
            amount = 0;
            unit.RemoveEffect(UnitEffect.Poisoned);
        }
        unit.health += (ushort)amount;
        Tile tile = MapRenderer.Current.GetTileInstance(unit.coordinates);
        tile.Heal(amount);
    }


    public static List<TechData> polibGetUnlockableTech(PlayerState player)
    {
        var gld = GameManager.GameState.GameLogicData;
        if (player.tribe == TribeData.Type.None)
        {
            return null;
        }
        TribeData tribe;
        if (GameManager.GameState.GameLogicData.TryGetData(player.tribe, out tribe))
        {
            List<TechData> list = new List<TechData>();
            for (int i = 0; i < player.availableTech.Count; i++)
            {
                TechData @override;
                if (gld.TryGetData(player.availableTech[i], out @override))
                {
                    @override = gld.GetOverride(@override, tribe);
                    foreach (TechData techData in @override.techUnlocks)
                    {
                        TechData override2 = gld.GetOverride(techData, tribe);
                        if (!player.HasTech(override2.type) && !list.Contains(override2))
                        {
                            list.Add(override2);
                        }
                    }
                }
            }
            return list;
        }
        return null;
    }

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
            Main.HealUnit(gameState, tile.unit, 40);
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_cleanseonce")) && tile.unit != null)
        {
            Main.CleanseUnit(gameState, tile.unit);
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

            var unlockableTech = Main.polibGetUnlockableTech(player);
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
        if (player.Id != gameState.CurrentPlayer)
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

    /// MODULARITY
    /// 
    /// HERE WE GO
    /// 

    public static Dictionary<TribeData.Type, int> startingStarsDict = new Dictionary<TribeData.Type, int>(); //its like excel, but polyscript and hard
    public static Dictionary<TribeData.Type, string> leaderNameDict = new Dictionary<TribeData.Type, string>();
    public static Dictionary<ImprovementData.Type, int> defenceBoostDict = new Dictionary<ImprovementData.Type, int>();
    public static Dictionary<TribeData.Type, List<(ResourceData.Type, int)>> startingResources = new Dictionary<TribeData.Type, List<(ResourceData.Type, int)>>();
    public class PolibCityRewardData //oh boy its time to bake some lights, except its not lights and we're not baking anything and flowey undertale
    {
        public int productionModifier { get; set; }
        public int currencyReward { get; set; }
        public int populationReward { get; set; }
        public int scoreReward { get; set; }
        public int defenceBoostReward { get; set; }
        public int scoutSpawnAmount { get; set; }
        public int scoutMoveAmount { get; set; } = 15;
        public int borderGrowthAmount { get; set; } //yay now its useful
        public UnitData.Type unitType { get; set; }
        public int level { get; set; } = -1;
        public string persistence { get; set; } = "none";
        public int order { get; set; } = 0;
        public bool hidden { get; set; } = false;
        public int boostAttackOverSpawn { get; set; }
        public int boostDefenceOverSpawn { get; set; }
        public int boostMaxHpOverSpawn { get; set; }
        public int boostMovementOverSpawn { get; set; }
        public bool healUnitOverSpawn { get; set; } = false;
    }
    public class CityRewardOverrideClass
    {
        public CityReward og { get; set; }
        public CityReward neu { get; set; }
    }
    public class PolibUnitEffectData //So I haveth a Laser Pointre...
    {
        public int defenceMult { get; set; }
        public int attackMult { get; set; }
        public int attackAdd { get; set; }
        public int movementMult { get; set; }
        public int movementAdd { get; set; }
        public string color { get; set; }
        public List<string> removal { get; set; }
        public bool freezing { get; set; }
    }
    public static Dictionary<CityReward, PolibCityRewardData> cityRewardDict = new Dictionary<CityReward, PolibCityRewardData>();
    public static Dictionary<TribeData.Type, List<CityRewardOverrideClass>> cityRewardOverrideDict = new Dictionary<TribeData.Type, List<CityRewardOverrideClass>>();
    public static List<CityReward> rewardList = MakeSystemList<CityReward>(CityRewardData.cityRewards);
    public static Dictionary<UnitEffect, PolibUnitEffectData> unitEffectDataDict = new Dictionary<UnitEffect, PolibUnitEffectData>();
    public class PolibUnitAbilityData
    {
        public int visionRadius { get; set; }
        public bool allowsFly { get; set; }
        public UnitEffect effect { get; set; }
        public string effectApplication { get; set; }
        public string effectApplicationActionTarget { get; set; }
    }
    public static Dictionary<UnitAbility.Type, PolibUnitAbilityData> unitAbilityDataDict = new Dictionary<UnitAbility.Type, PolibUnitAbilityData>();
    public static UnitEffect[] vanillaUnitEffects = new UnitEffect[] { UnitEffect.Boosted, UnitEffect.Bubble, UnitEffect.Frozen, UnitEffect.Invisible, UnitEffect.Petrified, UnitEffect.Poisoned };


    //thanks exploit
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse6(GameLogicData __instance, JObject rootObject) //in this world, its analfuck, or be analfucked
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.tribeData.*").ToList()) // "// tribeData!" -exploit, 2025
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<TribeData.Type>.TryGetType(token.Path.Split('.').Last(), out var tribeType))
                {
                    if (token["startingStars"] != null)
                    {
                        int amount = token["startingStars"]!.ToObject<int>();
                        startingStarsDict[tribeType] = amount;
                        token.Remove("startingStars");
                    }

                    if (token["leaderName"] != null)
                    {
                        string leaderName = token["leaderName"]!.ToObject<string>();
                        leaderNameDict[tribeType] = leaderName;
                        token.Remove("leaderName");
                    }

                    List<(ResourceData.Type, int)> startingResourcesList = new List<(ResourceData.Type, int)>();
                    if (token["startingResources"] != null)
                    {
                        JArray resarray = token["startingResources"].TryCast<JArray>();
                        if (resarray != null)
                        {
                            for (int i = 0; i < resarray.Count; i++)
                            {
                                JToken restoken = resarray[i];
                                ResourceData.Type restype = ResourceData.Type.Fruit;
                                int amount = -1;

                                JObject resobject = restoken.TryCast<JObject>(); //hey, I wrote this shit without using gpt! I'm proud of myself, and if you think otherwise, touch grass nerd!
                                if (resobject != null)
                                {
                                    if (resobject["resource"] != null)
                                    {
                                        EnumCache<ResourceData.Type>.TryGetType(resobject["resource"]!.ToObject<string>(), out restype);
                                    }
                                    if (resobject["amount"] != null)
                                    {
                                        amount = resobject["amount"]!.ToObject<int>();
                                    }
                                }
                                startingResourcesList.Add((restype, amount));
                            }
                        }
                        token.Remove("startingResources");
                        startingResources[tribeType] = startingResourcesList;
                    }
                    else
                    {
                        startingResources[tribeType] = new List<(ResourceData.Type, int)> { (ResourceData.Type.Fruit, -1) };
                    }

                    List<CityRewardOverrideClass> overlist = new List<CityRewardOverrideClass>();
                    foreach (JToken overtoken in token.SelectTokens("$.cityRewardOverrides.*").ToList())
                    {
                        if (EnumCache<CityReward>.TryGetType(overtoken.Path.Split('.').Last(), out var reward))
                        {
                            if (EnumCache<CityReward>.TryGetType(overtoken!.ToObject<string>(), out var overreward))
                            {
                                CityRewardOverrideClass overrideClass = new CityRewardOverrideClass
                                {
                                    og = reward,
                                    neu = overreward
                                };
                                overlist.Add(overrideClass);
                            }
                        }
                    }
                    cityRewardOverrideDict[tribeType] = overlist;
                }
            }
        }
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var improvementType))
                {
                    if (token["defenceBoost"] != null)
                    {
                        int amount = token["defenceBoost"]!.ToObject<int>();
                        defenceBoostDict[improvementType] = amount;
                        token.Remove("defenceBoost");
                    }
                }
            }
        }

        foreach (CityReward reward in CityRewardData.cityRewards) //default for vanilla cityRewards
        {
            cityRewardDict[reward] = SetVanillaCityRewardDefaults(reward);
        }

        foreach (JToken jtoken in rootObject.SelectTokens("$.cityRewardData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<CityReward>.TryGetType(token.Path.Split('.').Last(), out var cityReward))
                {
                    PolibCityRewardData cityRewardData = new PolibCityRewardData();

                    if (token["addProduction"] != null)
                    {
                        int addProduction = token["addProduction"]!.ToObject<int>();
                        token.Remove("addProduction");
                        cityRewardData.productionModifier = addProduction;

                    }
                    if (token["currencyReward"] != null)
                    {
                        int currencyReward = token["currencyReward"]!.ToObject<int>();
                        token.Remove("currencyReward");
                        cityRewardData.currencyReward = currencyReward;

                    }
                    if (token["populationReward"] != null)
                    {
                        int populationReward = token["populationReward"]!.ToObject<int>();
                        token.Remove("populationReward");
                        cityRewardData.populationReward = populationReward;

                    }
                    if (token["scoreReward"] != null)
                    {
                        int scoreReward = token["scoreReward"]!.ToObject<int>();
                        token.Remove("scoreReward");
                        cityRewardData.scoreReward = scoreReward;

                    }
                    if (token["defenceBoost"] != null)
                    {
                        int defenceBoost = token["defenceBoost"]!.ToObject<int>();
                        token.Remove("defenceBoost");
                        cityRewardData.defenceBoostReward = defenceBoost;

                    }
                    if (token["scoutSpawnAmount"] != null)
                    {
                        int scoutSpawnAmount = token["scoutSpawnAmount"]!.ToObject<int>();
                        token.Remove("scoutSpawnAmount");
                        cityRewardData.scoutSpawnAmount = scoutSpawnAmount;

                    }
                    if (token["scoutMoveAmount"] != null)
                    {
                        int scoutMoveAmount = token["scoutMoveAmount"]!.ToObject<int>();
                        token.Remove("scoutMoveAmount");
                        cityRewardData.scoutMoveAmount = scoutMoveAmount;

                    }
                    if (token["borderGrowthAmount"] != null)
                    {
                        int borderGrowthAmount = token["borderGrowthAmount"]!.ToObject<int>();
                        token.Remove("borderGrowthAmount");
                        cityRewardData.borderGrowthAmount = borderGrowthAmount;

                    }
                    if (token["spawnUnit"] != null)
                    {
                        if (EnumCache<UnitData.Type>.TryGetType(token["spawnUnit"]!.ToObject<string>(), out var type))
                        {
                            cityRewardData.unitType = type;

                        }
                        token.Remove("spawnUnit");
                    }
                    if (token["level"] != null)
                    {
                        int level = token["level"]!.ToObject<int>();
                        token.Remove("level");
                        cityRewardData.level = level;

                    }
                    if (token["persistence"] != null)
                    {
                        string persistence = token["persistence"]!.ToObject<string>();
                        token.Remove("persistence");
                        cityRewardData.persistence = persistence;

                    }
                    if (token["order"] != null)
                    {
                        int order = token["order"]!.ToObject<int>();
                        token.Remove("order");
                        cityRewardData.order = order;

                    }
                    if (token["hidden"] != null)
                    {
                        bool hidden = token["hidden"]!.ToObject<bool>();
                        token.Remove("hidden");
                        cityRewardData.hidden = hidden;

                    }
                    if (token["boostAttackOverSpawn"] != null)
                    {
                        int boostAttackOverSpawn = token["boostAttackOverSpawn"]!.ToObject<int>();
                        token.Remove("boostAttackOverSpawn");
                        cityRewardData.boostAttackOverSpawn = boostAttackOverSpawn;

                    }
                    if (token["boostDefenceOverSpawn"] != null)
                    {
                        int boostDefenceOverSpawn = token["boostDefenceOverSpawn"]!.ToObject<int>();
                        token.Remove("boostDefenceOverSpawn");
                        cityRewardData.boostDefenceOverSpawn = boostDefenceOverSpawn;

                    }
                    if (token["boostMaxHpOverSpawn"] != null)
                    {
                        int boostMaxHpOverSpawn = token["boostMaxHpOverSpawn"]!.ToObject<int>();
                        token.Remove("boostMaxHpOverSpawn");
                        cityRewardData.boostMaxHpOverSpawn = boostMaxHpOverSpawn;

                    }
                    if (token["boostMovementOverSpawn"] != null)
                    {
                        int boostMovementOverSpawn = token["boostMovementOverSpawn"]!.ToObject<int>();
                        token.Remove("boostMovementOverSpawn");
                        cityRewardData.boostMovementOverSpawn = boostMovementOverSpawn;

                    }
                    if (token["healUnitOverSpawn"] != null)
                    {
                        bool healUnitOverSpawn = token["healUnitOverSpawn"]!.ToObject<bool>();
                        token.Remove("healUnitOverSpawn");
                        cityRewardData.healUnitOverSpawn = healUnitOverSpawn;

                    }
                    if (!rewardList.Contains(cityReward))
                    {
                        rewardList.Add(cityReward);
                    }
                    else
                    {
                        modLogger!.LogInfo($"{cityReward} is already in the list, so obviously we don't add it in again");
                    }
                    modLogger!.LogInfo($"added {cityReward} to list, list length: {rewardList.Count} (shouldn't be 8)");
                    cityRewardDict[cityReward] = cityRewardData;
                }
            }
        }

        foreach (UnitEffect effect in vanillaUnitEffects)
        {
            unitEffectDataDict[effect] = SetVanillaUnitEffectDefaults(effect);
        }

        foreach (JToken jtoken in rootObject.SelectTokens("$.unitEffectData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<UnitEffect>.TryGetType(token.Path.Split('.').Last(), out var unitEffect))
                {
                    PolibUnitEffectData unitEffectData = new PolibUnitEffectData();

                    if (token["defenceMult"] != null)
                    {
                        int defenceMult = token["defenceMult"]!.ToObject<int>();
                        token.Remove("defenceMult");
                        unitEffectData.defenceMult = defenceMult;

                    }
                    if (token["attackAdd"] != null)
                    {
                        int attackAdd = token["attackAdd"]!.ToObject<int>();
                        token.Remove("attackAdd");
                        unitEffectData.attackAdd = attackAdd;

                    }
                    if (token["attackMult"] != null)
                    {
                        int attackMult = token["attackMult"]!.ToObject<int>();
                        token.Remove("attackMult");
                        unitEffectData.attackMult = attackMult;

                    }
                    if (token["movementAdd"] != null)
                    {
                        int movementAdd = token["movementAdd"]!.ToObject<int>();
                        token.Remove("movementAdd");
                        unitEffectData.movementAdd = movementAdd;

                    }
                    if (token["movementMult"] != null)
                    {
                        int movementMult = token["movementMult"]!.ToObject<int>();
                        token.Remove("movementMult");
                        unitEffectData.movementMult = movementMult;

                    }
                    if (token["color"] != null)
                    {
                        string color = token["color"]!.ToObject<string>();
                        token.Remove("color");
                        unitEffectData.color = color;
                    }
                    if (token["freezing"] != null)
                    {
                        bool freezing = token["freezing"]!.ToObject<bool>();
                        token.Remove("freezing");
                        unitEffectData.freezing = freezing;
                    }

                    unitEffectDataDict[unitEffect] = unitEffectData;
                }
            }
        }
    }


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
    // The following section may contain:
    //      -Warcrimes
    //      -Bullshit
    //      -Unfiltered autism
    //      -Shitcode
    //      -Various oddities
    //
    //          YOU HAVE BEEN WARNED!!



    //tribe startingstars patch
    [HarmonyPostfix]  //in this method it doesnt matter which one you use
    [HarmonyPatch(typeof(StartMatchAction), nameof(StartMatchAction.ExecuteDefault))] //"dude polyscript is easy you just gotta polyscript"
    private static void StartMatchAction_ExecuteDefault(StartMatchAction __instance) // code helped alot by fapingvin
    {
        GameState state = GameManager.GameState; //lets get this bread, but instead of bread its the gameState

        if (GameManager.Client.clientType == ClientBase.ClientType.Local || GameManager.Client.clientType == ClientBase.ClientType.PassAndPlay) //check if not online multiplayer (important step because john klipi did it so its probably important)
        {
            foreach (PlayerState playerState in GameManager.GameState.PlayerStates) //make it apply to everyone (note: polytopia can have more than one players/bots, dumbass)
            {
                TribeData tribeData; //i was terrorized into adding this in by VS Code, it says its needed, i think it needs to touch fucking grass for once
                state.GameLogicData.TryGetData(playerState.tribe, out tribeData); //get the tribedata of current player

                if (startingStarsDict.TryGetValue(tribeData.type, out int stars)) //try to get the value from the dictionary we created (the value from the gld)
                {
                    playerState.Currency = stars;
                }
            }
        }
    }

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

    private static void ApplyEffect(GameState gameState, WorldCoordinates Origin, WorldCoordinates Target, UnitEffect effect)
    {
        TileData tile = gameState.Map.GetTile(Origin);
        TileData tile2 = gameState.Map.GetTile(Target);
        UnitState unit = tile.unit;
        UnitState unit2 = tile2.unit;
        if (unit2 == null)
        {
            return;
        }
        unit2.AddEffect(effect);
        if (unit2.passengerUnit != null)
        {
            unit2.passengerUnit.AddEffect(effect);
        }
    }

    //improvement defenceBoost patch
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
                if (unitEffectDataDict.TryGetValue(effect, out var effectData))
                {
                    effectMultiplicative = (effectData.defenceMult != 0) ? effectMultiplicative + (effectData.defenceMult / 10) : effectMultiplicative;
                }
            }
            if (tile.owner == unit.owner && tile.improvement != null && defenceBoostDict.TryGetValue(tile.improvement.type, out int def))
            {
                finaldef = def; //python users got triggered here
            }
            else if (tile.owner == unit.owner && tile.improvement != null && tile.improvement.type == ImprovementData.Type.City && tile.improvement.rewards != null)
            {
                int def2 = 0;
                modLogger!.LogInfo(tile.improvement.rewards.Count);
                foreach (CityReward reward in tile.improvement.rewards)
                {
                    if (cityRewardDict.TryGetValue(reward, out var cityRewardData))
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
        foreach (CityReward reward in GetSpawningRewardsForUnit(unitData.type))
        {
            boostDefenceOverSpawn += GetRewardData(reward).boostDefenceOverSpawn;
        }
        __result = (unitData.defence + boostDefenceOverSpawn * GetRewardCountForPlayer(unit.owner, GetSpawningRewardsForUnit(unitData.type))) * unit.GetDefenceBonus(state);
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
            if (unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                effectAdditive = effectAdditive + effectData.movementAdd;
                effectMultiplicative = (effectData.movementMult != 0) ? effectMultiplicative * (effectData.movementMult / 10) : effectMultiplicative;
            }
        }
        int boostMovementOverSpawn = 0;
        foreach (CityReward reward in GetSpawningRewardsForUnit(unitData.type))
        {
            boostMovementOverSpawn += GetRewardData(reward).boostMovementOverSpawn;
        }
        __result = ((unitData.GetMovement() + boostMovementOverSpawn * GetRewardCountForPlayer(unitState.owner, GetSpawningRewardsForUnit(unitData.type))) * effectMultiplicative) + effectAdditive;
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
            if (unitEffectDataDict.TryGetValue(effect, out var effectData))
            {
                effectAdditive = effectAdditive + effectData.attackAdd;
                effectMultiplicative = (effectData.attackMult != 0) ? effectMultiplicative * (effectData.attackMult / 10) : effectMultiplicative;
            }
        }
        int boostAttackOverSpawn = 0;
        foreach (CityReward reward in GetSpawningRewardsForUnit(unitData.type))
        {
            boostAttackOverSpawn += GetRewardData(reward).boostAttackOverSpawn;
        }
        __result = ((unitData.GetAttack() + boostAttackOverSpawn * GetRewardCountForPlayer(unitState.owner, GetSpawningRewardsForUnit(unitData.type)) * 10) * effectMultiplicative) + effectAdditive * 10;
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
        /*
        if (unitBuffDict.TryGetValue(unitState.owner, out var unitBuffData) && unitBuffData.unit == unitData.type)
        {
            __result = unitData.health + (unitState.promotionLevel * 50) + unitBuffData.mhp;
            return;
        }
        */
        int boostMaxHpOverSpawn = 0;
        foreach (CityReward reward in GetSpawningRewardsForUnit(unitData.type))
        {
            boostMaxHpOverSpawn += GetRewardData(reward).boostMaxHpOverSpawn;
        }
        __result = unitData.health + (unitState.promotionLevel * 50) + (boostMaxHpOverSpawn * GetRewardCountForPlayer(unitState.owner, GetSpawningRewardsForUnit(unitData.type)));
    }
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

    //MAN I SURE HOPE CITYREWARD ISN'T CODED LIKE HOT FUCKING GARBAGE, OH WAIT!! IT IS!! ITS ALL HARD CODED!!! YAAAAYYY!!!!
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CityRewardAction), nameof(CityRewardAction.Execute))]
    public static bool CityRewardAction_Execute(GameState state, CityRewardAction __instance)
    {
        CityReward reward = __instance.Reward;
        byte playerId = __instance.PlayerId;
        TileData tile = state.Map.GetTile(__instance.Coordinates);

        if (cityRewardDict.TryGetValue(reward, out var cityRewardData))
        {
            if (cityRewardData.productionModifier != 0)
            {
                state.ActionStack.Add(new ModifyProductionAction(__instance.PlayerId, System.Convert.ToInt16(cityRewardData.productionModifier), __instance.Coordinates));
            }
            if (cityRewardData.currencyReward != 0)
            {
                state.ActionStack.Add(new IncreaseCurrencyAction(playerId, tile.coordinates, cityRewardData.currencyReward, 40));
            }
            if (cityRewardData.populationReward != 0)
            {
                for (int i = 0; i < cityRewardData.populationReward; i++)
                {
                    state.ActionStack.Add(new IncreasePopulationAction(playerId, tile.coordinates, tile.coordinates, 40));
                }
            }
            if (cityRewardData.scoreReward != 0)
            {
                state.ActionStack.Add(new IncreaseScoreAction(playerId, cityRewardData.scoreReward, tile.coordinates, 0));
            }
            for (int i = 0; i < cityRewardData.scoutSpawnAmount; i++)
            {
                state.ActionStack.Add(new ScoutMoveAction(playerId, state.GetNextUnitId(), System.Convert.ToUInt32(cityRewardData.scoutMoveAmount), state.RandomHash.GetHash(tile.coordinates.X, tile.coordinates.Y), tile.coordinates, new Il2Gen.List<WorldCoordinates>()));
            }
            for (int i = 0; i < cityRewardData.borderGrowthAmount; i++)
            {
                __instance.AddBorderGrowthActions(state, tile);
            }
            if (cityRewardData.unitType != UnitData.Type.None)
            {
                if (cityRewardData.boostAttackOverSpawn != 0 || cityRewardData.boostDefenceOverSpawn != 0 || cityRewardData.boostMaxHpOverSpawn != 0 || cityRewardData.boostMovementOverSpawn != 0 || cityRewardData.healUnitOverSpawn)
                {
                    int num = 0;
                    MapData map = state.Map;
                    foreach (TileData tile1 in map.tiles)
                    {
                        if (tile1.unit != null && tile1.unit.type == cityRewardData.unitType)
                        {
                            num++;
                        }
                    }
                    if (num == 0)
                    {
                        ActionUtils.TrainUnitOnOccupiedSpace(state, playerId, cityRewardData.unitType, tile);
                    }
                    if (cityRewardData.healUnitOverSpawn)
                    {
                        foreach (TileData tile1 in map.tiles)
                        {
                            if (tile1.unit != null && tile1.unit.type == cityRewardData.unitType)
                            {
                                tile1.unit.health = (ushort)tile1.unit.GetMaxHealth(state);
                            }
                        }
                    }
                }
                else
                {
                    ActionUtils.TrainUnitOnOccupiedSpace(state, playerId, cityRewardData.unitType, tile);
                }
            }
            tile.improvement.AddReward(reward);
            return false;
        }
        else { return true; }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(global::ImprovementDataExtensions), "GetCityRewardsForLevel")] //this is the polyscript equivalent of the pear of anguish (idk what the name is yk that iron shit that they shove up your ass and then they extend it and it opens and it mighty fucks up you arsehole)
    public static bool ImprovementDataExtentions_GetCityRewardsForLevel(ref Il2CppStructArray<CityReward> __result, ImprovementData data, int level)
    {
        if (GameManager.Client.GameState.Settings.GameType == GameType.Competitive || GameManager.Client.GameState.Settings.GameType == GameType.Multiplayer)
        {
            return true;
        }

        Il2Gen.List<CityReward> list = new Il2Gen.List<CityReward>();
        GameState state = GameManager.GameState;

        PlayerState playerState;
        TribeData.Type tribeType = TribeData.Type.Aimo;
        if (state.TryGetPlayer(state.CurrentPlayer, out playerState))
        {
            tribeType = playerState.tribe;
        }
        else { modLogger!.LogInfo($"KRIS SHIT IS SERIOUSLY FUCKED"); }


        foreach (CityReward reward in rewardList)
        {
            if (cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if ((cityRewardData.level == level || (cityRewardData.persistence == "post" && cityRewardData.level <= level) || (cityRewardData.persistence == "pre" && cityRewardData.level >= level)) && !cityRewardData.hidden)
                {
                    if (cityRewardOverrideDict.TryGetValue(tribeType, out var cityRewardOverrideClasses))
                    {
                        int num2 = 0;
                        foreach (CityRewardOverrideClass overrideClass in cityRewardOverrideClasses)
                        {
                            if (overrideClass != null)
                            {
                                if (overrideClass.og == reward)
                                {
                                    list.Add(overrideClass.neu);
                                }
                                else
                                {
                                    num2++;
                                }
                            }
                        }
                        if (num2 >= cityRewardOverrideClasses.Count)
                        {
                            list.Add(reward);
                        }
                    }
                    else
                    {
                        list.Add(reward);
                    }

                }
            }
        }

        List<CityReward> orderedlist = ToSystemList(list);
        System.Comparison<CityReward> comparison = (a, b) => cityRewardDict[a].order.CompareTo(cityRewardDict[b].order);

        orderedlist.Sort(comparison);

        Il2CppStructArray<CityReward> array = ArrayFromListIl2Cpp(ToIl2CppList(orderedlist));

        if (array != null || array.Length != 0)
        {
            __result = array;
            return false;
        }
        else { return true; }

    }

    public static bool isCustomReward(string s) //fapingvin came in clutch with this one
    {
        //Is it even a city reward?
        //idk fap you tell me
        string[] words = s.Split("_");
        if (words[1] != "rewards")
        {
            return false;
        }


        if (int.TryParse(words[2], out int whatever)) //parse? i hate parse! all my homies hate parse! fuck parse! yeah!
        {
            return true;
        }

        return false;
    }

    public static CityReward getEnum(string s) //wow talk about naming things clearly
    {
        int a = int.Parse(s.Split("_")[2]); //sure?
        return (CityReward)a;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIIconData), nameof(UIIconData.GetSprite))] //idfk what this does but yeah
    public static void UIIconData_GetSprite(UIIconData __instance, ref Une.Sprite __result, string id)
    {
        if (Main.isCustomReward(id))
        {
            __result = PolyMod.Registry.GetSprite(EnumCache<CityReward>.GetName(Main.getEnum(id)))!;
        }
    }

    [HarmonyPrefix] //I HATE THIS I HATE I HATE I HATE FUCK THIS SHIT FUCKING HELL I HATE THIS SO MUCH WHY DOESNT IT WORK WHY WHY WHYX WHY WHY WHY HWY HWY HWYH WHH A FUCK
    [HarmonyPatch(typeof(AI), nameof(AI.ChooseCityReward))]
    public static bool AI_ChooseCityReward(GameState gameState, TileData tile, CityReward[] rewards, ref CityReward __result)
    {
        GameLogicData gld = gameState.GameLogicData;
        CityReward[] rewardarray = AIIsFuckingWithMe_GetCityRewardsForLevel(gld.GetImprovementData(tile.improvement.type), tile.improvement.level - 1);



        System.Random random = new System.Random();
        int num = random.Next(0, rewardarray.Length);



        __result = rewardarray[num];

        return false;
    }

    public static CityReward[] AIIsFuckingWithMe_GetCityRewardsForLevel(ImprovementData data, int level) //c# waterboarding. i'm not gonna elaborate. leave.
    {
        Il2CppSystem.Collections.Generic.List<CityReward> list = new Il2CppSystem.Collections.Generic.List<CityReward>();

        GameState state = GameManager.GameState;

        PlayerState playerState;
        state.TryGetPlayer(state.CurrentPlayer, out playerState);
        TribeData.Type tribeType = TribeData.Type.Aimo;
        if (state.TryGetPlayer(state.CurrentPlayer, out playerState))
        {
            tribeType = playerState.tribe;
        }
        else { modLogger!.LogInfo($"KRIS SHIT IS SERIOUSLY FUCKED"); }

        foreach (CityReward reward in rewardList)
        {
            if (cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if ((cityRewardData.level == level || (cityRewardData.persistence == "post" && cityRewardData.level <= level) || (cityRewardData.persistence == "pre" && cityRewardData.level >= level)) && !cityRewardData.hidden)
                {
                    if (cityRewardOverrideDict.TryGetValue(tribeType, out var cityRewardOverrideClasses))
                    {
                        int num2 = 0;
                        foreach (CityRewardOverrideClass overrideClass in cityRewardOverrideClasses)
                        {
                            if (overrideClass != null)
                            {
                                if (overrideClass.og == reward)
                                {
                                    list.Add(overrideClass.neu);
                                }
                                else
                                {
                                    num2++;
                                }
                            }
                        }
                        if (num2 >= cityRewardOverrideClasses.Count)
                        {
                            list.Add(reward);
                        }
                    }
                    else
                    {
                        list.Add(reward);
                    }

                }
            }
        }
        List<CityReward> orderedlist = ToSystemList(list);
        System.Comparison<CityReward> comparison = (a, b) => cityRewardDict[a].order.CompareTo(cityRewardDict[b].order);

        orderedlist.Sort(comparison);

        Il2CppStructArray<CityReward> array = ArrayFromListIl2Cpp(ToIl2CppList(orderedlist));

        if (array != null || array.Length != 0)
        {
            return array;

        }
        else { modLogger!.LogInfo($"KRIS WTF HAPPENED?? AI [GetCityRewardsForLevel] COULDN'T FUCKING FIND A DAMN [CityReward[]]!!"); return new CityReward[2]; }

    }













    //my lil'  startingResourceGeneratorThingy rewrite
    //whatever you do, DO NOT TOUCH IT!!! IT ***WILL*** BREAK!!!
    //man I really thought this was one of my bigger features. And then I was introduced to cityRewards
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.addStartingResourcesToCapital))]
    public static bool MapGenerator_addStartingResourcesToCapital(MapGenerator __instance, MapData map, GameState gameState, PlayerState player, ResourceData startingResource, int minResourcesCount = 2)
    {
        TribeData tribeData;
        if (!PolytopiaDataManager.GetGameLogicData(VersionManager.GetGameLogicDataVersionFromGameVersion(gameState.Version)).TryGetData(player.tribe, out tribeData))
        {
            modLogger!.LogInfo($"HEY     EVERY      !! WE DIDNT [Fifty Percent Off]ING GET THE [TribeData] OF TRIBE {player.tribe}!!");
        }

        if (tribeData != null && startingResources.TryGetValue(tribeData.type, out var list)) //if things are okay (we check so we dont get errors)
        {
            foreach (TileData tile in map.GetArea(player.startTile, 1, true, false)) //change climate of tiles (important for polaris and packed games)
            {
                tile.climate = gameState.GameLogicData.GetTribeData(player.tribe).climate;
            }
            foreach ((ResourceData.Type ListResType, int ListAmount) in list) //change resource amounts (basically, for some reason, I chose to do defaulting in here, not somewhere else)
            {

                int amount = ListAmount;
                ResourceData.Type restype = ListResType;

                if (ListAmount == -1 && restype == ResourceData.Type.Fruit)
                {
                    amount = 2;
                    if (tribeData.startingResource.Count != 0)
                    {
                        restype = tribeData.startingResource[0].type;
                    }
                    else
                    {
                        restype = ResourceData.Type.Fruit;
                        modLogger!.LogInfo($"KRIS YOU LEFT YOUR [ResourceData.Type]s ON AISLE 3 [Lyeing Around]?? TF?");
                    }
                }

                Il2Gen.List<TileData> area = map.GetArea(player.startTile, 1, true, false);
                __instance.Shuffle(area);
                GameLogicData gld = gameState.GameLogicData;
                ResourceData resdata = gld.GetResourceData(restype); //poo
                Il2Gen.List<Polytopia.Data.TerrainData> terraindatalist = resdata.resourceTerrainRequirements; //I LOVE IL2CPP TO BITS
                int rgamount = 0;
                int terrainNotMatchCount = 0;
                Il2Gen.List<TileData> terrainNotMatchList = new Il2Gen.List<TileData>(); //I LOOOOVE WRITING IT OUT EVERY SINGLE FUCKING TIME (this is outdated as i have updated my code to use aliases, so I dont have to write a fucking novel each time I want-.. I mean HAVE to use Il2Cpp)
                Il2Gen.List<TileData> selectedTileList = new Il2Gen.List<TileData>(); //why


                foreach (TileData tile in area)
                {
                    if (tile != null)
                    {
                        if (rgamount >= amount) { }
                        else
                        if (tile.resource != null && tile.resource.type == restype)
                        {
                            rgamount++;
                        }
                        else
                        if (terraindatalist.Contains(tile.terrain))
                        {
                            selectedTileList.Add(tile);
                            rgamount++;
                        }
                        else
                        {
                            terrainNotMatchCount++;
                            terrainNotMatchList.Add(tile);
                        }
                    }
                    else { modLogger!.LogInfo($"KRIS WHAT THE &#!@ [Tile] IS [Null]??? WHY?? [Y]?? [Yellow]??"); }

                }

                if (terrainNotMatchCount + rgamount == 8 && rgamount < amount)
                {
                    modLogger!.LogInfo($"KRIS WE DONT @&!%ING HAVE ENOUGH [TerrainData.Type], FIXING IT NOW");
                    int necessaryAmount = amount - rgamount;
                    for (int i = 0; i < necessaryAmount; i++)
                    {
                        System.Random rng = new System.Random();
                        int result = rng.Next(0, terraindatalist.Count);

                        terrainNotMatchList[i].terrain = terraindatalist[result].type;
                        selectedTileList.Add(terrainNotMatchList[i]);
                    }
                }

                __instance.Shuffle(selectedTileList);
                foreach (TileData tile in selectedTileList)
                {
                    if (tile != null)
                    {
                        tile.resource = new ResourceState
                        {
                            type = restype
                        };
                    }

                }
            }
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AI), "CheckForTechNeeds")]
    public static bool Logshit(GameState gameState, PlayerState player, List<TileData> playerEmpire, Il2Gen.Dictionary<TechData.Type, int> neededTech)
    {
        int num = 0;
        int num2 = 0;
        for (int i = 0; i < gameState.Map.Tiles.Length; i++)
        {
            TileData tileData = gameState.Map.Tiles[i];
            bool explored = tileData.GetExplored(player.Id);
            if (explored)
            {
                bool flag = tileData.owner == player.Id;
                if (flag)
                {
                    bool flag2 = tileData.HasImprovement(ImprovementData.Type.City) && !tileData.IsConnected;
                    if (flag2)
                    {
                        num2++;
                    }
                    bool flag3 = tileData.terrain == Polytopia.Data.TerrainData.Type.Field || tileData.terrain == Polytopia.Data.TerrainData.Type.Forest;
                    if (flag3)
                    {
                        num++;
                    }
                }
                bool flag4 = !tileData.CanBeAccessedByPlayer(gameState, player);
                if (flag4)
                {
                    TechData techThatUnlocks = gameState.GameLogicData.GetTechThatUnlocks(tileData.terrain);
                    bool flag5 = techThatUnlocks != null;
                    if (flag5)
                    {
                        AI.AddTechNeed(neededTech, techThatUnlocks!.type, 1);
                    }
                    else
                    {
                        modLogger?.LogInfo("HOTCHI MAMA, KRIS, [Slow Down] THERE! I JUST SAVED YOUR [$2.99] LIFE FROM A [Null Crash1997]!! ALSO, WHO IS [Lougg Kaard]??");
                    }
                }
                bool flag6 = tileData.resource != null && gameState.GameLogicData.IsResourceVisibleToPlayer(tileData.resource.type, player);
                if (flag6)
                {
                    Il2Gen.List<ImprovementData> improvementForResource = gameState.GameLogicData.GetImprovementForResource(tileData.resource!.type);
                    for (int j = 0; j < improvementForResource.Count; j++)
                    {
                        ImprovementData improvementData = improvementForResource[j];
                        bool flag7 = improvementData != null && improvementData.HasAbility(ImprovementAbility.Type.Freelance) && !gameState.GameLogicData.IsUnlocked(improvementData.type, player);
                        if (flag7)
                        {
                            TribeData tribeData = gameState.GameLogicData.GetTribeData(player.tribe);
                            TechData techThatUnlocks2 = gameState.GameLogicData.GetTechThatUnlocks(improvementData, tribeData);
                            AI.AddTechNeed(neededTech, techThatUnlocks2.type, 5);
                        }
                    }
                }
            }
        }
        bool flag8 = num > 0;
        if (flag8)
        {
            int num3 = num * (1 + num2);
            AI.AddTechNeed(neededTech, TechData.Type.Roads, num3);
        }
        return false;
    }

    public static PolibCityRewardData SetVanillaCityRewardDefaults(CityReward reward) //dont laugh
    {
        PolibCityRewardData rewardData = new PolibCityRewardData();
        switch (reward)
        {
            case CityReward.Workshop:
                {
                    rewardData = new PolibCityRewardData
                    {
                        productionModifier = 1,
                        level = 1,
                        order = 0
                    };
                    break;
                }
            case CityReward.Explorer:
                {
                    rewardData = new PolibCityRewardData
                    {
                        scoutSpawnAmount = 1,
                        scoutMoveAmount = 15,
                        level = 1,
                        order = 1
                    };
                    break;
                }
            case CityReward.Resources:
                {
                    rewardData = new PolibCityRewardData
                    {
                        currencyReward = 5,
                        level = 2,
                        order = 1
                    };
                    break;
                }
            case CityReward.CityWall:
                {
                    rewardData = new PolibCityRewardData
                    {
                        defenceBoostReward = 40,
                        level = 2,
                        order = 0
                    };
                    break;
                }
            case CityReward.PopulationGrowth:
                {
                    rewardData = new PolibCityRewardData
                    {
                        populationReward = 3,
                        level = 3,
                        order = 0
                    };
                    break;
                }
            case CityReward.BorderGrowth:
                {
                    rewardData = new PolibCityRewardData
                    {
                        borderGrowthAmount = 1,
                        level = 3,
                        order = 1
                    };
                    break;
                }
            case CityReward.Park:
                {
                    rewardData = new PolibCityRewardData
                    {
                        productionModifier = 1,
                        scoreReward = 250,
                        level = 4,
                        persistence = "post",
                        order = 0
                    };
                    break;
                }
            case CityReward.SuperUnit:
                {
                    rewardData = new PolibCityRewardData
                    {
                        unitType = UnitData.Type.Giant, //i really like that I dont have to account for unitOverride
                        level = 4,
                        persistence = "post",
                        order = 1
                    };
                    break;
                }
        }
        return rewardData;
    }
    public static PolibUnitEffectData SetVanillaUnitEffectDefaults(UnitEffect effect)
    {
        PolibUnitEffectData effectData = new PolibUnitEffectData();
        switch (effect)
        {
            case UnitEffect.Boosted:
                {
                    effectData = new PolibUnitEffectData
                    {
                        movementAdd = 1,
                        attackAdd = 5,
                        removal = new List<string> { "action", "attack", "hurt" }
                    };
                    break;
                }
            case UnitEffect.Poisoned:
                {
                    effectData = new PolibUnitEffectData
                    {
                        defenceMult = 7,
                        removal = new List<string> { "heal" }
                    };
                    break;
                }
            case UnitEffect.Bubble:
                {
                    effectData = new PolibUnitEffectData
                    {
                        movementAdd = 1,
                        removal = new List<string> { "nonflooded", "hurt" }
                    };
                    break;
                }
            case UnitEffect.Frozen:
                {
                    effectData = new PolibUnitEffectData
                    {
                        freezing = true,
                        removal = new List<string> { "endturn" }
                    };
                    break;
                }
            case UnitEffect.Petrified:
                {
                    effectData = new PolibUnitEffectData
                    {
                        freezing = true,
                        removal = new List<string> { "endturn" }
                    };
                    break;
                }
        }
        return effectData;

    }
    public static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(System.Collections.Generic.List<T> sysList)
    {
        var il2cppList = new Il2CppSystem.Collections.Generic.List<T>();
        for (int i = 0; i < sysList.Count; i++)
        {
            il2cppList.Add(sysList[i]);
        }
        return il2cppList;
    }
    public static List<T> ToSystemList<T>(Il2Gen.List<T> il2cppList)
    {
        var sysList = new List<T>(il2cppList.Count);
        for (int i = 0; i < il2cppList.Count; i++)
        {
            sysList.Add(il2cppList[i]);
        }
        return sysList;
    }
    public static T[] ArrayFromListIl2Cpp<T>(Il2Gen.List<T> il2cppList)
    {
        T[] array = new T[il2cppList.Count];
        for (int i = 0; i < il2cppList.Count; i++)
        {
            array[i] = il2cppList[i];
        }
        return array;
    }
    public static T[] ArrayFromListSystem<T>(List<T> sysList)
    {
        T[] array = new T[sysList.Count];
        for (int i = 0; i < sysList.Count; i++)
        {
            array[i] = sysList[i];
        }
        return array;
    }
    public static T[] MakeSystemArray<T>(T value)
    {
        return new T[] { value };
    }
    public static List<T> MakeSystemList<T>(T[] array)
    {
        return new List<T>(array);
    }
    public static int GetRewardCountForPlayer(byte playerId, CityReward[] targetRewards)
    {
        GameManager.GameState.TryGetPlayer(playerId, out var playerState);
        Il2Gen.List<TileData> tiles = playerState.GetCityTiles(GameManager.GameState);
        int num = 0;
        foreach (TileData tile in tiles)
        {
            Il2Gen.List<CityReward> rewards = tile.improvement.rewards;
            foreach (CityReward checkedReward in rewards)
            {
                if (targetRewards != null)
                {
                    foreach (CityReward compareToThisReward in targetRewards)
                    {
                        if (checkedReward == compareToThisReward)
                        {
                            num++;
                        }
                    }
                }
            }
        }
        return num;
    }
    public static int GetRewardCountForPlayer(byte playerId, CityReward targetReward)
    {
        return GetRewardCountForPlayer(playerId, MakeSystemArray(targetReward));
    }
    public static CityReward[] GetSpawningRewardsForUnit(UnitData.Type unit)
    {
        List<CityReward> list = new List<CityReward>();
        foreach (CityReward reward in rewardList)
        {
            if (cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if (cityRewardData.unitType == unit)
                {
                    list.Add(reward);
                }
            }
        }
        return ArrayFromListSystem(list);
    }
    public static PolibCityRewardData GetRewardData(CityReward reward)
    {
        cityRewardDict.TryGetValue(reward, out var data);
        return data;
    }








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


