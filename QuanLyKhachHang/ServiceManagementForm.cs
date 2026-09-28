using System.Drawing;
using System.Globalization;
using QuanLyKhachHang.Data;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang
{
    public sealed class ServiceManagementForm : Form
    {
        private readonly ServiceRepository _repository;
        private readonly TextBox _txtServiceName = CreateSingleLineInput("Tên dịch vụ", 150);
        private readonly TextBox _txtPrice = CreateSingleLineInput("Ví dụ: 150000", 20);
        private readonly TextBox _txtImageName = new();
        private readonly TextBox _txtDescription = new();
        private readonly TextBox _txtSearch = CreateSingleLineInput("Tên hoặc mô tả dịch vụ", 200);
        private readonly PictureBox _pictureService = new();
        private readonly Label _imagePlaceholder = new();
        private readonly Button _btnRemoveAvatar = new();
        private readonly Label _lblAlbumImageCount = new();
        private readonly FlowLayoutPanel _albumThumbnailPanel = new();
        private readonly DataGridView _dgvServices = new();
        private readonly Label _lblServiceCount = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnUpdate = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnClear = new();

        private long? _selectedServiceId;
        private DateTime _selectedServiceCreatedAt;
        private string _selectedImagePath = string.Empty;
        private string _pendingImageSourcePath = string.Empty;
        private bool _removeSelectedImage;
        private readonly List<string> _selectedAlbumImagePaths = new();
        private readonly List<AlbumImageEntry> _albumImageEntries = new();
        private bool _albumImagesChanged;
        private bool _normalizingPriceText;
        private bool _suppressSelectionChanged;

        private sealed class AlbumImageEntry
        {
            public AlbumImageEntry(string imagePath, bool isNew)
            {
                ImagePath = imagePath;
                IsNew = isNew;
            }

            public string ImagePath { get; }

            public bool IsNew { get; }
        }

        public ServiceManagementForm()
        {
            InitializeComponent();
            _repository = new ServiceRepository();
            ConfigureServiceGrid();
            LoadServices();
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
                Text = "QUẢN LÝ DỊCH VỤ"
            };
            _lblServiceCount.Dock = DockStyle.Right;
            _lblServiceCount.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _lblServiceCount.ForeColor = Color.White;
            _lblServiceCount.Size = new Size(290, 76);
            _lblServiceCount.Text = "Tổng cộng: 0 dịch vụ";
            _lblServiceCount.TextAlign = ContentAlignment.MiddleCenter;
            headerPanel.Controls.Add(_lblServiceCount);
            headerPanel.Controls.Add(titleLabel);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 20, 22, 22)
            };
            var splitContainer = new SplitContainer
            {
                BackColor = Color.FromArgb(241, 244, 245),
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 10
            };
            splitContainer.Panel1.Padding = new Padding(0, 0, 12, 0);
            splitContainer.Panel1.Controls.Add(CreateServiceEditor());
            splitContainer.Panel2.Controls.Add(CreateServiceList());
            contentPanel.Controls.Add(splitContainer);

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
            Text = "Quản lý dịch vụ";
            Shown += (_, _) => BeginInvoke(() => SetInitialSplitterDistance(splitContainer));

            ResumeLayout(false);
        }

        private static void SetInitialSplitterDistance(SplitContainer splitContainer)
        {
            var minimumDistance = splitContainer.Panel1MinSize;
            var maximumDistance = splitContainer.ClientSize.Width - splitContainer.Panel2MinSize - splitContainer.SplitterWidth;
            if (maximumDistance < minimumDistance)
            {
                return;
            }

            splitContainer.SplitterDistance = Math.Clamp(525, minimumDistance, maximumDistance);
        }

        private GroupBox CreateServiceEditor()
        {
            var groupBox = new GroupBox
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                Padding = new Padding(14, 30, 14, 14),
                Text = "Thông tin dịch vụ"
            };

            var editorLayout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 13
            };
            editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            _txtServiceName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtPrice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtPrice.KeyPress += txtPrice_KeyPress;
            _txtPrice.TextChanged += txtPrice_TextChanged;
            _txtDescription.AcceptsReturn = true;
            _txtDescription.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _txtDescription.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _txtDescription.MaxLength = 2000;
            _txtDescription.Multiline = true;
            _txtDescription.ScrollBars = ScrollBars.Vertical;

            editorLayout.Controls.Add(CreateFieldLabel("Tên dịch vụ *"), 0, 0);
            editorLayout.Controls.Add(_txtServiceName, 0, 1);
            editorLayout.Controls.Add(CreateFieldLabel("Giá (VNĐ) *"), 0, 2);
            editorLayout.Controls.Add(_txtPrice, 0, 3);
            editorLayout.Controls.Add(CreateFieldLabel("Lưu hình ảnh đại diện"), 0, 4);
            editorLayout.Controls.Add(CreateImageSelectionPanel(), 0, 5);
            editorLayout.Controls.Add(CreateImagePreviewPanel(), 0, 6);
            editorLayout.Controls.Add(CreateFieldLabel("Album ảnh dịch vụ"), 0, 7);
            editorLayout.Controls.Add(CreateAlbumSelectionPanel(), 0, 8);
            editorLayout.Controls.Add(CreateAlbumListPanel(), 0, 9);
            editorLayout.Controls.Add(CreateFieldLabel("Mô tả"), 0, 10);
            editorLayout.Controls.Add(_txtDescription, 0, 11);
            editorLayout.Controls.Add(CreateActionPanel(), 0, 12);

            groupBox.Controls.Add(editorLayout);
            return groupBox;
        }

        private GroupBox CreateServiceList()
        {
            var groupBox = new GroupBox
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                Padding = new Padding(14, 30, 14, 14),
                Text = "Danh sách dịch vụ"
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
            listLayout.Controls.Add(_dgvServices, 0, 1);
            listLayout.SetColumnSpan(_dgvServices, 2);

            groupBox.Controls.Add(listLayout);
            return groupBox;
        }

        private Panel CreateImageSelectionPanel()
        {
            var imageSelectionLayout = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            imageSelectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            imageSelectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));

            _txtImageName.BackColor = Color.FromArgb(235, 241, 239);
            _txtImageName.Dock = DockStyle.Fill;
            _txtImageName.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _txtImageName.ReadOnly = true;
            _txtImageName.TabStop = false;

            var chooseImageButton = CreateActionButton("Chọn ảnh đại diện", Color.FromArgb(38, 104, 155), 112);
            chooseImageButton.Click += btnChooseImage_Click;

            imageSelectionLayout.Controls.Add(_txtImageName, 0, 0);
            imageSelectionLayout.Controls.Add(chooseImageButton, 1, 0);

            return new Panel
            {
                Controls = { imageSelectionLayout },
                Dock = DockStyle.Fill
            };
        }

        private Panel CreateImagePreviewPanel()
        {
            var previewPanel = new Panel
            {
                BackColor = Color.FromArgb(247, 250, 249),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };
            _pictureService.Dock = DockStyle.Fill;
            _pictureService.SizeMode = PictureBoxSizeMode.Zoom;
            _imagePlaceholder.BackColor = Color.FromArgb(247, 250, 249);
            _imagePlaceholder.Dock = DockStyle.Fill;
            _imagePlaceholder.Font = new Font("Segoe UI", 9F, FontStyle.Italic, GraphicsUnit.Point);
            _imagePlaceholder.ForeColor = Color.FromArgb(98, 108, 112);
            _imagePlaceholder.Text = "Chưa chọn ảnh đại diện";
            _imagePlaceholder.TextAlign = ContentAlignment.MiddleCenter;
            _btnRemoveAvatar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRemoveAvatar.BackColor = Color.FromArgb(181, 63, 63);
            _btnRemoveAvatar.FlatAppearance.BorderSize = 0;
            _btnRemoveAvatar.FlatStyle = FlatStyle.Flat;
            _btnRemoveAvatar.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold, GraphicsUnit.Point);
            _btnRemoveAvatar.ForeColor = Color.White;
            _btnRemoveAvatar.Size = new Size(24, 24);
            _btnRemoveAvatar.Text = "X";
            _btnRemoveAvatar.UseVisualStyleBackColor = false;
            _btnRemoveAvatar.Visible = false;
            _btnRemoveAvatar.Click += btnRemoveAvatar_Click;
            previewPanel.Layout += (_, _) => _btnRemoveAvatar.Location = new Point(
                Math.Max(3, previewPanel.ClientSize.Width - _btnRemoveAvatar.Width - 4),
                4);
            previewPanel.Controls.Add(_pictureService);
            previewPanel.Controls.Add(_imagePlaceholder);
            previewPanel.Controls.Add(_btnRemoveAvatar);
            _btnRemoveAvatar.BringToFront();
            return previewPanel;
        }

        private Panel CreateAlbumSelectionPanel()
        {
            var albumSelectionLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            albumSelectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            albumSelectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));

            _lblAlbumImageCount.BackColor = Color.FromArgb(235, 241, 239);
            _lblAlbumImageCount.Dock = DockStyle.Fill;
            _lblAlbumImageCount.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _lblAlbumImageCount.Padding = new Padding(6, 0, 6, 0);
            _lblAlbumImageCount.TextAlign = ContentAlignment.MiddleLeft;

            var chooseAlbumButton = CreateActionButton("Chọn ảnh album", Color.FromArgb(38, 104, 155), 106);
            chooseAlbumButton.Click += btnChooseAlbumImages_Click;

            albumSelectionLayout.Controls.Add(_lblAlbumImageCount, 0, 0);
            albumSelectionLayout.Controls.Add(chooseAlbumButton, 1, 0);

            return new Panel
            {
                Controls = { albumSelectionLayout },
                Dock = DockStyle.Fill
            };
        }

        private Panel CreateAlbumListPanel()
        {
            var albumListPanel = new Panel
            {
                BackColor = Color.FromArgb(247, 250, 249),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };
            _albumThumbnailPanel.AutoScroll = true;
            _albumThumbnailPanel.BackColor = Color.FromArgb(247, 250, 249);
            _albumThumbnailPanel.Dock = DockStyle.Fill;
            _albumThumbnailPanel.FlowDirection = FlowDirection.LeftToRight;
            _albumThumbnailPanel.Padding = new Padding(4);
            _albumThumbnailPanel.WrapContents = true;
            albumListPanel.Controls.Add(_albumThumbnailPanel);
            return albumListPanel;
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

        private void ConfigureServiceGrid()
        {
            _dgvServices.AllowUserToAddRows = false;
            _dgvServices.AllowUserToDeleteRows = false;
            _dgvServices.AllowUserToResizeRows = false;
            _dgvServices.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(247, 250, 249)
            };
            _dgvServices.AutoGenerateColumns = false;
            _dgvServices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvServices.BackgroundColor = Color.White;
            _dgvServices.BorderStyle = BorderStyle.FixedSingle;
            _dgvServices.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _dgvServices.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _dgvServices.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(222, 239, 235),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 53, 50),
                SelectionBackColor = Color.FromArgb(222, 239, 235),
                SelectionForeColor = Color.FromArgb(30, 53, 50),
                WrapMode = DataGridViewTriState.True
            };
            _dgvServices.ColumnHeadersHeight = 38;
            _dgvServices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvServices.DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(41, 50, 54),
                SelectionBackColor = Color.FromArgb(199, 229, 222),
                SelectionForeColor = Color.FromArgb(22, 55, 49),
                WrapMode = DataGridViewTriState.False
            };
            _dgvServices.Dock = DockStyle.Fill;
            _dgvServices.EnableHeadersVisualStyles = false;
            _dgvServices.GridColor = Color.FromArgb(226, 232, 230);
            _dgvServices.MultiSelect = false;
            _dgvServices.ReadOnly = true;
            _dgvServices.RowHeadersVisible = false;
            _dgvServices.RowTemplate.Height = 34;
            _dgvServices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgvServices.SelectionChanged += dgvServices_SelectionChanged;

            _dgvServices.Columns.Add(CreateTextColumn("Name", "Tên dịch vụ", 27));
            _dgvServices.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Price",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" },
                FillWeight = 18,
                HeaderText = "Giá (VNĐ)"
            });
            _dgvServices.Columns.Add(CreateTextColumn("Description", "Mô tả", 37));
            _dgvServices.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "UpdatedAt",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" },
                FillWeight = 18,
                HeaderText = "Cập nhật"
            });
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

        private static Button CreateActionButton(string text, Color backColor, int width)
        {
            var button = new Button();
            ConfigureActionButton(button, text, backColor, width);
            return button;
        }

        private static void ConfigureActionButton(Button button, string text, Color backColor, int width)
        {
            button.BackColor = backColor;
            button.FlatAppearance.BorderSize = 0;
            button.FlatStyle = FlatStyle.Flat;
            button.ForeColor = Color.White;
            button.Margin = new Padding(3, 3, 3, 3);
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

        private void LoadServices()
        {
            _suppressSelectionChanged = true;
            try
            {
                var services = _repository.GetAll(_txtSearch.Text).ToList();
                _dgvServices.DataSource = services;
                _dgvServices.ClearSelection();
                _dgvServices.CurrentCell = null;
                _lblServiceCount.Text = $"Tổng cộng: {services.Count} dịch vụ";
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private void btnAdd_Click(object? sender, EventArgs e)
        {
            if (!TryGetServiceFromForm(out var service))
            {
                return;
            }

            string? copiedImagePath = null;
            var copiedAlbumImagePaths = new List<string>();
            try
            {
                copiedImagePath = CopyPendingImage();
                var albumImagePaths = BuildAlbumImagePaths(copiedAlbumImagePaths);
                service.ImagePath = ResolveImagePath(copiedImagePath);
                _repository.Add(service, albumImagePaths);
            }
            catch (Exception exception)
            {
                DeleteManagedImage(copiedImagePath);
                DeleteManagedImages(copiedAlbumImagePaths);
                ShowSaveError(exception);
                return;
            }

            LoadServices();
            ClearForm();
            MessageBox.Show("Đã thêm dịch vụ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnUpdate_Click(object? sender, EventArgs e)
        {
            if (_selectedServiceId is null)
            {
                MessageBox.Show("Hãy chọn một dịch vụ trong danh sách để cập nhật.", "Chưa chọn dịch vụ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryGetServiceFromForm(out var service))
            {
                return;
            }

            string? copiedImagePath = null;
            var copiedAlbumImagePaths = new List<string>();
            var previousImagePath = _selectedImagePath;
            var previousAlbumImagePaths = _selectedAlbumImagePaths.ToList();
            IReadOnlyList<string>? albumImagePaths = null;
            try
            {
                copiedImagePath = CopyPendingImage();
                if (_albumImagesChanged)
                {
                    albumImagePaths = BuildAlbumImagePaths(copiedAlbumImagePaths);
                }

                service.ImagePath = ResolveImagePath(copiedImagePath);
                _repository.Update(service, albumImagePaths);
            }
            catch (Exception exception)
            {
                DeleteManagedImage(copiedImagePath);
                DeleteManagedImages(copiedAlbumImagePaths);
                ShowSaveError(exception);
                return;
            }

            if (!string.Equals(previousImagePath, service.ImagePath, StringComparison.OrdinalIgnoreCase))
            {
                DeleteManagedImage(previousImagePath);
            }

            if (_albumImagesChanged && albumImagePaths is not null)
            {
                var retainedAlbumImagePaths = albumImagePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
                DeleteManagedImages(previousAlbumImagePaths.Where(imagePath => !retainedAlbumImagePaths.Contains(imagePath)));
            }

            LoadServices();
            ClearForm();
            MessageBox.Show("Đã cập nhật dịch vụ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedServiceId is null)
            {
                MessageBox.Show("Hãy chọn một dịch vụ trong danh sách để xóa.", "Chưa chọn dịch vụ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Bạn có chắc muốn xóa dịch vụ '{_txtServiceName.Text}'?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _repository.Delete(_selectedServiceId.Value);
                DeleteManagedImage(_selectedImagePath);
                DeleteManagedImages(_selectedAlbumImagePaths);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Không thể xóa dịch vụ.\n{exception.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadServices();
            ClearForm();
            MessageBox.Show("Đã xóa dịch vụ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnClear_Click(object? sender, EventArgs e)
        {
            ClearForm();
        }

        private void btnChooseImage_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Tệp ảnh|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tất cả tệp|*.*",
                Title = "Chọn ảnh đại diện dịch vụ"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _pendingImageSourcePath = dialog.FileName;
            _removeSelectedImage = false;
            _txtImageName.Text = Path.GetFileName(dialog.FileName);
            ShowImagePreview(dialog.FileName);
        }

        private void btnRemoveAvatar_Click(object? sender, EventArgs e)
        {
            _pendingImageSourcePath = string.Empty;
            _removeSelectedImage = true;
            _txtImageName.Text = "Chưa chọn ảnh đại diện";
            ShowImagePreview(null);
        }

        private void btnChooseAlbumImages_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Tệp ảnh|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tất cả tệp|*.*",
                Multiselect = true,
                Title = "Chọn các ảnh cho album dịch vụ"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            foreach (var imagePath in dialog.FileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (_albumImageEntries.All(entry => !string.Equals(entry.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase)))
                {
                    _albumImageEntries.Add(new AlbumImageEntry(imagePath, isNew: true));
                }
            }

            _albumImagesChanged = true;
            RenderAlbumThumbnails();
        }

        private void txtSearch_TextChanged(object? sender, EventArgs e)
        {
            LoadServices();
            ClearForm(focusServiceName: false);
        }

        private void dgvServices_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressSelectionChanged || _dgvServices.CurrentRow?.DataBoundItem is not Service service)
            {
                return;
            }

            _selectedServiceId = service.Id;
            _selectedServiceCreatedAt = service.CreatedAt;
            _selectedImagePath = service.ImagePath;
            _pendingImageSourcePath = string.Empty;
            _removeSelectedImage = false;
            _selectedAlbumImagePaths.Clear();
            _selectedAlbumImagePaths.AddRange(_repository.GetAlbumImagePaths(service.Id));
            _albumImageEntries.Clear();
            _albumImageEntries.AddRange(_selectedAlbumImagePaths.Select(imagePath => new AlbumImageEntry(imagePath, isNew: false)));
            _albumImagesChanged = false;
            _txtServiceName.Text = service.Name;
            _txtPrice.Text = service.Price.ToString("0", CultureInfo.InvariantCulture);
            _txtImageName.Text = service.ImageFileName;
            _txtDescription.Text = service.Description;
            ShowImagePreview(service.ImagePath);
            RenderAlbumThumbnails();
            _btnAdd.Enabled = false;
            _btnUpdate.Enabled = true;
            _btnDelete.Enabled = true;
        }

        private bool TryGetServiceFromForm(out Service service)
        {
            service = new Service();
            if (string.IsNullOrWhiteSpace(_txtServiceName.Text))
            {
                ShowRequiredFieldMessage("tên dịch vụ", _txtServiceName);
                return false;
            }

            if (!TryParsePrice(out var price) || price < 0)
            {
                MessageBox.Show("Vui lòng nhập giá hợp lệ lớn hơn hoặc bằng 0.", "Giá chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPrice.Focus();
                return false;
            }

            service = new Service
            {
                Id = _selectedServiceId ?? 0,
                Name = _txtServiceName.Text,
                Price = price,
                Description = _txtDescription.Text,
                CreatedAt = _selectedServiceCreatedAt
            };
            return true;
        }

        private bool TryParsePrice(out decimal price)
        {
            var priceText = _txtPrice.Text.Trim();
            return decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.CurrentCulture, out price)
                || decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
        }

        private static void txtPrice_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9'))
            {
                e.Handled = true;
            }
        }

        private void txtPrice_TextChanged(object? sender, EventArgs e)
        {
            if (_normalizingPriceText)
            {
                return;
            }

            var numericText = new string(_txtPrice.Text.Where(character => character is >= '0' and <= '9').ToArray());
            if (_txtPrice.Text == numericText)
            {
                return;
            }

            _normalizingPriceText = true;
            try
            {
                _txtPrice.Text = numericText;
                _txtPrice.SelectionStart = numericText.Length;
            }
            finally
            {
                _normalizingPriceText = false;
            }
        }

        private string ResolveImagePath(string? copiedImagePath)
        {
            if (!string.IsNullOrWhiteSpace(copiedImagePath))
            {
                return copiedImagePath;
            }

            return _removeSelectedImage ? string.Empty : _selectedImagePath;
        }

        private string? CopyPendingImage()
        {
            if (string.IsNullOrWhiteSpace(_pendingImageSourcePath))
            {
                return null;
            }

            return CopyImageToManagedDirectory(_pendingImageSourcePath);
        }

        private IReadOnlyList<string> BuildAlbumImagePaths(ICollection<string> copiedImagePaths)
        {
            var imagePaths = new List<string>();
            foreach (var entry in _albumImageEntries)
            {
                if (entry.IsNew)
                {
                    var copiedImagePath = CopyImageToManagedDirectory(entry.ImagePath);
                    copiedImagePaths.Add(copiedImagePath);
                    imagePaths.Add(copiedImagePath);
                }
                else
                {
                    imagePaths.Add(entry.ImagePath);
                }
            }

            return imagePaths;
        }

        private static string CopyImageToManagedDirectory(string sourceImagePath)
        {
            if (!File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("Không tìm thấy hình ảnh đã chọn.", sourceImagePath);
            }

            var imageDirectory = GetServiceImageDirectory();
            var extension = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var destinationPath = Path.Combine(imageDirectory, $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
            File.Copy(sourceImagePath, destinationPath);
            return destinationPath;
        }

        private static string GetServiceImageDirectory()
        {
            var imageDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuanLyKhachHang",
                "ServiceImages");
            Directory.CreateDirectory(imageDirectory);
            return imageDirectory;
        }

        private static void DeleteManagedImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            try
            {
                var imageDirectory = Path.GetFullPath(GetServiceImageDirectory());
                var imageDirectoryPrefix = imageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var fullImagePath = Path.GetFullPath(imagePath);
                if (fullImagePath.StartsWith(imageDirectoryPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(fullImagePath))
                {
                    File.Delete(fullImagePath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void DeleteManagedImages(IEnumerable<string> imagePaths)
        {
            foreach (var imagePath in imagePaths)
            {
                DeleteManagedImage(imagePath);
            }
        }

        private void RenderAlbumThumbnails()
        {
            foreach (var control in _albumThumbnailPanel.Controls.OfType<Control>().ToArray())
            {
                control.Dispose();
            }

            _albumThumbnailPanel.Controls.Clear();
            foreach (var entry in _albumImageEntries)
            {
                _albumThumbnailPanel.Controls.Add(CreateAlbumThumbnail(entry));
            }

            _lblAlbumImageCount.Text = _albumImageEntries.Count == 0
                ? "Chưa có ảnh trong album"
                : $"Album có {_albumImageEntries.Count} ảnh";
        }

        private Control CreateAlbumThumbnail(AlbumImageEntry entry)
        {
            var thumbnailPanel = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(3),
                Size = new Size(76, 66)
            };
            var imagePreview = new PictureBox
            {
                BackColor = Color.FromArgb(247, 250, 249),
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            thumbnailPanel.Controls.Add(imagePreview);
            var previewImage = LoadImageCopy(entry.ImagePath);
            if (previewImage is not null)
            {
                imagePreview.Image = previewImage;
            }
            else
            {
                thumbnailPanel.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 7F, FontStyle.Italic, GraphicsUnit.Point),
                    ForeColor = Color.FromArgb(98, 108, 112),
                    Text = "Không xem được ảnh",
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }

            var removeButton = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(181, 63, 63),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 7F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Location = new Point(52, 3),
                Size = new Size(20, 20),
                Text = "X",
                UseVisualStyleBackColor = false
            };
            removeButton.FlatAppearance.BorderSize = 0;
            removeButton.Click += (_, _) => RemoveAlbumImage(entry);

            thumbnailPanel.Controls.Add(removeButton);
            removeButton.BringToFront();
            thumbnailPanel.Disposed += (_, _) => imagePreview.Image?.Dispose();
            return thumbnailPanel;
        }

        private void RemoveAlbumImage(AlbumImageEntry entry)
        {
            if (_albumImageEntries.Remove(entry))
            {
                _albumImagesChanged = true;
                RenderAlbumThumbnails();
            }
        }

        private static Image? LoadImageCopy(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return null;
            }

            try
            {
                using var sourceImage = Image.FromFile(imagePath);
                return new Bitmap(sourceImage);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ShowImagePreview(string? imagePath)
        {
            var previousImage = _pictureService.Image;
            _pictureService.Image = null;
            previousImage?.Dispose();

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                _imagePlaceholder.Text = "Chưa chọn ảnh đại diện";
                _imagePlaceholder.Visible = true;
                UpdateAvatarRemoveButtonVisibility();
                return;
            }

            try
            {
                using var sourceImage = Image.FromFile(imagePath);
                _pictureService.Image = new Bitmap(sourceImage);
                _imagePlaceholder.Visible = false;
            }
            catch (Exception)
            {
                _imagePlaceholder.Text = "Không thể xem ảnh";
                _imagePlaceholder.Visible = true;
            }

            UpdateAvatarRemoveButtonVisibility();
        }

        private void UpdateAvatarRemoveButtonVisibility()
        {
            _btnRemoveAvatar.Visible = !_removeSelectedImage
                && (!string.IsNullOrWhiteSpace(_pendingImageSourcePath) || !string.IsNullOrWhiteSpace(_selectedImagePath));
        }

        private static void ShowRequiredFieldMessage(string fieldName, Control control)
        {
            MessageBox.Show($"Vui lòng nhập {fieldName}.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            control.Focus();
        }

        private static void ShowSaveError(Exception exception)
        {
            MessageBox.Show($"Không thể lưu dịch vụ.\n{exception.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ClearForm(bool focusServiceName = true)
        {
            _selectedServiceId = null;
            _selectedServiceCreatedAt = default;
            _selectedImagePath = string.Empty;
            _pendingImageSourcePath = string.Empty;
            _removeSelectedImage = false;
            _selectedAlbumImagePaths.Clear();
            _albumImageEntries.Clear();
            _albumImagesChanged = false;
            _txtServiceName.Clear();
            _txtPrice.Clear();
            _txtImageName.Text = "Chưa chọn ảnh đại diện";
            _txtDescription.Clear();
            ShowImagePreview(null);
            RenderAlbumThumbnails();
            _btnAdd.Enabled = true;
            _btnUpdate.Enabled = false;
            _btnDelete.Enabled = false;

            _suppressSelectionChanged = true;
            try
            {
                _dgvServices.ClearSelection();
                _dgvServices.CurrentCell = null;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            if (focusServiceName)
            {
                _txtServiceName.Focus();
            }
        }
    }
}