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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;
using SuperPutty.Data;
using log4net;
using System.Runtime.InteropServices;
using SuperPutty.Utils;
using SuperPuTTY.Scripting;
using System.Configuration;
using System.Linq;
using SuperPutty.Gui;
using log4net.Core;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace SuperPutty
{
    public partial class frmSuperPutty : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(frmSuperPutty));

        private static string XmlEditor => ConfigurationManager.AppSettings["SuperPuTTY.XmlEditor"];

        internal DockPanel DockPanel { get; }

        public ToolWindowDocument CurrentPanel { get; set; }

        private readonly SingletonToolWindowHelper<SessionTreeview> sessions;
        private readonly SingletonToolWindowHelper<LayoutsList> layouts;
        private readonly SingletonToolWindowHelper<Log4netLogViewer> logViewer;
        private readonly SingletonToolWindowHelper<SessionDetail> sessionDetail;

        private readonly TextBoxFocusHelper tbFocusHelperHost;
        private readonly TextBoxFocusHelper tbFocusHelperUserName;
        private readonly TextBoxFocusHelper tbFocusHelperPassword;
        private readonly frmDocumentSelector sendCommandsDocumentSelector;

        //private NativeMethods.LowLevelKMProc llmp;
        private static IntPtr kbHookID = IntPtr.Zero;
        private static readonly IntPtr mHookID = IntPtr.Zero;
        private bool forceClose;
        private FormWindowState lastNonMinimizedWindowState = FormWindowState.Normal;
        private Rectangle lastNormalDesktopBounds;
        private readonly ChildWindowFocusHelper focusHelper;
        bool isControlDown;
        bool isShiftDown;
        bool isAltDown;
        int commandMRUIndex = -1;

        private readonly TabSwitcher tabSwitcher;
        private readonly ViewState fullscreenViewState;

        private readonly Dictionary<Keys, SuperPuttyAction> shortcuts = new Dictionary<Keys, SuperPuttyAction>();

        /// <summary>A collection containing send command history</summary>
        private readonly SortableBindingList<HistoryEntry> tsCommandHistory = new SortableBindingList<HistoryEntry>();

        /// <summary>The main SuperPuTTY application form</summary>
        public frmSuperPutty()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            // Verify Putty is set; Prompt user if necessary; exit otherwise
            dlgFindPutty.PuttyCheck();

            InitializeComponent();

            DockPanel = dockPanel1;
            // force toolbar locations...designer likes to flip them around
            tsConnect.Location = new Point(0, menuStrip1.Height);
            tsCommands.Location = new Point(0, tsConnect.Height);

            if (DesignMode) return;

            // setup connection bar
            tbTxtBoxPassword.TextBox.PasswordChar = '*';
            RefreshConnectionToolbarData();

            // version in status bar
            toolStripStatusLabelVersion.Text = SuperPuTTY.Version;

            // tool windows
            sessions = new SingletonToolWindowHelper<SessionTreeview>("Sessions", DockPanel, null, x => new SessionTreeview(x.DockPanel));
            layouts = new SingletonToolWindowHelper<LayoutsList>("Layouts", DockPanel);
            logViewer = new SingletonToolWindowHelper<Log4netLogViewer>("Log Viewer", DockPanel);
            sessionDetail = new SingletonToolWindowHelper<SessionDetail>(
                "Session Detail", DockPanel, sessions,
                x => new SessionDetail(x.InitializerResource as SingletonToolWindowHelper<SessionTreeview>));

            // for toolbar
            tbFocusHelperHost = new TextBoxFocusHelper(tbTxtBoxHost.TextBox);
            tbFocusHelperUserName = new TextBoxFocusHelper(tbTxtBoxLogin.TextBox);
            tbFocusHelperPassword = new TextBoxFocusHelper(tbTxtBoxPassword.TextBox);
            sendCommandsDocumentSelector = new frmDocumentSelector(DockPanel) {Owner = this};

            // Send Command toolbar history
            PropertyDescriptor pd = TypeDescriptor.GetProperties(typeof(HistoryEntry))["TimeStamp"];
            ((IBindingList)tsCommandHistory).ApplySort(pd, ListSortDirection.Descending);

            tsSendCommandCombo.ComboBox.DisplayMember = "Command";
            tsSendCommandCombo.ComboBox.ValueMember = "Command";            
            tsSendCommandCombo.ComboBox.DataSource = tsCommandHistory;

            // load saved history
            if(SuperPuTTY.Settings.PersistCommandBarHistory)
                tsCommandHistory.DeserializeXML(SuperPuTTY.Settings.CommandBarHistory);

            tsSendCommandCombo.SelectedIndex = -1;

            tsCommandHistory.ListChanged += TsCommandHistory_ListChanged;
           
            // Hook into status
            SuperPuTTY.StatusEvent += delegate(String msg) { toolStripStatusLabelMessage.Text = msg; };
            SuperPuTTY.ReportStatus("Ready");


            // Check for updates if enabled. (disabled if compiled with DEBUG)
            if (SuperPuTTY.Settings.AutoUpdateCheck)
            {
#if DEBUG
                Log.Info("Automatic Update Check Disabled in DEBUG mode");
#else
                Log.Info("Checking for updates");
                this.checkForUpdatesToolStripMenuItem_Click(this, new EventArgs());
#endif
            }
            // Hook into LayoutChanging/Changed
            SuperPuTTY.LayoutChanging += SuperPuTTY_LayoutChanging;

            // Low-Level Mouse and Keyboard hooks
            kbHookID = SetKBHook(KBHookCallback);
            //llmp = MHookCallback;
            //mHookID = SetMHook(llmp);

            focusHelper = new ChildWindowFocusHelper(this);
            focusHelper.Start();

            // Restore window location and size
            if (SuperPuTTY.Settings.RestoreWindowLocation)
            {
                FormUtils.RestoreFormPositionAndState(this, SuperPuTTY.Settings.WindowPosition, SuperPuTTY.Settings.WindowState);
            }

            ResizeEnd += frmSuperPutty_ResizeEnd;

            // tab switching
            tabSwitcher = new TabSwitcher(DockPanel);

            // full screen
            fullscreenViewState = new ViewState(this);

            // Apply Settings
            ApplySettings();
            ApplySettingsToToolbars();

            DockPanel.ContentAdded += DockPanel_ContentAdded;
            DockPanel.ContentRemoved += DockPanel_ContentRemoved;
        }

        private void TsCommandHistory_ListChanged(object sender, ListChangedEventArgs e)
        {
            DateTime daysAgo = DateTime.UtcNow.Subtract(TimeSpan.FromDays(SuperPuTTY.Settings.SaveCommandHistoryDays));
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                // purge duplicates from history
                HistoryEntry he = tsCommandHistory[e.NewIndex];
                for(int i = 0; i < tsCommandHistory.Count; i++)
                {
                    if(i != e.NewIndex 
                        && tsCommandHistory[i].Command.Equals(he.Command))
                    {
                        tsCommandHistory.RemoveAt(i);
                    }                    
                }
                
                for(int i = 0; i < tsCommandHistory.Count; i++)
                {
                    // purge old entries from history
                    if (tsCommandHistory[i].TimeStamp < daysAgo)
                    {
                        tsCommandHistory.RemoveAt(i);
                    }
                }           
            }
        }       

        void DockPanel_ContentAdded(object sender, DockContentEventArgs e)
        {
            if (e.Content is CtlPuttyPanel p)
            {
                p.TextChanged += puttyPanel_TextChanged;
            }
        }

        void DockPanel_ContentRemoved(object sender, DockContentEventArgs e)
        {
            if (e.Content is CtlPuttyPanel p)
            {
                p.TextChanged -= puttyPanel_TextChanged;
            }
        }

        void puttyPanel_TextChanged(object sender, EventArgs e)
        {
            CtlPuttyPanel p = (CtlPuttyPanel)sender;
            if (p == DockPanel.ActiveDocument)
            {
                UpdateWindowText(p.Text);
            }
        }

        void UpdateWindowText(string text)
        {
            Text = string.Format("SuperPuTTY - {0}", text);
        }

        private void frmSuperPutty_Load(object sender, EventArgs e)
        {
            BeginInvoke(new Action(LoadLayout));            
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // free hooks
            NativeMethods.UnhookWindowsHookEx(kbHookID);
            //NativeMethods.UnhookWindowsHookEx(mHookID);

            // save window size and location if not maximized or minimized
            if (SuperPuTTY.Settings.RestoreWindowLocation)// && this.WindowState != FormWindowState.Minimized)
            {
                SuperPuTTY.Settings.WindowPosition = lastNormalDesktopBounds;
                SuperPuTTY.Settings.WindowState = WindowState == FormWindowState.Minimized ? WindowState = lastNonMinimizedWindowState : WindowState;
                SuperPuTTY.Settings.Save();
            }

            // save layout for auto-restore
            if (SuperPuTTY.Settings.DefaultLayoutName == LayoutData.AutoRestore)
            {
                SaveLayout(SuperPuTTY.AutoRestoreLayoutPath, "Saving auto-restore layout");
            }

            if(SuperPuTTY.Settings.PersistCommandBarHistory)
            {
                SuperPuTTY.Settings.CommandBarHistory = tsCommandHistory.SerializeXML();
                SuperPuTTY.Settings.Save();
            }

            focusHelper.Dispose();

            base.OnFormClosed(e);
        }

        void frmSuperPutty_ResizeEnd(object sender, EventArgs e)
        {
            SaveLastWindowBounds();
        }

        private void frmSuperPutty_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (SuperPuTTY.Settings.ExitConfirmation && !forceClose)
            {
                if (MessageBox.Show("Exit SuperPuTTY?", "Confirm Exit", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        /// <summary>
        /// Handles focusing on tabs/windows which host PuTTY
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dockPanel1_ActiveDocumentChanged(object sender, EventArgs e)
        {            
            FocusActiveDocument("ActiveDocumentChanged");
        }

        public void FocusActiveDocument(string caller)
        {
            if (DockPanel.ActiveDocument == null)
            {
                Text = "SuperPuTTY";
            }
            else
            {
                if (DockPanel.ActiveDocument is ToolWindowDocument window)
                {
                    // If we aren't using Ctrl-Tab to move between panels,
                    // i.e. we got here because the operator clicked on the
                    // panel directly, then record it as the current panel.
                    if (!tabSwitcher.IsSwitchingTabs)
                    {
                        tabSwitcher.CurrentDocument = window;
                    }

                    if (window is CtlPuttyPanel p)
                    {
                        p.SetFocusToChildApplication(caller);
                        UpdateWindowText(p.Text);
                    }
                }
            }
        }

        private void frmSuperPutty_Activated(object sender, EventArgs e)
        {
            Log.DebugFormat("[{0}] Activated", Handle);
            //dockPanel1_ActiveDocumentChanged(null, null);
        }

        public void SetActiveDocument(ToolWindow toolWindow)
        {
            if (DockPanel.ActiveDocument != toolWindow)
            {
                toolWindow.Show();
            }
        }

        #region File

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "XML Files|*.xml|All files|*.*",
                FileName = "Sessions.XML",
                InitialDirectory = Application.StartupPath
            };
            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                SessionData.SaveSessionsToFile(SuperPuTTY.GetAllSessions(), saveDialog.FileName);
            }
        }

        private void fromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "XML Files|*.xml|All files|*.*",
                FileName = "Sessions.XML",
                CheckFileExists = true,
                InitialDirectory = Application.StartupPath
            };
            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                SuperPuTTY.ImportSessionsFromFile(openDialog.FileName);
            }
        }


        private void fromPuTTYCMExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "XML Files|*.xml|All files|*.*",
                FileName = "export.xml",
                CheckFileExists = true,
                InitialDirectory = Application.StartupPath
            };
            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                SuperPuTTY.ImportSessionsFromPuttyCM(openDialog.FileName);
            }
        }


        private void fromPuTTYSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show(
                "Do you want to copy all sessions from PuTTY/KiTTY?  Duplicates may be created.",
                "SuperPuTTY",
                MessageBoxButtons.YesNo);
            if (res == DialogResult.Yes)
            {
                SuperPuTTY.ImportSessionsFromPuTTY();
            }
        }

        private void openSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuickSelector q = new QuickSelector();
            QuickSelectorData data = new QuickSelectorData
            {
                CaseSensitive = SuperPuTTY.Settings.QuickSelectorCaseSensitiveSearch
            };

            foreach (SessionData sd in SuperPuTTY.Sessions)
            {
                data.ItemData.AddItemDataRow(
                    sd.SessionName,
                    sd.SessionId,
                    sd.Proto == ConnectionProtocol.Cygterm || sd.Proto == ConnectionProtocol.Mintty ? Color.Blue : Color.Black,
                    null);
            }

            QuickSelectorOptions opt = new QuickSelectorOptions
            {
                Sort = data.ItemData.DetailColumn.ColumnName,
                BaseText = "Open Session"
            };

            QuickSelector d = new QuickSelector();
            if (d.ShowDialog(this, data, opt) == DialogResult.OK)
            {
                SuperPuTTY.OpenPuttySession(d.SelectedItem.Detail);
            }
        }

        private void switchSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuickSelector q = new QuickSelector();
            QuickSelectorData data = new QuickSelectorData
            {
                CaseSensitive = SuperPuTTY.Settings.QuickSelectorCaseSensitiveSearch
            };

            foreach (ToolWindow content in tabSwitcher.Documents)
            {
                if (content is CtlPuttyPanel panel)
                {
                    SessionData sd = panel.Session;
                    data.ItemData.AddItemDataRow(
                        panel.Text,
                        sd.SessionId,
                        sd.Proto == ConnectionProtocol.Cygterm || sd.Proto == ConnectionProtocol.Mintty ? Color.Blue : Color.Black,
                        panel);
                }
            }

            QuickSelectorOptions opt = new QuickSelectorOptions
            {
                Sort = data.ItemData.DetailColumn.ColumnName,
                BaseText = "Switch Session",
                ShowNameColumn = true
            };

            QuickSelector d = new QuickSelector();
            if (d.ShowDialog(this, data, opt) == DialogResult.OK)
            {
                CtlPuttyPanel panel = (CtlPuttyPanel)d.SelectedItem.Tag;
                panel.Activate();
            }
        }

        private void editSessionsInNotepadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(XmlEditor ?? "notepad", Path.Combine(SuperPuTTY.Settings.SettingsFolder, "Sessions.XML"));
        }

        private void reloadSessionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SuperPuTTY.LoadSessions();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            Close();
        }
        #endregion

        #region View Menu

        private void toggleCheckedState(object sender, EventArgs e)
        {
            // toggle
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            mi.Checked = !mi.Checked;

            // save
            SuperPuTTY.Settings.ShowStatusBar = showStatusBarToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowToolBarConnections = quickConnectionToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowToolBarCommands = sendCommandsToolStripMenuItem.Checked;
            SuperPuTTY.Settings.AlwaysOnTop = alwaysOnTopToolStripMenuItem.Checked;
            SuperPuTTY.Settings.ShowMenuBar = showMenuBarToolStripMenuItem.Checked;

            SuperPuTTY.Settings.Save();

            // apply
            ApplySettingsToToolbars();
        }

        void ApplySettingsToToolbars()
        {
            statusStrip1.Visible = SuperPuTTY.Settings.ShowStatusBar;
            showStatusBarToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowStatusBar;

            tsConnect.Visible = SuperPuTTY.Settings.ShowToolBarConnections;
            quickConnectionToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowToolBarConnections;

            tsCommands.Visible = SuperPuTTY.Settings.ShowToolBarCommands;
            sendCommandsToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowToolBarCommands;

            TopMost = SuperPuTTY.Settings.AlwaysOnTop;
            alwaysOnTopToolStripMenuItem.Checked = SuperPuTTY.Settings.AlwaysOnTop;

            menuStrip1.Visible = SuperPuTTY.Settings.ShowMenuBar;
            showMenuBarToolStripMenuItem.Checked = SuperPuTTY.Settings.ShowMenuBar;
        }

        private void sessionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (layouts.IsVisibleAsToolWindow || sessionDetail.IsVisibleAsToolWindow)
            {
                DockPane Pane = layouts.IsVisibleAsToolWindow ? layouts.Instance.DockHandler.Pane : sessionDetail.Instance.DockHandler.Pane;
                sessions.ShowWindow(Pane, DockAlignment.Top, 0.5);
            }
            else
            {
                sessions.ShowWindow(DockState.DockRight);
            }
        }

        private void logViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            logViewer.ShowWindow(DockState.DockBottom);
        }


        private void layoutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sessionDetail.IsVisibleAsToolWindow)
            {
                layouts.ShowWindow(sessionDetail.Instance.Pane, sessionDetail.Instance);
            }
            else if (sessions.IsVisibleAsToolWindow)
            {
                layouts.ShowWindow(sessions.Instance.DockHandler.Pane, DockAlignment.Bottom, 0.5);
            }
            else
            {
                layouts.ShowWindow(DockState.DockRight);
            }
        }

        private void sessionDetailMenuItem_Click(object sender, EventArgs e)
        {
            if (layouts.IsVisibleAsToolWindow)
            {
                sessionDetail.ShowWindow(layouts.Instance.Pane, layouts.Instance);
            }
            else if (sessions.IsVisibleAsToolWindow)
            {
                sessionDetail.ShowWindow(sessions.Instance.DockHandler.Pane, DockAlignment.Bottom, 0.5);
            }
            else
            {
                sessionDetail.ShowWindow(DockState.DockRight);
            }
        }


        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleFullScreen();
        }

        void ToggleFullScreen()
        {
            if (fullScreenToolStripMenuItem.Checked)
            {
                Log.InfoFormat("Restore from Fullscreen");
                fullscreenViewState.Restore();
            }
            else
            {
                Log.InfoFormat("Go to Fullscreen");
                fullscreenViewState.SaveState();
                fullscreenViewState.Hide();
            }
            fullScreenToolStripMenuItem.Checked = !fullScreenToolStripMenuItem.Checked;

        }

        class ViewState
        {
            public ViewState(frmSuperPutty mainForm)
            {
                MainForm = mainForm;
                ConnectionBarLocation = MainForm.tsConnect.Location;
                CommandBarLocation = MainForm.tsConnect.Location;
            }

            public frmSuperPutty MainForm { get; }
            public bool StatusBar { get; set; }
            public bool MenuBar { get; set; }
            public bool ConnectionBar { get; set; }
            public bool CommandBar { get; set; }
            public bool SessionsWindow { get; set; }
            public bool LogWindow { get; set; }
            public bool LayoutWindow { get; set; }
            public bool SessionDetail { get; set; }

            public FormBorderStyle FormBorderStyle { get; set; }
            public FormWindowState FormWindowState { get; set; }

            public Point ConnectionBarLocation { get; }
            public Point CommandBarLocation { get; }

            public bool IsFullScreen { get; set; }

            public void SaveState()
            {
                StatusBar = MainForm.showStatusBarToolStripMenuItem.Checked;
                MenuBar = MainForm.showMenuBarToolStripMenuItem.Checked;

                ConnectionBar = MainForm.quickConnectionToolStripMenuItem.Checked;
                CommandBar = MainForm.sendCommandsToolStripMenuItem.Checked;

                SessionsWindow = MainForm.sessions.IsVisible;
                LogWindow = MainForm.logViewer.IsVisible;
                LayoutWindow = MainForm.layouts.IsVisible;
                SessionDetail = MainForm.sessionDetail.IsVisible;

                FormBorderStyle = MainForm.FormBorderStyle;
                FormWindowState = MainForm.WindowState;

            }

            public void Hide()
            {
                try
                {
                    MainForm.DockPanel.Visible = false;

                    // windows
                    MainForm.sessions.Hide();
                    MainForm.layouts.Hide();
                    MainForm.logViewer.Hide();
                    MainForm.sessionDetail.Hide();

                    // status bar
                    MainForm.statusStrip1.Hide();

                    // toolbars
                    MainForm.tsCommands.Visible = false;
                    MainForm.tsConnect.Visible = false;

                    // menubar
                    MainForm.menuStrip1.Hide();

                    MainForm.FormBorderStyle = FormBorderStyle.None;
                    if (MainForm.WindowState == FormWindowState.Maximized)
                    {
                        // if maximized, goto normal first
                        MainForm.WindowState = FormWindowState.Normal;
                    }
                    MainForm.WindowState = FormWindowState.Maximized;
                    MainForm.TopMost = true;

                    IsFullScreen = true;
                }
                finally
                {
                    MainForm.DockPanel.Visible = true;
                }

            }

            public void Restore()
            {
                try
                {
                    MainForm.DockPanel.Visible = false;

                    // windows
                    if (SessionsWindow) { MainForm.sessions.Restore(); }
                    if (LayoutWindow) { MainForm.layouts.Restore(); }
                    if (LogWindow) { MainForm.logViewer.Restore(); }
                    if (SessionDetail) { MainForm.sessionDetail.Restore(); }

                    // status bar
                    if (StatusBar) { MainForm.statusStrip1.Show(); }

                    // toolbars
                    if (CommandBar && ConnectionBar)
                    {
                        // both visible so set locations
                        MainForm.tsConnect.Visible = true;
                        MainForm.tsConnect.Location = ConnectionBarLocation;
                        MainForm.tsCommands.Visible = true;
                        MainForm.tsCommands.Location = CommandBarLocation;
                    }
                    else if (CommandBar) { MainForm.tsCommands.Visible = true; }
                    else if (ConnectionBar) { MainForm.tsConnect.Visible = true; }

                    // menubar
                    if (MenuBar) { MainForm.menuStrip1.Show(); }

                    MainForm.TopMost = false;
                    MainForm.WindowState = FormWindowState;
                    MainForm.FormBorderStyle = FormBorderStyle;
                    IsFullScreen = false;
                }
                finally
                {
                    MainForm.DockPanel.Visible = true;

                }
            }
        }

        #endregion

        #region Layout

        void LoadLayout()
        {
            String dir = SuperPuTTY.LayoutsDir;
            if (Directory.Exists(dir))
            {
                openFileDialogLayout.InitialDirectory = dir;
                saveFileDialogLayout.InitialDirectory = dir;
            }

            if (SuperPuTTY.StartingSession != null)
            {
                // coming from command line, so no don't load any layout
                //SuperPuTTY.LoadLayout(null);
                SuperPuTTY.OpenSession(SuperPuTTY.StartingSession);
            }
            else
            {
                // default layout or null for hard-coded default
                SuperPuTTY.LoadLayout(SuperPuTTY.StartingLayout);
                SuperPuTTY.ApplyDockRestrictions(DockPanel);
            }
        }

        void SuperPuTTY_LayoutChanging(object sender, LayoutChangedEventArgs eventArgs)
        {
            if (eventArgs.IsNewLayoutAlreadyActive)
            {
                toolStripStatusLabelLayout.Text = eventArgs.New.Name;
            }
            else
            {
                // reset old layout (close old putty instances)
                foreach (DockContent dockContent in DockPanel.DocumentsToArray())
                {
                    Log.Debug("Unhooking document: " + dockContent);
                    dockContent.DockPanel = null;
                    // close old putty
                    if (dockContent.CloseButtonVisible)
                    {
                        dockContent.Close();
                    }
                }
                List<DockContent> contents = DockPanel.Contents.Cast<DockContent>().ToList();
                foreach (DockContent dockContent in contents)
                {
                    Log.Debug("Unhooking dock content: " + dockContent);
                    dockContent.DockPanel = null;
                    // close non-persistant windows
                    if (dockContent.CloseButtonVisible)
                    {
                        dockContent.Close();
                    }
                }


                if (eventArgs.New == null)
                {
                    // 1st time or reset
                    Log.Debug("Initializing default layout");
                    InitDefaultLayout();
                    toolStripStatusLabelLayout.Text = "";
                    SuperPuTTY.ReportStatus("Initialized default layout");
                }
                else if (!File.Exists(eventArgs.New.FilePath))
                {
                    // file missing
                    Log.WarnFormat("Layout file doesn't exist, file={0}", eventArgs.New.FilePath);
                    InitDefaultLayout();
                    toolStripStatusLabelLayout.Text = eventArgs.New.Name;
                    SuperPuTTY.ReportStatus("Could not load layout, file missing: {0}", eventArgs.New.FilePath);
                }
                else
                {
                    // load new one
                    Log.DebugFormat("Loading layout: {0}", eventArgs.New.FilePath);
                    DockPanel.LoadFromXml(eventArgs.New.FilePath, RestoreLayoutFromPersistString);
                    toolStripStatusLabelLayout.Text = eventArgs.New.Name;
                    SuperPuTTY.ReportStatus("Loaded layout: {0}", eventArgs.New.FilePath);
                }

                // after all is done, cause a repaint to 
            }
        }

        void InitDefaultLayout()
        {
            sessionsToolStripMenuItem_Click(this, EventArgs.Empty);
            layoutsToolStripMenuItem_Click(this, EventArgs.Empty);
        }

        private void saveLayoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SuperPuTTY.CurrentLayout != null)
            {
                String file = SuperPuTTY.CurrentLayout.FilePath;
                SaveLayout(file, string.Format("Saving layout: {0}", file));
            }
            else
            {
                saveLayoutAsToolStripMenuItem_Click(sender, e);
            }
        }

        private void saveLayoutAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == saveFileDialogLayout.ShowDialog(this))
            {
                String file = saveFileDialogLayout.FileName;
                SaveLayout(file, string.Format("Saving layout as: {0}", file));
                SuperPuTTY.AddLayout(file);
            }
        }

        void SaveLayout(string file, string statusMsg)
        {
            SuperPuTTY.ReportStatus(statusMsg);
            DockPanel.SaveAsXml(file);
        }

        private IDockContent RestoreLayoutFromPersistString(String persistString)
        {
            if (typeof(SessionTreeview).FullName == persistString)
            {
                // session tree
                return sessions.Instance ?? sessions.Initialize();
            }
            else if (typeof(LayoutsList).FullName == persistString)
            {
                // layouts list
                return layouts.Instance ?? layouts.Initialize();
            }
            else if (typeof(Log4netLogViewer).FullName == persistString)
            {
                return logViewer.Instance ?? logViewer.Initialize();
            }
            else if (typeof(SessionDetail).FullName == persistString)
            {
                return sessionDetail.Instance ?? sessionDetail.Initialize();
            }
            else
            {
                // putty session
                CtlPuttyPanel puttyPanel = CtlPuttyPanel.FromPersistString(persistString);
                if (puttyPanel != null)
                {
                    return puttyPanel;
                }                
            }
            return null;
        }


        #endregion

        #region Tools

        private void puTTYConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process p = new Process {StartInfo = {FileName = SuperPuTTY.Settings.PuttyExe}};
            p.Start();

            SuperPuTTY.ReportStatus("Lauched Putty Configuration");
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SuperPuTTY.ReportStatus("Editing Options");

            dlgFindPutty dialog = new dlgFindPutty();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                ApplySettings();
            }

            SuperPuTTY.ReportStatus("Ready");
        }

        void ApplySettings()
        {
            SuperPuTTY.ApplyDockRestrictions(DockPanel);

            // apply tab switching strategy change
            tabSwitcher.TabSwitchStrategy = TabSwitcher.StrategyFromTypeName(SuperPuTTY.Settings.TabSwitcher);

            SaveLastWindowBounds();
            UpdateShortcutsFromSettings();
            Opacity = SuperPuTTY.Settings.Opacity;
            DockPanel.ShowDocumentIcon = SuperPuTTY.Settings.ShowDocumentIcons;
        }

        #endregion

        #region Help Menu
        private void aboutSuperPuttyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 about = new AboutBox1();
            about.ShowDialog(this);
            about = null;
        }

        private void superPuttyWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/jimradford/superputty/");
        }

        private void helpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (File.Exists(Application.StartupPath + @"\superputty.chm"))
            {
                Process.Start(Application.StartupPath + @"\superputty.chm");
            }
            else
            {
                DialogResult result = MessageBox.Show("Local documentation could not be found. Would you like to view the documentation online instead?", "Documentation Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    Process.Start("https://github.com/jimradford/superputty/wiki/Documentation");
                }
            }
        }

        #endregion

        #region Toolbar


        private string oldHostName;

        private void tbBtnConnect_Click(object sender, EventArgs e)
        {
            TryConnectFromToolbar();
        }

        private void tbItemConnect_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                TryConnectFromToolbar();
                e.Handled = true;
            }
        }

        void TryConnectFromToolbar()
        {
            String host = tbTxtBoxHost.Text;
            String protoString = (string)tbComboProtocol.SelectedItem;

            if (!String.IsNullOrEmpty(host))
            {
                bool isScp = "SCP" == protoString;
                bool isVnc = "VNC" == protoString;
                HostConnectionString connStr = new HostConnectionString(host, isVnc);
                ConnectionProtocol proto = isScp
                    ? ConnectionProtocol.SSH
                    : connStr.Protocol.GetValueOrDefault((ConnectionProtocol)Enum.Parse(typeof(ConnectionProtocol), protoString));
                SessionData session = new SessionData
                {
                    Host = connStr.Host,
                    SessionName = connStr.Host,
                    SessionId = SuperPuTTY.MakeUniqueSessionId(SessionData.CombineSessionIds("ConnectBar", connStr.Host)),
                    Proto = proto,
                    Port = connStr.Port.GetValueOrDefault(dlgEditSession.GetDefaultPort(proto)),
                    Username = tbTxtBoxLogin.Text,
                    Password = tbTxtBoxPassword.Text,
                    PuttySession = (string)tbComboSession.SelectedItem
                };
                SuperPuTTY.OpenSession(new SessionDataStartInfo { Session = session, UseScp = isScp });
                oldHostName = tbTxtBoxHost.Text;
                RefreshConnectionToolbarData();
            }
        }

        void RefreshConnectionToolbarData()
        {
            if (tbComboProtocol.Items.Count == 0)
            {
                tbComboProtocol.Items.Clear();
                foreach (ConnectionProtocol protocol in Enum.GetValues(typeof(ConnectionProtocol)))
                {
                    tbComboProtocol.Items.Add(protocol.ToString());
                }
                tbComboProtocol.Items.Add("SCP");
                tbComboProtocol.SelectedItem = ConnectionProtocol.SSH.ToString();
            }

            String prevSession = (string)tbComboSession.SelectedItem;
            tbComboSession.Items.Clear();
            foreach (string sessionName in PuttyDataHelper.GetSessionNames())
            {
                tbComboSession.Items.Add(sessionName);
            }
            tbComboSession.SelectedItem = prevSession ?? PuttyDataHelper.SessionEmptySettings;
        }

        private void toolStripButtonClearFields_Click(object sender, EventArgs e)
        {
            tbComboProtocol.SelectedItem = ConnectionProtocol.SSH.ToString();
            tbTxtBoxHost.Clear();
            tbTxtBoxLogin.Clear();
            tbTxtBoxPassword.Clear();
            tbComboSession.SelectedItem = PuttyDataHelper.SessionEmptySettings;
        }

        /// <summary>
        /// Show selector below toolbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tsBtnSelectDocs_Click(object sender, EventArgs e)
        {
            Rectangle rect = tbBtnSelectDocs.Bounds;
            int top = tsCommands.Top + tsCommands.Height + 3;
            int left = rect.Left + rect.Width - sendCommandsDocumentSelector.Width + 3;
            sendCommandsDocumentSelector.StartPosition = FormStartPosition.Manual;
            sendCommandsDocumentSelector.Location = PointToScreen(new Point(left, top));
            sendCommandsDocumentSelector.Show();
        }

        private void tsSendCommandCombo_KeyDown(object sender, KeyEventArgs e)
        {
            if (Log.Logger.IsEnabledFor(Level.Trace))
            {
                Log.DebugFormat("Keys={0}, control={1}, shift={2}, keyData={3}", e.KeyCode, e.Control, e.Shift, e.KeyData);
            }
            if (e.KeyCode == Keys.Down)
            {
                if (tsSendCommandCombo.Items.Count > 0)
                {
                    commandMRUIndex--;
                    if (commandMRUIndex < 0)
                    {
                        commandMRUIndex = tsSendCommandCombo.Items.Count - 1;
                    }
                    if (commandMRUIndex >= 0)
                    {
                        tsSendCommandCombo.SelectedIndex = commandMRUIndex;                        
                        tsSendCommandCombo.SelectionStart = tsSendCommandCombo.Text.Length;
                    }
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                for(int i = 0; i < tsSendCommandCombo.Items.Count; i++)
                {
                    Console.WriteLine("{0} {1} MRUOld:{2}", i, ((HistoryEntry)tsSendCommandCombo.Items[i]), commandMRUIndex);
                }

                if (tsSendCommandCombo.Items.Count > 0)
                {
                    commandMRUIndex++;
                    if (commandMRUIndex >= tsSendCommandCombo.Items.Count)
                    {
                        commandMRUIndex = 0;
                    }
                    if (commandMRUIndex < tsSendCommandCombo.Items.Count)
                    {
                        tsSendCommandCombo.SelectedIndex = commandMRUIndex;
                        tsSendCommandCombo.SelectionStart = tsSendCommandCombo.Text.Length;
                    }
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // send commands
                TrySendCommandsFromToolbar(new CommandData(tsSendCommandCombo.Text, new KeyEventArgs(Keys.Enter)), !tbBtnMaskText.Checked);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode != Keys.ControlKey)
            {
                // special keys
                TrySendCommandsFromToolbar(new CommandData(e), !tbBtnMaskText.Checked);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }


        private void tbBtnSendCommand_Click(object sender, EventArgs e)
        {
            TrySendCommandsFromToolbar(!tbBtnMaskText.Checked);
        }

        private void toggleCommandMaskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tbBtnMaskText.PerformClick();
        }

        private void tbBtnMaskText_Click(object sender, EventArgs e)
        {
            IntPtr handle = NativeMethods.GetWindow(tsSendCommandCombo.ComboBox.Handle, NativeMethods.GetWindowCmd.GW_CHILD);
            NativeMethods.SendMessage(handle, NativeMethods.EM_SETPASSWORDCHAR, tbBtnMaskText.Checked ? '*' : 0, 0);
            tsSendCommandCombo.ComboBox.Refresh();
        }

        /// <summary>Send command from send command toolbar to open sessions</summary>
        /// <param name="saveHistory">If true, save the history in the command toolbar combobox</param>
        /// <returns>The number of commands sent</returns>
        private int TrySendCommandsFromToolbar(bool saveHistory)
        {
            return TrySendCommandsFromToolbar(new CommandData(tsSendCommandCombo.Text), saveHistory);
        }

        /// <summary>Send commands to open sessions</summary>
        /// <param name="command">The <seealso cref="CommandData"/> object containing text and or keyboard commands</param>
        /// <param name="saveHistory">If True, save the history in the command toolbar combobox</param>
        /// <returns>The number terminals commands have been sent to</returns>
        private int TrySendCommandsFromToolbar(CommandData command, bool saveHistory)
        {
            int sent = 0;
            if (string.IsNullOrEmpty(command?.Command)) return 0;

            if (DockPanel.Contents.Count > 0)
            {
                foreach (IDockContent doc in VisualOrderTabSwitchStrategy.GetDocuments(DockPanel))
                {
                    if (doc is CtlPuttyPanel)
                    {
                        CtlPuttyPanel panel = doc as CtlPuttyPanel;
                        if (sendCommandsDocumentSelector.IsDocumentSelected(panel))
                        {
                            IntPtr hPtr = panel.AppPanel.AppWindowHandle;
                            int handle = hPtr.ToInt32();
                            //Log.InfoFormat("SendCommand: session={0}, command=[{1}], handle={2}", panel.Session.SessionId, command, handle);

                            command.SendToTerminal(handle);

                            sent++;                 
                        }
                    }
                }                 

                if (sent > 0)
                {
                    // success...clear text and save in mru                    
                    if (!string.IsNullOrEmpty(command.Command) && saveHistory)
                    {
                        if (InvokeRequired)
                        {
                            BeginInvoke((MethodInvoker)delegate {
                                tsCommandHistory.Insert(0, new HistoryEntry() { Command = command.Command });
                            });
                        }
                        else
                        {                            
                            tsCommandHistory.Insert(0, new HistoryEntry() { Command = command.Command });
                        }                       
                    }

                    if (InvokeRequired)
                    {
                        BeginInvoke((MethodInvoker)delegate {
                            tsSendCommandCombo.Text = string.Empty;
                        });
                    }
                    else
                    {
                        tsSendCommandCombo.Text = string.Empty;
                    }
                }
            }
            return sent;
        }

        #endregion

        #region Mouse and Keyboard Hooks

        private static IntPtr SetKBHook(NativeMethods.LowLevelKMProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // Intercept keyboard messages for Ctrl-F4 and Ctrl-Tab handling
        private IntPtr KBHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys keys = (Keys)vkCode;

                // track key state globally for control/alt/shift is up/down
                bool isKeyDown = wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN;
                if (keys == Keys.LControlKey || keys == Keys.RControlKey) { isControlDown = isKeyDown; }
                if (keys == Keys.LShiftKey || keys == Keys.RShiftKey) { isShiftDown = isKeyDown; }
                if (keys == Keys.LMenu || keys == Keys.RMenu) { isAltDown = isKeyDown; }

                if (Log.Logger.IsEnabledFor(Level.Trace))
                {
                    Log.DebugFormat("### KBHook: nCode={0}, wParam={1}, lParam={2} ({4,-4} - {3}) [{5}{6}{7}]",
                        nCode, wParam, vkCode, keys, isKeyDown ? "Down" : "Up",
                        isControlDown ? "Ctrl" : "", isAltDown ? "Alt" : "", isAltDown ? "Shift" : "");
                }

                if (IsForegroundWindow(this))
                {
                    // SuperPutty or Putty is the window in front...

                    if (keys == Keys.LControlKey || keys == Keys.RControlKey)
                    {
                        // If Ctrl-Tab has been pressed to move to an older panel then
                        // make it current panel when Ctrl key is finally released.
                        if (SuperPuTTY.Settings.EnableControlTabSwitching && !isControlDown && !isShiftDown)
                        {
                            tabSwitcher.CurrentDocument = (ToolWindow)DockPanel.ActiveDocument;
                        }
                    }

                    if (keys == Keys.LShiftKey || keys == Keys.RShiftKey)
                    {
                        // If Ctrl-Shift-Tab has been pressed to move to an older panel then
                        // make it current panel when both keys are finally released.
                        if (SuperPuTTY.Settings.EnableControlTabSwitching && !isControlDown && !isShiftDown)
                        {
                            tabSwitcher.CurrentDocument = (ToolWindow)DockPanel.ActiveDocument;
                        }
                    }

                    if (SuperPuTTY.Settings.EnableControlTabSwitching && isControlDown && !isShiftDown && keys == Keys.Tab)
                    {
                        // Operator has pressed Ctrl-Tab, make next PuTTY panel active
                        if (isKeyDown && DockPanel.ActiveDocument is ToolWindowDocument)
                        {
                            if (tabSwitcher.MoveToNextDocument())
                            {
                                // Eat the keystroke
                                return (IntPtr)1;
                            }
                        }
                    }

                    if (SuperPuTTY.Settings.EnableControlTabSwitching && isControlDown && isShiftDown && keys == Keys.Tab)
                    {
                        // Operator has pressed Ctrl-Shift-Tab, make previous PuTTY panel active
                        if (isKeyDown && DockPanel.ActiveDocument is ToolWindowDocument)
                        {
                            if (tabSwitcher.MoveToPrevDocument())
                            {
                                // Eat the keystroke
                                return (IntPtr)1;
                            }
                        }
                    }

                    // misc action handling (eat keyup and down)
                    if (SuperPuTTY.Settings.EnableKeyboadShortcuts &&
                        isKeyDown &&
                        keys != Keys.LControlKey && keys != Keys.RControlKey &&
                        keys != Keys.LMenu && keys != Keys.RMenu &&
                        keys != Keys.LShiftKey && keys != Keys.RShiftKey)
                    {
                        if (isControlDown) keys |= Keys.Control;
                        if (isShiftDown) keys |= Keys.Shift;
                        if (isAltDown) keys |= Keys.Alt;

                        if (Log.Logger.IsEnabledFor(Level.Trace))
                        {
                            Log.DebugFormat("#### TryExecute shortcut: keys={0}", keys);
                        }
                        if (shortcuts.TryGetValue(keys, out var action))
                        {
                            // post action to avoid getting errant keystrokes (e.g. allow current to be eaten)
                            BeginInvoke(new Action(() =>
                            {
                                ExecuteSuperPuttyAction(action);
                            }));
                            return (IntPtr)1;
                        }
                    }
                }


            }

            return NativeMethods.CallNextHookEx(kbHookID, nCode, wParam, lParam);
        }

        private static IntPtr SetMHook(NativeMethods.LowLevelKMProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr MHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM.LBUTTONUP || wParam == (IntPtr)NativeMethods.WM.RBUTTONUP) && IsForegroundWindow(this))
            {
                BringToFront();
                //if (!Menu_IsMouseOver()) dockPanel.Focus();
            }
            return NativeMethods.CallNextHookEx(mHookID, nCode, wParam, lParam);
        }

        private static bool IsForegroundWindow(Form parent)
        {
            IntPtr fgWindow = NativeMethods.GetForegroundWindow();
            if (parent.Handle == fgWindow) return true; // main form is FG
            //foreach (Form f in Application.OpenForms) { if (f.Handle == fgWindow) return true; }
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                NativeMethods.EnumWindowProc childProc = EnumWindow;
                NativeMethods.EnumChildWindows(parent.Handle, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result.Count > 0;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (handle == NativeMethods.GetForegroundWindow()) list.Add(handle);
            if (list.Count == 0) return true; else return false;
        }

        void UpdateShortcutsFromSettings()
        {
            // remove old
            shortcuts.Clear();
            fullScreenToolStripMenuItem.ShortcutKeys = Keys.None;
            optionsToolStripMenuItem.ShortcutKeys = Keys.None;

            // reload new definitions
            foreach (KeyboardShortcut ks in SuperPuTTY.Settings.LoadShortcuts())
            {
                try
                {
                    // shortcuts
                    SuperPuttyAction action = (SuperPuttyAction)Enum.Parse(typeof(SuperPuttyAction), ks.Name);
                    Keys keys = ks.Key | ks.Modifiers;
                    if (keys != Keys.None)
                    {
                        shortcuts.Add(keys, action);
                    }

                    // sync menu items
                    switch (action)
                    {
                        case SuperPuttyAction.FullScreen:
                            fullScreenToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.Options:
                            optionsToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.OpenSession:
                            openSessionToolStripMenuItem.ShortcutKeys = keys;
                            break;
                        case SuperPuttyAction.SwitchSession:
                            switchSessionToolStripMenuItem.ShortcutKeys = keys;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error creating shortcut: " + ks + ", disabled.", ex);
                }

            }
        }

        bool ExecuteSuperPuttyAction(SuperPuttyAction action)
        {
            CtlPuttyPanel activePanel = DockPanel.ActiveDocument as CtlPuttyPanel;
            bool success = true;

            Log.InfoFormat("Executing action, name={0}", action);
            switch (action)
            {
                case SuperPuttyAction.CloseTab:
                    if (DockPanel.ActiveDocument is ToolWindow win) { win.Close(); }
                    break;
                case SuperPuttyAction.NextTab:
                    tabSwitcher.MoveToNextDocument();
                    break;
                case SuperPuttyAction.PrevTab:
                    tabSwitcher.MoveToPrevDocument();
                    break;
                case SuperPuttyAction.FullScreen:
                    ToggleFullScreen();
                    break;
                case SuperPuttyAction.OpenSession:
                    KeyEventWindowActivator.ActivateForm(this);
                    openSessionToolStripMenuItem.PerformClick();
                    break;
                case SuperPuttyAction.SwitchSession:
                    KeyEventWindowActivator.ActivateForm(this);
                    switchSessionToolStripMenuItem.PerformClick();
                    break;
                case SuperPuttyAction.Options:
                    KeyEventWindowActivator.ActivateForm(this);
                    optionsToolStripMenuItem.PerformClick();
                    break;
                case SuperPuttyAction.DuplicateSession:
                    if (activePanel?.Session != null)
                        SuperPuTTY.OpenPuttySession(activePanel.Session);
                    break;
                case SuperPuttyAction.GotoCommandBar:
                    if (!fullscreenViewState.IsFullScreen)
                    {
                        KeyEventWindowActivator.ActivateForm(this);
                        if (!tsCommands.Visible)
                        {
                            toggleCheckedState(sendCommandsToolStripMenuItem, EventArgs.Empty);
                        }
                        tsSendCommandCombo.Focus();
                    }
                    break;
                case SuperPuttyAction.GotoConnectionBar:
                    // perhaps consider allowing this later...need to really have a better approach to the state saving/invoking the toggle.
                    if (!fullscreenViewState.IsFullScreen)
                    {
                        KeyEventWindowActivator.ActivateForm(this);
                        if (!tsConnect.Visible)
                        {
                            toggleCheckedState(quickConnectionToolStripMenuItem, EventArgs.Empty);
                        }
                        tbTxtBoxHost.Focus();
                    }
                    break;
                case SuperPuttyAction.FocusActiveSession:
                    // focus on current super putty session...or at least try to
                    KeyEventWindowActivator.ActivateForm(this);
                    activePanel?.SetFocusToChildApplication("ExecuteAction");
                    break;
                case SuperPuttyAction.OpenScriptEditor:
                    KeyEventWindowActivator.ActivateForm(this);
                    toolStripButtonRunScript_Click(this, EventArgs.Empty);
                    break;
                case SuperPuttyAction.RenameTab:                    
                    if (activePanel?.Session != null)
                    {
                        dlgRenameItem dialog = new dlgRenameItem
                        {
                            ItemName = activePanel.Text,
                            DetailName = activePanel.Session.SessionId
                        };

                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            activePanel.Text = activePanel.TextOverride = dialog.ItemName;                            
                        }                        
                    }
                    break;
                default:
                    success = false;
                    break;
            }

            return success;
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.Alt) == Keys.Alt)
            {
                menuStrip1.Visible = true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #region Tray
        private void frmSuperPutty_Resize(object sender, EventArgs e)
        {
            if (SuperPuTTY.Settings.MinimizeToTray)
            {
                if (FormWindowState.Minimized == WindowState && !notifyicon.Visible)
                {
                    notifyicon.Visible = true;
                    ShowInTaskbar = false;

                }
                else if (FormWindowState.Normal == WindowState || FormWindowState.Maximized == WindowState)
                {
                    notifyicon.Visible = false;
                    lastNonMinimizedWindowState = WindowState;
                }
            }

            SaveLastWindowBounds();
        }

        private void SaveLastWindowBounds()
        {
            if (WindowState == FormWindowState.Normal)
            {
                lastNormalDesktopBounds = DesktopBounds;
            }
        }

        private void notifyicon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowInTaskbar = true;
                WindowState = lastNonMinimizedWindowState;
            }
        }

        private void exitSuperPuTTYToolStripMenuItem_Click(object sender, EventArgs e)
        {
            forceClose = true;
            Close();
        }

        #endregion

        #region Diagnostics

        private void logWindowLocationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (IDockContent c in DockPanel.Documents)
            {
                if (c is CtlPuttyPanel panel)
                {
                    NativeMethods.RECT rect = new NativeMethods.RECT();
                    NativeMethods.GetWindowRect(panel.AppPanel.AppWindowHandle, ref rect);
                    Point p = panel.PointToScreen(new Point());
                    Log.InfoFormat(
                        "[{0,-20} {1,8}] WindowLocations: panel={2}, putty={3}, x={4}, y={5}",
                        panel.Text + (panel == panel.DockPanel.ActiveDocument ? "*" : ""),
                        panel.AppPanel.AppWindowHandle,
                        panel.DisplayRectangle,
                        rect, p.X, p.Y);
                }
            }
        }

        private void cleanUpStrayProcessesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Regex regex = new Regex(@"^(putty|pscp|cthelper|bash|mintty)$", RegexOptions.IgnoreCase);
                IDictionary<string, List<Process>> procs = new Dictionary<string, List<Process>>();
                foreach (Process p in Process.GetProcesses())
                {
                    if (regex.IsMatch(p.ProcessName))
                    {
                        if (!procs.TryGetValue(p.ProcessName, out var procList))
                        {
                            procList = new List<Process>();
                            procs.Add(p.ProcessName, procList);
                        }
                        procList.Add(p);
                    }
                }

                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, List<Process>> plist in procs)
                {
                    sb.AppendFormat("{0} ({1})", plist.Key, plist.Value.Count).AppendLine();
                }
                if (procs.Count > 0 && DialogResult.OK == MessageBox.Show(this, sb.ToString(), "Kill Processes?", MessageBoxButtons.OKCancel))
                {
                    int success = 0;
                    int error = 0;
                    foreach (KeyValuePair<string, List<Process>> plist in procs)
                    {
                        foreach (Process procToKill in plist.Value)
                        {
                            try
                            {
                                procToKill.Kill();
                                success++;
                            }
                            catch (Exception ex)
                            {
                                Log.ErrorFormat("Error killing proc: {0} ({1}) {2}", procToKill.ProcessName, procToKill.Id, ex);
                                error++;
                            }
                        }
                    }
                    MessageBox.Show(this, string.Format("Killed {0} processes, {1} errors", success, error), "Clean Up Complete");
                }
            }
            catch (Exception ex)
            {
                string msg = "";
                Log.Error(msg, ex);
                MessageBox.Show(this, msg, "Error Cleaning Processes");
            }
        }

        private void menuStrip1_MenuDeactivate(object sender, EventArgs e)
        {
            menuStrip1.Visible = SuperPuTTY.Settings.ShowMenuBar;
        }

        /// <summary>Check for a newer version of the SuperPuTTY Application</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Log.Info("Checking for application update");
            try {
                httpRequest httpUpdateRequest = new httpRequest();
                httpUpdateRequest.MakeRequest("https://api.github.com/repos/jimradford/superputty/releases/latest", delegate (bool success, string content)
                {
                    if (success)
                    {
                        DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(GitRelease));
                        MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(content));
                        GitRelease latest = (GitRelease)js.ReadObject(ms);
                        ms.Close();

                        Version latest_version = new Version(latest.version.Trim());
                        Version SuperPuTTY_version = new Version(SuperPuTTY.Version);

                        if (latest_version.CompareTo(SuperPuTTY_version) > 0)
                        {
                            Log.Info("New Application version found! " + latest.version);

                            if (MessageBox.Show("An updated version of SuperPuTTY (" + latest.version + ") is Available Would you like to visit the download page to upgrade?",
                                "SuperPutty Update Found",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question,
                                MessageBoxDefaultButton.Button1,
                                MessageBoxOptions.DefaultDesktopOnly) == DialogResult.Yes)
                            {
                                Process.Start(latest.release_url);
                            }
                        }
                        else
                        {
                            if (sender.ToString().Equals(checkForUpdatesToolStripMenuItem.Text))
                            {
                                MessageBox.Show("You are running the latest version of SuperPutty", "SuperPuTTY Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("There was an error while checking for updates. Please try again later.", "Error during update check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Log.Warn("An Error occurred trying to check for program updates: " + content);                        
                    }
                });
            }
            catch (System.Net.WebException ex)
            {
                Log.Warn($"An Exception occurred while trying to check for program updates: {ex}");
            }
            catch (FormatException ex)
            {
                Log.Warn($"An Exception occurred while trying to check for program updates: {ex}");
            }
        }
        #endregion

        protected override void WndProc(ref Message m)
        {
            bool callBase = focusHelper.WndProcForFocus(ref m);
            if (callBase)
            {
                base.WndProc(ref m);
            }
        }

        public enum TabTextBehavior
        {
            Static,
            Dynamic,
            Mixed
        }

        /// <summary>Open a window which will allow multiline scripts (commands) to be sent to hosts.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButtonRunScript_Click(object sender, EventArgs e)
        {            
            dlgScriptEditor editor = new dlgScriptEditor();
            editor.ScriptReady += Editor_ScriptReady;
            editor.SetDesktopLocation(MousePosition.X, MousePosition.Y);                       
            editor.Show();           
        }
        
        /// <summary>Process the script.</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Editor_ScriptReady(object sender, ExecuteScriptEventArgs e)
        {            
            if (!String.IsNullOrEmpty(e.Script))
            {
                string[] scriptlines = e.Script.Split('\n');
                if (scriptlines.Length > 0 
                    && e.IsSPSL)
                {
                    new Thread(delegate()
                    {
                        foreach (string line in scriptlines)
                        {
                            SPSL.TryParseScriptLine(line, out var command);
                            if (command != null)
                            {
                                TrySendCommandsFromToolbar(command, false);
                            }
                        }
                    }).Start();
                }
                else // Not a spsl script
                {
                    foreach (string line in scriptlines)
                    {
                        TrySendCommandsFromToolbar(new CommandData(line.TrimEnd('\n'), new KeyEventArgs(Keys.Enter)), !tbBtnMaskText.Checked);
                    }
                }
            }
        }
    }
}
