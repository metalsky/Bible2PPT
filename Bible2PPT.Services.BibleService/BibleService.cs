using Bible2PPT.Bibles;
using Bible2PPT.Data;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Sources;
using Microsoft.EntityFrameworkCore;

namespace Bible2PPT.Services.BibleService;

public class BibleService
{
    private readonly IDbContextFactory<BibleContext> _dbFactory;
    private readonly BibleIndexService.BibleIndexService _bibleIndexService;

    public BibleService(IDbContextFactory<BibleContext> dbFactory, BibleIndexService.BibleIndexService bibleIndexService)
    {
        _dbFactory = dbFactory;
        _bibleIndexService = bibleIndexService;
    }



    public Bible? FindBible(int id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Bibles.Find(id);
    }

    public async ValueTask ClearCachesAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
    }

    private void LinkSource(BibleBase target, BibleSource source)
    {
        target.Source = source;
        target.SourceId = source.Id;
    }

    private void LinkSource(BibleBase target, BibleBase source)
    {
        LinkSource(target, source.Source);
    }

    public async Task<List<Bible>> GetBiblesAsync(BibleSource source)
    {
        // 캐시가 있으면 사용
        var bibles = GetCached();
        if (bibles.Any())
        {
            return bibles;
        }
        // 캐시가 없으면 온라인에서 가져와서 저장
        bibles = await source.GetBiblesOnlineAsync().ConfigureAwait(false);
        bibles.ForEach(bible => LinkSource(bible, source));
        Cache();
        return bibles;

        List<Bible> GetCached()
        {
            using var db = _dbFactory.CreateDbContext();
            return db.Bibles.Where(i => i.SourceId == source.Id).ToList();
        }

        void Cache()
        {
            using var db = _dbFactory.CreateDbContext();
            db.Bibles.AddRange(bibles);
            db.SaveChanges();
        }
    }

    public async Task<List<Book>> GetBooksAsync(Bible bible)
    {
        // 캐시가 있으면 사용
        var books = GetCached();
        if (books.Any())
        {
            books.ForEach(LinkInfo);
            return books;
        }
        // 캐시가 없으면 온라인에서 가져와서 저장
        books = await bible.Source.GetBooksOnlineAsync(bible).ConfigureAwait(false);
        books.ForEach(LinkForeigns);
        Cache();
        books.ForEach(LinkInfo);
        return books;

        List<Book> GetCached()
        {
            using var db = _dbFactory.CreateDbContext();
            return db.Books.Where(i => i.BibleId == bible.Id).ToList();
        }

        void LinkForeigns(Book book)
        {
            LinkSource(book, bible);
            book.Bible = bible;
            book.BibleId = bible.Id;
        }

        void Cache()
        {
            using var db = _dbFactory.CreateDbContext();
            db.Bibles.Attach(bible);
            db.Books.AddRange(books);
            db.SaveChanges();
        }

        void LinkInfo(Book book)
        {
            book.NameInfo = _bibleIndexService.GetBookName(book.Key, bible.LanguageCode);
            book.AbbreviationInfo = _bibleIndexService.GetBookAbbreviation(book.Key, bible.LanguageCode);
        }
    }

    public async Task<List<Chapter>> GetChaptersAsync(Book? book)
    {
        if (book is null)
        {
            return new List<Chapter>();
        }

        // 캐시가 있으면 사용
        var chapters = GetCached();
        if (chapters.Any())
        {
            return chapters;
        }
        // 캐시가 없으면 온라인에서 가져와서 저장
        chapters = await book.Source.GetChaptersOnlineAsync(book).ConfigureAwait(false);
        chapters.ForEach(LinkForeigns);
        chapters.Sort((a, b) => a.Number.CompareTo(b.Number));
        Cache();
        return chapters;

        List<Chapter> GetCached()
        {
            using var db = _dbFactory.CreateDbContext();
            return db.Chapters.Where(i => i.BookId == book.Id).ToList();
        }

        void LinkForeigns(Chapter chapter)
        {
            LinkSource(chapter, book);
            chapter.Book = book;
            chapter.BookId = book.Id;
        }

        void Cache()
        {
            using var db = _dbFactory.CreateDbContext();
            db.Books.Attach(book);
            db.Chapters.AddRange(chapters);
            db.SaveChanges();
        }
    }

    public async Task<List<Verse>> GetVersesAsync(Chapter? chapter)
    {
        if (chapter is null)
        {
            return new List<Verse>();
        }

        // 캐시가 있으면 사용
        var verses = GetCached();

        // 개역개정(4판)은 2~3개의 절이 하나의 절로 합쳐져 있는 구절이 있다(신6:18-19, 시92:1-3 등).
        // 과거에 저장된 캐시가 이 합쳐진 구절을 누락(공백)한 경우, 소스에서 다시 가져와 갱신한다.
        if (IsMergedVerseChapter(chapter, out var mergedVerseNumber) &&
            !MergedVersePresent(verses, mergedVerseNumber))
        {
            var freshVerses = await chapter.Source.GetVersesOnlineAsync(chapter).ConfigureAwait(false);
            freshVerses = freshVerses.Where(v => v.Number > 0).ToList();

            // 소스가 합쳐진 구절을 포함하는지 확인(다른 번역본 소스는 해당 구절이 없을 수 있음)
            if (freshVerses.Any(v => v.Number == mergedVerseNumber))
            {
                freshVerses.ForEach(v => LinkForeigns(v));
                freshVerses.Sort((a, b) => a.Number.CompareTo(b.Number));
                ReplaceChapterVersesAsync(chapter, freshVerses);
                return freshVerses;
            }
        }

        // 캐시가 없으면 온라인에서 가져와서 저장
        if (!verses.Any())
        {
            verses = await chapter.Source.GetVersesOnlineAsync(chapter).ConfigureAwait(false);
            verses.ForEach(v => LinkForeigns(v));
            verses.Sort((a, b) => a.Number.CompareTo(b.Number));
            Cache();
        }

        return verses;

        List<Verse> GetCached()
        {
            using var db = _dbFactory.CreateDbContext();
            return db.Verses.Where(i => i.ChapterId == chapter.Id).ToList();
        }

        void LinkForeigns(Verse verse)
        {
            LinkSource(verse, chapter);
            verse.Chapter = chapter;
            verse.ChapterId = chapter.Id;
        }

        void Cache()
        {
            using var db = _dbFactory.CreateDbContext();
            db.Chapters.Attach(chapter);
            db.Verses.AddRange(verses);
            db.SaveChanges();
        }
    }

    /// <summary>
    /// 지정된 장이 합쳐진 구절 장인지 확인하고, 합쳐진 구절의 첫 절 번호를 반환한다.
    /// </summary>
    private bool IsMergedVerseChapter(Chapter chapter, out int mergedVerseNumber)
    {
        var book = chapter.Book;
        if (book is null)
        {
            mergedVerseNumber = 0;
            return false;
        }

        if (MergedVerses.TryGetRange((book.Key, chapter.Number), out var range))
        {
            mergedVerseNumber = range.first;
            return true;
        }

        mergedVerseNumber = 0;
        return false;
    }

    /// <summary>
    /// 구절 목록에 합쳐진 구절(첫 절 번호)이 포함되어 있는지 확인한다.
    /// </summary>
    private static bool MergedVersePresent(List<Verse> verses, int mergedVerseNumber)
    {
        return verses.Any(v => v.Number == mergedVerseNumber);
    }

    /// <summary>
    /// 장의 기존 구절을 제거하고 새 구절로 교체한다.
    /// </summary>
    private void ReplaceChapterVersesAsync(Chapter chapter, List<Verse> verses)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = db.Verses.Where(v => v.ChapterId == chapter.Id).ToList();
        db.Verses.RemoveRange(existing);
        db.Chapters.Attach(chapter);
        db.Verses.AddRange(verses);
        db.SaveChanges();
    }

    /// <summary>
    /// DB에 저장된 성경 전체 정보(서역/책/장)를 한 번에 반환한다.
    /// </summary>
    public sealed class DbCatalog
    {
        public List<Bible> Bibles { get; }
        public List<Book> Books { get; }
        public List<Chapter> Chapters { get; }

        public DbCatalog(List<Bible> bibles, List<Book> books, List<Chapter> chapters)
        {
            Bibles = bibles;
            Books = books;
            Chapters = chapters;
        }
    }

    /// <summary>
    /// DB(에디터용)에 저장된 모든 서역/책/장을 반환한다.
    /// </summary>
    public DbCatalog GetStoredCatalog()
    {
        using var db = _dbFactory.CreateDbContext();
        var bibles = db.Bibles.ToList();
        var books = db.Books.Include(b => b.Bible).ToList();
        var chapters = db.Chapters.Include(c => c.Book).ToList();
        return new DbCatalog(bibles, books, chapters);
    }

    /// <summary>
    /// 지정된 장에 저장된 구절(텍스트 포함)을 반환한다.
    /// </summary>
    public IReadOnlyList<Verse> StoredVerses(int chapterId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Verses.Where(i => i.ChapterId == chapterId).OrderBy(v => v.Number).ToList();
    }

    /// <summary>
    /// 구절 텍스트를 DB에 저장한다.
    /// </summary>
    public void UpdateVerseText(Verse verse, string newText)
    {
        using var db = _dbFactory.CreateDbContext();
        var v = db.Verses.Find(verse.Id);
        if (v is not null)
        {
            v.Text = newText;
            db.SaveChanges();
        }
    }
}
