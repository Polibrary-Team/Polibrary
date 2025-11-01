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
using Il2CppSystem.Linq.Expressions;


namespace Polibrary;


public class Action
{
    private static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
    }

    //scrapping the basic action, so only the pScript version will be made

    public static string[] lines;
    public static object[] variables;



    public static void Execute()
    {
        foreach (string line in lines)
        {
            ReadLine(line);
        }
    }

    static void ReadLine(string line)
    {
        string[] commandAndParams;

        if (line.Contains(":"))
        {
            commandAndParams = line.Split(":");
        }
        else
        {
            commandAndParams = line.Split("=");
        }

        string command = commandAndParams[0].Trim();
        string[] parameters = commandAndParams[1].Split(" ");

        RunFunction(command, parameters);
    }

    static void RunFunction(string command, string[] parameters)
    {
        switch (command)
        {
            case "set":
                SetVariable(parameters[0], parameters[1], parameters[2]);
                break;
            case "log":
                LogMessage(parameters[0]);
                break;
        }
    }
    
    static void SetVariable(string varName, string obj, string value)
    {
        
    }
    static void LogMessage(string msg)
    {
        modLogger.LogInfo(msg);
    }
}