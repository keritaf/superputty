using System;
using System.Windows.Forms;
using SuperPutty.Gui;

namespace SuperPutty
{
    public partial class frmTransferStatus : ToolWindow
    {
        public TransferUpdateCallback m_callback;
        public frmTransferStatus()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Update the progress bars and associated text labels with transfer progress
        /// </summary>
        /// <param name="currentFile">Object containing the current file being transferred</param>
        /// <param name="sofar">The bytes transferred sofar</param>
        /// <param name="total">The total number of bytes we're expecting</param>
        /// <param name="fileNum">The current file number being transferred.</param>
        /// <param name="totalFiles">The total number of files we're expecting to be transferred</param>
        public void UpdateProgress(FileTransferStatus currentFile, int sofar, int total, int fileNum, int totalFiles)
        {
            if (InvokeRequired)
                BeginInvoke((MethodInvoker)delegate
                {
                    UpdateProgress(currentFile, sofar, total, fileNum, totalFiles);
                });
            else
            {
                labelCurrentFile.Text = String.Format(
                    LocalizedText.frmTransferStatus_labelCurrentFile,
                    currentFile.Filename, currentFile.BytesTransferred, currentFile.TransferRate, currentFile.TimeLeft);
                progressBarCurrentFile.Value = currentFile.PercentComplete;
                labelCurrentPercent.Text = string.Format(LocalizedText.frmTransferStatus_UpdateProgress_Percent, currentFile.PercentComplete);

                labelOverall.Visible = progressBarOverall.Visible  = labelOverallPct.Visible = totalFiles > 1;
                
                if (fileNum >= totalFiles)
                {
                    progressBarOverall.Value = 100;
                    labelOverallPct.Text = LocalizedText.frmTransferStatus_UpdateProgress__100_percent;
                    button1.Text = LocalizedText.frmTransferStatus_UpdateProgress_Close;
                }
                else if(totalFiles > 1)
                {
                    progressBarOverall.Value = (int)((float)sofar / total * 100);
                    labelOverallPct.Text = String.Format(LocalizedText.frmTransferStatus_UpdateProgress_Percent, progressBarOverall.Value);

                    labelOverall.Text = String.Format(LocalizedText.frmTransferStatus_labelOverall,
                    sofar, total, fileNum, totalFiles);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            m_callback?.Invoke(false, true, new FileTransferStatus());
        }
    }
}
