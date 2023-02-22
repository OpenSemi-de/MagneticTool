using System.Collections.Concurrent;

namespace ACDCs.Extension.Magnetic;

public class FftWorker
{
    /*  private readonly FftInfoPacket _seriesFftX = new(VectorAxis.X);
      private readonly FftInfoPacket _seriesFftY = new(VectorAxis.Y);
      private readonly FftInfoPacket _seriesFftZ = new(VectorAxis.Z);*/
    private int _fftWindowSize = 256;
    private Filter _filter = Filter.None;
    private double _filterFrequency = 0;
    private bool _isRunning = false;
    private ConcurrentQueue<MagneticSample> _samples = new();
    private Thread _thread;

    public int FftWindowSize
    {
        get => _fftWindowSize;
        set
        {
            UseMutex(() => { _fftWindowSize = value; });
        }
    }

    public Filter Filter
    {
        get => _filter;
        set
        {
            UseMutex(() => _filter = value);
        }
    }

    public double FilterFrequency
    {
        get => _filterFrequency;
        set
        {
            UseMutex(() => _filterFrequency = value);
        }
    }

    public double FilterFrequencyMax { get; set; }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            UseMutex(() => { _isRunning = value; });
        }
    }

    public Mutex Mutex { get; set; } = new();
    public ConcurrentQueue<FftInfoPacket> OutputQueue { get; } = new();

    public ConcurrentQueue<MagneticSample> Samples
    {
        get => _samples;
        set
        {
            UseMutex(() => { _samples = value; });
        }
    }

    public FftWorker()
    {
        _thread = GetThread();
    }

    private void Enqueue(FftInfoPacket seriesFft)
    {
        if (seriesFft == null || seriesFft.Count < 1) return;
        OutputQueue.Enqueue(seriesFft);
    }

    private async void Fft_BackgroundTask()
    {
        while (true)
        {
            if (IsRunning)
            {
                FftInfoPacket seriesFftX = new(VectorAxis.X);
                await GetFft(VectorAxis.X, seriesFftX);
                Enqueue(seriesFftX);
                FftInfoPacket seriesFftY = new(VectorAxis.Y);
                await GetFft(VectorAxis.Y, seriesFftY);
                Enqueue(seriesFftY);
                FftInfoPacket seriesFftZ = new(VectorAxis.Z);
                await GetFft(VectorAxis.Z, seriesFftZ);
                Enqueue(seriesFftZ);
            }
            while (OutputQueue.Count > 3)
                await Task.Delay(200);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task GetFft(VectorAxis axis, FftInfoPacket values)
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

    private async Task<double[]> GetLastSamples(VectorAxis axis, int windowSize = 256)
    {
        var samplesList = _samples
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

    private Thread GetThread()
    {
        Thread thread = new(Fft_BackgroundTask)
        {
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
        return thread;
    }

    private void UseMutex(Action action)
    {
        Mutex.WaitOne();
        action.Invoke();
        Mutex.ReleaseMutex();
    }
}

public class FftInfoPacket : List<FftInfo>
{
    public VectorAxis Axis { get; set; }

    public FftInfoPacket(VectorAxis axis)
    {
        Axis = axis;
    }
}