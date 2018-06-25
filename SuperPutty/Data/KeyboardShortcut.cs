using System.Text;
using System.Windows.Forms;

namespace SuperPutty.Data
{
    public class KeyboardShortcut
    {
        public string Name { get;set; }

        public Keys Key { get; set; }
        public Keys Modifiers { get; set; }

        public void Clear()
        {
            Key = Keys.None;
            Modifiers = Keys.None;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (Name != null){
                hash ^= Name.GetHashCode();
            }
            hash ^= Key.GetHashCode();
            hash ^= Modifiers.GetHashCode();

            return hash;
        }

        public override bool Equals(object thatObj)
        {
            return thatObj is KeyboardShortcut that &&
                Name == that.Name &&
                Key == that.Key &&
                Modifiers == that.Modifiers;
        }

        public string ShortcutString
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (Modifiers != Keys.None)
                {
                    AppendIfSet(sb, Modifiers, Keys.Control, "Ctrl");
                    AppendIfSet(sb, Modifiers, Keys.Alt, "Alt");
                    AppendIfSet(sb, Modifiers, Keys.Shift, "Shift");
                }
                if (Key != Keys.None)
                {
                    sb.Append(Key);
                }
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            return string.Format("[Shortcut name={0}, key={1}, modifiers={2}]", Name, Key, Modifiers);
        }

        #region Utils

        public static KeyboardShortcut FromKeys(Keys keys)
        {
            KeyboardShortcut ks = new KeyboardShortcut();

            // check for modifers and remove from val
            if (IsSet(keys, Keys.Control)) 
            {  
                ks.Modifiers |= Keys.Control;
                keys ^= Keys.Control;
            }
            if (IsSet(keys, Keys.Alt)) 
            { 
                ks.Modifiers |= Keys.Alt;
                keys ^= Keys.Alt;
            }
            if (IsSet(keys, Keys.Shift)) 
            { 
                ks.Modifiers |= Keys.Shift;
                keys ^= Keys.Shift;
            }

            // remaining should be the key
            ks.Key = keys;

            return ks;
        }

        static void AppendIfSet(StringBuilder sb, Keys modifers, Keys key, string keyText)
        {
            if (IsSet(modifers, key))
            {
                sb.Append(keyText).Append("+");
            }
        }

        static bool IsSet(Keys modifiers, Keys key)
        {
            return (modifiers & key) == key;
        }

        #endregion
    }
}
