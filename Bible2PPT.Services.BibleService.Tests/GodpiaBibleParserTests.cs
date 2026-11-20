using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Sources;
using Xunit;

namespace Bible2PPT.Services.BibleService.Tests;

/// <summary>
/// 임베디드 HTML 픽처를 읽어온다 (네트워크 없이 결정적 테스트).
/// </summary>
internal static class FixtureReader
{
    private static readonly System.Reflection.Assembly Assembly = typeof(FixtureReader).Assembly;

    public static string Read(string fileName)
    {
        // 픽처 파일명 기준으로 리소스 이름을 찾아서 읽는다.
        var name = Assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(fileName, System.StringComparison.OrdinalIgnoreCase));
        using var stream = Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// 갓피아 HTML 파서(ParseBooks / ParseChapters / ParseVerses) 테스트.
/// </summary>
public class GodpiaBibleParserTests
{
    #region ParseBooks

    [Fact]
    public void ParseBooks_ReturnsOneBookPerInputWithMappedKey()
    {
        var html = FixtureReader.Read("books.html");
        var books = GodpiaBible.ParseBooks(html);

        var byCode = books.ToDictionary(b => b.OnlineId);

        Assert.True(byCode.ContainsKey("gen"));
        Assert.True(byCode.ContainsKey("exo"));
        Assert.True(byCode.ContainsKey("rev"));
        Assert.Equal(3, books.Count);

        Assert.Equal(BookKey.Genesis, byCode["gen"].Key);
        Assert.Equal("Genesis", byCode["gen"].Name);
        Assert.Equal(BookKey.Exodus, byCode["exo"].Key);
        Assert.Equal("Exodus", byCode["exo"].Name);
        Assert.Equal(BookKey.Revelation, byCode["rev"].Key);
        Assert.Equal("Revelation", byCode["rev"].Name);
    }

    [Fact]
    public void ParseBooks_ThrowOnEmptyOrWhitespace()
    {
        Assert.Throws<InvalidDataException>(() => GodpiaBible.ParseBooks(""));
        Assert.Throws<InvalidDataException>(() => GodpiaBible.ParseBooks("   "));
    }

    [Fact]
    public void ParseBooks_ReturnsEmptyListOnHtmlWithoutBooks()
    {
        // 책 블록이 없는 HTML은 예외 없이 빈 목록을 반환한다.
        var books = GodpiaBible.ParseBooks("<p>there are no books</p>");
        Assert.Empty(books);
    }

    #endregion ParseBooks

    #region ParseChapters

    [Fact]
    public void ParseChapters_SortsByNumberAndPreservesOnlineId()
    {
        // 픽처는 3, 1, 2 순서로 되어 있다. 파서는 Number 순서로 정렬해야 한다.
        var html = FixtureReader.Read("chapters.html");
        var chapters = GodpiaBible.ParseChapters(html);

        Assert.Equal(new[] { 1, 2, 3 }, chapters.Select(c => c.Number).ToArray());
        Assert.Equal(new[] { "1", "2", "3" }, chapters.Select(c => c.OnlineId).ToArray());
        Assert.Equal(3, chapters.Count);
    }

    [Fact]
    public void ParseChapters_ThrowOnEmptyOrWhitespace()
    {
        Assert.Throws<InvalidDataException>(() => GodpiaBible.ParseChapters(""));
        Assert.Throws<InvalidDataException>(() => GodpiaBible.ParseChapters("   "));
    }

    #endregion ParseChapters

    #region ParseVerses

    [Fact]
    public void ParseVerses_StripsNumberSpanAndTagsToPureText()
    {
        var html = FixtureReader.Read("verses.html");
        var verses = GodpiaBible.ParseVerses(html);

        var v1 = verses.First(v => v.Number == 1);
        var v2 = verses.First(v => v.Number == 2);

        // 절 번호 스페이와 HTML 태그(dic-link 등)가 제거되고 공백이 압축된다.
        Assert.Equal("One two", v1.Text);
        Assert.Equal("Three", v2.Text);
    }

    [Fact]
    public void ParseVerses_UsesFirstNumberForRangeVerse()
    {
        // range 번호(dataSec="18-19")는 첫 번호(18)를 절 번호로 사용해야 한다.
        var html = FixtureReader.Read("verses.html");
        var verses = GodpiaBible.ParseVerses(html);

        var v18 = verses.First(v => v.Number == 18);

        Assert.Equal(18, v18.Number);
        Assert.Equal("Merged Fourteen Fifteen", v18.Text);
        // range인 18-19는 18로만 저장(19는 별도 구절이 아님).
        Assert.False(verses.Any(v => v.Number == 19));
    }

    [Fact]
    public void ParseVerses_ReturnsAllVerseCount()
    {
        var html = FixtureReader.Read("verses.html");
        var verses = GodpiaBible.ParseVerses(html);
        Assert.Equal(3, verses.Count);
    }

    [Fact]
    public void ParseVerses_MatchesCurrentSiteDatasecFormat()
    {
        // 갓피아 재설치(2026-11) 이후 실제 사이트가 쓰는 대소문자 datasec 속성,
        // range(18-19) 및 HTML 태그(dic-link)가 포함된 실제 형태를 픽처로 재현.
        var html = FixtureReader.Read("verses_current_site.html");
        var verses = GodpiaBible.ParseVerses(html);

        var v1 = verses.Single(v => v.Number == 1);
        var v2 = verses.Single(v => v.Number == 2);
        var v18 = verses.Single(v => v.Number == 18);

        Assert.Equal("One two", v1.Text);
        Assert.Equal("Three", v2.Text);
        Assert.Equal(18, v18.Number);
        Assert.Equal("Merged Fourteen Fifteen", v18.Text);
        Assert.Equal(3, verses.Count);
        Assert.False(verses.Any(v => v.Number == 19));
    }

    [Fact]
    public void ParseVerses_ThrowOnEmptyOrWhitespace()
    {
        Assert.Throws<InvalidDataException>(() => GodpiaBible.ParseVerses(""));
        Assert.Throws<InvalidDataException>(() => GodpiaBible.ParseVerses("   "));
    }

    [Fact]
    public void ParseVerses_ReturnsEmptyListOnHtmlWithoutVerses()
    {
        var verses = GodpiaBible.ParseVerses("<p>no verses here</p>");
        Assert.Empty(verses);
    }

    #endregion ParseVerses
}
