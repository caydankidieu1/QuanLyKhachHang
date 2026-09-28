using System.Globalization;
using Microsoft.Data.Sqlite;
using QuanLyKhachHang.Models;

namespace QuanLyKhachHang.Data
{
    public sealed class CustomerRepository
    {
        private readonly string _connectionString;

        public CustomerRepository()
        {
            var applicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuanLyKhachHang");
            Directory.CreateDirectory(applicationDataPath);

            var databasePath = Path.Combine(applicationDataPath, "customers.db");
            _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

            InitializeDatabase();
        }

        public IReadOnlyList<Customer> GetAll(string? searchTerm = null)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                command.CommandText = """
                    SELECT Id, CustomerCode, FullName, Phone, Email, Address, Notes, CreatedAt, UpdatedAt
                    FROM Customers
                    ORDER BY UpdatedAt DESC, Id DESC;
                    """;
            }
            else
            {
                command.CommandText = """
                    SELECT Id, CustomerCode, FullName, Phone, Email, Address, Notes, CreatedAt, UpdatedAt
                    FROM Customers
                    WHERE CustomerCode LIKE $search
                       OR FullName LIKE $search
                       OR Phone LIKE $search
                       OR Email LIKE $search
                    ORDER BY UpdatedAt DESC, Id DESC;
                    """;
                command.Parameters.AddWithValue("$search", $"%{searchTerm.Trim()}%");
            }

            using var reader = command.ExecuteReader();
            var customers = new List<Customer>();
            while (reader.Read())
            {
                customers.Add(ReadCustomer(reader));
            }

            return customers;
        }

        public string GetNextCustomerCode()
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COALESCE(
                    (SELECT seq FROM sqlite_sequence WHERE name = 'Customers'),
                    0
                ) + 1;
                """;

            var nextCustomerId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return FormatCustomerCode(nextCustomerId);
        }

        public void Add(Customer customer)
        {
            var now = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Customers (CustomerCode, FullName, Phone, Email, Address, Notes, CreatedAt, UpdatedAt)
                VALUES (
                    'KH' || printf('%05d', COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'Customers'), 0) + 1),
                    $fullName,
                    $phone,
                    $email,
                    $address,
                    $notes,
                    $createdAt,
                    $updatedAt
                );
                """;
            AddCustomerParameters(command, customer, now, now);
            command.ExecuteNonQuery();
        }

        public void Update(Customer customer)
        {
            var now = DateTime.UtcNow;

            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Customers
                SET FullName = $fullName,
                    Phone = $phone,
                    Email = $email,
                    Address = $address,
                    Notes = $notes,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;
            AddCustomerParameters(command, customer, customer.CreatedAt, now);
            command.Parameters.AddWithValue("$id", customer.Id);
            command.ExecuteNonQuery();
        }

        public void Delete(long customerId)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Customers WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", customerId);
            command.ExecuteNonQuery();
        }

        private static string FormatCustomerCode(long customerId) => $"KH{customerId:D5}";

        private SqliteConnection CreateConnection() => new(_connectionString);

        private void InitializeDatabase()
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Customers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerCode TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    FullName TEXT NOT NULL,
                    Phone TEXT NOT NULL DEFAULT '',
                    Email TEXT NOT NULL DEFAULT '',
                    Address TEXT NOT NULL DEFAULT '',
                    Notes TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        private static void AddCustomerParameters(
            SqliteCommand command,
            Customer customer,
            DateTime createdAt,
            DateTime updatedAt)
        {
            command.Parameters.AddWithValue("$fullName", customer.FullName.Trim());
            command.Parameters.AddWithValue("$phone", customer.Phone.Trim());
            command.Parameters.AddWithValue("$email", customer.Email.Trim());
            command.Parameters.AddWithValue("$address", customer.Address.Trim());
            command.Parameters.AddWithValue("$notes", customer.Notes.Trim());
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        private static Customer ReadCustomer(SqliteDataReader reader)
        {
            return new Customer
            {
                Id = reader.GetInt64(0),
                CustomerCode = reader.GetString(1),
                FullName = reader.GetString(2),
                Phone = reader.GetString(3),
                Email = reader.GetString(4),
                Address = reader.GetString(5),
                Notes = reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            };
        }
    }
}