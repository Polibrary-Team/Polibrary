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
using Il2CppSystem.Text.Json;
using pbb = PolytopiaBackendBase.Common;


namespace Polibrary;

public static class ActionTriggers
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(ActionTriggers));
        modLogger = logger;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void BuildTriggers(BuildAction __instance, GameState gameState)
    {
        if (Parse.improvementTriggers.TryGetValue(__instance.Type, out var dict))
        {
            if (dict.TryGetValue("onBuild", out string name))
            {
                PolibUtils.RunAction(name, __instance.Coordinates, __instance.PlayerId);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    private static void MoveTriggers(MoveAction __instance, GameState gameState)
    {
        gameState.TryGetUnit(__instance.UnitId, out UnitState unit);

        if (Parse.unitTriggers.TryGetValue(unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onMove", out string name))
            {
                PolibUtils.RunAction(name, unit.coordinates, __instance.PlayerId);
            }
            if (unitdict.TryGetValue("onMove_AtOrigin", out string name1))
            {
                PolibUtils.RunAction(name1, __instance.Path[__instance.Path.Count - 1], __instance.PlayerId);
            }
        }

        foreach (WorldCoordinates coords in __instance.Path)
        {
            if (GameManager.GameState.Map.GetTile(coords).improvement == null) continue;
            
            if (Parse.improvementTriggers.TryGetValue(GameManager.GameState.Map.GetTile(coords).improvement.type, out var impdict))
            {
                if (impdict.TryGetValue("onStep", out string name))
                {
                    PolibUtils.RunAction(name, unit.coordinates, __instance.PlayerId);
                }
            }
        }

        if (GameManager.GameState.Map.GetTile(__instance.Path[0]).improvement != null)
        {
            if (Parse.improvementTriggers.TryGetValue(GameManager.GameState.Map.GetTile(__instance.Path[0]).improvement.type, out var impdict1))
            {
                if (impdict1.TryGetValue("onLand", out string name))
                {
                    PolibUtils.RunAction(name, unit.coordinates, __instance.PlayerId);
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackAction), nameof(AttackAction.Execute))]
    private static void AttackTriggers(AttackAction __instance, GameState state)
    {
        UnitState attacker = GameManager.GameState.Map.GetTile(__instance.Origin).unit;
        UnitState defender = GameManager.GameState.Map.GetTile(__instance.Target).unit;

        if (Parse.unitTriggers.TryGetValue(attacker.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onAttack", out string name1))
            {
                PolibUtils.RunAction(name1, defender.coordinates, __instance.PlayerId);
            }
            if (unitdict.TryGetValue("onAttack_AtOrigin", out string name))
            {
                PolibUtils.RunAction(name, attacker.coordinates, __instance.PlayerId);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.Execute))]
    private static void MoveCommandTriggers(MoveCommand __instance, GameState gameState)
    {
        gameState.TryGetUnit(__instance.UnitId, out UnitState unit);

        if (Parse.unitTriggers.TryGetValue(unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onMoveCommand", out string name))
            {
                PolibUtils.RunAction(name, unit.coordinates, __instance.PlayerId);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ExpandCityAction), nameof(ExpandCityAction.ExecuteDefault))]
    private static void ExpandCityTriggers(ExpandCityAction __instance, GameState state)
    {
        TileData tile = state.Map.GetTile(__instance.Coordinates);
        state.TryGetPlayer(__instance.PlayerId, out var playerState);
        Il2Gen.List<TileData> cityAreaSorted = ActionUtils.GetCityAreaSorted(state, tile);
        cityAreaSorted.Reverse();

        foreach (TileData tile1 in cityAreaSorted)
        {
            if (Parse.improvementTriggers.TryGetValue(tile1.improvement.type, out var impdict))
            {
                if (impdict.TryGetValue("onCaptured", out string name))
                {
                    PolibUtils.RunAction(name, tile1.coordinates, __instance.PlayerId);
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityRewardAction), nameof(CityRewardAction.Execute))]
    private static void CityRewardTriggers(CityRewardAction __instance, GameState state)
    {
        if (Parse.rewardTriggers.TryGetValue(__instance.Reward, out var unitdict))
        {
            if (unitdict.TryGetValue("onRewardChosen", out string name))
            {
                PolibUtils.RunAction(name, __instance.Coordinates, __instance.PlayerId);
            }
        }
    }
}