using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System;

namespace WhatsApp_Endpoints.Entities
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public WhatsAppController(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromForm] WhatsAppRequestDto request)
        {
            // 1) Extraer el token de acceso (Access Token)
            string metaToken = request.MetaToken ?? _configuration["META_TOKEN"] ?? string.Empty;
            if (string.IsNullOrEmpty(metaToken))
            {
                return BadRequest(new { error = "Missing META token" });
            }

            // 2) Crear el payload
            var payload = new
            {
                messaging_product = "whatsapp",
                to = request.EndUserNumber ?? string.Empty,
                text = new { body = request.Message ?? string.Empty }
            };

            // 3) Construir la petición
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                // <--- Aquí es vital usar el Phone Number ID, no el Business Account ID
                $"https://graph.facebook.com/v22.0/{request.PhoneNumberId ?? string.Empty}/messages"
            )
            {
                Headers = { { "Authorization", $"Bearer {metaToken}" } },
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            // 4) Enviar y leer la respuesta
            var response = await _httpClient.SendAsync(httpRequest);
            var responseData = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { error = "Failed to send message", details = responseData });
            }

            return Ok(new { success = "Message sent successfully!" });
        }


        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string hubMode,
            [FromQuery(Name = "hub.verify_token")] string hubVerifyToken,
            [FromQuery(Name = "hub.challenge")] string hubChallenge
        )
        {
            // This line logs the incoming verification parameters to the console
            Console.WriteLine($"[META] mode={hubMode}, token={hubVerifyToken}, challenge={hubChallenge}");

            if (hubMode == "subscribe" && hubVerifyToken == "abc123")
            {
                return Content(hubChallenge, "text/plain");
            }

            return Forbid();
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JsonDocument body)
        {
            if (string.IsNullOrEmpty(_configuration["META_TOKEN"]))
            {
                return BadRequest(new { error = "Missing META_TOKEN environment variable" });
            }

            try
            {
                var entry = body.RootElement.GetProperty("entry")[0]
                                     .GetProperty("changes")[0]
                                     .GetProperty("value");

                var message = entry.GetProperty("messages")[0];
                var phoneNumberId = entry.GetProperty("metadata")
                                         .GetProperty("phone_number_id")
                                         .GetString() ?? string.Empty;

                if (message.GetProperty("type").GetString() == "text")
                {
                    var responsePayload = new
                    {
                        messaging_product = "whatsapp",
                        to = message.GetProperty("from").GetString() ?? string.Empty,
                        text = new
                        {
                            body = "Echo: " + (message.GetProperty("text")
                                                     .GetProperty("body")
                                                     .GetString() ?? string.Empty)
                        },
                        context = new
                        {
                            message_id = message.GetProperty("id").GetString() ?? string.Empty
                        }
                    };

                    var httpRequest = new HttpRequestMessage(
                        HttpMethod.Post,
                        $"https://graph.facebook.com/v22.0/{phoneNumberId}/messages"
                    )
                    {
                        Headers = { { "Authorization", $"Bearer {_configuration["META_TOKEN"] ?? string.Empty}" } },
                        Content = new StringContent(JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
                    };

                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseData = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(new { error = "Error sending message", details = responseData });
                    }

                    return Ok(new { success = "Message sent" });
                }
            }
            catch (JsonException ex)
            {
                return BadRequest(new { error = "Invalid JSON format", details = ex.Message });
            }

            return Ok(new { success = "Webhook received!" });
        }
    }
}