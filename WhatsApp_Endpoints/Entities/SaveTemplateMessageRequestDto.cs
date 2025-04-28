namespace WhatsApp_Endpoints.Entities
{
    public class SaveTemplateMessageRequestDto
    {
        public required string PhoneNumber { get; set; }
        public required string Content { get; set; }
    }

}
