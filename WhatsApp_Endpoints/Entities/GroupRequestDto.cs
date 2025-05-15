namespace WhatsApp_Endpoints.Entities
{
    public class GroupRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public List<int> ContactIds { get; set; } = new();
    }
}
