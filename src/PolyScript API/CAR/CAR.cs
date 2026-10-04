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
using Il2CppSystem.Runtime.InteropServices;

namespace Polibrary.PolyScript;

public static class CAR
{
    static Dictionary<ActionType, Type> ActionMapping = new();
    static Dictionary<Type, ActionType> ActionReverseMapping = new();
    static Dictionary<CommandType, Type> CommandMapping = new();
    static Dictionary<Type, CommandType> CommandReverseMapping = new();
    static Dictionary<ActionType, Type> ReactionMapping = new();
    static Dictionary<Type, ActionType> ReactionReverseMapping = new();

    #region Utils

    /// <summary>
    /// Register an action into Polibrary.
    /// </summary>
    /// <param name="actionType">The ActionType in the JSON.</param>
    public static void RegisterAction(Type type, string actionType)
    {
        if (EnumCache<ActionType>.TryGetType(actionType, out var aType))
        RegisterAction(type, aType);

        else
        Main.modLogger.LogError($"Failed to register action '{actionType}'. Enum isn't valid. Check spelling.");
    }

    /// <summary>
    /// Register an action into Polibrary. The string overload is preferred for usage.
    /// </summary>
    /// <param name="actionType">The EnumCached ActionType.</param>
    public static void RegisterAction(Type type, ActionType actionType)
    {
        ActionMapping[actionType] = type;
        ActionReverseMapping[type] = actionType;
        WrapTypeReflection(type);
        Main.modLogger.LogInfo($"Registered action '{actionType}' as an action.");
    }

    /// <summary>
    /// Register a command into Polibrary.
    /// </summary>
    /// <param name="commandType">The CommandType in the JSON.</param>
    public static void RegisterCommand(Type type, string commandType)
    {
        if (EnumCache<CommandType>.TryGetType(commandType, out var cType))
        RegisterCommand(type, cType);

        else
        Main.modLogger.LogError($"Failed to register command '{commandType}'. Enum isn't valid. Check spelling.");
    }

    /// <summary>
    /// Register a command into Polibrary. The string overload is preferred for usage.
    /// </summary>
    /// <param name="commandType">The EnumCached CommandType.</param>
    public static void RegisterCommand(Type type, CommandType commandType)
    {
        CommandMapping[commandType] = type;
        CommandReverseMapping[type] = commandType;
        WrapTypeReflection(type);
        Main.modLogger.LogInfo($"Registered command '{commandType}' as a command.");
    }

    /// <summary>
    /// Assign a reaction to an action.
    /// </summary>
    /// <param name="actionType">The ActionType in the JSON.</param>
    public static void AssignReaction(Type type, string actionType)
    {
        if (EnumCache<ActionType>.TryGetType(actionType, out var aType))
        AssignReaction(type, aType);

        else
        Main.modLogger.LogError($"Failed to assign reaction to '{actionType}'. Enum isn't valid. Check spelling.");
    }

    /// <summary>
    /// Assign a reaction to an action. The string overload is preferred for usage.
    /// </summary>
    /// <param name="actionType">The EnumCached ActionType.</param>
    public static void AssignReaction(Type type, ActionType actionType)
    {
        ReactionMapping[actionType] = type;
        ReactionReverseMapping[type] = actionType;
        WrapTypeReflection(type);
        Main.modLogger.LogInfo($"Assigned '{type}' reaction to '{actionType}' action.");
    }

    public static void New<T>(out T action) where T : class
    {
        Il2CppSystem.Object a = Il2CppSystem.Activator.CreateInstance(WrapType<T>());
        action = (T)(object)a;
    }

    internal static Il2CppSystem.Type WrapTypeReflection(Type type)
    {
        MethodInfo wrapMethod = typeof(CAR).GetMethod(nameof(WrapType), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(type);

        return (Il2CppSystem.Type)wrapMethod.Invoke(null, null);
    }

    internal static Il2CppSystem.Type WrapType<T>() where T : class
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return Il2CppType.From(typeof(T));
    }

