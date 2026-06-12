namespace ApiFacturaConcurrente.Models.DTOs;

public class TokenRequestDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}