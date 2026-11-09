using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class HangulWordsTests
{
    [Fact]
    public void Each_Korean_word_is_kept_whole_and_spaces_stay_breakable() =>
        Assert.Equal(
            "<nobr>짝을</nobr> <nobr>짓습니다.</nobr>\n<nobr>ESC로</nobr> Esc",
            HangulWords.KeepWhole("짝을 짓습니다.\nESC로 Esc"));

    [Theory]
    [InlineData("Pairs form on the island")]
    [InlineData("<size=68%>짝</size>")]
    [InlineData("")]
    public void Text_without_Korean_or_with_markup_is_left_alone(string text) =>
        Assert.Equal(text, HangulWords.KeepWhole(text));
}
