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
            case "alert":
                Alert(parameters[0]);
                break;
            case "setimp":
                SetImprovement(parameters[0],parameters[1],parameters[2]);
                break;
        }
    }

    #region commands
    
    private void SetVariable(string varName, string obj, string value)
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

            case "string": //helloworld! (no spaces allowed) OR §hello world!§ (spaces allowed)
                valueObj = value;
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
        if (IsVariable(msg, out string var))
        {
            modLogger.LogInfo(var);
        }
        else
        {
            modLogger.LogInfo(msg);
        }
    }

    private void Alert(string msg) //idk how tf this is done in the main game I'll check later (should be as elyrion sanctuary shit)
    {
        
    }

    private void SetImprovement(string swcoords, string simprovement, string sdeductCost)
    {
        WorldCoordinates wcoords;
        ImprovementData.Type imp;
        bool deductCost;
        
        if (IsVariable<WorldCoordinates>(swcoords, out wcoords)){}
        else wcoords = ParseWcoords(swcoords);

        if (IsVariable<ImprovementData.Type>(simprovement, out imp)){}
        else imp = ParseImprovementDataType(simprovement);

        if (IsVariable<bool>(sdeductCost, out deductCost)){}
        else deductCost = ParseBool(sdeductCost);

        byte playerId = GameManager.GameState.CurrentPlayer;
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

    private WorldCoordinates ParseWcoords(string value)
    {
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