namespace EffiTex.Engine.Models.Inspect;

public class ColorPairInfo
{
    public string Foreground { get; set; }   // 6-char uppercase hex, no #
    public string Background { get; set; }   // 6-char uppercase hex, no #
    public int OccurrenceCount { get; set; }
}
