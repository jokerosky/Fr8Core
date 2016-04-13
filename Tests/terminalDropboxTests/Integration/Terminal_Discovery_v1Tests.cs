﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Interfaces.Manifests;
using HealthMonitor.Utility;
using Microsoft.Owin.Hosting;
using NUnit.Framework;
using Ploeh.AutoFixture;
using terminalDropbox;

namespace terminalDropboxTests.Integration
{
    [Explicit]
    public class Terminal_Discover_v1Tests : BaseTerminalIntegrationTest
    {
        private const int ActivityCount = 1;
        private const string Get_File_List_Activity_Name = "Get_File_List";

        private const string Host = "http://localhost:19760";
        private IDisposable _app;

        public override string TerminalName => "terminalDropbox";

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _app = WebApp.Start<Startup>(Host);
        }

        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            _app.Dispose();
        }

        [Test, CategoryAttribute("Integration.terminalDropbox")]
        public async Task Discover_Check_Returned_Activities()
        {
            //Arrange
            var discoverUrl = GetTerminalDiscoverUrl();

            //Act
            var terminalDiscoverResponse = await HttpGetAsync<StandardFr8TerminalCM>(discoverUrl);

            //Assert
            Assert.IsNotNull(terminalDiscoverResponse, "Terminal Dropbox discovery did not happen.");
            Assert.IsNotNull(terminalDiscoverResponse.Activities, "Dropbox terminal actions were not loaded");
            Assert.AreEqual(ActivityCount, terminalDiscoverResponse.Activities.Count,
            "Not all terminal Dropbox actions were loaded");
            Assert.AreEqual(terminalDiscoverResponse.Activities.Any(a => a.Name == Get_File_List_Activity_Name), true, "Action " + Get_File_List_Activity_Name + " was not loaded");

        }
    }
}
