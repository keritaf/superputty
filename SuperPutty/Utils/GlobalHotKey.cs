using System;
using System.Linq;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Utils
{
    public class GlobalHotkey : IDisposable
    {
        public GlobalHotkey(Form form, KeyboardShortcut shortcut)
        {
            Form = form;
            Shortcut = shortcut;

            // convert the Keys to modifiers
            Modifiers = NativeMethods.HotKeysConstants.NOMOD;
            if (IsControlSet)
            {
                Modifiers += NativeMethods.HotKeysConstants.CTRL;
            }
            if (IsAltSet)
            {
                Modifiers += NativeMethods.HotKeysConstants.ALT;
            }
            if (IsShiftSet)
            {
                Modifiers += NativeMethods.HotKeysConstants.SHIFT;
            }

            // make uid
            Id = Shortcut.GetHashCode() ^ Form.Handle.ToInt32();

            Register();
        }

        public bool Register()
        {
            return NativeMethods.RegisterHotKey(Form.Handle, Id, Modifiers, (int) Shortcut.Key);
        }

        public void Dispose()
        {
            NativeMethods.UnregisterHotKey(Form.Handle, Id);
        }

        private static bool IsSet(Keys keys, params Keys[] modifiers)
        {
            return modifiers.Any(modifier => (keys & modifier) == modifier);
        }

        public bool IsControlSet => IsSet(Shortcut.Modifiers, Keys.Control);
        public bool IsAltSet => IsSet(Shortcut.Modifiers, Keys.Alt);
        public bool IsShiftSet => IsSet(Shortcut.Modifiers, Keys.Shift);

        public KeyboardShortcut Shortcut { get; private set; }
        public Form Form { get; private set; }
        public int Id { get; private set; }

        public int Modifiers { get; private set; }
        public Keys Key { get; private set; }
    }

}
