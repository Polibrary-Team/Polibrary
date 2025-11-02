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
    public static Dictionary<string, object> variables = new Dictionary<string, object>(); 



    public static void Execute()
    {
        foreach (string line in lines)
        {
            ReadLine(line);
        }
    }

    static void ReadLine(string line)
    {
        string[] commandAndParams; //pl. set:szam int 1

        commandAndParams = line.Split(":"); //set / szam int 1
        

        string command = commandAndParams[0]; //set
        string[] parameters = commandAndParams[1].Split(" "); //szam / int / 1

        RunFunction(command, parameters);
    }

    static void RunFunction(string command, string[] parameters)
    {
        switch (command)
        {
            case "set": //sets a variable
                SetVariable(parameters[0], parameters[1], parameters[2]);
                break;
            case "log": //logs a string
                LogMessage(parameters[0]);
                break;
        }
    }
    
    static void SetVariable(string varName, string obj, string value)
    {
        object valueObj = null;
        switch (obj)
        {
            case "int": //0
                if (int.TryParse(value, out var parsedInt))
                {
                    valueObj = parsedInt;
                }
                else LogError("SetVariable", "Invalid int format. Correct format: 0 . eg. set:var int 5");
                break;
            
            case "string": //iamtext //this need redo as it doesnt allow for spaces in the string (would break parsing)
                valueObj = value;
                break;
            
            case "wcoords": //0;0
                string[] strings = value.Split(';');
                WorldCoordinates wcoords = new WorldCoordinates(0, 0);

                if (int.TryParse(strings[0], out int X) && int.TryParse(strings[1], out int Y))
                {
                    wcoords.X = X;
                    wcoords.Y = Y;
                    valueObj = wcoords;
                }
                else LogError("SetVariable", "Invalid wcoords format. Correct format: 0;0 . eg. set:var wcoords 0;0");
                
                break;
            
            case "unitType":
                if (EnumCache<UnitData.Type>.TryGetType(value, out var enumValue))
                {
                    valueObj = enumValue;
                }
                else LogError("SetVariable", $"Couldn't find {value} unit. Check spelling or idk.");
                break;
        }
        variables[varName] = valueObj;
    }
    static void LogMessage(string msg)
    {
        modLogger.LogInfo(msg);
    }
    static void LogError(string origin, string msg)
    {
        modLogger.LogError($"Error from {origin}: " + msg);
    }
}