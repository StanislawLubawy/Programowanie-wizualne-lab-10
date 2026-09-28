using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CsvHelper;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AnalizatorFASTA;

public class FastaSequence
{
    public string Header { get; set; } = "";
    public string Sequence { get; set; } = "";

    public string Name => string.IsNullOrWhiteSpace(Header) ? "Nieznana" : Header.Split(' ')[0];
    public int Length => Sequence.Length;
    public int CountA => Sequence.Count(c => c == 'A' || c == 'a');
    public int CountT => Sequence.Count(c => c == 'T' || c == 't');
    public int CountG => Sequence.Count(c => c == 'G' || c == 'g');
    public int CountC => Sequence.Count(c => c == 'C' || c == 'c');

    public double GCContent => Length > 0 ? Math.Round((double)(CountG + CountC) / Length * 100, 2) : 0;
    public int CodonCount => Length / 3;
}

public partial class MainWindow : Window
{
    private List<FastaSequence> _sequences = new List<FastaSequence>();

    public MainWindow()
    {
        InitializeComponent();

        MyPlot.Model = new PlotModel
        {
            Title = "Długości sekwencji (brak danych)",
            TextColor = OxyColors.White,
            PlotAreaBorderColor = OxyColors.Gray,
            Background = OxyColors.Transparent
        };
    }

    public async void OnLoadFastaClick(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions { AllowMultiple = true };
        var files = await StorageProvider.OpenFilePickerAsync(options);

        if (files.Count > 0)
        {
            _sequences.Clear();
            foreach (var file in files)
            {
                ParseFastaFile(file.Path.LocalPath);
            }

            SequenceListBox.ItemsSource = null;
            SequenceListBox.ItemsSource = _sequences;
            UpdateChart();

            TxtStatus.Foreground = Avalonia.Media.Brushes.Green;
            TxtStatus.Text = $"Wczytano {_sequences.Count} sekwencji.";
        }
    }

    private void ParseFastaFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0 || !lines[0].StartsWith(">"))
        {
            TxtStatus.Foreground = Avalonia.Media.Brushes.Red;
            TxtStatus.Text = "Błąd: Nieprawidłowy format pliku FASTA.";
            return;
        }

        FastaSequence? currentSequence = null;
        StringBuilder seqBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith(">"))
            {
                if (currentSequence != null)
                {
                    currentSequence.Sequence = seqBuilder.ToString();
                    _sequences.Add(currentSequence);
                }
                currentSequence = new FastaSequence { Header = line.Substring(1).Trim() };
                seqBuilder.Clear();
            }
            else
            {
                seqBuilder.Append(line.Trim());
            }
        }

        if (currentSequence != null)
        {
            currentSequence.Sequence = seqBuilder.ToString();
            _sequences.Add(currentSequence);
        }
    }

    public void OnSequenceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (SequenceListBox.SelectedItem is FastaSequence seq)
        {
            TxtSeqName.Text = $"Nazwa: {seq.Name}";
            TxtSeqLength.Text = $"Długość: {seq.Length} bp";
            TxtGCContent.Text = $"Zawartość GC: {seq.GCContent}%";
            TxtCodons.Text = $"Liczba kodonów: {seq.CodonCount}";

            TxtCountA.Text = $"A: {seq.CountA}";
            TxtCountT.Text = $"T: {seq.CountT}";
            TxtCountG.Text = $"G: {seq.CountG}";
            TxtCountC.Text = $"C: {seq.CountC}";
        }
    }

    private void UpdateChart()
    {
        var plotModel = new PlotModel
        {
            Title = "Długości wczytanych sekwencji (bp)",
            TextColor = OxyColors.White,
            PlotAreaBorderColor = OxyColors.Gray,
            Background = OxyColors.Transparent
        };

        var categoryAxis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            TicklineColor = OxyColors.White,
            TextColor = OxyColors.White,
            Angle = 25
        };

        var valueAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            Title = "Długość (bp)",
            TicklineColor = OxyColors.White,
            TextColor = OxyColors.White,
            TitleColor = OxyColors.White
        };

        var rectSeries = new RectangleBarSeries
        {
            Title = "Długość",
            FillColor = OxyColor.Parse("#007ACC"),
            StrokeColor = OxyColors.White,
            StrokeThickness = 1
        };

        int index = 0;
        foreach (var seq in _sequences)
        {
            categoryAxis.Labels.Add(seq.Name);
            rectSeries.Items.Add(new RectangleBarItem(index - 0.4, 0, index + 0.4, seq.Length));
            index++;
        }

        plotModel.Axes.Clear();
        plotModel.Series.Clear();

        plotModel.Axes.Add(categoryAxis);
        plotModel.Axes.Add(valueAxis);
        plotModel.Series.Add(rectSeries);

        MyPlot.Model = plotModel;
        MyPlot.InvalidatePlot(true);
    }

    public async void OnExportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (_sequences.Count == 0) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Eksportuj do CSV",
            DefaultExtension = "csv"
        });

        if (file != null)
        {
            using var writer = new StreamWriter(file.Path.LocalPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(_sequences);
            TxtStatus.Text = "Wyeksportowano do CSV!";
        }
    }

    public async void OnExportJsonClick(object? sender, RoutedEventArgs e)
    {
        if (_sequences.Count == 0) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Eksportuj do JSON",
            DefaultExtension = "json"
        });

        if (file != null)
        {
            string json = JsonConvert.SerializeObject(_sequences, Formatting.Indented);
            File.WriteAllText(file.Path.LocalPath, json);
            TxtStatus.Text = "Wyeksportowano do JSON!";
        }
    }
}