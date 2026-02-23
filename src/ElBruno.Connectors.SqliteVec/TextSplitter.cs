namespace ElBruno.Connectors.SqliteVec;

/// <summary>
/// A simple text splitter that breaks text into word-based chunks.
/// Based on Semantic Kernel's TextChunker.SplitPlainTextParagraphs.
/// </summary>
public static class TextSplitter
{
    /// <summary>
    /// Splits the combined text from the given inputs into chunks of approximately
    /// <paramref name="maxWordsPerChunk"/> words each.
    /// </summary>
    /// <param name="texts">The input text segments to combine and split.</param>
    /// <param name="maxWordsPerChunk">The maximum number of words per chunk.</param>
    /// <returns>A list of text chunks.</returns>
    public static List<string> SplitParagraphs(IEnumerable<string> texts, int maxWordsPerChunk)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (maxWordsPerChunk < 1)
            throw new ArgumentOutOfRangeException(nameof(maxWordsPerChunk), "Must be at least 1.");

        var allText = string.Join(" ", texts);
        var words = allText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        for (int i = 0; i < words.Length; i += maxWordsPerChunk)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(maxWordsPerChunk));
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);
        }

        return chunks;
    }
}
