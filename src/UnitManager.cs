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
                steve!.LogInfo(tile.improvement.rewards.Count);
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
}