namespace Gameboard.Api.Structure;

public static class MimeTypes
{
    public static string ApplicationJson { get => "application/json"; }
    public static string ApplicationPdf { get => "application/pdf"; }
    public static string ImagePng { get => "image/png"; }
    public static string ImageSvg { get => "image/svg+xml"; }
    public static string ImageWebp { get => "image/webp"; }
    public const string OctetStream = "application/octet-stream";
    public static string TextCsv { get => "text/csv"; }
    public const string Zip = "application/zip";
}
