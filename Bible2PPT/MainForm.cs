using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Bible2PPT.Controls;
using Bible2PPT.PPT;
using Bible2PPT.Services.BibleService;
using Bible2PPT.Services.TemplateService;

namespace Bible2PPT
{
    internal partial class MainForm : AssemblyIconForm
    {
        private readonly Builder _builder;
        private readonly BibleService _bibleService;
        private readonly TemplateService _templateService;

        private readonly Dictionary<int, CancellationTokenSource> workCts = new Dictionary<int, CancellationTokenSource>();

        public MainForm(Builder builder, BibleService bibleService, TemplateService templateService)
        {
            _builder = builder;
            _bibleService = bibleService;
            _templateService = templateService;

            InitializeComponent();
            InitializeBuildComponent();
            InitializeHistoryComponent();
            InitializeSettingsComponent();

            // 저장된 테마(다크/라이트) 적용 — 시작 시 항상 다크였던 버그 수정
            ThemeManager.Apply(this);

            // TODO: 마지막 페이지 기억하기
            mainMultiPanel.SelectedPage = buildMultiPanelPage;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            AppConfig.Context.Save();
        }

        private Button[] Navs => new[]
        {
            buildNavButton,
            historyNavButton,
            settingsNavButton,
            dbNavButton,
        };

        private void MainMultiPanel_SelectedPanelChanged(object sender, EventArgs e)
        {
            // 제목 표시줄에 페이지 제목 추가
            Text = $"{mainMultiPanel.SelectedPage.Text} - 성경2PPT (by Metalsky)";
            var targetButton = mainMultiPanel.SelectedPage.Name switch
            {
                nameof(buildMultiPanelPage) => buildNavButton,
                nameof(historyMultiPanelPage) => historyNavButton,
                nameof(settingsMultiPanelPage) => settingsNavButton,
                _ => throw new NotImplementedException(mainMultiPanel.SelectedPage.Name),
            };
            // 연결된 Nav만 비활성화
            foreach (var nav in Navs)
            {
                nav.Enabled = true;
            }
            targetButton.Enabled = false;
        }

        private void Nav_Click(object sender, EventArgs e)
        {
            var name = (sender as Control)?.Name;

            // DB 메뉴는 별도 리사이즈 가능한 편집 창(1024x786+)으로 표시한다.
            if (name == nameof(dbNavButton))
            {
                OpenDbEditor();
                return;
            }

            var targetPage = name switch
            {
                nameof(buildNavButton) => buildMultiPanelPage,
                nameof(historyNavButton) => historyMultiPanelPage,
                nameof(settingsNavButton) => settingsMultiPanelPage,
                _ => throw new NotImplementedException(name),
            };
            // 연결된 페이지 활성화
            mainMultiPanel.SelectedPage = targetPage;
        }

        /// <summary>DB 편집 창(리사이즈 가능, 테마 적용)을 나타낸다.</summary>
        private void OpenDbEditor()
        {
            using var form = new DbEditorForm(_bibleService);
            form.ShowDialog();
        }

    }
}
