using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

[ProtoContract]
public class PlacePatternMessage
{
    [ProtoMember(1)]
    public List<int> BlockIds { get; set; }

    [ProtoMember(2)]
    public List<SerializedBlockPos> Positions { get; set; }

    [ProtoMember(3)]
    public string PlayerId { get; set; }

    [ProtoMember(4)]
    public Dictionary<string, int> RequiredPatterns { get; set; }

    [ProtoMember(5)]
    public List<SerializedBlockPos> AutoConnectPositions { get; set; }

    public PlacePatternMessage()
    {
        BlockIds = new List<int>();
        Positions = new List<SerializedBlockPos>();
        PlayerId = "";
        RequiredPatterns = new Dictionary<string, int>();
        AutoConnectPositions = new List<SerializedBlockPos>();
    }

    public void AddBlock(int blockId, BlockPos pos, bool shouldAutoConnect = false)
    {
        BlockIds.Add(blockId);
        Positions.Add(new SerializedBlockPos(pos));

        if (shouldAutoConnect)
        {
            // Call the special helper method for adaptive blocks
            AutoConnectPositions.Add(new SerializedBlockPos(pos));
        }
    }
}

[ProtoContract]
public class SerializedBlockPos
{
    [ProtoMember(1)]
    public int X { get; set; }

    [ProtoMember(2)]
    public int Y { get; set; }

    [ProtoMember(3)]
    public int Z { get; set; }

    public SerializedBlockPos()
    {
    }

    public SerializedBlockPos(BlockPos pos)
    {
        X = pos.X;
        Y = pos.Y;
        Z = pos.Z;
    }

    public BlockPos ToBlockPos()
    {
        return new BlockPos(X, Y, Z);
    }
}