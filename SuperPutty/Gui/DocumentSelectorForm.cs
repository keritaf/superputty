/*
 * Copyright (c) 2009 - 2015 Jim Radford http://www.jimradford.com
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions: 
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.ComponentModel;
using System.Windows.Forms;
using log4net;
using SuperPutty.App;
using SuperPutty.Utils;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Gui
{
    public partial class DocumentSelectorForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentSelectorForm));

        private readonly DockPanel dockPanel;

        public DocumentSelectorForm(DockPanel dockPanel)
        {
            this.dockPanel = dockPanel;
            InitializeComponent();
            checkSendToVisible.Checked = SuperPuTTY.Settings.SendCommandsToVisibleOnly;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            var d = dockPanel;
            base.OnVisibleChanged(e);
            if (Visible)
            {
                // load docs into the ListView
                listViewDocs.Items.Clear();
                int i = 0;
                foreach (IDockContent doc in VisualOrderTabSwitchStrategy.GetDocuments(dockPanel))
                {
                    i++;
                    if (doc is CtlPuttyPanel pp)
                    {
                        string tabNum = pp == dockPanel.ActiveDocument ? i + "*" : i.ToString();
                        ListViewItem item = listViewDocs.Items.Add(tabNum);
                        item.SubItems.Add(new ListViewItem.ListViewSubItem(item, pp.Text));
                        item.SubItems.Add(new ListViewItem.ListViewSubItem(item, pp.Session.SessionId));
                        item.SubItems.Add(new ListViewItem.ListViewSubItem(item, pp.GetHashCode().ToString()));

                        item.Selected = IsDocumentSelected(pp);
                        item.Tag = pp;
                    }

                }
                BeginInvoke(new Action(delegate { listViewDocs.Focus(); }));
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Log.Debug("Cancel");
            Hide();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Log.Debug("OK");
            foreach (ListViewItem item in listViewDocs.Items)
            {
                ((CtlPuttyPanel)item.Tag).AcceptCommands = item.Selected;
            }
            DialogResult = DialogResult.OK;
            Hide();

            SuperPuTTY.Settings.SendCommandsToVisibleOnly = checkSendToVisible.Checked;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            e.Cancel = true;
            Hide();
        }

        public bool IsDocumentSelected(CtlPuttyPanel document)
        {
            bool selected = false;
            if (document?.Session != null)
            {
                selected = checkSendToVisible.Checked ? document.Visible : document.AcceptCommands;
            }
            return selected;
        }

        private void checkSendToVisible_CheckedChanged(object sender, EventArgs e)
        {
            listViewDocs.Enabled = !checkSendToVisible.Checked;
        }
    }
}
