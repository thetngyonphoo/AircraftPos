using AircraftPos.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AircraftPos.Api.Controllers
{
    [Route("pos-reports")]
    [ApiController]
    public class PosReportController : ControllerBase
    {
        private readonly IPosReportParser _posReportParser;

        public PosReportController(IPosReportParser posReportParser)
        {
            _posReportParser = posReportParser;              
        }

        [HttpPost]
        public async Task<IActionResult> ReceivePosReport()
        {
            using var reader = new StreamReader(Request.Body);

            var rawMessage = await reader.ReadToEndAsync();

            var receivedAt = DateTime.UtcNow;

            var report = _posReportParser.Parse(rawMessage, receivedAt);

            return Ok(report);
        }
    }
}
