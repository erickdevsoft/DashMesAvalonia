using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashAvalonia.Controls;
using DashAvalonia.Models;

namespace DashAvalonia.ViewModels;

public partial class NavItemViewModel(string title, Geometry icon) : ObservableObject
{
    public string Title { get; } = title;
    public Geometry Icon { get; } = icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    public partial int Badge { get; set; }

    public bool HasBadge => Badge > 0;
}

/// <summary>Stat tile: label, value, signed delta vs. a named period and a 12-point trend.</summary>
public partial class KpiTileViewModel(string title, Geometry icon) : ObservableObject
{
    public string Title { get; } = title;
    public Geometry Icon { get; } = icon;

    [ObservableProperty] public partial string Value { get; set; } = "";
    [ObservableProperty] public partial string Unit { get; set; } = "";
    [ObservableProperty] public partial string Delta { get; set; } = "";
    [ObservableProperty] public partial bool DeltaIsGood { get; set; }
    [ObservableProperty] public partial bool DeltaIsUp { get; set; }
    [ObservableProperty] public partial string Comparison { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<double>? Trend { get; set; }
}

public sealed record MetricItem(string Name, double Percent)
{
    public double Fraction => Percent / 100;
    public string Text => (Percent / 100).ToString("0.0 %", CultureInfo.CurrentCulture);
}

public sealed record LegendItem(string Label, int Count, string Share, IBrush Brush);

public sealed record FilterOption(string Title, int Count);

public sealed record ProductionRow(string Label, string Actual, string Plan, string Deviation, bool IsBehind);

public partial class OrderViewModel : ObservableObject
{
    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    public OrderViewModel(ProductionOrder order)
    {
        Id = order.Id;
        Sku = order.Sku;
        Product = order.Product;
        LineCode = order.LineCode;
        LineName = order.LineName;
        Target = order.Target;
        Eta = order.Eta;
        Done = order.Done;
        Status = order.Status;
    }

    public string Id { get; }
    public string Sku { get; }
    public string Product { get; }
    public string LineCode { get; }
    public string LineName { get; }
    public int Target { get; }
    public DateTime Eta { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress), nameof(ProgressText), nameof(DoneText))]
    public partial double Done { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(StatusBrush), nameof(StatusSoftBrush), nameof(MeterBrush), nameof(EtaText))]
    public partial OrderStatus Status { get; set; }

    public double Progress => Target == 0 ? 0 : Done / Target;
    public string ProgressText => Progress.ToString("0 %", Culture);
    public string DoneText => Math.Floor(Done).ToString("N0", Culture);
    public string TargetText => $"de {Target.ToString("N0", Culture)}";

    public string StatusText => Status switch
    {
        OrderStatus.InProgress => "En proceso",
        OrderStatus.Waiting => "En espera",
        OrderStatus.Delayed => "Retrasada",
        _ => "Completada",
    };

    public IBrush StatusBrush => StatusPalette.For(Status);
    public IBrush StatusSoftBrush => StatusPalette.Soft(StatusPalette.For(Status));

    /// <summary>Meter fill: null keeps the themed accent for normal progress.</summary>
    public IBrush? MeterBrush => Status switch
    {
        OrderStatus.Delayed => StatusPalette.Critical,
        OrderStatus.Completed => StatusPalette.Good,
        _ => null,
    };

    public string EtaText
    {
        get
        {
            var today = DateTime.Today;
            var time = Eta.ToString("HH:mm", Culture);
            if (Status == OrderStatus.Completed) return $"Fin {time}";
            if (Eta.Date == today) return time;
            if (Eta.Date == today.AddDays(1)) return $"Mañana {time}";
            return Eta.ToString("dd/MM HH:mm", Culture);
        }
    }
}

public partial class AlarmViewModel(Alarm alarm, Action<AlarmViewModel> acknowledge) : ObservableObject
{
    public string Title { get; } = alarm.Title;
    public string Source { get; } = alarm.Source;
    public AlarmSeverity Severity { get; } = alarm.Severity;
    public DateTime RaisedAt { get; } = alarm.RaisedAt;

    public string SeverityText => Severity switch
    {
        AlarmSeverity.Critical => "Crítica",
        AlarmSeverity.High => "Alta",
        _ => "Media",
    };

    public IBrush SeverityBrush => StatusPalette.For(Severity);
    public IBrush SeveritySoftBrush => StatusPalette.Soft(StatusPalette.For(Severity));

    public Geometry SeverityIcon => Severity switch
    {
        AlarmSeverity.Critical => Icons.AlertOctagon,
        AlarmSeverity.High => Icons.AlertTriangle,
        _ => Icons.AlertCircle,
    };

    [ObservableProperty] public partial string Age { get; set; } = "";

    public void UpdateAge(DateTime now)
    {
        var minutes = (int)(now - RaisedAt).TotalMinutes;
        Age = minutes < 1 ? "ahora" : minutes < 60 ? $"hace {minutes} min" : $"hace {minutes / 60} h";
    }

    [RelayCommand]
    private void Acknowledge() => acknowledge(this);
}
