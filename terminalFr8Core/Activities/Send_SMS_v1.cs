﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Infrastructure;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Managers;
using Fr8Data.Manifests;
using Fr8Data.States;
using PhoneNumbers;
using StructureMap;
using terminalUtilities.Twilio;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;
using Twilio;

namespace terminalFr8Core.Activities
{
    public class Send_SMS_v1 : EnhancedTerminalActivity<Send_SMS_v1.ActivityUi>
    {
        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Name = "Send_SMS",
            Label = "Send SMS using Fr8 core account",
            Version = "1",
            Category = ActivityCategory.Forwarders,
            NeedsAuthentication = false,
            MinPaneWidth = 400,
            WebService = TerminalData.WebServiceDTO,
            Terminal = TerminalData.TerminalDTO
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        private ITwilioService _twilio;

        public class ActivityUi : StandardConfigurationControlsCM
        {
            public TextSource SmsNumber { get; set; }
            public TextSource SmsBody { get; set; }

            public ActivityUi()
            {
                SmsNumber = new TextSource("SMS Number", string.Empty, nameof(SmsNumber))
                {
                    Source = new FieldSourceDTO
                    {
                        Label = string.Empty,
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields,
                        FilterByTag = string.Empty,
                        RequestUpstream = true
                    }
                };
                SmsNumber.Events.Add(new ControlEvent("onChange", "requestConfig"));

                SmsBody = new TextSource("SMS Body", string.Empty, nameof(SmsBody))
                {
                    Source = new FieldSourceDTO
                    {
                        Label = string.Empty,
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields,
                        FilterByTag = string.Empty,
                        RequestUpstream = true
                    }
                };
                SmsBody.Events.Add(new ControlEvent("onChange", "requestConfig"));

                Controls = new List<ControlDefinitionDTO> { SmsNumber, SmsBody };
            }
        }

        public Send_SMS_v1(ICrateManager crateManager)
            : base(false, crateManager)
        {
            _twilio = ObjectFactory.GetInstance<ITwilioService>();
        }

        protected override async Task InitializeETA()
        {
            Storage.Add(await CreateAvailableFieldsCrate());
        }

        protected override async Task ConfigureETA()
        {
            Storage.RemoveByLabel("Upstream Terminal-Provided Fields");
            Storage.Add(await CreateAvailableFieldsCrate());
        }

        /// <summary>
        /// Creates a crate with available design-time fields.
        /// </summary>
        /// <param name="terminalActivity">ActionDO.</param>
        /// <returns></returns>
        protected async Task<Crate> CreateAvailableFieldsCrate()
        {
            var curUpstreamFields = await HubCommunicator.GetDesignTimeFieldsByDirection(ActivityId, CrateDirection.Upstream, AvailabilityType.RunTime) ?? new FieldDescriptionsCM();

            var availableFieldsCrate = CrateManager.CreateDesignTimeFieldsCrate(
                    "Upstream Terminal-Provided Fields",
                    curUpstreamFields.Fields,
                    AvailabilityType.Configuration);

            return availableFieldsCrate;
        }

        protected override async Task RunETA()
        {
            Message curMessage;
            try
            {
                FieldDTO smsFieldDTO = ParseSMSNumberAndMsg();
                string smsNumber = smsFieldDTO.Key;
                string smsBody = smsFieldDTO.Value + "\nThis message was generated by Fr8. http://www.fr8.co";

                try
                {
                    curMessage = _twilio.SendSms(smsNumber, smsBody);
                    EventManager.TwilioSMSSent(smsNumber, smsBody);
                    var curFieldDTOList = CreateKeyValuePairList(curMessage);
                    Payload.Add(Crate.FromContent("Message Data", new StandardPayloadDataCM(curFieldDTOList)));
                }
                catch (Exception ex)
                {
                    EventManager.TwilioSMSSendFailure(smsNumber, smsBody, ex.Message);
                    RaiseError( "Twilio Service Failure due to " + ex.Message);
                }
            }
            catch (ArgumentException appEx)
            {
                RaiseError(appEx.Message);
            }
        }

        public FieldDTO ParseSMSNumberAndMsg()
        {
            var smsNumber = GeneralisePhoneNumber(ActivityUI.SmsNumber.GetValue(Payload).Trim());
            var smsBody = ActivityUI.SmsBody.GetValue(Payload);

            return new FieldDTO(smsNumber, smsBody);
        }

        private string GeneralisePhoneNumber(string smsNumber)
        {
            PhoneNumberUtil phoneUtil = PhoneNumberUtil.GetInstance();
            smsNumber = new string(smsNumber.Where(s => char.IsDigit(s) || s == '+' || (phoneUtil.IsAlphaNumber(smsNumber) && char.IsLetter(s))).ToArray());
            if (smsNumber.Length == 10 && !smsNumber.Contains("+"))
                smsNumber = "+1" + smsNumber; //we assume that default region is USA
            return smsNumber;
        }

        private List<FieldDTO> CreateKeyValuePairList(Message curMessage)
        {
            List<FieldDTO> returnList = new List<FieldDTO>();
            returnList.Add(new FieldDTO("Status", curMessage.Status));
            returnList.Add(new FieldDTO("ErrorMessage", curMessage.ErrorMessage));
            returnList.Add(new FieldDTO("Body", curMessage.Body));
            returnList.Add(new FieldDTO("ToNumber", curMessage.To));
            return returnList;
        }
    }
}