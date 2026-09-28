using System.Drawing;
using System.Globalization;
using Microsoft.Data.Sqlite;
using QuanLyKhachHang.Data;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang
{
    public sealed class ServicePackageManagementForm : Form
    {
        private readonly ServicePackageRepository _packageRepository;
        private readonly ServiceRepository _serviceRepository;
        private readonly TextBox _txtIdentifierName = CreateSingleLineInput("Ví dụ: Gói chăm sóc toàn diện", 150);
        private readonly TextBox _txtPackagePrice = CreateSingleLineInput("Ví dụ: 500000", 20);
        private readonly NumericUpDown _nudDiscountPercent = new();
        private readonly TextBox _txtServiceFilter = CreateSingleLineInput("Tìm dịch vụ theo tên", 150);
        private readonly TextBox _txtSearch = CreateSingleLineInput("Tên gói hoặc tên dịch vụ", 200);
        private readonly CheckedListBox _clbServices = new();
        private readonly Label _lblSelectedServiceCount = new();
        private readonly Label _lblSelectedServicesTotal = new();
        private readonly Label _lblDiscountAmount = new();
        private readonly Label _lblDiscountedServicesTotal = new();
        private readonly Label _lblPackageCount = new();
        private readonly DataGridView _dgvPackages = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnUpdate = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnClear = new();

        private long? _selectedPackageId;
        private DateTime _selectedPackageCreatedAt;
        private readonly List<Service> _availableServices = new();
        private readonly HashSet<long> _selectedServiceIds = new();
        private bool _normalizingPriceText;
        private bool _suppressServiceChecks;
        private bool _suppressPackageSelection;

        private sealed class ServiceOption
        {
            public ServiceOption(Service service)
            {
                Id = service.Id;
                Price = service.Price;
                DisplayName = $"{service.Name} ({service.Price:N0} VNĐ)";
            }

            public long Id { get; }

            public decimal Price { get; }

            public string DisplayName { get; }

            public override string ToString() => DisplayName;
        }

        public ServicePackageManagementForm()
        {
            InitializeComponent();
            _packageRepository = new ServicePackageRepository();
            _serviceRepository = new ServiceRepository();
            ConfigurePackageGrid();
            LoadAvailableServices();
            LoadPackages();
            ClearForm();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            var rootLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                RowCount = 2
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var headerPanel = new Panel
            {
                BackColor = Color.FromArgb(19, 86, 76),
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            var titleLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Location = new Point(24, 17),
                Text = "QUẢN LÝ GÓI DỊCH VỤ"
            };
            _lblPackageCount.Dock = DockStyle.Right;
            _lblPackageCount.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _lblPackageCount.ForeColor = Color.White;
            _lblPackageCount.Size = new Size(250, 76);
            _lblPackageCount.Text = "Tổng cộng: 0 gói";
            _lblPackageCount.TextAlign = ContentAlignment.MiddleCenter;
            headerPanel.Controls.Add(_lblPackageCount);
            headerPanel.Controls.Add(titleLabel);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 20, 22, 22)
            };
            var contentLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                RowCount = 1
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var editorContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 12, 0)
            };
            editorContainer.Controls.Add(CreatePackageEditor());
            contentLayout.Controls.Add(editorContainer, 0, 0);
            contentLayout.Controls.Add(CreatePackageList(), 1, 0);
            contentPanel.Controls.Add(contentLayout);

            rootLayout.Controls.Add(headerPanel, 0, 0);
            rootLayout.Controls.Add(contentPanel, 0, 1);

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(241, 244, 245);
            ClientSize = new Size(1320, 760);
            Controls.Add(rootLayout);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            MinimumSize = new Size(1000, 620);
            StartPosition = FormStartPosition.CenterParent;
            Text = "Quản lý gói dịch vụ";

            ResumeLayout(false);
        }

        private GroupBox CreatePackageEditor()
        {
            var groupBox = new GroupBox
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                Padding = new Padding(14, 30, 14, 14),
                Text = "Thông tin gói dịch vụ"
            };
            var editorLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 11
            };
            editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            _txtIdentifierName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtPackagePrice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtPackagePrice.KeyPress += txtPackagePrice_KeyPress;
            _txtPackagePrice.TextChanged += txtPackagePrice_TextChanged;
            _nudDiscountPercent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _nudDiscountPercent.DecimalPlaces = 2;
            _nudDiscountPercent.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _nudDiscountPercent.Increment = 1M;
            _nudDiscountPercent.Maximum = 100M;
            _nudDiscountPercent.Minimum = 0M;
            _nudDiscountPercent.ValueChanged += nudDiscountPercent_ValueChanged;

            _lblSelectedServiceCount.BackColor = Color.FromArgb(235, 241, 239);
            _lblSelectedServiceCount.Dock = DockStyle.Fill;
            _lblSelectedServiceCount.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _lblSelectedServiceCount.Padding = new Padding(6, 0, 6, 0);
            _lblSelectedServiceCount.TextAlign = ContentAlignment.MiddleLeft;

            _clbServices.BackColor = Color.FromArgb(247, 250, 249);
            _clbServices.BorderStyle = BorderStyle.FixedSingle;
            _clbServices.CheckOnClick = true;
            _clbServices.Dock = DockStyle.Fill;
            _clbServices.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _clbServices.IntegralHeight = false;
            _clbServices.ItemCheck += clbServices_ItemCheck;

            _txtServiceFilter.Dock = DockStyle.Fill;
            _txtServiceFilter.TextChanged += txtServiceFilter_TextChanged;

            editorLayout.Controls.Add(CreateFieldLabel("Tên định danh gói *"), 0, 0);
            editorLayout.Controls.Add(_txtIdentifierName, 0, 1);
            editorLayout.Controls.Add(CreateFieldLabel("Giá bán gói (VNĐ) *"), 0, 2);
            editorLayout.Controls.Add(_txtPackagePrice, 0, 3);
            editorLayout.Controls.Add(CreateFieldLabel("Giảm giá (%)"), 0, 4);
            editorLayout.Controls.Add(_nudDiscountPercent, 0, 5);
            editorLayout.Controls.Add(CreateFieldLabel("Dịch vụ trong gói *"), 0, 6);
            editorLayout.Controls.Add(CreateServiceFilterPanel(), 0, 7);
            editorLayout.Controls.Add(_clbServices, 0, 8);
            editorLayout.Controls.Add(CreatePriceSummaryPanel(), 0, 9);
            editorLayout.Controls.Add(CreateActionPanel(), 0, 10);

            groupBox.Controls.Add(editorLayout);
            return groupBox;
        }

        private Panel CreateServiceFilterPanel()
        {
            var filterLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));

            _lblSelectedServiceCount.BackColor = Color.FromArgb(235, 241, 239);
            _lblSelectedServiceCount.Dock = DockStyle.Fill;
            _lblSelectedServiceCount.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _lblSelectedServiceCount.Padding = new Padding(6, 0, 6, 0);
            _lblSelectedServiceCount.TextAlign = ContentAlignment.MiddleRight;

            filterLayout.Controls.Add(_txtServiceFilter, 0, 0);
            filterLayout.Controls.Add(_lblSelectedServiceCount, 1, 0);
            return new Panel
            {
                Controls = { filterLayout },
                Dock = DockStyle.Fill
            };
        }

        private Panel CreatePriceSummaryPanel()
        {
            var summaryPanel = new Panel
            {
                BackColor = Color.FromArgb(235, 241, 239),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };
            var summaryLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                RowCount = 3
            };
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));

            ConfigureSummaryLabel(_lblSelectedServicesTotal, FontStyle.Regular);
            ConfigureSummaryLabel(_lblDiscountAmount, FontStyle.Regular);
            ConfigureSummaryLabel(_lblDiscountedServicesTotal, FontStyle.Bold);
            summaryLayout.Controls.Add(_lblSelectedServicesTotal, 0, 0);
            summaryLayout.Controls.Add(_lblDiscountAmount, 0, 1);
            summaryLayout.Controls.Add(_lblDiscountedServicesTotal, 0, 2);
            summaryPanel.Controls.Add(summaryLayout);
            return summaryPanel;
        }

        private static void ConfigureSummaryLabel(Label label, FontStyle fontStyle)
        {
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Segoe UI", 9F, fontStyle, GraphicsUnit.Point);
            label.ForeColor = Color.FromArgb(30, 53, 50);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        private GroupBox CreatePackageList()
        {
            var groupBox = new GroupBox
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                Padding = new Padding(14, 30, 14, 14),
                Text = "Danh sách gói dịch vụ"
            };
            var listLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                RowCount = 2
            };
            listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var searchLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Text = "Tìm kiếm",
                TextAlign = ContentAlignment.MiddleLeft
            };
            _txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtSearch.Margin = new Padding(3, 9, 3, 3);
            _txtSearch.TextChanged += txtSearch_TextChanged;

            listLayout.Controls.Add(searchLabel, 0, 0);
            listLayout.Controls.Add(_txtSearch, 1, 0);
            listLayout.Controls.Add(_dgvPackages, 0, 1);
            listLayout.SetColumnSpan(_dgvPackages, 2);
            groupBox.Controls.Add(listLayout);
            return groupBox;
        }

        private FlowLayoutPanel CreateActionPanel()
        {
            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 5, 0, 0),
                WrapContents = false
            };
            ConfigureActionButton(_btnAdd, "Thêm", Color.FromArgb(19, 116, 99), 84);
            ConfigureActionButton(_btnUpdate, "Cập nhật", Color.FromArgb(38, 104, 155), 76);
            ConfigureActionButton(_btnDelete, "Xóa", Color.FromArgb(181, 63, 63), 70);
            ConfigureActionButton(_btnClear, "Làm mới", Color.FromArgb(98, 108, 112), 78);
            _btnAdd.Click += btnAdd_Click;
            _btnUpdate.Click += btnUpdate_Click;
            _btnDelete.Click += btnDelete_Click;
            _btnClear.Click += btnClear_Click;
            actionPanel.Controls.Add(_btnAdd);
            actionPanel.Controls.Add(_btnUpdate);
            actionPanel.Controls.Add(_btnDelete);
            actionPanel.Controls.Add(_btnClear);
            return actionPanel;
        }

        private void ConfigurePackageGrid()
        {
            _dgvPackages.AllowUserToAddRows = false;
            _dgvPackages.AllowUserToDeleteRows = false;
            _dgvPackages.AllowUserToResizeRows = false;
            _dgvPackages.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(247, 250, 249)
            };
            _dgvPackages.AutoGenerateColumns = false;
            _dgvPackages.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvPackages.BackgroundColor = Color.White;
            _dgvPackages.BorderStyle = BorderStyle.FixedSingle;
            _dgvPackages.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _dgvPackages.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _dgvPackages.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(222, 239, 235),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                SelectionBackColor = Color.FromArgb(222, 239, 235),
                SelectionForeColor = Color.FromArgb(30, 53, 50),
                WrapMode = DataGridViewTriState.True
            };
            _dgvPackages.ColumnHeadersHeight = 38;
            _dgvPackages.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvPackages.DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(41, 50, 54),
                SelectionBackColor = Color.FromArgb(199, 229, 222),
                SelectionForeColor = Color.FromArgb(22, 55, 49),
                WrapMode = DataGridViewTriState.False
            };
            _dgvPackages.Dock = DockStyle.Fill;
            _dgvPackages.EnableHeadersVisualStyles = false;
            _dgvPackages.GridColor = Color.FromArgb(226, 232, 230);
            _dgvPackages.MultiSelect = false;
            _dgvPackages.ReadOnly = true;
            _dgvPackages.RowHeadersVisible = false;
            _dgvPackages.RowTemplate.Height = 34;
            _dgvPackages.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgvPackages.SelectionChanged += dgvPackages_SelectionChanged;

            _dgvPackages.Columns.Add(CreateTextColumn("IdentifierName", "Tên định danh", 21));
            _dgvPackages.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PackagePrice",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" },
                FillWeight = 15,
                HeaderText = "Giá gói (VNĐ)"
            });
            _dgvPackages.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DiscountPercent",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "0.##" },
                FillWeight = 10,
                HeaderText = "Giảm (%)"
            });
            _dgvPackages.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ServiceCount",
                FillWeight = 10,
                HeaderText = "Số dịch vụ"
            });
            _dgvPackages.Columns.Add(CreateTextColumn("ServiceNames", "Dịch vụ", 28));
            _dgvPackages.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "UpdatedAt",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" },
                FillWeight = 16,
                HeaderText = "Cập nhật"
            });
        }

        private void LoadAvailableServices()
        {
            _availableServices.Clear();
            _availableServices.AddRange(_serviceRepository.GetAll());
            _selectedServiceIds.IntersectWith(_availableServices.Select(service => service.Id));
            ApplyServiceFilter();
        }

        private void ApplyServiceFilter()
        {
            var filterText = _txtServiceFilter.Text.Trim();
            _suppressServiceChecks = true;
            _clbServices.BeginUpdate();
            try
            {
                _clbServices.Items.Clear();
                foreach (var service in _availableServices.Where(service =>
                    string.IsNullOrWhiteSpace(filterText)
                    || service.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase)))
                {
                    var option = new ServiceOption(service);
                    _clbServices.Items.Add(option, _selectedServiceIds.Contains(option.Id));
                }
            }
            finally
            {
                _clbServices.EndUpdate();
                _suppressServiceChecks = false;
            }

            UpdateSelectedServiceCount();
        }

        private void LoadPackages()
        {
            _suppressPackageSelection = true;
            try
            {
                var packages = _packageRepository.GetAll(_txtSearch.Text).ToList();
                _dgvPackages.DataSource = packages;
                _dgvPackages.ClearSelection();
                _dgvPackages.CurrentCell = null;
                _lblPackageCount.Text = $"Tổng cộng: {packages.Count} gói";
            }
            finally
            {
                _suppressPackageSelection = false;
            }
        }

        private void btnAdd_Click(object? sender, EventArgs e)
        {
            if (!TryGetPackageFromForm(out var servicePackage, out var serviceIds))
            {
                return;
            }

            try
            {
                _packageRepository.Add(servicePackage, serviceIds);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                MessageBox.Show("Tên định danh gói đã tồn tại. Vui lòng dùng tên khác.", "Không thể thêm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtIdentifierName.Focus();
                return;
            }

            LoadPackages();
            ClearForm();
            MessageBox.Show("Đã thêm gói dịch vụ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnUpdate_Click(object? sender, EventArgs e)
        {
            if (_selectedPackageId is null)
            {
                MessageBox.Show("Hãy chọn một gói dịch vụ trong danh sách để cập nhật.", "Chưa chọn gói", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryGetPackageFromForm(out var servicePackage, out var serviceIds))
            {
                return;
            }

            try
            {
                _packageRepository.Update(servicePackage, serviceIds);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                MessageBox.Show("Tên định danh gói đã tồn tại. Vui lòng dùng tên khác.", "Không thể cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtIdentifierName.Focus();
                return;
            }

            LoadPackages();
            ClearForm();
            MessageBox.Show("Đã cập nhật gói dịch vụ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedPackageId is null)
            {
                MessageBox.Show("Hãy chọn một gói dịch vụ trong danh sách để xóa.", "Chưa chọn gói", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Bạn có chắc muốn xóa gói '{_txtIdentifierName.Text}'?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            _packageRepository.Delete(_selectedPackageId.Value);
            LoadPackages();
            ClearForm();
            MessageBox.Show("Đã xóa gói dịch vụ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnClear_Click(object? sender, EventArgs e)
        {
            ClearForm();
        }

        private void txtSearch_TextChanged(object? sender, EventArgs e)
        {
            LoadPackages();
            ClearForm(focusIdentifierName: false);
        }

        private void dgvPackages_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressPackageSelection || _dgvPackages.CurrentRow?.DataBoundItem is not ServicePackage servicePackage)
            {
                return;
            }

            _selectedPackageId = servicePackage.Id;
            _selectedPackageCreatedAt = servicePackage.CreatedAt;
            _txtIdentifierName.Text = servicePackage.IdentifierName;
            _txtPackagePrice.Text = servicePackage.PackagePrice.ToString("0", CultureInfo.InvariantCulture);
            _nudDiscountPercent.Value = Math.Clamp(servicePackage.DiscountPercent, _nudDiscountPercent.Minimum, _nudDiscountPercent.Maximum);
            SetSelectedServiceIds(_packageRepository.GetServiceIds(servicePackage.Id));
            _btnAdd.Enabled = false;
            _btnUpdate.Enabled = true;
            _btnDelete.Enabled = true;
        }

        private void clbServices_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_suppressServiceChecks || e.Index < 0 || _clbServices.Items[e.Index] is not ServiceOption service)
            {
                return;
            }

            if (e.NewValue == CheckState.Checked)
            {
                _selectedServiceIds.Add(service.Id);
            }
            else
            {
                _selectedServiceIds.Remove(service.Id);
            }

            BeginInvoke(UpdateSelectedServiceCount);
        }

        private void txtServiceFilter_TextChanged(object? sender, EventArgs e)
        {
            ApplyServiceFilter();
        }

        private void nudDiscountPercent_ValueChanged(object? sender, EventArgs e)
        {
            UpdateSelectedServiceCount();
        }

        private bool TryGetPackageFromForm(out ServicePackage servicePackage, out IReadOnlyCollection<long> serviceIds)
        {
            servicePackage = new ServicePackage();
            serviceIds = Array.Empty<long>();

            if (string.IsNullOrWhiteSpace(_txtIdentifierName.Text))
            {
                ShowRequiredFieldMessage("tên định danh gói", _txtIdentifierName);
                return false;
            }

            if (!decimal.TryParse(_txtPackagePrice.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var packagePrice) || packagePrice < 0)
            {
                MessageBox.Show("Vui lòng nhập giá gói hợp lệ lớn hơn hoặc bằng 0.", "Giá chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPackagePrice.Focus();
                return false;
            }

            serviceIds = GetSelectedServiceIds();
            if (serviceIds.Count == 0)
            {
                var message = _clbServices.Items.Count == 0
                    ? "Chưa có dịch vụ để thêm vào gói. Hãy tạo dịch vụ trước."
                    : "Vui lòng chọn ít nhất một dịch vụ cho gói.";
                MessageBox.Show(message, "Thiếu dịch vụ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _clbServices.Focus();
                return false;
            }

            servicePackage = new ServicePackage
            {
                Id = _selectedPackageId ?? 0,
                IdentifierName = _txtIdentifierName.Text,
                PackagePrice = packagePrice,
                DiscountPercent = _nudDiscountPercent.Value,
                CreatedAt = _selectedPackageCreatedAt
            };
            return true;
        }

        private IReadOnlyCollection<long> GetSelectedServiceIds()
        {
            return _selectedServiceIds.ToArray();
        }

        private void SetSelectedServiceIds(IReadOnlyCollection<long> serviceIds)
        {
            _selectedServiceIds.Clear();
            var availableServiceIds = _availableServices.Select(service => service.Id).ToHashSet();
            _selectedServiceIds.UnionWith(serviceIds.Where(availableServiceIds.Contains));
            ApplyServiceFilter();
        }

        private void UpdateSelectedServiceCount()
        {
            var selectedServices = _availableServices.Where(service => _selectedServiceIds.Contains(service.Id)).ToList();
            var selectedCount = selectedServices.Count;
            var servicesTotal = selectedServices.Sum(service => service.Price);
            var discountAmount = servicesTotal * _nudDiscountPercent.Value / 100M;
            var discountedServicesTotal = servicesTotal - discountAmount;
            _lblSelectedServiceCount.Text = _clbServices.Items.Count == 0
                ? "Chưa có dịch vụ. Hãy tạo dịch vụ trước."
                : $"Đã chọn {selectedCount}/{_availableServices.Count} dịch vụ";
            _lblSelectedServicesTotal.Text = $"Tổng giá dịch vụ: {servicesTotal:N0} VNĐ";
            _lblDiscountAmount.Text = $"Giảm giá ({_nudDiscountPercent.Value:0.##}%): -{discountAmount:N0} VNĐ";
            _lblDiscountedServicesTotal.Text = $"Sau giảm theo dịch vụ: {discountedServicesTotal:N0} VNĐ";
        }

        private void ClearForm(bool focusIdentifierName = true)
        {
            _selectedPackageId = null;
            _selectedPackageCreatedAt = default;
            _txtIdentifierName.Clear();
            _txtPackagePrice.Clear();
            _nudDiscountPercent.Value = 0M;
            SetSelectedServiceIds(Array.Empty<long>());
            _btnAdd.Enabled = true;
            _btnUpdate.Enabled = false;
            _btnDelete.Enabled = false;

            _suppressPackageSelection = true;
            try
            {
                _dgvPackages.ClearSelection();
                _dgvPackages.CurrentCell = null;
            }
            finally
            {
                _suppressPackageSelection = false;
            }

            if (focusIdentifierName)
            {
                _txtIdentifierName.Focus();
            }
        }

        private static void txtPackagePrice_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9'))
            {
                e.Handled = true;
            }
        }

        private void txtPackagePrice_TextChanged(object? sender, EventArgs e)
        {
            if (_normalizingPriceText)
            {
                return;
            }

            var numericText = new string(_txtPackagePrice.Text.Where(character => character is >= '0' and <= '9').ToArray());
            if (_txtPackagePrice.Text == numericText)
            {
                return;
            }

            _normalizingPriceText = true;
            try
            {
                _txtPackagePrice.Text = numericText;
                _txtPackagePrice.SelectionStart = numericText.Length;
            }
            finally
            {
                _normalizingPriceText = false;
            }
        }

        private static TextBox CreateSingleLineInput(string placeholder, int maxLength)
        {
            return new TextBox
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                MaxLength = maxLength,
                PlaceholderText = placeholder
            };
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

        private static void ConfigureActionButton(Button button, string text, Color backColor, int width)
        {
            button.BackColor = backColor;
            button.FlatAppearance.BorderSize = 0;
            button.FlatStyle = FlatStyle.Flat;
            button.ForeColor = Color.White;
            button.Margin = new Padding(3);
            button.Size = new Size(width, 30);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string headerText, float fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                FillWeight = fillWeight,
                HeaderText = headerText
            };
        }

        private static void ShowRequiredFieldMessage(string fieldName, Control control)
        {
            MessageBox.Show($"Vui lòng nhập {fieldName}.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            control.Focus();
        }
    }
}