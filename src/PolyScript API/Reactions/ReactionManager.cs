using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Polytopia.Data;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;
using pbb = PolytopiaBackendBase.Common;
using System.Reflection;
using UnityEngine;
using Il2CppInterop.Runtime.Runtime;

using Il2Gen = Il2CppSystem.Collections.Generic;

namespace Polibrary;

public static class PolibReactionManager
{

    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(PolibReactionManager));
        ClassInjector.RegisterTypeInIl2Cpp<PolibActionBase>();
    }

    static Dictionary<ActionType, Type> ReactionMapping = new();
    static Dictionary<Type, ActionType> ReactionReverseMapping = new();

    #region Utils

    /// <summary>
    /// Assign a reaction to an action.
    /// </summary>
    /// <typeparam name="T">Must inherit from <see cref="PolibReactionBase"/>.</typeparam>
    /// <param name="actionType">The ActionType in the JSON.</param>
    public static void AssignReaction<T>(string actionType) where T : PolibReactionBase
    {
        if (EnumCache<ActionType>.TryGetType(actionType, out var aType))
        AssignReaction<T>(aType);

        else
        Main.modLogger.LogError($"Failed to assign reaction to '{actionType}'. Enum isn't valid. Check spelling.");
    }

    /// <summary>
    /// Assign a reaction to an action. The string overload is preferred for usage.
    /// </summary>
    /// <typeparam name="T">Must inherit from <see cref="PolibReactionBase"/>.</typeparam>
    /// <param name="actionType">The EnumCached ActionType.</param>
    public static void AssignReaction<T>(ActionType actionType) where T : PolibReactionBase
    {
        ReactionMapping[actionType] = typeof(T);
        ReactionReverseMapping[typeof(T)] = actionType;
        ClassInjector.RegisterTypeInIl2Cpp<T>();
        Main.modLogger.LogInfo($"Assigned '{typeof(T).GetType()}' reaction to '{actionType}' action.");
    }

    /// <summary>
    /// Create an Il2Cpp instance of a reaction.
    /// </summary>
    /// <typeparam name="T">Must inherit from <see cref="PolibReactionBase"/>.</typeparam>
    /// <returns>Returns a usable PolibActionBase instance.</returns>
    public static T MakeIl2CppReaction<T>() where T : PolibReactionBase
    {
        T action = (T)Il2CppSystem.Activator.CreateInstance(WrapType<T>());
        return action;
    }

    #endregion

    #region Patches

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ReactionManager), nameof(ReactionManager.GetReaction))]
    private static bool ReactionManager_GetReaction(ref ReactionBase  __result, ActionBase action) 
    {
        if (ReactionMapping.TryGetValue(action.GetActionType(), out System.Type actionClass))
        {
            MethodInfo wrapMethod = typeof(PolibReactionManager)
                .GetMethod(nameof(WrapType), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(actionClass);

            Il2CppSystem.Type il2cppType = (Il2CppSystem.Type)wrapMethod.Invoke(null, null);
            PolibReactionBase reactionBase = (PolibReactionBase)Il2CppSystem.Activator.CreateInstance(il2cppType);
            reactionBase.actionProperty = action;
            __result = reactionBase;
            return false;
        }
        return true;
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
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void REGISTER_TEST(GameLogicData __instance, JObject rootObject)
    {
        AssignReaction<TestReaction>("polibactionbase");
    }
*/