    public static Type ByName(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse())
        {
            var tt = assembly.GetType(name);
            if (tt != null)
            {
                return tt;
            }
            
            //fallback. slower than the above one but easier to use. 
            // does fuck shit up with ambiguity so thats why we need the above one, so you can specify namespaces and shit
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var t in types)
            {
                if (t.Name == name)
                {
                    return t;
                }
            }
        }

        return null;
    }


    #endregion

    #region Patches

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.commandType.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                string tokenName = token.Path.Split('.').Last();
                EnumCache<CommandType>.AddMapping(tokenName, (CommandType)PolyMod.Registry.autoidx);
                EnumCache<CommandType>.AddMapping(tokenName, (CommandType)PolyMod.Registry.autoidx);
                Main.modLogger.LogInfo($"Added command mapping '{token.Path.Split('.').Last()}', id: {PolyMod.Registry.autoidx}");
                PolyMod.Registry.autoidx++;

                if (token["command"] != null)
                {
                    string commandName = token["command"].ToObject<string>();

                    Type commandType = ByName(commandName);

                    if (commandType != null)
                    {
                        RegisterCommand(commandType, tokenName);
                    }
                    else
                    {
                        Main.modLogger.LogError($"Can't find type \"{commandName}\".");
                        continue;
                    }
                }
                else
                {
                    Main.modLogger.LogInfo($"No command class name assigned for {tokenName}");
                }
            }
        }
        RegisterCommand(typeof(PolibCommandBase), "polibcommandbase");

        foreach (JToken jtoken in rootObject.SelectTokens("$.actionType.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                string tokenName = token.Path.Split('.').Last();
                EnumCache<ActionType>.AddMapping(tokenName, (ActionType)PolyMod.Registry.autoidx);
                EnumCache<ActionType>.AddMapping(tokenName, (ActionType)PolyMod.Registry.autoidx);
                Main.modLogger.LogInfo($"Added action mapping '{tokenName}', id: {PolyMod.Registry.autoidx}");

                if (token["action"] != null)
                {
                    string actionName = token["action"].ToObject<string>();

                    Type actionType = ByName(actionName);
                    if (actionType != null)
                    {
                        RegisterAction(actionType, tokenName);
                    }
                    else
                    {
                        Main.modLogger.LogError($"Can't find type \"{actionName}\".");
                        continue;
                    }
                    
                    if (token["reaction"] != null)
                    {
                        string reactionName = token["reaction"].ToObject<string>();

                        Type reactionType = ByName(reactionName);
                        if (reactionType != null)
                        {
                            AssignReaction(reactionType, tokenName);
                        }
                        else
                        {
                            Main.modLogger.LogError($"Can't find type \"{actionName}\".");
                            continue;
                        }
                    }
                }
                else
                {
                    Main.modLogger.LogInfo($"No action class name assigned for {tokenName}");
                }
                
                PolyMod.Registry.autoidx++;
            }
        }
        RegisterAction(typeof(PolibActionBase), "polibactionbase");        
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.GetAction))]
    private static bool GameState_GetAction(ref ActionBase  __result, ActionType type) 
    {
        if (ActionMapping.TryGetValue(type, out Type actionType))
        {
            __result = (ActionBase)Il2CppSystem.Activator.CreateInstance(WrapTypeReflection(actionType));
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.GetCommand))]
    private static bool GameState_GetCommand(ref CommandBase  __result, CommandType type) {
        if (CommandMapping.TryGetValue(type, out Type commandType))
        {
            __result = (CommandBase)Il2CppSystem.Activator.CreateInstance(WrapTypeReflection(commandType));
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.Id), MethodType.Getter)]
    private static void CommandBase_Id_get(ref CommandBase  __instance, ref string __result)
    {
        if (CommandReverseMapping.TryGetValue(__instance.GetType(), out var type))
        {
            __result = EnumCache<CommandType>.GetName(__instance.GetCommandType());
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIIconData), nameof(UIIconData.GetSprite))]
    public static void UIIconData_GetSprite(UIIconData __instance, ref Sprite __result, string id)
    {
        string id2 = id.Remove(0, 3);
        if (EnumCache<CommandType>.TryGetType(id2, out var type))
        {
            if (CommandMapping.Keys.Contains(type))
            {
                Sprite sprite = PolyMod.Registry.GetSprite(id2);
                if (sprite != null)
                {
                    __result = sprite;
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ReactionManager), nameof(ReactionManager.GetReaction))]
    private static bool ReactionManager_GetReaction(ref ReactionBase  __result, ActionBase action) 
    {
        if (ReactionMapping.TryGetValue(action.GetActionType(), out Type reactionType))
        {
            PolibReactionBase reactionBase = (PolibReactionBase)Il2CppSystem.Activator.CreateInstance(WrapTypeReflection(reactionType));
            reactionBase.actionProperty = action;
            __result = reactionBase;
            return false;
        }
        return true;
    }
    
    #endregion
}