using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DistriHub.Models;
using DistriHub.Services.Interfaces;

namespace DistriHub.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SerialNoController : ControllerBase
    {
        private readonly ISerialService _service;

        public SerialNoController(ISerialService service)
        {
            _service = service;
        }

        // POST api/SerialNo?op=IsSerialNoUsed OR op=SerialNoUnfreeze
        [HttpPost]
        public async Task<IActionResult> Post([FromQuery] string op, [FromBody] SerialNoRequest request)
        {
            if (string.IsNullOrWhiteSpace(op))
                return BadRequest(new ResultWrapper { Result = new[] { new SerialResponse { responseStatus = "-6", responseMessage = "Missing operation (op)" } } });

            if (request == null)
                return BadRequest(new ResultWrapper { Result = new[] { new SerialResponse { responseStatus = "-7", responseMessage = "Invalid request body" } } });

            int code;
            // get source (username) from JWT claims - token is issued by AuthController
            var source = User?.FindFirst("source")?.Value ?? User?.Identity?.Name ?? string.Empty;
            switch (op.Trim())
            {
                case "IsSerialNoUsed":
                    code = await _service.ValidateSerialAsync(request.MaterialCode ?? string.Empty, request.SerialNumber ?? string.Empty, source ?? string.Empty);
                    break;
                case "SerialNoUnfreeze":
                    code = await _service.UnfreezeSerialAsync(request.MaterialCode ?? string.Empty, request.SerialNumber ?? string.Empty, source ?? string.Empty);
                    break;
                default:
                    return BadRequest(new ResultWrapper { Result = new[] { new SerialResponse { responseStatus = "-8", responseMessage = "Unknown operation" } } });
            }

            var response = MapStatusToResponse(code, op.Trim());
            return Ok(new ResultWrapper { Result = new[] { response } });
        }

        private SerialResponse MapStatusToResponse(int code, string op)
        {
            return code switch
            {
                0 when op == "IsSerialNoUsed" => new SerialResponse { responseStatus = "0", responseMessage = "Valid Serial No" },
                0 when op == "SerialNoUnfreeze" => new SerialResponse { responseStatus = "0", responseMessage = "Unfreezed Serial No" },
                -1 => new SerialResponse { responseStatus = "-1", responseMessage = "Invalid Serial Number" },
                -2 => new SerialResponse { responseStatus = "-2", responseMessage = "Mismatch in model and serial number" },
                -3 => new SerialResponse { responseStatus = "-3", responseMessage = "Serial Number Already Validated" },
                -4 => new SerialResponse { responseStatus = "-4", responseMessage = "Invalid Material code" },
                -5 => new SerialResponse { responseStatus = "-5", responseMessage = "Invalid Access Code" },
                _ => new SerialResponse { responseStatus = "-9", responseMessage = "Unknown error" }
            };
        }
    }

    // DTOs moved to DistriHub.Models for reuse and clarity
}
