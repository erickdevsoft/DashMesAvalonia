using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>One horizontal bar: category label, value and an optional tooltip detail.</summary>
public sealed record BarDatum(string Label, double Value, string? Detail = null);

/// <summary>
/// Ranked horizontal bars (e.g. a downtime Pareto): labels on the left, value at the bar tip,
/// share of total in the hover tooltip.
/// </summary>
public class BarListChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<BarDatum>?> ItemsProperty =
        AvaloniaProperty.Register<BarListChart, IReadOnlyList<BarDatum>?>(nameof(Items));

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<BarListChart, string>(nameof(ValueFormat), "N0");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<BarListChart, string>(nameof(Unit), "");

    public static readonly StyledProperty<string> ValueNameProperty =
        AvaloniaProperty.Register<BarListChart, string>(nameof(ValueName), "Valor");

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<BarListChart, IBrush?>(nameof(Fill));

    static BarListChart()
    {
        AffectsRender<BarListChart>(ItemsProperty, ValueFormatProperty, UnitProperty, FillProperty);
    }

    public IReadOnlyList<BarDatum>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string ValueName { get => GetValue(ValueNameProperty); set => SetValue(ValueNameProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var items = Items;
        if (items is null || items.Count == 0)
            return;

        var n = items.Count;
        var fill = Fill ?? Res("AccentBrush");
        var secondary = Res("TextSecondaryBrush");
        var primary = Res("TextPrimaryBrush");
        var max = Math.Max(items.Max(i => i.Value), 1e-9);
        var total = items.Sum(i => i.Value);

        var labels = items.Select(i => Text(i.Label, 12, secondary)).ToArray();
        var values = items.Select(i => Text(Format(i.Value, ValueFormat, Unit), 11.5, primary, FontWeight.SemiBold)).ToArray();
        var labelWidth = Math.Min(labels.Max(t => t.Width), bounds.Width * 0.42) + 14;
        var valueWidth = values.Max(t => t.Width) + 8;

        var rowHeight = Math.Min(bounds.Height / n, 40);
        var barHeight = Math.Min(12, rowHeight * 0.45);
        var barLeft = labelWidth;
        var barSpan = Math.Max(10, bounds.Width - barLeft - valueWidth);
        var baseline = new Pen(Res("AxisBrush"));
        ctx.DrawLine(baseline, new Point(barLeft + 0.5, 0), new Point(barLeft + 0.5, rowHeight * n));

        int? hovered = null;
        if (Hover is { } h && h.Y >= 0 && h.Y < rowHeight * n)
            hovered = Math.Clamp((int)(h.Y / rowHeight), 0, n - 1);

        for (var i = 0; i < n; i++)
        {
            var cy = rowHeight * (i + 0.5);
            if (hovered == i)
                ctx.DrawRectangle(Res("HoverBrush"), null, new RoundedRect(new Rect(0, cy - rowHeight / 2 + 1, bounds.Width, rowHeight - 2), 6));

            var label = labels[i];
            using (ctx.PushClip(new Rect(0, 0, labelWidth - 10, bounds.Height)))
                ctx.DrawText(label, new Point(4, cy - label.Height / 2));

            var length = barSpan * items[i].Value / max;
            using (ctx.PushOpacity(hovered is { } hi && hi != i ? 0.45 : 1))
            {
                if (length > 0.5)
                    ctx.DrawGeometry(fill, null, BarGeometry(new Rect(barLeft + 1, cy - barHeight / 2, length, barHeight), 4));
                ctx.DrawText(values[i], new Point(barLeft + length + 8, cy - values[i].Height / 2));
            }
        }

        if (hovered is not { } index)
            return;

        var item = items[index];
        var rows = new List<TipRow>
        {
            new(ValueName, Format(item.Value, ValueFormat, Unit), fill),
            new("Participación", (total > 0 ? item.Value / total : 0).ToString("0 %", Culture)),
        };
        if (item.Detail is { } detail)
            rows.Add(new TipRow("Eventos", detail));

        DrawTooltip(ctx, new Point(Hover!.Value.X, rowHeight * (index + 0.5)), item.Label, rows);
    }
}
