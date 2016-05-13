﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Data.Entities;
using Fr8Data.Constants;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Manifests;
using Fr8Data.States;
using Hub.Managers;
using StructureMap.Diagnostics;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;

namespace terminalFr8Core.Activities
{
    public class StoreFile_v1 : BaseTerminalActivity
    {
        public override ConfigurationRequestType ConfigurationEvaluator(ActivityDO curActivityDO)
        {
            if (CrateManager.IsStorageEmpty(curActivityDO))
            {
                return ConfigurationRequestType.Initial;
            }
            var controlsMS = CrateManager.GetStorage(curActivityDO).CrateContentsOfType<StandardConfigurationControlsCM>().FirstOrDefault();

            if (controlsMS == null)
            {
                return ConfigurationRequestType.Initial;
            }

            return ConfigurationRequestType.Followup;
        }


        public override async Task<ActivityDO> Configure(ActivityDO curActivityDO, AuthorizationTokenDO authTokenDO)
        {
            return await ProcessConfigurationRequest(curActivityDO, ConfigurationEvaluator, authTokenDO);
        }

        protected override async Task<ActivityDO> InitialConfigurationResponse(ActivityDO curActivityDO, AuthorizationTokenDO authTokenDO)
        {
            //build a controls crate to render the pane
            var configurationControlsCrate = CreateControlsCrate();

            using (var crateStorage = CrateManager.GetUpdatableStorage(curActivityDO))
            {
                crateStorage.Replace(AssembleCrateStorage(configurationControlsCrate));
                await UpdateUpstreamFileCrates(curActivityDO, crateStorage);
            }

            return curActivityDO;
        }

        public async Task<PayloadDTO> Run(ActivityDO curActivityDO, Guid containerId, AuthorizationTokenDO authTokenDO)
        {
            
        }

        private MemoryStream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public async Task UpdateUpstreamFileCrates(ActivityDO curActivityDO, ICrateStorage storage)
        {
            // Build a crate with the list of available upstream fields
            var curUpstreamFieldsCrate = storage.SingleOrDefault(c => c.ManifestType.Id == (int)MT.FieldDescription
                                                                                && c.Label == "Upstream Terminal-Provided File Crates");
            if (curUpstreamFieldsCrate != null)
            {
                storage.Remove(curUpstreamFieldsCrate);
            }

            var upstreamFileCrates = await GetCratesByDirection<StandardFileDescriptionCM>(CrateDirection.Upstream);

            var curUpstreamFields = upstreamFileCrates.Select(c => new FieldDTO(c.Label, c.Label)).ToArray();

            curUpstreamFieldsCrate = CrateManager.CreateDesignTimeFieldsCrate("Upstream Terminal-Provided File Crates", curUpstreamFields);
            storage.Add(curUpstreamFieldsCrate);
            
        }

        private Crate CreateControlsCrate()
        {
            var fileNameTextBox = new TextBox
            {
                Label = "Name of file",
                Name = "File_Name"
            };
            var textSource = new TextSource("File Crate Label", "Upstream Terminal-Provided File Crates", "File Crate label");
            return PackControlsCrate(fileNameTextBox, textSource);
        }

        public StoreFile_v1() : base(false)
        {
        }

        protected override ActivityTemplateDTO MyTemplate { get; }
        public override async Task Run()
        {

            var textSourceControl = GetControl<TextSource>("File Crate label");
            var fileNameField = GetControl<TextBox>("File_Name");
            var fileCrateLabel = textSourceControl.GetValue(Payload);
            if (string.IsNullOrEmpty(fileCrateLabel))
            {
                return Error(curPayloadDTO, "No Label was selected on design time", ActivityErrorCode.DESIGN_TIME_DATA_MISSING);
            }
            if (string.IsNullOrEmpty(fileNameField.Value))
            {
                return Error(curPayloadDTO, "No file name was given on design time", ActivityErrorCode.DESIGN_TIME_DATA_MISSING);
            }


            //we should upload this file to our file storage
            var userSelectedFileManifest = payloadStorage.CrateContentsOfType<StandardFileDescriptionCM>(f => f.Label == fileCrateLabel).FirstOrDefault();
            if (userSelectedFileManifest == null)
            {
                return Error(curPayloadDTO, "No StandardFileDescriptionCM Crate was found with label " + fileCrateLabel, ActivityErrorCode.PAYLOAD_DATA_MISSING);
            }


            var fileContents = userSelectedFileManifest.TextRepresentation;

            using (var stream = GenerateStreamFromString(fileContents))
            {
                //TODO what to do with this fileDO??
                var fileDO = await HubCommunicator.SaveFile(fileNameField.Value, stream, CurrentUserId);
            }


            Success();
        }

        public override Task Initialize()
        {
        }

        public override Task FollowUp()
        {
        }
    }
}