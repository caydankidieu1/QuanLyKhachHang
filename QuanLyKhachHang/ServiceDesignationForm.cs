using System.Drawing;
using System.Globalization;
using QuanLyKhachHang.Data;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang
{
    public sealed class ServiceDesignationForm : Form
    {
        private readonly long _customerId;
        private readonly string _customerCode;
        private readonly string _customerName;
        private readonly ServiceRepository _serviceRepository;
        private readonly ServicePackageRepository _servicePackageRepository;
        private readonly ServiceDesignationRepository _serviceDesignationRepository;
        private readonly DataGridView _serviceGrid = new();
        private readonly DataGridView _servicePackageGrid = new();
        private readonly DataGridView _designationGrid = new();
        private readonly Label _designationCountLabel = new();
        private readonly Label _totalPriceLabel = new();
        private readonly Button _deleteDesignationButton = new();
        private List<DesignationOption> _serviceOptions = new();
        private List<DesignationOption> _servicePackageOptions = new();
        private bool _suppressDesignationSelectionChanged;

        public ServiceDesignationForm(long customerId, string customerCode, string customerName)
        {
            _customerId = customerId;
            _customerCode = customerCode;
            _customerName = customerName;
            _serviceRepository = new ServiceRepository();
            _servicePackageRepository = new ServicePackageRepository();
            _serviceDesignationRepository = new ServiceDesignationRepository();

            InitializeComponent();
            LoadAvailableOptions();
            LoadDesignationList();
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
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));

            var headerPanel = new Panel
            {
                BackColor = Color.FromArgb(183, 108, 32),
                Dock = DockStyle.Fill
            };
            headerPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Location = new Point(24, 19),
                Text = "PHIẾU CHỈ ĐỊNH"
            });

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 20, 22, 16)
            };
            var contentSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300,
                SplitterWidth = 10
            };
            contentSplitContainer.Panel1.Padding = new Padding(0, 0, 0, 10);

            var selectionGroupBox = CreateGroupBox("Tạo phiếu chỉ định");
            var selectionDetailsLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                RowCount = 2
            };
            selectionDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            selectionDetailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            selectionDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            selectionDetailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            selectionDetailsLayout.Controls.Add(CreateFieldLabel("Khách hàng"), 0, 0);
            selectionDetailsLayout.Controls.Add(CreateReadOnlyValueLabel($"{_customerCode} - {_customerName}"), 1, 0);

            var availableItemsLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 3, 3, 6),
                RowCount = 1
            };
            availableItemsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            availableItemsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            ConfigureSelectionGrid(_serviceGrid, "Tên dịch vụ");
            ConfigureSelectionGrid(_servicePackageGrid, "Tên gói dịch vụ");
            availableItemsLayout.Controls.Add(CreateSelectionGroupBox("Dịch vụ", _serviceGrid), 0, 0);
            availableItemsLayout.Controls.Add(CreateSelectionGroupBox("Gói dịch vụ", _servicePackageGrid), 1, 0);
            selectionDetailsLayout.Controls.Add(availableItemsLayout, 0, 1);
            selectionDetailsLayout.SetColumnSpan(availableItemsLayout, 2);
            selectionGroupBox.Controls.Add(selectionDetailsLayout);
            contentSplitContainer.Panel1.Controls.Add(selectionGroupBox);

            var historyGroupBox = CreateGroupBox("Danh sách phiếu chỉ định");
            var historyLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 2
            };
            historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _designationCountLabel.Dock = DockStyle.Fill;
            _designationCountLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _designationCountLabel.TextAlign = ContentAlignment.MiddleRight;
            historyLayout.Controls.Add(_designationCountLabel, 0, 0);
            ConfigureDesignationGrid();
            historyLayout.Controls.Add(_designationGrid, 0, 1);
            historyGroupBox.Controls.Add(historyLayout);
            contentSplitContainer.Panel2.Controls.Add(historyGroupBox);
            contentPanel.Controls.Add(contentSplitContainer);

            var footerLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 22, 12),
                RowCount = 1
            };
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 438F));
            footerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _totalPriceLabel.Dock = DockStyle.Fill;
            _totalPriceLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
            _totalPriceLabel.ForeColor = Color.FromArgb(30, 53, 50);
            _totalPriceLabel.Text = "Tổng giá: 0 đ";
            _totalPriceLabel.TextAlign = ContentAlignment.MiddleRight;
            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var closeButton = CreateActionButton("Đóng", Color.FromArgb(98, 108, 112), 84);
            closeButton.DialogResult = DialogResult.Cancel;
            var clearButton = CreateActionButton("Bỏ chọn", Color.FromArgb(38, 104, 155), 92);
            clearButton.Click += btnClearSelection_Click;
            _deleteDesignationButton.BackColor = Color.FromArgb(181, 63, 63);
            _deleteDesignationButton.Enabled = false;
            _deleteDesignationButton.FlatAppearance.BorderSize = 0;
            _deleteDesignationButton.FlatStyle = FlatStyle.Flat;
            _deleteDesignationButton.ForeColor = Color.White;
            _deleteDesignationButton.Size = new Size(104, 34);
            _deleteDesignationButton.Text = "Xóa phiếu";
            _deleteDesignationButton.UseVisualStyleBackColor = false;
            _deleteDesignationButton.Click += btnDeleteDesignation_Click;
            var saveButton = CreateActionButton("Tạo phiếu", Color.FromArgb(19, 116, 99), 118);
            saveButton.Click += btnSave_Click;
            actionPanel.Controls.Add(closeButton);
            actionPanel.Controls.Add(clearButton);
            actionPanel.Controls.Add(_deleteDesignationButton);
            actionPanel.Controls.Add(saveButton);
            footerLayout.Controls.Add(_totalPriceLabel, 0, 0);
            footerLayout.Controls.Add(actionPanel, 1, 0);

            rootLayout.Controls.Add(headerPanel, 0, 0);
            rootLayout.Controls.Add(contentPanel, 0, 1);
            rootLayout.Controls.Add(footerLayout, 0, 2);

            AcceptButton = saveButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(241, 244, 245);
            CancelButton = closeButton;
            ClientSize = new Size(1040, 720);
            Controls.Add(rootLayout);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            MinimumSize = new Size(860, 620);
            StartPosition = FormStartPosition.CenterParent;
            Text = "Phiếu chỉ định";

            ResumeLayout(false);
        }

        private void LoadAvailableOptions()
        {
            _serviceOptions = _serviceRepository.GetAll()
                .Select(service => new DesignationOption(
                    service.Id,
                    service.Name,
                    service.Price))
                .ToList();
            _servicePackageOptions = _servicePackageRepository.GetAll()
                .Select(servicePackage => new DesignationOption(
                    servicePackage.Id,
                    servicePackage.IdentifierName,
                    servicePackage.PackagePrice))
                .ToList();

            BindOptions(_serviceGrid, _serviceOptions);
            BindOptions(_servicePackageGrid, _servicePackageOptions);
            UpdateTotalPrice();
        }

        private void LoadDesignationList()
        {
            _suppressDesignationSelectionChanged = true;
            try
            {
                var designations = _serviceDesignationRepository.GetAllForCustomer(_customerId).ToList();
                _designationGrid.DataSource = designations;
                _designationGrid.ClearSelection();
                _designationGrid.CurrentCell = null;
                _deleteDesignationButton.Enabled = false;
                _designationCountLabel.Text = $"Tổng cộng: {designations.Count} phiếu";
            }
            finally
            {
                _suppressDesignationSelectionChanged = false;
            }
        }

        private void btnSave_Click(object? sender, EventArgs e)
        {
            var serviceIds = _serviceOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Id)
                .ToList();
            var servicePackageIds = _servicePackageOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Id)
                .ToList();

            if (serviceIds.Count == 0 && servicePackageIds.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất một dịch vụ hoặc gói dịch vụ.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var designationId = _serviceDesignationRepository.Add(
                new ServiceDesignation
                {
                    CustomerId = _customerId
                },
                serviceIds,
                servicePackageIds);

            ClearSelection();
            LoadDesignationList();
            MessageBox.Show(
                $"Đã tạo phiếu chỉ định #{designationId} cho khách hàng {_customerName}.",
                "Thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void btnClearSelection_Click(object? sender, EventArgs e)
        {
            ClearSelection();
        }

        private void btnDeleteDesignation_Click(object? sender, EventArgs e)
        {
            if (_designationGrid.CurrentRow?.DataBoundItem is not ServiceDesignation designation)
            {
                MessageBox.Show(
                    "Hãy chọn một phiếu chỉ định trong danh sách để xóa.",
                    "Chưa chọn phiếu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Bạn có chắc muốn xóa phiếu chỉ định #{designation.Id}?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            if (!_serviceDesignationRepository.Delete(designation.Id))
            {
                MessageBox.Show(
                    "Phiếu chỉ định không còn tồn tại.",
                    "Không thể xóa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                LoadDesignationList();
                return;
            }

            ClearSelection();
            LoadDesignationList();
            MessageBox.Show(
                $"Đã xóa phiếu chỉ định #{designation.Id}.",
                "Thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void designationGrid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressDesignationSelectionChanged)
            {
                return;
            }

            if (_designationGrid.CurrentRow?.DataBoundItem is not ServiceDesignation designation)
            {
                _deleteDesignationButton.Enabled = false;
                return;
            }

            _deleteDesignationButton.Enabled = true;
            var selectedItemIds = _serviceDesignationRepository.GetSelectedItemIds(designation.Id);
            SetSelectedOptions(_serviceOptions, selectedItemIds.ServiceIds);
            SetSelectedOptions(_servicePackageOptions, selectedItemIds.ServicePackageIds);
            _serviceGrid.Refresh();
            _servicePackageGrid.Refresh();
            UpdateTotalPrice();
        }

        private void ClearSelection()
        {
            ClearSelectedOptions(_serviceOptions);
            ClearSelectedOptions(_servicePackageOptions);
            _serviceGrid.Refresh();
            _servicePackageGrid.Refresh();
            UpdateTotalPrice();
        }

        private void selectionGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs eventArgs)
        {
            if (sender is not DataGridView selectionGrid
                || eventArgs.RowIndex < 0
                || selectionGrid.Columns[eventArgs.ColumnIndex].DataPropertyName != nameof(DesignationOption.IsSelected))
            {
                return;
            }

            UpdateTotalPrice();
        }

        private void UpdateTotalPrice()
        {
            var totalPrice = _serviceOptions
                .Where(option => option.IsSelected)
                .Sum(option => option.Price)
                + _servicePackageOptions
                    .Where(option => option.IsSelected)
                    .Sum(option => option.Price);
            _totalPriceLabel.Text = $"Tổng giá: {totalPrice.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ";
        }

        private void ConfigureDesignationGrid()
        {
            _designationGrid.AllowUserToAddRows = false;
            _designationGrid.AllowUserToDeleteRows = false;
            _designationGrid.AllowUserToResizeRows = false;
            _designationGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(247, 250, 249)
            };
            _designationGrid.AutoGenerateColumns = false;
            _designationGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _designationGrid.BackgroundColor = Color.White;
            _designationGrid.BorderStyle = BorderStyle.FixedSingle;
            _designationGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _designationGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _designationGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(229, 236, 234),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                SelectionBackColor = Color.FromArgb(229, 236, 234),
                SelectionForeColor = Color.FromArgb(30, 53, 50)
            };
            _designationGrid.Dock = DockStyle.Fill;
            _designationGrid.EnableHeadersVisualStyles = false;
            _designationGrid.MultiSelect = false;
            _designationGrid.ReadOnly = true;
            _designationGrid.RowHeadersVisible = false;
            _designationGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _designationGrid.SelectionChanged += designationGrid_SelectionChanged;
            _designationGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ServiceDesignation.Id),
                FillWeight = 12,
                HeaderText = "Số phiếu"
            });
            _designationGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ServiceDesignation.CreatedAt),
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" },
                FillWeight = 18,
                HeaderText = "Ngày tạo"
            });
            _designationGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ServiceDesignation.SelectedItems),
                FillWeight = 70,
                HeaderText = "Dịch vụ / gói dịch vụ"
            });
        }

        private static GroupBox CreateGroupBox(string text)
        {
            return new GroupBox
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                Padding = new Padding(14, 30, 14, 14),
                Text = text
            };
        }

        private static GroupBox CreateSelectionGroupBox(string text, DataGridView selectionGrid)
        {
            var groupBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                Padding = new Padding(8, 22, 8, 8),
                Text = text
            };
            groupBox.Controls.Add(selectionGrid);
            return groupBox;
        }

        private static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateReadOnlyValueLabel(string text)
        {
            return new Label
            {
                AutoEllipsis = true,
                BackColor = Color.FromArgb(235, 241, 239),
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Padding = new Padding(8, 0, 8, 0),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void ConfigureSelectionGrid(DataGridView selectionGrid, string nameColumnHeader)
        {
            selectionGrid.AllowUserToAddRows = false;
            selectionGrid.AllowUserToDeleteRows = false;
            selectionGrid.AllowUserToResizeRows = false;
            selectionGrid.AutoGenerateColumns = false;
            selectionGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            selectionGrid.BackgroundColor = Color.White;
            selectionGrid.BorderStyle = BorderStyle.FixedSingle;
            selectionGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            selectionGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            selectionGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(229, 236, 234),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                SelectionBackColor = Color.FromArgb(229, 236, 234),
                SelectionForeColor = Color.FromArgb(30, 53, 50)
            };
            selectionGrid.Dock = DockStyle.Fill;
            selectionGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            selectionGrid.EnableHeadersVisualStyles = false;
            selectionGrid.MultiSelect = false;
            selectionGrid.RowHeadersVisible = false;
            selectionGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            selectionGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                DataPropertyName = nameof(DesignationOption.IsSelected),
                HeaderText = string.Empty
            });
            selectionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                HeaderText = "STT",
                Name = "SequenceNumber",
                ReadOnly = true
            });
            selectionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(DesignationOption.Name),
                FillWeight = 62,
                HeaderText = nameColumnHeader,
                ReadOnly = true
            });
            selectionGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(DesignationOption.Price),
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0"
                },
                FillWeight = 38,
                HeaderText = "Giá",
                ReadOnly = true
            });
            selectionGrid.CellFormatting += (_, eventArgs) =>
            {
                if (eventArgs.ColumnIndex == selectionGrid.Columns["SequenceNumber"].Index
                    && eventArgs.RowIndex >= 0)
                {
                    eventArgs.Value = (eventArgs.RowIndex + 1).ToString();
                    eventArgs.FormattingApplied = true;
                }
            };
            selectionGrid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (selectionGrid.IsCurrentCellDirty)
                {
                    selectionGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            selectionGrid.CellValueChanged += selectionGrid_CellValueChanged;
        }

        private static void BindOptions(DataGridView selectionGrid, IReadOnlyList<DesignationOption> options)
        {
            selectionGrid.DataSource = null;
            selectionGrid.DataSource = options;
        }

        private static void ClearSelectedOptions(IEnumerable<DesignationOption> options)
        {
            foreach (var option in options)
            {
                option.IsSelected = false;
            }
        }

        private static void SetSelectedOptions(IEnumerable<DesignationOption> options, IReadOnlySet<long> selectedIds)
        {
            foreach (var option in options)
            {
                option.IsSelected = selectedIds.Contains(option.Id);
            }
        }

        private static Button CreateActionButton(string text, Color backColor, int width)
        {
            var button = new Button
            {
                BackColor = backColor,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Size = new Size(width, 34),
                Text = text,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private sealed class DesignationOption
        {
            public DesignationOption(long id, string name, decimal price)
            {
                Id = id;
                Name = name;
                Price = price;
            }

            public long Id { get; }

            public string Name { get; }

            public decimal Price { get; }

            public bool IsSelected { get; set; }
        }
    }
}