using System;
using System.Windows.Forms;
using SuperPutty.Data;

namespace SuperPutty.Gui
{
    public partial class KeyboardShortcutEditor : Form
    {
        KeyboardShortcut KeyboardShortcut { get; }

        public KeyboardShortcutEditor()
        {
            InitializeComponent();
            KeyboardShortcut = new KeyboardShortcut();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            BeginInvoke(new Action(DoFocus));
        }

        void DoFocus()
        {
            textBoxKeys.Focus();
        }

        /// <summary>
        /// Show dialog to edit shortcut
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="shortcut"></param>
        /// <returns>return null if canceled</returns>
        public DialogResult ShowDialog(IWin32Window parent, KeyboardShortcut shortcut)
        {
            // init values
            Text = string.Format(LocalizedText.KeyboardShortcutEditor_Edit_shortcut, shortcut.Name);
            KeyboardShortcut.Key = shortcut.Key;
            KeyboardShortcut.Modifiers = shortcut.Modifiers;
            textBoxKeys.Text = KeyboardShortcut.ShortcutString;

            // show dialog
            DialogResult result = ShowDialog(parent);
            if (result == DialogResult.OK)
            {
                // update values
                shortcut.Key = KeyboardShortcut.Key;
                shortcut.Modifiers = KeyboardShortcut.Modifiers;
            }

            return result;
        }

        private void textBoxKeys_KeyDown(object sender, KeyEventArgs e)
        {
            KeyboardShortcut.Key = e.KeyCode;
            KeyboardShortcut.Modifiers = e.Modifiers;

            textBoxKeys.Text = KeyboardShortcut.ShortcutString;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            KeyboardShortcut.Key = Keys.None;
            KeyboardShortcut.Modifiers = Keys.None;
            textBoxKeys.Text = KeyboardShortcut.ShortcutString;
        }


    }
}
