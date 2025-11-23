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


namespace Polibrary;

public static class PolibUnitAbility
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
        Harmony.CreateAndPatchAll(typeof(PolibUnitAbility));
    }
    
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetUnitActions))]
    public static void CommandUtils_GetUnitActions(Il2Gen.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
    {
        __result.Add(new pActionCommand(Parse.actions.GetValueOrDefault("polib_basiclog")));
        
    }
}

public class pActionCommand : CommandBase
{
    public pAction action;

    public pActionCommand(pAction paction)
    {
        action = paction;
    }
    
    public override void Execute(GameState state)
    {
        base.Execute(state);
        action.Execute();
    }

    public override CommandType GetCommandType()
    {
        return CommandType.None;
    }

    public override bool IsValid(GameState state, out string validationError)
    {
        validationError = "none";
        return true;
    }
}