using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>Compact trend line for stat tiles: de-emphasised line, current point in the accent.</summary>
public class Sparkline : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double>?>(nameof(Values));

    static Sparkline()
    {
        AffectsRender<Sparkline>(ValuesProperty);
    }

    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    protected override void RenderChart(DrawingContext ctx, Rect bounds)
    {
        var values = Values;
        if (values is null || values.Count < 2)
            return;

        const double pad = 5;
        var min = values.Min();
        var max = values.Max();
        var span = max - min < 1e-9 ? 1 : max - min;
        var plot = bounds.Deflate(new Thickness(1, pad, pad + 1, pad));

        var pts = values.Select((v, i) => new Point(
            plot.Left + plot.Width * i / (values.Count - 1),
            plot.Bottom - (v - min) / span * plot.Height)).ToList();

        var accent = Res("AccentBrush");
        var line = Res("TextMutedBrush");

        var area = new StreamGeometry();
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(pts[0].X, bounds.Bottom), true);
            g.LineTo(pts[0]);
            MonotoneCurve(g, pts);
            g.LineTo(new Point(pts[^1].X, bounds.Bottom));
            g.EndFigure(true);
        }

        ctx.DrawGeometry(WithAlpha(accent, 0.08), null, area);

        var path = new StreamGeometry();
        using (var g = path.Open())
        {
            g.BeginFigure(pts[0], false);
            MonotoneCurve(g, pts);
            g.EndFigure(false);
        }

        ctx.DrawGeometry(null, new Pen(line, 1.5, null, PenLineCap.Round, PenLineJoin.Round), path);
        DrawMarker(ctx, pts[^1], accent, 3);
    }
}
