﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Av1ador
{
    internal class Encoder : ICloneable
    {
        public const string resdir = "resource\\\\";
        private readonly string gpu_name;
        private int ba;
        private readonly string[] v = new string[] { "AV1 (aom)", "AV1 (svt)", "AV1 (rav1e)", "AV1 (nvenc)", "VP9 (vpx)", "HEVC (x265)", "HEVC (nvenc)", "H264 (x264)", "H264 (nvenc)", "MPEG4 (xvid)", "VVC (vvenc)" };
        private readonly string[] j = new string[] { "mkv", "ivf", "vvc" };
        private readonly string[] a = new string[] { "aac", "opus", "vorbis", "mp3" };
        private readonly string[] c = new string[] { "2 (stereo)", "6 (surround 5.1)", "8 (surround 7.1)", "1 (mono)" };
        private string speed_str;
        public static bool Libfdk { get; set; }
        public static bool Libplacebo { get; set; }
        public static string OCL_Device { get; set; }
        public static string Vkn_Device { get; set; }
        public string[] Resos { get; set; }
        public int Max_crf { get; set; }
        public int Crf { get; set; }
        public int Cores { get; }
        public int Threads { get; set; }
        public string Cv { get; set; }
        public string Ca { get; set; }
        public string Ch { get; set; }
        public int V_kbps { get; set; }
        public int A_kbps { get; set; }
        public int A_min { get; set; }
        public int A_max { get; set; }
        public bool AudioPassthru { get; set; }
        public string Job { get; set; }
        public string A_Job { get; set; }
        public int SubIndex { get; set; }
        public string Speed { get; set; }
        public bool Hdr { get; set; }
        public int Bits { get; set; }
        public string Params { get; set; }
        public string Color { get; set; }
        public int Gs { get; set; }
        public int Gs_level { get; set; }
        public string[] V_codecs { get; set; }
        public string[] A_codecs { get; set; }
        public string[] Channels { get; set; }
        public string[] Bit_depth { get; set; }
        public string[] Presets { get; set; }
        public List<string> Vf { get; set; }
        public List<string> Af { get; set; }
        public double Playtime { get; set; }
        public int Out_w { get; set; }
        public int Out_h { get; set; }
        public int Out_fps { get; set; }
        public double Out_spd { get; set; } = 1;
        public bool Predicted { get; set; }
        public double Rate { get; set; }
        public string Multipass { get; set; }
        public string Vbr_str { get; set; }

        public int Ba
        {
            get
            { return ba; }
            set
            {
                if (int.Parse(Ch) > 0 && Ca == "libfdk_aac")
                {
                    if (value < 8)
                    {
                        if (!Af.Contains("aresample=8000"))
                            Af.Add("aresample=8000");
                        if (!Af.Contains("highpass=f=200"))
                            Af.Add("highpass=f=200");
                        if (!Af.Contains("adynamicsmooth"))
                            Af.Add("adynamicsmooth");
                    }
                    else
                    {
                        if (Af.Contains("aresample=8000"))
                            Af.RemoveAll(s => s.StartsWith("aresample="));
                        if (Af.Contains("highpass=f=200"))
                            Af.RemoveAll(s => s.StartsWith("highpass="));
                        if (Af.Contains("adynamicsmooth"))
                            Af.RemoveAll(s => s.StartsWith("adynamicsmooth"));
                    }
                }
                ba = value;
            }
        }

        public Encoder()
        {
            Resos = new string[] { "4320p", "2160p", "1080p", "900p", "720p", "576p", "540p", "480p", "360p", "240p", "160p" };
            Max_crf = 63;
            Crf = 36;
            Cores = Environment.ProcessorCount > 16 ? 16 : Environment.ProcessorCount;
            Threads = Cores / 2;
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                    gpu_name = obj["Name"].ToString();
            A_max = 320;
            Ch = "2";
            Gs_level = 0;
            Color = "";
            Vf = new List<string>();
            Af = new List<string>();
            Multipass = "";

            string[] exes = Func.Exes();
            OCL_Device = exes[1];
            Vkn_Device = exes[2];
            Libfdk = exes[0].Contains("enable-libfdk-aac");
            Libplacebo = exes[0].Contains("enable-libplacebo") && Vkn_Device != "";
        }
        private string[] CheckNvidia(string[] vtags)
        {
            if (gpu_name.Contains("NVIDIA"))
                return vtags;
            var nonv = new List<string>();
            foreach (var vtag in vtags)
                if (!vtag.Contains("nvenc"))
                    nonv.Add(vtag);
            return nonv.ToArray();

        }
        public void Format(string format)
        {
            switch (format)
            {
                case "mp4":
                    A_codecs = new string[] { a[0], a[1], a[3] };
                    V_codecs = new string[] { v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9] };
                    V_codecs = CheckNvidia(V_codecs);
                    break;
                case "mkv":
                    A_codecs = new string[] { a[0], a[1], a[2], a[3] };
                    V_codecs = new string[] { v[10], v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9] };
                    V_codecs = CheckNvidia(V_codecs);
                    break;
                case "webm":
                    A_codecs = new string[] { a[1], a[2] };
                    V_codecs = new string[] { v[0], v[1], v[2], v[3], v[4] };
                    break;
                case "avi":
                    A_codecs = new string[] { a[3], a[1] };
                    V_codecs = new string[] { v[10], v[9], v[4], v[5], v[6], v[7], v[8] };
                    V_codecs = CheckNvidia(V_codecs);
                    break;
            }
        }
        public void Set_video_codec(string codec)
        {
            Max_crf = 51;
            Gs = 0;
            Rate = 1.0;
            Multipass = "";
            Vbr_str = "";
            if (codec == v[0])
            {
                Cv = "libaom-av1";
                Max_crf = 63;
                Crf = 35;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "2", "3", "*4", "5", "6", "7", "8 (fastest)" };
                speed_str = "-cpu-used ";
                Params = "-tune 1 -enable-restoration 0 -threads !threads! -tiles 2x1 -keyint_min !minkey! -g !maxkey! -aom-params sharpness=4:max-gf-interval=20:gf-max-pyr-height=4:disable-trellis-quant=2:denoise-noise-level=!gs!:enable-dnl-denoising=0:denoise-block-size=16:arnr-maxframes=3:arnr-strength=4:max-reference-frames=4:enable-rect-partitions=0:enable-filter-intra=0:enable-masked-comp=0:enable-qm=1:qm-min=0:qm-max=5 -strict -2";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Gs = 100;
                Rate = 0.82;
                Vbr_str = "-undershoot-pct 60 -overshoot-pct 0 -minsection-pct 60 -maxsection-pct 96";
            }
            else if (codec == v[1])
            {
                Cv = "libsvtav1";
                Max_crf = 63;
                Crf = 18;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "2", "3", "*4", "5", "6", "7", "8", "9", "10", "11", "12 (fastest)" };
                speed_str = "-preset ";
                Params = " -svtav1-params tune=0:keyint=240:enable-qm=1:qm-min=0:qm-max=15:aq-mode=2:enable-dlf=2:enable-overlays=0:enable-restoration=0:enable-tf=0:enable-cdef=0:sharpness=3:enable-variance-boost=1:variance-boost-strength=3:variance-octile=4:qp-scale-compress-strength=3:adaptive-film-grain=1:film-grain=!gs!:noise-norm-strength=1"; //:psy-rd=1.0";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Gs = 50;
                Rate = 0.85;
                Vbr_str = "";
            }
            else if (codec == v[2])
            {
                Cv = "librav1e";
                Max_crf = 255;
                Crf = 120;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "2", "3", "4", "*5", "6", "7", "8", "9", "10 (fastest)" };
                speed_str = ":speed=";
                Params = ":threads=!threads!:tiles=2:min-keyint=!minkey!:keyint=!maxkey!";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Rate = 0.87;
            }
            else if (codec == v[3])
            {
                Cv = "av1_nvenc";
                Crf = 32;
                Bit_depth = new string[] { "8", "10" };
                Job = j[0];
                Presets = new string[] { "*slow", "p7", "p6", "p5", "p4", "p3", "p2", "p1", "fast" };
                speed_str = "-preset:v ";
                Params = "-tile-columns 1 -tile-rows 1";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else if (codec == v[4])
            {
                Cv = "libvpx-vp9";
                Max_crf = 63;
                Crf = 36;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "*2", "3", "4", "5 (fastest)" };
                speed_str = "-speed ";
                Params = "-tile-columns 2 -tile-rows 1 -frame-parallel 0 -row-mt 1 -auto-alt-ref 6 -lag-in-frames 25 -keyint_min !minkey! -g !maxkey! -max-intra-rate 0 -enable-tpl 1 -arnr-maxframes 4 -arnr-strength 2";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Rate = 0.95;
                Vbr_str = "-undershoot-pct 60 -overshoot-pct 0";
                Multipass = "-pass 1 -passlogfile \"!log!\"";
            }
            else if (codec == v[5])
            {
                Cv = "libx265";
                Crf = 28;
                Bit_depth = new string[] { "10", "8" };
                Job = j[0];
                Presets = new string[] { "placebo", "veryslow", "*slower", "slow", "medium", "fast", "faster", "veryfast", "superfast", "ultrafast" };
                speed_str = "-preset ";
                Params = "-x265-params min-keyint=!minkey!:keyint=!maxkey!:early-skip=1:psy-rd=0.5:rdoq-level=2:psy-rdoq=2:selective-sao=2:rskip-edge-threshold=1:aq-mode=4:rect=0:subme=2:limit-tu=1:amp=0:hme=1:hme-search=star,umh,hex:hme-range=16,32,100:strong-intra-smoothing=0";
                Color = ":colorprim=1:transfer=1:colormatrix=1";
                Rate = 0.9;
                Multipass = "-pass 1 -passlogfile \"!log!\"";
            }
            else if (codec == v[6])
            {
                Cv = "hevc_nvenc";
                Crf = 32;
                Bit_depth = new string[] { "8", "10" };
                Job = j[0];
                Presets = new string[] { "*slow", "p7", "p6", "p5", "p4", "p3", "p2", "p1", "fast" };
                speed_str = "-preset:v ";
                Params = "-bf:v 3 -spatial-aq 1 -temporal-aq 1 -aq-strength 7 -b_ref_mode 1";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else if (codec == v[7])
            {
                Cv = "libx264";
                Crf = 27;
                Bit_depth = new string[] { "8", "10" };
                Job = j[0];
                Presets = new string[] { "placebo", "*veryslow", "slower", "slow", "medium", "fast", "faster", "veryfast", "superfast", "ultrafast" };
                speed_str = "-preset ";
                Params = "-x264opts threads=!threads!:min-keyint=!minkey!:keyint=!maxkey!:stitchable=1";
                Color = ":colorprim=bt709:transfer=bt709:colormatrix=bt709";
                Multipass = "-pass 1 -passlogfile \"!log!\"";
            }
            else if (codec == v[8])
            {
                Cv = "h264_nvenc";
                Crf = 32;
                Bit_depth = new string[] { "8" };
                Job = j[0];
                Presets = new string[] { "*slow", "p7", "p6", "p5", "p4", "p3", "p2", "p1", "fast" };
                speed_str = "-preset ";
                Params = "-bf:v 4 -b_adapt 0 -spatial-aq 1 -temporal-aq 1 -aq-strength 7 -coder 1 -b_ref_mode 2";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else if( codec == v[9])
            {
                Cv = "libxvid";
                Max_crf = 31;
                Crf = 16;
                Bit_depth = new string[] { "8" };
                Job = j[0];
                Presets = new string[] { "" };
                speed_str = "";
                Params = "-g !maxkey! -mbd rd -trellis 1 -flags +mv4+aic+cgop -variance_aq 1 -bf 3 -b_qoffset 10";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else if (codec == v[10])
            {
                Cv = "libvvenc";
                Max_crf = 63;
                Crf = 32;
                Bit_depth = new string[] { "10" };
                Job = j[2];
                Presets = new string[] { "slower", "slow", "*medium", "fast", "faster" };
                speed_str = "-preset ";
                Params = "-vvenc-params threads=!threads!:tiles=2x1";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Rate = 0.9;
            }
        }

        public void Set_audio_codec(string codec, int ch)
        {
            if (codec == a[0])
            {
                if (Libfdk)
                    Ca = "libfdk_aac";
                else
                    Ca = "aac";
                Channels = new string[] { c[0], c[1], c[2], c[3] };
                A_kbps = 192;
                A_min = 2;
                A_Job = "m4a";
            }
            else if (codec == a[1])
            {
                Ca = "libopus";
                A_kbps = 192;
                A_min = 5;
                A_Job = "ogg";
                Channels = new string[] { c[0], c[3] };
            }
            else if (codec == a[2])
            {
                Ca = "libvorbis";
                A_kbps = 192;
                A_min = 48;
                A_Job = "ogg";
                Channels = new string[] { c[0], c[1], c[2], c[3] };
            }
            else
            {
                Ca = "libmp3lame";
                A_kbps = 192;
                A_min = 32;
                A_Job = "mp3";
                Channels = new string[] { c[0], c[3] };
            }
            var channels = new List<string>();
            foreach (var str_ch in Channels)
                if (int.Parse(str_ch.Split(' ')[0]) <= ch)
                    channels.Add(str_ch);
            Channels = channels.ToArray();
            Ch = ch.ToString();
        }

        public string[] Calc_kbps(double total, double duracion, int fps, int kbps = 0)
        {
            if (kbps == 0)
                kbps = (int)Math.Floor(total * 8.0 * 1024.0 / duracion);

            kbps -= fps / 10;
            kbps = kbps < 4 ? 4 : kbps;
            int reduccion = 16 - (((kbps * 4) - 4000) / (-253));
            int ba = A_kbps * (int.Parse(Ch) > 2 ? int.Parse(Ch) - 2 : int.Parse(Ch)) * reduccion / 64;
            ba = ba > A_kbps ? (ba - A_kbps) / 2 + A_kbps : ba;
            kbps -= ba;
            if (ba < 216 && int.Parse(Ch) > 2)
                Ch = "2";
            return new string[3] { kbps.ToString(), ba.ToString(), Ch };
        }

        public double Calc_total(int kbps, int ba, double duracion, int fps)
        {
            return Math.Round((double)(kbps + ba + fps / 10) * duracion / (double)8 / (double)1024, 1);
        }

        public string Params_replace(int fps, [Optional] string p)
        {
            int minkey = fps > 1 ? fps : 24;
            int maxkey = fps > 1 ? fps * 10 : 240;
            if (Cv == "libxvid")
                maxkey /= 2;
            if (p == null)
                p = Params;
            p = Func.Param_replace(p, "keyint_min", minkey.ToString());
            p = Func.Param_replace(p, "min-keyint", minkey.ToString());
            p = Func.Param_replace(p, "keyint", maxkey.ToString());
            p = Func.Param_replace(p, "g", maxkey.ToString());
            p = p.Replace("!minkey!", minkey.ToString()).Replace("!maxkey!", maxkey.ToString()).Replace("!threads!", Threads.ToString()).Replace("!gs!", Gs_level.ToString());
            return p;
        }

        private string Bit_Format([Optional] int bits)
        {
            if ((Bits == 10 && bits == 0) || bits == 10)
                return "format=yuv420p10le";
            else
                return "format=yuv420p";
        }

        public string Filter_convert(string f)
        {
            if (f.IndexOf("nlmeans") > -1)
            {
                Regex regex = new Regex(@"_opencl=s=([0-9\.]+):p=([0-9]+):r=([0-9]+)");
                Match match = regex.Match(f);
                if (match.Success)
                    return "nlmeans=s=" + match.Groups[1].Value + ":p=" + match.Groups[2].Value + ":r=" + match.Groups[3].Value;
            }
            return f;
        }

        public void Vf_add(string f, [Optional] string v, [Optional] string a, [Optional] string b, [Optional] string c)
        {
            if (f == "crop")
            {
                if (b == null)
                {
                    string[] dim = Func.Find_w_h(Vf);
                    if (dim.Count() > 0)
                    {
                        v = dim[0];
                        a = dim[1];
                    }
                    Vf.Add("crop=w=" + v + ":h=" + a + ":x=0:y=0");
                }
                else
                {
                    Vf.Remove("crop=w=Detecting:h=.:x=.:y=.");
                    Vf.Remove("crop=w=" + v + ":h=" + a + ":x=" + b + ":y=" + c);
                    Vf.Insert(0, "crop=w=" + v + ":h=" + a + ":x=" + b + ":y=" + c);
                }
            }
            else if (f == "deinterlace")
            {
                Vf.RemoveAll(s => s.StartsWith("nnedi"));
                if (v == "True")
                    Vf.Insert(0, "nnedi='weights=" + resdir + "nnedi3_weights.bin:field=a'");
            }
            else if (f == "Resize to 1080p (zscale spline36)")
                Vf.Add("zscale=w=1920:h=-2:f=spline36");
            else if (f == "Resize to 1080p (libplacebo mitchell)")
                Vf.Add("libplacebo=w=1920:h=-2:force_original_aspect_ratio=decrease:normalize_sar=true:downscaler=mitchell");
            else if (f == "Light denoise")
                Vf.Add("removegrain=1:0:0,noise=c0s=1:c0f=t");
            else if (f == "Strong denoise")
                Vf.Add("nlmeans=1:7:5:3:3");
            else if (f == "Vulkan")
                Vf.Add("\"" + Bit_Format(10) + ",hwupload,libplacebo=percentile=99.6:gamut_mode=relative:tonemapping=hable:range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709:" + Bit_Format() + ",hwdownload," + Bit_Format() + "\"");
        }

        public void Vf_update(string f, [Optional] string v, [Optional] string a, [Optional] bool b)
        {
            if (f == "tonemap")
            {
                if (v == "Yes" || a == "False")
                {
                    Vf.RemoveAll(s => s.Contains("tonemap"));
                    return;
                }
                bool d = false;
                for (int i = 0; i < Vf.Count; i++)
                {
                    if (Vf[i].Contains("tonemap"))
                        d = true;
                    if (Bits == 8 && (Vf[i].Contains("format=p010,hwdownload,format=p010") || Vf[i].Contains(Bit_Format(10) + ",hwdownload," + Bit_Format(10))))
                    {
                        Vf[i] = Vf[i].Replace("format=p010,hwdownload,format=p010", "format=nv12,hwdownload,format=nv12");
                        Vf[i] = Vf[i].Replace(Bit_Format(10) + ",hwdownload," + Bit_Format(10), Bit_Format(8) + ",hwdownload," + Bit_Format(8));
                        break;
                    }
                    else if (Bits > 8 && (Vf[i].Contains("format=nv12,hwdownload,format=nv12") || Vf[i].Contains(Bit_Format(8) + ",hwdownload," + Bit_Format(8))))
                    {
                        Vf[i] = Vf[i].Replace("format=nv12,hwdownload,format=nv12", "format=p010,hwdownload,format=p010");
                        Vf[i] = Vf[i].Replace(Bit_Format(8) + ",hwdownload," + Bit_Format(8), Bit_Format(10) + ",hwdownload," + Bit_Format(10));
                        break;
                    }
                }
                if (!d)
                {
                    if (Libplacebo)
                        Vf_add("Vulkan", "0");
                    else if (b)
                        Vf_add("OpenCL", "");
                }
            }
            else if (f.Contains("setpts="))
                Vf_add("Speed update");
        }

        public void Af_add(string f, [Optional] string v)
        {
            if (f == "sofalizer")
            {
                Af.Add("\"pan=stereo|FL = 0.414*FL + 0.293*FC + 0.293*SL|FR = 0.414*FR + 0.293*FC + 0.293*SR,loudnorm=I=-16:TP=-1.5:LRA=11:measured_I=-27.61:measured_LRA=18.06:measured_TP=-4.47:measured_thresh=-39.20:offset=0.58:linear=true:print_format=summary\"");
            }

            if (f == "volume")
                Af.Add("volume=1.3");

            if (f == "normalize")
                Af.Add("loudnorm=I=-16:TP=-1.5:LRA=11:measured_I=-27.61:measured_LRA=18.06:measured_TP=-4.47:measured_thresh=-39.20:offset=0.58:linear=true:print_format=summary");

            if (f == "noisereduction")
                Af.Add("arnndn=m='" + resdir + "std.rnnn':mix=0.65,afftdn=nr=3:nf=-20");
        }

        public string Build_vstr(bool predict = false)
        {
            string str = " -hide_banner -copyts -start_at_zero -display_rotation 0 -y !seek! -i \"!file!\" !start! !duration!";
            str += " -c:v:0 " + Cv;
            List<string> vf = new List<string>(Vf);
            bool always_2p = Cv == "libvpx-vp9" && Regex.Match(Params, "auto-alt-ref [1-6]").Success;
            if (Vf.Count > 0)
            {
                if (Vf.FindIndex(s => s.StartsWith("setpts=")) > -1)
                {
                    Out_spd = Func.Get_speed(Vf);
                    vf.RemoveAll(s => s.StartsWith("setpts="));
                }
                int pos = vf.FindIndex(s => s.Contains("libplacebo="));
                if (pos > -1)
                {
                    if (pos > 0 && vf[pos - 1].StartsWith("scale"))
                    {
                        string[] wh = Func.Find_w_h(new List<string>() { vf[pos - 1] });
                        Match algo = Regex.Match(vf[pos - 1], @"flags=(bilinear|neighbor|lanczos|spline|gauss)");
                        string downscaler = "";
                        if (algo.Success)
                            downscaler = ":downscaler=" + algo.Groups[1].ToString().Replace("neighbor", "nearest").Replace("spline", "spline36").Replace("gauss", "gaussian");
                        vf[pos] = vf[pos].Replace("libplacebo=", "libplacebo=w=" + wh[0] + ":h=" + wh[1] + downscaler + ":");
                        vf.RemoveAll(s => s.StartsWith("scale"));
                    }
                }
            }
            if (vf.Count > 0)
            {
                if (Vf.FindIndex(s => s.Contains("vidstabtransform")) > -1)
                    str = " -copyts -start_at_zero -y !seek! -i \"!file!\" !start! !duration! -vf \"vidstabdetect=shakiness=10:accuracy=5:result='transforms.trf'\" -f null NUL && ffmpeg" + str;

                foreach (string s in Vf)
                {
                    if (s.Contains("libplacebo") && Libplacebo)
                    {
                        str += " -init_hw_device vulkan:" + Vkn_Device;
                        break;
                    }
                    if (s.Contains("opencl"))
                    {
                        str += " -init_hw_device opencl=" + OCL_Device + " -filter_hw_device " + OCL_Device;
                        break;
                    }
                }
                str += " -vf " + String.Join(",", vf.ToArray());
            }
            str += " -pix_fmt " + (Bits == 8 ? "yuv420p" : "yuv420p10le");
            str += " -fps_mode vfr";
            if (V_kbps > 0)
            {
                str += " -b:v !bitrate!k";
                if (Multipass != "" && !predict)
                    str += " " + Multipass;
                if (Cv == "librav1e")
                    str += " -rav1e-params bitrate=!bitrate!";
            }
            else if (always_2p)
                str += " " + Multipass;
            else
            {
                if (Cv == "libxvid")
                    str += " -qscale:v ";
                else if (Cv == "h264_nvenc" || Cv == "hevc_nvenc" || Cv == "av1_nvenc")
                    str += " -maxrate:v 200M -cq:v ";
                else if (Cv == "librav1e")
                    str += " -rav1e-params quantizer=";
                else
                    str += " -crf ";
                str += Crf.ToString();
            }
            str += (Cv != "librav1e" ? " " : "") + speed_str + Speed;
            str += (Cv != "librav1e" ? " " : "") + Func.Replace_gs((Func.Param_replace(Params, "enable-keyframe-filtering", "")), Gs_level);
            if (V_kbps > 0 && Multipass != "" && !predict && Cv == "libx265")
                str += ":!reuse!";
            if (!Hdr)
                str += Color;
            str += " -an";
            if ((V_kbps > 0 || always_2p) && Multipass != "" && !predict)
                str = Pass(str) + " -loglevel error -f null NUL && ffmpeg" + Pass(str, 2);
            str += " -map 0:v:0 -muxpreload 0 -muxdelay 0 -mpegts_copyts 1 -bsf:v dump_extra \"!name!\"";
            return str;
        }

        public string Build_astr(int track)
        {
            string astr;
            if (AudioPassthru)
            {
                astr = " -vn -async 1 -c:a copy -map 0:a:" + track;
            }
            else
            {
                if (Ca == "libfdk_aac")
                {
                    if (Ba < 8)
                        Ch = "1";
                    else if (Ba < A_kbps)
                        Ch = "2";
                }
                astr = " -vn -async 1 -c:a " + Ca;
                astr += " -ac " + Ch + " ";
                string p2 = "-profile:a aac_he_v2", p1 = "-profile:a aac_he";
                if (Ca == "libfdk_aac")
                {
                    int ba = int.Parse(Ch);
                    ba = ba > 2 ? Ba / ba * 2 : Ba;
                    if (ba < 8)
                        astr += "-b:a " + Ba + "k ";
                    else if (ba < 24)
                        astr += "-b:a " + Ba + "k " + p2;
                    else if (ba < 41)
                        astr += "-b:a " + Ba + "k " + p1;
                    else if (ba < 45)
                        astr += "-vbr 1 " + p1;
                    else if (ba < 50)
                        astr += "-vbr 2 " + p1;
                    else if (ba < 57)
                        astr += "-vbr 1 " + p1;
                    else if (ba < 66)
                        astr += "-vbr 2 " + p1;
                    else if (ba < 81)
                        astr += "-vbr 3 " + p1;
                    else if (ba < 97)
                        astr += "-vbr 4 " + p1;
                    else if (ba < 121)
                        astr += "-vbr 5 " + p1;
                    else if (ba < 140)
                        astr += "-vbr 2 -cutoff 17000";
                    else if (ba < 160)
                        astr += "-vbr 3 -cutoff 17000";
                    else if (ba < 180)
                        astr += "-vbr 4 -cutoff 17000";
                    else
                        astr += "-vbr 5 -cutoff 18000";

                    if (ba < 8)
                        astr += " -ar 8000";
                    else if (ba < 10)
                        astr += " -ar 16000";
                    else if (ba < 14)
                        astr += " -ar 22050";
                    else if (ba < 20)
                        astr += " -ar 24000";
                    else if (ba < 50)
                        astr += " -ar 32000";
                }
                else
                    astr += "-b:a " + Ba + "k";
                if (Af.Count > 0)
                    astr += " -af " + String.Join(",", Af.ToArray());
                if (astr.IndexOf("[0:a") > -1)
                    astr = astr.Replace(" -af ", " -filter_complex ").Replace("[0:a]", "[0:a:" + track + "]");
                else
                    astr += " -map 0:a:" + track;
            }
            return astr;
        }

        private string Pass(string s, int p = 0)
        {
            if (p == 0)
                return Regex.Replace(s.Replace("!reuse!", "analysis-save=\"!log!.log.reuse\":analysis-save-reuse-level=10"), ":hme[^:]*", "");
            else
                return Regex.Replace(s.Replace("!reuse!", "analysis-load=\"!log!.log.reuse\":analysis-load-reuse-level=10:refine-intra=3").Replace("pass 1", "pass 2"), ":hme[-=][^:]*", "");
        }

        public string Params_vbr(string str, string vbr_str, bool remove = false)
        {
            if (vbr_str == null || vbr_str == "")
                return str;
            bool g = vbr_str[0] == '-';
            string[] param = g ? vbr_str.Substring(1).Split(new string[] { " -" }, StringSplitOptions.None) : vbr_str.Split(':');
            foreach (string p in param)
            {
                string[] arr = p.Replace('=', ' ').Split(' ');
                if (remove)
                    str = Func.Param_replace(str, arr[0], "");
                else
                {
                    if (!str.Contains(arr[0]))
                        str = g ? ("-" + arr[0] + " " + arr[1] + " " + str) : Regex.Replace(str, "params ([a-z])", m => "params " + arr[0] + "=" + arr[1] + ":" + m.Groups[1].Value);
                }
            }
            return str;
        }

        public void Save_settings(ToolStripComboBox format, ToolStripComboBox codec_video, ToolStripComboBox speed, ToolStripComboBox resolution, ToolStripComboBox hdr, ToolStripComboBox bit_depth, NumericUpDown crf, ToolStripComboBox codec_audio, ToolStripComboBox channels, TextBox ba, string output_folder, Settings s)
        {
            if (Form.ActiveForm == null)
                return;
            Settings settings;
            string res_s = resolution.SelectedIndex > 0 || (resolution.Text != "" && int.Parse(resolution.Text.Replace("p", "")) > Screen.FromControl(Form.ActiveForm).Bounds.Height) ? resolution.Text : "Default";
            string hdr_s = hdr.Enabled ? hdr.Text : "Default";
            string bit_s = bit_depth.Items.Count > 1 ? bit_depth.Text : "Default";
            string ch_s = (channels.Text != c[0] || c.Length > 2) && channels.Items.Count > 1 ? channels.Text : "Default";
            if (System.IO.File.Exists("settings.xml"))
            {
                settings = Load_settings();
                settings.Format = format.Text;
                settings.Codec_video = codec_video.Text;
                settings.Speed = speed.Text;
                if (resolution.Text != "" && settings.Resolution != "Default" && int.Parse(resolution.Text.Replace("p", "")) >= int.Parse(settings.Resolution.Replace("p", "")))
                    settings.Resolution = resolution.Text;
                else
                    settings.Resolution = res_s == "Default" ? settings.Resolution : res_s;
                settings.Hdr = hdr_s == "Default" ? settings.Hdr : hdr_s;
                settings.Bit_depth = bit_s == "Default" ? settings.Bit_depth : bit_s;
                settings.Crf = crf.Value.ToString();
                settings.Codec_audio = codec_audio.Text;
                settings.Channels = ch_s == "Default" ? settings.Channels : ch_s[0] == '2' ? "Default" : ch_s;
                settings.Audio_br = ba.Text;
                settings.Output_folder = output_folder;
                settings.Delete_temp_files = s.Delete_temp_files;
            }
            else
            {
                settings = new Settings
                {
                    Format = format.Text,
                    Codec_video = codec_video.Text,
                    Speed = speed.Text,
                    Resolution = res_s,
                    Hdr = hdr_s,
                    Bit_depth = bit_s,
                    Crf = crf.Value.ToString(),
                    Codec_audio = codec_audio.Text,
                    Channels = ch_s,
                    Audio_br = ba.Text,
                    Output_folder = output_folder,
                    Delete_temp_files = s.Delete_temp_files
                };
            }
            settings.CustomVf = s.CustomVf;
            settings.CustomAf = s.CustomAf;

            var writer = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
            var wfile = new System.IO.StreamWriter(@"settings.xml");
            writer.Serialize(wfile, settings);
            wfile.Close();
        }

        public Settings Load_settings()
        {
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
            System.IO.StreamReader file = new System.IO.StreamReader("settings.xml");
            Settings settings = (Settings)reader.Deserialize(file);
            file.Close();
            if (settings.Delete_temp_files == 0)
                settings.Delete_temp_files = 5; // keep segments and audio by default
            return settings;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    public class Settings
    {
        public string Format;
        public string Codec_video;
        public string Speed;
        public string Resolution;
        public string Hdr;
        public string Bit_depth;
        public string Crf;
        public string Codec_audio;
        public string Channels;
        public string Audio_br;
        public string Output_folder;
        public uint Delete_temp_files;
        public List<string> CustomVf;
        public List<string> CustomAf;
    }
}
