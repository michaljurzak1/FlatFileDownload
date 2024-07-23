﻿using DatabaseConnection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PlikPlaskiCheck
{
    internal class CheckDataSourceFactory : DataSourceFactoryAbstract
    {
        IConnection connection;
        int iterations;
        public CheckDataSourceFactory(IConnection connection) : base(connection)
        {
            this.connection = connection;
        }

        public int CountData()
        {
            DataTable dt = this.connection.ExecuteQuery(
                @"select sum(union_tables.c)
                  from
                      (select count() as c
                       from SkrotyPodatnikowCzynnych
                       union
                       select count() as c
                       from SkrotyPodatnikowZwolnionych) as union_tables"
                );

            int nRows = int.Parse(dt.Rows[0][0].ToString());

            if (dt.Rows.Count == 0 || nRows == 0)
                throw new DataException("No data in DB");

            return nRows;
        }

        #region database check for date, nip, nrb

        public string CheckAccount(string date, string nip, string nrb)
        {
            string? res = CheckDateNipNrb(date, nip, nrb);
            if (res != null)
            {
                return $"\nReal Account in {res}";
            }
            else
            {
                Console.WriteLine("No Real account found, checking masks");

                string? res1 = CheckDateNipNrbMasks(date, nip, nrb);
                if (res1 != null)
                {
                    return $"\nVirtual Account in {res1}";
                }
                else
                {
                    return "\nNo record found";
                }
            }
        }

        private string? CheckDateNipNrb(string date, string nip, string nrb)
        {
            nip = nip.Replace(" ", "");
            nrb = nrb.Replace(" ", "");

            if (date.Length != 8 || nip.Length != 10 || nrb.Length != 26)
                throw new ArgumentException("Invalid date, nip or nrb");

            string hashString = GetSha512(date, nip, nrb);
            if (Is_Record_In_Table("SkrotyPodatnikowCzynnych", hashString))
                return "SkrotyPodatnikowCzynnych";

            if (Is_Record_In_Table("SkrotyPodatnikowZwolnionych", hashString))
                return "SkrotyPodatnikowZwolnionych";

            return null;
        }

        private string? CheckDateNipNrbMasks(string date, string nip, string nrb)
        {
            nip = nip.Replace(" ", "");
            nrb = nrb.Replace(" ", "");

            // get according masks
            Console.WriteLine("Extracting according masks");
            string[] masks = NrbMasks(nrb);
            string[] hashStrings = new string[masks.Length];

            Console.WriteLine("Creating hash strings");
            for (int i = 0; i < masks.Length; i++)
            {
                hashStrings[i] = GetSha512(date, nip, masks[i]);
            }

            Console.WriteLine("Checking if hash strings are in tables");
            
            foreach(string mask in masks)
            {
                string? res = CheckDateNipNrb(date, nip, mask);
                if (res != null)
                {
                    return res;
                }
            }

            return null;
        }


        #region masks logic
        private string[] NrbMasks(string nrb)
        {
            string like = nrb.Substring(2, 8);
            string[] masks = GetTableColumnLikeValues("Maski", like);
            
            return ReplaceMasks(masks, nrb);
        }

        private static string[] ReplaceMasks(string[] masks, string template)
        {
            string[] nrbs = new string[masks.Length];
            for (int i = 0; i < nrbs.Length; i++)
            {
                char[] maskArray = masks[i].ToCharArray();

                for (int j = 0; j < maskArray.Length; j++)
                {
                    if (maskArray[j] == 'Y')
                    {
                        maskArray[j] = template[j];
                    }
                }

                nrbs[i] = new string(maskArray);
            }
            return nrbs;
        }

        #endregion masks logic

        #endregion database check for date, nip, nrb

        #region sha512 logic
        public static string GetSha512(string date, string nip, string nrb, int iterations=5000)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] hashBytes = Encoding.UTF8.GetBytes(date + nip + nrb);

                string hashString = "";

                for (int i = 0; i < iterations; i++)
                {
                    hashBytes = sha512.ComputeHash(hashBytes);
                    hashString = FromHashByteToString(hashBytes);
                    hashBytes = Encoding.UTF8.GetBytes(hashString);
                }

                return hashString;
            }
        }

        private static string FromHashByteToString(byte[] hashBytes)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                builder.Append(hashBytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
        #endregion sha512 logic

        #region helper methods

        public bool Is_Record_In_Table(string tableName, string value, string columnName = "value")
        {
            DataTable dt = connection.ExecuteQuery($"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = '{value}'");

            if (dt.Rows.Count > 0)
            {
                int n_rows = int.Parse(dt.Rows[0][0].ToString());
                if (n_rows == 1)
                    return true;
                else if (n_rows > 1)
                    throw new DataException("More than one record in table");
                else
                    return false;
            }
            else
                throw new DataException("No data in table");
        }

        private string[] GetTableColumnLikeValues(string tableName, string like, string columnName = "value")
        {
            DataTable dt = connection.ExecuteQuery($"SELECT {columnName} FROM {tableName} WHERE {columnName} LIKE '%{like}%'");
            if (dt.Rows.Count == 0)
                throw new DataException($"No data in DB: {tableName}");
            string[] columnValues = dt.AsEnumerable().Select(x => x[columnName].ToString()).ToArray<string>();

            return columnValues;
        }

        #endregion helper methods
    }
}
