using System;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Helper class to track single instance tool windows (sessions, log viewer, layouts)
    /// </summary>
    public class SingletonToolWindowHelper<T> where T : ToolWindow
    {
        public delegate T WindowInitializer(SingletonToolWindowHelper<T> helper);
        public delegate void InstanceChangedHandler(T Instance);

        public event InstanceChangedHandler InstanceChanged;

        public SingletonToolWindowHelper(string name, DockPanel dockPanel) : this(name, dockPanel, null, null) {}

        public SingletonToolWindowHelper(string name, DockPanel dockPanel, Object InitializerResource, WindowInitializer initializer)
        {
            Name = name;
            DockPanel = dockPanel;
            Initializer = initializer;
            this.InitializerResource = InitializerResource;
        }

        public void ShowWindow(DockState dockState)
        {
            if (Instance == null)
            {
                Initialize();
                Instance.Show(DockPanel, dockState);
                SuperPuTTY.ReportStatus("Showing " + Name);
            }
            else
            {
                Instance.Show(DockPanel);
                SuperPuTTY.ReportStatus("Bringing {0} to Front", Name);
            }
        }

        public void ShowWindow(DockPane pane, DockAlignment dockAlign, double proportion)
        {
            if (Instance == null)
            {
                Initialize();
                Instance.Show(pane, dockAlign, proportion);
                SuperPuTTY.ReportStatus("Showing " + Name);
            }
            else
            {
                Instance.Show(DockPanel);
                SuperPuTTY.ReportStatus("Bringing {0} to Front", Name);
            }
        }

        public void ShowWindow(DockPane pane, IDockContent PreviousContent)
        {
            if (Instance == null)
            {
                Initialize();
            }
            
            Instance.Show(pane, PreviousContent);
            SuperPuTTY.ReportStatus("Showing " + Name);
        }

        public bool IsVisibleAsToolWindow => Instance?.DockHandler.Pane != null && !Instance.DockHandler.Pane.IsAutoHide;

        public T Initialize()
        {
            Instance = Initializer == null ? Activator.CreateInstance<T>() : Initializer(this);

            Instance.FormClosed += Instance_FormClosed;
            InstanceChanged?.Invoke(Instance);
            return Instance;
        }

        void Instance_FormClosed(object sender, FormClosedEventArgs e)
        {
            Instance = null;
            SuperPuTTY.ReportStatus("Closed {0}", Name);
        }

        public void Hide()
        {
            Instance?.Hide();
        }

        public void Restore()
        {
            Instance?.Show(DockPanel);
        }

        public bool IsVisible => Instance != null && Instance.Visible;


        public string Name { get; }
        public DockPanel DockPanel { get; }
        public WindowInitializer Initializer { get; }
        public Object InitializerResource { get; }
        public T Instance { get; private set; }
    }
}
