using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DashAvalonia.Controls;

/// <summary>
/// Outline icon drawn from a 24×24 path, stroked with the inherited foreground so it follows
/// the text color of its container (hover, selection, theme).
/// </summary>
public class LineIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<LineIcon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LineIcon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LineIcon, double>(nameof(StrokeThickness), 1.6);

    static LineIcon()
    {
        AffectsRender<LineIcon>(DataProperty, ForegroundProperty, StrokeThicknessProperty);
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        if (scale <= 0)
            return;

        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
            context.DrawGeometry(null, new Pen(brush, StrokeThickness / scale, null, PenLineCap.Round, PenLineJoin.Round), data);
    }
}

/// <summary>Outline icon set (24×24 grid).</summary>
public static class Icons
{
    public static readonly Geometry Dashboard = Geometry.Parse("M3,3 H10 V12 H3 Z M14,3 H21 V8 H14 Z M14,12 H21 V21 H14 Z M3,16 H10 V21 H3 Z");
    public static readonly Geometry Factory = Geometry.Parse("M2,20 V9.5 L7,12.5 V9.5 L12,12.5 V9.5 L17,12.5 V4 H21 V20 Z M6,16.5 H8 M11,16.5 H13 M16,16.5 H18");
    public static readonly Geometry Activity = Geometry.Parse("M3,12 H7 L10,4 L14,20 L17,12 H21");
    public static readonly Geometry Clipboard = Geometry.Parse("M9,3 H15 V6 H9 Z M9,4.5 H6 V21 H18 V4.5 H15 M9,11 H15 M9,15 H13");
    public static readonly Geometry ShieldCheck = Geometry.Parse("M12,3 L19.5,6 V11.5 C19.5,16 16.3,19.3 12,21 C7.7,19.3 4.5,16 4.5,11.5 V6 Z M9,12 L11.2,14.2 L15.2,10");
    public static readonly Geometry Wrench = Geometry.Parse("M14.7,6.3 A1,1 0 0 0 14.7,7.7 L16.3,9.3 A1,1 0 0 0 17.7,9.3 L21.47,5.53 A6,6 0 0 1 13.53,13.47 L6.62,20.38 A2.12,2.12 0 0 1 3.62,17.38 L10.53,10.47 A6,6 0 0 1 18.47,2.53 Z");
    public static readonly Geometry Bell = Geometry.Parse("M6,8 A6,6 0 0 1 18,8 C18,15 21,17 21,17 H3 C3,17 6,15 6,8 Z M10.3,21 A1.94,1.94 0 0 0 13.7,21");
    public static readonly Geometry Package = Geometry.Parse("M21,8 L12,3 L3,8 V16 L12,21 L21,16 Z M3,8 L12,13 L21,8 M12,13 V21 M7.5,5.5 L16.5,10.5");
    public static readonly Geometry BarChart = Geometry.Parse("M3,3 V21 H21 M8,17 V12 M12.5,17 V7 M17,17 V10");
    public static readonly Geometry LineChart = Geometry.Parse("M3,3 V21 H21 M7,15 L11,10.5 L14,13.5 L20,7");
    public static readonly Geometry Table = Geometry.Parse("M3,4 H21 V20 H3 Z M3,9.5 H21 M3,15 H21 M9,9.5 V20");
    public static readonly Geometry Route = Geometry.Parse("M6,3 V15 M18,3 A3,3 0 1 1 18,9 A3,3 0 1 1 18,3 Z M6,15 A3,3 0 1 1 6,21 A3,3 0 1 1 6,15 Z M18,9 A9,9 0 0 1 9,18");
    public static readonly Geometry Sliders = Geometry.Parse("M4,6 H13 M17,6 H20 M15,4 V8 M4,12 H7 M11,12 H20 M9,10 V14 M4,18 H12 M16,18 H20 M14,16 V20");
    public static readonly Geometry Sun = Geometry.Parse("M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8 Z M12,2 V4 M12,20 V22 M4.93,4.93 L6.34,6.34 M17.66,17.66 L19.07,19.07 M2,12 H4 M20,12 H22 M6.34,17.66 L4.93,19.07 M19.07,4.93 L17.66,6.34");
    public static readonly Geometry Moon = Geometry.Parse("M12,3 A6,6 0 0 0 21,12 A9,9 0 1 1 12,3 Z");
    public static readonly Geometry AlertTriangle = Geometry.Parse("M10.29,3.86 L1.82,18 A2,2 0 0 0 3.53,21 H20.47 A2,2 0 0 0 22.18,18 L13.71,3.86 A2,2 0 0 0 10.29,3.86 Z M12,9 V13 M12,17 H12.01");
    public static readonly Geometry AlertOctagon = Geometry.Parse("M7.86,2 H16.14 L22,7.86 V16.14 L16.14,22 H7.86 L2,16.14 V7.86 Z M12,8 V12 M12,16 H12.01");
    public static readonly Geometry AlertCircle = Geometry.Parse("M12,2 A10,10 0 1 1 12,22 A10,10 0 1 1 12,2 Z M12,8 V12 M12,16 H12.01");
    public static readonly Geometry Check = Geometry.Parse("M20,6 L9,17 L4,12");
    public static readonly Geometry CheckCircle = Geometry.Parse("M22,11.08 V12 A10,10 0 1 1 16.07,2.86 M22,4 L12,14.01 L9,11.01");
    public static readonly Geometry Clock = Geometry.Parse("M12,2 A10,10 0 1 1 12,22 A10,10 0 1 1 12,2 Z M12,6 V12 L16,14");
    public static readonly Geometry PauseCircle = Geometry.Parse("M12,2 A10,10 0 1 1 12,22 A10,10 0 1 1 12,2 Z M10,9 V15 M14,9 V15");
    public static readonly Geometry Trash = Geometry.Parse("M3,6 H21 M8,6 V4 H16 V6 M19,6 L18,20 A2,2 0 0 1 16,22 H8 A2,2 0 0 1 6,20 L5,6 M10,11 V17 M14,11 V17");
    public static readonly Geometry Zap = Geometry.Parse("M13,2 L3,14 H12 L11,22 L21,10 H12 Z");
    public static readonly Geometry ArrowUpRight = Geometry.Parse("M7,17 L17,7 M8,7 H17 V16");
    public static readonly Geometry ArrowDownRight = Geometry.Parse("M7,7 L17,17 M17,8 V17 H8");
    public static readonly Geometry Calendar = Geometry.Parse("M4,5 H20 V21 H4 Z M16,3 V7 M8,3 V7 M4,11 H20");
}
