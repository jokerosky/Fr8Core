﻿
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

using Microsoft.Owin;

using Owin;


[assembly: OwinStartup(typeof(pluginAzureSqlServer.Startup))]

namespace pluginAzureSqlServer
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
          
        }


    }
}
