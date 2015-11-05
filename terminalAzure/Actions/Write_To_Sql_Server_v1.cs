﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Data.Crates;
using Newtonsoft.Json;
using StructureMap;
using Data.Entities;
using Data.Interfaces;
using Data.Interfaces.Manifests;
using Data.Interfaces.DataTransferObjects;
using Hub.Managers;
using TerminalBase;
using TerminalBase.BaseClasses;
using TerminalBase.Infrastructure;
using TerminalSqlUtilities;
using terminalAzure.Infrastructure;
using terminalAzure.Services;

namespace terminalAzure.Actions
{

    public class Write_To_Sql_Server_v1 : BasePluginAction
    {

        //================================================================================
        //General Methods (every Action class has these)

        //maybe want to return the full Action here
        public async Task<ActionDTO> Configure(ActionDTO curActionDTO)
        {
            return await ProcessConfigurationRequest(curActionDTO, EvaluateReceivedRequest);
        }

        //this entire function gets passed as a delegate to the main processing code in the base class
        //currently many actions have two stages of configuration, and this method determines which stage should be applied
        private ConfigurationRequestType EvaluateReceivedRequest(ActionDTO curActionDTO)
        {
            if (Crate.IsEmptyStorage(curActionDTO.CrateStorage))
                return ConfigurationRequestType.Initial;

            //load configuration crates of manifest type Standard Control Crates
            //look for a text field name connection string with a value

            var storage = Crate.GetStorage(curActionDTO);

            var connectionStrings = storage.CratesOfType<StandardConfigurationControlsCM>().Select(x => x.Value.FindByName("connection_string")).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Value)).ToArray();

