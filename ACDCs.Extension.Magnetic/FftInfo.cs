namespace ACDCs.Extension.Magnetic;

public class FftInfo
{
    public double Freq { get; }
    public double Value { get; }

    public FftInfo(double value, double freq)
    {
        Value = value;
        Freq = freq;
    }
}