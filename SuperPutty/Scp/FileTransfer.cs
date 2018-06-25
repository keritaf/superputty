using System;
using System.Collections.Generic;
using SuperPutty.Data;
using log4net;
using System.Threading;

namespace SuperPutty.Scp
{
    #region FileTransfer
    public class FileTransfer
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileTransfer));
        private static int idSeed;

        public event EventHandler Update;

        private Thread thread;
        Status status = Status.Initializing;

        public FileTransfer(PscpOptions options, FileTransferRequest request)
        {
            Options = options;
            Request = request;

            Id = Interlocked.Increment(ref idSeed);
        }

        public void Start()
        {
            lock (this)
            {
                if (TransferStatus == Status.Initializing || CanRestart(TransferStatus))
                {
                    Log.InfoFormat("Starting transfer, id={0}", Id);

                    StartTime = DateTime.Now;

                    thread = new Thread(DoTransfer) {IsBackground = false};
                    thread.Start();

                    UpdateStatus(0, Status.Running, "Started transfer");
                }
                else
                {
                    Log.WarnFormat("Attempted to start active transfer, id={0}", Id);
                }
            }
        }

        public void Cancel()
        {
            lock (this)
            {
                if (CanCancel(TransferStatus))
                {
                    Log.InfoFormat("Canceling active transfer, id={0}", Id);
                    thread.Abort();
                    Log.InfoFormat("Canceled active transfer, id={0}", Id);
                    UpdateStatus(PercentComplete, Status.Canceled, "Canceled");
                }
                else
                {
                    Log.WarnFormat("Attempted to cancel inactive transfer, id={0}", Id);
                }
            }
        }

        void DoTransfer()
        {
            try
            {
                PscpClient client = new PscpClient(Options, Request.Session);

                int estSizeKB = Int32.MaxValue;
                FileTransferResult res = client.CopyFiles(
                    Request.SourceFiles,
                    Request.TargetFile,
                    (complete, cancelAll, s) =>
                    {
                        string msg;
                        if (s.PercentComplete > 0)
                        {
                            estSizeKB = Math.Min(estSizeKB, s.BytesTransferred * 100 / s.PercentComplete);
                            string units = estSizeKB > 1024 * 10 ? "MB" : "KB";
                            int divisor = units == "MB" ? 1024 : 1;
                            msg = string.Format(
                                "{0}, ({1} of {2} {3}, {4})",
                                s.Filename,
                                s.BytesTransferred / divisor,
                                estSizeKB / divisor,
                                units,
                                s.TimeLeft);
                        }
                        else
                        {
                            // < 1% completed
                            msg = string.Format("{0}, ({1} KB, {2})", s.Filename, s.BytesTransferred, s.TimeLeft);
                        }
                        UpdateStatus(s.PercentComplete, Status.Running, msg);
                    });

                EndTime = DateTime.Now;
                switch (res.StatusCode)
                {
                    case ResultStatusCode.Success:
                        double duration = (EndTime.Value - StartTime.Value).TotalSeconds;
                        UpdateStatus(100, Status.Complete, String.Format("Duration {0:#,###} s", duration));
                        break;
                    case ResultStatusCode.RetryAuthentication:
                    case ResultStatusCode.Error:
                        UpdateStatus(PercentComplete, Status.Error, res.ErrorMsg);
                        break;
                }
            }
            catch (ThreadAbortException)
            {
                UpdateStatus(PercentComplete, Status.Canceled, "");
            }
            catch (Exception ex)
            {
                Log.Error("Error running transfer, id=" + Id, ex);
                UpdateStatus(0, Status.Error, ex.Message);
            }
        }

        void UpdateStatus(int percentageComplete, Status status, string message)
        {
            PercentComplete = percentageComplete;
            TransferStatus = status;
            TransferStatusMsg = message;
            Update?.Invoke(this, EventArgs.Empty);
        }

        public static bool CanRestart(Status status)
        {
            return status == Status.Complete || status == Status.Canceled || status == Status.Error;
        }

        public static bool CanCancel(Status status)
        {
            return status == Status.Running;
        }

        public PscpOptions Options { get; private set; }
        public FileTransferRequest Request { get; private set; }
        public int Id { get; private set; }

        public Status TransferStatus
        {
            get { lock (this) { return status; } }
            private set { lock (this) { status = value; } }
        }

        public int PercentComplete { get; private set; }
        public string TransferStatusMsg { get; private set; }
        public DateTime? StartTime { get; private set; }
        public DateTime? EndTime { get; private set; }

        public enum Status
        {
            Initializing,
            Running,
            Complete,
            Error,
            Canceled
        }
    } 
    #endregion

    #region FileTransferRequest
    public class FileTransferRequest
    {
        public FileTransferRequest()
        {
            SourceFiles = new List<BrowserFileInfo>();
        }
        public SessionData Session { get; set; }
        public List<BrowserFileInfo> SourceFiles { get; set; }
        public BrowserFileInfo TargetFile { get; set; }
    } 
    #endregion
}
