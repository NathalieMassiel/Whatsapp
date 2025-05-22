using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Collections.Generic;
using WhatsApp_Endpoints.Entities;

namespace YourNamespaceHere
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
            string metaToken = request.MetaToken ?? _configuration["META_TOKEN"] ?? string.Empty;
            if (string.IsNullOrEmpty(metaToken))
                return BadRequest(new { error = "Missing META token" });

            var payload = new
            {
                messaging_product = "whatsapp",
                to = request.EndUserNumber,
                text = new { body = request.Message }
            };

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/v22.0/{request.PhoneNumberId}/messages")
            {
                Headers = { { "Authorization", $"Bearer {metaToken}" } },
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(httpRequest);
            var responseData = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return BadRequest(new { error = "Failed to send message", details = responseData });

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.ExecuteAsync(
                @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType) 
                  VALUES (@to, 'outbound', @body, GETUTCDATE(), 'text')",
                new { to = request.EndUserNumber, body = request.Message });

            return Ok(new { success = "Message sent successfully!" });
        }

        [HttpPost("send-template-message")]
        public async Task<IActionResult> SendTemplateMessage([FromBody] SendTemplateRequestDto request)
        {
            try
            {
                string metaToken = request.MetaToken ?? _configuration["META_TOKEN"] ?? string.Empty;
                if (string.IsNullOrEmpty(metaToken))
                    return BadRequest(new { error = "Missing META token" });

                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var templateExists = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT Name FROM Templates WHERE Name = @Name", new { Name = request.TemplateName });

                if (templateExists == null)
                    return BadRequest(new { error = "Template not found in local database." });

                object payload;

                if (request.TemplateName.ToLower() == "hello_world") // plantilla sin variables
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = request.EndUserNumber,
                        type = "template",
                        template = new
                        {
                            name = request.TemplateName.ToLower(),
                            language = new { code = "es" }
                        }
                    };
                }
                else
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = request.EndUserNumber,
                        type = "template",
                        template = new
                        {
                            name = request.TemplateName.ToLower(),
                            language = new { code = "es" },
                            components = new[]
                            {
                                new
                                {
                                    type = "body",
                                    parameters = new[]
                                    {
                                        new { type = "text", text = request.UserName ?? "Cliente" }
                                    }
                                }
                            }
                        }
                    };
                }

                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://graph.facebook.com/v22.0/{request.PhoneNumberId}/messages")
                {
                    Headers = { { "Authorization", $"Bearer {metaToken}" } },
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(httpRequest);
                var responseData = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { error = "Failed to send template", details = responseData });

                await conn.ExecuteAsync(
                    @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType) 
                      VALUES (@to, 'outbound', @body, GETUTCDATE(), 'template')",
                    new { to = request.EndUserNumber, body = $"Plantilla: {request.TemplateName}" });

                return Ok(new { success = "Template message sent successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendTemplateMessage: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("save-template-message")]
        public async Task<IActionResult> SaveTemplateMessage([FromBody] SaveTemplateMessageRequestDto request)
        {
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.ExecuteAsync(
                    @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType) 
                      VALUES (@PhoneNumber, 'outbound', @Content, GETUTCDATE(), 'template')",
                    new { PhoneNumber = request.PhoneNumber, Content = request.Content });

                return Ok(new { success = "Template message saved successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en SaveTemplateMessage: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string hubMode,
            [FromQuery(Name = "hub.verify_token")] string hubVerifyToken,
            [FromQuery(Name = "hub.challenge")] string hubChallenge)
        {
            if (hubMode == "subscribe" && hubVerifyToken == "abc123")
                return Content(hubChallenge, "text/plain");

            return Forbid();
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JsonDocument body)
        {
            try
            {
                var entry = body.RootElement.GetProperty("entry")[0];
                var changes = entry.GetProperty("changes")[0];
                var value = changes.GetProperty("value");

                if (!value.TryGetProperty("messages", out var messagesElement) || messagesElement.GetArrayLength() == 0)
                {
                    Console.WriteLine("Webhook recibido sin mensajes. Puede ser una notificación de estado.");
                    return Ok();
                }

                var message = messagesElement[0];

                if (message.GetProperty("type").GetString() == "text")
                {
                    var from = message.GetProperty("from").GetString();
                    var content = message.GetProperty("text").GetProperty("body").GetString();

                    Console.WriteLine($"Mensaje INBOUND recibido de {from}: {content}");

                    using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await conn.ExecuteAsync(
                        @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType) 
                          VALUES (@from, 'inbound', @content, GETUTCDATE(), 'text')",
                        new { from, content });
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ReceiveWebhook: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("messages/{number}")]
        public async Task<IActionResult> GetMessages(string number)
        {
            var sql = @"SELECT 
                    Direction,
                    Content,
                    Timestamp,
                    MessageType
                FROM WhatsAppMessages
                WHERE PhoneNumber = @Number
                ORDER BY Timestamp ASC";

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var messages = await conn.QueryAsync(sql, new { Number = number });

            return Ok(messages);
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var sql = @"SELECT DISTINCT PhoneNumber FROM WhatsAppMessages";

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var numbers = await conn.QueryAsync<string>(sql);

            return Ok(numbers);
        }

        [HttpPost("send-bulk-template")]
        public async Task<IActionResult> SendBulkTemplate([FromBody] BulkTemplateRequestDto request)
        {
            string metaToken = request.MetaToken ?? _configuration["META_TOKEN"] ?? string.Empty;
            if (string.IsNullOrEmpty(metaToken))
                return BadRequest(new { error = "Missing META token" });

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            var templateExists = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Name FROM Templates WHERE Name = @Name",
                new { Name = request.TemplateName });

            if (templateExists == null)
                return BadRequest(new { error = "Template not found in local DB." });

            var contactos = await conn.QueryAsync<dynamic>(
                @"SELECT c.Name, c.PhoneNumber
                  FROM Contacts c
                  INNER JOIN GroupContacts gc ON gc.ContactId = c.Id
                  INNER JOIN ContactGroups g ON g.Id = gc.GroupId
                  WHERE g.Name = @GroupName",
                new { GroupName = request.GroupName });

            var errores = new List<object>();

            foreach (var contacto in contactos)
            {
                object payload;

                if (request.TemplateName.ToLower() == "hello_world")
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = contacto.PhoneNumber,
                        type = "template",
                        template = new
                        {
                            name = request.TemplateName.ToLower(),
                            language = new { code = "es" }
                        }
                    };
                }
                else
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = contacto.PhoneNumber,
                        type = "template",
                        template = new
                        {
                            name = request.TemplateName.ToLower(),
                            language = new { code = "es" },
                            components = new[]
                            {
                                new
                                {
                                    type = "body",
                                    parameters = new[]
                                    {
                                        new { type = "text", text = contacto.Name ?? "Cliente" }
                                    }
                                }
                            }
                        }
                    };
                }

                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://graph.facebook.com/v22.0/{request.PhoneNumberId}/messages")
                {
                    Headers = { { "Authorization", $"Bearer {metaToken}" } },
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(httpRequest);
                var responseData = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    errores.Add(new { contacto.PhoneNumber, error = responseData });
                    continue;
                }

                await conn.ExecuteAsync(
                    @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType)
                      VALUES (@PhoneNumber, 'outbound', @Content, GETUTCDATE(), 'template')",
                    new { PhoneNumber = contacto.PhoneNumber, Content = $"Plantilla: {request.TemplateName}" });
            }

            if (errores.Count > 0)
                return Ok(new { message = "Se enviaron algunos mensajes con errores.", errores });

            return Ok(new { message = "Todos los mensajes fueron enviados correctamente." });
        }
    }
}
