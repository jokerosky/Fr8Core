﻿using System;
using System.Collections.Generic;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using Microsoft.Owin;
using Owin;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;
using TerminalBase.Services;
using terminalQuickBooks.Actions;

[assembly: OwinStartup(typeof(terminalQuickBooks.Startup))]

namespace terminalQuickBooks
{
    public class Startup : BaseConfiguration
    {
        public void Configuration(IAppBuilder app)
        {
            Configuration(app, false);
        }

        public void Configuration(IAppBuilder app, bool selfHost)
        {
            ConfigureProject(selfHost, TerminalQuickbooksBootstrapper.ConfigureLive);
            RoutesConfig.Register(_configuration);
            ConfigureFormatters();

            app.UseWebApi(_configuration);

            if (!selfHost)
            {
                StartHosting("terminalQuickBooks");
            }
        }

        public override ICollection<Type> GetControllerTypes(IAssembliesResolver assembliesResolver)
        {
            return new Type[] {
                typeof(Controllers.ActivityController),
                typeof(Controllers.EventController),
                typeof(Controllers.AuthenticationController),
                typeof(Controllers.TerminalController)
            };
        }
        protected override void RegisterActivities()
        {
            ActivityStore.RegisterActivity<Convert_TableData_To_AccountingTransactions_v1>(Convert_TableData_To_AccountingTransactions_v1.ActivityTemplateDTO);
            ActivityStore.RegisterActivity<Create_Journal_Entry_v1>(Create_Journal_Entry_v1.ActivityTemplateDTO);
        }
    }
}
