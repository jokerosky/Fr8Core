﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.TerminalBase.Interfaces;
using log4net;

namespace Fr8.TerminalBase.Services
{
    public class HubEventReporter : IHubEventReporter
    {
        private static readonly ILog Logger = Fr8.Infrastructure.Utilities.Logging.Logger.GetCurrentClassLogger();

        private readonly IHubDiscoveryService _hubDiscovery;
        private readonly IActivityStore _activityStore;

        public TerminalDTO Terminal => _activityStore.Terminal;

        public HubEventReporter(IHubDiscoveryService hubDiscovery, IActivityStore activityStore)
        {
            _hubDiscovery = hubDiscovery;
            _activityStore = activityStore;
        }

        public async Task Broadcast(Crate eventPayload)
        {
            var hubList = await _hubDiscovery.GetSubscribedHubs();
            var tasks = new List<Task>();
            
            foreach (var hubUrl in hubList)
            {
                tasks.Add(NotifyHub(hubUrl, eventPayload));
            }

            await Task.WhenAll(tasks);
        }

        private async Task NotifyHub(string hubUrl, Crate eventPayload)
        {
            try
            {
                Logger.Info($"Terminal at '{Terminal?.Endpoint}' is sedning event to Hub at '{hubUrl}'.");
                var hubCommunicator = await _hubDiscovery.GetHubCommunicator(hubUrl);
                await hubCommunicator.SendEvent(eventPayload);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send event to hub '{hubUrl}'", ex);
            }
        }

        public Task<IHubCommunicator> GetMasterHubCommunicator()
        {
            return _hubDiscovery.GetMasterHubCommunicator();
        }
    }
}
