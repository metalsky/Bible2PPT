﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Bible2PPT.Bibles.Sources
{
    class GodpeopleBible : BibleSource
    {
        private const string BASE_URL = "http://find.godpeople.com";
        private static readonly Encoding ENCODING = Encoding.GetEncoding("EUC-KR");

        private BetterWebClient client = new BetterWebClient
        {
            BaseAddress = BASE_URL,
            Encoding = ENCODING,
        };

        public GodpeopleBible()
        {
            Name = "갓피플 성경";
        }

        public override List<Bible> GetBibles() => new List<Bible>
        {
            new Bible
            {
                Source = this,
                SequenceId = 0,
                BibleId = "rvsn",
                Version = "개역개정",
            },
            new Bible
            {
                Source = this,
                SequenceId = 1,
                BibleId = "ezsn",
                Version = "쉬운성경",
            },
        };

        public override List<BibleBook> GetBooks(Bible bible)
        {
            var data = client.DownloadString("/?page=bidx");
            var matches = Regex.Matches(data, @"option\s.+?'(.+?)'.+?(\d+).+?>(.+?)<");
            return matches.Cast<Match>().Select(match => new BibleBook
            {
                Source = this,
                Bible = bible,
                BookId = match.Groups[1].Value,
                Title = match.Groups[3].Value,
                ShortTitle = match.Groups[1].Value,
                ChapterCount = int.Parse(match.Groups[2].Value),
            }).ToList();
        }

        private static string EncodeString(string s) =>
            string.Join("", ENCODING.GetBytes(s).Select(b => $"%{b.ToString("X")}"));

        public override List<BibleChapter> GetChapters(BibleBook book) =>
            Enumerable.Range(1, book.ChapterCount.Value)
                .Select(i => new BibleChapter
                {
                    Source = this,
                    Book = book,
                    ChapterNumber = i,
                }).ToList();

        private static string StripHtmlTags(string s) => Regex.Replace(s, @"<.+?>", "", RegexOptions.Singleline);

        public override List<string> GetVerses(BibleChapter chapter)
        {
            var data = client.DownloadString($"/?page=bidx&kwrd={EncodeString(chapter.Book.BookId)}{chapter.ChapterNumber}&vers={chapter.Book.Bible.BibleId}");
            var matches = Regex.Matches(data, @"bidx_listTd_phrase.+?>(.+?)</td");
            return matches.Cast<Match>().Select(i => StripHtmlTags(i.Groups[1].Value)).ToList();
        }
    }
}