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

public static class CommandManager
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
        Harmony.CreateAndPatchAll(typeof(CommandManager));
        ClassInjector.RegisterTypeInIl2Cpp<PolibCommandBase>();
    }

    static Dictionary<CommandType, Type> CommandMapping = new();
    static Dictionary<Type, CommandType> CommandReverseMapping = new();

    #region Utils

    /// <summary>
    /// Register a command into Polibrary.
    /// </summary>
    /// <typeparam name="T">Must be a class.</typeparam>
    /// <param name="commandType">The CommandType in the JSON.</param>
    public static void RegisterCommand<T>(string commandType) where T : class
    {
        if (EnumCache<CommandType>.TryGetType(commandType, out var cType))
        RegisterCommand<T>(cType);

        else
        Main.modLogger.LogError($"Failed to register command '{commandType}'. Enum isn't valid. Check spelling.");
    }

    /// <summary>
    /// Register a command into Polibrary. The string overload is preferred for usage.
    /// </summary>
    /// <typeparam name="T">Must be a class.</typeparam>
    /// <param name="commandType">The EnumCached CommandType.</param>
    public static void RegisterCommand<T>(CommandType commandType) where T : class
    {
        CommandMapping[commandType] = typeof(T);
        CommandReverseMapping[typeof(T)] = commandType;
        ClassInjector.RegisterTypeInIl2Cpp<T>();
        Main.modLogger.LogInfo($"Registered command '{commandType}' as a command.");
    }

    /// <summary>
    /// Create an Il2Cpp instance of a command.
    /// </summary>
    /// <typeparam name="T">Must inherit from <see cref="PolibCommandBase"/>.</typeparam>
    /// <returns>Returns a usable PolibCommandBase instance.</returns>
    public static T MakeIl2CppCommand<T>() where T : PolibCommandBase
    {
        T command = (T)Il2CppSystem.Activator.CreateInstance(WrapType<T>());
        return command;
    }

    #endregion

    #region Patches

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.CommandType.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                EnumCache<CommandType>.AddMapping(token.Path.Split('.').Last(), (CommandType)PolyMod.Registry.autoidx);
                EnumCache<CommandType>.AddMapping(token.Path.Split('.').Last(), (CommandType)PolyMod.Registry.autoidx);
                Main.modLogger.LogInfo($"Added command mapping '{token.Path.Split('.').Last()}', id: {PolyMod.Registry.autoidx}");
                PolyMod.Registry.autoidx++;

                CommandMapping[EnumCache<CommandType>.GetType(token.Path.Split('.').Last())] = typeof(PolibCommandBase);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.GetCommand))]
    private static bool GameState_GetCommand(ref CommandBase  __result, CommandType type) {
        Main.modLogger.LogInfo("GameState_GetCommand");
        Main.modLogger.LogInfo(type);

        if (CommandMapping.TryGetValue(type, out Type commandClass))
        {
            MethodInfo wrapMethod = typeof(CommandManager)
                .GetMethod(nameof(WrapType), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(commandClass);

            Il2CppSystem.Type il2cppType = (Il2CppSystem.Type)wrapMethod.Invoke(null, null);
            __result = (CommandBase)Il2CppSystem.Activator.CreateInstance(il2cppType);
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.Execute))]
    private static void CommandBase_Execute(ref CommandBase  __instance, GameState state)
    {
        if (CommandReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            PolibCommandBase command = __instance.Cast<PolibCommandBase>();
            command.ExecuteNew(state);
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.Serialize))]
    private static void CommandBase_Serialize(ref CommandBase  __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        if (CommandReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            PolibCommandBase command = __instance.Cast<PolibCommandBase>();
            command.SerializeNew(writer, version);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.Deserialize))]
    private static void CommandBase_Deserialize(ref CommandBase  __instance, Il2CppSystem.IO.BinaryReader reader, int version)
    {
        if (CommandReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            PolibCommandBase command = __instance.Cast<PolibCommandBase>();
            command.DeserializeNew(reader, version);
        }
    }

    internal static Il2CppSystem.Type WrapType<T>() where T : class
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return Il2CppType.From(typeof(T));
    }


    #endregion
}


    /*

    Here's a quick tutorial on how to use this.
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void REGISTER_TEST(GameLogicData __instance, JObject rootObject)
    {
        RegisterCommand<TestCommand>("testcommand");
    }

    Let's add this action to units for example.

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetUnitActions))]
	private static void ACTIONS_TEST(ref Il2Gen.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable)
    {
        TestCommand command = MakeIl2CppCommand<TestCommand>();
        CommandUtils.AddCommand(gameState, __result, command, includeUnavailable);
    }
    */