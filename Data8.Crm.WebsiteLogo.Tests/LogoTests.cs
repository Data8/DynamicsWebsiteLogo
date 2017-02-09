using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;

namespace Data8.Crm.WebsiteLogo.Tests
{
    [TestClass]
    public class LogoTests
    {
        [TestMethod]
        public void MicrosoftTest()
        {
            var logo = WebsiteLogoActivity.GetLogo(new DebugLogger(), "http://www.microsoft.com");

            Assert.IsNotNull(logo);
            DumpLogo(logo);
        }

        [TestMethod]
        public void MissingProtocolTest()
        {
            var logo = WebsiteLogoActivity.GetLogo(new DebugLogger(), "www.microsoft.com");

            Assert.IsNotNull(logo);
            DumpLogo(logo);
        }

        [TestMethod]
        public void Data8Test()
        {
            var logo = WebsiteLogoActivity.GetLogo(new DebugLogger(), "http://www.data-8.co.uk");

            Assert.IsNotNull(logo);
            DumpLogo(logo);
        }

        private void DumpLogo(byte[] logo)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "logo.png");
            File.WriteAllBytes(path, logo);
        }
    }

    public class DebugLogger : ITracingService
    {
        public void Trace(string format, params object[] args)
        {
            System.Diagnostics.Trace.TraceInformation(format, args);
        }
    }
}
