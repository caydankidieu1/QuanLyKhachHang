using System.Globalization;
using Microsoft.Data.Sqlite;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang.Data
{
    public sealed class ServiceDesignationRepository
    {
        private readonly string _connectionString;

        public ServiceDesignationRepository()
        {
            var applicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuanLyKhachHang");
            Directory.CreateDirectory(applicationDataPath);

            var databasePath = Path.Combine(applicationDataPath, "customers.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

            InitializeDatabase();
        }

        public IReadOnlyList<ServiceDesignation> GetAllForCustomer(long customerId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT d.Id,
                       d.CustomerId,
                       d.Notes,
                       d.CreatedAt,
                       COALESCE(GROUP_CONCAT(
                           CASE
                               WHEN items.ServiceId IS NOT NULL THEN 'Dịch vụ: ' || services.Name
                               ELSE 'Gói: ' || packages.IdentifierName
                           END,
                           ' | '), '') AS SelectedItems
                FROM ServiceDesignations AS d
                LEFT JOIN ServiceDesignationItems AS items ON items.DesignationId = d.Id
                LEFT JOIN Services AS services ON services.Id = items.ServiceId
                LEFT JOIN ServicePackages AS packages ON packages.Id = items.ServicePackageId
                WHERE d.CustomerId = $customerId
                GROUP BY d.Id, d.CustomerId, d.Notes, d.CreatedAt
                ORDER BY d.CreatedAt DESC, d.Id DESC;
                """;
            command.Parameters.AddWithValue("$customerId", customerId);

            using var reader = command.ExecuteReader();
            var designations = new List<ServiceDesignation>();
            while (reader.Read())
            {
                designations.Add(new ServiceDesignation
                {
                    Id = reader.GetInt64(0),
                    CustomerId = reader.GetInt64(1),
                    Notes = reader.GetString(2),
                    CreatedAt = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    SelectedItems = reader.GetString(4)
                });
            }

            return designations;
        }

        public (IReadOnlySet<long> ServiceIds, IReadOnlySet<long> ServicePackageIds) GetSelectedItemIds(long designationId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ServiceId, ServicePackageId
                FROM ServiceDesignationItems
                WHERE DesignationId = $designationId;
                """;
            command.Parameters.AddWithValue("$designationId", designationId);

            var serviceIds = new HashSet<long>();
            var servicePackageIds = new HashSet<long>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    serviceIds.Add(reader.GetInt64(0));
                }

                if (!reader.IsDBNull(1))
                {
                    servicePackageIds.Add(reader.GetInt64(1));
                }
            }

            return (serviceIds, servicePackageIds);
        }

        public long Add(
            ServiceDesignation designation,
            IReadOnlyCollection<long> serviceIds,
            IReadOnlyCollection<long> servicePackageIds)
        {
            if (designation.CustomerId <= 0)
            {
                throw new ArgumentException("Khách hàng không hợp lệ.", nameof(designation));
            }

            var distinctServiceIds = serviceIds.Where(id => id > 0).Distinct().ToList();
            var distinctServicePackageIds = servicePackageIds.Where(id => id > 0).Distinct().ToList();
            if (distinctServiceIds.Count == 0 && distinctServicePackageIds.Count == 0)
            {
                throw new ArgumentException("Phiếu chỉ định cần có ít nhất một dịch vụ hoặc gói dịch vụ.", nameof(designation));
            }

            var createdAt = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ServiceDesignations (CustomerId, Notes, CreatedAt)
                VALUES ($customerId, $notes, $createdAt);
                """;
            command.Parameters.AddWithValue("$customerId", designation.CustomerId);
            command.Parameters.AddWithValue("$notes", designation.Notes.Trim());
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();

            using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var designationId = Convert.ToInt64(idCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            InsertItems(connection, transaction, designationId, distinctServiceIds, distinctServicePackageIds);
            transaction.Commit();
            return designationId;
        }

        public bool Delete(long designationId)
        {
            if (designationId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(designationId));
            }

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM ServiceDesignationItems WHERE DesignationId = $designationId;";
            command.Parameters.AddWithValue("$designationId", designationId);
            command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM ServiceDesignations WHERE Id = $designationId;";
            var wasDeleted = command.ExecuteNonQuery() > 0;
            transaction.Commit();
            return wasDeleted;
        }

        private SqliteConnection CreateConnection() => new(_connectionString);

        private void InitializeDatabase()
        {
            using var connection = CreateConnection();
            connection.Open();

            if (TableHasColumn(connection, "ServiceDesignations", "ServiceId")
                || TableHasColumn(connection, "ServiceDesignations", "ServicePackageId"))
            {
                MigrateLegacySchema(connection);
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS ServiceDesignations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NOT NULL,
                    Notes TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ServiceDesignationItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DesignationId INTEGER NOT NULL,
                    ServiceId INTEGER NULL,
                    ServicePackageId INTEGER NULL,
                    CHECK (
                        (ServiceId IS NOT NULL AND ServicePackageId IS NULL)
                        OR (ServiceId IS NULL AND ServicePackageId IS NOT NULL)
                    )
                );

                CREATE INDEX IF NOT EXISTS IX_ServiceDesignations_CustomerId_CreatedAt
                ON ServiceDesignations (CustomerId, CreatedAt DESC);

                CREATE INDEX IF NOT EXISTS IX_ServiceDesignationItems_DesignationId
                ON ServiceDesignationItems (DesignationId);
                """;
            command.ExecuteNonQuery();
        }

        private static void InsertItems(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long designationId,
            IReadOnlyCollection<long> serviceIds,
            IReadOnlyCollection<long> servicePackageIds)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ServiceDesignationItems (DesignationId, ServiceId, ServicePackageId)
                VALUES ($designationId, $serviceId, $servicePackageId);
                """;

            foreach (var serviceId in serviceIds)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$designationId", designationId);
                command.Parameters.AddWithValue("$serviceId", serviceId);
                command.Parameters.AddWithValue("$servicePackageId", DBNull.Value);
                command.ExecuteNonQuery();
            }

            foreach (var servicePackageId in servicePackageIds)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$designationId", designationId);
                command.Parameters.AddWithValue("$serviceId", DBNull.Value);
                command.Parameters.AddWithValue("$servicePackageId", servicePackageId);
                command.ExecuteNonQuery();
            }
        }

        private static bool TableHasColumn(SqliteConnection connection, string tableName, string columnName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void MigrateLegacySchema(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                ALTER TABLE ServiceDesignations RENAME TO ServiceDesignations_Legacy;

                DROP INDEX IF EXISTS IX_ServiceDesignations_CustomerId_CreatedAt;

                CREATE TABLE ServiceDesignations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NOT NULL,
                    Notes TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ServiceDesignationItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DesignationId INTEGER NOT NULL,
                    ServiceId INTEGER NULL,
                    ServicePackageId INTEGER NULL,
                    CHECK (
                        (ServiceId IS NOT NULL AND ServicePackageId IS NULL)
                        OR (ServiceId IS NULL AND ServicePackageId IS NOT NULL)
                    )
                );

                INSERT INTO ServiceDesignations (Id, CustomerId, Notes, CreatedAt)
                SELECT Id, CustomerId, Notes, CreatedAt
                FROM ServiceDesignations_Legacy;

                INSERT INTO ServiceDesignationItems (DesignationId, ServiceId, ServicePackageId)
                SELECT Id, ServiceId, NULL
                FROM ServiceDesignations_Legacy
                WHERE ServiceId IS NOT NULL;

                INSERT INTO ServiceDesignationItems (DesignationId, ServiceId, ServicePackageId)
                SELECT Id, NULL, ServicePackageId
                FROM ServiceDesignations_Legacy
                WHERE ServicePackageId IS NOT NULL;

                DROP TABLE ServiceDesignations_Legacy;
                """;
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }
}