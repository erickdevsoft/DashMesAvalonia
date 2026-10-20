using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>
/// Single-series column chart with value labels on the caps and an optional dashed target line.
/// Hovering a column dims the others and shows its detail tooltip.
/// </summary>
public class ColumnChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ColumnChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<ColumnChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IReadOnlyList<string>?> DetailsProperty =
        AvaloniaProperty.Register<ColumnChart, IReadOnlyList<string>?>(nameof(Details));

    public static readonly StyledProperty<double> TargetProperty =
        AvaloniaProperty.Register<ColumnChart, double>(nameof(Target), double.NaN);

    public static readonly StyledProperty<double> YMaxProperty =
        AvaloniaProperty.Register<ColumnChart, double>(nameof(YMax), double.NaN);

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<ColumnChart, string>(nameof(ValueFormat), "N0");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<ColumnChart, string>(nameof(Unit), "");

    public static readonly StyledProperty<string> SeriesNameProperty =
        AvaloniaProperty.Register<ColumnChart, string>(nameof(SeriesName), "Valor");

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ColumnChart, IBrush?>(nameof(Fill));

    static ColumnChart()
    {
        AffectsRender<ColumnChart>(LabelsProperty, ValuesProperty, DetailsProperty, TargetProperty,
            YMaxProperty, ValueFormatProperty, UnitProperty, FillProperty);
    }

    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IReadOnlyList<string>? Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public double Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public double YMax { get => GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string SeriesName { get => GetValue(SeriesNameProperty); set => SetValue(SeriesNameProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var labels = Labels;
        var values = Values;
        if (labels is null || values is null || values.Count == 0 || labels.Count != values.Count)
            return;

        var n = values.Count;
        var hasTarget = !double.IsNaN(Target);
        var muted = Res("TextMutedBrush");
        var secondary = Res("TextSecondaryBrush");
        var fill = Fill ?? Res("AccentBrush");

        var max = double.IsNaN(YMax) ? Math.Max(values.Max(), hasTarget ? Target : 0) : YMax;
        var step = NiceStep(max, 4);
        if (double.IsNaN(YMax)) max = Math.Ceiling(max / step) * step;

        var ticks = new List<(double Value, FormattedText Text)>();
        for (var v = 0.0; v <= max + step * 1e-6; v += step)
            ticks.Add((v, Text(Format(v, "N0", Unit), 11, muted)));

        var left = ticks.Max(t => t.Text.Width) + 12;
        var plot = new Rect(left, 20, Math.Max(10, bounds.Width - left - 4), Math.Max(10, bounds.Height - 20 - 24));
        double Y(double v) => plot.Bottom - v / max * plot.Height;

        var gridPen = new Pen(Res("GridBrush"));
        var axisPen = new Pen(Res("AxisBrush"));
        foreach (var (value, text) in ticks)
        {
            var y = Math.Round(Y(value)) + 0.5;
            ctx.DrawLine(value == 0 ? axisPen : gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            ctx.DrawText(text, new Point(left - 10 - text.Width, y - text.Height / 2));
        }

        // Target line sits behind the columns; their cap labels carry a halo over it.
        if (hasTarget && Target <= max)
        {
            var y = Math.Round(Y(Target)) + 0.5;
            ctx.DrawLine(new Pen(secondary, 1, new DashStyle([4, 3], 0)), new Point(plot.Left, y), new Point(plot.Right, y));
        }

        var slot = plot.Width / n;
        var barWidth = Math.Min(24, slot * 0.55);

        int? hovered = null;
        if (Hover is { } h && h.X >= plot.Left && h.X <= plot.Right && h.Y >= 0 && h.Y <= bounds.Height)
            hovered = Math.Clamp((int)((h.X - plot.Left) / slot), 0, n - 1);

        for (var i = 0; i < n; i++)
        {
            var cx = plot.Left + slot * (i + 0.5);
            var top = Y(Math.Clamp(values[i], 0, max));
            var rect = new Rect(cx - barWidth / 2, top, barWidth, Math.Max(0, plot.Bottom - top));

            using (ctx.PushOpacity(hovered is { } hi && hi != i ? 0.4 : 1))
            {
                if (rect.Height > 0)
                    ctx.DrawGeometry(fill, null, ColumnGeometry(rect, 4));

                var valueText = Text(Format(values[i], ValueFormat, ""), 11, Res("TextPrimaryBrush"), FontWeight.SemiBold);
                var textPos = new Point(cx - valueText.Width / 2, top - valueText.Height - 4);
                // Surface halo so the cap label stays legible where it sits on the target line.
                var halo = new Rect(textPos, new Size(valueText.Width, valueText.Height)).Inflate(new Thickness(3, 0));
                ctx.DrawRectangle(Res("SurfaceBrush"), null, new RoundedRect(halo, 3));
                ctx.DrawText(valueText, textPos);
            }

            var label = Text(labels[i], 11, hovered == i ? secondary : muted, hovered == i ? FontWeight.SemiBold : FontWeight.Normal);
            ctx.DrawText(label, new Point(cx - label.Width / 2, plot.Bottom + 8));
        }

        if (hovered is not { } index)
            return;

        var rows = new List<TipRow> { new(SeriesName, Format(values[index], ValueFormat, Unit), fill) };
        if (hasTarget)
        {
            rows.Add(new TipRow("Meta", Format(Target, ValueFormat, Unit), secondary, true));
            rows.Add(new TipRow("Brecha", (values[index] - Target).ToString("+0.0;−0.0;0.0", Culture) + " pts"));
        }

        var title = Details is { } d && d.Count == n ? d[index] : labels[index];
        DrawTooltip(ctx, new Point(plot.Left + slot * (index + 0.5) + barWidth / 2, Y(values[index])), title, rows);
    }
}
