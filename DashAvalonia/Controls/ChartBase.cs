using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace DashAvalonia.Controls;

/// <summary>A row in a chart tooltip: optional marker, label and formatted value.</summary>
public readonly record struct TipRow(string Label, string Value, IBrush? Swatch = null, bool Dashed = false);

/// <summary>
/// Base class for the dashboard charts: theme-aware brush lookup, text helpers,
/// pointer tracking for hover and a shared tooltip renderer.
/// </summary>
public abstract class ChartBase : Control
{
    protected ChartBase()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Pointer position while hovering, in local coordinates.</summary>
    protected Point? Hover { get; private set; }

    protected static CultureInfo Culture => CultureInfo.CurrentCulture;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Hover = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover = null;
        InvalidateVisual();
    }

    public sealed override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 1 || bounds.Height < 1)
            return;

        // Transparent fill so empty regions of the plot still receive pointer events.
        context.FillRectangle(Brushes.Transparent, bounds);
        RenderChart(context, bounds);
    }

    protected abstract void RenderChart(DrawingContext ctx, Rect bounds);

    protected IBrush Res(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    protected FormattedText Text(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        new(text, Culture, FlowDirection.LeftToRight,
            new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, weight), size, brush);

    protected static string Format(double value, string format, string unit) =>
        value.ToString(format, Culture) + unit;

    protected static Color ColorOf(IBrush brush) => brush is ISolidColorBrush s ? s.Color : Colors.Gray;

    protected static IBrush WithAlpha(IBrush brush, double alpha)
    {
        var c = ColorOf(brush);
        return new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B));
    }

    /// <summary>Picks a "nice" tick step (1, 2, 5 × 10ⁿ) for the given range.</summary>
    protected static double NiceStep(double range, int targetTicks)
    {
        if (range <= 0) return 1;
        var raw = range / targetTicks;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var nice = normalized < 1.5 ? 1 : normalized < 3 ? 2 : normalized < 7 ? 5 : 10;
        return nice * magnitude;
    }

    /// <summary>Appends a monotone cubic curve through <paramref name="pts"/> (no overshoot between samples).</summary>
    protected static void MonotoneCurve(StreamGeometryContext g, IReadOnlyList<Point> pts)
    {
        var n = pts.Count;
        if (n < 2) return;
        if (n == 2)
        {
            g.LineTo(pts[1]);
            return;
        }

        var dx = new double[n - 1];
        var slope = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
        {
            dx[i] = pts[i + 1].X - pts[i].X;
            slope[i] = dx[i] == 0 ? 0 : (pts[i + 1].Y - pts[i].Y) / dx[i];
        }

        var tangent = new double[n];
        tangent[0] = slope[0];
        tangent[n - 1] = slope[n - 2];
        for (var i = 1; i < n - 1; i++)
        {
            tangent[i] = slope[i - 1] * slope[i] <= 0
                ? 0
                : 3 * (dx[i - 1] + dx[i]) /
                  ((2 * dx[i] + dx[i - 1]) / slope[i - 1] + (dx[i] + 2 * dx[i - 1]) / slope[i]);
        }

        for (var i = 0; i < n - 1; i++)
        {
            var h = dx[i] / 3;
            g.CubicBezierTo(
                new Point(pts[i].X + h, pts[i].Y + tangent[i] * h),
                new Point(pts[i + 1].X - h, pts[i + 1].Y - tangent[i + 1] * h),
                pts[i + 1]);
        }
    }

    /// <summary>A bar whose data end (top) is rounded and whose baseline end is square.</summary>
    protected static Geometry ColumnGeometry(Rect r, double radius)
    {
        radius = Math.Min(radius, Math.Min(r.Width / 2, r.Height));
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(r.Left, r.Bottom), true);
        g.LineTo(new Point(r.Left, r.Top + radius));
        g.ArcTo(new Point(r.Left + radius, r.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        g.LineTo(new Point(r.Right - radius, r.Top));
        g.ArcTo(new Point(r.Right, r.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        g.LineTo(new Point(r.Right, r.Bottom));
        g.EndFigure(true);
        return geometry;
    }

    /// <summary>A horizontal bar rounded at its data end (right) and square at the baseline (left).</summary>
    protected static Geometry BarGeometry(Rect r, double radius)
    {
        radius = Math.Min(radius, Math.Min(r.Height / 2, r.Width));
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(r.Left, r.Top), true);
        g.LineTo(new Point(r.Right - radius, r.Top));
        g.ArcTo(new Point(r.Right, r.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        g.LineTo(new Point(r.Right, r.Bottom - radius));
        g.ArcTo(new Point(r.Right - radius, r.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        g.LineTo(new Point(r.Left, r.Bottom));
        g.EndFigure(true);
        return geometry;
    }

    /// <summary>Open arc on a circle, angles in degrees measured clockwise from 3 o'clock.</summary>
    protected static Geometry ArcGeometry(Point center, double radius, double startDeg, double sweepDeg)
    {
        static Point On(Point c, double r, double deg)
        {
            var rad = deg * Math.PI / 180;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        if (sweepDeg >= 359.99)
        {
            // A single ArcTo cannot close a full circle; draw it as two halves.
            g.BeginFigure(On(center, radius, startDeg), false);
            g.ArcTo(On(center, radius, startDeg + 180), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            g.ArcTo(On(center, radius, startDeg + 360), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            g.EndFigure(false);
            return geometry;
        }

        g.BeginFigure(On(center, radius, startDeg), false);
        g.ArcTo(On(center, radius, startDeg + sweepDeg), new Size(radius, radius), 0, sweepDeg > 180,
            SweepDirection.Clockwise);
        g.EndFigure(false);
        return geometry;
    }

    /// <summary>Draws a small end/hover marker with a ring in the surface color.</summary>
    protected void DrawMarker(DrawingContext ctx, Point p, IBrush fill, double radius = 4)
    {
        ctx.DrawEllipse(Res("SurfaceBrush"), null, p, radius + 2, radius + 2);
        ctx.DrawEllipse(fill, null, p, radius, radius);
    }

    /// <summary>Draws a tooltip card next to <paramref name="anchor"/>, kept inside the control bounds.</summary>
    protected void DrawTooltip(DrawingContext ctx, Point anchor, string title, IReadOnlyList<TipRow> rows)
    {
        var primary = Res("TextPrimaryBrush");
        var secondary = Res("TextSecondaryBrush");

        const double pad = 10, rowHeight = 20, gap = 16;
        var titleText = Text(title, 11.5, secondary, FontWeight.SemiBold);
        var labels = rows.Select(r => Text(r.Label, 12, secondary)).ToList();
        var values = rows.Select(r => Text(r.Value, 12, primary, FontWeight.SemiBold)).ToList();
        var swatchWidth = rows.Any(r => r.Swatch is not null) ? 16 : 0;

        var rowsWidth = rows.Count == 0 ? 0 : swatchWidth + labels.Max(t => t.Width) + gap + values.Max(t => t.Width);
        var width = Math.Ceiling(Math.Max(titleText.Width, rowsWidth) + pad * 2);
        var height = Math.Ceiling(pad * 2 + titleText.Height + (rows.Count > 0 ? 4 + rows.Count * rowHeight : 0));

        var x = anchor.X + 14;
        if (x + width > Bounds.Width) x = anchor.X - 14 - width;
        x = Math.Max(0, x);
        var y = Math.Clamp(anchor.Y - height / 2, 0, Math.Max(0, Bounds.Height - height));

        var rect = new Rect(x, y, width, height);
        var shadow = new BoxShadows(new BoxShadow { OffsetY = 6, Blur = 18, Color = Color.FromArgb(56, 0, 0, 0) });
        ctx.DrawRectangle(Res("TooltipBrush"), new Pen(Res("HairlineBrush")), new RoundedRect(rect, 8), shadow);
        ctx.DrawText(titleText, new Point(x + pad, y + pad));

        var rowY = y + pad + titleText.Height + 4;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var cy = rowY + rowHeight / 2;
            if (row.Swatch is { } swatch)
            {
                if (row.Dashed)
                    ctx.DrawLine(new Pen(swatch, 2, new DashStyle([1.5, 1.2], 0)),
                        new Point(x + pad, cy), new Point(x + pad + 10, cy));
                else
                    ctx.DrawEllipse(swatch, null, new Point(x + pad + 4, cy), 4, 4);
            }

            ctx.DrawText(labels[i], new Point(x + pad + swatchWidth, cy - labels[i].Height / 2));
            ctx.DrawText(values[i], new Point(x + width - pad - values[i].Width, cy - values[i].Height / 2));
            rowY += rowHeight;
        }
    }
}
