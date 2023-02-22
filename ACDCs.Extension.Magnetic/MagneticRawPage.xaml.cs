using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using System.Collections.ObjectModel;
using UraniumUI.Icons.FontAwesome;

namespace ACDCs.Extension.Magnetic;

#pragma warning disable IDE1007

using Sharp.UI;

#pragma warning restore IDE1007

public class MagneticRawPage : ContentPage
{
    private readonly Axis _axisFftX;
    private readonly Axis _axisFftY;
    private readonly LineSeries<FftInfo> _seriesFftX;
    private readonly LineSeries<FftInfo> _seriesFftY;
    private readonly LineSeries<FftInfo> _seriesFftZ;
    private readonly LineSeries<float> _seriesX;
    private readonly LineSeries<float> _seriesY;
    private readonly LineSeries<float> _seriesZ;
    private readonly MagneticWorker _worker;
    private double _filterFrequency;
    private double _filterFrequencyMax;
    private DateTime _lastUpdate = DateTime.Now;
    private Grid _grid;
    private Label labelSampleCount;
    private Label labelSampleBuffer;
    private Label labelSampleRecord;
    private Label labelRawX;
    private Label labelRawY;
    private Label labelRawZ;
    private readonly FftWorker _fftWorker;
    private readonly Timer _updeTimer;

    public MagneticRawPage()
    {
        _fftWorker = new FftWorker();
        _worker = new(_fftWorker);
        _updeTimer = new Timer(OnUpdateScreen, null, 0, 250);

        InitializeComponent();
        Content = _grid;

        _seriesX = GetSeries(SKColors.Red);
        _seriesY = GetSeries(SKColors.Green);
        _seriesZ = GetSeries(SKColors.Blue);

        _seriesFftX = GetFftSeries(SKColors.Red);
        _seriesFftY = GetFftSeries(SKColors.Green);
        _seriesFftZ = GetFftSeries(SKColors.Blue);

        _chart.Series = new ObservableCollection<ISeries> { _seriesX, _seriesY, _seriesZ };
        _chart.XAxes = new List<Axis>
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

        _chart.YAxes = new List<Axis>
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

        _fft.Series = new ObservableCollection<ISeries> { _seriesFftX, _seriesFftY, _seriesFftZ };

        _axisFftX = new()
        {
            Name = "Frequency (hz)",
            NameTextSize = 30,
            NamePaint = new SolidColorPaint(SKColors.White),
            LabelsPaint = new SolidColorPaint(SKColors.White),
            TextSize = 30,
            SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 2 }
        };

        _fft.XAxes = new List<Axis>
        {
            _axisFftX
        };

        _axisFftY = new()
        {
            Name = "Power (dbm)",
            NameTextSize = 30,
            NamePaint = new SolidColorPaint(SKColors.Yellow),
            LabelsPaint = new SolidColorPaint(SKColors.Yellow),
            TextSize = 30,
            SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
            {
                StrokeThickness = 2,
                PathEffect = new DashEffect(new float[] { 3, 3 })
            },
            MinLimit = -100,
        };

