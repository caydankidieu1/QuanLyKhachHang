using System.Drawing;

namespace QuanLyKhachHang
{
    public sealed class MainMenuForm : Form
    {
        public MainMenuForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            var rootLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                RowCount = 3
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

            var headerPanel = new Panel
            {
                BackColor = Color.FromArgb(19, 86, 76),
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            var applicationTitle = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Location = new Point(28, 20),
                Text = "QUẢN LÝ KHÁCH HÀNG"
            };
            headerPanel.Controls.Add(applicationTitle);

            var navigationLayout = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Fill,
                Padding = new Padding(42, 42, 42, 36),
                RowCount = 1
            };
            navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
            navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var customerButton = CreateNavigationButton("QUẢN LÝ KHÁCH HÀNG", Color.FromArgb(19, 116, 99));
            customerButton.Margin = new Padding(0, 0, 10, 0);
            customerButton.Click += btnCustomers_Click;
            var serviceButton = CreateNavigationButton("QUẢN LÝ DỊCH VỤ", Color.FromArgb(38, 104, 155));
            serviceButton.Margin = new Padding(5, 0, 5, 0);
            serviceButton.Click += btnServices_Click;
            var packageButton = CreateNavigationButton("QUẢN LÝ GÓI DỊCH VỤ", Color.FromArgb(183, 108, 32));
            packageButton.Margin = new Padding(10, 0, 0, 0);
            packageButton.Click += btnPackages_Click;
            navigationLayout.Controls.Add(customerButton, 0, 0);
            navigationLayout.Controls.Add(serviceButton, 1, 0);
            navigationLayout.Controls.Add(packageButton, 2, 0);

            var footerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 14, 28, 14)
            };
            var exitButton = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(98, 108, 112),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(730, 14),
                Size = new Size(110, 34),
                Text = "Thoát",
                UseVisualStyleBackColor = false
            };
            exitButton.FlatAppearance.BorderSize = 0;
            exitButton.Click += btnExit_Click;
            footerPanel.Controls.Add(exitButton);
            footerPanel.Resize += (_, _) => exitButton.Left = footerPanel.ClientSize.Width - exitButton.Width - 28;

            rootLayout.Controls.Add(headerPanel, 0, 0);
            rootLayout.Controls.Add(navigationLayout, 0, 1);
            rootLayout.Controls.Add(footerPanel, 0, 2);

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(241, 244, 245);
            ClientSize = new Size(900, 560);
            Controls.Add(rootLayout);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            MinimumSize = new Size(720, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Quản lý khách hàng";

            ResumeLayout(false);
        }

        private static Button CreateNavigationButton(string text, Color backColor)
        {
            var button = new Button
            {
                BackColor = backColor,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Text = text,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void btnCustomers_Click(object? sender, EventArgs e)
        {
            using var customerManagementForm = new Form1();
            customerManagementForm.ShowDialog(this);
        }

        private void btnServices_Click(object? sender, EventArgs e)
        {
            using var serviceManagementForm = new ServiceManagementForm();
            serviceManagementForm.ShowDialog(this);
        }

        private void btnPackages_Click(object? sender, EventArgs e)
        {
            using var packageManagementForm = new ServicePackageManagementForm();
            packageManagementForm.ShowDialog(this);
        }

        private void btnExit_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}