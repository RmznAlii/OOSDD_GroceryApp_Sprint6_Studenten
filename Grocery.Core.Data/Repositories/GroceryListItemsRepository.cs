using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grocery.Core.Data.Helpers;
using Grocery.Core.Interfaces.Repositories;
using Grocery.Core.Models;
using Microsoft.Data.Sqlite;

namespace Grocery.Core.Data.Repositories
{
    /// <summary>
    /// SQLite-repository voor GroceryListItem met FK-validatie en transacties.
    /// </summary>
    public class GroceryListItemsRepository : DatabaseConnection, IGroceryListItemsRepository
    {
        private readonly List<GroceryListItem> _cache = [];

        public GroceryListItemsRepository()
        {
            // Vangnet: zorg dat parent-tabellen bestaan (minimaal schema)
            CreateTable(@"
                CREATE TABLE IF NOT EXISTS Product(
                  [Id]    INTEGER PRIMARY KEY AUTOINCREMENT,
                  [Name]  TEXT NOT NULL,
                  [Stock] INTEGER NOT NULL DEFAULT 0
                )");
            CreateTable(@"
                CREATE TABLE IF NOT EXISTS GroceryList(
                  [Id]        INTEGER PRIMARY KEY AUTOINCREMENT,
                  [Name]      TEXT NOT NULL,
                  [CreatedOn] TEXT NULL,
                  [Color]     TEXT NULL,
                  [OwnerUserId] INTEGER NULL
                )");

            // Child tabel
            CreateTable(@"
                CREATE TABLE IF NOT EXISTS GroceryListItem (
                  [Id]            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                  [GroceryListId] INTEGER NOT NULL,
                  [ProductId]     INTEGER NOT NULL,
                  [Amount]        INTEGER NOT NULL,
                  FOREIGN KEY(GroceryListId) REFERENCES GroceryList(Id) ON DELETE CASCADE,
                  FOREIGN KEY(ProductId)     REFERENCES Product(Id)     ON DELETE CASCADE
                )");

            GetAll(); // warm de cache
        }

        // Zorgt dat FK's aan staan zodra er met de DB gewerkt wordt
        private void OpenConnectionWithFks()
        {
            OpenConnection();
            using var pragma = new SqliteCommand("PRAGMA foreign_keys = ON;", Connection);
            pragma.ExecuteNonQuery();
        }

        public List<GroceryListItem> GetAll()
        {
            _cache.Clear();

            OpenConnectionWithFks();
            try
            {
                const string sql = "SELECT Id, GroceryListId, ProductId, Amount FROM GroceryListItem";
                using var cmd = new SqliteCommand(sql, Connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _cache.Add(new GroceryListItem(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        reader.GetInt32(3)
                    ));
                }
                return _cache;
            }
            finally
            {
                CloseConnection();
            }
        }

        public List<GroceryListItem> GetAllOnGroceryListId(int listId)
        {
            var result = new List<GroceryListItem>();

            OpenConnectionWithFks();
            try
            {
                const string sql = @"SELECT Id, GroceryListId, ProductId, Amount
                                     FROM GroceryListItem
                                     WHERE GroceryListId = @ListId";
                using var cmd = new SqliteCommand(sql, Connection);
                cmd.Parameters.AddWithValue("@ListId", listId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new GroceryListItem(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        reader.GetInt32(3)
                    ));
                }
                return result;
            }
            finally
            {
                CloseConnection();
            }
        }

        public GroceryListItem? Delete(GroceryListItem item)
        {
            throw new NotImplementedException();
        }

        public GroceryListItem? Get(int id)
        {
            if (id <= 0) return null;

            OpenConnectionWithFks();
            try
            {
                const string sql = @"SELECT Id, GroceryListId, ProductId, Amount
                                     FROM GroceryListItem
                                     WHERE Id = @Id";
                using var cmd = new SqliteCommand(sql, Connection);
                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                return new GroceryListItem(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3)
                );
            }
            finally
            {
                CloseConnection();
            }
        }

        public GroceryListItem? Add(GroceryListItem item)
        {
            // Validatie – voorkomt crashes
            if (item is null) return null;
            if (item.GroceryListId <= 0 || item.ProductId <= 0 || item.Amount <= 0) return null;

            OpenConnectionWithFks();
            using var tx = Connection.BeginTransaction();
            try
            {
                // FK-bestaanscheck (duidelijke fout i.p.v. cryptische constraint)
                using (var chkList = new SqliteCommand("SELECT 1 FROM GroceryList WHERE Id=@Id LIMIT 1;", Connection, tx))
                {
                    chkList.Parameters.AddWithValue("@Id", item.GroceryListId);
                    if (chkList.ExecuteScalar() is null)
                    {
                        tx.Rollback();
                        return null;
                    }
                }
                using (var chkProd = new SqliteCommand("SELECT 1 FROM Product WHERE Id=@Id LIMIT 1;", Connection, tx))
                {
                    chkProd.Parameters.AddWithValue("@Id", item.ProductId);
                    if (chkProd.ExecuteScalar() is null)
                    {
                        tx.Rollback();
                        return null;
                    }
                }

                // Insert
                using (var insert = new SqliteCommand(
                    "INSERT INTO GroceryListItem (GroceryListId, ProductId, Amount) VALUES (@List, @Prod, @Amt);",
                    Connection, tx))
                {
                    insert.Parameters.AddWithValue("@List", item.GroceryListId);
                    insert.Parameters.AddWithValue("@Prod", item.ProductId);
                    insert.Parameters.AddWithValue("@Amt", item.Amount);
                    insert.ExecuteNonQuery();
                }

                // Id ophalen – betrouwbaar op mobile
                using (var getId = new SqliteCommand("SELECT last_insert_rowid();", Connection, tx))
                {
                    var newId = (long)(getId.ExecuteScalar() ?? 0L);
                    item.Id = (int)newId;
                }

                tx.Commit();
                return item;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GroceryListItemsRepository.Add error: " + ex);
                try { tx.Rollback(); } catch { }
                return null;
            }
            finally
            {
                CloseConnection();
            }
        }

        public GroceryListItem? Update(GroceryListItem item)
        {
            if (item is null || item.Id <= 0) return null;
            if (item.GroceryListId <= 0 || item.ProductId <= 0 || item.Amount <= 0) return null;

            OpenConnectionWithFks();
            using var tx = Connection.BeginTransaction();
            try
            {
                const string sql = @"UPDATE GroceryListItem
                                     SET GroceryListId = @List,
                                         ProductId     = @Prod,
                                         Amount        = @Amt
                                     WHERE Id = @Id";
                using var cmd = new SqliteCommand(sql, Connection, tx);
                cmd.Parameters.AddWithValue("@Id", item.Id);
                cmd.Parameters.AddWithValue("@List", item.GroceryListId);
                cmd.Parameters.AddWithValue("@Prod", item.ProductId);
                cmd.Parameters.AddWithValue("@Amt", item.Amount);

                int rows = cmd.ExecuteNonQuery();
                tx.Commit();
                return rows > 0 ? item : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GroceryListItemsRepository.Update error: " + ex);
                try { tx.Rollback(); } catch { }
                return null;
            }
            finally
            {
                CloseConnection();
            }
        }

        public bool Delete(int id)
        {
            if (id <= 0) return false;

            OpenConnectionWithFks();
            try
            {
                const string sql = "DELETE FROM GroceryListItem WHERE Id = @Id;";
                using var cmd = new SqliteCommand(sql, Connection);
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            finally
            {
                CloseConnection();
            }
        }
    }
}
