using System.Reflection;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace Polibrary.Parsing;

// It is imperative that fields are called what the user calls them in patch.json
public class PolibImprovementData
{
    public ImprovementData.Type? type;
    public string builtOnSpecific = null;
    public string unblock = null;
    public string infoOverride = null;
    public int? defenceBoost = null;
    public int? freelanceImprovementDefenceBoost = null;
    public float? aiScore = null;
    public List<UnitAbility.Type> unitAbilityWhitelist = null;
    public List<UnitAbility.Type> unitAbilityBlacklist = null;
    public int? defenceBoost_Neutral = null;
    public List<UnitData.Type> unitWhitelist = null;
    public List<UnitData.Type> unitBlacklist = null;

    public PolibImprovementData()
    {
        type = null;
    }
}

public class PolibTribeData
{
    public TribeType? type;
    public string leaderName = null;
    public PolibTribeData()
    {
        type = null;
    }
}

public class PolibData
{

    /// <summary>
    /// Tries to get a specific field's data. Return true if successful, false otherwise.
    /// </summary>
    /// <typeparam name="T1">PolibData list type</typeparam>
    /// <typeparam name="T2">id type</typeparam>
    /// <typeparam name="T3">field value type</typeparam>
    /// <param name="list">Polibdata list</param>
    /// <param name="type">ID</param>
    /// <param name="fieldName">nameof(FieldName)</param>
    /// <param name="result">Field's value outted</param>
    /// <returns>Returns success value</returns>
    public static bool TryGetValue<T1, T2, T3>(List<T1> list, T2 type, string fieldName, out T3 result)
    {
        int index = FindData(list, type);
        if(index == -1)
        {
            result = default;
            return false;
        }
        object obj = list[index].GetType().GetField(fieldName).GetValue(list[index]);
        if(obj is T3 value && !EqualityComparer<T3>.Default.Equals(value, default(T3)))
        {
            result = value;
            return true;
        }

        result = default;
        return false;
    }



    /// <summary>
    /// Finds an entry based on "type" for a list of a specific PolibData
    /// </summary>
    /// <typeparam name="T1">Type of PolibData</typeparam>
    /// <typeparam name="T2">Type of PolibData.type</typeparam>
    /// <param name="list">List of PolibDatas to search in</param>
    /// <param name="type">The PolibData.type to search with</param>
    /// <returns>Returns -1 if not found, otherwise the index</returns>
    
    // ooh whats this?
    public static int FindData<T1, T2>(List<T1> list, T2 type)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];

            var field = item.GetType().GetField("type");
            if (field == null)
                continue;

            var value = field.GetValue(item);

            if (value is T2 typedValue && EqualityComparer<T2>.Default.Equals(typedValue, type))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Modifies a certain field of a certain class in a certain list.
    /// </summary>
    /// <typeparam name="T1">The listed PolibData type</typeparam>
    /// <typeparam name="T2">PolibData.type, of which we are looking at</typeparam>
    /// <param name="list"></param>
    /// <param name="fieldName"></param>
    /// <param name="idx"></param>
    /// <param name="newValue"></param>
    /// <returns></returns>
    public static bool OverrideField<T1, T2>(List<T1> list, string fieldName, int idx, T2 newValue)
    {
        var item = list[idx];
        var field = item.GetType().GetField(fieldName);
        if(field == null) return false;
        if(!field.FieldType.IsAssignableFrom(typeof(T2))) return false;
        field.SetValue(item, newValue);
        list[idx] = item;
        return true;
    }

}