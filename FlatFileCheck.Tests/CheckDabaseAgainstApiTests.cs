﻿using DatabaseConnection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlatFileCheck.Tests
{
    [TestClass]
    //[assembly: CollectionBehavior(DisableTestParallelization = true)]
    public class CheckDabaseAgainstApiTests
    {
        /*
        [TestMethod]
        [DataRow("5260250274", "10101010100038252231000000")] // Ministerstwo Finansow
        public async Task CheckApi(string nip, string nrb)
        {
            try
            {
                bool result = await MinisterstwoFinansowApi.GetAccount(nip, nrb);
                Assert.IsTrue(result, "Api check failed.");
            }
            catch (Exception e)
            {
                HandleResponseException(e);
            }
        }*/
        private void HandleResponseException(Exception e)
        {
            var match = Regex.Match(e.Message, @"\b\d{3}\b");
            if (match.Success)
            {
                int statusCode = int.Parse(match.Value);
                HandleStatusCode(statusCode);
            }
            else
            {
                Assert.Fail("Api check failed or connection error.");
            }
        }

        [TestMethod]
        [assembly: DoNotParallelize]
        [DataRow("5260250274", "10101010100038252231000000")] // Ministerstwo Finansow
        public async Task CheckDbDefaultLocationAgainstApi(string nip, string nrb)
        {
            if (File.Exists("/DatabaseSqlite/flatfile.db"))
            {
                bool result;
                CheckDataSourceFactory factory = null;
                try
                {
                    result = await MinisterstwoFinansowApi.GetAccount(nip, nrb);
                    factory = new CheckDataSourceFactory(new SqliteDB(true));
                } 
                catch(HttpRequestException e)
                {
                    HandleResponseException(e);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Assert.Fail("Api check failed or connection error.");
                }
                    
                string today = DateTime.Now.ToString("yyyyMMdd");
                if (!factory.IsDataValid(today))
                {
                    try
                    {
                        bool isAvailable = !factory.CheckFlatFileAvailable(DateTime.Now);
                        if (isAvailable)
                        {
                            Assert.Inconclusive("No new data is available, exiting.");
                            return;
                        }
                    }
                    catch
                    {
                        Assert.Inconclusive("No new data is available, exiting.");
                        return;
                    }

                    Assert.Fail("Data not valid in database.");
                    return;
                }
                string isInResult = factory.CheckAccount(today, nip, nrb);

                Assert.AreEqual(isInResult, "\nReal Account in SkrotyPodatnikowCzynnych", "Local database differs with api.");
                
            }
            else
            {
                Assert.Inconclusive("Database not found.");
            }
        }
        private void HandleStatusCode(int statusCode)
        {
            if (statusCode >= 400 && statusCode < 500)
            {
                if (statusCode == 400)
                    Assert.Fail("Api check failed");
                else if (statusCode == 429)
                    Assert.Inconclusive("Api too many requests / limit reached.");

                Assert.Fail("Api user error occurred.");
            }
            else if (statusCode >= 500)
                Assert.Fail("Api server error occurred.");
            else
                Assert.Fail("Api unexpected error occurred.");
        }
    }
}
