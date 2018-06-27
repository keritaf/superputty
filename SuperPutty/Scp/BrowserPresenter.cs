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
using log4net;
using System.ComponentModel;
using SuperPutty.Data;
using SuperPutty.Gui;

namespace SuperPutty.Scp
{
    #region LocalBrowserPresenter
    /// <summary>Class that contains data and methods for displaying local or remote directories</summary>
    public class BrowserPresenter : IBrowserPresenter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BrowserPresenter));

        /// <summary>Raised when login and password information is required to authenticate against a ssh server serving files via scp</summary>
        public event EventHandler<AuthEventArgs> AuthRequest;

        /// <summary>Construct a new instance of the BrowserPresenter class with parameters</summary>
        /// <param name="name">A string that identifies the name of the associated <seealso cref="ViewModel"/> e.g. "Local" or "Remote"</param>
        /// <param name="model">The BrowserModel object used to get the data from a file system</param>
        /// <param name="session">The session information containing host, ip, and other data specific to the BrowserModel</param>
        /// <param name="fileTransferPresenter">Methods and tools for working with file transfers</param>
        public BrowserPresenter(string name, IBrowserModel model, SessionData session, IFileTransferPresenter fileTransferPresenter)
        {
            Model = model;
            Session = session;

            FileTransferPresenter = fileTransferPresenter;
            FileTransferPresenter.ViewModel.FileTransfers.ListChanged += FileTransfers_ListChanged;

            BackgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            BackgroundWorker.DoWork += BackgroundWorker_DoWork;
            BackgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;

            ViewModel = new BrowserViewModel
            {
                Name = name, 
                BrowserState = BrowserState.Ready
            };
        }

        /// <summary>Updates the <seealso cref="BrowserViewModel"/> when a change to the file transfers list is detected</summary>
        /// <param name="sender">The <seealso cref="IBindingList"/> of <seealso cref="FileTransferViewItem"/> items</param>
        /// <param name="e">The <seealso cref="ListChangedEventArgs"/> items containing the type of change detected and the index of the item</param>
        private void FileTransfers_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemChanged)
            {
                BindingList<FileTransferViewItem> list = (BindingList<FileTransferViewItem>)sender;
                FileTransferViewItem item = list[e.NewIndex];
                if (item.Status == FileTransfer.Status.Complete &&
                    item.Target == CurrentPath.Path)
                {
                    Log.InfoFormat("Refreshing for FileTransferUpdate, path={0}", CurrentPath.Path);

                    ViewModel.Context.Post((x) => Refresh(), null);
                }
            }
        }

        #region Async Work

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ViewModel.Status = (string) e.UserState;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BrowserFileInfo targetPath = (BrowserFileInfo)e.Argument;
            BackgroundWorker.ReportProgress(5, "Requesting files for " + targetPath.Path);
            
            ListDirectoryResult result = Model.ListDirectory(Session, targetPath);
            
            BackgroundWorker.ReportProgress(80, "Remote call complete: " + result.StatusCode);
            e.Result = result;
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                string msg = string.Format("System error while loading directory: {0}", e.Error.Message);
                Log.Error(msg, e.Error);
                ViewModel.Status = msg;
            }
            else
            {
                ListDirectoryResult result = (ListDirectoryResult)e.Result;
                switch (result.StatusCode)
                {
                    case ResultStatusCode.RetryAuthentication:
                        // Request login- first login will be initiated from here after failing 1st call to pscp which attemps key based auth
                        AuthEventArgs authEvent = new AuthEventArgs
                        { 
                            UserName = Session.Username, 
                            Password = Session.Password 
                        };
                        OnAuthRequest(authEvent);
                        if (authEvent.Handled)
                        {
                            // retry listing
                            Session.Username = authEvent.UserName;
                            Session.Password = authEvent.Password;
                            LoadDirectory(result.Path);
                        }
                        break;
                    case ResultStatusCode.Success:
                        // list files
                        string msg = result.MountCount > 0
                            ? string.Format("{0} items", result.MountCount)
                            : string.Format("{0} files {1} directories", result.FileCount, result.DirCount);
                        ViewModel.Status = string.Format("{0} @ {1}", msg, DateTime.Now);
                        CurrentPath = result.Path;
                        ViewModel.CurrentPath = result.Path.Path;
                        BaseViewModel.UpdateList(ViewModel.Files, result.Files);
                        break;
                    case ResultStatusCode.Error:
                        string errMsg = result.ErrorMsg ?? (result.Error != null 
                                            ? string.Format("Error listing directory, {0}", result.Error.Message) 
                                            : "Unknown Error listing directory");
                        Log.Error(errMsg, result.Error);
                        ViewModel.Status = errMsg;
                        break;
                    default:
                        Log.InfoFormat("Unknown result '{0}'", result.StatusCode);
                        break;
                }
            }
            ViewModel.BrowserState = BrowserState.Ready;
        }

        #endregion

        /// <summary>Asynchronously loads the contents of a directory</summary>
        /// <param name="dir">The BrowserFileInfo object containing the path to the directory to load</param>
        public void LoadDirectory(BrowserFileInfo dir)
        {
            if (BackgroundWorker.IsBusy)
            {
                ViewModel.Status = "Busy loading directory";
            }
            else
            {
                ViewModel.BrowserState = BrowserState.Working;
                if (dir != null)
                {
                    Log.InfoFormat("LoadDirectory, path={0}", dir);
                    BackgroundWorker.RunWorkerAsync(dir);
                }
                else
                {
                    Log.Error("LoadDirectory Failed: target was null");
                }
            }
        }

        /// <summary>Refresh the current directory</summary>
        public void Refresh()
        {
            Log.DebugFormat("Refreshing current directory: '{0}'", CurrentPath);
            LoadDirectory(CurrentPath);
        }

        /// <summary>Verify a file can be transfered</summary>
        /// <param name="source">The Source file</param>
        /// <param name="target">The Destination file</param>
        /// <returns>true if the file can be transfered</returns>
        public bool CanTransferFile(BrowserFileInfo source, BrowserFileInfo target)
        {
            return FileTransferPresenter.CanTransferFile(source, target);
        }

        /// <summary>Transfer a file between two locations</summary>
        /// <param name="fileTransferReqeust">The request data containing files to transfer</param>
        public void TransferFiles(FileTransferRequest fileTransferReqeust)
        {
            FileTransferPresenter.TransferFiles(fileTransferReqeust);
        }


        protected void OnAuthRequest(AuthEventArgs evt)
        {
            AuthRequest?.Invoke(this, evt);
        }

        IBrowserModel Model { get; }
        IFileTransferPresenter FileTransferPresenter { get; }
        
        BackgroundWorker BackgroundWorker { get; }

        public IBrowserViewModel ViewModel { get; protected set; }
        public BrowserFileInfo CurrentPath { get; protected set; }
        public SessionData Session { get; protected set; }
    } 

    #endregion
}
