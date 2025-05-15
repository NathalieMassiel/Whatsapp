using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Data;
using Dapper;
using WhatsApp_Endpoints.Entities;

namespace YourNamespaceHere
{
    [ApiController]
    [Route("api/groups")]
    public class GroupsController(IConfiguration configuration, IDbConnection db) : ControllerBase
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IDbConnection _db = db;

        [HttpGet]
        public async Task<IActionResult> ObtenerGrupos()
        {
            var grupos = await _db.QueryAsync<GroupResponseDto>(
                "SELECT Id, Name FROM ContactGroups ORDER BY CreatedAt DESC"
            );

            return Ok(grupos);
        }

        [HttpPost]
        public IActionResult CrearGrupo([FromBody] GroupRequestDto grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo.Name) || grupo.ContactIds.Count == 0)
                return BadRequest(new { error = "El nombre del grupo y al menos un contacto son obligatorios." });

            var table = new DataTable();
            table.Columns.Add("ContactId", typeof(int));
            foreach (var id in grupo.ContactIds)
                table.Rows.Add(id);

            _db.Execute(
                "sp_CreateGroupWithContacts",
                new
                {
                    GroupName = grupo.Name,
                    ContactIds = table.AsTableValuedParameter("dbo.IntList")
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { message = "Grupo creado correctamente." });
        }

        // NUEVO: Este endpoint es necesario para que Teams.tsx cargue bien las plantillas
        [HttpGet("/api/templates")]
        public async Task<IActionResult> ObtenerTemplates()
        {
            var templates = await _db.QueryAsync("SELECT Name FROM Templates ORDER BY CreatedAt DESC");
            return Ok(templates);
        }
    }
}
