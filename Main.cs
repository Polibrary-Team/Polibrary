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
using PolyMod.Json;
using Il2CppSystem.Linq;


namespace Polibrary;

public static class Main
{


    private static ManualLogSource? modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Main));
        modLogger = logger;
        logger.LogMessage("Polibrary.dll loaded.");
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

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject? token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    if (token["BuiltBySpecific"] != null)
                    {
                        string ability = token["BuiltBySpecific"]!.ToObject<string>();
                        BuildersDict[impType] = ability;
                        token.Remove("BuiltBySpecific");
                        modLogger.LogInfo($"Added {ability} ability to {impType} in BuildersDict");
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
            JObject? token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    if (token["NotBuiltBySpecific"] != null)
                    {
                        string ability = token["NotBuiltBySpecific"]!.ToObject<string>();
                        NoBuildersDict[impType] = ability;
                        token.Remove("NotBuildBySpecific");
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
            JObject? token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    if (token["Unblock"] != null)
                    {
                        string ability = token["Unblock"]!.ToObject<string>();
                        UnblockDict[impType] = ability;
                        token.Remove("Unblock");
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


}


