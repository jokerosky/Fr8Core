﻿using System;
using System.IO;
using NUnit.Framework;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;
using terminalDocuSign.Infrastructure;

namespace terminalDocuSign.Tests.Infrastructure
{
    [TestFixture]
    [Category("DocusignConnectParser")]
    public class DocusignConnectParserTests : BaseTest
    {
        private string _xmlPayloadFullPath;

        [SetUp]
        public override void SetUp()
        {
            var target = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.FullName, "Tests");

            _xmlPayloadFullPath = FixtureData.FindXmlPayloadFullPath(target);

            if (_xmlPayloadFullPath == string.Empty)
                throw new Exception("XML payload file for testing DocuSign notification is not found.");
        }

        [Test, Ignore("Ignored for now, breaks build on AppVeyor")]
        public void DocusignConnectParser_CanParseEnvelopeData()
        {
            var xml = File.ReadAllText(_xmlPayloadFullPath);
            var info = FixtureData.TestDocuSignEnvelopeInformation();
            var docuSignEnvelopeInformation = DocuSignConnectParser.GetEnvelopeInformation(xml);

            Assert.NotNull(docuSignEnvelopeInformation);
            Assert.AreEqual(docuSignEnvelopeInformation.EnvelopeStatus.EnvelopeId, info.EnvelopeStatus.EnvelopeId);
        }
    }
}