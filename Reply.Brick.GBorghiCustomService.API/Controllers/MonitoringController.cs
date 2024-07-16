using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Reply.Brick.FryPlusConnector.API.Controllers
{
    [Route("api/v1/Test")]
    [ApiController]
    [Authorize]
    public class MonitoringController : ControllerBase
    {
        #region Private variables
        #endregion

        #region Constructor
        public MonitoringController()
        {
        }
        #endregion

        [HttpGet("GetStatus")]
        public ActionResult<string> GetStatus()
        {
            return Ok("I am aliveee");
        }
    }
}
