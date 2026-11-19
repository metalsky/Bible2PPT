using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FontAwesome.Sharp;

namespace Bible2PPT
{
    partial class MainForm
    {
        private FontAwesome.Sharp.IconButton _darkThemeButton;
        private FontAwesome.Sharp.IconButton _lightThemeButton;
        private TextBox _fileInfoBox;

        private void InitializeSettingsComponent()
        {
            // ===== 테마 선택 (아이콘) =====
            var themeLabel = new Label
            {
                Text = "테마:",
                AutoSize = true,
                Location = new Point(25, 128)
            };
            settingsMultiPanelPage.Controls.Add(themeLabel);

            _darkThemeButton = new IconButton
            {
                IconChar = IconChar.Moon,
                IconColor = Color.White,
                IconSize = 22,
                Text = string.Empty,
                AutoSize = false,
                Size = new Size(56, 38),
                Location = new Point(112, 123),
                FlatStyle = FlatStyle.Flat
            };
            _darkThemeButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _darkThemeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
            _darkThemeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 40, 40);
            _darkThemeButton.FlatAppearance.CheckedBackColor = Color.FromArgb(60, 60, 60);
            _darkThemeButton.Click += DarkThemeButton_Click;
            settingsMultiPanelPage.Controls.Add(_darkThemeButton);

            _lightThemeButton = new IconButton
            {
                IconChar = IconChar.Sun,
                IconColor = Color.Gold,
                IconSize = 22,
                Text = string.Empty,
                AutoSize = false,
                Size = new Size(56, 38),
                Location = new Point(176, 123),
                FlatStyle = FlatStyle.Flat
            };
            _lightThemeButton.FlatAppearance.BorderColor = Color.FromArgb(190, 190, 190);
            _lightThemeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
            _lightThemeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 235, 235);
            _lightThemeButton.FlatAppearance.CheckedBackColor = Color.FromArgb(220, 220, 220);
            _lightThemeButton.Click += LightThemeButton_Click;
            settingsMultiPanelPage.Controls.Add(_lightThemeButton);

            // ===== 생성 파일 & DB 정보 =====
            var fileInfoLabel = new Label
            {
                Text = "생성 파일 & DB:",
                AutoSize = true,
                Location = new Point(25, 178)
            };
            settingsMultiPanelPage.Controls.Add(fileInfoLabel);

            _fileInfoBox = new TextBox
            {
                Location = new Point(25, 200),
                Size = new Size(430, 105),
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei", 9F)
            };
            settingsMultiPanelPage.Controls.Add(_fileInfoBox);

            UpdateFileInfo();
            ThemeManager.Apply(this);
            HighlightActiveThemeButton();
        }

        private void DarkThemeButton_Click(object sender, EventArgs e)
        {
            SetTheme(AppTheme.Dark);
        }

        private void LightThemeButton_Click(object sender, EventArgs e)
        {
            SetTheme(AppTheme.Light);
        }

        private void SetTheme(AppTheme theme)
        {
            AppConfig.Context.Theme = theme;
            AppConfig.Context.Save();
            ThemeManager.Apply(this);
            HighlightActiveThemeButton();
        }

        private void HighlightActiveThemeButton()
        {
            bool darkActive = AppConfig.Context.Theme == AppTheme.Dark;
            _darkThemeButton.FlatAppearance.BorderColor = darkActive ? Color.FromArgb(0, 120, 212) : Color.FromArgb(80, 80, 80);
            _lightThemeButton.FlatAppearance.BorderColor = darkActive ? Color.FromArgb(190, 190, 190) : Color.FromArgb(0, 120, 212);
        }

        private void UpdateFileInfo()
        {
            var lines = new List<string>();

            // ===== 생성된 PPT 파일(최근 2개) =====
            int shownGenerated = 0;
            foreach (var job in _builder.GetJobs().OrderByDescending(x => x.CreatedAt).Take(2))
            {
                string desc = DescribePptOutput(job.OutputDestination);
                if (string.IsNullOrWhiteSpace(desc))
                {
                    continue;
                }
                lines.Add($"생성 PPT: {desc}");
                shownGenerated++;
            }
            if (shownGenerated == 0)
            {
                lines.Add("생성 PPT: (없음)");
            }

            // ===== DB =====
            var dbPath = DbFileService.ActiveDbPath;
            if (File.Exists(dbPath))
            {
                long dbSize = new FileInfo(dbPath).Length;
                var meta = DbFileService.ReadActiveMeta();
                lines.Add($"DB: {Path.GetFileName(dbPath)} ({HumanizeSize(dbSize)})");
                if (meta is not null)
                {
                    lines.Add($"  리비전: v{meta.Revision}  ({meta.SavedAt:yyyy-MM-dd HH:mm})");
                }
            }
            else
            {
                lines.Add("DB: (파일 없음)");
            }

            // ===== 템플릿 =====
            var templatePath = AppConfig.TemplatePath;
            if (File.Exists(templatePath))
            {
                long tSize = new FileInfo(templatePath).Length;
                lines.Add($"템플릿: {Path.GetFileName(templatePath)} ({HumanizeSize(tSize)})");
            }
            else
            {
                lines.Add("템플릿: (파일 없음)");
            }

            _fileInfoBox.Text = string.Join("\n", lines);
        }

        /// <summary>
        /// PPT 생성 대상 경로(파일 또는 장별 출력 폴더)를 파일명·크기·파일 수로 요약한다.
        /// </summary>
        private static string DescribePptOutput(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (File.Exists(path))
            {
                return $"{Path.GetFileName(path)} ({HumanizeSize(new FileInfo(path).Length)})";
            }

            if (Directory.Exists(path))
            {
                int count = 0;
                long total = 0;
                try
                {
                    foreach (var file in Directory.GetFiles(path, "*.pptx", SearchOption.AllDirectories))
                    {
                        count++;
                        total += new FileInfo(file).Length;
                        if (count >= 1000)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    count = -1;
                }

                return count > 0
                    ? $"{Path.GetFileName(path)} ({HumanizeSize(total)}, {count}개)"
                    : string.Empty;
            }

            return string.Empty;
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppConfig.ContactUrl,
                UseShellExecute = true,
            });
        }

        private async void CleanCacheButton_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show("진행 중인 작업을 취소하고 프로그램을 다시 시작할까요?", Text, MessageBoxButtons.YesNo))
            {
                await _bibleService.ClearCachesAsync();
                Application.Restart();
            }
        }
    }
}
