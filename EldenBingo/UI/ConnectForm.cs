namespace EldenBingo.UI
{
    public partial class ConnectForm : Form
    {
        public ConnectForm()
        {
            InitializeComponent();
        }

        public string Address
        {
            get { return _addressTextBox.Text; }
            set
            {
                _addressTextBox.Text = value;
            }
        }

        public bool AutoConnect
        {
            get { return _autoConnectCheckBox.Checked; }
            set { _autoConnectCheckBox.Checked = value; }
        }

        private void _cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void _connectButton_Click(object sender, EventArgs e)
        {
            if (validate())
            {
                DialogResult = DialogResult.OK;
                Properties.Settings.Default.ServerAddress = Address;
                Properties.Settings.Default.AutoConnect = AutoConnect;
                Properties.Settings.Default.Save();
                Close();
            }
        }

        private void ConnectForm_Load(object sender, EventArgs e)
        {
            Address = Properties.Settings.Default.ServerAddress;
            AutoConnect = Properties.Settings.Default.AutoConnect;
        }

        private bool validate()
        {
            if (string.IsNullOrWhiteSpace(_addressTextBox.Text))
            {
                errorProvider1.SetError(_addressTextBox, "Invalid address");
                return false;
            }
            else
            {
                errorProvider1.SetError(_addressTextBox, null);
            }
            return true;
        }
    }
}