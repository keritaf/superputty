using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace SuperPutty.Gui
{
    public partial class dlgRenameItem : Form
    {
        public delegate bool ItemNameValidationHandler(string name, out string error);

        public dlgRenameItem()
        {
            InitializeComponent();
        }

        public string ItemName {
            get => txtItemName.Text;
            set => txtItemName.Text = value;
        }

        public string DetailName
        {
            get => labelDetailName.Text;
            set => labelDetailName.Text = value;
        }

        public ItemNameValidationHandler ItemNameValidator { get; set; }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtItemName_Validating(object sender, CancelEventArgs e)
        {
            if (ItemNameValidator != null)
            {
                if (!ItemNameValidator(txtItemName.Text, out var error))
                {
                    errorProvider.SetError(txtItemName, error ?? "Invalid Name");
                    btnOK.Enabled = false;
                }
                else
                {
                    errorProvider.SetError(txtItemName, String.Empty);
                    btnOK.Enabled = true;
                }
            }

        }

        private void txtItemName_Validated(object sender, EventArgs e)
        {
            //this.errorProvider.SetError(this.txtItemName, String.Empty);
        }

        /// <summary>
        /// Allow them to close the form if validation is not passed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dlgRenameItem_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = false;
        }

        private void folderForm_TextChanged(object sender, EventArgs e)
        {
            btnOK.Enabled = txtItemName.Text.Length > 0;
        }

    }
}
