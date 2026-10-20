using System;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>Pill-shaped progress meter; the unfilled track is a light step of the fill hue.</summary>
public class MeterBar : ChartBase
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MeterBar, IBrush?>(nameof(Fill));

    static MeterBar()
    {
        AffectsRender<MeterBar>(ValueProperty, FillProperty);
    }

    /// <summary>Fraction filled, 0..1.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var fill = Fill ?? Res("AccentBrush");
        var radius = bounds.Height / 2;
        ctx.DrawRectangle(WithAlpha(fill, 0.18), null, new RoundedRect(bounds, radius));

        var value = Math.Clamp(Value, 0, 1);
        if (value <= 0)
            return;

        var width = Math.Max(bounds.Height, bounds.Width * value);
        ctx.DrawRectangle(fill, null, new RoundedRect(new Rect(0, 0, width, bounds.Height), radius));
    }
}
