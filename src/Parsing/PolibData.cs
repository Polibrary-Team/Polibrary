using Polytopia.Data;

namespace Polibrary.Parsing;

// It is imperative that fields are called what the user calls them in patch.json
//idk what imperative means but i totally agree, have been doing it since cityrewards
public class PolibImprovementData
{
    public ImprovementData.Type? type;
    public string builtOnSpecific = null;
    public string unblock = null;
    public int? defenceBoost = null;
    public int? freelanceImprovementDefenceBoost = null; // didnt even know we had something like this //honestly i completely forgot aswell lmao i think bananique asked for it once
    public float? aiScore = null;
    public List<UnitAbility.Type> unitAbilityWhitelist = null;
    public List<UnitAbility.Type> unitAbilityBlacklist = null;
    public int? defenceBoost_Neutral = null; //dpne
    public List<UnitData.Type> unitWhitelist = null;
    public List<UnitData.Type> unitBlacklist = null;

    public PolibImprovementData()
    {
        type = null;
    }
}

public class PolibData
{
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