using Xunit;

namespace ElBruno.Connectors.SqliteVec.Tests;

public class TextSplitterTests
{
    [Fact]
    public void SplitParagraphs_NormalSplitting_ReturnsCorrectChunks()
    {
        var result = TextSplitter.SplitParagraphs(["one two three four five six"], 3);

        Assert.Equal(2, result.Count);
        Assert.Equal("one two three", result[0]);
        Assert.Equal("four five six", result[1]);
    }

    [Fact]
    public void SplitParagraphs_EmptyInput_ReturnsEmptyList()
    {
        var result = TextSplitter.SplitParagraphs([""], 5);

        Assert.Empty(result);
    }

    [Fact]
    public void SplitParagraphs_SingleWord_ReturnsSingleChunk()
    {
        var result = TextSplitter.SplitParagraphs(["hello"], 10);

        Assert.Single(result);
        Assert.Equal("hello", result[0]);
    }

    [Fact]
    public void SplitParagraphs_ExactBoundary_ReturnsOneChunk()
    {
        var result = TextSplitter.SplitParagraphs(["a b c"], 3);

        Assert.Single(result);
        Assert.Equal("a b c", result[0]);
    }

    [Fact]
    public void SplitParagraphs_BoundaryPlusOne_ReturnsTwoChunks()
    {
        var result = TextSplitter.SplitParagraphs(["a b c d"], 3);

        Assert.Equal(2, result.Count);
        Assert.Equal("a b c", result[0]);
        Assert.Equal("d", result[1]);
    }

    [Fact]
    public void SplitParagraphs_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextSplitter.SplitParagraphs(null!, 5));
    }

    [Fact]
    public void SplitParagraphs_MultipleInputTexts_CombinesAndSplits()
    {
        var result = TextSplitter.SplitParagraphs(["hello world", "foo bar"], 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("hello world", result[0]);
        Assert.Equal("foo bar", result[1]);
    }
}
