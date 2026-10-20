using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>One donut segment.</summary>
public sealed record DonutSlice(string Label, double Value, IBrush Brush);

/// <summary>
/// Part-to-whole donut with 2px surface gaps between segments. The centre shows the total,
/// or the hovered segment's value and share.
/// </summary>
public class DonutChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<DonutSlice>?> SlicesProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<DonutSlice>?>(nameof(Slices));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 18);

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(Caption), "total");

    static DonutChart()
    {
        AffectsRender<DonutChart>(SlicesProperty, ThicknessProperty, CaptionProperty);
    }

    public IReadOnlyList<DonutSlice>? Slices { get => GetValue(SlicesProperty); set => SetValue(SlicesProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var slices = Slices;
        if (slices is null || slices.Count == 0)
            return;

        var total = slices.Sum(s => s.Value);
        if (total <= 0)
            return;

        var center = bounds.Center;
        var radius = Math.Min(bounds.Width, bounds.Height) / 2 - Thickness / 2 - 4;
        if (radius <= 4)
            return;

        var hovered = HitSlice(slices, total, center, radius);
        var gapDegrees = slices.Count(s => s.Value > 0) > 1 ? 2.0 / radius * 180 / Math.PI : 0;

        var angle = -90.0;
        for (var i = 0; i < slices.Count; i++)
        {
            var sweep = slices[i].Value / total * 360;
            var drawnSweep = sweep - gapDegrees;
            if (drawnSweep > 0.1)
            {
                var thickness = hovered == i ? Thickness + 5 : Thickness;
                using (ctx.PushOpacity(hovered is { } h && h != i ? 0.45 : 1))
                    ctx.DrawGeometry(null, new Pen(slices[i].Brush, thickness),
                        ArcGeometry(center, radius, angle + gapDegrees / 2, drawnSweep));
            }

            angle += sweep;
        }

        var primary = Res("TextPrimaryBrush");
        var muted = Res("TextMutedBrush");
        string big, small;
        if (hovered is { } index)
        {
            big = slices[index].Value.ToString("N0", Culture);
            small = $"{slices[index].Label} · {(slices[index].Value / total).ToString("0 %", Culture)}";
        }
        else
        {
            big = total.ToString("N0", Culture);
            small = Caption;
        }

        var bigText = Text(big, Math.Clamp(radius * 0.42, 18, 34), primary, FontWeight.SemiBold);
        var smallText = Text(small, 11.5, muted);
        var inner = (radius - Thickness / 2) * 2 - 12;
        if (smallText.Width > inner)
            smallText = Text(small, 10.5, muted);

        var blockHeight = bigText.Height + smallText.Height;
        ctx.DrawText(bigText, new Point(center.X - bigText.Width / 2, center.Y - blockHeight / 2));
        ctx.DrawText(smallText, new Point(center.X - smallText.Width / 2, center.Y - blockHeight / 2 + bigText.Height));
    }

    private int? HitSlice(IReadOnlyList<DonutSlice> slices, double total, Point center, double radius)
    {
        if (Hover is not { } p)
            return null;

        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (Math.Abs(distance - radius) > Thickness / 2 + 6)
            return null;

        // Angle clockwise from 12 o'clock, 0..360.
        var degrees = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        var start = 0.0;
        for (var i = 0; i < slices.Count; i++)
        {
            var sweep = slices[i].Value / total * 360;
            if (degrees >= start && degrees < start + sweep)
                return i;
            start += sweep;
        }

        return null;
    }
}
