namespace JWTAuthTemplate.DTO.Identity
{
    public class MinioSettingsDTO
    {
        public string? Endpoint { get; set; }
        public string? PublicEndpoint { get; set; }
        public string? AccessKey { get; set; }
        public string? SecretKey { get; set; }
    }
}
