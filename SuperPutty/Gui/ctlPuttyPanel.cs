/*
 * Copyright (c) 2009 - 2015 Jim Radford http://www.jimradford.com
 * Copyright (c) 2012 John Peterson
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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using log4net;
using log4net.Core;
using SuperPutty.App;
using SuperPutty.Data;
using SuperPutty.Utils;
using WeifenLuo.WinFormsUI.Docking;

namespace SuperPutty.Gui
{
    /// <inheritdoc />
    /// <summary>A control that hosts a putty window</summary>
    public partial class CtlPuttyPanel : ToolWindowDocument
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CtlPuttyPanel));

        private static readonly int RefocusAttempts = Convert.ToInt32(ConfigurationManager.AppSettings["SuperPuTTY.RefocusAttempts"] ?? "5");
        private static readonly int RefocusIntervalMs = Convert.ToInt32(ConfigurationManager.AppSettings["SuperPuTTY.RefocusIntervalMs"] ?? "80");

        private readonly PuttyStartInfo _mPuttyStartInfo;
        private ApplicationPanel _mAppPanel;
        private readonly SessionData _mSession;
        private readonly PuttyClosedCallback _mApplicationExit;

        public CtlPuttyPanel(SessionData session, PuttyClosedCallback callback)
        {
            _mSession = session;
            _mApplicationExit = callback;
            _mPuttyStartInfo = new PuttyStartInfo(session);

            InitializeComponent();

            Text = session.SessionName;
            TabText = session.SessionName;
            TextOverride = session.SessionName;

            CreatePanel();
            AdjustMenu();
        }

        /// <summary>Gets or sets the text displayed on a tab.</summary>
        public sealed override string Text
        {
            get => base.Text;
            set
            {
                TabText = value?.Replace(@"&", @"&&");
                base.Text = value;

                if (Log.Logger.IsEnabledFor(Level.Trace))
                {
                    Log.DebugFormat("SetText: text={0}", value);
                }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            ToolTipText = Text;
        }

        private void CreatePanel()
        {
            _mAppPanel = new ApplicationPanel();
            SuspendLayout();            
            _mAppPanel.Dock = DockStyle.Fill;
            _mAppPanel.ApplicationName = _mPuttyStartInfo.Executable;
            _mAppPanel.ApplicationParameters = _mPuttyStartInfo.Args;
            _mAppPanel.ApplicationWorkingDirectory = _mPuttyStartInfo.WorkingDir;
            _mAppPanel.Location = new System.Drawing.Point(0, 0);
            _mAppPanel.Name = _mSession.SessionId; // "applicationControl1";
            _mAppPanel.Size = new System.Drawing.Size(Width, Height);
            _mAppPanel.TabIndex = 0;            
            _mAppPanel.m_CloseCallback = _mApplicationExit;
            Controls.Add(_mAppPanel);

            ResumeLayout();
        }

        void AdjustMenu()
        {
            // for mintty, disable the putty menu items
            if (Session.Proto == ConnectionProtocol.Mintty)
            {
                toolStripPuttySep1.Visible = false;
                eventLogToolStripMenuItem.Visible = false;
                toolStripPuttySep2.Visible = false;
                changeSettingsToolStripMenuItem.Visible = false;
                copyAllToClipboardToolStripMenuItem.Visible = false;
                restartSessionToolStripMenuItem.Visible = false;
                clearScrollbackToolStripMenuItem.Visible = false;
                resetTerminalToolStripMenuItem.Visible = false;
            }
        }

        void CreateMenu()
        {
            newSessionToolStripMenuItem.Enabled = SuperPuTTY.Settings.PuttyPanelShowNewSessionMenu;
            if (SuperPuTTY.Settings.PuttyPanelShowNewSessionMenu)
            {
                contextMenuStrip1.SuspendLayout();

                // BBB: do i need to dispose each one?
                newSessionToolStripMenuItem.DropDownItems.Clear();
                foreach (SessionData session in SuperPuTTY.GetAllSessions())
                {
                    ToolStripMenuItem tsmiParent = newSessionToolStripMenuItem;
                    foreach (string part in SessionData.GetSessionNameParts(session.SessionId))
                    {
                        if (part == session.SessionName)
                        {
                            var newSessionTsmi = new ToolStripMenuItem
                            {
                                Tag = session,
                                Text = session.SessionName
                            };
                            newSessionTsmi.Click += newSessionTSMI_Click;
                            newSessionTsmi.ToolTipText = session.ToString();
                            tsmiParent.DropDownItems.Add(newSessionTsmi);
                        }
                        else
                        {
                            if (tsmiParent.DropDownItems.ContainsKey(part))
                            {
                                tsmiParent = (ToolStripMenuItem)tsmiParent.DropDownItems[part];
                            }
                            else
                            {
                                ToolStripMenuItem newSessionFolder = new ToolStripMenuItem(part) {Name = part};
                                tsmiParent.DropDownItems.Add(newSessionFolder);
                                tsmiParent = newSessionFolder;
                            }
                        }
                    }
                }
                contextMenuStrip1.ResumeLayout();
            }

            DockPane pane = GetDockPane();
            if (pane != null)
            {
                closeOthersToTheRightToolStripMenuItem.Enabled =
                    pane.Contents.IndexOf(this) != pane.Contents.Count - 1;
            }
            closeOthersToolStripMenuItem.Enabled = DockPanel.DocumentsCount > 1;
            closeAllToolStripMenuItem.Enabled = DockPanel.DocumentsCount > 1;
        }

        private void closeSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void closeOthersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var docs = from doc in DockPanel.DocumentsToArray()
                       where doc is ToolWindowDocument && doc != this
                       select doc as ToolWindowDocument;
            CloseDocs("Close Others", docs);
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var docs = from doc in DockPanel.DocumentsToArray()
                       where doc is ToolWindowDocument
                       select doc as ToolWindowDocument;
            CloseDocs("Close All", docs);
        }

        private void closeOthersToTheRightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // find the dock pane with this window
            DockPane pane = GetDockPane();
            if (pane != null)
            {
                // found the pane
                List<ToolWindowDocument> docsToClose = new List<ToolWindowDocument>();
                bool close = false;
                foreach (IDockContent content in new List<IDockContent>(pane.Contents))
                {
                    if (content == this)
                    {
                        close = true;
                        continue;
                    }
                    if (close)
                    {
                        if (content is ToolWindowDocument win)
                        {
                            docsToClose.Add(win);
                        }
                    }
                }
                if (docsToClose.Count > 0)
                {
                    CloseDocs("Close Other To the Right", docsToClose);
                }
            }
        }

        void CloseDocs(string source, IEnumerable<ToolWindowDocument> docsToClose)
        {
            var docsToCloseList = docsToClose as IList<ToolWindowDocument> ?? docsToClose.ToList();
            int n = docsToCloseList.Count;
            Log.InfoFormat("Closing mulitple docs: source={0}, count={1}, conf={2}", source, n, SuperPuTTY.Settings.MultipleTabCloseConfirmation);

            bool okToClose = true;
            if (SuperPuTTY.Settings.MultipleTabCloseConfirmation && n > 1)
            {
                okToClose = DialogResult.Yes == MessageBox.Show(this, string.Format(LocalizedText.CtlPuttyPanel_CloseDocs_Close__0__Tabs_, n), source, MessageBoxButtons.YesNo);
            }

            if (okToClose)
            {
                foreach (ToolWindowDocument doc in docsToCloseList)
                {
                    doc.Close();
                }
            }
        }

        DockPane GetDockPane()
        {
            return DockPanel.Panes.FirstOrDefault(pane => pane.Contents.Contains(this));
        }

        /// <summary>
        /// Reset the focus to the child application window
        /// </summary>
        internal void SetFocusToChildApplication(string caller)
        {
            if (!_mAppPanel.ExternalProcessCaptured) { return; }

            bool success = false;
            for (int i = 0; i < RefocusAttempts; i++)
            {
                Thread.Sleep(RefocusIntervalMs);
                if (_mAppPanel.ReFocusPuTTY(caller))
                {
                    if (i > 0)
                    {
                        Log.DebugFormat("SetFocusToChildApplication success after {0} attempts", i + 1);
                    }
                    success = true;
                    break;
                }
            }

            if (!success)
            {
                Log.WarnFormat("Unable to SetFocusToChildApplication, {0}", Text);
            }
        }

        protected override string GetPersistString()
        {
            string str = string.Format(@"{0}?SessionId={1}&TabName={2}", 
                GetType().FullName, 
                HttpUtility.UrlEncode(_mSession.SessionId), 
                HttpUtility.UrlEncode(TextOverride));
            return str;
        }

        /// <summary>Restore sessions from a string containing previous sessions</summary>
        /// <param name="persistString">A string containing the sessions to restore</param>
        /// <returns>The <seealso cref="CtlPuttyPanel"/> object which is the parent of the hosted putty application, null if unable to start session</returns>
        public static CtlPuttyPanel FromPersistString(String persistString)
        {
            if (persistString == null) throw new ArgumentNullException(nameof(persistString));
            CtlPuttyPanel panel = null;
            if (persistString.StartsWith(typeof(CtlPuttyPanel).FullName ?? throw new InvalidOperationException()))
            {
                int idx = persistString.IndexOf("?", StringComparison.Ordinal);
                if (idx != -1)
                {
                    NameValueCollection data = HttpUtility.ParseQueryString(persistString.Substring(idx + 1));
                    string sessionId = data["SessionId"] ?? data["SessionName"];
                    string tabName = data["TabName"];

                    Log.InfoFormat("Restoring putty session, sessionId={0}, tabName={1}", sessionId, tabName);

                    SessionData session = SuperPuTTY.GetSessionById(sessionId);
                    if (session != null)
                    {
                        panel = SuperPuTTY.OpenPuttySession(session);
                        if (panel == null)
                        {
                            Log.WarnFormat("Could not restore putty session, sessionId={0}", sessionId);
                        }
                        else
                        {
                            panel.Icon = SuperPuTTY.GetIconForSession(session);
                            panel.Text = tabName;
                            panel.TextOverride = tabName;
                        }
                    }
                    else
                    {
                        Log.WarnFormat("Session not found, sessionId={0}", sessionId);
                    }
                }
                else
                {
                    idx = persistString.IndexOf(":", StringComparison.Ordinal);
                    if (idx != -1)
                    {
                        string sessionId = persistString.Substring(idx + 1);
                        Log.InfoFormat("Restoring putty session, sessionId={0}", sessionId);
                        SessionData session = SuperPuTTY.GetSessionById(sessionId);
                        if (session != null)
                        {
                            panel = SuperPuTTY.OpenPuttySession(session);
                        }
                        else
                        {
                            Log.WarnFormat("Session not found, sessionId={0}", sessionId);
                        }
                    }
                }
            }
            return panel;
        }

        private void aboutPuttyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.chiark.greenend.org.uk/~sgtatham/putty/");
        }

 
        private void duplicateSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SuperPuTTY.OpenPuttySession(_mSession);
        }

        private void renameTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dlgRenameItem dialog = new dlgRenameItem
            {
                ItemName = Text,
                DetailName = _mSession.SessionId
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Text = dialog.ItemName;
                TextOverride = dialog.ItemName;
            }
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _mAppPanel?.RefreshAppWindow();
        }

        public SessionData Session => _mSession;
        public ApplicationPanel AppPanel => _mAppPanel;

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            CreateMenu();
        }

        private void newSessionTSMI_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is SessionData session)
            {
                SuperPuTTY.OpenPuttySession(session);
            }
        }

        private void puTTYMenuTSMI_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem) sender;
            string[] tags = ((ToolStripMenuItem)sender).Tag.ToString().Split(';');
            uint[] commands = new uint[tags.Length];
            for (int i = 0; i < tags.Length; ++i)
            {
                commands[i] = Convert.ToUInt32(tags[i], 16);
                Log.DebugFormat("Sending Putty Command: menu={2}, tag={0}, command={1}", tags[i], commands[i], menuItem.Text);
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    SetFocusToChildApplication("MenuHandler");
                    foreach (uint t in commands)
                    {
                        NativeMethods.SendMessage(_mAppPanel.AppWindowHandle, (uint)NativeMethods.WM.SYSCOMMAND, t, 0);
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error sending command menu command to embedded putty: {0}", ex);
                }
            });
        }

        public bool AcceptCommands
        {
            get => acceptCommandsToolStripMenuItem.Checked;
            set => acceptCommandsToolStripMenuItem.Checked = value;
        }

        public string TextOverride { get; set; }

    }
}
