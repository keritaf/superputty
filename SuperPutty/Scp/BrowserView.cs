using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using log4net;
using SuperPutty.Gui;
using System.IO;

namespace SuperPutty.Scp
{
    /// <summary>
    /// Notes:
    /// 
    /// MVPVM
    /// http://www.codeproject.com/Articles/88390/MVP-VM-Model-View-Presenter-ViewModel-with-Data-Bi
    /// DragDrop To Desktop
    /// http://blogs.msdn.com/b/delay/archive/2009/10/26/creating-something-from-nothing-developer-friendly-virtual-file-implementation-for-net.aspx
    /// http://www.codeproject.com/Articles/23139/Transferring-Virtual-Files-to-Windows-Explorer-in
    /// </summary>
    public partial class BrowserView : UserControl 
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BrowserView));

        bool initialized;
        public BrowserView(IBrowserPresenter presenter, BrowserFileInfo startingDir) : this()
        {
            Initialize(presenter, startingDir);
        }

        public BrowserView()
        {
            InitializeComponent();

            Comparer = new BrowserFileInfoComparer();
            listViewFiles.ListViewItemSorter = Comparer;
        }

        public void Initialize(IBrowserPresenter presenter, BrowserFileInfo startingDir)
        {
            if (initialized) return;

            Presenter = presenter;
            Presenter.AuthRequest += Presenter_AuthRequest;
            Bind(Presenter.ViewModel);

            Presenter.LoadDirectory(startingDir);
            ConfirmTransfer = true;
            initialized = true;
        }

        void Presenter_AuthRequest(object sender, AuthEventArgs e)
        {
            // present login
            using (LoginDialog loginDialog = new LoginDialog(e.UserName))
            {
                loginDialog.StartPosition = FormStartPosition.CenterParent;
                if (loginDialog.ShowDialog(this) == DialogResult.OK)
                {
                    e.UserName = loginDialog.Username;
                    e.Password = loginDialog.Password;
                    e.Handled = true;
                }
                else
                {
                    Log.InfoFormat("Login canceled.  Closing parent form");
                    ParentForm.Close();
                }
            }
        }

        #region Data Binding
        void Bind(IBrowserViewModel model)
        {
            // Bind the controls
            bindingSource.DataSource = model;

            // can't bind toolbar
            toolStripLabelName.Text = Presenter.ViewModel.Name;

            // Ugh, ListView not bindable, do it manually
            PopulateListView(model.Files);
            model.Files.ListChanged += Files_ListChanged;
            model.PropertyChanged += (s, e) => EnableDisableControls(model.BrowserState);
        }

        void EnableDisableControls(BrowserState state)
        {
            bool enabled = state == BrowserState.Ready;
            tsBtnRefresh.Enabled = enabled;
            listViewFiles.Enabled = enabled;
        }

        void PopulateListView(BindingList<BrowserFileInfo> files)
        {
            listViewFiles.BeginUpdate();
            listViewFiles.Items.Clear();
            listViewFiles.ListViewItemSorter = null;

            foreach (BrowserFileInfo file in files)
            {
                string sizeKB = file.Type == FileType.File ? (file.Size / 1024).ToString("#,##0 KB") : String.Empty;
                ListViewItem addedItem = listViewFiles.Items.Add(file.Name, file.Name);
                addedItem.Tag = file;
                addedItem.ImageIndex = file.Type == FileType.ParentDirectory 
                    ? 2 
                    : file.Type == FileType.Directory ? 1 : 0;
                addedItem.SubItems.Add(new ListViewItem.ListViewSubItem(addedItem, sizeKB));
                addedItem.SubItems.Add(new ListViewItem.ListViewSubItem(addedItem, file.LastModTime.ToString("yyyy-MM-dd  h:mm:ss tt")));
                addedItem.SubItems.Add(new ListViewItem.ListViewSubItem(addedItem, file.Permissions));
                addedItem.SubItems.Add(new ListViewItem.ListViewSubItem(addedItem, file.Owner));
                addedItem.SubItems.Add(new ListViewItem.ListViewSubItem(addedItem, file.Group));
            }

            listViewFiles.EndUpdate();
            listViewFiles.ListViewItemSorter = Comparer;
        }

        void Files_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.Reset)
            {
                BindingList<BrowserFileInfo> list = (BindingList<BrowserFileInfo>)sender;
                PopulateListView(list);
            }
        }
        #endregion

        #region ListView Sorting 

        private void listViewFiles_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Get BrowserFileInfo propertyName
            ColumnHeader header = listViewFiles.Columns[e.Column];

            // Do Sort
            listViewFiles.Sorting = listViewFiles.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            Comparer.Column = e.Column;
            Comparer.SortOrder = listViewFiles.Sorting;

            Log.InfoFormat("Sorting ListView: field={0}, dir={1}", header.Text, Comparer.SortOrder);
            listViewFiles.Sort();
            listViewFiles.SetSortIcon(e.Column, listViewFiles.Sorting);
        }

        public class BrowserFileInfoComparer : IComparer
        {
            public int Column { get; set; }
            public SortOrder SortOrder { get; set; }

            public int Compare(object x, object y)
            {
                ListViewItem lviX = (ListViewItem)x;
                ListViewItem lviY = (ListViewItem)y;
                BrowserFileInfo a = (BrowserFileInfo)lviX.Tag;
                BrowserFileInfo b = (BrowserFileInfo)lviY.Tag;

                // direction
                int dir = SortOrder == SortOrder.Descending ? -1 : 1;

                // identity
                if (a == b) return 0;

                // preference based on type
                int type = a.Type.CompareTo(b.Type);
                if (type != 0) { return type; }

                // resolve based on field
                switch (Column)
                {
                    case 1: return dir * Comparer<long>.Default.Compare(a.Size, b.Size);
                    case 2: return dir * Comparer<DateTime>.Default.Compare(a.LastModTime, b.LastModTime);
                    case 3: return dir * Comparer<string>.Default.Compare(a.Permissions, b.Permissions);
                    case 4: return dir * Comparer<string>.Default.Compare(a.Owner, b.Owner);
                    case 5: return dir * Comparer<string>.Default.Compare(a.Group, b.Group);
                }

                // default to using name 
                return dir * Comparer<string>.Default.Compare(a.Name, b.Name);
            }
        }
        
        #endregion

        #region ListView View Modes
        private void detailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckViewModeItem((ToolStripMenuItem)sender);
            listViewFiles.View = View.Details;
        }

        private void smallIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckViewModeItem((ToolStripMenuItem)sender);
            listViewFiles.View = View.SmallIcon;
        }

        private void largeIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckViewModeItem((ToolStripMenuItem)sender);
            listViewFiles.View = View.LargeIcon;
        }

        private void tileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckViewModeItem((ToolStripMenuItem)sender);
            listViewFiles.View = View.Tile;
        }

        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckViewModeItem((ToolStripMenuItem)sender);
            listViewFiles.View = View.List;
        }

        void CheckViewModeItem(ToolStripMenuItem itemToSelect)
        {
            foreach (ToolStripMenuItem item in toolStripSplitButtonView.DropDownItems)
            {
                item.Checked = false;
            }
            itemToSelect.Checked = true;
        }
        #endregion

        #region ListView DragDrop

        int dragDropLastX = -1;
        int dragDropLastY = -1;
        bool dragDropIsValid;
        FileTransferRequest dragDropFileTransfer;

        private void listViewFiles_DragEnter(object sender, DragEventArgs e)
        {
            // Check for copy allowed, payload = DataFormats.FileDrop or BrowserFileInfo
            bool copyAllowed = (e.AllowedEffect & DragDropEffects.Copy) == DragDropEffects.Copy;
            bool isFile = e.Data.GetDataPresent(DataFormats.FileDrop, false);
            bool isBrowserFile = e.Data.GetDataPresent(typeof(BrowserFileInfo[]));
            dragDropIsValid = copyAllowed && ( isFile || isBrowserFile);

            // update effect
            e.Effect = dragDropIsValid ? DragDropEffects.Copy : DragDropEffects.None;

            // parse out payload
            if (dragDropIsValid)
            {
                dragDropFileTransfer = new FileTransferRequest { Session = Presenter.Session };

                // Get source files (payload)
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // windows files
                    Array files = (Array)e.Data.GetData(DataFormats.FileDrop, false);
                    foreach (string fileName in files)
                    {
                        BrowserFileInfo item = Directory.Exists(fileName)
                            ? new BrowserFileInfo(new DirectoryInfo(fileName))
                            : new BrowserFileInfo(new FileInfo(fileName));
                        dragDropFileTransfer.SourceFiles.Add(item);
                    }
                }
                else if (e.Data.GetDataPresent(typeof(BrowserFileInfo[])))
                {
                    // from another browser
                    BrowserFileInfo[] files = (BrowserFileInfo[])e.Data.GetData(typeof(BrowserFileInfo[]));
                    dragDropFileTransfer.SourceFiles.AddRange(files);
                }
            }

            Log.DebugFormat(
                "DragEnter: allowedEffect={0}, effect={1}, isFile={2}, isBrowserFile={3}",
                e.AllowedEffect, e.Effect, isFile, isBrowserFile);
        }

        private void listViewFiles_DragLeave(object sender, EventArgs e) 
        {
            ResetDragDrop();
        }

        private void listViewFiles_DragOver(object sender, DragEventArgs e)
        {
            // Prevent event from firing too often
            if (!dragDropIsValid || (e.X == dragDropLastX && e.Y == dragDropLastY))
            {
                return;
            }
            dragDropLastX = e.X;
            dragDropLastY = e.Y;

            // Get item under mouse
            ListView listView = (ListView) sender;
            Point p = listView.PointToClient(new Point(e.X, e.Y));
            ListViewHitTestInfo hti = listView.HitTest(p.X, p.Y);

            BrowserFileInfo target = hti.Item != null ? (BrowserFileInfo) hti.Item.Tag : Presenter.CurrentPath;
            dragDropFileTransfer.TargetFile = target;

            // Clear selection and select item under mouse if folder
            listView.SelectedItems.Clear();
            if (hti.Item != null && target.Type == FileType.Directory)
            {
                hti.Item.Selected = true;
            }

            // Validate source/targets and update effect
            // - Windows file to remote panel (do transfer)
            // - Windows file to Local (do transfer?)
            // - Local BrowserFileInfo to Remote (do transfer)
            // - Remote BrowserFileInfo to Local (do transfer)
            // Ask model if allowed?
            // - Local BrowserFileInfo to Local (not allowed)
            // - Remote BrowserFileInfo to Remote (not allowed)
            e.Effect = DragDropEffects.Copy;
            foreach (BrowserFileInfo source in dragDropFileTransfer.SourceFiles)
            {
                if (!Presenter.CanTransferFile(source, target))
                {
                    e.Effect = DragDropEffects.None;
                    break;
                }
            }
        }

        private void listViewFiles_DragDrop(object sender, DragEventArgs e)
        {
            Log.InfoFormat("DragDrop: valid={0}, effect={1}", dragDropIsValid, e.Effect);

            // Ask for confirmation
            if (ConfirmTransfer)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Source Files:");
                foreach (BrowserFileInfo source in dragDropFileTransfer.SourceFiles)
                {
                    sb.AppendLine(source.Path);
                }
                sb.AppendLine();
                sb.AppendLine("Target:");
                sb.AppendLine(dragDropFileTransfer.TargetFile.Path);
                sb.AppendLine();

                DialogResult res = MessageBox.Show(this, sb.ToString(), "Transfer Files?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.No)
                {
                    Log.InfoFormat("FileTransfer canceled: {0}", dragDropFileTransfer);
                    ResetDragDrop();
                    return;
                }
            }

            // Request file transfers   
            Presenter.TransferFiles(dragDropFileTransfer);
            ResetDragDrop();
        }

        private void listViewFiles_ItemDrag(object sender, ItemDragEventArgs e)
        {
            ListView listView = (ListView) sender;

            List<BrowserFileInfo> items = new List<BrowserFileInfo>();
            foreach (ListViewItem item in listView.SelectedItems)
            {
                BrowserFileInfo bfi = (BrowserFileInfo) item.Tag;
                if (bfi.Type == FileType.ParentDirectory)
                {
                    // can't work with parent dir...bug out
                    return;
                }
                items.Add(bfi);
            }
                
            DoDragDrop(items.ToArray(), DragDropEffects.Copy);
        }

        private void ResetDragDrop()
        {
            dragDropLastX = -1;
            dragDropLastY = -1;
            dragDropIsValid = false;
            dragDropFileTransfer = null;
        }

        #endregion

        private void tsBtnRefresh_Click(object sender, EventArgs e)
        {
            Presenter.Refresh();
        }

        private void listViewFiles_DoubleClick(object sender, EventArgs e)
        {
            if (listViewFiles.SelectedItems.Count != 0)
            {
                BrowserFileInfo bfi = (BrowserFileInfo) listViewFiles.SelectedItems[0].Tag;
                if (bfi.Type == FileType.Directory || bfi.Type == FileType.ParentDirectory)
                {
                    Presenter.LoadDirectory(bfi);
                }
            }
        }

        IBrowserPresenter Presenter { get; set; }
        BrowserFileInfoComparer Comparer { get; }
        public bool ConfirmTransfer { get; set; }


    }
}
