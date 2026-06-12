namespace ApiFacturaConcurrente.Models.DTOs;

public class TokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public List<string> SucursalesPermitidas { get; set; } = new();
}