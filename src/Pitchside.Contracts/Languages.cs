namespace Pitchside.Contracts;

/// <summary>The languages Pitchside writes in. English is the source and the fallback.</summary>
public static class Languages
{
    public const string English = "en";
    public const string Afrikaans = "af";
    public const string IsiZulu = "zu";
    public const string Spanish = "es";

    public static IReadOnlyList<string> All { get; } = [English, Afrikaans, IsiZulu, Spanish];
}
