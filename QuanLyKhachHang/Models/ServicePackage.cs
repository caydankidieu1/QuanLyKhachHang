namespace QuanLyKhachHang.Models
{
    public sealed class ServicePackage
    {
        public long Id { get; set; }

        public string IdentifierName { get; set; } = string.Empty;

        public decimal PackagePrice { get; set; }

        public decimal DiscountPercent { get; set; }

        public int ServiceCount { get; set; }

        public string ServiceNames { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}