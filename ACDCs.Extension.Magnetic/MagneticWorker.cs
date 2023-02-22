using System.Collections.Concurrent;
using System.Numerics;

namespace ACDCs.Extension.Magnetic;

public class MagneticWorker
{
    private readonly ConcurrentQueue<MagneticSample> _samples = new();
    private readonly ConcurrentQueue<MagneticSample> _samplesBackup = new();
    private int _fftWindowSize = 256;
    private FftWorker _fftWorker;
    private bool _isRecording;

    public int FftWindowSize
    {
        get => _fftWindowSize;
        set
        {
            _fftWindowSize = value;
            CleanupWorker.FftWindowSize = value;
            _fftWorker.FftWindowSize = value;
        }
    }

    public FftWorker FftWorker
    {
        get => _fftWorker;
        set => _fftWorker = value;
    }

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            _isRecording = value;
            CleanupWorker.IsRecording = value;
        }
    }

    public int SampleBackupCount => _samplesBackup.Count;
    public int SampleCacheCount => _samples.Count;
    public int SampleCount => _samples.Count + _samplesBackup.Count;

    public MagneticWorker(FftWorker fftWorker)
    {
        CleanupWorker.Samples = _samples;
        CleanupWorker.SamplesBackup = _samplesBackup;
        FftWorker = fftWorker;
        FftWorker.Samples = _samples;
    }

    public void AddSample(Vector3 vector)
    {
        DateTime now = DateTime.UtcNow;
        MagneticSample sample = new(now, vector);
        _samples.Enqueue(sample);
    }
}