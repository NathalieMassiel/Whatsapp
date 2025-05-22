using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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

        // 1) Envío de mensaje libre
        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromForm] WhatsAppRequestDto request)
        {
            var token = request.MetaToken ?? _configuration["META_TOKEN"] ?? "";
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Missing META token");

            var payload = new
            {
                messaging_product = "whatsapp",
                to = request.EndUserNumber,
                text = new { body = request.Message }
            };

            var httpReq = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/v22.0/{request.PhoneNumberId}/messages")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var resp = await _httpClient.SendAsync(httpReq);
            var respBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return BadRequest(new { error = respBody });

            // Guardar en BD
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.ExecuteAsync(
                @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType)
                  VALUES (@to, 'outbound', @body, GETUTCDATE(), 'text')",
                new { to = request.EndUserNumber, body = request.Message });

            return Ok(new { success = true });
        }

        // 2) Envío de UNA plantilla individual
        [HttpPost("send-template-message")]
        public async Task<IActionResult> SendTemplateMessage([FromBody] SendTemplateRequestDto request)
        {
            var token = request.MetaToken ?? _configuration["META_TOKEN"] ?? "";
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Missing META token");

            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            // Obtener contenido y ver si tiene {{Name}}
            var templateContent = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Content FROM Templates WHERE Name = @Name",
                new { Name = request.TemplateName });
            if (templateContent == null)
                return BadRequest("Template not found in DB");

            bool tieneVariables = templateContent.Contains("{{");
            var personalizedContent = templateContent.Replace("{{Name}}", request.UserName ?? "Cliente");

            // Detectar idioma exacto
            string languageCode = request.TemplateName.ToLower() switch
            {
                "inicio_de_conversacion" => "es_PAN",
                "hello_world" => "en_US",
                _ => "es_PAN"
            };

            // Construir payload template
            object payload = !tieneVariables
                ? new
                {
                    messaging_product = "whatsapp",
                    to = request.EndUserNumber,
                    type = "template",
                    template = new
                    {
                        name = request.TemplateName.ToLower(),
                        language = new { code = languageCode }
                    }
                }
                : new
                {
                    messaging_product = "whatsapp",
                    to = request.EndUserNumber,
                    type = "template",
                    template = new
                    {
                        name = request.TemplateName.ToLower(),
                        language = new { code = languageCode },
                        components = new[]
                        {
                            new {
                                type       = "body",
                                parameters = new[] { new { type = "text", text = request.UserName ?? "Cliente" } }
                            }
                        }
                    }
                };

            var httpReq = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/v22.0/{request.PhoneNumberId}/messages")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var resp = await _httpClient.SendAsync(httpReq);
            var respBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return BadRequest(new { error = respBody });

            // Guardar el mensaje real
            await conn.ExecuteAsync(
                @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType)
                  VALUES (@to, 'outbound', @body, GETUTCDATE(), 'template')",
                new { to = request.EndUserNumber, body = personalizedContent });

            return Ok(new { success = true });
        }

        // 3) Envío de plantilla MASIVO por grupo
        [HttpPost("send-bulk-template")]
        public async Task<IActionResult> SendBulkTemplate([FromBody] BulkTemplateRequestDto request)
        {
            var token = request.MetaToken ?? _configuration["META_TOKEN"] ?? "";
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Missing META token");

            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            // Obtener contenido y variable
            var templateContent = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Content FROM Templates WHERE Name = @Name",
                new { Name = request.TemplateName });
            if (templateContent == null)
                return BadRequest("Template not found in DB");

            bool tieneVariables = templateContent.Contains("{{");
            string languageCode = request.TemplateName.ToLower() switch
            {
                "inicio_de_conversacion" => "es_PAN",
                "hello_world" => "en_US",
                _ => "es_PAN"
            };

            // Cargar contactos del grupo
            var contactos = await conn.QueryAsync<dynamic>(
                @"
                  SELECT c.Name, c.PhoneNumber
                    FROM Contacts c
                   INNER JOIN GroupContacts gc ON gc.ContactId = c.Id
                   INNER JOIN ContactGroups g   ON g.Id       = gc.GroupId
                   WHERE g.Name = @GroupName
                ",
                new { GroupName = request.GroupName }
            );

            var errores = new List<object>();
            foreach (var contacto in contactos)
            {
                // Personalizar contenido
                var personalizedContent = templateContent.Replace("{{Name}}", contacto.Name ?? "Cliente");

                // Armar payload
                object payload = !tieneVariables
                    ? new
                    {
                        messaging_product = "whatsapp",
                        to = contacto.PhoneNumber,
                        type = "template",
                        template = new
                        {
                            name = request.TemplateName.ToLower(),
                            language = new { code = languageCode }
                        }
                    }
                    : new
                    {
                        messaging_product = "whatsapp",
                        to = contacto.PhoneNumber,
                        type = "template",
                        template = new
                        {
                            name = request.TemplateName.ToLower(),
                            language = new { code = languageCode },
                            components = new[]
                            {
                                new {
                                    type       = "body",
                                    parameters = new[] { new { type = "text", text = contacto.Name ?? "Cliente" } }
                                }
                            }
                        }
                    };

                // Enviar
                var httpReq = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://graph.facebook.com/v22.0/{request.PhoneNumberId}/messages")
                {
                    Headers = { { "Authorization", $"Bearer {token}" } },
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                var resp = await _httpClient.SendAsync(httpReq);
                var respBody = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    errores.Add(new { contacto.PhoneNumber, error = respBody });
                    continue;
                }

                // Guardar en BD
                await conn.ExecuteAsync(
                    @"INSERT INTO WhatsAppMessages (PhoneNumber, Direction, Content, Timestamp, MessageType)
                      VALUES (@pn, 'outbound', @body, GETUTCDATE(), 'template')",
                    new { pn = contacto.PhoneNumber, body = personalizedContent });
            }

            if (errores.Count > 0)
                return Ok(new { message = "Algunos mensajes fallaron", errores });

            return Ok(new { message = "Campaña enviada a todo el grupo correctamente." });
        }

        // 4) Obtener lista de conversaciones
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var nums = await conn.QueryAsync<string>(
                "SELECT DISTINCT PhoneNumber FROM WhatsAppMessages");
            return Ok(nums);
        }

        // 5) Obtener mensajes de un número
        [HttpGet("messages/{number}")]
        public async Task<IActionResult> GetMessages(string number)
        {
            await using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var msgs = await conn.QueryAsync(
                @"
                  SELECT Direction, Content, Timestamp, MessageType
                    FROM WhatsAppMessages
                   WHERE PhoneNumber = @num
                ORDER BY Timestamp ASC",
                new { num = number }
            );
            return Ok(msgs);
        }
    }
}
