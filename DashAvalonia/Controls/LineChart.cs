using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>
/// Line/area chart with an optional dashed reference series (e.g. plan) and an optional
/// horizontal target line. Hovering shows a crosshair and a tooltip for the nearest point.
/// </summary>
public class LineChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<IReadOnlyList<double?>?> ValuesProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<double?>?>(nameof(Values));

    public static readonly StyledProperty<IReadOnlyList<double>?> ReferenceProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<double>?>(nameof(Reference));

    public static readonly StyledProperty<double> TargetProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(Target), double.NaN);

    public static readonly StyledProperty<string> TargetLabelProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(TargetLabel), "Meta");

    public static readonly StyledProperty<double> YMinProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(YMin), double.NaN);

    public static readonly StyledProperty<double> YMaxProperty =
        AvaloniaProperty.Register<LineChart, double>(nameof(YMax), double.NaN);

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(ValueFormat), "N0");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(Unit), "");

    public static readonly StyledProperty<string> SeriesNameProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(SeriesName), "Real");

    public static readonly StyledProperty<string> ReferenceNameProperty =
        AvaloniaProperty.Register<LineChart, string>(nameof(ReferenceName), "Plan");

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<bool> ShowAreaProperty =
        AvaloniaProperty.Register<LineChart, bool>(nameof(ShowArea), true);

    static LineChart()
    {
        AffectsRender<LineChart>(LabelsProperty, ValuesProperty, ReferenceProperty, TargetProperty,
            TargetLabelProperty, YMinProperty, YMaxProperty, ValueFormatProperty, UnitProperty,
            StrokeProperty, ShowAreaProperty);
    }

    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public IReadOnlyList<double?>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IReadOnlyList<double>? Reference { get => GetValue(ReferenceProperty); set => SetValue(ReferenceProperty, value); }
    public double Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public string TargetLabel { get => GetValue(TargetLabelProperty); set => SetValue(TargetLabelProperty, value); }
    public double YMin { get => GetValue(YMinProperty); set => SetValue(YMinProperty, value); }
    public double YMax { get => GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string SeriesName { get => GetValue(SeriesNameProperty); set => SetValue(SeriesNameProperty, value); }
    public string ReferenceName { get => GetValue(ReferenceNameProperty); set => SetValue(ReferenceNameProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public bool ShowArea { get => GetValue(ShowAreaProperty); set => SetValue(ShowAreaProperty, value); }

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var labels = Labels;
        var values = Values;
        if (labels is null || values is null || labels.Count < 2 || values.Count != labels.Count)
            return;

        var n = labels.Count;
        var reference = Reference is { } r && r.Count == n ? r : null;
        var hasTarget = !double.IsNaN(Target);

        var muted = Res("TextMutedBrush");
        var secondary = Res("TextSecondaryBrush");
        var primary = Res("TextPrimaryBrush");
        var stroke = Stroke ?? Res("AccentBrush");

        // ---- Y domain -------------------------------------------------------------------
        var samples = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (reference is not null) samples.AddRange(reference);
        if (hasTarget) samples.Add(Target);
        if (samples.Count == 0) samples.Add(0);

        var lo = double.IsNaN(YMin) ? Math.Min(0, samples.Min()) : YMin;
        var hi = double.IsNaN(YMax) ? samples.Max() : YMax;
        if (hi <= lo) hi = lo + 1;
        var step = NiceStep(hi - lo, 4);
        if (double.IsNaN(YMax)) hi = Math.Ceiling(hi / step) * step;
        if (double.IsNaN(YMin)) lo = Math.Floor(lo / step) * step;

        var ticks = new List<(double Value, FormattedText Text)>();
        for (var v = lo; v <= hi + step * 1e-6; v += step)
            ticks.Add((v, Text(Format(v, TickFormat(step), ""), 11, muted)));

        // ---- Layout -----------------------------------------------------------------------
        var last = -1;
        for (var i = 0; i < n; i++)
            if (values[i].HasValue) last = i;

        var endLabel = last >= 0 ? Text(Format(values[last]!.Value, ValueFormat, Unit), 11.5, primary, FontWeight.SemiBold) : null;
        var left = ticks.Max(t => t.Text.Width) + 12;
        var right = endLabel is not null && last == n - 1 ? endLabel.Width + 14 : 10;
        var plot = new Rect(left, 10, Math.Max(10, bounds.Width - left - right), Math.Max(10, bounds.Height - 10 - 24));

        double X(int i) => plot.Left + plot.Width * i / (n - 1);
        double Y(double v) => plot.Bottom - (v - lo) / (hi - lo) * plot.Height;

        // ---- Grid & axes ------------------------------------------------------------------
        var gridPen = new Pen(Res("GridBrush"));
        var axisPen = new Pen(Res("AxisBrush"));
        foreach (var (value, text) in ticks)
        {
            var y = Math.Round(Y(value)) + 0.5;
            ctx.DrawLine(Math.Abs(value - lo) < 1e-9 ? axisPen : gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            ctx.DrawText(text, new Point(left - 10 - text.Width, y - text.Height / 2));
        }

        var xTexts = labels.Select(l => Text(l, 11, muted)).ToArray();
        var widest = xTexts.Max(t => t.Width);
        var every = Math.Max(1, (int)Math.Ceiling((widest + 14) / (plot.Width / (n - 1))));
        for (var i = 0; i < n; i += every)
        {
            var t = xTexts[i];
            var tx = Math.Clamp(X(i) - t.Width / 2, 0, bounds.Width - t.Width);
            ctx.DrawText(t, new Point(tx, plot.Bottom + 8));
        }

        // ---- Target line ------------------------------------------------------------------
        if (hasTarget && Target >= lo && Target <= hi)
        {
            var y = Math.Round(Y(Target)) + 0.5;
            ctx.DrawLine(new Pen(secondary, 1, new DashStyle([4, 3], 0)), new Point(plot.Left, y), new Point(plot.Right, y));
        }

        // ---- Reference (plan) -------------------------------------------------------------
        if (reference is not null)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(X(0), Y(reference[0])), false);
                for (var i = 1; i < n; i++)
                    g.LineTo(new Point(X(i), Y(reference[i])));
                g.EndFigure(false);
            }

            ctx.DrawGeometry(null, new Pen(secondary, 1.5, new DashStyle([3, 2.5], 0), PenLineCap.Flat, PenLineJoin.Round), geometry);
        }

        // ---- Main series ------------------------------------------------------------------
        var pts = new List<Point>();
        for (var i = 0; i <= last; i++)
            if (values[i] is { } v)
                pts.Add(new Point(X(i), Y(v)));

        if (pts.Count >= 2)
        {
            if (ShowArea)
            {
                var c = ColorOf(stroke);
                var fill = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(Color.FromArgb(58, c.R, c.G, c.B), 0),
                        new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
                    },
                };

                var area = new StreamGeometry();
                using (var g = area.Open())
                {
                    g.BeginFigure(new Point(pts[0].X, plot.Bottom), true);
                    g.LineTo(pts[0]);
                    MonotoneCurve(g, pts);
                    g.LineTo(new Point(pts[^1].X, plot.Bottom));
                    g.EndFigure(true);
                }

                ctx.DrawGeometry(fill, null, area);
            }

            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(pts[0], false);
                MonotoneCurve(g, pts);
                g.EndFigure(false);
            }

            ctx.DrawGeometry(null, new Pen(stroke, 2, null, PenLineCap.Round, PenLineJoin.Round), line);
        }

        if (pts.Count > 0 && endLabel is not null)
        {
            var end = pts[^1];
            DrawMarker(ctx, end, stroke);
            var labelPos = last == n - 1
                ? new Point(end.X + 10, end.Y - endLabel.Height / 2)
                : new Point(end.X - endLabel.Width / 2, end.Y - endLabel.Height - 10);
            labelPos = new Point(Math.Clamp(labelPos.X, 0, bounds.Width - endLabel.Width), Math.Max(0, labelPos.Y));
            // Surface halo keeps the label legible where it crosses the reference line or the grid.
            var halo = new Rect(labelPos, new Size(endLabel.Width, endLabel.Height)).Inflate(new Thickness(4, 1));
            ctx.DrawRectangle(Res("SurfaceBrush"), null, new RoundedRect(halo, 4));
            ctx.DrawText(endLabel, labelPos);
        }

        // ---- Hover ------------------------------------------------------------------------
        if (Hover is not { } hover || hover.X < plot.Left - 6 || hover.X > plot.Right + 6)
            return;

        var index = Math.Clamp((int)Math.Round((hover.X - plot.Left) / plot.Width * (n - 1)), 0, n - 1);
        var hx = Math.Round(X(index)) + 0.5;
        ctx.DrawLine(axisPen, new Point(hx, plot.Top), new Point(hx, plot.Bottom));

        var rows = new List<TipRow>();
        var anchorY = hover.Y;
        if (reference is not null)
        {
            DrawMarker(ctx, new Point(X(index), Y(reference[index])), secondary, 3);
            anchorY = Y(reference[index]);
        }

        if (values[index] is { } actual)
        {
            DrawMarker(ctx, new Point(X(index), Y(actual)), stroke);
            anchorY = Y(actual);
            rows.Add(new TipRow(SeriesName, Format(actual, ValueFormat, Unit), stroke));
        }
        else
        {
            rows.Add(new TipRow(SeriesName, "Pendiente", stroke));
        }

        if (reference is not null)
        {
            rows.Add(new TipRow(ReferenceName, Format(reference[index], ValueFormat, Unit), secondary, true));
            if (values[index] is { } a && reference[index] > 0)
            {
                var deviation = (a - reference[index]) / reference[index];
                rows.Add(new TipRow("Desvío", deviation.ToString("+0.0 %;−0.0 %;0.0 %", Culture)));
            }
        }

        if (hasTarget)
        {
            rows.Add(new TipRow(TargetLabel, Format(Target, ValueFormat, Unit), secondary, true));
            if (values[index] is { } a)
                rows.Add(new TipRow("Brecha", (a - Target).ToString("+0.0;−0.0;0.0", Culture) + " pts"));
        }

        DrawTooltip(ctx, new Point(X(index), anchorY), labels[index], rows);
    }

    private static string TickFormat(double step) => step >= 1 ? "N0" : step >= 0.1 ? "N1" : "N2";
}
