#nullable enable
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

namespace Bible2PPT
{
    /// <summary>
    /// DB 파일에 저장된 리비전(버전) 정보.
    /// </summary>
    public class DbMetaInfo
    {
        public int Revision { get; set; }
        public DateTime SavedAt { get; set; }
        public string FileName { get; set; } = string.Empty;

        public override string ToString() =>
            $"v{Revision} ({SavedAt:yyyy-MM-dd HH:mm}) {FileName}";
    }

    /// <summary>
    /// DB 파일 관련 작업(저장(다른이름), 복원, 유효성 확인, 리비전 관리).
    /// </summary>
    public static class DbFileService
    {
        // SQLite 파일 마기 헤더: "SQLite format 3\x00"
        private static readonly byte[] SqliteHeader =
        {
            0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66,
            0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00
        };

        /// <summary>
        /// 현재 앱이 사용하는 DB 파일 경로.
        /// 사용자 폴더(%APPDATA%\Bible2PPT)에 영구 저장하는 구조:
        /// app.config의 Data Source가 절대경로이면 그 경로를 사용하고,
        /// 상대 파일명이면 사용자 폴더를 기준 삼는다.
        /// </summary>
        public static string ActiveDbPath
        {
            get
            {
                string dataName = "bible-v3.db";
                string? cs = ConfigurationManager.ConnectionStrings["BibleContext"]?.ConnectionString;
                if (!string.IsNullOrEmpty(cs))
                {
                    var m = Regex.Match(cs, @"Data Source\s*=\s*([^;]+)");
                    if (m.Success)
                    {
                        string candidate = m.Groups[1].Value.Trim();
                        // 절대경로이고 실제 파일이 있으면 그대로 사용(호환용).
                        if (Path.IsPathRooted(candidate) && File.Exists(candidate))
                        {
                            return Path.GetFullPath(candidate);
                        }
                        dataName = Path.GetFileName(candidate);
                    }
                }

                // 사용자 폴더(%APPDATA%\Bible2PPT)로 영구 저장.
                // 앱 폴더가 다시 빌드·재설치되어도 DB는 유지된다.
                return Path.Combine(AppConfig.UserDataPath, dataName);
            }
        }

        public static string ActiveDbFileName => Path.GetFileName(ActiveDbPath);

        /// <summary>
        /// 지정된 파일이 유효한 SQLite DB(성경 DB 구조 포함)인지 확인한다.
        /// 잘못된 DB가 읽히는 것을 막기 위해:
        /// 1) SQLite 마기 헤더 2) 무결성 확인(integrity_check)
        /// 3) Bibles 테이블 존재 여부까지 검증한다.
        /// </summary>
        public static (bool isValid, string message) ValidateDbFile(string path)
        {
            if (!File.Exists(path))
            {
                return (false, "DB 파일이 없습니다.");
            }

            if (new FileInfo(path).Length == 0)
            {
                return (false, "DB 파일 크기가 0바이트입니다.");
            }

            // SQLite 마기 헤더 확인
            // SQLite가 파일 핸들(저장/읽기)을 잡고 있을 수 있으므로 File.OpenRead(FileShare.None)와 충돌하지
            // 않도록 읽기 공유를 허용해 열린다.
            byte[] header = new byte[16];
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Read(header, 0, 16) != 16 || !header.SequenceEqual(SqliteHeader))
                {
                    return (false, "SQLite 파일이 아닙니다.");
                }
            }

            // 무결성 확인 및 Bibles 테이블 확인
            // SQLite 연결 문자열: "Data Source=<절대경로>" 형식 (File: URI / mode=로/ cache=shared는
            // Microsoft.Data.Sqlite에서 지원하지 않으므로 사용하지 않는다.)
            using (SqliteConnection conn = new($"Data Source={path}"))
            {
                conn.Open();

                using (SqliteCommand cmd = new("PRAGMA integrity_check;", conn))
                {
                    object? scalar = cmd.ExecuteScalar();
                    string result = (scalar?.ToString() ?? string.Empty).Trim();
                    if (!result.StartsWith("ok", StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"DB 무결성이 손상되었습니다.\n{result}");
                    }
                }

                using (SqliteCommand cmd = new("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Bibles';", conn))
                {
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count == 0)
                    {
                        return (false, "성경 DB가 아닙니다 (Bibles 테이블이 없습니다).");
                    }
                }

