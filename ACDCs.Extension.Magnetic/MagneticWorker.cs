using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Numerics;

namespace ACDCs.Extension.Magnetic;

public static class CleanupWorker
{
    private static int _fftWindowSize = 128;
    private static bool _isRecording = false;
    private static ConcurrentQueue<MagneticSample> _samples = new();
    private static ConcurrentQueue<MagneticSample> _samplesBackup = new();
    private static Thread _thread = GetThread();

    public static int FftWindowSize
    {
        get => _fftWindowSize;
        set
        {
            UseMutex(() => { _fftWindowSize = value; });
        }
    }

    public static bool IsRecording
    {
        get
        {
            return _isRecording;
        }
        set
        {
            UseMutex(() => { _isRecording = value; });
        }
    }

    public static Mutex Mutex { get; set; } = new();

    public static ConcurrentQueue<MagneticSample> Samples
    {
        get => _samples;
        set
        {
            UseMutex(() => { _samples = value; });
        }
    }

    public static ConcurrentQueue<MagneticSample> SamplesBackup
    {
        get => _samplesBackup;
        set
        {
            UseMutex(() => { _samplesBackup = value; });
        }
    }

    private static void Cleanup_BackgroundTask()
    {
        while (true)
        {
            while (_samples.Count > FftWindowSize)
            {
                if (_samples.TryDequeue(out var backupSample))
                {
                    if (IsRecording)
                    {
                        _samplesBackup.Enqueue(backupSample);
                    }
                }
            }

            Thread.Sleep(100);
        }
    }

    private static Thread GetThread()
    {
        Thread thread = new(Cleanup_BackgroundTask)
        {
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
        return thread;
    }

    private static void UseMutex(Action action)
    {
        Mutex.WaitOne();
        action.Invoke();
        Mutex.ReleaseMutex();
    }
}

public class MagneticWorker
{
    private readonly ConcurrentQueue<MagneticSample> _samples = new();
    private readonly ConcurrentQueue<MagneticSample> _samplesBackup = new();
    private int _fftWindowSize = 256;
    private bool _isRecording;

    public int FftWindowSize
    {
        get => _fftWindowSize;
        set
        {
            _fftWindowSize = value;
            CleanupWorker.FftWindowSize = value;
        }
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

    public MagneticWorker()
    {
        CleanupWorker.Samples = _samples;
        CleanupWorker.SamplesBackup = _samplesBackup;
    }

    public async Task AddSample(Vector3 vector)
    {
        DateTime now = DateTime.UtcNow;
        MagneticSample sample = new(now, vector);
        await Task.Run(() => _samples.Enqueue(sample));
    }

    public async Task GetFft(VectorAxis axis, IEnumerable<FftInfo> targetCollection)
    {
        var signal = await GetLastSamples(axis);
        if (signal.Length < FftWindowSize)
        {
            return;
        }

        const int sampleRate = 100;
        var psd = FftSharp.Transform.FFTpower(signal);
        var freq = FftSharp.Transform.FFTfreq(sampleRate, psd.Length);
        if (targetCollection is ObservableCollection<FftInfo> values)
        {
            values.Clear();
            var x = 0;
            foreach (var value in psd)
            {
                FftInfo info = new(value, freq[x]);
                values.Add(info);
                x++;
            }
        }
    }

    public async Task<double[]> GetLastSamples(VectorAxis axis, int windowSize = 256)
    {
        var samplesList = _samples.AsParallel()
            .OrderByDescending(sample => sample.Time)
            .Take(FftWindowSize)
            .Select(sampleRecord => sampleRecord.Sample)
            .Select(sample => Convert.ToDouble(axis switch
            {
                VectorAxis.X => sample.X,
                VectorAxis.Y => sample.Y,
                VectorAxis.Z => sample.Z,
                _ => 0
            })).ToArray();

        return await Task.FromResult(samplesList);
    }
}

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