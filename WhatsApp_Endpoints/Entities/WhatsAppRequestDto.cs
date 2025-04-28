namespace WhatsApp_Endpoints.Entities
{
   
        public class WhatsAppRequestDto
        {
        public required string MetaToken { get; set; }
        public required string PhoneNumberId { get; set; }
        public required string EndUserNumber { get; set; }
        public required string Message { get; set; }
    }
    

}
