using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
///   번역 목록 : (내부 정의, 8개)
///   책   : GET /read/reading.asp?ver={ver}
///   장   : GET /include/asp/chapinfo.asp?ver={ver}&vol={vol}
///   구절 : GET /read/reading_body.asp?ver={ver}&vol={volname}&chap={chap}
/// </summary>
public class GodpiaBible : BibleSource
{
    private const string BaseUrl = "https://www.godpia.com";

    /// <summary>HTTP 요청 재시도 횟수 (초기 1초, 2초 간격으로 지수적 백오프).</summary>
    private const int MaxRetryAttempts = 3;

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
    // 갓피아 사이트는 <li dataSec="1"> -> <li datasec="1">로 속성 대소를 변경했다.
    // (2026-11 갓피아 재설치 후) 둘 다 지원하도록 data[Ss]ec로 매칭한다.
    private static readonly Regex VersePattern = new(
        @"<li[^>]*data[Ss]ec=""(\d+(?:-\d+)?)""[^>]*>([\s\S]*?)</li>",
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

    /// <summary>
    /// 갓피아 책 목록 HTML에서 책을 파싱한다.
    /// (HTTP와 분리되어 테스트에 쓸 수 있는 정적 메소드)
    /// </summary>
    public static List<Book> ParseBooks(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidDataException("갓피아 책 목록 HTML이 비어 있습니다.");
        }

        return BookPattern.Matches(html).Cast<Match>()
            .Select(i => new Book
            {
                OnlineId = i.Groups[1].Value,
                Name = i.Groups[2].Value,
            })
            .Select(x => x with { Key = GetBookKey(x) })
            .ToList();
    }

    /// <summary>
    /// 갓피아 장 목록 HTML에서 장을 파싱한다.
    /// (HTTP와 분리되어 테스트에 쓸 수 있는 정적 메소드)
    /// </summary>
    public static List<Chapter> ParseChapters(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidDataException("갓피아 장 목록 HTML이 비어 있습니다.");
        }

        return ChapterPattern.Matches(html).Cast<Match>()
            .Select(i => new Chapter
            {
                OnlineId = i.Groups[1].Value,
                Number = int.Parse(i.Groups[1].Value, CultureInfo.InvariantCulture),
            })
            .OrderBy(c => c.Number)
            .ToList();
    }

    /// <summary>
    /// 갓피아 구절 목록 HTML에서 구절을 파싱한다.
    /// (HTTP와 분리되어 테스트에 쓸 수 있는 정적 메소드)
    /// </summary>
    public static List<Verse> ParseVerses(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidDataException("갓피아 구절 목록 HTML이 비어 있습니다.");
        }

        var matches = VersePattern.Matches(html);
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

    public override async Task<List<Book>> GetBooksOnlineAsync(Bible bible)
    {
        var data = await GetStringWithRetryAsync($"/read/reading.asp?ver={bible.OnlineId}").ConfigureAwait(false);
        return ParseBooks(data);
    }

    public override async Task<List<Chapter>> GetChaptersOnlineAsync(Book book)
    {
        var ver = book.Bible?.OnlineId ?? throw new InvalidOperationException("장 목록을 가져올 때 성경(translation) 참조가 없습니다.");
        var vol = book.OnlineId;

        var data = await GetStringWithRetryAsync($"/include/asp/chapinfo.asp?ver={ver}&vol={vol}").ConfigureAwait(false);
        return ParseChapters(data);
    }

    public override async Task<List<Verse>> GetVersesOnlineAsync(Chapter chapter)
    {
        var book = chapter.Book;
        var ver = book?.Bible?.OnlineId ?? throw new InvalidOperationException("구절 목록을 가져올 때 성경(translation) 참조가 없습니다.");
        var vol = book?.OnlineId ?? throw new InvalidOperationException("구절 목록을 가져올 때 책 참조가 없습니다.");
        var chap = chapter.Number;

        var data = await GetStringWithRetryAsync($"/read/reading_body.asp?ver={ver}&vol={vol}&chap={chap}").ConfigureAwait(false);
        return ParseVerses(data);
    }

    /// <summary>
    /// 갓피아 페이지를 가져온다.
    /// 일시적인 네트워크/HTTP 오류 시 지수적 백오프(1초, 2초)를 붙여 재시도하고,
    /// 재시도 횟수를 초과하면 UI의 기존 재시도 다이얼로그가
    /// 잡도록 <see cref="WebException"/>을 던진다.
    /// </summary>
    private static async Task<string> GetStringWithRetryAsync(string relativePath)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                using var response = await Client.GetAsync(relativePath).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 취소 요청됨: 재시도하지 않고 전파
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == MaxRetryAttempts)
                {
                    break;
                }

                // 지수적 백오프: 1초, 2초
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }

        var message = $"갓피아 성경 사이트({BaseUrl})에 연결할 수 없습니다. ({relativePath})\n{lastException?.Message}";
        throw new WebException(message);
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
