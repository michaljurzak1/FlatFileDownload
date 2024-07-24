﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Data.SqlClient;
using System.Data.Common;
using System.Transactions;

namespace DatabaseConnection
{
    public sealed class SqliteDB : IConnection
    {
        private SqliteConnection connection;

        public SqliteDB(bool onlyRead=true, string dbPath="C:/DatabaseSqlite")
        {
            Connect(onlyRead, dbPath);
        }

        ~SqliteDB()
        {
            Close();
        }

        public bool Connect(bool onlyRead, string dbPath)
        {
            try
            {
                dbPath = Path.Combine(new string[] { dbPath, "plikplaski.db" });
                // ?
                if (File.Exists(dbPath))
                {
                    connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();
                    Console.WriteLine("Connected to database");
                }
                else if (onlyRead)
                {
                    throw new DataException("Database not found");
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                    File.Create(dbPath).Close();
                    connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();
                    Console.WriteLine("Created and connected database");

                }
            }
            catch (SqliteException e)
            {
                Console.WriteLine("Error connecting to database. Exiting");
                Console.WriteLine(e.Message);
                Environment.Exit(1);
                return false;
            }
            return true;
        }

        public bool Close()
        {
            if(this.connection != null)
                this.connection.Close();
            return true;
        }

        public DataTable ExecuteQuery(string query)
        {
            return Read<SqliteConnection>(query);
        }

        private DataTable Read<T>(string query) where T : IDbConnection, new()
        {
            using (var conn = new T())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Connection = connection;
                    cmd.Connection.Open();
                    var table = new DataTable();
                    table.Load(cmd.ExecuteReader());
                    return table;
                }
            }
        }

        public int ExecuteNonQuery(string query, params IDbDataParameter[]? parameters)
        {
            using (var command = this.connection.CreateCommand())
            {
                command.CommandText = query;
                if (parameters != null && parameters.Length > 0)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }
                    return command.ExecuteNonQuery();
                }
                return new SqliteCommand(query, this.connection).ExecuteNonQuery();
            }


            //SqliteCommand newDatabase = new SqliteCommand(query, this.connection);
        }

        public IDbDataParameter CreateParameter(string name, object value)
        {
            return new SqliteParameter(name, value);
        }

        public void BulkInsert(Dictionary<string, string[]> tableDataPairs)
        {
            foreach (var pair in tableDataPairs)
            {
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();
                    command.CommandText = $"INSERT INTO {pair.Key} (value) VALUES ($value)";

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "$value";
                    command.Parameters.Add(parameter);

                    foreach (var value in pair.Value)
                    {
                        parameter.Value = value;
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }
    }
}