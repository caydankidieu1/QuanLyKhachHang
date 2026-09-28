using System.Globalization;
using Microsoft.Data.Sqlite;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang.Data
{
    public sealed class ServiceRepository
    {
        private readonly string _connectionString;

        public ServiceRepository()
        {
            var applicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuanLyKhachHang");
            Directory.CreateDirectory(applicationDataPath);

            var databasePath = Path.Combine(applicationDataPath, "customers.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

            InitializeDatabase();
        }

        public IReadOnlyList<Service> GetAll(string? searchTerm = null)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                command.CommandText = """
                    SELECT s.Id,
                           s.Name,
                           s.ImagePath,
                           s.Price,
                           s.Description,
                           s.CreatedAt,
                           s.UpdatedAt,
                           (SELECT COUNT(*) FROM ServiceAlbumImages AS images WHERE images.ServiceId = s.Id) AS AlbumImageCount
                    FROM Services AS s
                    ORDER BY s.UpdatedAt DESC, s.Id DESC;
                    """;
            }
            else
            {
                command.CommandText = """
                    SELECT s.Id,
                           s.Name,
                           s.ImagePath,
                           s.Price,
                           s.Description,
                           s.CreatedAt,
                           s.UpdatedAt,
                           (SELECT COUNT(*) FROM ServiceAlbumImages AS images WHERE images.ServiceId = s.Id) AS AlbumImageCount
                    FROM Services AS s
                    WHERE s.Name LIKE $search
                       OR s.Description LIKE $search
                    ORDER BY s.UpdatedAt DESC, s.Id DESC;
                    """;
                command.Parameters.AddWithValue("$search", $"%{searchTerm.Trim()}%");
            }

            using var reader = command.ExecuteReader();
            var services = new List<Service>();
            while (reader.Read())
            {
                services.Add(ReadService(reader));
            }

            return services;
        }

        public long Add(Service service, IReadOnlyList<string> albumImagePaths)
        {
            var now = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Services (Name, ImagePath, Price, Description, CreatedAt, UpdatedAt)
                VALUES ($name, $imagePath, $price, $description, $createdAt, $updatedAt);
                """;
            AddServiceParameters(command, service, now, now);
            command.ExecuteNonQuery();

            using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var serviceId = Convert.ToInt64(idCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            InsertAlbumImages(connection, transaction, serviceId, albumImagePaths);
            transaction.Commit();
            return serviceId;
        }

        public void Add(Service service)
        {
            Add(service, Array.Empty<string>());
        }

        public void Update(Service service, IReadOnlyList<string>? albumImagePaths)
        {
            var now = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Services
                SET Name = $name,
                    ImagePath = $imagePath,
                    Price = $price,
                    Description = $description,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            AddServiceParameters(command, service, service.CreatedAt, now);
            command.Parameters.AddWithValue("$id", service.Id);
            command.ExecuteNonQuery();

            if (albumImagePaths is not null)
            {
                ReplaceAlbumImages(connection, transaction, service.Id, albumImagePaths);
            }

            transaction.Commit();
        }

        public void Update(Service service)
        {
            Update(service, null);
        }

        public IReadOnlyList<string> GetAlbumImagePaths(long serviceId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ImagePath
                FROM ServiceAlbumImages
                WHERE ServiceId = $serviceId
                ORDER BY SortOrder, Id;
                """;
            command.Parameters.AddWithValue("$serviceId", serviceId);

            using var reader = command.ExecuteReader();
            var imagePaths = new List<string>();
            while (reader.Read())
            {
                imagePaths.Add(reader.GetString(0));
            }

            return imagePaths;
        }

        public void Delete(long serviceId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            if (TableExists(connection, transaction, "ServicePackageItems"))
            {
                using var packageItemCommand = connection.CreateCommand();
                packageItemCommand.Transaction = transaction;
                packageItemCommand.CommandText = "DELETE FROM ServicePackageItems WHERE ServiceId = $id;";
                packageItemCommand.Parameters.AddWithValue("$id", serviceId);
                packageItemCommand.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM ServiceAlbumImages WHERE ServiceId = $id;";
            command.Parameters.AddWithValue("$id", serviceId);
            command.ExecuteNonQuery();
            command.CommandText = "DELETE FROM Services WHERE Id = $id;";
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        private static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string tableName)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT EXISTS(
                    SELECT 1
                    FROM sqlite_master
                    WHERE type = 'table' AND name = $tableName
                );
                """;
            command.Parameters.AddWithValue("$tableName", tableName);
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }

        private SqliteConnection CreateConnection() => new(_connectionString);

        private void InitializeDatabase()
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Services (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ImagePath TEXT NOT NULL DEFAULT '',
                    Price TEXT NOT NULL,
                    Description TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ServiceAlbumImages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ServiceId INTEGER NOT NULL,
                    ImagePath TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_ServiceAlbumImages_ServiceId_SortOrder
                ON ServiceAlbumImages (ServiceId, SortOrder);
                """;
            command.ExecuteNonQuery();
        }

        private static void ReplaceAlbumImages(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long serviceId,
            IReadOnlyList<string> albumImagePaths)
        {
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM ServiceAlbumImages WHERE ServiceId = $serviceId;";
            deleteCommand.Parameters.AddWithValue("$serviceId", serviceId);
            deleteCommand.ExecuteNonQuery();

            InsertAlbumImages(connection, transaction, serviceId, albumImagePaths);
        }

        private static void InsertAlbumImages(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long serviceId,
            IReadOnlyList<string> albumImagePaths)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ServiceAlbumImages (ServiceId, ImagePath, SortOrder)
                VALUES ($serviceId, $imagePath, $sortOrder);
                """;

            var sortOrder = 0;
            foreach (var imagePath in albumImagePaths)
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    continue;
                }

                command.Parameters.Clear();
                command.Parameters.AddWithValue("$serviceId", serviceId);
                command.Parameters.AddWithValue("$imagePath", imagePath);
                command.Parameters.AddWithValue("$sortOrder", sortOrder++);
                command.ExecuteNonQuery();
            }
        }

        private static void AddServiceParameters(
            SqliteCommand command,
            Service service,
            DateTime createdAt,
            DateTime updatedAt)
        {
            command.Parameters.AddWithValue("$name", service.Name.Trim());
            command.Parameters.AddWithValue("$imagePath", service.ImagePath);
            command.Parameters.AddWithValue("$price", service.Price.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$description", service.Description.Trim());
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        private static Service ReadService(SqliteDataReader reader)
        {
            return new Service
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                ImagePath = reader.GetString(2),
                Price = decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                Description = reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                AlbumImageCount = Convert.ToInt32(reader.GetInt64(7), CultureInfo.InvariantCulture)
            };
        }
    }
}