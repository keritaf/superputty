using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using log4net;

namespace SuperPutty.Gui
{
    public partial class QuickSelector : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(QuickSelector));
        private static readonly char[] sanitizeChars = { '*', '[', ',' };
        private static readonly char[] splitChars = { ' ', '.', '*' };

        public QuickSelector()
        {
            InitializeComponent();
        }

        void DoClose()
        {
            SelectedItem = null;
            DialogResult = DialogResult.Cancel;
        }

        void DoSelectItem()
        {
            DataGridViewSelectedRowCollection selectedRows = dataGridViewData.SelectedRows;
            if (selectedRows.Count == 1)
            {
                DataRowView row = (DataRowView) selectedRows[0].DataBoundItem;
                SelectedItem = (QuickSelectorData.ItemDataRow) row.Row;
                DialogResult = DialogResult.OK;
            }
        }

        void UpdateFilter()
        {
            DataView.RowFilter = String.IsNullOrEmpty(textBoxData.Text) ? String.Empty : FormatFilterString(textBoxData.Text);
            Text = string.Format("{0} [{1}]", Options.BaseText, DataView.Count);
        }

        string FormatFilterString(string text)
        {
            string filter = "";

            int i = 0;
            int tokenCount = cleanTokens(text.Split(splitChars)).Length;

            foreach (string token in cleanTokens(text.Split(splitChars)))
            {
                if (i > 0 && i < tokenCount)
                {
                    filter += " AND ";
                }

                filter += string.Format("([Name] LIKE '%{0}%' OR [Detail] LIKE '%{0}%')", tokenSanitize(token));

                i++;
            }

            return filter;
        }

        string[] cleanTokens(string[] tokens)
        {
            int i = tokens.Count(token => token.Length > 0);

            string[] result = new string[i];

            i = 0;
            foreach(string token in tokens)
            {
                if (token.Length > 0)
                {
                    result[i] = token;
                    i++;
                }
            }
            return result;
        }

        string tokenSanitize(string token)
        {
            return sanitizeChars.Aggregate(token, (current, sanitizeChar) => current.Replace(Convert.ToString(sanitizeChar), ""));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Log.InfoFormat("Closed - DialogResult={0}, SelectedItem={1}", 
                DialogResult, SelectedItem != null ? SelectedItem.Name + ":" + SelectedItem.Detail : "");
        }

        private void textBoxData_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    if (DataView.Count > 0)
                    {
                        DoSelectItem();
                    }
                    break;
                case Keys.Escape:
                    DoClose();
                    e.Handled = true;
                    break;
                case Keys.Down:
                    // focus grid and move selection down by 1 row if possible
                    dataGridViewData.Focus();
                    if (dataGridViewData.SelectedRows[0].Index == 0)
                    {
                        if (dataGridViewData.Rows.Count > 1)
                        {
                            dataGridViewData.CurrentCell = dataGridViewData.Rows[1].Cells[0];
                        }
                    }
                    e.Handled = true;
                    break;
                case Keys.Back:
                    if (e.Control && textBoxData.SelectionStart == textBoxData.Text.Length)
                    {
                        // delete word
                        int idx = textBoxData.Text.LastIndexOf("/", StringComparison.Ordinal);
                        if (idx != -1)
                        {
                            textBoxData.Text = textBoxData.Text.Substring(0, idx);
                            textBoxData.SelectionStart = textBoxData.Text.Length;
                        }
                        else
                        {
                            textBoxData.Text = "";
                        }
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;
            }
        }

        private void textBoxData_TextChanged(object sender, EventArgs e)
        {
            UpdateFilter();
        }

        private void dataGridViewData_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    DoSelectItem();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    DoClose();
                    e.Handled = true;
                    break;
                case Keys.Up:
                    if (dataGridViewData.Rows[0].Selected)
                    {
                        textBoxData.Focus();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void dataGridViewData_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            DoSelectItem();
        }

        private void dataGridViewData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                QuickSelectorData.ItemDataRow row = 
                    (QuickSelectorData.ItemDataRow) ((DataRowView)dataGridViewData.Rows[e.RowIndex].DataBoundItem).Row;
                if (!row.IsTextColorNull())
                {
                    e.CellStyle.ForeColor = (Color) row.TextColor;
                }
            }
        }

        readonly Brush highLighter = new SolidBrush(Color.FromArgb(120, 255, 255, 0));

        private void dataGridViewData_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (!Options.HighlightMatchingText) { return; }

            // draw default
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All);

            String txt = textBoxData.Text;
            if (txt.Length > 0)
            {
                String display = (String) e.FormattedValue;
                
                int idx = display.IndexOf(txt, StringComparison.Ordinal);
                if (idx != -1)
                {
                    String skipText = display.Substring(0, idx);
                    SizeF match = e.Graphics.MeasureString(txt, e.CellStyle.Font);
                    SizeF skip = e.Graphics.MeasureString(skipText, e.CellStyle.Font);

                    // highlight matching text
                    Rectangle newRect = new Rectangle(
                        e.CellBounds.X + (int) skip.Width,
                        (int) (e.CellBounds.Y + (e.CellBounds.Height - match.Height) / 2), 
                        (int) match.Width - 3, 
                        (int) match.Height + 2);
                    e.Graphics.FillRectangle(highLighter, newRect);
                }
            }

            e.Handled = true;
        }

        public DialogResult ShowDialog(IWin32Window parent, QuickSelectorData data, QuickSelectorOptions options)
        {
            // bind data
            Options = options;
            DataView = new DataView(data.ItemData) {Sort = options.Sort};
            dataGridViewData.DataSource = DataView;

            // configure grid
            nameDataGridViewTextBoxColumn.Visible = Options.ShowNameColumn;
            detailDataGridViewTextBoxColumn.Visible = Options.ShowDetailColumn;
            if (Options.ShowDetailColumn && !Options.ShowNameColumn)
            {
                detailDataGridViewTextBoxColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }

            // update title
            UpdateFilter();            
            return ShowDialog(parent);
        }

        DataView DataView { get; set; }
        public QuickSelectorOptions Options { get; private set; }
        public QuickSelectorData.ItemDataRow SelectedItem { get; private set; }
    }
    public class QuickSelectorOptions
    {
        public QuickSelectorOptions()
        {
            ShowDetailColumn = true;
            BaseText = "Select Item";
        }
        public bool ShowNameColumn { get; set; }
        public bool ShowDetailColumn { get; set; }
        public string BaseText { get; set; }
        public string Sort { get; set; }
        public bool HighlightMatchingText { get; set; }
    }
}
