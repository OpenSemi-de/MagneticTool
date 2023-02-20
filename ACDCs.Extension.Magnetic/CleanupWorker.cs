using System.Collections.Concurrent;

namespace ACDCs.Extension.Magnetic;

public static class CleanupWorker
{
    private static int _fftWindowSize = 256;
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