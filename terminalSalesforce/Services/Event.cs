﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Crates;
using Data.Interfaces.DataTransferObjects;
using Data.Interfaces.Manifests;
using Hub.Managers;
using StructureMap;
using terminalSalesforce.Infrastructure;
using TerminalBase.BaseClasses;
using System.Linq;

namespace terminalSalesforce.Services
{
    public class Event : IEvent
    {
       
        private BaseTerminalController _baseTerminalController = new BaseTerminalController();
       

        public Event()
        {        
        }

        public Task<Crate> ProcessEvent(string curExternalEventPayload)
        {
            try
            {
                var curEventEnvelope = SalesforceNotificationParser.GetEnvelopeInformation(curExternalEventPayload);

                string accountId = string.Empty;

                if(curEventEnvelope.Body.Notifications.NotificationList != null && curEventEnvelope.Body.Notifications.NotificationList.Length > 0)
                {
                    accountId = curEventEnvelope.Body.Notifications.NotificationList[0].SObject.OwnerId;
                }

                //prepare the content from the external event payload            
                var eventReportContent = new EventReportCM
                {
                    EventNames = GetEventNames(curEventEnvelope),
                    ContainerDoId = "",
                    EventPayload = ExtractEventPayload(curEventEnvelope),
                    ExternalAccountId = accountId,
                    Manufacturer = "Salesforce",
                };

                return Task.FromResult(Crate.FromContent("Standard Event Report", eventReportContent));
            }
            catch (Exception e)
            {
                _baseTerminalController.ReportTerminalError("terminalSalesforce", e);
                throw new Exception(string.Format("Error while processing. \r\n{0}", curExternalEventPayload));
            }
        }

        private string GetEventNames(Envelope curEventEnvelope)
        {
            List<string> result = new List<string>();

            result = curEventEnvelope.Body.Notifications.NotificationList.ToList().Select(notification =>
            {
                return ExtractOccuredEvent(notification);
            }).ToList();

            return string.Join(",", result);
        }

        private ICrateStorage ExtractEventPayload(Envelope curEventEnvelope)
        {
            var stroage = new CrateStorage();

            var payloadDataCM = new StandardPayloadDataCM();
            foreach (var curNotification in curEventEnvelope.Body.Notifications.NotificationList)
            {
                payloadDataCM.PayloadObjects.Add(new PayloadObjectDTO(CreateKeyValuePairList(curNotification)));
            }

            var payloadCrate = Crate.FromContent("Salesforce Event Notification Payload", payloadDataCM);
            stroage.Add(payloadCrate);

            return stroage;
        }

        private List<FieldDTO> CreateKeyValuePairList(Notification curNotification)
        {
            var returnList = new List<FieldDTO>();

            returnList.Add(new FieldDTO("ObjectType", curNotification.SObject.Type.Substring(curNotification.SObject.Type.LastIndexOf(':') + 1)));
            returnList.Add(new FieldDTO("Id", curNotification.SObject.Id));
            returnList.Add(new FieldDTO("CreatedDate", curNotification.SObject.CreatedDate.ToString()));
            returnList.Add(new FieldDTO("LastModifiedDate", curNotification.SObject.LastModifiedDate.ToString()));
            returnList.Add(new FieldDTO("OccuredEvent", ExtractOccuredEvent(curNotification)));


            return returnList;
        }

        private static string ExtractOccuredEvent(Notification notification)
        {
            if (notification.SObject.CreatedDate.Equals(notification.SObject.LastModifiedDate))
            {
                return notification.SObject.Type.Substring(notification.SObject.Type.LastIndexOf(':') + 1) + "Created";
            }
            else if (notification.SObject.CreatedDate < notification.SObject.LastModifiedDate)
            {
                return notification.SObject.Type.Substring(notification.SObject.Type.LastIndexOf(':') + 1) + "Updated";
            }
            else
            {
                throw new InvalidOperationException("Not able to detect the Salesforce event notification reason.");
            }
        }
    }
}