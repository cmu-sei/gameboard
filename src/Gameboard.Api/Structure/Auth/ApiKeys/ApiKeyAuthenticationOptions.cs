using Microsoft.AspNetCore.Authentication;

namespace Gameboard.Api.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public static int MIN_BYTES_RANDOMNESS = 16;
    public static int MIN_RANDOMNESS_LENGTH = 10;

    public int BytesOfRandomness { get; set; } = 32;
    public bool IsEnabled { get; set; } = false;
    public string KeyPrefix { get; set; } = "GB";
    public int RandomCharactersLength { get; set; } = 36;

    public override void Validate()
    {
        if (BytesOfRandomness < MIN_BYTES_RANDOMNESS || RandomCharactersLength < MIN_RANDOMNESS_LENGTH)
        {
            throw new InvalidApiKeyAuthenticationOptions("Invalid configuration of API key authentication. The minimum value ");
        }
    }
}