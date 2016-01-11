﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web;

namespace HubWeb.Infrastructure
{
    public class TerminalPrinciple : IPrincipal
    {
        private Guid _terminalId { get; set; }
        private string _userId { get; set; }
        private IIdentity _identity { get; set; }

        public TerminalPrinciple(Guid terminalId, string userId, IIdentity identity)
        {
            _userId = userId;
            _terminalId = terminalId;
            _identity = identity;
        }

        public IIdentity Identity
        {
            get
            {
                return _identity;
            }
        }

        public Guid GetTerminalId()
        {
            return _terminalId;
        }
        public string GetOnBehalfUserId()
        {
            return _userId;
        }

        public bool IsInRole(string role)
        {
            return false;
        }
    }
}