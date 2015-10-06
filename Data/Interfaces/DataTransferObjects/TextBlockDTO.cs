﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Data.Interfaces.DataTransferObjects
{
	public class TextBlockFieldDTO : ControlsDefinitionDTO
	{
		[JsonProperty("class")]
		public string cssClass;

		public TextBlockFieldDTO()
		{
			Type = "textBlockField";
		}
	}
}