                return (true, "정상");
            }
        }

        public static (bool isValid, string message) ValidateActiveDb() => ValidateDbFile(ActiveDbPath);

        /// <summary>
        /// DB에 저장된 마지막 리비전 정보를 반환한다.
        /// </summary>
        public static DbMetaInfo? ReadDbMeta(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using var conn = new SqliteConnection($"Data Source={path}");
                conn.Open();

                using var cmd = new SqliteCommand(
                    "SELECT Revision, SavedAt, FileName FROM DbMeta ORDER BY Revision DESC LIMIT 1;", conn);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new DbMetaInfo
                    {
                        Revision = Convert.ToInt32(reader.GetString(0)),
                        SavedAt = DateTime.Parse(reader.GetString(1)),
                        FileName = reader.GetString(2)
                    };
                }
            }
            catch
            {
                // meta 테이블이 없거나 읽을 수 없으면 null
            }

            return null;
        }

        public static DbMetaInfo? ReadActiveMeta() => ReadDbMeta(ActiveDbPath);

        /// <summary>
        /// 현재 DB를 다른 이름(리비전)으로 복사하여 저장한다.
        /// 저장할 때 시간과 리비전 번호를 파일명에 삽입한다.
        /// </summary>
        public static DbMetaInfo SaveDbAs(string userBaseName)
        {
            string activePath = ActiveDbPath;
            DateTime now = DateTime.Now;

            // 1) 활성 DB에 저장 meta를 기록
            int revision = 0;
            using (var conn = new SqliteConnection($"Data Source={activePath}"))
            {
                conn.Open();

                using (var cmd = new SqliteCommand(
                    "CREATE TABLE IF NOT EXISTS DbMeta (Revision INTEGER PRIMARY KEY, SavedAt TEXT NOT NULL, FileName TEXT NOT NULL);", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SqliteCommand("SELECT COALESCE(MAX(Revision), 0) FROM DbMeta;", conn))
                {
                    revision = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                }

                using (var cmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO DbMeta (Revision, SavedAt, FileName) VALUES (@r, @t, @f);", conn))
                {
                    cmd.Parameters.AddWithValue("@r", revision);
                    cmd.Parameters.AddWithValue("@t", now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@f", Path.GetFileName(BuildTargetPath(userBaseName, now, revision)));
                    cmd.ExecuteNonQuery();
                }
            }

            // 2) meta가 기록된 활성 DB 파일을 새 이름으로 복사
            string targetPath = BuildTargetPath(userBaseName, now, revision);
            File.Copy(activePath, targetPath, true);

            return new DbMetaInfo
            {
                Revision = revision,
                SavedAt = now,
                FileName = Path.GetFileName(targetPath)
            };
        }

        private static string BuildTargetPath(string userBaseName, DateTime now, int revision)
        {
            string safe = Path.GetFileName(userBaseName.Trim());
            if (string.IsNullOrEmpty(safe))
            {
                safe = Path.GetFileNameWithoutExtension(ActiveDbPath);
            }
            else
            {
                // 확장자를 제거하여 통일
                safe = Path.GetFileNameWithoutExtension(safe);
            }

            string dir = Path.GetDirectoryName(ActiveDbPath)!;
            return Path.Combine(dir, $"{safe}_{now:yyyyMMdd_HHmmss}_r{revision:D4}.db");
        }

        /// <summary>
        /// 선택한 DB 파일을 활성 DB 위치로 복원(복사)한다.
        /// </summary>
        public static DbMetaInfo? RestoreDbFile(string sourcePath)
        {
            File.Copy(sourcePath, ActiveDbPath, true);
            return ReadActiveMeta();
        }
    }
}
