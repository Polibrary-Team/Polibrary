using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;
using UnityEngine;

using Il2Gen = Il2CppSystem.Collections.Generic;
using pbb = PolytopiaBackendBase.Common;
using Polibrary.Parsing;
using Scriban;


namespace Polibrary.Parsing;

public static class ParseUtils
{
    
    public static void ParseToDictWithHandler<targetType, KT, VT, PDataType>(JObject token, string fieldName, List<PDataType> list, Func<PDataType> factory)
    where targetType : struct, System.IConvertible
    {
        if (token[fieldName] != null)
        {
            var jt = token[fieldName].TryCast<JObject>();
            if (jt != null)
            {
                Dictionary<KT, VT> dict = new Dictionary<KT, VT>();

                foreach (JProperty property in jt.Properties().ToList())
                {
                    VT value = property.Value.ToObject<VT>();
                    KT key = new JValue(property.Name).ToObject<KT>();
                    
                    dict[key] = value;
                }

                if (EnumCache<targetType>.TryGetType(token.Path.Split('.').Last(), out var type))
                {
                    int idx = PolibData.FindData<PDataType, targetType>(list, type);

                    if (idx == -1)
                    {
                        PDataType newone = factory();
                        list.Add(newone);
                        PolibData.OverrideField(list, "type", list.Count - 1, type);
                        idx = list.Count - 1;
                    }
                    PolibData.OverrideField(list, fieldName, idx, dict);
                }
            }
        }
    }
    public static void ParseWithHandler<targetType, T, PDataType>(JObject token, string fieldName, List<PDataType> list, Func<PDataType> factory)
    where targetType : struct, System.IConvertible
    {
        if (token[fieldName] != null)
        {
            T value = token[fieldName].ToObject<T>();
            if (EnumCache<targetType>.TryGetType(token.Path.Split('.').Last(), out var type))
            {
                int idx = PolibData.FindData<PDataType, targetType>(list, type);
                if (idx >= 0)
                {
                    PolibData.OverrideField<PDataType, T>(list, fieldName, idx, value);
                }
                else
                {
                    PDataType newone = factory();
                    list.Add(newone);
                    PolibData.OverrideField<PDataType, targetType>(list, "type", list.Count - 1, type);
                    PolibData.OverrideField<PDataType, T>(list, fieldName, list.Count - 1, value);
                }
                token.Remove(fieldName);

            }
        }
    }

    public static void ParseWithHandlerIntoArray<PDataType, targetType, listType>(JObject token, string fieldName, List<PDataType> list, Func<PDataType> factory) where targetType : struct, System.IConvertible where listType : struct, System.IConvertible
    {
        if (token != null)
        {
            if (EnumCache<targetType>.TryGetType(token.Path.Split('.').Last(), out var type))
            {
                int idx = PolibData.FindData(list, type);
                if (idx == -1)
                {
                    PDataType newone = factory();
                    list.Add(newone);
                    PolibData.OverrideField(list, "type", list.Count - 1, type);
                    idx = list.Count - 1;
                }
                if (token[fieldName] != null)
                {
                    PolibData.OverrideField(list, fieldName, idx, PolibUtils.ParseEnumsToSysList<listType>(token[fieldName]));
                }
            }
        }

    }
}