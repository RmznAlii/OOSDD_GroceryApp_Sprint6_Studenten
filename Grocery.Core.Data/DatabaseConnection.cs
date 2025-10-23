using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
#if IOS
using Microsoft.Maui.Storage;
#endif

namespace Grocery.Core.Data
{
    public abstract class DatabaseConnection : IDisposable
    {
        protected SqliteConnection Connection { get; }

        private readonly string _dbPath;

        public DatabaseConnection()
        {
            // Haal bestandsnaam op uit je helper; verwacht iets als "identifier.sqlite"
            var databaseName = Grocery.Core.Data.Helpers.ConnectionHelper.ConnectionStringValue("GroceryAppDb");
            if (string.IsNullOrWhiteSpace(databaseName))
                databaseName = "grocery.db";

            // Bepaal platform-specifiek pad
#if ANDROID || IOS || MACCATALYST
            var folder = FileSystem.AppDataDirectory; // sandbox van de app
#else
            var folder = AppContext.BaseDirectory;     // bin/Debug/... op desktop
#endif
            Directory.CreateDirectory(folder);

            // >>> BELANGRIJK: correcte Path.Combine (niet string concats)
            _dbPath = Path.Combine(folder, databaseName);

            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default
            };
            Connection = new SqliteConnection(csb.ToString());

            // Log het pad zodat je dit bestand in Rider/DB Browser kunt openen
            Debug.WriteLine("[DB] Using SQLite file: " + _dbPath);
        }

        protected void OpenConnection()
        {
            if (Connection.State != System.Data.ConnectionState.Open)
            {
                Connection.Open();

                // Zet foreign keys AAN (SQLite heeft ze standaard uit)
                using var fk = new SqliteCommand("PRAGMA foreign_keys = ON;", Connection);
                fk.ExecuteNonQuery();
            }
        }

        protected void CloseConnection()
        {
            if (Connection.State != System.Data.ConnectionState.Closed)
                Connection.Close();
        }

        public void CreateTable(string commandText)
        {
            OpenConnection();
            try
            {
                using var command = Connection.CreateCommand();
                command.CommandText = commandText;
                command.ExecuteNonQuery();
            }
            finally
            {
                CloseConnection();
            }
        }

        public void InsertMultipleWithTransaction(List<string> linesToInsert)
        {
            OpenConnection();
            using var transaction = Connection.BeginTransaction();
            try
            {
                foreach (var sql in linesToInsert)
                {
                    using var cmd = new SqliteCommand(sql, Connection, transaction);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[DB] Insert batch error: " + ex);
                try { transaction.Rollback(); } catch { /* ignore */ }
                throw;
            }
            finally
            {
                CloseConnection();
            }
        }

        public void Dispose()
        {
            try { CloseConnection(); } catch { /* ignore */ }
            Connection.Dispose();
        }
    }
}
