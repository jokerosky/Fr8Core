﻿using System.Collections.Generic;
using Data.Interfaces.DataTransferObjects;
using System.Linq;

namespace Data.Interfaces.Manifests
{
    public class FieldDescriptionsCM : Manifest
    {
        public FieldDescriptionsCM()
			  : base(Constants.MT.FieldDescription)
        {
            Fields = new List<FieldDTO>();
        }

        public FieldDescriptionsCM(IEnumerable<FieldDTO> fields) : this()
        {
            Fields.AddRange(fields);
        }

        public FieldDescriptionsCM(params FieldDTO[] fields) : this()
        {
            Fields.AddRange(fields);
        }

        public List<FieldDTO> Fields { get; set; }

        public string this[string key]
        {
            get { return Fields?.FirstOrDefault(x => x.Key == key)?.Value; }
            set
            {
                var field = Fields.FirstOrDefault();
                if (field != null)
                {
                    field.Value = value;
                }
            }
        }
    }
}
