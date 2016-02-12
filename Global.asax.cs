﻿using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Data.Interfaces;
using FluentValidation.WebApi;
using Hub.Managers;
using Hub.ModelBinders;
using Hub.StructureMap;
using HubWeb.App_Start;
using HubWeb.ExceptionHandling;
using LogentriesCore.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Segment;
using StructureMap;
using Utilities;
using Logger = Utilities.Logging.Logger;
using HubWeb.Infrastructure;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Extensibility;
using TerminalBase.Infrastructure;
using Data.Infrastructure;

namespace HubWeb
{
    public class MvcApplication : HttpApplication
    {
        private static bool _IsInitialised;

        protected void Application_Start()
        {
            Init(false);
        }

        public void Init(bool selfHostMode = false)
        {
            if (!selfHostMode)
            {
                GlobalConfiguration.Configure(WebApiConfig.Register);
                AreaRegistration.RegisterAllAreas();
                FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
                RouteConfig.RegisterRoutes(RouteTable.Routes);
                BundleConfig.RegisterBundles(BundleTable.Bundles);
            }

            // Configure formatters
            // Enable camelCasing in JSON responses
            var formatters = GlobalConfiguration.Configuration.Formatters;
            var jsonFormatter = formatters.JsonFormatter;
            var settings = jsonFormatter.SerializerSettings;
            settings.Formatting = Formatting.Indented;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            //Register global Exception Filter for WebAPI 
            GlobalConfiguration.Configuration.Filters.Add(new WebApiExceptionFilterAttribute());

            StructureMapBootStrapper.ConfigureDependencies(StructureMapBootStrapper.DependencyType.LIVE);
            ObjectFactory.GetInstance<AutoMapperBootStrapper>().ConfigureAutoMapper();

            var db = ObjectFactory.GetInstance<DbContext>();
            db.Database.Initialize(true);

            if (selfHostMode)
            {
                StructureMapBootStrapper.ConfigureDependencies(StructureMapBootStrapper.DependencyType.LIVE);
                ObjectFactory.GetInstance<AutoMapperBootStrapper>().ConfigureAutoMapper();
            }

            Utilities.Server.IsProduction = ObjectFactory.GetInstance<IConfigRepository>().Get<bool>("IsProduction");
            Utilities.Server.IsDevMode = ObjectFactory.GetInstance<IConfigRepository>().Get<bool>("IsDev", true);

            if (!selfHostMode)
            {
                Utilities.Server.ServerPhysicalPath = Server.MapPath("~");
                var segmentWriteKey = new ConfigRepository().Get("SegmentWriteKey");
                Analytics.Initialize(segmentWriteKey);
            }

            EventReporter curReporter = ObjectFactory.GetInstance<EventReporter>();
            curReporter.SubscribeToAlerts();

            IncidentReporter incidentReporter = ObjectFactory.GetInstance <IncidentReporter>();
            incidentReporter.SubscribeToAlerts();

            ModelBinders.Binders.Add(typeof(DateTimeOffset), new KwasantDateBinder());

            var configRepository = ObjectFactory.GetInstance<IConfigRepository>();
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                //THIS IS CURRENTLY CAUSING AN EXCEPTION
                //uow.RemoteServiceProviderRepository.CreateRemoteServiceProviders(configRepository);
                uow.SaveChanges();
            }

            SetServerUrl();

            Logger.GetLogger().Warn("Dockyard  starting...");

            ConfigureValidationEngine();
            StartupMigration.CreateSystemUser();

        }

        private void ConfigureValidationEngine()
        {
            FluentValidationModelValidatorProvider.Configure(GlobalConfiguration.Configuration);
        }


        protected void Application_Error(Object sender, EventArgs e)
        {
            var exception = Server.GetLastError();
            String errorMessage = "Critical internal error occured.";
            try
            {
                if (HttpContext.Current != null && HttpContext.Current.Request != null)
                    errorMessage += " URL accessed: " + HttpContext.Current.Request.Url;
            }
            catch (Exception)
            {
                errorMessage += " Error on startup.";
            }


            Logger.GetLogger().Error(errorMessage, exception);
        }

        private readonly object _initLocker = new object();

