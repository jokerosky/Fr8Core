﻿using Fr8.TerminalBase.BaseClasses;
using Fr8.TerminalBase.Services;

namespace terminalBox.Controllers
{
    public class TerminalController : DefaultTerminalController
    {
        public TerminalController(IActivityStore activityStore)
            : base(activityStore)
        {
        }
    }
}