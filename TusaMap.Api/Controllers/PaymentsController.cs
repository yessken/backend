using Microsoft.AspNetCore.Mvc;

namespace TusaMap.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    [HttpPost("webhook")]
    public IActionResult Webhook() => StatusCode(StatusCodes.Status410Gone,
        "Оплата билетов подтверждается только Telegram successful_payment. Этот legacy webhook отключён.");
}
