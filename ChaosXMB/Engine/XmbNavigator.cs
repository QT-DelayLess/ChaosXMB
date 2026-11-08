namespace ChaosXMB.Engine;

/// <summary>Single navigation state, time-independent animation, no storyboard/input lock.</summary>
public sealed class XmbNavigator
{
    public int CategoryIndex { get; private set; }
    public int ItemIndex { get; private set; }
    public double HorizontalOffset { get; private set; }
    public double VerticalOffset { get; private set; }
    private const double CategorySpacing = 305;
    private const double ItemSpacing = 94;
    private double _targetX, _targetY;

    public void SelectCategory(int index)
    {
        CategoryIndex = Math.Clamp(index, 0, 4);
        ItemIndex = 0;
        _targetX = -CategoryIndex * CategorySpacing;
        // New categories open at their first item immediately, rather than
        // inheriting the previous category's vertical scroll position.
        _targetY = 0;
        VerticalOffset = 0;
    }

    public void SelectItem(int index, int count)
    {
        ItemIndex = Math.Clamp(index, 0, Math.Max(0, count - 1));
        _targetY = -ItemIndex * ItemSpacing;
    }

    public void Tick(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.05);
        double blend = 1 - Math.Exp(-dt * 16.0);
        HorizontalOffset += (_targetX - HorizontalOffset) * blend;
        VerticalOffset += (_targetY - VerticalOffset) * blend;
        if (Math.Abs(_targetX - HorizontalOffset) < 0.05) HorizontalOffset = _targetX;
        if (Math.Abs(_targetY - VerticalOffset) < 0.05) VerticalOffset = _targetY;
    }
}
