namespace WhatsApp_Endpoints.Entities
{
    public class BulkTemplateRequestDto
    {
        public required string GroupName { get; set; }
        public required string TemplateName { get; set; }
        public required string PhoneNumberId { get; set; }
        public required string MetaToken { get; set; }
    }
}
