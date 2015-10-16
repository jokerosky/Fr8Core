﻿using System;
using System.Collections.Generic;
using Data.Entities;
using Data.Interfaces;
using Data.States;
using Newtonsoft.Json;
using Utilities;

namespace Data.Repositories
{
    public class AuthorizationTokenRepository : GenericRepository<AuthorizationTokenDO>, IAuthorizationTokenRepository
    {
        internal AuthorizationTokenRepository(IUnitOfWork uow)
            : base(uow)
        {

        }

        public String GetAuthorizationTokenURL(String url, Fr8AccountDO dockyardAccountDO, String segmentEventName = null, Dictionary<String, Object> segmentTrackingProperties = null)
        {
            return GetAuthorizationTokenURL(url, dockyardAccountDO.Id, segmentEventName, segmentTrackingProperties);
        }

        public String GetAuthorizationTokenURL(String url, String userID, String segmentEventName = null, Dictionary<String, Object> segmentTrackingProperties = null)
        {
            var token = GetAuthorizationToken(url, userID, segmentEventName, segmentTrackingProperties);

            var responseUrl = String.Format("{0}tokenAuth?token={1}",
                    Server.ServerUrl,
                    token);

            return responseUrl;
        }

        private String GetAuthorizationToken(String url, String userID, String segmentEventName = null, Dictionary<String, Object> segmentTrackingProperties = null)
        {
            var newTokenLink = new AuthorizationTokenDO
            {
                RedirectURL = url,
                UserID = userID,
                ExpiresAt = DateTime.Now.AddDays(10),
                SegmentTrackingEventName = segmentEventName,
                Plugin = new PluginDO()
                {
                    Id = 0,
                    Name = "",
                    Endpoint = "",
                    Version = "1",
                    PluginStatus = PluginStatus.Active
                }
            };

            if (segmentTrackingProperties != null)
                newTokenLink.SegmentTrackingProperties = JsonConvert.SerializeObject(segmentTrackingProperties);

            UnitOfWork.AuthorizationTokenRepository.Add(newTokenLink);
            return newTokenLink.Id.ToString();
        }
    }

    public interface IAuthorizationTokenRepository : IGenericRepository<AuthorizationTokenDO>
    {

    }
}
