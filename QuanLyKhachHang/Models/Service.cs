using System.IO;

namespace QuanLyKhachHang.Models
{
    public sealed class Service
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ImagePath { get; set; } = string.Empty;

        public int AlbumImageCount { get; set; }

        public decimal Price { get; set; }

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string ImageFileName => string.IsNullOrWhiteSpace(ImagePath)
            ? "Chưa có ảnh"
            : Path.GetFileName(ImagePath);
    }
}