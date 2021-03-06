﻿using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Bible2PPT
{
    static class Resources
    {
        private static readonly Assembly Assembly = Assembly.GetCallingAssembly();
        private static readonly string Prefix = $"{Assembly.GetName().Name}.Resources";

        public static byte[] Get(string name)
        {
            using var stream = GetStream(name);
            if (stream == null)
            {
                return null;
            }

            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        public static async Task<byte[]> GetAsync(string name)
        {
            using var stream = GetStream(name);
            if (stream == null)
            {
                return null;
            }

            var buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            return buffer;
        }

        public static Stream GetStream(string name) =>
            Assembly.GetManifestResourceStream($"{Prefix}.{name.Replace('-', '_')}");
    }
}
