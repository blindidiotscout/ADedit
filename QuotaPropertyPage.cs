using System;
using System.Drawing;
using System.Windows.Forms;

namespace HieQuotaMMC
{
    public class QuotaControl : UserControl
    {
        private Label lblSendQuota;
        private TextBox txtSendQuota;
        private Label lblReceiveQuota;
        private TextBox txtReceiveQuota;
        private Label lblStorageQuota;
        private TextBox txtStorageQuota;

        private GroupBox grpExchange;
        private Label lblExSendQuota;
        private TextBox txtExSendQuota;
        private Label lblExReceiveQuota;
        private TextBox txtExReceiveQuota;
        private Label lblExStorageQuota;
        private TextBox txtExStorageQuota;

        public string SendQuotaText { get => txtSendQuota.Text; set => txtSendQuota.Text = value; }
        public string ReceiveQuotaText { get => txtReceiveQuota.Text; set => txtReceiveQuota.Text = value; }
        public string StorageQuotaText { get => txtStorageQuota.Text; set => txtStorageQuota.Text = value; }

        public string ExSendQuotaText { get => txtExSendQuota.Text; set => txtExSendQuota.Text = value; }
        public string ExReceiveQuotaText { get => txtExReceiveQuota.Text; set => txtExReceiveQuota.Text = value; }
        public string ExStorageQuotaText { get => txtExStorageQuota.Text; set => txtExStorageQuota.Text = value; }

        public QuotaControl()
        {
            InitializeControls();
        }

        private void InitializeControls()
        {
            this.Size = new Size(350, 300);

            // HIE Attribute (Editierbar)
            lblSendQuota = new Label { Text = "HIE Sende-Limit (KB):", Location = new Point(10, 15), AutoSize = true };
            txtSendQuota = new TextBox { Location = new Point(180, 12), Size = new Size(150, 20) };
            txtSendQuota.TextChanged += (s, e) => MarkDirty();

            lblReceiveQuota = new Label { Text = "HIE Empfangs-Limit (KB):", Location = new Point(10, 45), AutoSize = true };
            txtReceiveQuota = new TextBox { Location = new Point(180, 42), Size = new Size(150, 20) };
            txtReceiveQuota.TextChanged += (s, e) => MarkDirty();

            lblStorageQuota = new Label { Text = "HIE Max Mailbox (KB):", Location = new Point(10, 75), AutoSize = true };
            txtStorageQuota = new TextBox { Location = new Point(180, 72), Size = new Size(150, 20) };
            txtStorageQuota.TextChanged += (s, e) => MarkDirty();

            // Exchange Aliase (Schreibgeschützt)
            grpExchange = new GroupBox { Text = "Exchange Aliase (Read-Only)", Location = new Point(10, 110), Size = new Size(320, 130) };

            lblExSendQuota = new Label { Text = "mDBOverQuotaLimit:", Location = new Point(10, 25), AutoSize = true };
            txtExSendQuota = new TextBox { Location = new Point(180, 22), Size = new Size(120, 20), ReadOnly = true, BackColor = SystemColors.Control };

            lblExReceiveQuota = new Label { Text = "mDBOverHardQuotaLimit:", Location = new Point(10, 55), AutoSize = true };
            txtExReceiveQuota = new TextBox { Location = new Point(180, 52), Size = new Size(120, 20), ReadOnly = true, BackColor = SystemColors.Control };

            lblExStorageQuota = new Label { Text = "mDBStorageQuota:", Location = new Point(10, 85), AutoSize = true };
            txtExStorageQuota = new TextBox { Location = new Point(180, 82), Size = new Size(120, 20), ReadOnly = true, BackColor = SystemColors.Control };

            grpExchange.Controls.AddRange(new Control[] {
                lblExSendQuota, txtExSendQuota,
                lblExReceiveQuota, txtExReceiveQuota,
                lblExStorageQuota, txtExStorageQuota
            });

            this.Controls.AddRange(new Control[] {
                lblSendQuota, txtSendQuota,
                lblReceiveQuota, txtReceiveQuota,
                lblStorageQuota, txtStorageQuota,
                grpExchange
            });
        }

        private void MarkDirty()
        {
            // Benachrichtigt das MMC PropertyPage Framework, dass sich Daten geändert haben
            if (this.ParentForm is PropertyPage parentPage)
            {
                parentPage.Dirty = true;
            }
        }

        public void ClearFields()
        {
            txtSendQuota.Text = "";
            txtReceiveQuota.Text = "";
            txtStorageQuota.Text = "";
            txtExSendQuota.Text = "";
            txtExReceiveQuota.Text = "";
            txtExStorageQuota.Text = "";
        }
    }
}
