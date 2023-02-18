using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace ACDCs.Extension.Magnetic;

public partial class MagneticRawPage : ContentPage
{
    private readonly LineSeries<FftInfo> _seriesFftX;
    private readonly LineSeries<FftInfo> _seriesFftY;
    private readonly LineSeries<FftInfo> _seriesFftZ;
    private readonly LineSeries<float> _seriesX;
    private readonly LineSeries<float> _seriesY;
    private readonly LineSeries<float> _seriesZ;
    private readonly MagneticWorker _worker;
    private DateTime _lastUpdate = DateTime.Now;

    public MagneticRawPage()
    {
        InitializeComponent();

        _worker = new MagneticWorker();
        FftSizePicker.SelectedIndex = 3;

        _seriesX = GetSeries(SKColors.Red);
        _seriesY = GetSeries(SKColors.Green);
        _seriesZ = GetSeries(SKColors.Blue);

        _seriesFftX = GetFftSeries(SKColors.Red);
        _seriesFftY = GetFftSeries(SKColors.Green);
        _seriesFftZ = GetFftSeries(SKColors.Blue);

        Chart.Series = new ObservableCollection<ISeries> { _seriesX, _seriesY, _seriesZ };
        Chart.XAxes = new List<Axis>
        {
            new()
            {
                Name = "Time",
                NameTextSize= 30,
                NamePaint = new SolidColorPaint(SKColors.White),
                LabelsPaint = new SolidColorPaint(SKColors.White),
                TextSize = 30,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
                {
                    StrokeThickness = 2
                }
            }
        };

        Chart.YAxes = new List<Axis>
        {
            new()
            {
                NameTextSize= 30,
                Name = "Power (μT)",
                NamePaint = new SolidColorPaint(SKColors.Yellow),
                LabelsPaint = new SolidColorPaint(SKColors.Yellow),
                TextSize = 30,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
                {
                    StrokeThickness = 2,
                    PathEffect = new DashEffect(new float[] { 3, 3 })
                }
            }
        };

        Fft.Series = new ObservableCollection<ISeries> { _seriesFftX, _seriesFftY, _seriesFftZ };

        Fft.XAxes = new List<Axis>
        {
            new()
            {
                Name = "Frequency (hz)",
                NameTextSize= 30,
                NamePaint = new SolidColorPaint(SKColors.White),
                LabelsPaint = new SolidColorPaint(SKColors.White),
                TextSize = 30,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 2 }
            }
        };

        Fft.YAxes = new List<Axis>
        {
            new()
            {
                Name = "Power (dbm)",
                NameTextSize= 30,
                NamePaint = new SolidColorPaint(SKColors.Yellow),
                LabelsPaint = new SolidColorPaint(SKColors.Yellow),
                TextSize = 30,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
                {
                    StrokeThickness = 2,
                    PathEffect = new DashEffect(new float[] { 3, 3 })
                },
                MinLimit = -100,
            }
        };
    }

    private static async Task<ObservableCollection<float>> AddSample(IEnumerable<float> values, float value)
    {
        if (values is not ObservableCollection<float> list) return null;
        list.Add(value);
        while (list.Count > 100) list.RemoveAt(0);
        return await Task.FromResult(list);
    }

    private static LineSeries<FftInfo> GetFftSeries(SKColor color)
    {
        return new LineSeries<FftInfo>
        {
            Values = new ObservableCollection<FftInfo>(),
            Stroke = new SolidColorPaint(color),
            GeometryFill = null,
            Fill = null,
            GeometryStroke = new SolidColorPaint(color),
            Mapping = Mapping
        };
    }

    private static LineSeries<float> GetSeries(SKColor color)
    {
        return new LineSeries<float>
        {
            Values = new ObservableCollection<float>(),
            Stroke = new SolidColorPaint(color),
            GeometryStroke = new SolidColorPaint(color),
            Fill = null,
            GeometryFill = null,
        };
    }

    private static void Mapping(FftInfo info, ChartPoint point)
    {
        point.PrimaryValue = info.Value;
        point.SecondaryValue = info.Freq;
    }

    private void DataSeries_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender == SeriesXCheckBox)
        {
            _seriesX.IsVisible = e.Value;
            _seriesFftX.IsVisible = e.Value;
        }
        else if (sender == SeriesYCheckBox)
        {
            _seriesY.IsVisible = e.Value;
            _seriesFftY.IsVisible = e.Value;
        }
        else if (sender == SeriesZCheckBox)
        {
            _seriesZ.IsVisible = e.Value;
            _seriesFftZ.IsVisible = e.Value;
        }
    }

    private void FFTSize_SelectedIndexChanged(object sender, EventArgs e)
    {
        _worker.FftWindowSize = Convert.ToInt32(FftSizePicker.SelectedItem);
    }

    private async void Magnetometer_ReadingChanged(object sender, MagnetometerChangedEventArgs e)
    {
        await _worker.AddSample(e.Reading.MagneticField);

        if (DateTime.Now.Ticks < _lastUpdate.Ticks + 2500000) return;

        await AddSample(_seriesX.Values, e.Reading.MagneticField.X);
        await AddSample(_seriesY.Values, e.Reading.MagneticField.Y);
        await AddSample(_seriesZ.Values, e.Reading.MagneticField.Z);
        labelSampleCount.Text = $"Tot:{_worker.SampleCount}";
        labelSampleBuffer.Text = $"Buf:{_worker.SampleCacheCount}";
        labelSampleRecord.Text = $"Rec:{_worker.SampleBackupCount}";
        labelRawX.Text = $"X:{e.Reading.MagneticField.X}";
        labelRawY.Text = $"Y:{e.Reading.MagneticField.Y}";
        labelRawZ.Text = $"Z:{e.Reading.MagneticField.Z}";
        UpdateFft();

        _lastUpdate = DateTime.Now;
    }

    private void OnOffSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        if (Magnetometer.Default.IsSupported)
        {
            if (e.Value)
            {
                Magnetometer.Default.ReadingChanged += Magnetometer_ReadingChanged;
                Magnetometer.Default.Start(SensorSpeed.Fastest);
            }
            else
            {
                Magnetometer.Default.Stop();
                Magnetometer.Default.ReadingChanged -= Magnetometer_ReadingChanged;
            }
        }
    }

    private void RecordSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        _worker.IsRecording = e.Value;
    }

    private async void UpdateFft()
    {
        await _worker.GetFft(VectorAxis.X, _seriesFftX.Values);
        await _worker.GetFft(VectorAxis.Y, _seriesFftY.Values);
        await _worker.GetFft(VectorAxis.Z, _seriesFftZ.Values);
    }
}