using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using SuperPutty.Gui;
using SuperPutty.Utils;
using System.Threading;

namespace SuperPutty.Scp
{
    #region FileTransferViewModel
    /// <summary>
    /// UI view adapter.
    /// Must be created on GUI thread to pickup proper context for notifications to be 
    /// auto-marshalled properly
    /// </summary>
    public class FileTransferViewModel : BaseViewModel
    {
        public FileTransferViewModel()
        {
            FileTransfers = new SortableBindingList<FileTransferViewItem>();
            Context = SynchronizationContext.Current;
        }

        public int FindIndexById(int id)
        {
            int idx = -1;
            for (int i = 0; i < FileTransfers.Count; i++)
            {
                FileTransferViewItem item = FileTransfers[i];
                if (item.Id == id)
                {
                    idx = i;
                    break;
                }
            }
            return idx;
        }

        public BindingList<FileTransferViewItem> FileTransfers { get; set; }
    } 
    #endregion

    #region FileTransferViewItem
    /// <summary>
    /// Converts FileTransfer
    /// </summary>
    public class FileTransferViewItem 
    {
        public FileTransferViewItem() 
        {
        }

        public FileTransferViewItem(string session, string source, string target)
            : this()
        {
            Session = session;
            Source = source;
            Target = target;
        }

        public FileTransferViewItem(FileTransfer transfer)
            : this()
        {
            Id = transfer.Id;
            Session = transfer.Request.Session.SessionId;
            Source = ToString(transfer.Request.SourceFiles);
            Target = transfer.Request.TargetFile.Path;
            Start = DateTime.Now;
        }

        public int Id { get; private set; }
        public string Session { get; private set; }
        public string Source { get; private set; }
        public string Target { get; private set; }

        private FileTransfer Transfer { get; set; }

        public DateTime? Start { get;set;}
        public DateTime? End {get;set;}
        public int Progress {get;set;}
        public FileTransfer.Status Status { get;set;}
        public string Message { get; set; }

        public bool CanRestart { get; set; }
        public bool CanCancel { get; set; }
        public bool CanDelete { get; set; }

        static string ToString(List<BrowserFileInfo> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string strSource;
            if (source.Count == 1)
            {
                strSource = source[0].Path;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (BrowserFileInfo info in source)
                {
                    sb.AppendLine(info.Path);
                }
                strSource = sb.ToString();
            }
            return strSource;
        }
    }
    
    #endregion
}