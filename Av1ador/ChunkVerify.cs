using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Av1ador
{
    internal static class ChunkVerify
    {
        public static bool Enabled = true;
        public static bool Diagnostic = false;

        // the ffprobe banner rounds NTSC rates (24000/1001 etc.), restore the exact value
        public static double ExactFps(double fps)
        {
            if (fps > 23.97 && fps < 23.99)
                return 24000.0 / 1001.0;
            if (fps > 29.96 && fps < 29.98)
                return 30000.0 / 1001.0;
            if (fps > 59.93 && fps < 59.95)
                return 60000.0 / 1001.0;
            return fps;
        }

        // true when the filter chain can change the number of frames in a chunk
        public static bool Gated = false;

        public static bool ChangesFrameCount(string Param, string Fps_filter)
        {
            if (!string.IsNullOrEmpty(Fps_filter))
                return true;
            if (string.IsNullOrEmpty(Param))
                return true;
            return Regex.IsMatch(Param, @"\b(fps|framestep|select|decimate|mpdecimate|yadif|bwdif|w3fdif|minterpolate|minter|tinterlace|setpts)\s*=");
        }

        public static int CountFrames(string chunkFile)
        {
            string[] outp = Probe(chunkFile, " -v error -count_packets -select_streams v:0 -show_entries stream=nb_read_packets -of default=nw=1");
            for (int i = 0; i < outp.Length; i++)
            {
                int eq = outp[i].IndexOf('=');
                if (eq < 0)
                    continue;
                if (outp[i].Substring(0, eq).Trim() != "nb_read_packets")
                    continue;
                if (int.TryParse(outp[i].Substring(eq + 1).Trim(), out int n))
                    return n;
            }
            return -1;
        }

        public static bool Judge(string chunkFile, int expected, int actual)
        {
            if (Diagnostic || Gated)
                return true;
            // frame accuracy is the only judge: no file size, and a missing/failed
            // count must not pass either (tiny chunks are perfectly valid)
            return expected > 0 && actual >= 0 && actual == expected;
        }

        // a persistent record for unattended runs, next to the executable
        public static void GlobalLog(string line)
        {
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "failures.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + "\r\n");
            }
            catch { }
        }

        // append a timestamped trace line next to the chunk's temp files
        public static void Log(string chunkFile, string message)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(chunkFile);
                if (string.IsNullOrEmpty(dir))
                    dir = ".";
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "frames_check.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + System.IO.Path.GetFileName(chunkFile) + "  " + message + "\r\n");
            }
            catch { }
        }

        private static string[] Probe(string file, string args)
        {
            try
            {
                Process p = new Process();
                // id 2 is ffprobe, the default id 3 is ffmpeg and rejects -show_entries
                Func.Setinicial(p, 2, args + " \"" + file + "\"");
                p.Start();
                string s = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                return s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
