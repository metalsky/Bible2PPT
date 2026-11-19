using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bible2PPT.Bibles;
using Bible2PPT.Services.BibleService;

namespace Bible2PPT
{
    /// <summary>
    /// 선택된 성경 DB(책/장/구절)의 내용을 보고 수정·편집하는 창.
    /// </summary>
    public class DbEditForm : Form
    {
        private readonly Bible _bible;
        private readonly BibleService _bibleService;

        private ComboBox _bookCombo = null!;
        private ComboBox _chapterCombo = null!;
        private ListBox _verseList = null!;
        private TextBox _verseTextBox = null!;
        private Label _statusLabel = null!;
        private Button _saveButton = null!;

        private Verse _currentVerse;
        private List<Verse> _currentVerses = new List<Verse>();
        private bool _dirty;

        public DbEditForm(Bible bible, BibleService bibleService)
        {
            _bible = bible;
            _bibleService = bibleService;
            Text = $"DB 편집 - {bible.Name}";
            ClientSize = new Size(780, 580);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimumSize = new Size(640, 480);
            BuildUi();
            DarkTheme.Apply(this);
            LoadBooksAsync();
        }

        private void BuildUi()
        {
            _statusLabel = new Label { Margin = new Padding(8, 2, 0, 0) };
            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 3,
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                Padding = new Padding(6),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 상단: 책, 장 선택
            var topFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            _bookCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 10, 0),
            };
            _chapterCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false,
                Margin = new Padding(0),
                Width = 120,
            };
            topFlow.Controls.Add(new Label { Text = "책: ", Margin = new Padding(0, 0, 4, 0) });
            topFlow.Controls.Add(_bookCombo);
            topFlow.Controls.Add(new Label { Text = "장: ", Margin = new Padding(0, 0, 4, 0) });
            topFlow.Controls.Add(_chapterCombo);
            _bookCombo.SelectedIndexChanged += BookCombo_SelectedIndexChanged;
            _chapterCombo.SelectedIndexChanged += ChapterCombo_SelectedIndexChanged;
            root.Controls.Add(topFlow, 0, 0);

            // 중앙: 구절 목록 + 본문 편집
            var content = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _verseList = new ListBox
            {
                Margin = new Padding(0),
                Enabled = false,
                Font = new Font("Microsoft YaHei", 10),
            };
            _verseTextBox = new TextBox
            {
                Margin = new Padding(0),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsReturn = true,
                Enabled = false,
                Font = new Font("Microsoft YaHei", 10),
                WordWrap = false,
            };
            content.Controls.Add(_verseList, 0, 0);
            content.Controls.Add(_verseTextBox, 1, 0);
            root.Controls.Add(content, 0, 1);

            // 하단: 저장 + 상태
            var bottomFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            _saveButton = new Button
            {
                Text = "저장",
                Margin = new Padding(0),
                Enabled = false,
            };
            bottomFlow.Controls.Add(_saveButton);
            bottomFlow.Controls.Add(_statusLabel);
            root.Controls.Add(bottomFlow, 0, 2);

            Controls.Add(root);

            _verseList.SelectedIndexChanged += VerseList_SelectedIndexChanged;
            _verseTextBox.TextChanged += VerseTextBox_TextChanged;
            _saveButton.Click += SaveButton_Click;
        }

        private async void LoadBooksAsync()
        {
            _bookCombo.Enabled = false;
            _bookCombo.Items.Clear();
            try
            {
                var books = await _bibleService.GetBooksAsync(_bible);
                _bookCombo.DataSource = books;
                _bookCombo.DisplayMember = nameof(Book.Name);
                if (books.Any())
                {
                    _bookCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서북 오류:\n{ex.Message}");
            }
            finally
            {
                _bookCombo.Enabled = true;
            }
        }

        private async void BookCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_bookCombo.SelectedItem is not Book book)
            {
                return;
            }
            _chapterCombo.Enabled = false;
            _chapterCombo.Items.Clear();
            try
            {
                var chapters = await _bibleService.GetChaptersAsync(book);
                _chapterCombo.DataSource = chapters;
                _chapterCombo.DisplayMember = nameof(Chapter.Number);
                if (chapters.Any())
                {
                    _chapterCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"장 오류:\n{ex.Message}");
            }
            finally
            {
                _chapterCombo.Enabled = true;
            }
        }

        private async void ChapterCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_chapterCombo.SelectedItem is not Chapter chapter)
            {
                return;
            }
            _verseList.Enabled = false;
            _verseTextBox.Enabled = false;
            _saveButton.Enabled = false;
            try
            {
                var verses = await _bibleService.GetVersesAsync(chapter);
                _verseList.Items.Clear();
                _currentVerses = verses.ToList();
                foreach (var v in _currentVerses)
                {
                    _verseList.Items.Add($"{v.Number}. {Truncate(v.Text, 60)}");
                }
                _verseList.Enabled = true;
                if (_verseList.Items.Count > 0)
                {
                    _verseList.SelectedIndex = 0;
                }
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구절 오류:\n{ex.Message}");
            }
        }

        private void VerseList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_verseList.SelectedIndex < 0 || _currentVerses == null || _verseList.SelectedIndex >= _currentVerses.Count)
            {
                return;
            }
            _currentVerse = _currentVerses[_verseList.SelectedIndex];
            _verseTextBox.Text = _currentVerse.Text ?? string.Empty;
            _verseTextBox.Enabled = true;
            _saveButton.Enabled = true;
            _dirty = false;
            UpdateStatus();
        }

        private void VerseTextBox_TextChanged(object sender, EventArgs e)
        {
            _dirty = true;
            UpdateStatus();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (_currentVerse is null)
            {
                return;
            }
            _bibleService.UpdateVerseText(_currentVerse, _verseTextBox.Text);
            _currentVerse.Text = _verseTextBox.Text;
            _dirty = false;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            string status;
            if (_currentVerse is null)
            {
                status = "";
            }
            else
            {
                status = _dirty ? $"(수정됨) {_verseList.Items.Count}구절" : $"{_verseList.Items.Count}구절";
            }
            _statusLabel.Text = status;
        }

        private static string Truncate(string? s, int max)
        {
            s = s ?? string.Empty;
            if (s.Length <= max)
            {
                return s;
            }
            return s.Substring(0, max) + "…";
        }
    }
}
