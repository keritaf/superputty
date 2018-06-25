using System;
using System.Windows.Forms;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Make a text box focus and select all on mouse click, tab in, etc.
    /// http://stackoverflow.com/questions/97459/automatically-select-all-text-on-focus-in-winforms-textbox
    /// </summary>
    public class TextBoxFocusHelper : IDisposable
    {
        public TextBoxFocusHelper(TextBox txt)
        {
            TextBox = txt;
            TextBox.GotFocus += TextBox_GotFocus;
            TextBox.MouseUp += TextBox_MouseUp;
            TextBox.Leave += TextBox_Leave;
        }

        void TextBox_MouseUp(object sender, MouseEventArgs e)
        {
            // Web browsers like Google Chrome select the text on mouse up.
            // They only do it if the textbox isn't already focused,
            // and if the user hasn't selected all text.
            if (!alreadyFocused && TextBox.SelectionLength == 0)
            {
                alreadyFocused = true;
                TextBox.SelectAll();
            }
        }

        void TextBox_GotFocus(object sender, EventArgs e)
        {
            // Select all text only if the mouse isn't down.
            // This makes tabbing to the textbox give focus.
            if (Control.MouseButtons == MouseButtons.None)
            {
                TextBox.SelectAll();
                alreadyFocused = true;
            }
        }

        void TextBox_Leave(object sender, EventArgs e)
        {
            alreadyFocused = false;
        }

        public void Dispose()
        {
            TextBox.GotFocus -= TextBox_GotFocus;
            TextBox.MouseUp -= TextBox_MouseUp;
            TextBox.Leave -= TextBox_Leave;
            TextBox = null;
        }

        bool alreadyFocused;
        public TextBox TextBox { get; private set; }

    }
}
