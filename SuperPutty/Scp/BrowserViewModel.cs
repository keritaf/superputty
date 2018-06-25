using System;
using System.ComponentModel;
using SuperPutty.Gui;
using System.Threading;

namespace SuperPutty.Scp
{
    /// <summary>
    /// Adapter class over the IBrowserModel to facilitate GUI binding
    /// </summary>
    public class BrowserViewModel : BaseViewModel, IBrowserViewModel
    {
        string name;
        string currentPath;
        string status;
        BrowserState browserState;

        public BrowserViewModel()
        {
            currentPath = null;
            status = String.Empty;
            browserState = BrowserState.Ready;
            Files = new BindingList<BrowserFileInfo>();
            Context = SynchronizationContext.Current;
        }

        public string Name
        {
            get => name;
            set { SetField(ref name, value, () => Name); }
        }

        public string CurrentPath
        {
            get => currentPath;
            set { SetField(ref currentPath, value, () => CurrentPath); }
        }

        public string Status
        {
            get => status;
            set { SetField(ref status, value, () => Status); }
        }

        public BrowserState BrowserState
        {
            get => browserState;
            set { SetField(ref browserState, value, () => BrowserState); }
        }

        public BindingList<BrowserFileInfo> Files { get; private set; }
    }

}
