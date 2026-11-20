using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Bible2PPT.PPT;
using Bible2PPT.Services;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;
using Bible2PPT.Services.BuildService;
using Bible2PPT.Services.TemplateService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.ApplicationServices;

namespace Bible2PPT
{
    static class Program
    {
        static IServiceProvider ServiceProvider { get; set; }

        [STAThread]
        static void Main(string[] args)
        {
            Application.SetDefaultFont(new System.Drawing.Font("Gulim", 9));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ConfigureServices();
            new Startup().Run(args);
        }

        static void ConfigureServices()
        {
            var services = new ServiceCollection();

            // ===== DB는 사용자 폴더에 영구 저장 (2026-11) =====
            // 앱 폴더(빌드/설치 위치)가 바뀌어도 사용자의 구절 DB는 유지된다.
            string exeDbDir = Path.GetDirectoryName(Application.ExecutablePath)
                               + Path.DirectorySeparatorChar;
            string userDataDir = AppConfig.UserDataPath;
            Directory.CreateDirectory(userDataDir);

            // 1회 이관: exe 폴더에 있는 DB(예: 이전 버전/직접 실행 시 생성분)가
            //     사용자 폴더에 없으면 복사한다. 존재하면 사용자 폴더 쪽을 우선.
            foreach (string dbFile in new[] { "bible-v3.db", "bindex-v3.db", "build-v3.db" })
            {
                foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
                {
                    string src = Path.Combine(exeDbDir, dbFile + suffix);
                    string dst = Path.Combine(userDataDir, dbFile + suffix);
                    if (File.Exists(src) && !File.Exists(dst))
                    {
                        File.Copy(src, dst, overwrite: false);
                    }
                }
            }

            services.AddTransient<SplashForm>();
            services.AddTransient<MainForm>();
            services.AddBibleIndexService(options =>
                options.UseSqlite($"Data Source={Path.Combine(userDataDir, "bindex-v3.db")}"));
            services.AddBibleService(
                dbContextOptionsAction: options =>
                    options.UseSqlite($"Data Source={Path.Combine(userDataDir, "bible-v3.db")}"));
            services.AddBuildService(
                dbContextOptionsAction: options =>
                    options.UseSqlite($"Data Source={Path.Combine(userDataDir, "build-v3.db")}"),
                interopInitializeErrorAction: ex =>
                {
                    MessageBox.Show(
                        $"마이크로소프트 파워포인트가 설치되어 있나요?\n\n자세한 오류: {ex}",
                        @"프로그램 초기화 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // TODO: GitHub로 오류 포스팅하도록 안내하기 (전역 예외 처리기 사용)
                    Environment.Exit(0);
                });
            services.AddTemplateService();

            ServiceProvider = services.BuildServiceProvider();

            ServiceProvider.UseBibleIndexService();
            ServiceProvider.UseBibleService();
            ServiceProvider.UseBuildService();
        }

        class Startup : WindowsFormsApplicationBase
        {
            protected override void OnCreateSplashScreen()
            {
                SplashScreen = ServiceProvider.GetService<SplashForm>();
            }

            protected override void OnCreateMainForm()
            {
                MainForm = ServiceProvider.GetRequiredService<MainForm>();
            }
        }
    }
}
