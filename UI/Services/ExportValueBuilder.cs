namespace MgaWwiseIMImporter.UI.Services;

/// <summary>EXPORT 用パート値辞書の構築。</summary>
internal static class ExportValueBuilder
{
    public static IReadOnlyDictionary<int, T> Build<T>(
        IReadOnlySet<int> enabledNumbers,
        Func<int, T> resolver)
    {
        var result = new Dictionary<int, T>();
        foreach (var partNumber in enabledNumbers)
        {
            result[partNumber] = resolver(partNumber);
        }

        return result;
    }
}
