﻿using Hangfire;
using Hub.Infrastructure;
using Hub.Interfaces;
using StructureMap;
using System;
using System.Threading.Tasks;
using System.Web.Http;
using HubWeb.Infrastructure_HubWeb;
using log4net;
using Data.Interfaces;
using System.Linq;
using System.Net.Http;
using Fr8.Infrastructure.Communication;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Interfaces;
using System.Web.Http.Description;
using Fr8.Infrastructure.Utilities;

namespace HubWeb.Controllers
{
    public class AlarmsController : ApiController
    {
        private static readonly ILog Logger = Fr8.Infrastructure.Utilities.Logging.Logger.GetCurrentClassLogger();

        /// <summary>
        /// Schedules specified alarm to be executed at specified time
        /// </summary>
        /// <param name="alarmDTO">Alarm to schedule at its startTime property</param>
        /// <response code="200">Alarm was succesfully scheduled</response>
        [HttpPost]
        [Fr8TerminalAuthentication]
        [Fr8ApiAuthorize]
        public async Task<IHttpActionResult> Post(AlarmDTO alarmDTO)
        {
            BackgroundJob.Schedule(() => Execute(alarmDTO), alarmDTO.StartTime);
            return Ok();
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        public void Execute(AlarmDTO alarmDTO)
        {
            try
            {
                var containerService = ObjectFactory.GetInstance<IContainerService>();
                using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
                {
                    var container = uow.ContainerRepository.GetByKey(alarmDTO.ContainerId);
                    if (container == null)
                    {
                        throw new Exception($"Container {alarmDTO.ContainerId} was not found.");
                    }

                    var continueTask = containerService.Continue(uow, container);
                    Task.WaitAll(continueTask);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to run container: {alarmDTO.ContainerId}", ex);
            }

            //TODO report output to somewhere to pusher service maybe
        }
        /// <summary>
        /// Initiates periodic requests to the terminal with the specified Id configured with specified settings
        /// </summary>
        /// <remarks>
        /// Alarms provide the ability to resend requests with specified data to the terminal until the latter responses with status 200 OK. It works as follows. A terminal calls this endpoint with specified data and sets the time intervals. The Hub then will iteratively call the terminal until the latter replies with status 200 OK
        /// </remarks>
        /// <param name="terminalId">Id of the terminal to perform requests to</param>
        /// <param name="pollingData">Parameters of polling requests</param>
        /// <response code="200">Polling was successfully initiated</response>
        [HttpPost]
        public IHttpActionResult Polling([FromUri] string terminalToken, [FromBody]PollingDataDTO pollingData)
        {
            Logger.Info($"Polling: requested for {pollingData.ExternalAccountId} from a terminal {terminalToken}");
            pollingData.JobId = terminalToken + "|" + pollingData.ExternalAccountId;
            RecurringJob.AddOrUpdate(pollingData.JobId, () => SchedullerHelper.ExecuteSchedulledJob(pollingData, terminalToken), "*/" + pollingData.PollingIntervalInMinutes + " * * * *");
            if (pollingData.TriggerImmediately)
            {
                RecurringJob.Trigger(pollingData.JobId);
            }
            return Ok();
        }
    }


    public static class SchedullerHelper
    {
        private static async Task<bool> RenewAuthToken(PollingDataDTO pollingData, string terminalToken)
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var terminalDO = await ObjectFactory.GetInstance<ITerminal>().GetByToken(terminalToken);
                var token = uow.AuthorizationTokenRepository.FindTokenByExternalAccount(pollingData.ExternalAccountId, terminalDO.Id, pollingData.Fr8AccountId);
                if (token != null)
                {
                    pollingData.AuthToken = token.Token;
                    return true;
                }
            }
            return false;
        }

        private static readonly ILog Logger = Fr8.Infrastructure.Utilities.Logging.Logger.GetCurrentClassLogger();

