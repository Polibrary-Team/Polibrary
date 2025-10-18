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


public class Action
{
    private static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
    }

    public enum ActionMode //HOW its executed
    {
        Single,
        Radial,
        //stb.
    }
    public enum ActionType //WHAT it executes
    {
        DealDamage,
        ApplyEffect,
        Heal,
        //stb.
    }

    #region Variables

    public static MapData mapData;
    public static WorldCoordinates location;
    public static WorldCoordinates target;
    public static ActionMode actionMode;
    public static ActionType actionType;
    public static int value;

    
    #endregion

    public static void Execute()
    {

    }
}