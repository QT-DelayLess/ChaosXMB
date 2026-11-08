using System.Windows;
using System.Windows.Media;

namespace ChaosXMB.Controls;

public sealed class WaveRenderer : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private readonly Pen _front;
    private readonly Pen _back;
    public WaveRenderer()
    {
        IsHitTestVisible = false;
        AddVisualChild(_visual);
        _front = new Pen(new SolidColorBrush(Color.FromArgb(45, 207, 239, 255)), 1.6);
        _back = new Pen(new SolidColorBrush(Color.FromArgb(25, 175, 190, 240)), 2.7);
        _front.Freeze(); _back.Freeze();
    }
    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => index == 0 ? _visual : throw new ArgumentOutOfRangeException(nameof(index));

    public void Draw(double time, double cameraX, double width, double height)
    {
        if (width < 2 || height < 2) return;
        using var dc = _visual.RenderOpen();
        double mid = height * 0.58;
        var frontGeometry = new StreamGeometry();
        var backGeometry = new StreamGeometry();
        using (var front = frontGeometry.Open())
        using (var back = backGeometry.Open())
        {
            bool first = true;
            for (double x = -20; x <= width + 25; x += 42)
            {
                var a = new Point(x, mid + Math.Sin(x * .003 + time * .58) * 92 + Math.Cos(x * .004 - time * .37) * 35 - cameraX * .025);
                var b = new Point(x, mid + Math.Sin(x * .002 - time * .39) * 146 + Math.Cos(x * .0026 + time * .62) * 48 - cameraX * .038);
                if (first) { front.BeginFigure(a, false, false); back.BeginFigure(b, false, false); first = false; }
                else { front.LineTo(a, true, false); back.LineTo(b, true, false); }
            }
        }
        frontGeometry.Freeze(); backGeometry.Freeze();
        dc.DrawGeometry(null, _front, frontGeometry);
        dc.DrawGeometry(null, _back, backGeometry);
    }
}
