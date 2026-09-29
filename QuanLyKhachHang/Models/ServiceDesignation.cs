namespace QuanLyKhachHang.Models
{
    public sealed class ServiceDesignation
    {
        public long Id { get; set; }

        public long CustomerId { get; set; }

        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string SelectedItems { get; set; } = string.Empty;
    }
}