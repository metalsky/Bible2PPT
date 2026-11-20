using System.Collections.Generic;
using Xunit;

using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;

namespace Bible2PPT.Services.BibleService.Tests;

/// <summary>
/// 합쳐진 구절(range) 범위 포함 판별 로직 검증.
/// 갓피아는 "신6:18-19"를 첫 번호(18) 하나로만 저장하므로,
/// 요청 범위가 range에 오버플랩하는 경우 그 구절이 PPT에 포함되도록
/// <see cref="MergedVerses.VerseInRange"/>를 검증한다.
/// </summary>
public class MergedVersesTests
{
    // (요청하는 구절, 시작, 끝, 책, 장, 포함 여부)
    [Theory]
    [InlineData(18, 1, null, BookKey.Deuteronomy, 6, true)]    // 신6 전체
    [InlineData(18, 18, 18, BookKey.Deuteronomy, 6, true)]   // 신6:18 (정상)
    [InlineData(18, 19, 19, BookKey.Deuteronomy, 6, true)]   // 신6:19 -> 합쳐진 18절 포함 (핵심 수정)
    [InlineData(18, 18, 19, BookKey.Deuteronomy, 6, true)]   // 신6:18-19
    [InlineData(18, 17, 19, BookKey.Deuteronomy, 6, true)]   // 신6:17-19
    [InlineData(18, 18, 25, BookKey.Deuteronomy, 6, true)]   // 신6:18-말
    [InlineData(18, 20, 25, BookKey.Deuteronomy, 6, false)]  // 신6:20~ -> 제외
    [InlineData(18, 1, 17, BookKey.Deuteronomy, 6, false)]   // 신6:1~17 -> 제외
    [InlineData(20, 19, 19, BookKey.Deuteronomy, 6, false)]  // 신6:20 (합치 range 밖 -> 제외)

    [InlineData(1, 2, 2, BookKey.Psalms, 92, true)]    // 시92:2 -> 합쳐진 1-3절 포함 (핵심 수정)
    [InlineData(1, 3, 3, BookKey.Psalms, 92, true)]    // 시92:3 -> 포함
    [InlineData(1, 4, 100, BookKey.Psalms, 92, false)] // 시92:4~ -> 제외

    [InlineData(3, 5, 5, BookKey.Jeremiah, 32, true)]   // 렘32:5 -> 합쳐진 3-5절 포함
    [InlineData(3, 4, 4, BookKey.Jeremiah, 32, true)]   // 렘32:4 -> 포함
    [InlineData(3, 6, 6, BookKey.Jeremiah, 32, false)]  // 렘32:6 -> 제외

    [InlineData(5, 5, 5, BookKey.Psalms, 105, true)]    // 시105:5 -> 합쳐진 5-6절 포함
    [InlineData(5, 6, 6, BookKey.Psalms, 105, true)]    // 시105:6 -> 포함
    [InlineData(5, 7, 7, BookKey.Psalms, 105, false)]   // 시105:7 -> 제외

    // 합쳐지지 않은 일반 장: 정상 범위 판별만
    [InlineData(1, 1, 1, BookKey.Deuteronomy, 6, true)]
    [InlineData(1, 2, 2, BookKey.Deuteronomy, 6, false)]
    [InlineData(25, 1, null, BookKey.Deuteronomy, 6, true)]

    [InlineData(1, 1, 1, BookKey.Genesis, 1, true)]    // 비합치 장
    [InlineData(31, 1, null, BookKey.Genesis, 1, true)]

    [InlineData(18, 19, 19, null, 6, false)]            // bookKey 없음: merge 판별 불가 -> 제외
    public void VerseInRange_ExpectedResult(
        int verseNumber, int start, int? end, BookKey? bookKey, int? chapterNumber, bool expected)
    {
        Assert.Equal(expected, MergedVerses.VerseInRange(verseNumber, start, end, bookKey, chapterNumber));
    }

    [Fact]
    public void TryGetRange_KnownMergedChapters()
    {
        Assert.True(MergedVerses.TryGetRange((BookKey.Deuteronomy, 6), out var r6));
        Assert.Equal((18, 19), r6);
        Assert.True(MergedVerses.TryGetRange((BookKey.Jeremiah, 32), out var r32));
        Assert.Equal((3, 5), r32);
        Assert.False(MergedVerses.TryGetRange((BookKey.Genesis, 1), out _));
        Assert.False(MergedVerses.TryGetRange((BookKey.Deuteronomy, 7), out _));
    }
}
