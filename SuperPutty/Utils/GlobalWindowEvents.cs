using System;

namespace SuperPutty.Utils
{
    public class GlobalWindowEventArgs : EventArgs
    {
        public IntPtr hwnd {get; }
        public uint eventType {get; }

        public GlobalWindowEventArgs(IntPtr hwnd, uint eventType)
        {
            this.hwnd = hwnd;
            this.eventType = eventType;
        }
    }

    public class GlobalWindowEvents
    {
        public event EventHandler<GlobalWindowEventArgs> SystemSwitch;
        readonly IntPtr m_hWinEventHook;

        public GlobalWindowEvents()
        {
            uint eventMin = (uint)NativeMethods.WinEvents.EVENT_SYSTEM_SWITCHSTART;
            uint eventMax = (uint)NativeMethods.WinEvents.EVENT_SYSTEM_SWITCHEND;
            m_hWinEventHook = NativeMethods.SetWinEventHook(eventMin, eventMax, IntPtr.Zero, WinEventProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        }

        void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            SystemSwitch?.Invoke(this, new GlobalWindowEventArgs(hwnd, eventType));
        }

        ~GlobalWindowEvents()
        {
            NativeMethods.UnhookWinEvent(m_hWinEventHook);
        }
    }
}
