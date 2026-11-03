namespace DocumentLink.Infrastructure;

/// <summary>Revit-free helpers that behave the same on .NET Framework 4.8 and .NET 8.</summary>
public static class TextUtil
{
    public static bool ContainsIgnoreCase(this string text, string value) =>
        text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;

    /// <summary>SHA-256 hex (works on .NET Framework 4.8 and .NET 8).</summary>
    public static string Sha256Hex(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        return BitConverter.ToString(bytes).Replace("-", "");
    }
}
