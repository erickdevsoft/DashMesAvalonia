using System;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>
/// 270° meter for a 0–100 % figure. The fill carries severity (accent → warning → critical)
/// over a lighter track of the same hue; an optional tick marks the target.
/// </summary>
public class RingGauge : ChartBase
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RingGauge, double>(nameof(Value));

    public static readonly StyledProperty<double> TargetProperty =
        AvaloniaProperty.Register<RingGauge, double>(nameof(Target), double.NaN);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<RingGauge, double>(nameof(Thickness), 12);

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<RingGauge, string>(nameof(Caption), "");

    public static readonly StyledProperty<double> WarningBelowProperty =
        AvaloniaProperty.Register<RingGauge, double>(nameof(WarningBelow), 75);

    public static readonly StyledProperty<double> CriticalBelowProperty =
        AvaloniaProperty.Register<RingGauge, double>(nameof(CriticalBelow), 60);

    static RingGauge()
    {
        AffectsRender<RingGauge>(ValueProperty, TargetProperty, ThicknessProperty, CaptionProperty,
            WarningBelowProperty, CriticalBelowProperty);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public double WarningBelow { get => GetValue(WarningBelowProperty); set => SetValue(WarningBelowProperty, value); }
    public double CriticalBelow { get => GetValue(CriticalBelowProperty); set => SetValue(CriticalBelowProperty, value); }

    private const double StartAngle = 135, Sweep = 270;

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var center = bounds.Center;
        var radius = Math.Min(bounds.Width, bounds.Height) / 2 - Thickness / 2 - 2;
        if (radius <= 4)
            return;

        var value = Math.Clamp(Value, 0, 100);
        var fill = value < CriticalBelow ? Res("StatusCriticalBrush")
            : value < WarningBelow ? Res("StatusWarningBrush")
            : Res("AccentBrush");

        ctx.DrawGeometry(null, new Pen(WithAlpha(fill, 0.18), Thickness, null, PenLineCap.Round),
            ArcGeometry(center, radius, StartAngle, Sweep));
        if (value > 0.1)
            ctx.DrawGeometry(null, new Pen(fill, Thickness, null, PenLineCap.Round),
                ArcGeometry(center, radius, StartAngle, Sweep * value / 100));

        if (!double.IsNaN(Target))
        {
            var rad = (StartAngle + Sweep * Math.Clamp(Target, 0, 100) / 100) * Math.PI / 180;
            var dir = new Vector(Math.Cos(rad), Math.Sin(rad));
            var inner = radius - Thickness / 2 - 3;
            var outer = radius + Thickness / 2 + 3;
            ctx.DrawLine(new Pen(Res("TextPrimaryBrush"), 2, null, PenLineCap.Round),
                center + dir * inner, center + dir * outer);
        }

        var primary = Res("TextPrimaryBrush");
        var number = Text(value.ToString("0.0", Culture), Math.Clamp(radius * 0.5, 16, 34), primary, FontWeight.SemiBold);
        var percent = Text("%", Math.Clamp(radius * 0.24, 10, 16), Res("TextSecondaryBrush"), FontWeight.Medium);
        var caption = Text(Caption, 11.5, Res("TextMutedBrush"), FontWeight.Medium);

        var rowWidth = number.Width + 2 + percent.Width;
        var top = center.Y - (number.Height + caption.Height) / 2;
        var x = center.X - rowWidth / 2;
        ctx.DrawText(number, new Point(x, top));
        ctx.DrawText(percent, new Point(x + number.Width + 2, top + number.Baseline - percent.Baseline));
        ctx.DrawText(caption, new Point(center.X - caption.Width / 2, top + number.Height));
    }
}
