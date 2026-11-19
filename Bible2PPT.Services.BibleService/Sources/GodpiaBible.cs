using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bible2PPT.Bibles;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;

namespace Bible2PPT.Sources;

/// <summary>
/// 갓피아 성경 소스.
/// 갓피아 홈페이지 재설치로 기존 http://bible.godpia.com은 더 이상 작동하지 않는다.
/// 새로운 사이트는 https://www.godpia.com이며, 아래 엔드포인트가 정상으로 동작한다:
///   책   : GET /read/reading.asp?ver={ver}
///   장   : GET /include/asp/chapinfo.asp?ver={ver}&vol={vol}
///   구절 : GET /read/reading_body.asp?ver={ver}&vol={volname}&chap={chap}
/// </summary>
public class GodpiaBible : BibleSource
{
    private const string BaseUrl = "https://www.godpia.com";

    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(30),
    };

    // /read/reading.asp?ver={ver} 페이지의 책 목록:
    // <input ... name="volname" ... value="gen">
    // <label ... for="btn-check-gen">창세기</label>
    private static readonly Regex BookPattern = new(
        @"<input[^>]*name=""volname""[^>]*value=""([^""]+)""[^>]*>[\s\S]*?<label[^>]*for=""btn-check-\1""[^>]*>([^<]+)<",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // /include/asp/chapinfo.asp?ver={ver}&vol={vol} 응답의 장 목록:
    // <input type="radio" name="chapname" ... value="1">
    private static readonly Regex ChapterPattern = new(
        @"<input[^>]*name=""chapname""[^>]*value=""(\d+)""[^>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // /read/reading_body.asp?ver={ver}&vol={volname}&chap={chap} 응답의 각 구절:
    // <li ... dataSec="1"><span class="bible-read-no">1</span>
    //    <span class="bible-read-cont"><span class='dic-link' ...>태초</span>에 하나님이...</span></li>
    //
    // 각 구절 <li dataSec="N"> ... </li> 블록으로 구분한다.
    // 개역개정(4판)은 2~3개의 절이 하나의 절로 합쳐져 있는 구절이 있다(신6:18-19, 시92:1-3 등).
    // 이 합쳐진 구절은 range 번호(dataSec="18-19")로 제공되므로 (\d+)만으로는 못 잡아
    // 구절이 누락된다. range 번호까지 매칭해 첫 번호로 저장한다.
    private static readonly Regex VersePattern = new(
        @"<li[^>]*dataSec=""(\d+(?:-\d+)?)""[^>]*>([\s\S]*?)</li>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // <span class="bible-read-no">N</span> : 절 번호 스페이
    private static readonly Regex VerseNoSpanPattern = new(
        @"<span[^>]*class=""bible-read-no[^>]*>[^<]*</span>",
        RegexOptions.Compiled);

    // 모든 HTML 태그
    private static readonly Regex TagPattern = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    // 공백/줄바꿈 여러 개 압축
    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled);

    public GodpiaBible()
    {
        Name = "갓피아 성경";
    }

    // 갓피아 홈페이지에 있는 번역판 목록.
    // ID는 기존과 동일하며(신규 사이트도 동일하게 사용), 언어 코드를 직접 지정한다.
    public override Task<List<Bible>> GetBiblesOnlineAsync() => Task.FromResult(new List<Bible>
    {
        new() { OnlineId = "gae", Name = "개역개정(4판)", LanguageCode = "ko" },
        new() { OnlineId = "han", Name = "개역한글", LanguageCode = "ko" },
        new() { OnlineId = "easy", Name = "쉬운성경", LanguageCode = "ko" },
        new() { OnlineId = "hyun", Name = "현대인의성경", LanguageCode = "ko" },
        new() { OnlineId = "saenew", Name = "새번역", LanguageCode = "ko" },
        new() { OnlineId = "niv", Name = "NIV", LanguageCode = "en" },
        new() { OnlineId = "hebrew", Name = "히브리어(구약)", LanguageCode = "he" },
        new() { OnlineId = "greek", Name = "헬라어(신약)", LanguageCode = "el" },
    });

    public override async Task<List<Book>> GetBooksOnlineAsync(Bible bible)
    {
        var data = await Client.GetStringAsync($"/read/reading.asp?ver={bible.OnlineId}").ConfigureAwait(false);

        var matches = BookPattern.Matches(data);
        return matches.Cast<Match>()
            .Select(i => new Book
            {
                OnlineId = i.Groups[1].Value,
                Name = i.Groups[2].Value,
            })
            .Select(x => x with { Key = GetBookKey(x) })
            .ToList();
    }

    public override async Task<List<Chapter>> GetChaptersOnlineAsync(Book book)
    {
        var ver = book.Bible?.OnlineId ?? throw new InvalidOperationException("장 목록을 가져올 때 성경(translation) 참조가 없습니다.");
        var vol = book.OnlineId;

        var data = await Client.GetStringAsync($"/include/asp/chapinfo.asp?ver={ver}&vol={vol}").ConfigureAwait(false);

        var matches = ChapterPattern.Matches(data);
        return matches.Cast<Match>()
            .Select(i => new Chapter
            {
                OnlineId = i.Groups[1].Value,
                Number = int.Parse(i.Groups[1].Value, CultureInfo.InvariantCulture),
            })
            .OrderBy(c => c.Number)
            .ToList();
    }

    public override async Task<List<Verse>> GetVersesOnlineAsync(Chapter chapter)
    {
        var book = chapter.Book;
        var ver = book?.Bible?.OnlineId ?? throw new InvalidOperationException("구절 목록을 가져올 때 성경(translation) 참조가 없습니다.");
        var vol = book?.OnlineId ?? throw new InvalidOperationException("구절 목록을 가져올 때 책 참조가 없습니다.");
        var chap = chapter.Number;

        var data = await Client.GetStringAsync($"/read/reading_body.asp?ver={ver}&vol={vol}&chap={chap}").ConfigureAwait(false);

        var matches = VersePattern.Matches(data);
        var verses = new List<Verse>();

        foreach (Match m in matches)
        {
            verses.Add(new Verse
            {
                Number = ParseVerseNumber(m.Groups[1].Value),
                Text = ExtractVerseText(m.Groups[2].Value),
            });
        }

        return verses;
    }

    /// <summary>
    /// 절 번호를 정수로 변환한다.
    /// 합쳐진 구절은 range 번호("18-19")로 제공되며, 이때는 range의 첫 번호(18)를
    /// 절 번호로 사용해 downstream의 절 범위로 필터링할 수 있도록 한다.
    /// </summary>
    private static int ParseVerseNumber(string raw)
    {
        var first = raw.Split('-', 2)[0];
        return int.TryParse(first, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    /// <summary>
    /// 구절 <li> 블록에서 절 번호 스페이(bible-read-no)를 제거하고,
    /// 남은 dic-link 등 모든 HTML 태그를 제거하여 순수 텍스트만 반환한다.
    /// </summary>
    private static string ExtractVerseText(string body)
    {
        // 절 번호 스페이 제거
        body = VerseNoSpanPattern.Replace(body, "");
        // 나머지 HTML 태그 제거 (dic-link 등) — 기존 HTML 공백을 보존
        body = TagPattern.Replace(body, "");
        // 공백/줄바꿈 여러 개 압축
        body = WhitespacePattern.Replace(body, " ");
        return body.Trim();
    }

    private static BookKey GetBookKey(Book book) => book.OnlineId switch
    {
        "gen" => BookKey.Genesis,
        "exo" => BookKey.Exodus,
        "lev" => BookKey.Leviticus,
        "num" => BookKey.Numbers,
        "deu" => BookKey.Deuteronomy,
        "jos" => BookKey.Joshua,
        "jdg" => BookKey.Judges,
        "rut" => BookKey.Ruth,
        "1sa" => BookKey.ISamuel,
        "2sa" => BookKey.IISamuel,
        "1ki" => BookKey.IKings,
        "2ki" => BookKey.IIKings,
        "1ch" => BookKey.IChronicles,
        "2ch" => BookKey.IIChronicles,
        "ezr" => BookKey.Ezra,
        "neh" => BookKey.Nehemiah,
        "est" => BookKey.Esther,
        "job" => BookKey.Job,
        "psa" => BookKey.Psalms,
        "pro" => BookKey.Proverbs,
        "ecc" => BookKey.Ecclesiastes,
        "sng" => BookKey.SongOfSolomon,
        "isa" => BookKey.Isaiah,
        "jer" => BookKey.Jeremiah,
        "lam" => BookKey.Lamentations,
        "ezk" => BookKey.Ezekiel,
        "dan" => BookKey.Daniel,
        "hos" => BookKey.Hosea,
        "jol" => BookKey.Joel,
        "amo" => BookKey.Amos,
        "oba" => BookKey.Obadiah,
        "jnh" => BookKey.Jonah,
        "mic" => BookKey.Micah,
        "nam" => BookKey.Nahum,
        "hab" => BookKey.Habakkuk,
        "zep" => BookKey.Zephaniah,
        "hag" => BookKey.Haggai,
        "zec" => BookKey.Zechariah,
        "mal" => BookKey.Malachi,
        "mat" => BookKey.Matthew,
        "mrk" => BookKey.Mark,
        "luk" => BookKey.Luke,
        "jhn" => BookKey.John,
        "act" => BookKey.Acts,
        "rom" => BookKey.Romans,
        "1co" => BookKey.ICorinthians,
        "2co" => BookKey.IICorinthians,
        "gal" => BookKey.Galatians,
        "eph" => BookKey.Ephesians,
        "php" => BookKey.Philippians,
        "col" => BookKey.Colossians,
        "1th" => BookKey.IThessalonians,
        "2th" => BookKey.IIThessalonians,
        "1ti" => BookKey.ITimothy,
        "2ti" => BookKey.IITimothy,
        "tit" => BookKey.Titus,
        "phm" => BookKey.Philemon,
        "heb" => BookKey.Hebrews,
        "jas" => BookKey.James,
        "1pe" => BookKey.IPeter,
        "2pe" => BookKey.IIPeter,
        "1jn" => BookKey.IJohn,
        "2jn" => BookKey.IIJohn,
        "3jn" => BookKey.IIIJohn,
        "jud" => BookKey.Jude,
        "rev" => BookKey.Revelation,
        _ => throw new NotImplementedException($"서로 다른 책 코드: {book.OnlineId}"),
    };
}
