using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bible2PPT.Bibles;
using Bible2PPT.Services.BibleService;

namespace Bible2PPT
{
    partial class MainForm
    {
        #region DB 편집 (좌측 메뉴)

        private ListView _dbBooksList = null!;
        private ListView _dbChaptersList = null!;
        private ListView _dbVersesList = null!;
        private ComboBox _dbBibleCombo = null!;
        private TextBox _dbVerseEditor = null!;
        private Button _dbSaveVerseButton = null!;
        private Button _dbReloadButton = null!;
        private Button _dbSaveAsButton = null!;
        private Button _dbRestoreButton = null!;
        private Label _dbSelectedLabel = null!;
        private Label _dbInfoLabel = null!;
        private Label _dbStatsLabel = null!;

        private List<Bible> _dbBibles = new();
        private Dictionary<int, List<Book>> _dbBooksByBibleId = new();
        private Dictionary<int, List<Chapter>> _dbChaptersByBookId = new();

        private Verse? _currentVerse;
        private Book? _currentBook;
        private bool _dbDirty;

        /// <summary>
        /// 첫 번째 선택된 ListView 항목을 반환한다(없으면 null).
        /// SelectedItems은 IEnumerable<ListViewItem> 형식으로, [i]로 접근한다.
        /// </summary>
        private static ListViewItem? FirstSelected(ListView list)
        {
            var items = list.SelectedItems;
            if (items is null || items.Count == 0)
            {
                return null;
            }

            return items[0];
        }

        private void InitializeDbComponent()
        {
            var dbRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            dbRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            dbRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            dbRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dbRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            dbRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // ===== 상단 도구바 =====
            var dbToolbar = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Margin = new Padding(4, 0, 0, 0),
                Padding = new Padding(0)
            };

            _dbReloadButton = NewDbToolbarButton("새로 불러오기");
            _dbSaveAsButton = NewDbToolbarButton("다른 이름으로 저장");
            _dbRestoreButton = NewDbToolbarButton("복원");
            _dbInfoLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(10, 10, 0, 0),
                Font = new Font("Microsoft YaHei", 9F)
            };

            dbToolbar.Controls.Add(_dbReloadButton);
            dbToolbar.Controls.Add(_dbSaveAsButton);
            dbToolbar.Controls.Add(_dbRestoreButton);
            dbToolbar.Controls.Add(_dbInfoLabel);

            _dbReloadButton.Click += (s, e) => ReloadDbPageAsync();
            _dbSaveAsButton.Click += DbSaveAsButton_Click;
            _dbRestoreButton.Click += DbRestoreButton_Click;

            dbRoot.Controls.Add(dbToolbar, 0, 0);

