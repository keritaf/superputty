using System.Windows.Forms;
using SuperPutty.App;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Gui
{
    /// <summary>
    /// ToolWindow that supports an MRU tab switching
    /// </summary>
    public partial class ToolWindowDocument : ToolWindow
    {
        public ToolWindowDocument()
        {
            InitializeComponent();
            if (SuperPuTTY.MainForm == null) return;
            
            // Insert this panel into the list used for Ctrl-Tab handling.
            if (SuperPuTTY.MainForm.CurrentPanel == null)
            {
                // First panel to be created
                SuperPuTTY.MainForm.CurrentPanel = PreviousPanel = NextPanel = this;
            }
            else
            {
                // Other panels exist. Tie ourselves into list ahead of current panel.
                PreviousPanel = SuperPuTTY.MainForm.CurrentPanel;
                NextPanel = SuperPuTTY.MainForm.CurrentPanel.NextPanel;
                SuperPuTTY.MainForm.CurrentPanel.NextPanel = this;
                NextPanel.PreviousPanel = this;

                // We are now the current panel
                SuperPuTTY.MainForm.CurrentPanel = this;
            }
        }

        // Make this panel the current one. Remove from previous
        // position in list and re-add in front of current panel
        public void MakePanelCurrent()
        {
            if (SuperPuTTY.MainForm.CurrentPanel == this)
                return;

            // Remove ourselves from our position in chain
            PreviousPanel.NextPanel = NextPanel;
            NextPanel.PreviousPanel = PreviousPanel;

            PreviousPanel = SuperPuTTY.MainForm.CurrentPanel;
            NextPanel = SuperPuTTY.MainForm.CurrentPanel.NextPanel;
            SuperPuTTY.MainForm.CurrentPanel.NextPanel = this;
            NextPanel.PreviousPanel = this;

            SuperPuTTY.MainForm.CurrentPanel = this;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            if (SuperPuTTY.MainForm == null) return;

            // only 1 panel
            if (SuperPuTTY.MainForm.CurrentPanel == this && NextPanel == this && PreviousPanel == this)
            {
                SuperPuTTY.MainForm.CurrentPanel = null;
                return;
            }

            // Remove ourselves from our position in chain and set last active tab as current
            if (PreviousPanel != null)
            {
                PreviousPanel.NextPanel = NextPanel;
            }
            if (NextPanel != null)
            {
                NextPanel.PreviousPanel = PreviousPanel;
            }
            SuperPuTTY.MainForm.CurrentPanel = PreviousPanel;

            // manipulate tabs
            if (DockHandler.Pane != null)
            {
                int idx = DockHandler.Pane.Contents.IndexOf(this);
                if (idx > 0)
                {
                    IDockContent contentToActivate = DockHandler.Pane.Contents[idx - 1];
                    contentToActivate.DockHandler.Activate();
                }
            }
        }


        public ToolWindowDocument PreviousPanel { get; set; }
        public ToolWindowDocument NextPanel { get; set; }
    }
}
