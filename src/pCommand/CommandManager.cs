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
    }

    static Dictionary<CommandType, Type> pCommandMapping = new();

    public static void RegisterCommand(string commandType, Type type)
    {
        if (EnumCache<CommandType>.TryGetType(commandType, out var cType))
        RegisterCommand(cType, type);

        else
        Main.modLogger.LogError($"Failed to register command '{commandType}'.");
    }

    public static void RegisterCommand(CommandType commandType, Type type)
    {
        pCommandMapping[commandType] = type;
        Main.modLogger.LogInfo($"Registered command '{commandType}' as a command");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.pCommands.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                EnumCache<CommandType>.AddMapping(token.Path.Split('.').Last(), (CommandType)PolyMod.Registry.autoidx);
                EnumCache<CommandType>.AddMapping(token.Path.Split('.').Last(), (CommandType)PolyMod.Registry.autoidx);
                Main.modLogger.LogInfo($"Added command mapping '{token.Path.Split('.').Last()}', id: {PolyMod.Registry.autoidx}");
                PolyMod.Registry.autoidx++;

                pCommandMapping[EnumCache<CommandType>.GetType(token.Path.Split('.').Last())] = typeof(PolibCommandBase);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.GetCommand))]
    public static bool GameState_GetCommand(ref CommandBase  __result, CommandType type) {
        Main.modLogger.LogInfo("GameState_GetCommand");
        Main.modLogger.LogInfo(type);

        if (pCommandMapping.TryGetValue(type, out Type commandClass))
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
    public static void CommandBase_Execute(ref CommandBase  __instance, GameState state)
    {
        if (pCommandMapping.TryGetValue(__instance.GetCommandType(), out Type commandClass))
        {
            PolibCommandBase command = __instance.Cast<PolibCommandBase>();
            command.ExecuteNew(state);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.Serialize))]
    public static void CommandBase_Execute(ref CommandBase  __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        if(pCommandMapping.TryGetValue(__instance.GetCommandType(), out Type commandClass))
        {
            PolibCommandBase command = __instance.Cast<PolibCommandBase>();
            command.SerializeNew(writer, version);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.Deserialize))]
    public static void CommandBase_Deserialize(ref CommandBase  __instance, Il2CppSystem.IO.BinaryReader reader, int version)
    {
        if(pCommandMapping.TryGetValue(__instance.GetCommandType(), out Type commandClass))
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




    //TESTING
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void REGISTER_TEST(GameLogicData __instance, JObject rootObject)
    {
        RegisterCommand("testcommand", typeof(TestCommand));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetUnitActions))]
	public static void GetUnitActions(ref Il2CppSystem.Collections.Generic.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable)
    {
        TestCommand command = (TestCommand)Il2CppSystem.Activator.CreateInstance(WrapType<TestCommand>());
        CommandUtils.AddCommand(gameState, __result, command, includeUnavailable);
    }*/
}