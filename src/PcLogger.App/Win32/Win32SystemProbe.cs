using System.Runtime.InteropServices;
using System.Text;
using PcLogger.Core.Sampling;

namespace PcLogger.App.Win32;

/// <summary>
/// Reads presence without installing any input hook: GetLastInputInfo reports when
/// the user last touched keyboard or mouse, XInput covers gamepads, which Windows
/// deliberately excludes from GetLastInputInfo.
/// </summary>
public sealed class Win32SystemProbe : ISystemProbe
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    // Separate "have we sampled yet" flags rather than a 0 sentinel: 0 is a legal tick count
    // (GetTickCount wraps every 49.7 days) and a legal packet number, so the sentinel would
    // silently discard a real first reading — or accept a fake one.
    private bool _haveInput;
    private uint _lastInputTick;
    private readonly bool[] _havePad = new bool[4];
    private readonly uint[] _padPackets = new uint[4];

    public SystemSnapshot Sample()
    {
        var input = ReadInputChanged();
        var gamepad = ReadGamepadChanged();
        return new SystemSnapshot(input, gamepad, ReadForegroundPath());
    }

    private bool ReadInputChanged()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return false;

        var changed = _haveInput && info.dwTime != _lastInputTick;
        _lastInputTick = info.dwTime;
        _haveInput = true;
        return changed;
    }

    private bool ReadGamepadChanged()
    {
        var changed = false;
        for (uint slot = 0; slot < 4; slot++)
        {
            if (XInputGetState(slot, out var state) != 0)
            {
                // An empty slot is not an error. Forgetting its packet number matters though:
                // a pad unplugged and plugged back in restarts its counter, and comparing the
                // new number against the stale one reads as input the child never made.
                _havePad[slot] = false;
                continue;
            }

            if (_havePad[slot] && state.dwPacketNumber != _padPackets[slot]) changed = true;
            _padPackets[slot] = state.dwPacketNumber;
            _havePad[slot] = true;
        }
        return changed;
    }

    private static string? ReadForegroundPath()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return null;

        GetWindowThreadProcessId(window, out var pid);
        return ProcessPath(pid);
    }

    internal static string? ProcessPath(uint pid)
    {
        if (pid == 0) return null;

        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out XINPUT_STATE state);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
}
