using SuperPutty.Data;
using SuperPutty.Utils;
using System;
using System.Windows.Forms;
using SuperPutty.Gui;

namespace SuperPutty
{
    public partial class SessionDetail : ToolWindow
    {
        private SessionTreeview TreeViewInstance;
        readonly SingletonToolWindowHelper<SessionTreeview> SessionsToolWindowHelper;
        private SessionDetail()
        {
            InitializeComponent();
            TreeViewInstance = null;
            sessionDetailPropertyGrid.PropertySort = PropertySort.NoSort;
        }

        public SessionDetail(SingletonToolWindowHelper<SessionTreeview> Sessions) : this()
        {
            SessionsToolWindowHelper = Sessions;
            if (SessionsToolWindowHelper != null)
            {
                // We need to know when an instance of the SessionTreeView is created
                // so that we can register for the SelectionChanged event.
                SessionsToolWindowHelper.InstanceChanged += SessionTreeviewInstanceChanged;
                SessionTreeviewInstanceChanged(SessionsToolWindowHelper.Instance);
            }

            FormClosed += SessionDetail_FormClosed;
        }

        private void SelectedSessionChanged(SessionData session)
        {
            if (sessionDetailPropertyGrid.SelectedObject is SessionData oldSession)
            {
                oldSession.OnPropertyChanged -= OnPropertyChanged;
            }
            sessionDetailPropertyGrid.SelectedObject = session;
            if (session != null)
            {
                session.OnPropertyChanged += OnPropertyChanged;
            }
        }

        private void SessionTreeviewInstanceChanged(SessionTreeview treeViewInstance)
        {
            if (TreeViewInstance == treeViewInstance)
                return;

            Attach(treeViewInstance);
        }

        private void OnPropertyChanged(SessionData session, String attributeName)
        {
            if (session == null)
                return;

            sessionDetailPropertyGrid.Refresh();
        }

        private void Attach(SessionTreeview sessionTreeView)
        {
            Detach();
            TreeViewInstance = sessionTreeView;
            if (sessionTreeView != null)
            {
                TreeViewInstance.FormClosed += SessionTreeView_FormClosed;
                sessionTreeView.SelectionChanged += SelectedSessionChanged;
                SelectedSessionChanged(sessionTreeView.SelectedSession);
            }
        }

        private void Detach()
        {
            if (TreeViewInstance != null)
            {
                TreeViewInstance.FormClosed -= SessionTreeView_FormClosed;
                TreeViewInstance.SelectionChanged -= SelectedSessionChanged;
            }
            TreeViewInstance = null;
            SelectedSessionChanged(null);
        }

        private void SessionTreeView_FormClosed(object sender, FormClosedEventArgs e)
        {
            Detach();
        }

        private void SessionDetail_FormClosed(object sender, FormClosedEventArgs e)
        {
            Detach();
            if (SessionsToolWindowHelper != null)
            {
                SessionsToolWindowHelper.InstanceChanged -= SessionTreeviewInstanceChanged;
            }
        }

        private void sessionDetailPropertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (sessionDetailPropertyGrid.SelectedObject is SessionData Session)
            {
                String HostPropertyName = "Host";
                String PuttySessionPropertyName = "PuttySession";
                if (e.ChangedItem.PropertyDescriptor?.Name == HostPropertyName || e.ChangedItem.PropertyDescriptor?.Name == PuttySessionPropertyName)
                {
                    if (String.IsNullOrEmpty(Session.PuttySession) && String.IsNullOrEmpty(Session.Host))
                    {
                        if (e.ChangedItem.PropertyDescriptor.Name == HostPropertyName)
                        {
                            MessageBox.Show(LocalizedText.SessionDetail_sessionDetailPropertyGrid_PropertyValueChanged_A_host_name_must_be_specified_if_a_Putty_Session_Profile_is_not_selected);
                            Session.Host = (String)e.OldValue;
                        }
                        else
                        {
                            MessageBox.Show(LocalizedText.SessionDetail_sessionDetailPropertyGrid_PropertyValueChanged_A_Putty_Session_Profile_must_be_selected_if_a_Host_Name_is_not_provided);
                            Session.PuttySession = (String)e.OldValue;
                        }
                        sessionDetailPropertyGrid.Refresh();
                    }
                }


                String ExtraArgsPropertyName = "ExtraArgs";
                if (e.ChangedItem.PropertyDescriptor?.Name == ExtraArgsPropertyName)
                {
                    
                    if (!String.IsNullOrEmpty(CommandLineOptions.getcommand(Session.ExtraArgs, "-pw")))
                    {
                        if (MessageBox.Show(LocalizedText.SessionDetail_sessionDetailPropertyGrid_PropertyValueChanged_, LocalizedText.SessionDetail_sessionDetailPropertyGrid_PropertyValueChanged_Are_you_sure_that_you_want_to_save_the_password_,
                            MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button1) == DialogResult.Cancel)
                        {
                            Session.ExtraArgs = (String)e.OldValue;
                            return;
                        }
                    }
                    sessionDetailPropertyGrid.Refresh();
                    
                }


                Session.SessionId = SessionData.CombineSessionIds(SessionData.GetSessionParentId(Session.SessionId), Session.SessionName);
            }
            SuperPuTTY.SaveSessions();
        }
    }
}
