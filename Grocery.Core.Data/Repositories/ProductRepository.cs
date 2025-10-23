using System;
using System.Collections.Generic;
using System.Linq;
using Grocery.Core.Interfaces.Repositories;
using Grocery.Core.Models;
using Microsoft.Data.Sqlite;

namespace Grocery.Core.Data.Repositories
{
    /// <summary>
    /// Repository voor het beheren van producten in de SQLite-database.
    /// Verzorgt CRUD-operaties en seedt voorbeelddata bij de eerste start.
    /// </summary>
    public class ProductRepository : DatabaseConnection, IProductRepository
    {
        private readonly List<Product> _cache = [];

        /// <summary>
        /// Initialiseert de repository, maakt de tabel aan en seedt basisdata.
        /// </summary>
        public ProductRepository()
        {
            CreateTable(@"
                CREATE TABLE IF NOT EXISTS Product (
                    [Id]     INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    [Name]   TEXT NOT NULL,
                    [Stock]  INTEGER NOT NULL,
                    [Date]   TEXT NOT NULL,
                    [Price]  REAL NOT NULL
                )");

            List<string> seedData =
            [
                "INSERT OR IGNORE INTO Product(Id, Name, Stock, Date, Price) VALUES(1, 'Melk', 300, '2025-09-25', 0.95)",
                "INSERT OR IGNORE INTO Product(Id, Name, Stock, Date, Price) VALUES(2, 'Kaas', 100, '2025-09-30', 7.98)",
                "INSERT OR IGNORE INTO Product(Id, Name, Stock, Date, Price) VALUES(3, 'Brood', 400, '2025-09-12', 2.19)",
                "INSERT OR IGNORE INTO Product(Id, Name, Stock, Date, Price) VALUES(4, 'Cornflakes', 0, '2025-12-31', 1.48)"
            ];

            InsertMultipleWithTransaction(seedData);
            GetAll(); // cache vullen
        }

        /// <summary>
        /// Haalt alle producten op uit de database en ververst de lokale cache.
        /// </summary>
        public List<Product> GetAll()
        {
            _cache.Clear();
            const string sql = "SELECT Id, Name, Stock, Date, Price FROM Product";

            OpenConnection();
            using (var cmd = new SqliteCommand(sql, Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    int stock = reader.GetInt32(2);
                    var date = DateOnly.Parse(reader.GetString(3));
                    decimal price = Convert.ToDecimal(reader.GetDouble(4));

                    _cache.Add(new Product(id, name, stock, date, price));
                }
            }
            CloseConnection();
            return _cache;
        }

        /// <summary>
        /// Haalt één product op aan de hand van zijn Id.
        /// </summary>
        public Product? Get(int id)
        {
            if (id <= 0) return null;
            const string sql = "SELECT Id, Name, Stock, Date, Price FROM Product WHERE Id = @Id";

            OpenConnection();
            Product? product = null;
            using (var cmd = new SqliteCommand(sql, Connection))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int productId = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    int stock = reader.GetInt32(2);
                    var date = DateOnly.Parse(reader.GetString(3));
                    decimal price = Convert.ToDecimal(reader.GetDouble(4));

                    product = new Product(productId, name, stock, date, price);
                }
            }
            CloseConnection();
            return product;
        }

        /// <summary>
        /// Voegt een nieuw product toe aan de database en geeft het terug met Id.
        /// </summary>
        public Product Add(Product item)
        {
            const string sql = @"INSERT INTO Product (Name, Stock, Date, Price)
                                 VALUES (@Name, @Stock, @Date, @Price);
                                 SELECT last_insert_rowid();";

            OpenConnection();
            using (var cmd = new SqliteCommand(sql, Connection))
            {
                cmd.Parameters.AddWithValue("@Name", item.Name);
                cmd.Parameters.AddWithValue("@Stock", item.Stock);
                cmd.Parameters.AddWithValue("@Date", item.ShelfLife.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Price", item.Price);

                item.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }
            CloseConnection();

            _cache.Add(item);
            return item;
        }

        /// <summary>
        /// Verwijdert een product op basis van zijn Id.
        /// </summary>
        public Product? Delete(Product item)
        {
            if (item.Id <= 0) return null;

            const string sql = "DELETE FROM Product WHERE Id = @Id;";
            OpenConnection();
            using (var cmd = new SqliteCommand(sql, Connection))
            {
                cmd.Parameters.AddWithValue("@Id", item.Id);
                int rows = cmd.ExecuteNonQuery();
                if (rows > 0)
                {
                    _cache.RemoveAll(p => p.Id == item.Id);
                    CloseConnection();
                    return item;
                }
            }
            CloseConnection();
            return null;
        }

        /// <summary>
        /// Werkt een bestaand product bij in de database en in de cache.
        /// </summary>
        public Product? Update(Product item)
        {
            if (item.Id <= 0) return null;

            const string sql = @"UPDATE Product 
                                 SET Name=@Name, Stock=@Stock, Date=@Date, Price=@Price
                                 WHERE Id=@Id;";

            OpenConnection();
            using (var cmd = new SqliteCommand(sql, Connection))
            {
                cmd.Parameters.AddWithValue("@Name", item.Name);
                cmd.Parameters.AddWithValue("@Stock", item.Stock);
                cmd.Parameters.AddWithValue("@Date", item.ShelfLife.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Price", item.Price);
                cmd.Parameters.AddWithValue("@Id", item.Id);

                cmd.ExecuteNonQuery();
            }
            CloseConnection();

            // cache bijwerken
            var cached = _cache.FirstOrDefault(p => p.Id == item.Id);
            if (cached != null)
            {
                cached.Name = item.Name;
                cached.Stock = item.Stock;
                cached.ShelfLife = item.ShelfLife;
                cached.Price = item.Price;
            }

            return item;
        }
    }
}
