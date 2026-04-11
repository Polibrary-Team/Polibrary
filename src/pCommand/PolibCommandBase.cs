using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace Polibrary;
public class PolibCommandBase : CommandBase
{
	public PolibCommandBase(IntPtr ptr) : base(ptr) {}
	public PolibCommandBase() {}

	public PolibCommandBase(byte playerId) 
    : base(playerId)
	{

	}

	public override bool IsValid(GameState state, out string validationError)
	{
        validationError = "";
		return true;
	}

	public override CommandType GetCommandType()
	{
		CommandType type = EnumCache<CommandType>.GetType("polib_commandbase");
		return type;
	}

	public virtual void ExecuteNew(GameState state)
	{
        Main.modLogger.LogInfo("PolibCommandBase.ExecuteNew");
	}

	public override bool ShouldAskForConfirmation()
	{
		return false;
	}

	public void SerializeNew(Il2CppSystem.IO.BinaryWriter writer, int version)
	{
        /*
		writer.Write((ushort)this.Tribe);
        writer.Write((ushort)this.Skin);
        writer.Write((ushort)this.Improvement);
        writer.Write((ushort)this.Resource);
        writer.Write((ushort)this.Terrain);
        writer.Write((ushort)this.TileEffect);
		this.Coordinates.Serialize(writer, version);*/
	}

	public void DeserializeNew(Il2CppSystem.IO.BinaryReader reader, int version)
	{
        /*
		this.Tribe = (TribeType)reader.ReadUInt16();
        this.Skin = (SkinType)reader.ReadUInt16();
        this.Improvement = (ImprovementData.Type)reader.ReadUInt16();
        this.Resource = (ResourceData.Type)reader.ReadUInt16();
        this.Terrain = (TerrainData.Type)reader.ReadUInt16();
        this.TileEffect = (TileData.EffectType)reader.ReadUInt16();
		this.Coordinates = new WorldCoordinates(reader, version);*/
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


public class TestCommand : PolibCommandBase
{
	public TestCommand(IntPtr ptr) : base(ptr) {}
	public TestCommand() {}

	public int Num {get; set;}

	public TestCommand(byte playerId, int num) 
    : base(playerId)
	{
		Num = num;
	}

    public override void ExecuteNew(GameState state)
    {
        base.ExecuteNew(state);
		Main.modLogger.LogInfo("TestCommand.ExecuteNew");
    }
}