            //if there are more than 2 return connection strings, something is wrong
            //if there are none or if there's one but it's value is "" the return initial else return followup
            var objCount = connectionStrings.Length;
            if (objCount > 1)
                throw new ArgumentException("didn't expect to see more than one connectionStringObject with the name Connection String on this Action", "curActionDTO");
            if (objCount == 0)
                return ConfigurationRequestType.Initial;
            else
            {
                //we should validate our data now
                //CheckFields(curActionDTO, new List<ValidationDataTuple> { new ValidationDataTuple("connection_string", "test", GetCrateDirection.Upstream, CrateManifests.DESIGNTIME_FIELDS_MANIFEST_NAME) });
                return ConfigurationRequestType.Followup;
            }            
        }

        //If the user provides no Connection String value, provide an empty Connection String field for the user to populate
        protected override async Task<ActionDTO> InitialConfigurationResponse(ActionDTO curActionDTO)
        {
            using (var updater = Crate.UpdateStorage(curActionDTO))
            {
                updater.CrateStorage.Add(CreateControlsCrate());
            }

            return await Task.FromResult<ActionDTO>(curActionDTO);
        }

        private Crate CreateControlsCrate()
        { 
            // "[{ type: 'textField', name: 'connection_string', required: true, value: '', fieldLabel: 'SQL Connection String' }]"
            var control = new TextBoxControlDefinitionDTO()
            {
                Label = "SQL Connection String",
                Name = "connection_string",
                Required = true,
                Events = new List<ControlEvent>() { new ControlEvent("onChange", "requestConfig") }
            };
            return PackControlsCrate(control);
        }

        //if the user provides a connection string, this action attempts to connect to the sql server and get its columns and tables
        protected override async Task<ActionDTO> FollowupConfigurationResponse(ActionDTO curActionDTO)
        {
            //In all followup calls, update data fields of the configuration store          
            List<String> contentsList = GetFieldMappings(curActionDTO);

            using (var updater = Crate.UpdateStorage(curActionDTO))
            {
                updater.CrateStorage.RemoveByLabel("Sql Table Columns");
                //this needs to be updated to hold Crates instead of FieldDefinitionDTO
                updater.CrateStorage.Add(Crate.CreateDesignTimeFieldsCrate("Sql Table Columns", contentsList.Select(col => new FieldDTO() {Key = col, Value = col}).ToArray()));
                }

            return await Task.FromResult<ActionDTO>(curActionDTO);
        }

        public object Activate(ActionDO curActionDO)
        {
            //not currently any requirements that need attention at Activation Time
            return null;
        }

        public object Deactivate(ActionDO curActionDO)
        {
            return "Deactivated";
        }

        public async Task<PayloadDTO> Run(ActionDTO actionDto)
        {
            var processPayload = await GetProcessPayload(actionDto.ProcessId);

            var curCommandArgs = PrepareSQLWrite(actionDto, processPayload);

            var dbService = new DbService();
            dbService.WriteCommand(curCommandArgs);

            return processPayload;
        }

        //===============================================================================================
        //Specialized Methods (Only found in this Action class)

        private const string ProviderName = "System.Data.SqlClient";
        private const string FieldMappingQuery = @"SELECT CONCAT('[', r.NAME, '].', r.COLUMN_NAME) as tblcols " +
                                                 @"FROM ( " +
                                                    @"SELECT DISTINCT tbls.NAME, cols.COLUMN_NAME " +
                                                    @"FROM sys.Tables tbls, INFORMATION_SCHEMA.COLUMNS cols " +
                                                 @") r " +
                                                 @"ORDER BY r.NAME, r.COLUMN_NAME";


        //CONFIGURATION-Related Methods
        //-----------------------------------------

        public List<string> GetFieldMappings(ActionDTO curActionDTO)
        {
            //Get configuration settings and check for connection string
            //CrateStorageDTO curCrates = curActionDTO.CrateStorage;

            var storage = Crate.GetStorage(curActionDTO);

            if (storage.Count == 0)
            {
                throw new PluginCodedException(PluginErrorCode.SQL_SERVER_CONNECTION_STRING_MISSING);
            }

            var confControls = storage.CrateValuesOfType<StandardConfigurationControlsCM>().FirstOrDefault();

            if (confControls == null)
            {
                throw new PluginCodedException(PluginErrorCode.SQL_SERVER_CONNECTION_STRING_MISSING);
            }

            var connStringField = confControls.Controls.First();
            if (connStringField == null || String.IsNullOrEmpty(connStringField.Value))
            {
                throw new PluginCodedException(PluginErrorCode.SQL_SERVER_CONNECTION_STRING_MISSING);
            }

            var curProvider = ObjectFactory.GetInstance<IDbProvider>();

            return (List<string>)curProvider.ConnectToSql(connStringField.Value, (command) =>
            {
                command.CommandText = FieldMappingQuery;

                //The list to serialize
                List<string> tableMetaData = new List<string>();

                //Iterate through each entry from the const query
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tableMetaData.Add(reader["tblcols"].ToString());
                    }
                }

                //Serialize and return
                return tableMetaData;
            });
        }

        //EXECUTION-Related Methods
        //-----------------------------------------
        private WriteCommandArgs PrepareSQLWrite(ActionDTO curActionDTO, PayloadDTO processPayload)
        {
            var parser = new DbServiceJsonParser();
            var curConnStringObject = parser.ExtractConnectionString(curActionDTO);
            var curSQLData = ConvertProcessPayloadToSqlInputs(processPayload);

            return new WriteCommandArgs(ProviderName, curConnStringObject, curSQLData);
        }

        private IEnumerable<Table> ConvertProcessPayloadToSqlInputs(PayloadDTO processPayload)
        {
            var mappedFieldsCrate = Crate.GetStorage(processPayload).CratesOfType<StandardPayloadDataCM>().FirstOrDefault(x => x.Label == "MappedFields");

//            var mappedFieldsCrate = processPayload.CrateStorageDTO()
//                .CrateDTO
//                .Where(x => x.Label == "MappedFields"
//                    && x.ManifestType == CrateManifests.STANDARD_PAYLOAD_MANIFEST_NAME)
//                .FirstOrDefault();

            if (mappedFieldsCrate == null)
            {
                throw new ApplicationException("No payload crate found with Label == MappdFields.");
            }

            var valuesCrate = Crate.GetStorage(processPayload).CratesOfType<StandardPayloadDataCM>().FirstOrDefault(x => x.Label == "DocuSign Envelope Data");
//
//            var valuesCrate = processPayload.CrateStorageDTO()
//                .CrateDTO
//                .Where(x => x.ManifestType == CrateManifests.STANDARD_PAYLOAD_MANIFEST_NAME
//                    && x.Label == "DocuSign Envelope Data")
//                .FirstOrDefault();

            if (valuesCrate == null)
            {
                throw new ApplicationException("No payload crate found with Label == DocuSign Envelope Data");
            }

//            var mappedFields = mappedFieldsCrate.Value.AllValues();// JsonConvert.DeserializeObject<List<FieldDTO>>(mappedFieldsCrate.Contents);
//            var values = JsonConvert.DeserializeObject<List<FieldDTO>>(valuesCrate.Contents);

            return CreateTables(mappedFieldsCrate.Value.AllValues().ToList(), valuesCrate.Value.AllValues().ToList());
        }

        private IEnumerable<Table> CreateTables(List<FieldDTO> fields, List<FieldDTO> values)
        {
            // Map tableName -> field -> value.
            var tableMap = new Dictionary<string, Dictionary<string, string>>();

            foreach (var field in fields)
            {
                var tableTokens = field.Value.Split('.');
                if (tableTokens == null || tableTokens.Length != 2)
                {
                    throw new ApplicationException(string.Format("Invalid column name {0}", field.Value));
                }

                var tableName = PrepareSqlName(tableTokens[0]);
                var columnName = PrepareSqlName(tableTokens[1]);
                var valueField = values.FirstOrDefault(x => x.Key == field.Key);

                string value = "";
                if (value != null)
                {
                    value = valueField.Value;
                }

                Dictionary<string, string> columnMap;
                if (!tableMap.TryGetValue(tableName, out columnMap))
                {
                    columnMap = new Dictionary<string, string>();
                    tableMap.Add(tableName, columnMap);
                }

                columnMap[columnName] = value;
            }

            var tables = CreateTablesFromMap(tableMap);
            return tables;
        }

        private IEnumerable<Table> CreateTablesFromMap(
            Dictionary<string, Dictionary<string, string>> tableMap)
        {
            var tables = new List<Table>();

            foreach (var tablePair in tableMap)
            {
                var values = new List<FieldValue>();

                foreach (var columnPair in tablePair.Value)
                {
                    values.Add(new FieldValue(columnPair.Key, columnPair.Value));
                }

                // TODO: change "dbo" schema later.
                tables.Add(new Table(new TableInfo("dbo", tablePair.Key), new[] { new Row(values) }));
            }

            return tables;
        }

        private string PrepareSqlName(string rawName)
        {
            return rawName.Replace("[", "").Replace("]", "");
        }
    }
}