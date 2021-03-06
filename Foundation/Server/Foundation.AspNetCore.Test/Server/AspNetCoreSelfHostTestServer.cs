﻿using Foundation.Test.Server;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace Foundation.AspNetCore.Test.Server
{
    public class AspNetCoreSelfHostTestServer : TestServerBase
    {
        private IWebHost _host;

        public override void Initialize(string uri)
        {
            base.Initialize(uri);

            _host = new WebHostBuilder()
                .UseHttpListener()
                .UseUrls(uri)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<AutofacAspNetCoreAppStartup>()
                .Build();

            _host.Start();
        }

        public override void Dispose()
        {
            _host.Dispose();
        }

        protected override HttpMessageHandler GetHttpMessageHandler()
        {
            return new HttpClientHandler();
        }
    }
}
