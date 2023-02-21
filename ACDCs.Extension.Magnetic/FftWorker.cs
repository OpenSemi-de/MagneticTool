using System.Collections.Concurrent;

namespace ACDCs.Extension.Magnetic;

public static class FftWorker
{
    private static readonly List<FftInfo> _seriesFftX = new();
    private static readonly List<FftInfo> _seriesFftY = new();
    private static readonly List<FftInfo> _seriesFftZ = new();
    private static int _fftWindowSize = 256;
    private static Filter _filter = Filter.None;
    private static double _filterFrequency = 0;
    private static bool _isRunning = false;
    private static ConcurrentQueue<MagneticSample> _samples = new();
    private static Thread _thread = GetThread();

    public static int FftWindowSize
    {
        get => _fftWindowSize;
        set
        {
            UseMutex(() => { _fftWindowSize = value; });
        }
    }

    public static Filter Filter
    {
        get => _filter;
        set
        {
            UseMutex(() => _filter = value);
        }
    }

    public static double FilterFrequency
    {
        get => _filterFrequency;
        set
        {
            UseMutex(() => _filterFrequency = value);
        }
    }

    public static double FilterFrequencyMax { get; set; }

    public static bool IsRunning
    {
        get => _isRunning;
        set
        {
            UseMutex(() => { _isRunning = value; });
        }
    }

    public static Action<VectorAxis, List<FftInfo>> OnFftUpdate { get; set; }

    public static ConcurrentQueue<MagneticSample> Samples
    {
        get => _samples;
        set
        {
            UseMutex(() => { _samples = value; });
        }
    }

    private static Mutex Mutex { get; set; } = new();

    private static async void Fft_BackgroundTask()
    {
        while (true)
        {
            Mutex.WaitOne();

            if (IsRunning)
            {
                await GetFft(VectorAxis.X, _seriesFftX);
                Task.Run(() => OnFftUpdate.Invoke(VectorAxis.X, _seriesFftX));
                Thread.Sleep(5);
                await GetFft(VectorAxis.Y, _seriesFftY);
                Task.Run(() => OnFftUpdate.Invoke(VectorAxis.Y, _seriesFftY));
                Thread.Sleep(5);
                await GetFft(VectorAxis.Z, _seriesFftZ);
                Task.Run(() => OnFftUpdate.Invoke(VectorAxis.Z, _seriesFftY));
                Mutex.ReleaseMutex();
                Thread.Sleep(200);
            }
            else
            {
                Mutex.ReleaseMutex();
                Thread.Sleep(100);
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static async Task GetFft(VectorAxis axis, List<FftInfo> values)
    {
        double[] signal = await GetLastSamples(axis);
        if (signal.Length < FftWindowSize)
        {
            return;
        }

        switch (Filter)
        {
            case Filter.LowPass:
                signal = FftSharp.Filter.LowPass(signal, 100, maxFrequency: FilterFrequency);
                break;

            case Filter.HighPass:
                signal = FftSharp.Filter.HighPass(signal, 100, minFrequency: FilterFrequency);
                break;

            case Filter.BandPass:
                signal = FftSharp.Filter.BandPass(signal, 100, minFrequency: FilterFrequency, maxFrequency: FilterFrequencyMax);
                break;

            case Filter.BandStop:
                signal = FftSharp.Filter.BandStop(signal, 100, minFrequency: FilterFrequency, maxFrequency: FilterFrequencyMax);
                break;
        }

        const int sampleRate = 100;
        var psd = FftSharp.Transform.FFTpower(signal);
        var freq = FftSharp.Transform.FFTfreq(sampleRate, psd.Length);
        values.Clear();
        var x = 0;
        foreach (var value in psd)
        {
            var rvalue = value;
            if (value < -380) rvalue = -380;
            FftInfo info = new(rvalue, freq[x]);
            values.Add(info);
            x++;
        }
    }

    private static async Task<double[]> GetLastSamples(VectorAxis axis, int windowSize = 256)
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

    private static Thread GetThread()
    {
        Thread thread = new(Fft_BackgroundTask)
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