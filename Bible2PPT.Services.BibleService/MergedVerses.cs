using System.Collections.Generic;
using Bible2PPT.Services.BibleIndexService;

namespace Bible2PPT.Services.BibleService;

/// <summary>
/// 개역개정(4판)에서 2~3개의 절이 하나의 절로 합쳐져 저장되는 장과 그 범위.
/// 갓피아 사이트는 이 번역본 구절에 "합쳐진 절"을 range 번호(예: dataSec="18-19")로
/// 제공하므로, 저장된 구절은 range의 첫 번호(18) 하나로만 존재한다.
/// PPT 범위가 이 range에 오버플랩할 때 합쳐진 구절이 빠지지 않도록
/// (첫 번호만 저장하는) 범위를 관리한다.
/// </summary>
public static class MergedVerses
{
    /// (책, 장) -> (첫 절 번호, 마지막 절 번호).
    private static readonly Dictionary<(BookKey book, int chapter), (int first, int last)> _ranges = new()
    {
        { (BookKey.Deuteronomy, 6),  (18, 19) },
        { (BookKey.Deuteronomy, 15), (4, 5) },
        { (BookKey.Deuteronomy, 30), (9, 10) },
        { (BookKey.IChronicles, 16), (12, 13) },
        { (BookKey.Psalms, 92),      (1, 3) },
        { (BookKey.Psalms, 105),     (5, 6) },
        { (BookKey.Jeremiah, 32),    (3, 5) },
        { (BookKey.Jeremiah, 33),    (10, 11) },
        { (BookKey.Ezekiel, 24),     (4, 5) },
        { (BookKey.Acts, 15),        (25, 26) },
        { (BookKey.Romans, 9),       (1, 2) },
    };

    /// 지정된 장이 합쳐진 구절 장인지 확인하고, (첫 번호, 마지막 번호)를 반환한다.
    public static bool TryGetRange((BookKey book, int chapter) key, out (int first, int last) range)
        => _ranges.TryGetValue(key, out range);

    /// 구절이 PPT 범위(시작~끝)에 포함되는지 확인한다.
    /// - 일반 구절: startVerseNumber <= verseNumber && (end null이면 끝까지, 아니면) <= endVerseNumber
    /// - 합쳐진 구절(첫 번호만 저장): 요청 범위가 range에 오버플랩하는 경우 포함
    public static bool VerseInRange(
        int verseNumber,
        int startVerseNumber,
        int? endVerseNumber,
        BookKey? bookKey = null,
        int? chapterNumber = null)
    {
        bool normal = verseNumber >= startVerseNumber
            && (endVerseNumber is null || verseNumber <= endVerseNumber);
        if (normal)
            return true;

        if (bookKey is { } bk && chapterNumber is { } cn)
        {
            if (TryGetRange((bk, cn), out var r) && verseNumber == r.first)
            {
                bool overlap = r.last >= startVerseNumber
                    && (endVerseNumber is null || r.first <= endVerseNumber);
                if (overlap)
                    return true;
            }
        }

        return false;
    }
}
