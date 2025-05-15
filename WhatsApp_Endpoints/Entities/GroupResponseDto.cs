using Microsoft.AspNetCore.Mvc;

namespace WhatsApp_Endpoints.Entities
{
    public class GroupResponseDto : Controller
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
