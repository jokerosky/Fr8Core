﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr8Data.Constants;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Managers;
using Fr8Data.Manifests;
using Fr8Data.States;
using Newtonsoft.Json;
using ServiceStack;
using StructureMap;
using TerminalBase.BaseClasses;
using terminalSalesforce.Infrastructure;
using TerminalBase.Errors;
using TerminalBase.Infrastructure;

namespace terminalSalesforce.Actions
{
    public class Get_Data_v1 : BaseSalesforceTerminalActivity<Get_Data_v1.ActivityUi>
    {
        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Version = "1",
            Name = "Get_Data",
            Label = "Get Data from Salesforce",
            NeedsAuthentication = true,
            Category = ActivityCategory.Receivers,
            MinPaneWidth = 550,
            Tags = Tags.TableDataGenerator,
            WebService = TerminalData.WebServiceDTO,
            Terminal = TerminalData.TerminalDTO
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        public class ActivityUi : StandardConfigurationControlsCM
        {
            public DropDownList SalesforceObjectSelector { get; set; }

            public QueryBuilder SalesforceObjectFilter { get; set; }

            public ActivityUi()
            {
                SalesforceObjectSelector = new DropDownList
                {
                    Name = nameof(SalesforceObjectSelector),
                    Label = "Get all objects of type:",
                    Required = true,
                    Events = new List<ControlEvent> {  ControlEvent.RequestConfig }
                };
                SalesforceObjectFilter = new QueryBuilder
                {
                    Name = nameof(SalesforceObjectFilter),
                    Label = "That meet the following conditions:",
                    Required = true,
                    Source = new FieldSourceDTO
                    {
                        Label = QueryFilterCrateLabel,
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    }
                };
                Controls.Add(SalesforceObjectSelector);
                Controls.Add(SalesforceObjectFilter);
            }
        }
        //NOTE: this label must be the same as the one expected in QueryBuilder.ts
        public const string QueryFilterCrateLabel = "Queryable Criteria";

        public const string RuntimeDataCrateLabel = "Table from Salesforce Get Data";
        public const string PayloadDataCrateLabel = "Payload from Salesforce Get Data";

        public const string SalesforceObjectFieldsCrateLabel = "Salesforce Object Fields";

        private readonly ISalesforceManager _salesforceManager;

        public Get_Data_v1(ICrateManager crateManager, ISalesforceManager salesforceManager)
            : base(crateManager)
        {
            _salesforceManager = salesforceManager;
        }

        protected override Task InitializeETA()
        {
            ActivityUI.SalesforceObjectSelector.ListItems = _salesforceManager
                .GetSalesforceObjectTypes()
                .Select(x => new ListItem() { Key = x.Key, Value = x.Key })
                .ToList();
            CrateSignaller.MarkAvailableAtRuntime<StandardTableDataCM>(RuntimeDataCrateLabel);
            return Task.FromResult(true);
        }

        protected override async Task ConfigureETA()
        {
            //If Salesforce object is empty then we should clear filters as they are no longer applicable
            var selectedObject = ActivityUI.SalesforceObjectSelector.selectedKey;
            if (string.IsNullOrEmpty(selectedObject))
            {
                Storage.RemoveByLabel(QueryFilterCrateLabel);
                Storage.RemoveByLabel(SalesforceObjectFieldsCrateLabel);
                this[nameof(ActivityUi.SalesforceObjectSelector)] = selectedObject;
                return;
            }
            //If the same object is selected we shouldn't do anything
            if (selectedObject == this[nameof(ActivityUi.SalesforceObjectSelector)])
            {
                return;
            }
            //Prepare new query filters from selected object properties
            var selectedObjectProperties = await _salesforceManager
                .GetProperties(selectedObject.ToEnum<SalesforceObjectType>(), AuthorizationToken);
            var queryFilterCrate = Crate<FieldDescriptionsCM>.FromContent(
                QueryFilterCrateLabel,
                new FieldDescriptionsCM(selectedObjectProperties),
                AvailabilityType.Configuration);
            Storage.ReplaceByLabel(queryFilterCrate);


            var objectPropertiesCrate = Crate<FieldDescriptionsCM>.FromContent(
            SalesforceObjectFieldsCrateLabel,
            new FieldDescriptionsCM(selectedObjectProperties.Select(c => new FieldDTO(c.Key, c.Key) { SourceCrateLabel = RuntimeDataCrateLabel })),
            AvailabilityType.RunTime);
            Storage.ReplaceByLabel(objectPropertiesCrate);

            this[nameof(ActivityUi.SalesforceObjectSelector)] = selectedObject;
            //Publish information for downstream activities
            CrateSignaller.MarkAvailableAtRuntime<StandardTableDataCM>(RuntimeDataCrateLabel);
        }

        protected override async Task RunETA()
        {
            var salesforceObject = ActivityUI.SalesforceObjectSelector.selectedKey;
            if (string.IsNullOrEmpty(salesforceObject))
            {
                throw new ActivityExecutionException(
                    "No Salesforce object is selected", 
                    ActivityErrorCode.DESIGN_TIME_DATA_MISSING);
            }
            var salesforceObjectFields = Storage
                .FirstCrate<FieldDescriptionsCM>(x => x.Label == QueryFilterCrateLabel)
                .Content
                .Fields
                .Select(x => x.Key);

            var filterValue = ActivityUI.SalesforceObjectFilter.Value;
            var filterDataDTO = JsonConvert.DeserializeObject<List<FilterConditionDTO>>(filterValue);
            //If without filter, just get all selected objects
            //else prepare SOQL query to filter the objects based on the filter conditions
            var parsedCondition = string.Empty;
            if (filterDataDTO.Count > 0)
            {
                parsedCondition = ControlHelper.ParseConditionToText(filterDataDTO);
            }

            var resultObjects = await _salesforceManager
                .Query(
                    salesforceObject.ToEnum<SalesforceObjectType>(),
                    salesforceObjectFields,
                    parsedCondition,
                    AuthorizationToken
                );

            Payload.Add(
                Crate<StandardPayloadDataCM>
                    .FromContent(
                        PayloadDataCrateLabel,
                        resultObjects.ToPayloadData(),
                        AvailabilityType.RunTime
                    )
            );

            Payload.Add(
                Crate<StandardTableDataCM>
                    .FromContent(
                        RuntimeDataCrateLabel,
                        resultObjects,
                        AvailabilityType.RunTime
                    )
                );
        }
    }
}