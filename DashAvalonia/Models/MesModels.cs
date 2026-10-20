using System;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace DashAvalonia.Models;

public enum OrderStatus { InProgress, Waiting, Delayed, Completed }

public enum AlarmSeverity { Critical, High, Medium }

public enum MachineState { Running, Changeover, Down, Maintenance }

public sealed record ProductionOrder(
    string Id, string Sku, string Product, string LineCode, string LineName,
    int Target, int Done, OrderStatus Status, DateTime Eta);

public sealed record Alarm(string Title, string Source, AlarmSeverity Severity, DateTime RaisedAt);

public sealed record DowntimeCause(string Name, double Minutes, int Events);

/// <summary>Production series for the selected range: cumulative per hour (today) or totals per day.</summary>
public sealed record ProductionSeries(
    string[] Labels, double?[] Actual, double[] Plan,
    double TotalActual, double TotalPlan, bool IsCumulative, string WindowText);

public sealed record OeeSnapshot(
    double Availability, double Performance, double Quality,
    string[] LineCodes, string[] LineNames, double[] LineOee)
{
    public double Oee => Availability * Performance * Quality * 100;
}

public sealed record KpiSnapshot(
    double ScrapPercent, double ScrapDeltaPts, double DowntimeMinutes, double DowntimeDelta,
    double EnergyPerUnit, double EnergyDelta, double ProductionDelta,
    double[] ProductionTrend, double[] ScrapTrend, double[] DowntimeTrend, double[] EnergyTrend);

/// <summary>
/// Fixed status colors (good / warning / serious / critical + neutral). They are theme-invariant
/// and never reused for categorical series; they always travel with an icon or a label.
/// </summary>
public static class StatusPalette
{
    public static readonly IBrush Good = Brush(0x0C, 0xA3, 0x0C);
    public static readonly IBrush Warning = Brush(0xFA, 0xB2, 0x19);
    public static readonly IBrush Serious = Brush(0xEC, 0x83, 0x5A);
    public static readonly IBrush Critical = Brush(0xD0, 0x3B, 0x3B);
    public static readonly IBrush Neutral = Brush(0x8A, 0x94, 0xA3);

    public static IBrush Soft(IBrush brush)
    {
        var c = ((ISolidColorBrush)brush).Color;
        return new ImmutableSolidColorBrush(Color.FromArgb(0x2E, c.R, c.G, c.B));
    }

    public static IBrush For(OrderStatus status) => status switch
    {
        OrderStatus.InProgress => Good,
        OrderStatus.Waiting => Warning,
        OrderStatus.Delayed => Critical,
        _ => Neutral,
    };

    public static IBrush For(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Critical => Critical,
        AlarmSeverity.High => Serious,
        _ => Warning,
    };

    public static IBrush For(MachineState state) => state switch
    {
        MachineState.Running => Good,
        MachineState.Changeover => Warning,
        MachineState.Down => Critical,
        _ => Neutral,
    };

    private static IBrush Brush(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
}
