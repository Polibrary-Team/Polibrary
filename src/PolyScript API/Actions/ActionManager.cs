using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Polytopia.Data;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;
using Il2Gen = Il2CppSystem.Collections.Generic;
using pbb = PolytopiaBackendBase.Common;
using System.Reflection;
using UnityEngine;
using Il2CppInterop.Runtime.Runtime;

namespace Polibrary;

public static class ActionManager
{
    /*
    USAGE:

    0. Have a class that inherits from PolibCommandBase, and overrides all the necessary things
    1. register the commandtypes in the patch.json (in pCommands, as seen in the polib patch.json)
    2. postfix GameLogicData.AddGameLogicPlaceholders and call Polibrary.pCommand.CommandManager.RegisterCommand()
    3. ur done
    */

    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(ActionManager));
        ClassInjector.RegisterTypeInIl2Cpp<PolibActionBase>();
    }

    static Dictionary<ActionType, Type> ActionMapping = new();
    static Dictionary<Type, ActionType> ActionReverseMapping = new();

    #region Utils

    /// <summary>
    /// Register an action into Polibrary.
    /// </summary>
    /// <typeparam name="T">Must be a class.</typeparam>
    /// <param name="actionType">The ActionType in the JSON.</param>
    public static void RegisterAction<T>(string actionType) where T : class
    {
        if (EnumCache<ActionType>.TryGetType(actionType, out var aType))
        RegisterAction<T>(aType);

        else
        Main.modLogger.LogError($"Failed to register action '{actionType}'. Enum isn't valid. Check spelling.");
    }

    /// <summary>
    /// Register an action into Polibrary. The string overload is preferred for usage.
    /// </summary>
    /// <typeparam name="T">Must be a class.</typeparam>
    /// <param name="actionType">The EnumCached ActionType.</param>
    public static void RegisterAction<T>(ActionType actionType) where T : class
    {
        ActionMapping[actionType] = typeof(T);
        ActionReverseMapping[typeof(T)] = actionType;
        ClassInjector.RegisterTypeInIl2Cpp<T>();
        Main.modLogger.LogInfo($"Registered action '{actionType}' as an action.");
    }

    /// <summary>
    /// Create an Il2Cpp instance of an action.
    /// </summary>
    /// <typeparam name="T">Must inherit from <see cref="PolibActionBase"/>.</typeparam>
    /// <returns>Returns a usable PolibActionBase instance.</returns>
    public static T MakeIl2CppAction<T>() where T : PolibActionBase
    {
        T action = (T)Il2CppSystem.Activator.CreateInstance(WrapType<T>());
        return action;
    }

    #endregion

    #region Patches

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.actionType.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                EnumCache<ActionType>.AddMapping(token.Path.Split('.').Last(), (ActionType)PolyMod.Registry.autoidx);
                EnumCache<ActionType>.AddMapping(token.Path.Split('.').Last(), (ActionType)PolyMod.Registry.autoidx);
                Main.modLogger.LogInfo($"Added action mapping '{token.Path.Split('.').Last()}', id: {PolyMod.Registry.autoidx}");
                PolyMod.Registry.autoidx++;
            }
        }
        RegisterAction<PolibActionBase>("polibactionbase");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.GetAction))]
    private static bool GameState_GetAction(ref ActionBase  __result, ActionType type) 
    {
        Main.modLogger.LogInfo($"GetAction for {type}");
        if (ActionMapping.TryGetValue(type, out Type actionClass))
        {
            MethodInfo wrapMethod = typeof(ActionManager)
                .GetMethod(nameof(WrapType), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(actionClass);

            Il2CppSystem.Type il2cppType = (Il2CppSystem.Type)wrapMethod.Invoke(null, null);
            __result = (ActionBase)Il2CppSystem.Activator.CreateInstance(il2cppType);
            return false;
        }
        return true;
    }
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionBase), nameof(ActionBase.Execute))]
    private static void ActionBase_Execute(ref ActionBase  __instance, GameState state)
    {
        if (ActionReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            PolibActionBase action = __instance.Cast<PolibActionBase>();
            action.ExecuteNew(state);
        }
    }*/
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionBase), nameof(ActionBase.Serialize))]
    private static void ActionBase_Serialize(ref ActionBase  __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        Main.modLogger.LogInfo($"Serialize for {__instance.GetType}");
        if (ActionReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            PolibActionBase action = __instance.Cast<PolibActionBase>();
            action.SerializeNew(writer, version);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionBase), nameof(ActionBase.Deserialize))]
    private static void ActionBase_Deserialize(ref ActionBase  __instance, Il2CppSystem.IO.BinaryReader reader, int version)
    {
        Main.modLogger.LogInfo($"Deserialize for {__instance.GetType}");
        if (ActionReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            PolibActionBase action = __instance.Cast<PolibActionBase>();
            action.DeserializeNew(reader, version);
        }
    }

    internal static Il2CppSystem.Type WrapType<T>() where T : class
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return Il2CppType.From(typeof(T));
    }


    #endregion

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void REGISTER_TEST(GameLogicData __instance, JObject rootObject)
    {
        //RegisterAction<>("testcommand");
    }
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetUnitActions))]
	private static void ACTIONS_TEST(ref Il2Gen.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable)
    {
        PolibCommandBase command = CommandManager.MakeIl2CppCommand<PolibCommandBase>();
        CommandUtils.AddCommand(gameState, __result, command, includeUnavailable);
    }*/
}