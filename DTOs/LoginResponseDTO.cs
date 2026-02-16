namespace News_Back_end.DTOs
{
    public class LoginResponseDTO
    {
        public string Token { get; set; }
        public string Message { get; set; }
        public bool NeedsTopicSelection { get; set; } = false; // New property
    }
}
