namespace DropLink.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public string PublicHost { get; set; } = "";
    public int PublicPort { get; set; } = 443;
    public int ServerPort { get; set; } = 43821;
}
