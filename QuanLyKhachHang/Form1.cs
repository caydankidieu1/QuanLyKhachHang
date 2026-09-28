using QuanLyKhachHang.Data;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang
{
    public partial class Form1 : Form
    {
        private readonly CustomerRepository _repository;
        private long? _selectedCustomerId;
        private DateTime _selectedCustomerCreatedAt;
        private string _selectedCustomerCode = string.Empty;
        private bool _suppressSelectionChanged;

        public Form1()
        {
            InitializeComponent();
            _repository = new CustomerRepository();
            ConfigureCustomerGrid();
            LoadCustomers();
            ClearForm();
        }

        private void ConfigureCustomerGrid()
        {
            dgvCustomers.AutoGenerateColumns = false;
            dgvCustomers.Columns.Add(CreateTextColumn("CustomerCode", "Mã khách hàng", 18));
            dgvCustomers.Columns.Add(CreateTextColumn("FullName", "Họ và tên", 25));
            dgvCustomers.Columns.Add(CreateTextColumn("Phone", "Số điện thoại", 18));
            dgvCustomers.Columns.Add(CreateTextColumn("Email", "Email", 25));
            dgvCustomers.Columns.Add(CreateTextColumn("Address", "Địa chỉ", 28));
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "UpdatedAt",
                HeaderText = "Cập nhật",
                FillWeight = 18,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" }
            });
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string headerText, float fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                FillWeight = fillWeight
            };
        }

        private void LoadCustomers()
        {
            _suppressSelectionChanged = true;
            try
            {
                var customers = _repository.GetAll(txtSearch.Text).ToList();
                dgvCustomers.DataSource = customers;
                dgvCustomers.ClearSelection();
                dgvCustomers.CurrentCell = null;
                lblCustomerCount.Text = $"Tổng cộng: {customers.Count} khách hàng";
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private void btnAdd_Click(object? sender, EventArgs e)
        {
            if (!TryGetCustomerFromForm(out var customer))
            {
                return;
            }

            _repository.Add(customer);
            LoadCustomers();
            ClearForm();
            MessageBox.Show("Đã thêm khách hàng.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnUpdate_Click(object? sender, EventArgs e)
        {
            if (_selectedCustomerId is null)
            {
                MessageBox.Show("Hãy chọn một khách hàng trong danh sách để cập nhật.", "Chưa chọn khách hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryGetCustomerFromForm(out var customer))
            {
                return;
            }

            _repository.Update(customer);
            LoadCustomers();
            ClearForm();
            MessageBox.Show("Đã cập nhật thông tin khách hàng.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedCustomerId is null)
            {
                MessageBox.Show("Hãy chọn một khách hàng trong danh sách để xóa.", "Chưa chọn khách hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Bạn có chắc muốn xóa khách hàng '{txtFullName.Text}'?",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            _repository.Delete(_selectedCustomerId.Value);
            LoadCustomers();
            ClearForm();
            MessageBox.Show("Đã xóa khách hàng.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnClear_Click(object? sender, EventArgs e)
        {
            ClearForm();
        }

        private void txtSearch_TextChanged(object? sender, EventArgs e)
        {
            LoadCustomers();
            ClearForm(focusFullName: false);
        }

        private void dgvCustomers_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressSelectionChanged || dgvCustomers.CurrentRow?.DataBoundItem is not Customer customer)
            {
                return;
            }

            _selectedCustomerId = customer.Id;
            _selectedCustomerCreatedAt = customer.CreatedAt;
            _selectedCustomerCode = customer.CustomerCode;
            txtCustomerCode.Text = customer.CustomerCode;
            txtFullName.Text = customer.FullName;
            txtPhone.Text = customer.Phone;
            txtEmail.Text = customer.Email;
            txtAddress.Text = customer.Address;
            txtNotes.Text = customer.Notes;
            btnAdd.Enabled = false;
            btnUpdate.Enabled = true;
            btnDelete.Enabled = true;
        }

        private bool TryGetCustomerFromForm(out Customer customer)
        {
            customer = new Customer();
            if (string.IsNullOrWhiteSpace(txtFullName.Text))
            {
                ShowRequiredFieldMessage("Họ và tên", txtFullName);
                return false;
            }

            customer = new Customer
            {
                Id = _selectedCustomerId ?? 0,
                CustomerCode = _selectedCustomerCode,
                FullName = txtFullName.Text,
                Phone = txtPhone.Text,
                Email = txtEmail.Text,
                Address = txtAddress.Text,
                Notes = txtNotes.Text,
                CreatedAt = _selectedCustomerCreatedAt
            };
            return true;
        }

        private static void ShowRequiredFieldMessage(string fieldName, Control control)
        {
            MessageBox.Show($"Vui lòng nhập {fieldName.ToLowerInvariant()}.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            control.Focus();
        }

        private void ClearForm(bool focusFullName = true)
        {
            _selectedCustomerId = null;
            _selectedCustomerCreatedAt = default;
            _selectedCustomerCode = string.Empty;
            txtCustomerCode.Text = _repository.GetNextCustomerCode();
            txtFullName.Clear();
            txtPhone.Clear();
            txtEmail.Clear();
            txtAddress.Clear();
            txtNotes.Clear();
            btnAdd.Enabled = true;
            btnUpdate.Enabled = false;
            btnDelete.Enabled = false;

            _suppressSelectionChanged = true;
            try
            {
                dgvCustomers.ClearSelection();
                dgvCustomers.CurrentCell = null;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            if (focusFullName)
            {
                txtFullName.Focus();
            }
        }
    }
}
