using System.Windows.Forms;

namespace Bible2PPT;

/// <summary>
/// 프로그램 테마 종류.
/// 0 = 다크, 1 = 라이트. 설정 파일에 1바이트로 저장된다.
/// </summary>
public enum AppTheme
{
    Dark = 0,
    Light = 1
}

/// <summary>
/// 저장된 테마에 따라 메인 창 등 컨트롤 트리 전체에 테마를 적용한다.
/// </summary>
public static class ThemeManager
{
    public static AppTheme Current => AppConfig.Context.Theme;

    public static void Apply(Control root)
    {
        if (root is null)
        {
            return;
        }

        if (Current == AppTheme.Dark)
        {
            DarkTheme.Apply(root);
        }
        else
        {
            LightTheme.Apply(root);
        }
    }
}
