using System;
using System.IO;

namespace Bible2PPT
{
    /// <summary>
    /// UI 텍스트 포맷팅에 사용하는 정적 도운 함수.
    /// (기존 MainForm / DbEditorForm에 중복으로 정의되어 있어 여기로 묶었다)
    /// </summary>
    internal static class UiText
    {
        /// <summary>텍스트를 지정된 최대 길이로 잘라서 표시한다.</summary>
        public static string TruncateText(string? text, int max)
        {
            text ??= string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "\u2026";
        }

        /// <summary>바이트 크기를 인간가독 크기로 포맷한다 (B/KB/MB, 1024 기준).</summary>
        public static string HumanizeSize(long bytes)
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

        /// <summary>
        /// PPT 생성 대상 경로(파일 또는 장별 출력 폴더)를 파일명·크기·파일 수로 요약한다.
        /// </summary>
        public static string DescribePptOutput(string? path)
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
    }
}
