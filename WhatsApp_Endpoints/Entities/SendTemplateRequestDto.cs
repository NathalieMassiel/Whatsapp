namespace WhatsApp_Endpoints.Entities
{
    public class SendTemplateRequestDto
    {
        public required string PhoneNumberId { get; set; }
        public required string EndUserNumber { get; set; }
        public required string MetaToken { get; set; }
        public required string TemplateName { get; set; }
        public required string UserName { get; set; } // nombre de la persona para reemplazar {{Name}}
    }
}
