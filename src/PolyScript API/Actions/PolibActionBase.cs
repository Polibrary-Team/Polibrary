using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine.ResourceManagement.Util;

namespace Polibrary;
public class PolibActionBase : ActionBase
{
	public PolibActionBase(IntPtr ptr) : base(ptr) {}
	public PolibActionBase() {}

	public PolibActionBase(byte playerId) 
    : base(playerId)
	{
		base.PlayerId = playerId;
	}
	
	public override bool IsValid(GameState state)
    {
        return true;
    }

    public override ActionType GetActionType()
    {
        return EnumCache<ActionType>.GetType("polibactionbase");
    }
    
	public override void Execute(GameState state)
    {
        
    }

    public override void Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        base.Serialize(writer, version);
    }

    public override void Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    {
        base.Deserialize(reader, version);
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


/* Example: 
public class TestAction : PolibActionBase
{
    public int ExampleValue;
	public TestAction(IntPtr ptr) : base(ptr) {}
	public TestAction() {}

	public TestAction(byte playerId, int val) 
    : base(playerId)
	{
		base.PlayerId = playerId;
        this.ExampleValue = val;
	}
	
	public override bool IsValid(GameState state)
    {
        return true;
    }

    public override ActionType GetActionType()
    {
        return EnumCache<ActionType>.GetType("testaction");
    }
    
	public override void Execute(GameState state)
	{
		Main.modLogger.LogInfo($"hell yeah! value is {this.ExampleValue}");
	}

    public override void Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        base.Serialize(writer, version); //this line is important btw
        writer.Write(ExampleValue);
    }

    public override void Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    {
        base.Deserialize(reader, version); //leave this line in
        ExampleValue = reader.ReadInt32();
    }

	public override string ToString()
	{
		return string.Format("{0} (PlayerId: {1})", new object[]
		{
			base.GetType(),
            base.PlayerId
		});
	}
}*/