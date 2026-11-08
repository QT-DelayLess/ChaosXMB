using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ChaosXMB.Services;

/// <summary>Optional Xbox/XInput controller, graceful fallback if XInput is unavailable.</summary>
public sealed class XInputController
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LX, LY, RX, RY;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint Packet;
        public Gamepad Pad;
    }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint GetState(uint player, out State state);

    private ushort _priorButtons;
    private Key? _priorDirection;
    private DateTime _nextRepeatUtc;
    private bool _disabled;
    public event Action<Key>? Direction;
    public event Action? Confirm;
    public event Action? Cancel;

    public void Poll()
    {
        if (_disabled) return;
        try
        {
            if (GetState(0, out var state) != 0) { _priorButtons = 0; _priorDirection = null; return; }
            ushort buttons = state.Pad.Buttons;
            if ((buttons & 0x1000) != 0 && (_priorButtons & 0x1000) == 0) Confirm?.Invoke();
            if ((buttons & 0x2000) != 0 && (_priorButtons & 0x2000) == 0) Cancel?.Invoke();
            _priorButtons = buttons;
            Key? key = null;
            if ((buttons & 1) != 0 || state.Pad.LY > 17000) key = Key.Up;
            else if ((buttons & 2) != 0 || state.Pad.LY < -17000) key = Key.Down;
            else if ((buttons & 4) != 0 || state.Pad.LX < -17000) key = Key.Left;
            else if ((buttons & 8) != 0 || state.Pad.LX > 17000) key = Key.Right;
            if (key.HasValue && (key != _priorDirection || DateTime.UtcNow >= _nextRepeatUtc))
            {
                Direction?.Invoke(key.Value);
                _nextRepeatUtc = DateTime.UtcNow.AddMilliseconds(key == _priorDirection ? 135 : 260);
            }
            _priorDirection = key;
        }
        catch (DllNotFoundException) { _disabled = true; }
        catch (EntryPointNotFoundException) { _disabled = true; }
    }
}
