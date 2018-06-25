﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SuperPutty.Scp
{
    public partial class FileTransferView : UserControl
    {
        private bool initialized;

        public FileTransferView()
        {
            InitializeComponent();
        }

        public FileTransferView(IFileTransferPresenter presenter) : this()
        {
            Initialize(presenter);
        }

        public void Initialize(IFileTransferPresenter presenter)
        {
            if (initialized) return;

            Presenter = presenter;
            bindingSource.DataSource = presenter.ViewModel.FileTransfers;
            initialized = true;
        }

        #region Context Menu

        private void contextMenu_Opening(object sender, CancelEventArgs e)
        {
            Point p = PointToClient(MousePosition);
            DataGridView.HitTestInfo hit = grid.HitTest(p.X, p.Y);
            if (hit.Type == DataGridViewHitTestType.Cell)
            {
                // toggle on/off the actions based on view model
                FileTransferViewItem item = (FileTransferViewItem) grid.Rows[hit.RowIndex].DataBoundItem;
                runAgainToolStripMenuItem.Enabled = item.CanRestart;
                cancelToolStripMenuItem.Enabled = item.CanCancel;
                deleteToolStripMenuItem.Enabled = item.CanDelete;
            }
            else
            {
                // Only open on cells
                e.Cancel = true;
            }
        }

        private void grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (MouseButtons == MouseButtons.Right)
            {
                if (!grid.Rows[e.RowIndex].Selected)
                {
                    grid.ClearSelection();
                    grid.Rows[e.RowIndex].Selected = true;
                }
            }
        }


        private void cancelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileTransferViewItem item = GetSelectedItem<FileTransferViewItem>();
            if (item != null)
            {
                Presenter.Cancel(item.Id);
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileTransferViewItem item = GetSelectedItem<FileTransferViewItem>();
            if (item != null)
            {
                Presenter.Remove(item.Id);
            }
        }

        private void runAgainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileTransferViewItem item = GetSelectedItem<FileTransferViewItem>();
            if (item != null)
            {
                Presenter.Restart(item.Id);
            }
        }

        #endregion

        T GetSelectedItem<T>()
        {
            T item = default(T);
            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                item = (T) row.DataBoundItem;
                break;
            }
            return item;
        }

        IFileTransferPresenter Presenter { get; set; }
    }
}
