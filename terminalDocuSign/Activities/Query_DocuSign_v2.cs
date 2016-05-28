﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fr8Data.Constants;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Managers;
using Fr8Data.Manifests;
using Fr8Data.States;
using StructureMap;
using terminalDocuSign.Activities;
using terminalDocuSign.Infrastructure;
using terminalDocuSign.Interfaces;
using terminalDocuSign.Services;
using terminalDocuSign.Services.New_Api;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;
using FolderItem = DocuSign.eSign.Model.FolderItem;
using ListItem = Fr8Data.Control.ListItem;

namespace terminalDocuSign.Activities
{
    public class Query_DocuSign_v2 : EnhancedDocuSignActivity<Query_DocuSign_v2.ActivityUi>
    {
        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Name = "Query_DocuSign",
            Label = "Query DocuSign",
            Version = "2",
            Category = ActivityCategory.Receivers,
            NeedsAuthentication = true,
            MinPaneWidth = 380,
            WebService = TerminalData.WebServiceDTO,
            Terminal = TerminalData.TerminalDTO
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        public class ActivityUi : StandardConfigurationControlsCM
        {
            public TextArea IntroductionText { get; set; }

            public TextBox SearchTextFilter { get; set; }

            public DropDownList FolderFilter { get; set; }

            public DropDownList StatusFilter { get; set; }

            public ActivityUi()
            {
                IntroductionText = new TextArea
                {
                    Name = nameof(IntroductionText),
                    IsReadOnly = true,
                    Value = "<p>Search for DocuSign Envelopes where the following are true:</p><div>Envelope contains text:</div>"
                };
                SearchTextFilter = new TextBox
                {
                    Name = nameof(SearchTextFilter)
                };
                FolderFilter = new DropDownList
                {
                    Label = "Envelope is in folder:",
                    Name = nameof(FolderFilter)
                };
                StatusFilter = new DropDownList
                {
                    Label = "Envelope has status:",
                    Name = nameof(StatusFilter),
                };
                Controls.Add(IntroductionText);
                Controls.Add(SearchTextFilter);
                Controls.Add(FolderFilter);
                Controls.Add(StatusFilter);
            }
        }

        protected readonly IDocuSignFolders DocuSignFolders;

        public Query_DocuSign_v2(ICrateManager crateManager, IDocuSignManager docuSignManager, IDocuSignFolders docuSignFolders)
            : base(crateManager, docuSignManager)
        {
            DocuSignFolders = docuSignFolders;
        }

        private static IEnumerable<FieldDTO> GetEnvelopeProperties()
        {
            var properties = typeof(DocuSignEnvelopeDTO).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                        .Where(x => x.CanRead && x.CanWrite);
            return properties.Select(x => new FieldDTO(x.Name, x.Name, AvailabilityType.Always)).ToArray();
        }

        private const string RunTimeCrateLabel = "DocuSign Envelope Data";

        public override async Task Initialize()
        {
            var configuration = DocuSignManager.SetUp(AuthorizationToken);
            ActivityUI.FolderFilter.ListItems = DocuSignFolders.GetFolders(configuration)
                                                                          .Select(x => new ListItem { Key = x.Key, Value = x.Value })
                                                                          .ToList();
            ActivityUI.FolderFilter.ListItems.Insert(0, new ListItem { Key = "Any Folder", Value = string.Empty });
            ActivityUI.StatusFilter.ListItems = DocuSignQuery.Statuses
                                                                        .Select(x => new ListItem { Key = x.Key, Value = x.Value })
                                                                        .ToList();
            CrateSignaller.MarkAvailableAtRuntime<DocuSignEnvelopeCM_v3>(RunTimeCrateLabel)
                          .AddFields(GetEnvelopeProperties());
        }

        public override Task FollowUp()
        {
            return Task.FromResult(0);
        }

        public override async Task Run()
        {
            var configuration = DocuSignManager.SetUp(AuthorizationToken);
            var settings = GetDocusignQuery();
            var folderItems = DocuSignFolders.GetFolderItems(configuration, settings);
            Payload.Add(Crate.FromContent(RunTimeCrateLabel, new DocuSignEnvelopeCM_v3
                                                                           {
                                                                               Envelopes = folderItems.Select(ConvertFolderItemToDocuSignEnvelope).ToList()
                                                                           }));
        }

        private DocuSignQuery GetDocusignQuery()
        {
            return new DocuSignQuery
            {
                Folder = ActivityUI.FolderFilter.Value,
                Status = ActivityUI.StatusFilter.Value,
                SearchText = ActivityUI.SearchTextFilter.Value
            };
        }

        private DocuSignEnvelopeDTO ConvertFolderItemToDocuSignEnvelope(FolderItem item)
        {
            return new DocuSignEnvelopeDTO
            {
                Name = item.Name,
                CompletedDate = DateTimeHelper.Parse(item.CompletedDateTime),
                CreateDate = DateTimeHelper.Parse(item.CreatedDateTime),
                SentDate = DateTimeHelper.Parse(item.SentDateTime),
                Description = item.Description,
                TemplateId = item.TemplateId,
                Status = item.Status,
                SenderName = item.SenderName,
                Subject = item.Subject,
                EnvelopeId = item.EnvelopeId,
                SenderEmail = item.SenderEmail,
                Shared = item.Shared,
                OwnerName = item.OwnerName
            };
        }
    }
}