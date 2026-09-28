using System.Globalization;
using Microsoft.Data.Sqlite;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang.Data
{
    public sealed class ServicePackageRepository
    {
        private readonly string _connectionString;

        public ServicePackageRepository()
        {
            var applicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuanLyKhachHang");
            Directory.CreateDirectory(applicationDataPath);

            var databasePath = Path.Combine(applicationDataPath, "customers.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

            InitializeDatabase();
        }

        public IReadOnlyList<ServicePackage> GetAll(string? searchTerm = null)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT p.Id,
                       p.IdentifierName,
                       p.PackagePrice,
                       p.DiscountPercent,
                       p.CreatedAt,
                       p.UpdatedAt,
                       COUNT(s.Id) AS ServiceCount,
                       COALESCE(GROUP_CONCAT(s.Name, ', '), '') AS ServiceNames
                FROM ServicePackages AS p
                LEFT JOIN ServicePackageItems AS items ON items.PackageId = p.Id
                LEFT JOIN Services AS s ON s.Id = items.ServiceId
                WHERE $search = ''
                   OR p.IdentifierName LIKE $searchPattern
                   OR EXISTS (
                       SELECT 1
                       FROM ServicePackageItems AS matchingItems
                       INNER JOIN Services AS matchingServices ON matchingServices.Id = matchingItems.ServiceId
                       WHERE matchingItems.PackageId = p.Id
                         AND matchingServices.Name LIKE $searchPattern
                   )
                GROUP BY p.Id, p.IdentifierName, p.PackagePrice, p.DiscountPercent, p.CreatedAt, p.UpdatedAt
                ORDER BY p.UpdatedAt DESC, p.Id DESC;
                """;

            var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
            command.Parameters.AddWithValue("$search", normalizedSearchTerm);
            command.Parameters.AddWithValue("$searchPattern", $"%{normalizedSearchTerm}%");

            using var reader = command.ExecuteReader();
            var packages = new List<ServicePackage>();
            while (reader.Read())
            {
                packages.Add(ReadServicePackage(reader));
            }

            return packages;
        }

        public IReadOnlyList<long> GetServiceIds(long packageId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ServiceId
                FROM ServicePackageItems
                WHERE PackageId = $packageId
                ORDER BY SortOrder, Id;
                """;
            command.Parameters.AddWithValue("$packageId", packageId);

            using var reader = command.ExecuteReader();
            var serviceIds = new List<long>();
            while (reader.Read())
            {
                serviceIds.Add(reader.GetInt64(0));
            }

            return serviceIds;
        }

        public void Add(ServicePackage servicePackage, IReadOnlyCollection<long> serviceIds)
        {
            var now = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ServicePackages (IdentifierName, PackagePrice, DiscountPercent, CreatedAt, UpdatedAt)
                VALUES ($identifierName, $packagePrice, $discountPercent, $createdAt, $updatedAt);
                """;
            AddPackageParameters(command, servicePackage, now, now);
            command.ExecuteNonQuery();

            using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var packageId = Convert.ToInt64(idCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            InsertPackageItems(connection, transaction, packageId, serviceIds);
            transaction.Commit();
        }

        public void Update(ServicePackage servicePackage, IReadOnlyCollection<long> serviceIds)
        {
            var now = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE ServicePackages
                SET IdentifierName = $identifierName,
                    PackagePrice = $packagePrice,
                    DiscountPercent = $discountPercent,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            AddPackageParameters(command, servicePackage, servicePackage.CreatedAt, now);
            command.Parameters.AddWithValue("$id", servicePackage.Id);
            command.ExecuteNonQuery();

            ReplacePackageItems(connection, transaction, servicePackage.Id, serviceIds);
            transaction.Commit();
        }

        public void Delete(long packageId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM ServicePackageItems WHERE PackageId = $id;";
            command.Parameters.AddWithValue("$id", packageId);
            command.ExecuteNonQuery();
            command.CommandText = "DELETE FROM ServicePackages WHERE Id = $id;";
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        private SqliteConnection CreateConnection() => new(_connectionString);

        private void InitializeDatabase()
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS ServicePackages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IdentifierName TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    PackagePrice TEXT NOT NULL,
                    DiscountPercent TEXT NOT NULL DEFAULT '0',
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ServicePackageItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PackageId INTEGER NOT NULL,
                    ServiceId INTEGER NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    UNIQUE (PackageId, ServiceId)
                );

                CREATE INDEX IF NOT EXISTS IX_ServicePackageItems_PackageId_SortOrder
                ON ServicePackageItems (PackageId, SortOrder);

                CREATE INDEX IF NOT EXISTS IX_ServicePackageItems_ServiceId
                ON ServicePackageItems (ServiceId);
                """;
            command.ExecuteNonQuery();
            EnsureDiscountPercentColumn(connection);
        }

        private static void EnsureDiscountPercentColumn(SqliteConnection connection)
        {
            using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText = "PRAGMA table_info(ServicePackages);";
            var hasDiscountPercentColumn = false;
            using (var reader = columnCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "DiscountPercent", StringComparison.OrdinalIgnoreCase))
                    {
                        hasDiscountPercentColumn = true;
                        break;
                    }
                }
            }

            if (hasDiscountPercentColumn)
            {
                return;
            }

            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE ServicePackages ADD COLUMN DiscountPercent TEXT NOT NULL DEFAULT '0';";
            alterCommand.ExecuteNonQuery();
        }

        private static void ReplacePackageItems(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long packageId,
            IReadOnlyCollection<long> serviceIds)
        {
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM ServicePackageItems WHERE PackageId = $packageId;";
            deleteCommand.Parameters.AddWithValue("$packageId", packageId);
            deleteCommand.ExecuteNonQuery();

            InsertPackageItems(connection, transaction, packageId, serviceIds);
        }

        private static void InsertPackageItems(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long packageId,
            IReadOnlyCollection<long> serviceIds)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ServicePackageItems (PackageId, ServiceId, SortOrder)
                VALUES ($packageId, $serviceId, $sortOrder);
                """;

            var sortOrder = 0;
            foreach (var serviceId in serviceIds.Distinct())
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$packageId", packageId);
                command.Parameters.AddWithValue("$serviceId", serviceId);
                command.Parameters.AddWithValue("$sortOrder", sortOrder++);
                command.ExecuteNonQuery();
            }
        }

        private static void AddPackageParameters(
            SqliteCommand command,
            ServicePackage servicePackage,
            DateTime createdAt,
            DateTime updatedAt)
        {
            command.Parameters.AddWithValue("$identifierName", servicePackage.IdentifierName.Trim());
            command.Parameters.AddWithValue("$packagePrice", servicePackage.PackagePrice.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$discountPercent", servicePackage.DiscountPercent.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        private static ServicePackage ReadServicePackage(SqliteDataReader reader)
        {
            return new ServicePackage
            {
                Id = reader.GetInt64(0),
                IdentifierName = reader.GetString(1),
                PackagePrice = decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                DiscountPercent = decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                CreatedAt = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                ServiceCount = Convert.ToInt32(reader.GetInt64(6), CultureInfo.InvariantCulture),
                ServiceNames = reader.GetString(7)
            };
        }
    }
}