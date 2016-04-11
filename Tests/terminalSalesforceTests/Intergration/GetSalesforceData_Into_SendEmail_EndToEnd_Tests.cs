﻿using System;
using HealthMonitor.Utility;
using NUnit.Framework;
using System.Threading.Tasks;
using Data.Interfaces.DataTransferObjects;
using System.Web.Http;
using StructureMap;
using Data.Interfaces;
using System.Linq;
using Data.Entities;
using System.Collections.Generic;
using Hub.Managers;
using Data.Interfaces.Manifests;
using Data.Crates;
using Data.Control;
using Newtonsoft.Json;
using System.Diagnostics;

namespace terminalSalesforceTests.Intergration
{
    [Explicit]
    [Category("terminalSalesforceTests.Integration")]
    public class GetSalesforceData_Into_SendEmail_EndToEnd_Tests : BaseHubIntegrationTest
    {
        public override string TerminalName
        {
            get { return "terminalSalesforce"; }
        }

        [Test]
        public async Task GetSalesforceData_Into_SendEmail_EndToEnd()
        {
            AuthorizationTokenDO authTokenDO = null;
            Guid initialPlanId = Guid.Empty;
            try
            {
                authTokenDO = await Fixtures.HealthMonitor_FixtureData.CreateSalesforceAuthToken();

                //Create the required plan
                initialPlanId = await CreatePlan_GetSalesforceDataIntoSendEmail(authTokenDO);

                //get the full plan which is created
                var plan = await HttpGetAsync<PlanDTO>(_baseUrl + "Plans/full?id=" + initialPlanId.ToString());
                Debug.WriteLine("Created plan with all activities.");

                //make get salesforce data to get Lead
                var getData = plan.Plan.SubPlans.First().Activities.First();
                using (var updatableStorage = Crate.GetUpdatableStorage(getData))
                {
                    //select Lead
                    var configControls = updatableStorage.CratesOfType<StandardConfigurationControlsCM>().Single();
                    (configControls.Content.Controls.Single(c => c.Name.Equals("WhatKindOfData")) as DropDownList).selectedKey = "Lead";

                    //give condition
                    var conditionQuery = new List<FilterConditionDTO>() { new FilterConditionDTO { Field = "Full Name", Operator = "eq", Value = "Marty McSorely" } };
                    (configControls.Content.Controls.Single(c => c.Name.Equals("SelectedQuery")) as QueryBuilder).Value = JsonConvert.SerializeObject(conditionQuery);
                }
                getData = await ConfigureActivity(getData);
                Assert.IsTrue(getData.CrateStorage.Crates.Any(c => c.Label.Equals("Salesforce Object Fields - Lead")),
                              "Follow up configuration is not getting any Salesforce Object Fields");
                Debug.WriteLine("Get Lead using condition is successful in the Follow Up Configure");

                //prepare the send email activity controls.
                var sendEmail = plan.Plan.SubPlans.First().Activities.Last();
                using (var updatableStorage = Crate.GetUpdatableStorage(sendEmail))
                {
                    var configControls = updatableStorage.CratesOfType<StandardConfigurationControlsCM>().Single();

                    var emailAddressControl = (TextSource)configControls.Content.Controls.Single(c => c.Name.Equals("EmailAddress"));
                    var emailSubjectControl = (TextSource)configControls.Content.Controls.Single(c => c.Name.Equals("EmailSubject"));
                    var emailBodyControl = (TextSource)configControls.Content.Controls.Single(c => c.Name.Equals("EmailBody"));

                    emailAddressControl.ValueSource = "specific";
                    emailAddressControl.TextValue = "fr8.testing@yahoo.com";

                    emailSubjectControl.ValueSource = emailBodyControl.ValueSource = "upstream";
                    emailSubjectControl.selectedKey = "Name";
                    emailBodyControl.selectedKey = "Phone";
                }
                sendEmail = await ConfigureActivity(sendEmail);
                Debug.WriteLine("Send Email follow up configure is successful.");

                //Run the plan
                await HttpPostAsync<string, string>(_baseUrl + "plans/run?planId=" + plan.Plan.Id, null);
                Debug.WriteLine("Plan execution is successful.");

                await CleanUp(authTokenDO, initialPlanId);

                //Verify the email fr8.testing@yahoo.com
                EmailAssert.EmailReceived("fr8ops@fr8.company", "Marty McSorely", true);
            }
            finally
            {
                await CleanUp(authTokenDO, initialPlanId);
            }
        }

        private async Task<Guid> CreatePlan_GetSalesforceDataIntoSendEmail(AuthorizationTokenDO authToken)
        {
            //get required activity templates
            var activityTemplates = await HttpGetAsync<IEnumerable<ActivityTemplateCategoryDTO>>(_baseUrl + "plannodes/available");
            var getData = activityTemplates.Single(at => at.Name.Equals("Receivers")).Activities.Single(a => a.Name.Equals("Get_Data"));
            var sendEmail = activityTemplates.Single(at => at.Name.Equals("Forwarders")).Activities.Single(a => a.Name.Equals("SendEmailViaSendGrid"));
            Assert.IsNotNull(getData, "Get Salesforce Data activity is not available");
            Assert.IsNotNull(sendEmail, "Send Email activity is not available");
            Debug.WriteLine("Got required activity templates.");

            //create initial plan
            var initialPlan = await HttpPostAsync<PlanEmptyDTO, PlanDTO>(_baseUrl + "plans", new PlanEmptyDTO()
            {
                Name = "GetSalesforceData_Into_SendEmail_EndToEnd_Test"
            });
            Debug.WriteLine("Created initial plan without actions");

            string mainUrl = _baseUrl + "activities/create";
            var postUrl = "?actionTemplateId={0}&createPlan=false";
            var formattedPostUrl = string.Format(postUrl, getData.Id);
            formattedPostUrl += "&parentNodeId=" + initialPlan.Plan.StartingSubPlanId;
            formattedPostUrl += "&authorizationTokenId=" + authToken.Id.ToString();
            formattedPostUrl += "&order=" + 1;
            formattedPostUrl = mainUrl + formattedPostUrl;
            var getDataActivity = await HttpPostAsync<ActivityDTO>(formattedPostUrl, null);
            Assert.IsNotNull(getDataActivity, "Initial Create and Configure of Get Salesforce Data action is failed.");
            Debug.WriteLine("Create and Initial Configure of Get Salesforce Data activity is successful.");

            formattedPostUrl = string.Format(postUrl, sendEmail.Id);
            formattedPostUrl += "&parentNodeId=" + initialPlan.Plan.StartingSubPlanId;
            formattedPostUrl += "&order=" + 2;
            formattedPostUrl = mainUrl + formattedPostUrl;
            var sendEmailActivity = await HttpPostAsync<ActivityDTO>(formattedPostUrl, null);
            Assert.IsNotNull(sendEmailActivity, "Initial Create and Configure of Send Email action is failed.");
            Debug.WriteLine("Create and Initial Configure of Send Email activity is successful.");

            return initialPlan.Plan.Id;
        }

        private async Task CleanUp(AuthorizationTokenDO authTokenDO, Guid initialPlanId)
        {
            if (initialPlanId != Guid.Empty)
            {
                await HttpDeleteAsync(_baseUrl + "Plans/Delete?id=" + initialPlanId.ToString());
            }

            if (authTokenDO != null)
            {
                await HttpPostAsync<string>(_baseUrl + "manageauthtoken/revoke?id=" + authTokenDO.Id.ToString(), null);
            }
        }
    }
}
