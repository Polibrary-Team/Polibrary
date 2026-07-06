using Polytopia.Data;
using PolytopiaBackendBase.Common;
using Polibrary;
using System.Reflection;
using Il2CppSystem.Xml;

public class PolibAction : PolibActionBase
{
    public List<Variable> Variables;
    public string[] lines;
	public PolibAction(IntPtr ptr) : base(ptr) {}
	public PolibAction() {}
	
	public override bool IsValid(GameState state)
    {
        return true;
    }

    public override ActionType GetActionType()
    {
        return EnumCache<ActionType>.GetType("polibaction");
    }
    
	public override void Execute(GameState state)
	{
		var methods = ScriptManager.Read(lines, typeof(PolibAction));

        foreach (MethodCall call in methods)
        {
            List<object> parameters = call.Params;

            ParameterInfo[] expectedParams = call.MethodInfo.GetParameters();

            foreach (object parameter in parameters)
            {
                if (parameter is Variable)
                {
                    Variable v = (Variable)parameter;
                    v = ScriptManager.FindFirstVariableByName(v.Name, Variables);

                    if (v == null)
                    {
                        Main.modLogger.LogError($"Couldn't find variable by name '{v.Name}' in line {methods.LastIndexOf(call)}");
                        return;
                    }

                    
                }
            }

            call.MethodInfo.Invoke(this, parameters.ToArray());
        }
	}

    public override void Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        writer.Write(PlayerId);
    }

    public override void Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    {
        PlayerId = reader.ReadByte();
    }

	public override string ToString()
	{
		return string.Format("{0} (PlayerId: {1})", new object[]
		{
			base.GetType(),
            base.PlayerId
		});
	}
}