namespace EffiTex.Engine.Models.Inspect;

public class PdfStreamData
{
    /// <summary>Base64-encoded raw stream bytes (GetBytes(false) — not decoded through the filter pipeline).</summary>
    public string Data { get; set; }

    /// <summary>Normalized /Filter value. Always string[] regardless of whether the PDF source uses a name or array. Null if no /Filter entry.</summary>
    public string[] Filter { get; set; }

    /// <summary>Parallel array to Filter. Each element is the /DecodeParms dict for the corresponding filter, or null if that filter has no parameters. Null if no /DecodeParms entry.</summary>
    public Dictionary<string, object>[] DecodeParms { get; set; }
}
