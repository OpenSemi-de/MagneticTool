using System.Numerics;

namespace MagneticTool;

public class MagneticSample
{
    public Vector3 Sample { get; set; }

    public DateTime Time { get; set; }

    public MagneticSample(DateTime time, Vector3 sample)
    {
        Time = time;
        Sample = sample;
    }
}