        _fft.YAxes = new List<Axis>
        {
            _axisFftY
        };
    }

    private void OnUpdateScreen(object state)
    {
        if (!IsRunning) return;
        if (_fftWorker.OutputQueue.TryDequeue(out FftInfoPacket fftInfos))
        {
            OnFftUpdate(fftInfos.Axis, fftInfos);
        }
    }

    private void OnFftUpdate(VectorAxis axis, List<FftInfo> list)
    {
        switch (axis)
        {
            case VectorAxis.X:
                if (_seriesFftX.Values is ObservableCollection<FftInfo> collectionX)
                {
                    collectionX.Clear();
                    list.ForEach(collectionX.Add);
                }

                break;

            case VectorAxis.Y:
                if (_seriesFftY.Values is ObservableCollection<FftInfo> collectionY)
                {
                    collectionY.Clear();
                    list.ForEach(collectionY.Add);
                }

                break;

            case VectorAxis.Z:
                if (_seriesFftZ.Values is ObservableCollection<FftInfo> collectionZ)
                {
                    collectionZ.Clear();
                    list.ForEach(collectionZ.Add);
                }

                break;
        }
    }

    private void InitializeComponent()
    {
        RowDefinitionCollection rowDefinitions = new()
        {
            new(32),
            new(),
            new(40)
        };

        ColumnDefinitionCollection columnDefinitions = new()
        {
            new(70),
            new()
        };

        _grid = new Grid()
            .RowDefinitions(rowDefinitions)
            .ColumnDefinitions(columnDefinitions);

        labelSampleCount = GetLabel().Text("Sam.:");
        labelSampleBuffer = GetLabel().Text("Buf.:");
        labelSampleRecord = GetLabel().Text("Rec.:");
        labelRawX = GetLabel().Text("X:").TextColor(Colors.Red);
        labelRawY = GetLabel().Text("Y:").TextColor(Colors.Green);
        labelRawZ = GetLabel().Text("Z:").TextColor(Colors.Blue);

        _grid.Add(
            new Label("ACDCs.MagneticTool")
                    .FontSize(10)
                    .ColumnSpan(2)
        );

        VerticalStackLayout controlLayout = new VerticalStackLayout
        {
            new HorizontalStackLayout
            {
                new Switch()
                    .HorizontalOptions(LayoutOptions.Start)
                    .OnToggled(OnOffSwitch_Toggled),
                new Label(Solid.PowerOff)
                    .FontSize(20)
                    .FontFamily("FASolid")
                    .HorizontalOptions(LayoutOptions.Start)
                    .VerticalTextAlignment(TextAlignment.Center)
            }.Margin(new Thickness(0,0,0,5)),
            new HorizontalStackLayout
            {
                new Switch()
                    .HorizontalOptions(LayoutOptions.Start)
                    .OnToggled(RecordSwitch_Toggled),
                new Label(Solid.FloppyDisk)
                    .FontSize(20)
                    .FontFamily("FASolid")
                    .HorizontalOptions(LayoutOptions.Start)
                    .VerticalTextAlignment(TextAlignment.Center)
            }.Margin(new Thickness(0,0,0,5)),
            new VerticalStackLayout
            {
                labelSampleCount,
                labelSampleBuffer,
                labelSampleRecord,
                labelRawX,
                labelRawY,
                labelRawZ
            }.Margin(new Thickness(0,0,0,5)),
            new HorizontalStackLayout
            {
                new CheckBox()
                    .HorizontalOptions(LayoutOptions.Start)
                    .Color(Colors.Red)
                    .WidthRequest(20)
                    .IsChecked(true)
                    .OnCheckedChanged(DataSeriesX_OnCheckedChanged),
                new Label("X")
                    .HorizontalOptions(LayoutOptions.Start)
                    .TextColor(Colors.Red)
                    .HorizontalTextAlignment(TextAlignment.Start)
                    .VerticalTextAlignment(TextAlignment.Center)
            }.Margin(new Thickness(0,0,0,0)),
            new HorizontalStackLayout
            {
                new CheckBox()
                    .HorizontalOptions(LayoutOptions.Start)
                    .Color(Colors.Green)
                    .WidthRequest(20)
                    .IsChecked(true)
                    .OnCheckedChanged(DataSeriesY_OnCheckedChanged),
                new Label("Y")
                    .HorizontalOptions(LayoutOptions.Start)
                    .TextColor(Colors.Green)
                    .HorizontalTextAlignment(TextAlignment.Start)
                    .VerticalTextAlignment(TextAlignment.Center)
            }.Margin(new Thickness(0,0,0,0)),
            new HorizontalStackLayout
            {
                new CheckBox()
                    .HorizontalOptions(LayoutOptions.Start)
                    .Color(Colors.Blue)
                    .WidthRequest(20)
                    .IsChecked(true)
                    .OnCheckedChanged(DataSeriesZ_OnCheckedChanged),
                new Label("X")
                    .HorizontalOptions(LayoutOptions.Start)
                    .TextColor(Colors.Blue)
                    .HorizontalTextAlignment(TextAlignment.Start)
                    .VerticalTextAlignment(TextAlignment.Center)
            }.Margin(new Thickness(0,0,0,5)),
            new VerticalStackLayout
            {
                new HorizontalStackLayout
                {
                    new Picker()
                        .HorizontalOptions(LayoutOptions.Start)
                        .OnSelectedIndexChanged(FFTSize_SelectedIndexChanged)
                        .Items("2048", "1024", "512", "256", "128")
                        .SelectedIndex(3),
                    new Label("samples")
                        .VerticalTextAlignment(TextAlignment.Center)
                        .HorizontalOptions(LayoutOptions.Start)
                },
                new Label("FFT size")
                        .VerticalTextAlignment(TextAlignment.Center)
                        .HorizontalOptions(LayoutOptions.Start)
            }
        }.Row(1);

        _chart = new();
        _fft = new();

        _grid.Add(new FlexLayout
        {
            _chart,
            _fft
        }
            .RowSpan(2)
            .Column(1)
            .Direction(FlexDirection.Row)
            .Wrap(FlexWrap.Wrap)
        );

        _freqToLabel = new Label("to freq.(hz)")
            .HorizontalOptions(LayoutOptions.Start)
            .VerticalOptions(LayoutOptions.Center);

        _freqToEntry = new Entry()
            .WidthRequest(50)
            .MaxLength(5)
            .OnTextChanged(frequencyEntryMax_TextChanged);

        _grid.Add(new HorizontalStackLayout
           {
               new Label("Filter:")
                   .HorizontalOptions(LayoutOptions.Start)
                   .VerticalOptions(LayoutOptions.Center),
               new Picker()
                   .HorizontalOptions(LayoutOptions.Start)
                   .Items("None", "Low-pass", "High-pass","Band-pass", "Band-stop")
                   .OnSelectedIndexChanged(FilterPicker_SelectedIndexChanged)
                   .Margin(new Thickness(0,0, 5, 0)),
               new Label("From")
                   .HorizontalOptions(LayoutOptions.Start)
                   .VerticalOptions(LayoutOptions.Center),
               new Entry()
                   .WidthRequest(50)
                   .MaxLength(5)
                   .OnTextChanged(FrequencyEntry_TextChanged),
               _freqToLabel,
               _freqToEntry
           }
            .Row(2)
            .ColumnSpan(2)
        );

        _grid.Add(controlLayout);
    }

    private CartesianChart _fft;

    private CartesianChart _chart;
    private Label _freqToLabel;
    private Entry _freqToEntry;

    private static Label GetLabel()
    {
        return new Label()
            .FontSize(10)
            .HorizontalOptions(LayoutOptions.Start)
            .VerticalOptions(LayoutOptions.Start)
            .HorizontalTextAlignment(TextAlignment.Start)
            .VerticalTextAlignment(TextAlignment.End);
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
        return new()
        {
            Values = new ObservableCollection<FftInfo>(),
            Stroke = new SolidColorPaint(color) { StrokeThickness = 3 },
            GeometryFill = null,
            Fill = null,
            GeometryStroke = null,
            Mapping = Mapping,
        };
    }

    private static LineSeries<float> GetSeries(SKColor color)
    {
        return new()
        {
            Values = new ObservableCollection<float>(),
            Stroke = new SolidColorPaint(color) { StrokeThickness = 3 },
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

    private void DataSeriesX_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        _seriesX.IsVisible = e.Value;
        _seriesFftX.IsVisible = e.Value;
    }

    private void DataSeriesY_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        _seriesY.IsVisible = e.Value;
        _seriesFftY.IsVisible = e.Value;
    }

    private void DataSeriesZ_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        _seriesZ.IsVisible = e.Value;
        _seriesFftZ.IsVisible = e.Value;
    }

    private void FFTSize_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (sender is Picker FftSizePicker)
        {
            _worker.FftWindowSize = Convert.ToInt32(FftSizePicker.SelectedItem);
        }
    }

    private void FilterPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (sender is not Picker filterPicker) return;
        switch (filterPicker.SelectedIndex)
        {
            case 0:
                _fftWorker.Filter = Filter.None;
                break;

            case 1:
                _fftWorker.Filter = Filter.LowPass;
                _freqToEntry.IsEnabled = false;
                _freqToLabel.IsEnabled = false;
                break;

            case 2:
                _fftWorker.Filter = Filter.HighPass;
                _freqToEntry.IsEnabled = false;
                _freqToLabel.IsEnabled = false;
                break;

            case 3:
                _fftWorker.Filter = Filter.BandPass;
                _freqToEntry.IsEnabled = true;
                _freqToLabel.IsEnabled = true;
                break;

            case 4:
                _fftWorker.Filter = Filter.BandStop;
                _freqToEntry.IsEnabled = true;
                _freqToLabel.IsEnabled = true;
                break;

            default:
                _fftWorker.Filter = Filter.None;
                break;
        }
    }

    private void FrequencyEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry frequencyEntry) return;

        if (double.TryParse(e.NewTextValue, out var frequency))
        {
            _filterFrequency = frequency;
        }
        else
        {
            frequencyEntry.Text = "";
        }

        _fftWorker.FilterFrequency = _filterFrequency;
    }

    private void frequencyEntryMax_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry frequencyEntryMax) return;

        if (double.TryParse(e.NewTextValue, out var frequency))
        {
            _filterFrequencyMax = frequency;
        }
        else
        {
            frequencyEntryMax.Text = "";
        }

        _fftWorker.FilterFrequencyMax = _filterFrequencyMax;
    }

    private async void Magnetometer_ReadingChanged(object sender, MagnetometerChangedEventArgs e)
    {
        _worker.AddSample(e.Reading.MagneticField);

        if (DateTime.Now.Ticks < _lastUpdate.Ticks + 2500000) return;

        Task.WaitAll(AddSample(_seriesX.Values, e.Reading.MagneticField.X), AddSample(_seriesY.Values, e.Reading.MagneticField.Y), AddSample(_seriesZ.Values, e.Reading.MagneticField.Z));
        labelSampleCount.Text = $"Tot:{_worker.SampleCount}";

        labelSampleBuffer.Text = $"Buf:{_worker.SampleCacheCount}";
        labelSampleRecord.Text = $"Rec:{_worker.SampleBackupCount}";
        labelRawX.Text = $"X:{e.Reading.MagneticField.X}";
        labelRawY.Text = $"Y:{e.Reading.MagneticField.Y}";
        labelRawZ.Text = $"Z:{e.Reading.MagneticField.Z}";

        _lastUpdate = DateTime.Now;
    }

    private void OnOffSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        if (Magnetometer.Default.IsSupported)
        {
            _fftWorker.IsRunning = e.Value;
            IsRunning = e.Value;
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

    public bool IsRunning { get; set; }

    private void RecordSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        _worker.IsRecording = e.Value;
    }
}

public enum Filter
{
    None,
    LowPass,
    HighPass,
    BandPass,
    BandStop
}