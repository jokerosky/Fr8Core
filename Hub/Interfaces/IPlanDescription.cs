﻿using Data.Entities;
using Data.Interfaces.DataTransferObjects.PlanDescription;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hub.Interfaces
{
    public interface IPlanDescription
    {
        PlanDescriptionDTO SavePlan(Guid planId, string curFr8UserId);
        List<PlanDescriptionDTO> GetDescriptions(string userId);
        string LoadPlan(int planDescriptionId, string userId);
    }
}
