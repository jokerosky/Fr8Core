﻿using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using StructureMap;
using Core.Interfaces;
using Core.Services;
using Data.Entities;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Web.ViewModels;

namespace Web.Controllers
{
    public class AuthenticationCallbackController : Controller
    {
        private readonly IAction _action;

        private readonly Authorization _authorization;

        public AuthenticationCallbackController()
        {
            _action = ObjectFactory.GetInstance<IAction>();
            _authorization = new Authorization();
        }

        [HttpGet]
        public async Task<ActionResult> ProcessSuccessfulOAuthResponse(
            string pluginName,
            string pluginVersion)
        {
            
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(pluginVersion))
            {
                throw new ApplicationException("PluginName or PluginVersion is not specified.");
            }

            var requestQueryString = Request.Url.Query;
            if (!string.IsNullOrEmpty(requestQueryString) && requestQueryString[0] == '?')
            {
                requestQueryString = requestQueryString.Substring(1);
            }

            PluginDO plugin;

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                plugin = uow.PluginRepository
                    .FindOne(x => x.Name == pluginName && x.Version == pluginVersion);
                if (plugin == null)
                {
                    throw new ApplicationException("Could not find plugin.");
                }
            }
            
            var externalAuthenticationDTO = new ExternalAuthenticationDTO()
            {
                RequestQueryString = requestQueryString
            };

            var error = await _authorization.GetOAuthToken(plugin, externalAuthenticationDTO);

            if (string.IsNullOrEmpty(error))
            {
                return View();
            }
            else
            {
                return View("Error", new AuthenticationErrorVM()
                {
                    Error = error
                });
            }
        }
    }
}