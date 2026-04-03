namespace PhiInfo.Core.Models.Information;

/// <summary>
/// Extracted avatar information.
/// </summary>
/// <param name="Name">Display name of the avatar. I.e. <c>Engine x Start!! (melody mix)</c></param>
/// <param name="AddressablePath">Internal addressable path of the avatar. I.e. <c>avatar.Engine x Start!! (melody mix)</c></param>
public record Avatar(string Name, string AddressablePath);