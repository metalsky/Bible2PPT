﻿using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Bible2PPT
{
    class AppConfig : BinaryConfig
    {
        public const int ConfigSize = 1 + 4 + 4;
        public static string ConfigPath { get; } = Application.ExecutablePath + ".cfg";
        public static string TemplatePath { get; } = Application.ExecutablePath + ".pptx";
        public static string ContactUrl { get; } = "https://github.com/sunghwan2789/Bible2PPT";

        public static AppConfig Context { get; } = new AppConfig();
        
        /// <summary>
        /// Offset: 0,
        /// Mask: 0b0000_0001,
        /// </summary>
        public TemplateTextOptions ShowLongTitle { get; set; } = TemplateTextOptions.Always;
        
        /// <summary>
        /// Offset: 0,
        /// Mask: 0b0000_0010,
        /// </summary>
        public TemplateTextOptions ShowShortTitle { get; set; } = TemplateTextOptions.Always;

        /// <summary>
        /// Offset: 0,
        /// Mask: 0b0000_0100,
        /// </summary>
        public TemplateTextOptions ShowChapterNumber { get; set; } = TemplateTextOptions.Always;

        /// <summary>
        /// Offset: 0,
        /// Mask: 0b0001_0000,
        /// </summary>
        public bool SeperateByChapter { get; set; } = false;

        /// <summary>
        /// Offset: 1,
        /// Length: 4,
        /// </summary>
        public int BibleSourceSeq { get; set; } = 0;

        /// <summary>
        /// Offset: 5,
        /// Length: 4,
        /// </summary>
        public int BibleVersionSeq { get; set; } = 0;

        public AppConfig() : base(ConfigPath, ConfigSize) {}

        protected override byte[] Serialize()
        {
            var b = new byte[ConfigSize];
            b[0] = (byte) (int) ShowLongTitle;
            b[0] |= (byte) ((int) ShowShortTitle << 1);
            b[0] |= (byte) ((int) ShowChapterNumber << 2);
            b[0] |= (byte) (SeperateByChapter ? 16 : 0);
            BitConverter.GetBytes(BibleSourceSeq).CopyTo(b, 1);
            BitConverter.GetBytes(BibleVersionSeq).CopyTo(b, 5);
            return b;
        }

        protected override void Deserialize(byte[] s)
        {
            ShowLongTitle = (TemplateTextOptions) (s[0] & 1);
            ShowShortTitle = (TemplateTextOptions) ((s[0] & 2) >> 1);
            ShowChapterNumber = (TemplateTextOptions) ((s[0] & 4) >> 2);
            SeperateByChapter = (s[0] & 16) == 16;
            BibleSourceSeq = BitConverter.ToInt32(s, 1);
            BibleVersionSeq = BitConverter.ToInt32(s, 5);
        }
    }
}
