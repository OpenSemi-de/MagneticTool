namespace ACDCs.Extension.Magnetic;

public class FftInfoPacket : List<FftInfo>
{
    public VectorAxis Axis { get; set; }

    public FftInfoPacket(VectorAxis axis)
    {
        Axis = axis;
    }
}