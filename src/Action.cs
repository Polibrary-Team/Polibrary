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
using UnityEngine.Rendering.Universal;
using LibCpp2IL.Elf;


namespace Polibrary;


class pAction
{
    private static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
    }

    //scrapping the basic action, so only the pScript version will be made

    public string[] lines;
    private Dictionary<string, object> variables = new Dictionary<string, object>(); 



    public void Execute()
    {
        foreach (string line in this.lines)
        {
            ReadLine(line);
        }
    }

    private void ReadLine(string line)
    {
        string[] commandAndParams; //pl. set:szam int 1

        commandAndParams = line.Split(":"); //set / szam int 1
        

        string command = commandAndParams[0]; //set
        string[] rawparameters = commandAndParams[1].Split(" "); //szam / int / 1

        bool isString = false;

        List<string> untrimmedParams = new List<string>();



        foreach (string s in rawparameters)
        {
            if (s.Length == 0) continue;

            if (isString)
            {
                untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + s;

                if (s[s.Length - 1] == '§')
                {
                    isString = false;
                }
                else
                {
                    untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + " ";
                }
            }
            else
            {
                untrimmedParams.Add(s);

                if (s[0] == '§' && s[s.Length - 1] != '§')
                {
                    isString = true;
                    untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + " ";
                }
            }
        }

        List<string> parameters = new List<string>();
        foreach (string s in untrimmedParams)
        {
            parameters.Add(s.Trim('§'));
        }

        RunFunction(command, parameters);
    }

    private void RunFunction(string command, List<string> parameters)
    {
        switch (command)
        {
            case "set": //sets a variable
                SetVariable(parameters[0], parameters[1], parameters[2]);
                break;
            case "log": //logs a string
                LogMessage(parameters[0]);
                break;
            case "alert": //popup alert in game (not done yet)
                Alert(parameters[0]);
                break;
            case "setimprovement": //sets an improvement on a tile
                SetImprovement(parameters[0],parameters[1],parameters[2]);
                break;
            case "setimprovements": //sets improvements on tiles of an area
                SetImprovements(parameters[0],parameters[1],parameters[2]);
                break;
            case "getradius": //gets an area around an origin and returns it to a variable
                GetRadiusFromOrigin(parameters[0],parameters[1],parameters[2],parameters[3]);
                break;
        }
    }

    #region generic
    
    private void SetVariable(string varName, string obj, string value)
    {
        object valueObj = null;
        switch (obj)
        {
            case "int": //0
                valueObj = ParseInt(value);
                break;

            case "string": //helloworld! (no spaces allowed) OR §hello world!§ (spaces allowed)
                valueObj = ParseString(value);
                break;

            case "bool":
                valueObj = ParseBool(value);
                break;

            case "wcoords": //0;0
                valueObj = ParseWcoords(value);
                break;
            
            case "wcoords[]": //0;0|0;0|0;0|0;0
                valueObj = ParseWcoordsArray(value);
                break;

            case "unitType":
                valueObj = ParseUnitDataType(value);
                break;

            case "improvementType":
                valueObj = ParseImprovementDataType(value);
                break;
        }
        variables['@' + varName] = valueObj;
    }

    private void LogMessage(string msg)
    {
        modLogger.LogInfo(ParseString(msg));
    }

    private void Alert(string msg) //idk how tf this is done in the main game I'll check later (should be as elyrion sanctuary shit)
    {
        
    }

    #endregion
    #region functions

    private void GetRadiusFromOrigin(string variable, string sorigin, string sradius, string sallowCenter)
    {
        if (!IsVariable<WorldCoordinates[]>(variable, out var obj))
        {
            LogError("GetRadiusFromOrigin", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords[].");
            return;
        }
        
        WorldCoordinates origin = ParseWcoords(sorigin);
        int radius = ParseInt(sradius);
        bool allowCenter = ParseBool(sallowCenter);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;

        variables[variable] = map.GetArea(origin, radius, true, allowCenter); //who in the ever living fuck would want to exclude diagonals??

    }

    #endregion
    #region commands

    private void SetImprovement(string swcoords, string simprovement, string sdeductCost)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);;
        ImprovementData.Type imp = ParseImprovementDataType(simprovement);
        bool deductCost = ParseBool(sdeductCost);
        

        byte playerId = GameManager.GameState.CurrentPlayer;
        GameManager.GameState.ActionStack.Add(new BuildAction(playerId, imp, wcoords, deductCost));
    }

    private void SetImprovements(string swcoordsarray, string simprovement, string sdeductCost)
    {
        WorldCoordinates[] wcoordsarray = ParseWcoordsArray(swcoordsarray);;
        ImprovementData.Type imp = ParseImprovementDataType(simprovement);
        bool deductCost = ParseBool(sdeductCost);
        byte playerId = GameManager.GameState.CurrentPlayer;

        foreach (WorldCoordinates wcoords in wcoordsarray)
        GameManager.GameState.ActionStack.Add(new BuildAction(playerId, imp, wcoords, deductCost));
    }
    
    #endregion












    #region utils

    private void LogError(string origin, string msg)
    {
        modLogger.LogError($"Polibrary: Error from {origin}: " + msg);
    }

    private bool IsVariable<T>(string s, out T obj)
    {
        if (s[0] == 'ص')
        {
            s = s.Replace('ص', '@'); //you happy? huh? YOU GOT YOUR ARABIC LETTER ARE YOU SATISFIED?
        }
        if (s[0] == '@')
        {

            if (variables.TryGetValue(s, out var value))
            {
                obj = (T)value;
                if (obj != null)
                {
                    return true;
                }
            }
        }

        

        obj = default;
        return false;
    }

    private int ParseInt(string value)
    {
        if (IsVariable<int>(value, out var obj))
        {
            return obj;
        }
        if (int.TryParse(value, out var parsedInt))
        {
            return parsedInt;
        }
        else
        {
            LogError("ParseInt", "Invalid int format. Correct format: 0 . eg. set:var int 5");
            return 0;
        }
    }

    private string ParseString(string value)
    {
        if (IsVariable<string>(value, out var obj))
        {
            return obj;
        }
        return value;
    }

    private WorldCoordinates ParseWcoords(string value)
    {
        if (IsVariable<WorldCoordinates>(value, out var obj))
        {
            return obj;
        }
        string[] strings = value.Split(';');
        WorldCoordinates wcoords = new WorldCoordinates(0, 0);

        if (int.TryParse(strings[0], out int X) && int.TryParse(strings[1], out int Y))
        {
            wcoords.X = X;
            wcoords.Y = Y;
            return wcoords;
        }
        else
        {
            LogError("ParseWcoords", "Invalid wcoords format. Correct format: 0;0 . eg. set:var wcoords 0;0");
            return default;
        }
    }

    private WorldCoordinates[] ParseWcoordsArray(string value)
    {
        if (IsVariable<WorldCoordinates[]>(value, out var obj))
        {
            return obj;
        }
        string[] splitValues = value.Split('|');
        WorldCoordinates[] wcoordsarray = new WorldCoordinates[splitValues.Length];

        int i = 0;
        foreach (string newValue in splitValues)
        {
            string[] strings = newValue.Split(';');
            WorldCoordinates wcoords = new WorldCoordinates(0, 0);

            if (int.TryParse(strings[0], out int X) && int.TryParse(strings[1], out int Y))
            {
                wcoords.X = X;
                wcoords.Y = Y;
                wcoordsarray[i] = wcoords;
            }
            else
            {
                LogError("ParseWcoordsArray", "Invalid wcoords format. Correct format: 0;0 . eg. set:var wcoords 0;0");
            }
            i++;
        }
        return wcoordsarray;
    }

    private bool ParseBool(string value)
    {
        if (IsVariable<bool>(value, out var obj))
        {
            return obj;
        }
        if (bool.TryParse(value, out var parsedBool))
        {
            return parsedBool;
        }
        else
        {
            LogError("ParseBool", "Invalid bool format. Correct format: true/false . eg. set:var bool false");
            return true;
        }
    }

    private UnitData.Type ParseUnitDataType(string value)
    {
        if (IsVariable<UnitData.Type>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<UnitData.Type>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseUnitDataType", $"Couldn't find {value} unit. Check spelling or idk.");
            return default;
        }
    }

    private ImprovementData.Type ParseImprovementDataType(string value)
    {
        if (IsVariable<ImprovementData.Type>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<ImprovementData.Type>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseImprovementDataType", $"Couldn't find {value} improvement. Check spelling or idk.");
            return default;
        }
    }
    #endregion
}