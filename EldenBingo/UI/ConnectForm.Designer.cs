namespace EldenBingo.UI
{
    partial class ConnectForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this._cancelButton = new System.Windows.Forms.Button();
            this._connectButton = new System.Windows.Forms.Button();
            this._addressTextBox = new System.Windows.Forms.TextBox();
            this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
            this._autoConnectCheckBox = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Address:";
            // 
            // _cancelButton
            // 
            this._cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._cancelButton.Location = new System.Drawing.Point(177, 75);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Size = new System.Drawing.Size(75, 23);
            this._cancelButton.TabIndex = 4;
            this._cancelButton.Text = "Cancel";
            this._cancelButton.UseVisualStyleBackColor = true;
            this._cancelButton.Click += new System.EventHandler(this._cancelButton_Click);
            // 
            // _connectButton
            // 
            this._connectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._connectButton.Location = new System.Drawing.Point(96, 75);
            this._connectButton.Name = "_connectButton";
            this._connectButton.Size = new System.Drawing.Size(75, 23);
            this._connectButton.TabIndex = 3;
            this._connectButton.Text = "Connect";
            this._connectButton.UseVisualStyleBackColor = true;
            this._connectButton.Click += new System.EventHandler(this._connectButton_Click);
            // 
            // _addressTextBox
            // 
            this._addressTextBox.Location = new System.Drawing.Point(77, 9);
            this._addressTextBox.Name = "_addressTextBox";
            this._addressTextBox.Size = new System.Drawing.Size(175, 23);
            this._addressTextBox.TabIndex = 1;
            // 
            // errorProvider1
            // 
            this.errorProvider1.ContainerControl = this;
            // 
            // _autoConnectCheckBox
            // 
            this._autoConnectCheckBox.AutoSize = true;
            this._autoConnectCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._autoConnectCheckBox.Checked = true;
            this._autoConnectCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this._autoConnectCheckBox.Location = new System.Drawing.Point(12, 40);
            this._autoConnectCheckBox.Name = "_autoConnectCheckBox";
            this._autoConnectCheckBox.Size = new System.Drawing.Size(103, 19);
            this._autoConnectCheckBox.TabIndex = 2;
            this._autoConnectCheckBox.Text = "Auto-connect:";
            this._autoConnectCheckBox.UseVisualStyleBackColor = true;
            // 
            // ConnectForm
            // 
            this.AcceptButton = this._connectButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this._cancelButton;
            this.ClientSize = new System.Drawing.Size(264, 109);
            this.Controls.Add(this._autoConnectCheckBox);
            this.Controls.Add(this._addressTextBox);
            this.Controls.Add(this._connectButton);
            this.Controls.Add(this._cancelButton);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "ConnectForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Connect To Bingo Server";
            this.Load += new System.EventHandler(this.ConnectForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Label label1;
        private Button _cancelButton;
        private Button _connectButton;
        private TextBox _addressTextBox;
        private ErrorProvider errorProvider1;
        private CheckBox _autoConnectCheckBox;
    }
}