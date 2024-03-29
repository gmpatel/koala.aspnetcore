﻿using Microsoft.AspNetCore.Constants;
using Koala.Core;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class XInfoController : AbstractController<XInfoController>
    {
        [HttpGet("ServerVersion")]
        public IActionResult GetServerVersion()
        {
            return Ok($"v{this.GetVersion()}");
        }

        [HttpGet("ServerDescription")]
        public IActionResult GetServerDescription()
        {
            return Ok(SetConstants.ApiPageV1Description);
        }
    }
}