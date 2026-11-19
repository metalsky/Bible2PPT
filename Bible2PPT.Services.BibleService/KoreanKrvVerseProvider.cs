using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Bible2PPT.Services.BibleIndexService;

namespace Bible2PPT.Services.BibleService;

/// <summary>
/// 갓피아 웹의 구절 엔드포인트가 더 이상 응답하지 않기 때문에
/// 개역개정(4판) 본문은 독립된 JSON 데이터셋을 사용해 제공한다.
/// </summary>
public sealed class KoreanKrvVerseProvider
{
    private static readonly KoreanKrvVerseProvider _instance = new();

    public static KoreanKrvVerseProvider Instance => _instance;

    private const string JsonUrl =
        "https://raw.githubusercontent.com/stranger828/bibleAPI/main/bible_structured.json";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
    };

    private readonly object _gate = new();
    private Task? _loadTask;
    private bool _ready;

    private readonly Dictionary<string, List<int>> _chapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<int, List<(int Number, string Text)>>> _verses =
        new(StringComparer.OrdinalIgnoreCase);

    private KoreanKrvVerseProvider() { }

    public bool IsReady => _ready;

    /// <summary>
    /// 데이터를 로드(다운로드 + 파싱)한다. 여러 번 호출해도 한 번만 로드한다.
    /// </summary>
    public Task EnsureLoadedAsync()
    {
        lock (_gate)
        {
            if (_ready)
                return Task.CompletedTask;
            if (_loadTask is null || _loadTask.IsFaulted)
                _loadTask = LoadAsync();
            return _loadTask!;
        }
    }

    public IReadOnlyList<int> GetChapters(BookKey bookKey)
    {
        var abbr = Abbreviations[bookKey];
        return _chapters.TryGetValue(abbr, out var chs) ? chs : Array.Empty<int>();
    }

    public IReadOnlyList<(int Number, string Text)> GetVerses(BookKey bookKey, int chapter)
    {
        var abbr = Abbreviations[bookKey];
        if (_verses.TryGetValue(abbr, out var bookVerses) &&
            bookVerses.TryGetValue(chapter, out var verses))
        {
            return verses;
        }

        return Array.Empty<(int, string)>();
    }

    private async Task LoadAsync()
    {
        var path = GetLocalJsonPath();

        if (!File.Exists(path))
        {
            var dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var downloadedJson = await _http.GetStringAsync(JsonUrl).ConfigureAwait(false);
            File.WriteAllText(path, downloadedJson);
        }

        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<VerseEntry>>(json) ?? new List<VerseEntry>();

        var verses = new Dictionary<string, Dictionary<int, List<(int, string)>>>
            (StringComparer.OrdinalIgnoreCase);
        var chapters = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.book) || e.chapter <= 0 || e.verse <= 0 || string.IsNullOrEmpty(e.content))
                continue;

            if (!chapters.ContainsKey(e.book))
            {
                chapters[e.book] = new List<int>();
                verses[e.book] = new Dictionary<int, List<(int, string)>>();
            }

            chapters[e.book].Add(e.chapter);

            if (!verses[e.book].ContainsKey(e.chapter))
                verses[e.book][e.chapter] = new List<(int, string)>();
            verses[e.book][e.chapter].Add((e.verse, e.content));
        }

        _chapters.Clear();
        foreach (var kv in chapters)
            _chapters[kv.Key] = kv.Value.Distinct().OrderBy(v => v).ToList();

        _verses.Clear();
        foreach (var kv in verses)
            _verses[kv.Key] = kv.Value;

        _ready = true;
    }

    private static string GetLocalJsonPath()
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appDataDir, "Bible2PPT");
        return Path.Combine(dir, "krv-bible.json");
    }

    /// <summary>
    /// BookKey -> 개역개정 4판 책 약자 (JSON의 "book" 필드와 일치)
    /// </summary>
    private static readonly Dictionary<BookKey, string> Abbreviations = new()
    {
        [BookKey.Genesis] = "창",
        [BookKey.Exodus] = "출",
        [BookKey.Leviticus] = "레",
        [BookKey.Numbers] = "민",
        [BookKey.Deuteronomy] = "신",
        [BookKey.Joshua] = "수",
        [BookKey.Judges] = "삿",
        [BookKey.Ruth] = "룻",
        [BookKey.ISamuel] = "삼상",
        [BookKey.IISamuel] = "삼하",
        [BookKey.IKings] = "왕상",
        [BookKey.IIKings] = "왕하",
        [BookKey.IChronicles] = "대상",
        [BookKey.IIChronicles] = "대하",
        [BookKey.Ezra] = "스",
        [BookKey.Nehemiah] = "느",
        [BookKey.Esther] = "에",
        [BookKey.Job] = "욥",
        [BookKey.Psalms] = "시",
        [BookKey.Proverbs] = "잠",
        [BookKey.Ecclesiastes] = "전",
        [BookKey.SongOfSolomon] = "아",
        [BookKey.Isaiah] = "사",
        [BookKey.Jeremiah] = "렘",
        [BookKey.Lamentations] = "애",
        [BookKey.Ezekiel] = "겔",
        [BookKey.Daniel] = "단",
        [BookKey.Hosea] = "호",
        [BookKey.Joel] = "욕",
        [BookKey.Amos] = "암",
        [BookKey.Obadiah] = "옵",
        [BookKey.Jonah] = "욕",
        [BookKey.Micah] = "미",
        [BookKey.Nahum] = "나",
        [BookKey.Habakkuk] = "합",
        [BookKey.Zephaniah] = "습",
        [BookKey.Haggai] = "학",
        [BookKey.Zechariah] = "습",
        [BookKey.Malachi] = "말",
        [BookKey.Matthew] = "마",
        [BookKey.Mark] = "막",
        [BookKey.Luke] = "눅",
        [BookKey.John] = "요",
        [BookKey.Acts] = "행",
        [BookKey.Romans] = "롬",
        [BookKey.ICorinthians] = "고전",
        [BookKey.IICorinthians] = "고후",
        [BookKey.Galatians] = "갈",
        [BookKey.Ephesians] = "엡",
        [BookKey.Philippians] = "빌",
        [BookKey.Colossians] = "골",
        [BookKey.IThessalonians] = "살전",
        [BookKey.IIThessalonians] = "살후",
        [BookKey.ITimothy] = "딤전",
        [BookKey.IITimothy] = "딤후",
        [BookKey.Titus] = "딛",
        [BookKey.Philemon] = "몬",
        [BookKey.Hebrews] = "히",
        [BookKey.James] = "약",
        [BookKey.IPeter] = "벧전",
        [BookKey.IIPeter] = "벧후",
        [BookKey.IJohn] = "요일",
        [BookKey.IIJohn] = "요이",
        [BookKey.IIIJohn] = "요삼",
        [BookKey.Jude] = "유",
        [BookKey.Revelation] = "계",
    };

    private sealed class VerseEntry
    {
        public string book { get; set; } = "";
        public int chapter { get; set; }
        public int verse { get; set; }
        public string content { get; set; } = "";
    }
}
