﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fr8Data.Control;
using Fr8Data.Manifests;
using Fr8Data.States;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;
using TerminalBase.Models;

namespace TerminalBase.Helpers
{
    public class ControlHelper
    {
        private readonly IHubCommunicator _hubCommunicator;
        private readonly ActivityContext _activityContext;
        public ControlHelper(ActivityContext activityContext, IHubCommunicator hubCommunicator)
        {
            _hubCommunicator = hubCommunicator;
            _activityContext = activityContext;
        }

        /// <summary>
        /// This is a generic function for creating a CrateChooser which is suitable for most use-cases
        /// </summary>
        /// <param name="curTerminalActivity"></param>
        /// <param name="label"></param>
        /// <param name="name"></param>
        /// <param name="singleManifest"></param>
        /// <param name="requestUpstream"></param>
        /// <param name="requestConfig"></param>
        /// <returns></returns>
        public async Task<CrateChooser> GenerateCrateChooser(
            string name,
            string label,
            bool singleManifest,
            bool requestUpstream = false,
            bool requestConfig = false)
        {
            var crateDescriptions = await _hubCommunicator.GetCratesByDirection<CrateDescriptionCM>(_activityContext.ActivityPayload.Id, CrateDirection.Upstream, _activityContext.UserId);
            var runTimeCrateDescriptions = crateDescriptions.Where(c => c.Availability == AvailabilityType.RunTime || c.Availability == AvailabilityType.Always).SelectMany(c => c.Content.CrateDescriptions);
            var control = new CrateChooser
            {
                Label = label,
                Name = name,
                CrateDescriptions = runTimeCrateDescriptions.ToList(),
                SingleManifestOnly = singleManifest,
                RequestUpstream = requestUpstream
            };

            if (requestConfig)
            {
                control.Events.Add(new ControlEvent("onChange", "requestConfig"));
            }

            return control;
        }

        /// <summary>
        /// Creates TextBlock control and fills it with label, value and CssClass
        /// </summary>
        /// <param name="curLabel">Label</param>
        /// <param name="curValue">Value</param>
        /// <param name="curCssClass">Css Class</param>
        /// <param name="curName">Name</param>
        /// <returns>TextBlock control</returns>
        public TextBlock GenerateTextBlock(string curLabel, string curValue, string curCssClass, string curName = "unnamed")
        {
            return new TextBlock
            {
                Name = curName,
                Label = curLabel,
                Value = curValue,
                CssClass = curCssClass
            };
        }

        public UpstreamCrateChooser CreateUpstreamCrateChooser(string name, string label, bool isMultiSelection = true)
        {

            var manifestDdlb = new DropDownList { Name = name + "_mnfst_dropdown_0", Source = new FieldSourceDTO(CrateManifestTypes.StandardDesignTimeFields, "AvailableUpstreamManifests") };
            var labelDdlb = new DropDownList { Name = name + "_lbl_dropdown_0", Source = new FieldSourceDTO(CrateManifestTypes.StandardDesignTimeFields, "AvailableUpstreamLabels") };

            var ctrl = new UpstreamCrateChooser
            {
                Name = name,
                Label = label,
                SelectedCrates = new List<CrateDetails> { new CrateDetails { Label = labelDdlb, ManifestType = manifestDdlb } },
                MultiSelection = isMultiSelection
            };

            return ctrl;
        }
    }
}
