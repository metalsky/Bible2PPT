#nullable enable
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
    /// <summary>
    /// DB 편집 창.
    /// 번역(서역) / 책 / 장 / 절 트리 탐색, 구절 편집, 다른이름으로 저장, 복원,
    /// 리비전/버전 관리 및 잘못된 DB 읽기 방지.
    /// 1024x786 이상 화면에 최적화된 리사이즈 가능한 창으로, 항상 현재 테마를
    /// 따른다(라이트=검정 글씨, 다크=밝은 글씨) — 이전 '무조건 블랙' 문제를 막기 위해
    /// 창이 표시될 때 테마를 재적용한다.
    /// </summary>
    public sealed class DbEditorForm : Form
    {
        private readonly BibleService _bibleService;

        private ListView _booksList;
        private ListView _chaptersList;
        private ListView _versesList;
        private ComboBox _bibleCombo;
        private TextBox _verseEditor;
        private Label _selectedLabel;
        private Label _statsLabel;
        private Label _infoLabel;
        private Button _reloadBtn;
        private Button _saveAsBtn;
        private Button _restoreBtn;
        private Button _saveVerseBtn;

        private List<Bible> _bibles;
        private readonly Dictionary<int, List<Book>> _booksByBibleId = new();
        private readonly Dictionary<int, List<Chapter>> _chaptersByBookId = new();

        private Verse? _currentVerse;
        private Book? _currentBook;
        private bool _dirty;
        private bool _shownOnce;

        public DbEditorForm(BibleService bibleService)
        {
            _bibleService = bibleService;
            Text = "DB 편집";
            ClientSize = new Size(1124, 866);
            MinimumSize = new Size(1024, 786);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            ShowInTaskbar = true;

            BuildUi();
            ThemeManager.Apply(this);

            // 창이 완전히 보이므로 테마를 한 번 더 적용해 '무조건 블랙' 문제를 차단한다.
            Shown += OnShown;
            LoadCatalogAsync();
        }

        private void OnShown(object sender, EventArgs e)
        {
            if (!_shownOnce)
            {
                _shownOnce = true;
                ThemeManager.Apply(this);
            }
        }

        private static Font MakeFont(float size, bool bold = false)
        {
            return new Font("Microsoft YaHei", size, bold ? FontStyle.Bold : FontStyle.Regular);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8),
                ColumnCount = 1,
                RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // 상단 도구바
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));  // 번역 선택
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 책/장/절
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180)); // 구절 편집기

            // ===== 상단 도구바: 버튼들 한 줄에 =====
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _reloadBtn = DbToolbarButton("새로 불러오기");
            _saveAsBtn = DbToolbarButton("다른 이름으로 저장");
            _restoreBtn = DbToolbarButton("복원");
            _infoLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(16, 14, 0, 0),
                Font = MakeFont(10)
            };
            _reloadBtn.Click += (s, e) => LoadCatalogAsync();
            _saveAsBtn.Click += DbSaveAsButton_Click;
            _restoreBtn.Click += DbRestoreButton_Click;
            toolbar.Controls.Add(_reloadBtn);
            toolbar.Controls.Add(_saveAsBtn);
            toolbar.Controls.Add(_restoreBtn);
            toolbar.Controls.Add(_infoLabel);
            root.Controls.Add(toolbar, 0, 0);

            // ===== 번역(서역) 선택: 크게, 한 줄 =====
            var transRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            transRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            transRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            transRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            transRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var transLabel = new Label
            {
                Text = "번역:",
                Margin = new Padding(0, 0, 10, 0),
                Font = MakeFont(13),
                AutoSize = true
            };
            _bibleCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Height = 44,
                Font = MakeFont(13)
            };
            _bibleCombo.SelectedIndexChanged += DbBibleCombo_SelectedIndexChanged;
            _statsLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(16, 16, 0, 0),
                Font = MakeFont(11)
            };

            transRow.Controls.Add(transLabel, 0, 0);
            transRow.Controls.Add(_bibleCombo, 1, 0);
            transRow.Controls.Add(_statsLabel, 2, 0);
            root.Controls.Add(transRow, 0, 1);

            // ===== 중부: 책 / 장 / 절 (가로 3칸, 본문 너비 넓게) =====
            var lists = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            lists.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _booksList = new ListView
            {
                View = View.Details,
                Margin = new Padding(1),
                Font = MakeFont(11),
                FullRowSelect = true
            };
            _booksList.Columns.Add("책", -1, HorizontalAlignment.Left);
            _booksList.Columns.Add("장 수", 56, HorizontalAlignment.Center);

            _chaptersList = new ListView
            {
                View = View.Details,
                Margin = new Padding(1),
                Font = MakeFont(11),
                FullRowSelect = true
            };
            _chaptersList.Columns.Add("장", -1, HorizontalAlignment.Left);

            _versesList = new ListView
            {
                View = View.Details,
                Margin = new Padding(1),
                Font = MakeFont(11),
                FullRowSelect = true
            };
            _versesList.Columns.Add("절", 52, HorizontalAlignment.Center);
            _versesList.Columns.Add("본문", -1, HorizontalAlignment.Left);

            _booksList.SelectedIndexChanged += DbBooksList_SelectedIndexChanged;
            _chaptersList.SelectedIndexChanged += DbChaptersList_SelectedIndexChanged;
            _versesList.SelectedIndexChanged += DbVersesList_SelectedIndexChanged;

            lists.Controls.Add(_booksList, 0, 0);
            lists.Controls.Add(_chaptersList, 1, 0);
            lists.Controls.Add(_versesList, 2, 0);
            root.Controls.Add(lists, 0, 2);

            // ===== 하단: 선택 구절 편집기 =====
            var editor = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0)
            };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _selectedLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(6, 4, 6, 0),
                Font = MakeFont(11, true),
                Text = "구절 선택"
            };

            _verseEditor = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = true,
                Margin = new Padding(6, 0, 6, 0),
                Font = MakeFont(12),
                Enabled = false
            };

            _saveVerseBtn = new Button
            {
                Text = "이 구절 저장",
                AutoSize = false,
                Width = 128,
                Height = 46,
                Margin = new Padding(0, 0, 0, 4),
                Enabled = false,
                FlatStyle = FlatStyle.Flat
            };
            _saveVerseBtn.FlatAppearance.BorderSize = 1;

            _verseEditor.TextChanged += DbVerseEditor_TextChanged;
            _saveVerseBtn.Click += DbSaveVerseButton_Click;

            editor.Controls.Add(_selectedLabel, 0, 0);
            editor.Controls.Add(new Label { Visible = false }, 1, 0);
            editor.Controls.Add(_verseEditor, 0, 1);
            editor.Controls.Add(_saveVerseBtn, 1, 1);
            root.Controls.Add(editor, 0, 3);

            Controls.Add(root);
        }

        private static Button DbToolbarButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(2, 6, 2, 0),
                Font = new Font("Microsoft YaHei", 10.5f)
            };
        }

        private static ListViewItem? FirstSelected(ListView list)
        {
            var items = list.SelectedItems;
            if (items is null || items.Count == 0)
            {
                return null;
            }

            return items[0];
        }

        // ===== DB 카탈로그 로드 =====
        private void LoadCatalogAsync()
        {
            var (valid, message) = DbFileService.ValidateActiveDb();
            if (!valid)
            {
                MessageBox.Show($"DB 파일 확인 실패:\n{message}", "DB 편집", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ClearPage();
                UpdateInfoLabel();
                return;
            }

            Task.Run(() =>
            {
                var catalog = _bibleService.GetStoredCatalog();

                _bibles = catalog.Bibles;
                _booksByBibleId.Clear();
                _chaptersByBookId.Clear();

                foreach (var book in catalog.Books)
                {
                    if (!_booksByBibleId.TryGetValue(book.BibleId, out var bibleBooks))
                    {
                        bibleBooks = new List<Book>();
                        _booksByBibleId[book.BibleId] = bibleBooks;
                    }
                    bibleBooks.Add(book);
                }

                foreach (var chapter in catalog.Chapters)
                {
                    if (!_chaptersByBookId.TryGetValue(chapter.BookId, out var bookChapters))
                    {
                        bookChapters = new List<Chapter>();
                        _chaptersByBookId[chapter.BookId] = bookChapters;
                    }
                    bookChapters.Add(chapter);
                }

                this.Invoke(() =>
                {
                    UpdateBookLists();
                    UpdateInfoLabel();
                });
            });
        }

        private void UpdateBookLists()
        {
            _bibleCombo.Items.Clear();
            _bibleCombo.DisplayMember = nameof(Bible.Name);
            foreach (var bible in _bibles)
            {
                _bibleCombo.Items.Add(bible);
            }
            if (_bibleCombo.Items.Count > 0)
            {
                _bibleCombo.SelectedIndex = 0;
            }
            else
            {
                ClearLists();
                _booksList.Items.Add("DB에 저장된 성경이 없습니다.");
            }
        }

        private void DbBibleCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearPage();
            if (_bibleCombo.SelectedItem is not Bible selected)
            {
                return;
            }

            if (!_booksByBibleId.TryGetValue(selected.Id, out var bibleBooks) || bibleBooks.Count == 0)
            {
                _booksList.Items.Add($"[ {selected.Name} ]");
                return;
            }

            foreach (var book in bibleBooks.OrderBy(b => (int)b.Key))
            {
                int chapterCount = _chaptersByBookId.TryGetValue(book.Id, out var cl) ? cl.Count : book.ChapterCount;
                var item = new ListViewItem(book.Name)
                {
                    Tag = book
                };
                item.SubItems.Add($"{chapterCount}장");
                _booksList.Items.Add(item);
            }
        }

        private void DbBooksList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _chaptersList.Items.Clear();
            _versesList.Items.Clear();
            ClearVerseEditor();

            var firstItem = FirstSelected(_booksList);
            if (firstItem is null || firstItem.Tag is not Book book)
            {
                return;
            }

            _currentBook = book;

            if (!_chaptersByBookId.TryGetValue(book.Id, out var chapters))
            {
                chapters = new List<Chapter>();
            }

            foreach (var chapter in chapters)
            {
                var item = new ListViewItem($"{chapter.Number}장")
                {
                    Tag = chapter
                };
                _chaptersList.Items.Add(item);
            }

            UpdateSelectedLabel();
        }

        private void DbChaptersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _versesList.Items.Clear();
            ClearVerseEditor();
            _currentBook = null;

            var firstItem = FirstSelected(_chaptersList);
            if (firstItem is null || firstItem.Tag is not Chapter chapter)
            {
                return;
            }

            _verseEditor.Enabled = false;
            _statsLabel.Text = "구절 불러오는 중...";

            Task.Run(() =>
            {
                var verses = _bibleService.StoredVerses(chapter.Id);
                var items = verses
                    .Select(v => new ListViewItem
                    {
                        Text = $"{v.Number}",
                        Tag = v,
                        SubItems = { UiText.TruncateText(v.Text, 90) }
                    }).ToList();

                this.Invoke(() =>
                {
                    var firstChapItem = FirstSelected(_chaptersList);
                    if (firstChapItem?.Tag is not Chapter c || c.Id != chapter.Id)
                    {
                        return;
                    }

                    _versesList.BeginUpdate();
                    _versesList.Items.Clear();
                    foreach (var item in items)
                    {
                        _versesList.Items.Add(item);
                    }
                    _versesList.EndUpdate();

                    UpdateStatsLabel(chapter, verses.Count);
                    _currentVerse = null;
                    UpdateSelectedLabel();
                });
            });
        }

        private void DbVersesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var firstItem = FirstSelected(_versesList);
            if (firstItem is null || firstItem.Tag is not Verse verse)
            {
                return;
            }

            _currentVerse = verse;
            _verseEditor.Text = verse.Text ?? string.Empty;
            _verseEditor.Enabled = true;
            _saveVerseBtn.Enabled = true;
            _dirty = false;
            UpdateSelectedLabel();
        }

        private void DbVerseEditor_TextChanged(object sender, EventArgs e)
        {
            _dirty = true;
            UpdateSelectedLabel();
        }

        private void DbSaveVerseButton_Click(object sender, EventArgs e)
        {
            if (_currentVerse is null)
            {
                return;
            }

            string? text = _verseEditor.Text;
            if (text is null || string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("구절 내용을 입력하세요.", "구절 저장", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _bibleService.UpdateVerseText(_currentVerse, text);
                _currentVerse.Text = text;
                _dirty = false;

                var firstVerseItem = FirstSelected(_versesList);
                if (firstVerseItem is not null)
                {
                    firstVerseItem.SubItems[0].Text = UiText.TruncateText(text, 90);
                }

                UpdateSelectedLabel();
                MessageBox.Show("구절이 DB에 저장되었습니다.", "구절 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구절 저장 실패:\n{ex.Message}", "구절 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                UpdateInfoLabel();
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
                    $"현재 DB를 아래 DB로 복원할까요?\n\n{description}\n\n복원하면 현재 DB 내용이 덮어쓰됩니다.",
                    "DB 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Warning))
            {
                return;
            }

            try
            {
                var restored = DbFileService.RestoreDbFile(dialog.FileName);
                string revInfo = restored is not null ? $"리비전: v{restored.Revision}" : "";
                MessageBox.Show($"DB가 복원되었습니다.\n{revInfo}", "DB 복원 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);

                LoadCatalogAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB 복원 실패:\n{ex.Message}", "DB 복원 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearPage()
        {
            _chaptersList.Items.Clear();
            _versesList.Items.Clear();
            ClearVerseEditor();
            _statsLabel.Text = string.Empty;
            _currentBook = null;
            UpdateSelectedLabel();
        }

        private void ClearLists()
        {
            _booksList.Items.Clear();
            _chaptersList.Items.Clear();
            _versesList.Items.Clear();
        }

        private void ClearVerseEditor()
        {
            _currentVerse = null;
            _dirty = false;
            _verseEditor.Text = string.Empty;
            _verseEditor.Enabled = false;
            _saveVerseBtn.Enabled = false;
        }

        private void UpdateSelectedLabel()
        {
            string path;
            if (_currentVerse is not null)
            {
                path = $"{_currentBook?.Name} ({_currentBook?.Bible?.Name}) - {_currentVerse.Chapter?.Number}장 {_currentVerse.Number}절";
            }
            else if (_currentBook is not null)
            {
                path = $"{_currentBook.Name} ({_currentBook?.Bible?.Name})";
            }
            else
            {
                path = "구절 선택";
            }

            _selectedLabel.Text = _dirty ? $"{path}  (수정됨)" : path;
        }

        private void UpdateStatsLabel(Chapter? chapter, int verseCount)
        {
            _statsLabel.Text = chapter is null || verseCount == 0
                ? string.Empty
                : $"{verseCount}구절";
        }

        private void UpdateInfoLabel()
        {
            string path = DbFileService.ActiveDbPath;
            long size = File.Exists(path) ? new FileInfo(path).Length : 0;
            var meta = DbFileService.ReadActiveMeta();

            string info = $"{Path.GetFileName(path)} ({UiText.HumanizeSize(size)})";
            if (meta is not null)
            {
                info += $" | v{meta.Revision} ({meta.SavedAt:yyyy-MM-dd HH:mm})";
            }
            else
            {
                info += " | (리비전 없음)";
            }

            _infoLabel.Text = "DB: " + info;
        }
    }
}
