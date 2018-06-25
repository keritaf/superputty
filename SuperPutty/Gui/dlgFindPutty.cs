/*
 * Copyright (c) 2009 Jim Radford http://www.jimradford.com
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
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using SuperPutty.Utils;
using SuperPutty.Gui;

namespace SuperPutty
{
    public partial class dlgFindPutty : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(dlgFindPutty));

        private string OrigSettingsFolder { get; }
        private string OrigDefaultLayoutName { get; set; }

        private BindingList<KeyboardShortcut> Shortcuts { get; }

        public dlgFindPutty()
        {
            InitializeComponent();

            var puttyExe = SuperPuTTY.Settings.PuttyExe;
            var pscpExe = SuperPuTTY.Settings.PscpExe;

            var firstExecution = string.IsNullOrEmpty(puttyExe);
            textBoxFilezillaLocation.Text = getPathExe(@"\FileZilla FTP Client\filezilla.exe", SuperPuTTY.Settings.FileZillaExe, firstExecution);
            textBoxWinSCPLocation.Text = getPathExe(@"\WinSCP\WinSCP.exe", SuperPuTTY.Settings.WinSCPExe, firstExecution);
            textBoxVNCLocation.Text = getPathExe(@"\TightVNC\tvnviewer.exe", SuperPuTTY.Settings.VNCExe, firstExecution);

            // check for location of putty/pscp
            if (!string.IsNullOrEmpty(puttyExe) && File.Exists(puttyExe))
            {
                textBoxPuttyLocation.Text = puttyExe;
                if (!string.IsNullOrEmpty(pscpExe) && File.Exists(pscpExe))
                {
                    textBoxPscpLocation.Text = pscpExe;
                }
            }
            else if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)")))
            {
                if (File.Exists(Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\PuTTY\putty.exe"))
                {
                    textBoxPuttyLocation.Text = Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\PuTTY\putty.exe";
                    openFileDialog1.InitialDirectory = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                }

                if (File.Exists(Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\PuTTY\pscp.exe"))
                {

                    textBoxPscpLocation.Text = Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\PuTTY\pscp.exe";
                }
            }
            else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles")))
            {
                if (File.Exists(Environment.GetEnvironmentVariable("ProgramFiles") + @"\PuTTY\putty.exe"))
                {
                    textBoxPuttyLocation.Text = Environment.GetEnvironmentVariable("ProgramFiles") + @"\PuTTY\putty.exe";
                    openFileDialog1.InitialDirectory = Environment.GetEnvironmentVariable("ProgramFiles");
                }

                if (File.Exists(Environment.GetEnvironmentVariable("ProgramFiles") + @"\PuTTY\pscp.exe"))
                {
                    textBoxPscpLocation.Text = Environment.GetEnvironmentVariable("ProgramFiles") + @"\PuTTY\pscp.exe";
                }
            }            
            else
            {
                openFileDialog1.InitialDirectory = Application.StartupPath;
            }

            if (string.IsNullOrEmpty(SuperPuTTY.Settings.MinttyExe))
            {
                if (File.Exists(@"C:\cygwin\bin\mintty.exe"))
                {
                    textBoxMinttyLocation.Text = @"C:\cygwin\bin\mintty.exe";
                }
                if (File.Exists(@"C:\cygwin64\bin\mintty.exe"))
                {
                    textBoxMinttyLocation.Text = @"C:\cygwin64\bin\mintty.exe";
                }
            }
            else
            {
                textBoxMinttyLocation.Text = SuperPuTTY.Settings.MinttyExe;
            }
            
            // super putty settings (sessions and layouts)
            if (string.IsNullOrEmpty(SuperPuTTY.Settings.SettingsFolder))
            {
                // Set a default
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SuperPuTTY");
                if (!Directory.Exists(dir))
                {
                    Log.InfoFormat("Creating default settings dir: {0}", dir);
                    Directory.CreateDirectory(dir);
                }
                textBoxSettingsFolder.Text = dir;
            }
            else
            {
                textBoxSettingsFolder.Text = SuperPuTTY.Settings.SettingsFolder;
            }
            OrigSettingsFolder = SuperPuTTY.Settings.SettingsFolder;

            // tab text
            foreach(string s in Enum.GetNames(typeof(frmSuperPutty.TabTextBehavior)))
            {
                comboBoxTabText.Items.Add(s);
            }
            comboBoxTabText.SelectedItem = SuperPuTTY.Settings.TabTextBehavior;

            // tab switcher
            ITabSwitchStrategy selectedItem = null;
            foreach (ITabSwitchStrategy strat in TabSwitcher.Strategies)
            {
                comboBoxTabSwitching.Items.Add(strat);
                if (strat.GetType().FullName == SuperPuTTY.Settings.TabSwitcher)
                {
                    selectedItem = strat;
                }
            }
            comboBoxTabSwitching.SelectedItem = selectedItem ?? TabSwitcher.Strategies[0];

            // activator types
            comboBoxActivatorType.Items.Add(typeof(KeyEventWindowActivator).FullName);
            comboBoxActivatorType.Items.Add(typeof(CombinedWindowActivator).FullName);
            comboBoxActivatorType.Items.Add(typeof(SetFGWindowActivator).FullName);
            comboBoxActivatorType.Items.Add(typeof(RestoreWindowActivator).FullName);
            comboBoxActivatorType.Items.Add(typeof(SetFGAttachThreadWindowActivator).FullName);
            comboBoxActivatorType.SelectedItem = SuperPuTTY.Settings.WindowActivator;

            // search types
            foreach (string name in Enum.GetNames(typeof(SessionTreeview.SearchMode)))
            {
                comboSearchMode.Items.Add(name);
            }
            comboSearchMode.SelectedItem = SuperPuTTY.Settings.SessionsSearchMode;

            // default layouts
            InitLayouts();

            checkSingleInstanceMode.Checked = SuperPuTTY.Settings.SingleInstanceMode;
            checkConstrainPuttyDocking.Checked = SuperPuTTY.Settings.RestrictContentToDocumentTabs;
            checkRestoreWindow.Checked = SuperPuTTY.Settings.RestoreWindowLocation;
            checkExitConfirmation.Checked = SuperPuTTY.Settings.ExitConfirmation;
            checkExpandTree.Checked = SuperPuTTY.Settings.ExpandSessionsTreeOnStartup;
            checkMinimizeToTray.Checked = SuperPuTTY.Settings.MinimizeToTray;
            checkSessionsTreeShowLines.Checked = SuperPuTTY.Settings.SessionsTreeShowLines;
            checkConfirmTabClose.Checked = SuperPuTTY.Settings.MultipleTabCloseConfirmation;
            checkEnableControlTabSwitching.Checked = SuperPuTTY.Settings.EnableControlTabSwitching;
            checkEnableKeyboardShortcuts.Checked = SuperPuTTY.Settings.EnableKeyboadShortcuts;
            btnFont.Font = SuperPuTTY.Settings.SessionsTreeFont;
            btnFont.Text = ToShortString(SuperPuTTY.Settings.SessionsTreeFont);
            numericUpDownOpacity.Value = (decimal) SuperPuTTY.Settings.Opacity * 100;
            checkQuickSelectorCaseSensitiveSearch.Checked = SuperPuTTY.Settings.QuickSelectorCaseSensitiveSearch;
            checkShowDocumentIcons.Checked = SuperPuTTY.Settings.ShowDocumentIcons;
            checkRestrictFloatingWindows.Checked = SuperPuTTY.Settings.DockingRestrictFloatingWindows;
            checkSessionsShowSearch.Checked = SuperPuTTY.Settings.SessionsShowSearch;
            checkPuttyEnableNewSessionMenu.Checked = SuperPuTTY.Settings.PuttyPanelShowNewSessionMenu;
            checkBoxCheckForUpdates.Checked = SuperPuTTY.Settings.AutoUpdateCheck;
            textBoxHomeDirPrefix.Text = SuperPuTTY.Settings.PscpHomePrefix;
            textBoxRootDirPrefix.Text = SuperPuTTY.Settings.PscpRootHomePrefix;
            checkSessionTreeFoldersFirst.Checked = SuperPuTTY.Settings.SessiontreeShowFoldersFirst;
            checkBoxPersistTsHistory.Checked = SuperPuTTY.Settings.PersistCommandBarHistory;
            numericUpDown1.Value = SuperPuTTY.Settings.SaveCommandHistoryDays;
            checkBoxAllowPuttyPWArg.Checked = SuperPuTTY.Settings.AllowPlainTextPuttyPasswordArg;
            textBoxPuttyDefaultParameters.Text = SuperPuTTY.Settings.PuttyDefaultParameters;

            if (SuperPuTTY.IsFirstRun)
            {
                ShowIcon = true;
                ShowInTaskbar = true;
            }

            // shortcuts
            Shortcuts = new BindingList<KeyboardShortcut>();
            foreach (KeyboardShortcut ks in SuperPuTTY.Settings.LoadShortcuts())
            {
                Shortcuts.Add(ks);
            }
            dataGridViewShortcuts.DataSource = Shortcuts;
        }


        /// <summary>
        /// return the path of the exe. 
        /// return settingValue if it is a valid path, or if searchPath is false, else search and return the default location of pathInProgramFile.
        /// </summary>
        /// <param name="pathInProgramFile">relative path of file (in ProgramFiles or ProgramFiles(x86))</param>
        /// <param name="settingValue">path stored in settings </param>
        /// <param name="searchPath">boolean </param>
        /// <returns>The path of the exe</returns>
        private string getPathExe(string pathInProgramFile, string settingValue, bool searchPath)
        {
            if ((!string.IsNullOrEmpty(settingValue) && File.Exists(settingValue)) || !searchPath)
            {
                return settingValue;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)")))
            {
                if (File.Exists(Environment.GetEnvironmentVariable("ProgramFiles(x86)") + pathInProgramFile))
                {
                    return Environment.GetEnvironmentVariable("ProgramFiles(x86)") + pathInProgramFile;
                }
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles")))
            {
                if (File.Exists(Environment.GetEnvironmentVariable("ProgramFiles") + pathInProgramFile))
                {
                    return Environment.GetEnvironmentVariable("ProgramFiles") + pathInProgramFile;
                }
            }

            return "";
        }


        private void InitLayouts()
        {
            string defaultLayout;
            List<string> layouts = new List<string>();
            if (SuperPuTTY.IsFirstRun)
            {
                layouts.Add(string.Empty);
                // HACK: first time so layouts directory not set yet so layouts don't exist...
                //       preload <AutoRestore> so we can set it as default
                layouts.Add(LayoutData.AutoRestore);

                defaultLayout = LayoutData.AutoRestore;
            }
            else
            {
                layouts.Add(string.Empty);
                // auto restore is in the layouts collection already
                layouts.AddRange(SuperPuTTY.Layouts.Select(layout => layout.Name));

                defaultLayout = SuperPuTTY.Settings.DefaultLayoutName;
            }
            comboBoxLayouts.DataSource = layouts;
            comboBoxLayouts.SelectedItem = defaultLayout;
            OrigDefaultLayoutName = defaultLayout;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            BeginInvoke(new MethodInvoker(delegate { textBoxPuttyLocation.Focus(); }));
        }
       
        private void buttonOk_Click(object sender, EventArgs e)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrEmpty(textBoxFilezillaLocation.Text) || File.Exists(textBoxFilezillaLocation.Text))
            {
                SuperPuTTY.Settings.FileZillaExe = textBoxFilezillaLocation.Text;
            }

            if (string.IsNullOrEmpty(textBoxWinSCPLocation.Text) || File.Exists(textBoxWinSCPLocation.Text))
            {
                SuperPuTTY.Settings.WinSCPExe = textBoxWinSCPLocation.Text;
            }

            if (string.IsNullOrEmpty(textBoxPscpLocation.Text) || File.Exists(textBoxPscpLocation.Text))
            {
                SuperPuTTY.Settings.PscpExe = textBoxPscpLocation.Text;
            }

            if (string.IsNullOrEmpty(textBoxVNCLocation.Text) || File.Exists(textBoxVNCLocation.Text))
            {
                SuperPuTTY.Settings.VNCExe = textBoxVNCLocation.Text;
            }

            string settingsDir = textBoxSettingsFolder.Text;
            if (string.IsNullOrEmpty(settingsDir) || !Directory.Exists(settingsDir))
            {
                errors.Add("Settings Folder must be set to valid directory");
            }
            else
            {
                SuperPuTTY.Settings.SettingsFolder = settingsDir;
            }

            if (comboBoxLayouts.SelectedValue != null)
            {
                SuperPuTTY.Settings.DefaultLayoutName = (string) comboBoxLayouts.SelectedValue;
            }

            if (!string.IsNullOrEmpty(textBoxPuttyLocation.Text) && File.Exists(textBoxPuttyLocation.Text))
            {
                SuperPuTTY.Settings.PuttyExe = textBoxPuttyLocation.Text;
            }
            else
            {
                errors.Insert(0, "PuTTY is required to properly use this application.");
            }

            string mintty = textBoxMinttyLocation.Text;
            if (!string.IsNullOrEmpty(mintty) && File.Exists(mintty))
            {
                SuperPuTTY.Settings.MinttyExe = mintty;
            }

            if (errors.Count == 0)
            {
                SuperPuTTY.Settings.SingleInstanceMode = checkSingleInstanceMode.Checked;
                SuperPuTTY.Settings.RestrictContentToDocumentTabs = checkConstrainPuttyDocking.Checked;
                SuperPuTTY.Settings.MultipleTabCloseConfirmation= checkConfirmTabClose.Checked;
                SuperPuTTY.Settings.RestoreWindowLocation = checkRestoreWindow.Checked;
                SuperPuTTY.Settings.ExitConfirmation = checkExitConfirmation.Checked;
                SuperPuTTY.Settings.ExpandSessionsTreeOnStartup = checkExpandTree.Checked;
                SuperPuTTY.Settings.EnableControlTabSwitching = checkEnableControlTabSwitching.Checked;
                SuperPuTTY.Settings.EnableKeyboadShortcuts = checkEnableKeyboardShortcuts.Checked;
                SuperPuTTY.Settings.MinimizeToTray = checkMinimizeToTray.Checked;
                SuperPuTTY.Settings.TabTextBehavior = (string) comboBoxTabText.SelectedItem;
                SuperPuTTY.Settings.TabSwitcher = comboBoxTabSwitching.SelectedItem.GetType().FullName;
                SuperPuTTY.Settings.SessionsTreeShowLines = checkSessionsTreeShowLines.Checked;
                SuperPuTTY.Settings.SessionsTreeFont = btnFont.Font;
                SuperPuTTY.Settings.WindowActivator = (string) comboBoxActivatorType.SelectedItem;
                SuperPuTTY.Settings.Opacity = (double) numericUpDownOpacity.Value / 100.0;
                SuperPuTTY.Settings.SessionsSearchMode = (string) comboSearchMode.SelectedItem;
                SuperPuTTY.Settings.QuickSelectorCaseSensitiveSearch = checkQuickSelectorCaseSensitiveSearch.Checked;
                SuperPuTTY.Settings.ShowDocumentIcons = checkShowDocumentIcons.Checked;
                SuperPuTTY.Settings.DockingRestrictFloatingWindows = checkRestrictFloatingWindows.Checked;
                SuperPuTTY.Settings.SessionsShowSearch = checkSessionsShowSearch.Checked;
                SuperPuTTY.Settings.PuttyPanelShowNewSessionMenu = checkPuttyEnableNewSessionMenu.Checked;
                SuperPuTTY.Settings.AutoUpdateCheck = checkBoxCheckForUpdates.Checked;
                SuperPuTTY.Settings.PscpHomePrefix = textBoxHomeDirPrefix.Text;
                SuperPuTTY.Settings.PscpRootHomePrefix = textBoxRootDirPrefix.Text;
                SuperPuTTY.Settings.SessiontreeShowFoldersFirst = checkSessionTreeFoldersFirst.Checked;
                SuperPuTTY.Settings.PersistCommandBarHistory = checkBoxPersistTsHistory.Checked;
                SuperPuTTY.Settings.SaveCommandHistoryDays = (int)numericUpDown1.Value;
                SuperPuTTY.Settings.AllowPlainTextPuttyPasswordArg = checkBoxAllowPuttyPWArg.Checked;
                SuperPuTTY.Settings.PuttyDefaultParameters = textBoxPuttyDefaultParameters.Text;

                // save shortcuts
                KeyboardShortcut[] shortcuts = new KeyboardShortcut[Shortcuts.Count];
                Shortcuts.CopyTo(shortcuts, 0);
                SuperPuTTY.Settings.UpdateFromShortcuts(shortcuts);

                SuperPuTTY.Settings.Save();

                // @TODO - move this to a better place...maybe event handler after opening
                if (OrigSettingsFolder != SuperPuTTY.Settings.SettingsFolder)
                {
                    SuperPuTTY.LoadLayouts();
                    SuperPuTTY.LoadSessions();
                }
                else if (OrigDefaultLayoutName != SuperPuTTY.Settings.DefaultLayoutName)
                {
                    SuperPuTTY.LoadLayouts();
                }

                DialogResult = DialogResult.OK;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (string s in errors)
                {
                    sb.Append(s).AppendLine().AppendLine();
                }
                if (MessageBox.Show(sb.ToString(), LocalizedText.dlgFindPutty_buttonOk_Click_MessageBox_Errors, MessageBoxButtons.RetryCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                {
                    DialogResult = DialogResult.Cancel;
                }
            }
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = @"PuTTY|putty.exe|KiTTY|kitty*.exe";
            openFileDialog1.FileName = "putty.exe";
            if (File.Exists(textBoxPuttyLocation.Text))
            {
                openFileDialog1.FileName = Path.GetFileName(textBoxPuttyLocation.Text);
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(textBoxPuttyLocation.Text);
                openFileDialog1.FilterIndex = openFileDialog1.FileName != null && openFileDialog1.FileName.ToLower().StartsWith("putty") ? 1 : 2;
            }
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(openFileDialog1.FileName))
                    textBoxPuttyLocation.Text = openFileDialog1.FileName;
            }            
        }

        private void buttonBrowsePscp_Click(object sender, EventArgs e)
        {
            DialogBrowseExe("PScp|pscp.exe", "pscp.exe", textBoxPscpLocation);
        }

        private void btnBrowseMintty_Click(object sender, EventArgs e)
        {
            DialogBrowseExe("MinTTY|mintty.exe", "mintty.exe", textBoxMinttyLocation);
        }

        private void buttonBrowseFilezilla_Click(object sender, EventArgs e)
        {
            DialogBrowseExe("filezilla|filezilla.exe", "filezilla.exe", textBoxFilezillaLocation);
        }

        private void buttonBrowseWinSCP_Click(object sender, EventArgs e)
        {
            DialogBrowseExe("WinSCP|WinSCP.exe", "WinSCP.exe", textBoxWinSCPLocation);
        }

        private void btnBrowseVNC_Click(object sender, EventArgs e)
        {
            DialogBrowseExe("tvnviewer|tvnviewer.exe", "tvnviewer.exe", textBoxVNCLocation);
        }

        private void DialogBrowseExe(string filter, string filename, TextBox textbox)
        {
            openFileDialog1.Filter = filter;
            openFileDialog1.FileName = filename;

            if (File.Exists(textbox.Text))
            {
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(textbox.Text);
            }
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(openFileDialog1.FileName))
                    textbox.Text = openFileDialog1.FileName;
            }

        }

        //Search automaticaly the path of FileZilla when doubleClick when it is empty
        private void textBoxFilezillaLocation_DoubleClick(object sender, EventArgs e)
        {
            textBoxFilezillaLocation.Text = getPathExe(@"\FileZilla FTP Client\filezilla.exe", SuperPuTTY.Settings.FileZillaExe, true);
        }

        //Search automaticaly the path of WinSCP when doubleClick when it is empty
        private void textBoxWinSCPLocation_DoubleClick(object sender, EventArgs e)
        {
            textBoxWinSCPLocation.Text = getPathExe(@"\WinSCP\WinSCP.exe", SuperPuTTY.Settings.WinSCPExe, true);
        }

        //Search automaticaly the path of WinSCP when doubleClick when it is empty
        private void textBoxVNCLocation_DoubleClick(object sender, EventArgs e)
        {
            textBoxVNCLocation.Text = getPathExe(@"\TightVNC\tvnviewer.exe", SuperPuTTY.Settings.VNCExe, true);
        }


        /// <summary>
        /// Check that putty can be found.  If not, prompt the user
        /// </summary>
        public static void PuttyCheck()
        {
            if (string.IsNullOrEmpty(SuperPuTTY.Settings.PuttyExe) || SuperPuTTY.IsFirstRun || !File.Exists(SuperPuTTY.Settings.PuttyExe))
            {
                // first time, try to import old putty settings from registry
                SuperPuTTY.Settings.ImportFromRegistry();
                dlgFindPutty dialog = new dlgFindPutty();
                if (dialog.ShowDialog(SuperPuTTY.MainForm) == DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }
            }

            if (string.IsNullOrEmpty(SuperPuTTY.Settings.PuttyExe))
            {
                MessageBox.Show(LocalizedText.dlgFindPutty_PuttyCheck_Cannot_find_PuTTY_installation,
                    LocalizedText.dlgFindPutty_PuttyCheck_PuTTY_Not_Found, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                Environment.Exit(1);
            }

            if (SuperPuTTY.IsFirstRun && SuperPuTTY.Sessions.Count == 0)
            {
                // first run, got nothing...try to import from registry
                SuperPuTTY.ImportSessionsFromSuperPutty1030();
            }
        }

        private void buttonBrowseLayoutsFolder_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK) 
            {
                textBoxSettingsFolder.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnFont_Click(object sender, EventArgs e)
        {
            fontDialog.Font = btnFont.Font;
            if (fontDialog.ShowDialog(this) == DialogResult.OK)
            {
                btnFont.Font = fontDialog.Font;
                btnFont.Text = ToShortString(fontDialog.Font);
            }
        }

        private void dataGridViewShortcuts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1) { return; }

            Log.InfoFormat("Shortcuts grid click: row={0}, col={1}", e.RowIndex, e.ColumnIndex);
            DataGridViewColumn col = dataGridViewShortcuts.Columns[e.ColumnIndex];
            DataGridViewRow row = dataGridViewShortcuts.Rows[e.RowIndex];
            KeyboardShortcut ks = (KeyboardShortcut) row.DataBoundItem;

            if (col == colEdit)
            {
                KeyboardShortcutEditor editor = new KeyboardShortcutEditor
                {
                    StartPosition = FormStartPosition.CenterParent
                };
                if (DialogResult.OK == editor.ShowDialog(this, ks))
                {
                    Shortcuts.ResetItem(Shortcuts.IndexOf(ks));
                    Log.InfoFormat("Edited shortcut: {0}", ks);
                }
            }
            else if (col == colClear)
            {
                ks.Clear();
                Shortcuts.ResetItem(Shortcuts.IndexOf(ks));
                Log.InfoFormat("Cleared shortcut: {0}", ks);
            }
        }

        static string ToShortString(Font font)
        {
            return string.Format("{0}, {1} pt, {2}", font.FontFamily.Name, font.Size, font.Style);
        }       
    }

}
