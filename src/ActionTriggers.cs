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
        }

        foreach (WorldCoordinates coords in __instance.Path)
        {
            if (Parse.improvementTriggers.TryGetValue(GameManager.GameState.Map.GetTile(coords).improvement.type, out var impdict))
            {
                if (impdict.TryGetValue("onStep", out string name))
                {
                    PolibUtils.RunAction(name, unit.coordinates, __instance.PlayerId);
                }
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

        
        if (Parse.improvementTriggers.TryGetValue(GameManager.GameState.Map.GetTile(__instance.To).improvement.type, out var impdict))
        {
            if (impdict.TryGetValue("onLand", out string name))
            {
                PolibUtils.RunAction(name, unit.coordinates, __instance.PlayerId);
            }
        }
        
    }
}