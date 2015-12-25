﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Interfaces.Manifests
{
    public class DocuSignTemplateCM : Manifest
    {
        public string Status { get; set; }
        public string CreateDate { get; set; }
        public string Body { get; set; }
        public string Name { get; set; }
        public DocuSignTemplateCM()
            : base(Constants.MT.DocuSignTemplate)
        {

        }
    }
}
