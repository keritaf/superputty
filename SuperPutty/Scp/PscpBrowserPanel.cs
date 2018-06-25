using System;
using System.IO;
using SuperPutty.Data;

namespace SuperPutty.Scp
{
    public partial class PscpBrowserPanel : ToolWindowDocument
    {
        public PscpBrowserPanel()
        {
            InitializeComponent();
        }

        public PscpBrowserPanel(SessionData session, PscpOptions options) :
           // default value of localStartingDir moved to localPath in PscpBrowserPanel(SessionData session, PscpOptions options, string localStartingDir)            
           this(session, options, "")
        { }

        public PscpBrowserPanel(SessionData session, PscpOptions options, string localStartingDir) : this()
        {
            Name = session.SessionName;
            TabText = session.SessionName;

             //set the remote path
            String remotePath;            
            if (String.IsNullOrEmpty(session.RemotePath)){                
                remotePath = options.PscpHomePrefix + session.Username;
            }else{                
                remotePath = session.RemotePath;
            }

            //set the local path
            String localPath;
            if (String.IsNullOrEmpty(localStartingDir)){
                localPath = String.IsNullOrEmpty(session.LocalPath) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : session.LocalPath;
            }else{
                localPath = localStartingDir;
            }
 		 

            var fileTransferPresenter = new FileTransferPresenter(options);
            IBrowserPresenter localBrowserPresenter = new BrowserPresenter(
                "Local", new LocalBrowserModel(), session, fileTransferPresenter);
            IBrowserPresenter remoteBrowserPresenter = new BrowserPresenter(
                "Remote", new RemoteBrowserModel(options), session, fileTransferPresenter);

            browserViewLocal.Initialize(localBrowserPresenter, new BrowserFileInfo(new DirectoryInfo(localPath)));
            browserViewRemote.Initialize(remoteBrowserPresenter, RemoteBrowserModel.NewDirectory(remotePath));
            fileTransferView.Initialize(fileTransferPresenter);
        }
    }
}