        //Optimization. Even if running in DEBUG mode, this will only execute once.
        //But on production, there is no need for this call
        protected void Application_BeginRequest(object sender, EventArgs e)
        {

#if DEBUG
            SetServerUrl(HttpContext.Current);
#endif
            NormalizeUrl();
        }

        /// <summary>
        /// Make sure that User is accessing the website using correct and secure URL
        /// </summary>
        private void NormalizeUrl()
        {
            // Ignore requests to dev and API since API clients usually cannot process 301 redirects
            if (Request.Url.PathAndQuery.ToLower().StartsWith("/api") 
                || Request.Url.PathAndQuery.ToLower().StartsWith("/authenticationcallback")
                || Request.Url.Host.ToLower().Contains("dev"))
                return;

            // Force user to fr8.co from fr8.company (old address)
            if (Request.Url.Host.Contains("fr8.company") || Request.Url.Host.StartsWith("www."))
            {
                RedirectToCanonicalUrl();
            }

            // Force user to http if user is accessing the PROD site
            if (Request.Url.Host.StartsWith("fr8.co"))
            {
                switch (Request.Url.Scheme)
                {
                    case "https":
                        Response.AddHeader("Strict-Transport-Security", "max-age=300");
                        break;
                    case "http":
                        RedirectToCanonicalUrl();
                        break;
                }
            }
        }

        private void RedirectToCanonicalUrl()
        {
            var path = "https://fr8.co" + Request.Url.PathAndQuery;
            Response.Status = "301 Moved Permanently";
            Response.AddHeader("Location", path);
        }

        private void SetServerUrl(HttpContext context = null)
        {
            if (!_IsInitialised)
            {
                lock (_initLocker)
                {
                    //Not redunant - this check is more efficient for a 1-time set.
                    //If it's set, we exit without locking. We want to avoid locking as much as possible, so only do it once (at startup)
                    if (!_IsInitialised)
                    {
                        //First, try to read from the config
                        var config = ObjectFactory.GetInstance<IConfigRepository>();
                        var serverProtocol = config.Get("ServerProtocol", String.Empty);
                        var domainName = config.Get("ServerDomainName", String.Empty);
                        var domainPort = config.Get<int?>("ServerPort", null);

                        if (!String.IsNullOrWhiteSpace(domainName) && !String.IsNullOrWhiteSpace(serverProtocol) && domainPort.HasValue)
                        {
                            Utilities.Server.ServerUrl = String.Format("{0}{1}{2}/", serverProtocol, domainName,
                                domainPort.Value == 80 ? String.Empty : (":" + domainPort.Value));

                            Utilities.Server.ServerHostName = domainName;
                        }
                        else
                        {
                            if (context == null)
                                return;

                            //If the config is not set, then we setup our server URL based on the first request
                            string port = context.Request.ServerVariables["SERVER_PORT"];
                            if (port == null || port == "80" || port == "443")
                                port = "";
                            else
                                port = ":" + port;

                            string protocol = context.Request.ServerVariables["SERVER_PORT_SECURE"];
                            if (protocol == null || protocol == "0")
                                protocol = "http://";
                            else
                                protocol = "https://";

                            // *** Figure out the base Url which points at the application's root
                            Utilities.Server.ServerHostName = context.Request.ServerVariables["SERVER_NAME"];
                            string url = protocol + context.Request.ServerVariables["SERVER_NAME"] + port + context.Request.ApplicationPath;
                            Utilities.Server.ServerUrl = url;
                        }
                        _IsInitialised = true;
                    }
                }
            }
        }

        public void Application_End()
        {
            Logger.GetLogger().Info("fr8 web shutting down...");

            // This will give LE background thread some time to finish sending messages to Logentries.
            var numWaits = 3;
            while (!AsyncLogger.AreAllQueuesEmpty(TimeSpan.FromSeconds(5)) && numWaits > 0)
                numWaits--;
        }

        protected void Application_PostAuthenticateRequest(Object sender, EventArgs e)
        {
            var principal = (ClaimsPrincipal)Thread.CurrentPrincipal;
            if (principal != null)
            {
                var claims = principal.Claims;
                var roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
                var userPrincipal = new Fr8Principle(null, principal.Identity, roles);
                /*
                GenericPrincipal userPrincipal = new GenericPrincipal(principal.Identity,
                                         claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray());
                */
                Context.User = userPrincipal;
            }
        }
    }
}

