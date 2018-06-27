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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using log4net;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPutty.Gui
{
    public sealed partial class EditSessionDialog : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(EditSessionDialog));

        public delegate bool SessionNameValidationHandler(string name, out string error);

        private readonly SessionData Session;
        private String OldHostname;
        private readonly bool isInitialized;
        private ImageListPopup imgPopup;

        public EditSessionDialog(SessionData session, ImageList iconList)
        {
            Session = session;
            InitializeComponent();

            // get putty saved settings from the registry to populate
            // the dropdown
            PopulatePuttySettings();

            if (!String.IsNullOrEmpty(Session.SessionName))
            {
                Text = string.Format("Edit session: {0}", session.SessionName);
                textBoxSessionName.Text = Session.SessionName;
                textBoxHostname.Text = Session.Host;
                textBoxPort.Text = Session.Port.ToString();
                textBoxExtraArgs.Text = Session.ExtraArgs;
                textBoxUsername.Text = Session.Username;
                textBoxSPSLScriptFile.Text = Session.SPSLFileName;
                textBoxRemotePathSesion.Text = Session.RemotePath;
                textBoxLocalPathSesion.Text = Session.LocalPath;

                switch (Session.Proto)
                {
                    case ConnectionProtocol.Raw:
                        radioButtonRaw.Checked = true;
                        break;
                    case ConnectionProtocol.Rlogin:
                        radioButtonRlogin.Checked = true;
                        break;
                    case ConnectionProtocol.Serial:
                        radioButtonSerial.Checked = true;
                        break;
                    case ConnectionProtocol.SSH:
                        radioButtonSSH.Checked = true;
                        break;
                    case ConnectionProtocol.Telnet:
                        radioButtonTelnet.Checked = true;
                        break;
                    case ConnectionProtocol.Cygterm:
                        radioButtonCygterm.Checked = true;
                        break;
                    case ConnectionProtocol.Mintty:
                        radioButtonMintty.Checked = true;
                        break;
                    case ConnectionProtocol.VNC:
                        radioButtonVNC.Checked = true;
                        if (Session.Port == 0)
                            textBoxPort.Text = "";
                        break;
                    default:
                        radioButtonSSH.Checked = true;
                        break;
                }

                comboBoxPuttyProfile.DropDownStyle = ComboBoxStyle.DropDownList;
                foreach(String settings in comboBoxPuttyProfile.Items){
                    if (settings == session.PuttySession)
                    {
                        comboBoxPuttyProfile.SelectedItem = settings;
                        break;
                    }
                }

                buttonSave.Enabled = true;
            }
            else
            {
                Text = "Create new session";
                radioButtonSSH.Checked = true;
                buttonSave.Enabled = false;
            }


            // Setup icon chooser
            buttonImageSelect.ImageList = iconList;
            buttonImageSelect.ImageKey = string.IsNullOrEmpty(Session.ImageKey)
                ? SessionTreeview.ImageKeySession
                : Session.ImageKey;
            toolTip.SetToolTip(buttonImageSelect, buttonImageSelect.ImageKey);

            isInitialized = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            BeginInvoke(new MethodInvoker(delegate { textBoxSessionName.Focus(); }));
        }

        private void PopulatePuttySettings()
        {
            foreach (String sessionName in PuttyDataHelper.GetSessionNames())
            {
                comboBoxPuttyProfile.Items.Add(sessionName);
            }
            comboBoxPuttyProfile.SelectedItem = PuttyDataHelper.SessionDefaultSettings;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(CommandLineOptions.getcommand(textBoxExtraArgs.Text, "-pw")))
            {
                if (MessageBox.Show("SuperPutty save the password in Sessions.xml file in plain text.\nUse a password in 'Extra PuTTY Arguments' is very insecure.\nFor a secure connection use SSH authentication with Pageant. \nSelect yes, if you want save the password", "Are you sure that you want to save the password?",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1)==DialogResult.Cancel){
                            return;                
                }
            }
            Session.SessionName  = textBoxSessionName.Text.Trim();
            Session.PuttySession = comboBoxPuttyProfile.Text.Trim();
            Session.Host         = textBoxHostname.Text.Trim();
            Session.ExtraArgs    = textBoxExtraArgs.Text.Trim();
            Session.Port = !Int32.TryParse(textBoxPort.Text, out _) ? 0 : int.Parse(textBoxPort.Text.Trim());
            Session.Username     = textBoxUsername.Text.Trim();
            Session.SessionId    = SessionData.CombineSessionIds(SessionData.GetSessionParentId(Session.SessionId), Session.SessionName);
            Session.ImageKey     = buttonImageSelect.ImageKey;
            Session.SPSLFileName = textBoxSPSLScriptFile.Text.Trim();
            Session.RemotePath = textBoxRemotePathSesion.Text.Trim();
            Session.LocalPath = textBoxLocalPathSesion.Text.Trim();

            for (int i = 0; i < groupBox1.Controls.Count; i++)
            {
                RadioButton rb = (RadioButton)groupBox1.Controls[i];
                if (rb.Checked)
                {
                    Session.Proto = (ConnectionProtocol)rb.Tag;
                }
            }
            
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Special UI handling for cygterm or mintty sessions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButtonCygterm_CheckedChanged(object sender, EventArgs e)
        {
            string host = textBoxHostname.Text;
            bool isLocalShell = radioButtonCygterm.Checked || radioButtonMintty.Checked;
            textBoxPort.Enabled = !isLocalShell;
            textBoxExtraArgs.Enabled = !isLocalShell;
            textBoxUsername.Enabled = !isLocalShell;

            if (isLocalShell)
            {
                if (String.IsNullOrEmpty(host) || !host.StartsWith(CygtermStartInfo.LocalHost))
                {
                    OldHostname = textBoxHostname.Text;
                    textBoxHostname.Text = CygtermStartInfo.LocalHost;
                }
            }

        }

        private void radioButtonRaw_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonRaw.Checked && isInitialized)
            {
                if (!string.IsNullOrEmpty(OldHostname))
                {
                    textBoxHostname.Text = OldHostname;
                    OldHostname = null;
                }
            }
        }

        private void radioButtonTelnet_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonTelnet.Checked && isInitialized)
            {
                if (!string.IsNullOrEmpty(OldHostname))
                {
                    textBoxHostname.Text = OldHostname;
                    OldHostname = null;
                }
                textBoxPort.Text = "23";
            }
        }

        private void radioButtonRlogin_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonRlogin.Checked && isInitialized)
            {
                if (!string.IsNullOrEmpty(OldHostname))
                {
                    textBoxHostname.Text = OldHostname;
                    OldHostname = null;
                }
                textBoxPort.Text = "513";
            }
        }

        private void radioButtonSSH_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonSSH.Checked && isInitialized)
            {
                if (!string.IsNullOrEmpty(OldHostname))
                {
                    textBoxHostname.Text = OldHostname;
                    OldHostname = null;
                }
                textBoxPort.Text = "22";
            }
        }

        private void radioButtonVNC_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonVNC.Checked && isInitialized)
            {
                if (!string.IsNullOrEmpty(OldHostname))
                {
                    textBoxHostname.Text = OldHostname;
                    OldHostname = null;
                }
                textBoxPort.Text = "";
            }
            comboBoxPuttyProfile.Enabled = !radioButtonVNC.Checked;
        }

        public static int GetDefaultPort(ConnectionProtocol protocol)
        {
            int port = 22;
            switch (protocol)
            {
                case ConnectionProtocol.Raw:
                    break;
                case ConnectionProtocol.Rlogin:
                    port = 513;
                    break;
                case ConnectionProtocol.Serial:
                    break;
                case ConnectionProtocol.Telnet:
                    port = 23;
                    break;
                case ConnectionProtocol.VNC:
                    port = 0;
                    break;
            }
            return port;
        }

        #region Icon
        private void buttonImageSelect_Click(object sender, EventArgs e)
        {
            if (imgPopup == null)
            {
                // TODO: ImageList is null on initial installation and will throw a nullreference exception when creating a new session and trying to select an image.

                int n = buttonImageSelect.ImageList.Images.Count;
                int x = (int) Math.Floor(Math.Sqrt(n)) + 1;
                int cols = x;
                int rows = x;

                imgPopup = new ImageListPopup
                {
                    BackgroundColor = Color.FromArgb(241, 241, 241),
                    BackgroundOverColor = Color.FromArgb(102, 154, 204)
                };
                imgPopup.Init(buttonImageSelect.ImageList, 8, 8, cols, rows);
                imgPopup.ItemClick += OnItemClicked;
            }

            Point pt = PointToScreen(new Point(buttonImageSelect.Left, buttonImageSelect.Bottom));
            imgPopup.Show(pt.X + 2, pt.Y);
        }


        private void OnItemClicked(object sender, ImageListPopupEventArgs e)
        {
            if (imgPopup == sender)
            {
                buttonImageSelect.ImageKey = e.SelectedItem;
                toolTip.SetToolTip(buttonImageSelect, buttonImageSelect.ImageKey);
            }
        } 
        #endregion

        #region Validation Logic

        public SessionNameValidationHandler SessionNameValidator { get; set; }

        private void textBoxSessionName_Validating(object sender, CancelEventArgs e)
        {
            if (SessionNameValidator != null)
            {
                if (!SessionNameValidator(textBoxSessionName.Text, out var error))
                {
                    e.Cancel = true;
                    SetError(textBoxSessionName, error ?? "Invalid Session Name");
                }
            }
        }

        private void textBoxSessionName_Validated(object sender, EventArgs e)
        {
            SetError(textBoxSessionName, String.Empty);
        }

        private void textBoxPort_Validating(object sender, CancelEventArgs e)
        {
            if (!Int32.TryParse(textBoxPort.Text, out _))
            {
                if (textBoxPort.Text == "")
                    if (radioButtonVNC.Checked || radioButtonMintty.Checked || radioButtonCygterm.Checked)
                        return;

                e.Cancel = true;
                SetError(textBoxPort, "Invalid Port");
            }
        }

        private void textBoxPort_Validated(object sender, EventArgs e)
        {
            SetError(textBoxPort, String.Empty);
        }

        private void textBoxHostname_Validating(object sender, CancelEventArgs e)
        {
            if (string.IsNullOrEmpty((string)comboBoxPuttyProfile.SelectedItem) &&
                string.IsNullOrEmpty(textBoxHostname.Text.Trim()))
            {
                if (sender == textBoxHostname)
                {
                    SetError(textBoxHostname, "A host name must be specified if a Putty Session Profile is not selected");
                }
                else if (sender == comboBoxPuttyProfile)
                {
                    SetError(comboBoxPuttyProfile, "A Putty Session Profile must be selected if a Host Name is not provided");
                }
            }
            else
            {
                SetError(textBoxHostname, String.Empty);
                SetError(comboBoxPuttyProfile, String.Empty);
            }
        }

        private void comboBoxPuttyProfile_Validating(object sender, CancelEventArgs e)
        {
            textBoxHostname_Validating(sender, e);
        }

        private void comboBoxPuttyProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            ValidateChildren(ValidationConstraints.ImmediateChildren);    
        }

        void SetError(Control control, string error)
        {
            errorProvider.SetError(control, error);
            EnableDisableSaveButton();
        }

        void EnableDisableSaveButton()
        {
            buttonSave.Enabled = errorProvider.GetError(textBoxSessionName) == String.Empty &&
                                      errorProvider.GetError(textBoxHostname) == String.Empty &&
                                      errorProvider.GetError(textBoxPort) == String.Empty &&
                                      errorProvider.GetError(comboBoxPuttyProfile) == String.Empty;
        }

        #endregion

        private void buttonBrowse_Click(object sender, EventArgs e)
        {

            DialogResult dlgResult = openFileDialog1.ShowDialog();
            if (dlgResult == DialogResult.OK)
            {
                textBoxSPSLScriptFile.Text = openFileDialog1.FileName;
            }
        }

        private void buttonClearSPSLFile_Click(object sender, EventArgs e)
        {
            Session.SPSLFileName = textBoxSPSLScriptFile.Text = String.Empty;
            
        }

        private void buttonBrowseLocalPath_Click(object sender, EventArgs e)
        {            
            if (Directory.Exists(textBoxLocalPathSesion.Text))
            {
                folderBrowserDialog1.SelectedPath = textBoxLocalPathSesion.Text;
            }
            if (folderBrowserDialog1.ShowDialog(this) == DialogResult.OK)
            {
                if (!String.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
                    textBoxLocalPathSesion.Text = folderBrowserDialog1.SelectedPath;
            }


        }


       private void textBoxExtraArgs_TextChanged(object sender, EventArgs e)
       {
           //if extra Args contains a password, change the backgroudn
           textBoxExtraArgs.BackColor = String.IsNullOrEmpty(CommandLineOptions.getcommand(textBoxExtraArgs.Text, "-pw")) ? Color.White : Color.LightCoral;
       }
    }
}