            // ===== 번역 선택 =====
            var dbBibleRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Margin = new Padding(4, 0, 0, 0),
                Padding = new Padding(0)
            };
            var dbBibleLabel = new Label
            {
                Text = "번역:",
                Margin = new Padding(0, 0, 4, 0),
                Font = new Font("Microsoft YaHei", 10F)
            };
            _dbBibleCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Margin = new Padding(0),
                Font = new Font("Microsoft YaHei", 10F)
            };
            _dbBibleCombo.SelectedIndexChanged += DbBibleCombo_SelectedIndexChanged;
            _dbStatsLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(12, 8, 0, 0),
                Font = new Font("Microsoft YaHei", 10F)
            };
            dbBibleRow.Controls.Add(dbBibleLabel);
            dbBibleRow.Controls.Add(_dbBibleCombo);
            dbBibleRow.Controls.Add(_dbStatsLabel);

            dbRoot.Controls.Add(dbBibleRow, 0, 1);

            // ===== 중부: 번역별 책 / 장 / 구절 =====
            var dbMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(2, 0, 0, 0)
            };
            dbMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            dbMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            dbMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51));
            dbMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _dbBooksList = new ListView
            {
                View = View.Details,
                Margin = new Padding(2),
                Font = new Font("Microsoft YaHei", 11F),
                FullRowSelect = true
            };
            _dbBooksList.Columns.Add("책", -1, HorizontalAlignment.Left);
            _dbBooksList.Columns.Add("장 수", 48, HorizontalAlignment.Center);

            _dbChaptersList = new ListView
            {
                View = View.Details,
                Margin = new Padding(2),
                Font = new Font("Microsoft YaHei", 11F),
                FullRowSelect = true
            };
            _dbChaptersList.Columns.Add("장", -1, HorizontalAlignment.Left);

            _dbVersesList = new ListView
            {
                View = View.Details,
                Margin = new Padding(2),
                Font = new Font("Microsoft YaHei", 11F),
                FullRowSelect = true
            };
            _dbVersesList.Columns.Add("절", 44, HorizontalAlignment.Center);
            _dbVersesList.Columns.Add("본문", -1, HorizontalAlignment.Left);

            _dbBooksList.SelectedIndexChanged += DbBooksList_SelectedIndexChanged;
            _dbChaptersList.SelectedIndexChanged += DbChaptersList_SelectedIndexChanged;
            _dbVersesList.SelectedIndexChanged += DbVersesList_SelectedIndexChanged;

            dbMain.Controls.Add(_dbBooksList, 0, 0);
            dbMain.Controls.Add(_dbChaptersList, 1, 0);
            dbMain.Controls.Add(_dbVersesList, 2, 0);

            dbRoot.Controls.Add(dbMain, 0, 2);

            // ===== 하단: 선택 구절 편집기 =====
            var dbEditorArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(2),
                Padding = new Padding(0)
            };
            dbEditorArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dbEditorArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            dbEditorArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            dbEditorArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _dbSelectedLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(4, 4, 4, 0),
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
            };
            _dbSelectedLabel.Text = "구절 선택";

            _dbVerseEditor = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Margin = new Padding(4, 0, 4, 4),
                Font = new Font("Microsoft YaHei", 12F),
                Enabled = false
            };

            _dbSaveVerseButton = new Button
            {
                Text = "이 구절 저장",
                AutoSize = false,
                Width = 120,
                Height = 40,
                Margin = new Padding(0, 0, 0, 4),
                Enabled = false
            };
            _dbSaveVerseButton.FlatAppearance.BorderSize = 1;

            _dbVerseEditor.TextChanged += DbVerseEditor_TextChanged;
            _dbSaveVerseButton.Click += DbSaveVerseButton_Click;

            dbEditorArea.Controls.Add(_dbSelectedLabel, 0, 0);
            dbEditorArea.Controls.Add(new Label { Visible = false }, 1, 0);
            dbEditorArea.Controls.Add(_dbVerseEditor, 0, 1);
            dbEditorArea.Controls.Add(_dbSaveVerseButton, 1, 1);

            dbRoot.Controls.Add(dbEditorArea, 0, 3);

            this.dbMultiPanelPage.Controls.Add(dbRoot);

            ReloadDbPageAsync();
        }

        private static Button NewDbToolbarButton(string text)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(2, 4, 2, 0),
                Font = new Font("Microsoft YaHei", 9F)
            };
            return button;
        }

        private static string TruncateText(string? text, int max)
        {
            text ??= string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        private static string HumanizeSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes}B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{Math.Round(bytes / 1024.0, 1)}KB";
            }

            return $"{Math.Round(bytes / 1024.0 / 1024.0, 1)}MB";
        }

        private void ReloadDbPageAsync()
        {
            var (valid, message) = DbFileService.ValidateActiveDb();
            if (!valid)
            {
                MessageBox.Show($"DB 파일 확인 실패:\n{message}", "DB 편집", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ClearDbPage();
                UpdateDbInfoLabel();
                return;
            }

            Task.Run(() =>
            {
                var catalog = _bibleService.GetStoredCatalog();

                _dbBibles = catalog.Bibles;
                _dbBooksByBibleId = new Dictionary<int, List<Book>>();
                _dbChaptersByBookId = new Dictionary<int, List<Chapter>>();

                foreach (var book in catalog.Books)
                {
                    if (!_dbBooksByBibleId.TryGetValue(book.BibleId, out var bibleBooks))
                    {
                        bibleBooks = new List<Book>();
                        _dbBooksByBibleId[book.BibleId] = bibleBooks;
                    }
                    bibleBooks.Add(book);
                }

                foreach (var chapter in catalog.Chapters)
                {
                    if (!_dbChaptersByBookId.TryGetValue(chapter.BookId, out var bookChapters))
                    {
                        bookChapters = new List<Chapter>();
                        _dbChaptersByBookId[chapter.BookId] = bookChapters;
                    }
                    bookChapters.Add(chapter);
                }

                this.Invoke(() => UpdateDbBookLists());
            });
        }

        private void UpdateDbBookLists()
        {
            _dbBibleCombo.Items.Clear();
            _dbBibleCombo.DisplayMember = nameof(Bible.Name);
            foreach (var bible in _dbBibles)
            {
                _dbBibleCombo.Items.Add(bible);
            }
            if (_dbBibleCombo.Items.Count > 0)
            {
                _dbBibleCombo.SelectedIndex = 0;
            }
            else
            {
                _dbBooksList.Items.Clear();
                _dbBooksList.Items.Add("DB에 저장된 성경이 없습니다.");
            }
        }

        private void DbBibleCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearDbPage();
            if (_dbBibleCombo.SelectedItem is not Bible selected)
            {
                return;
            }

            if (!_dbBooksByBibleId.TryGetValue(selected.Id, out var bibleBooks) || bibleBooks.Count == 0)
            {
                _dbBooksList.Items.Add($"[ {selected.Name} ]");
                return;
            }

            foreach (var book in bibleBooks.OrderBy(b => (int)b.Key))
            {
                int chapterCount = _dbChaptersByBookId.TryGetValue(book.Id, out var cl) ? cl.Count : book.ChapterCount;
                var item = new ListViewItem(book.Name)
                {
                    Tag = book
                };
                item.SubItems.Add($"{chapterCount}장");
                _dbBooksList.Items.Add(item);
            }
        }

        private void DbBooksList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _dbChaptersList.Items.Clear();
            _dbVersesList.Items.Clear();
            ClearVerseEditor();

                        var firstBookItem = FirstSelected(_dbBooksList);
            if (firstBookItem is null || firstBookItem.Tag is not Book book)
            {
                return;
            }

            _currentBook = book;

            if (!_dbChaptersByBookId.TryGetValue(book.Id, out var chapters))
            {
                chapters = new List<Chapter>();
            }

            foreach (var chapter in chapters)
            {
                var item = new ListViewItem($"{chapter.Number}장")
                {
                    Tag = chapter
                };
                _dbChaptersList.Items.Add(item);
            }

            UpdateDbSelectedLabel();
        }

        private void DbChaptersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _dbVersesList.Items.Clear();
            ClearVerseEditor();

                        var firstChapItem = FirstSelected(_dbChaptersList);
            if (firstChapItem is null || firstChapItem.Tag is not Chapter chapter)
            {
                return;
            }

            _dbVerseEditor.Enabled = false;
            _dbStatsLabel.Text = "구절 불러오는 중...";

            Task.Run(() =>
            {
                var verses = _bibleService.StoredVerses(chapter.Id);
                var items = verses
                    .Select(v => new ListViewItem
                    {
                        Text = $"{v.Number}",
                        Tag = v,
                        SubItems = { TruncateText(v.Text, 45) }
                    }).ToList();

                this.Invoke(() =>
                {
                    var firstChapItem = FirstSelected(_dbChaptersList);
                    if (firstChapItem?.Tag is not Chapter c || c.Id != chapter.Id)
                    {
                        return;
                    }

                    _dbVersesList.BeginUpdate();
                    _dbVersesList.Items.Clear();
                    foreach (var item in items)
                    {
                        _dbVersesList.Items.Add(item);
                    }
                    _dbVersesList.EndUpdate();

                    UpdateDbStatsLabel(chapter, verses.Count);
                });
            });
        }

        private void DbVersesList_SelectedIndexChanged(object sender, EventArgs e)
        {
                        var firstVerseItem = FirstSelected(_dbVersesList);
            if (firstVerseItem is null || firstVerseItem.Tag is not Verse verse)
            {
                return;
            }

            _currentVerse = verse;
            _dbVerseEditor.Text = verse.Text ?? string.Empty;
            _dbVerseEditor.Enabled = true;
            _dbSaveVerseButton.Enabled = true;
            _dbDirty = false;
            UpdateDbSelectedLabel();
        }

        private void DbVerseEditor_TextChanged(object sender, EventArgs e)
        {
            _dbDirty = true;
            UpdateDbSelectedLabel();
        }

        private void DbSaveVerseButton_Click(object sender, EventArgs e)
        {
            if (_currentVerse is null)
            {
                return;
            }

            string? text = _dbVerseEditor.Text;
            if (text is null || string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("구절 내용을 입력하세요.", "구절 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _bibleService.UpdateVerseText(_currentVerse, text);
                _currentVerse.Text = text;
                _dbDirty = false;

                // 목록에 수정된 내용을 반영
                var firstVerseItem = FirstSelected(_dbVersesList);
                if (firstVerseItem is not null)
                {
                    firstVerseItem.SubItems[0].Text = TruncateText(text, 45);
                }

                UpdateDbSelectedLabel();
                MessageBox.Show("구절이 DB에 저장되었습니다.", "구절 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구절 저장 실패:\n{ex.Message}", "구절 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearVerseEditor()
        {
            _currentVerse = null;
            _dbDirty = false;
            _dbVerseEditor.Text = string.Empty;
            _dbVerseEditor.Enabled = false;
            _dbSaveVerseButton.Enabled = false;
            UpdateDbSelectedLabel();
        }

        private void ClearDbPage()
        {
            _dbBooksList.Items.Clear();
            _dbChaptersList.Items.Clear();
            _dbVersesList.Items.Clear();
            ClearVerseEditor();
            _dbStatsLabel.Text = string.Empty;
            UpdateDbSelectedLabel();
        }

        private void UpdateDbSelectedLabel()
        {
            string path = string.Empty;
            if (_currentVerse is not null)
            {
                path = $"{_currentBook?.Name} ({_currentBook?.Bible?.Name}) - {_currentVerse.Chapter?.Number}장 {_currentVerse.Number}절";
            }
            else if (_currentBook is not null)
            {
                path = $"{_currentBook.Name} ({_currentBook.Bible?.Name})";
            }
            else
            {
                path = "구절 선택";
            }

            _dbSelectedLabel.Text = _dbDirty
                ? $"{path}  (수정됨)"
                : path;
        }

        private void UpdateDbStatsLabel(Chapter? chapter, int verseCount)
        {
            _dbStatsLabel.Text = chapter is null || verseCount == 0
                ? string.Empty
                : $"{verseCount}구절";
        }

        private void UpdateDbInfoLabel()
        {
            string path = DbFileService.ActiveDbPath;
            long size = File.Exists(path) ? new FileInfo(path).Length : 0;
            var meta = DbFileService.ReadActiveMeta();

            string info = $"{Path.GetFileName(path)} ({HumanizeSize(size)})";
            if (meta is not null)
            {
                info += $" | v{meta.Revision} ({meta.SavedAt:yyyy-MM-dd HH:mm})";
            }
            else
            {
                info += " | (리비전 없음)";
            }

            _dbInfoLabel.Text = "DB: " + info;
        }

        private void DbSaveAsButton_Click(object sender, EventArgs e)
        {
            var (valid, message) = DbFileService.ValidateActiveDb();
            if (!valid)
            {
                MessageBox.Show($"DB 파일 확인 실패:\n{message}", "DB 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var input = new NameInputForm("DB 저장 이름", Path.GetFileNameWithoutExtension(DbFileService.ActiveDbPath));
            if (input.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            string? name = input.GetText();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("저장할 이름을 입력하세요.", "DB 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var meta = DbFileService.SaveDbAs(name);
                MessageBox.Show(
                    $"DB가 저장되었습니다.\n\n이름: {meta.FileName}\n리비전: v{meta.Revision}\n저장 시간: {meta.SavedAt:yyyy-MM-dd HH:mm}",
                    "DB 저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateDbInfoLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB 저장 실패:\n{ex.Message}", "DB 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DbRestoreButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "DB 파일 (*.db)|*.db|모든 파일 (*.*)|*.*",
                Title = "복원할 DB 파일을 선택하세요"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var (valid, message) = DbFileService.ValidateDbFile(dialog.FileName);
            if (!valid)
            {
                MessageBox.Show($"선택한 파일은 유효한 DB가 아닙니다.\n{message}", "DB 복원 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var meta = DbFileService.ReadDbMeta(dialog.FileName);
            string description = meta is not null
                ? $"리비전: v{meta.Revision}\n저장 시간: {meta.SavedAt:yyyy-MM-dd HH:mm}\n이름: {meta.FileName}"
                : "리비전 정보 없음";

            if (DialogResult.Yes != MessageBox.Show(
                $"현재 DB를 아래 DB로 복원할까요?\n\n{description}\n\n복원하면 현재 DB 내용이 덮어쓰입니다.",
                "DB 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
            {
                return;
            }

            try
            {
                var restored = DbFileService.RestoreDbFile(dialog.FileName);
                string revInfo = restored is not null ? $"리비전: v{restored.Revision}" : "";
                MessageBox.Show(
                    $"DB가 복원되었습니다.\n{revInfo}",
                    "DB 복원 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ReloadDbPageAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB 복원 실패:\n{ex.Message}", "DB 복원 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion DB 편집 (좌측 메뉴)
    }
}
