using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Numerics;

namespace MagneticTool;

public class MagneticWorker
{
    private readonly ConcurrentQueue<MagneticSample> _samples = new();
    private readonly ConcurrentQueue<MagneticSample> _samplesBackup = new();
    private Thread _thread;

    public bool IsRecording { get; set; }
    public int SampleBackupCount => _samplesBackup.Count;
    public int SampleCacheCount => _samples.Count;
    public int SampleCount => _samples.Count + _samplesBackup.Count;

    public MagneticWorker()
    {
    }

    public async Task AddSample(Vector3 vector)
    {
        if (_thread == null)
        {
            _thread = new Thread(BackgroundTask)
            {
                Name = "My Thread",
                Priority = ThreadPriority.BelowNormal
            };
            _thread.Start();
        }

        DateTime now = DateTime.UtcNow;
        MagneticSample sample = new(now, vector);
        await Task.Run(() => _samples.Enqueue(sample));
    }

    public async Task GetFft(VectorAxis axis, IEnumerable<FftInfo> targetCollection)
    {
        var signal = await GetLastFftSamples(axis);
        if (signal.Length < 256)
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

    public async Task<double[]> GetLastFftSamples(VectorAxis axis, int windowSize = 256)
    {
        var samplesList = _samples.AsParallel()
            .OrderByDescending(sample => sample.Time)
            .Take(windowSize)
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

    private void BackgroundTask()
    {
        Task.Run(() =>
        {
            while (true)
            {
                while (_samples.Count > 512)
                {
                    if (_samples.TryDequeue(out var backupSample))
                    {
                        if (IsRecording)
                        {
                            _samplesBackup.Enqueue(backupSample);
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                Thread.Sleep(100);
            }
        });
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