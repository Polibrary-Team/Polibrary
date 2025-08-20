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

public static class Parse
{
    private static ManualLogSource LogMan1997;
    public static void Load(ManualLogSource logger)
    {
        LogMan1997 = logger;
        LogMan1997.LogInfo("KRIS DON'T [Get The Heebie Jeebies] I'M STILL HERE");
        LogMan1997.LogInfo("BUT NOW, I'M NOT [Calling] THE [[BIG SHOTS]] ANYMORE!!");
        LogMan1997.LogInfo("[They] HAVE [Demoted] ME TO PERFORM [Medium] SHOTS??? I'M NO LONGER [XXL Family Size with 20% more value]!!!");
    }
}