        public static void ExecuteSchedulledJob(PollingDataDTO pollingData, string terminalToken)
        {
            IRestfulServiceClient _client = new RestfulServiceClient();

            //renewing token
            if (!(RenewAuthToken(pollingData, terminalToken)).Result)
            {
                RecurringJob.RemoveIfExists(pollingData.JobId);
                Logger.Info($"Polling: token is missing, removing the job for {pollingData.ExternalAccountId}");
            }

            var request = RequestPolling(pollingData, terminalToken, _client);
            var result = request.Result;

            if (result != null)
            {
                if (!result.Result)
                {
                    Logger.Info($"Polling: got result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Deschedulling the job");
                    if (pollingData.RetryCounter > 3)
                    {
                        Logger.Info($"Polling: for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Deschedulling the job");
                        RecurringJob.RemoveIfExists(pollingData.JobId);
                    }
                    else
                    {
                        pollingData.RetryCounter++;
                        Logger.Info($"Polling: got result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Starting Retry {pollingData.RetryCounter}");
                        RecurringJob.AddOrUpdate(pollingData.JobId, () => SchedullerHelper.ExecuteSchedulledJob(result, terminalToken), "*/" + result.PollingIntervalInMinutes + " * * * *");
                    }
                }
                else
                {
                    Logger.Info($"Polling: got result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Success");
                    RecurringJob.AddOrUpdate(pollingData.JobId, () => SchedullerHelper.ExecuteSchedulledJob(result, terminalToken), "*/" + result.PollingIntervalInMinutes + " * * * *");
                }
            }
            else
            {
                Logger.Info($"Polling: no result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Terminal didn't answer");
                //we didn't get any response from the terminal (it might have not started yet, for example) Let's give it one more chance, and if it will fail - the job will be descheduled cause of Result set to false;
                if (pollingData.Result) //was the job successfull last time we polled?
                {
                    Logger.Info($"Polling: no result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Last polling was successfull");

                    //in case of ongoing deployment when we have a minimal polling interval, could happen to remove the job. Add default polling interval of 10 minutes in this case as retry
                    pollingData.Result = false;
                    RecurringJob.AddOrUpdate(pollingData.JobId, () => SchedullerHelper.ExecuteSchedulledJob(pollingData, terminalToken), "*/" + pollingData.PollingIntervalInMinutes + " * * * *");
                }
                else
                {
                    if (pollingData.RetryCounter > 20)
                    {
                        Logger.Info($"Polling: no result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Remove Job");
                        //last polling was unsuccessfull, so let's deschedulle it
                        RecurringJob.RemoveIfExists(pollingData.JobId);
                    }
                    else
                    {
                        Logger.Info($"Polling: no result for {pollingData.ExternalAccountId} from a terminal {terminalToken}. Retry Counter {pollingData.RetryCounter}");
                        pollingData.RetryCounter++;
                        RecurringJob.AddOrUpdate(pollingData.JobId, () => SchedullerHelper.ExecuteSchedulledJob(pollingData, terminalToken), "*/" + pollingData.PollingIntervalInMinutes + " * * * *");
                    }

                }
            }
        }

        private static async Task<PollingDataDTO> RequestPolling(PollingDataDTO pollingData, string terminalToken, IRestfulServiceClient _client)
        {
            try
            {
                var terminalService = ObjectFactory.GetInstance<ITerminal>();

                using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
                {
                    var terminal = uow.TerminalRepository.GetQuery().FirstOrDefault(a => a.Secret == terminalToken);
                    string url = terminal.Endpoint + "/terminals/" + terminal.Name + "/polling_notifications";
                    Logger.Info($"Polling: executing request for {pollingData?.ExternalAccountId} from {Server.ServerUrl} to a terminal {terminal?.Name} at {terminal?.Endpoint}");

                    using (var client = new HttpClient())
                    {
                        foreach (var header in terminalService.GetRequestHeaders(terminal))
                        {
                            client.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }

                        try
                        {
                            var response = await _client.PostAsync<PollingDataDTO, PollingDataDTO>(new Uri(url), pollingData);

                            return response;
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}