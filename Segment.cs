﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Av1ador
{
    internal class Encode
    {
        const string tempdir = "temp\\";
        private double progress;
        private double initial_progress;
        private double video_size;
        private long audio_size;
        private double speed;
        private TimeSpan remaining;
        private readonly Stopwatch watch;
        private int bitrate;
        private double track_delay;
        public string Dir { get; set; }
        public string File { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public int Split_min_time { get; set; }
        public bool Can_run { get; set; }
        public bool Failed { get; set; }
        public List<string> Splits { get; set; }
        public List<string> Complexity { get; set; }
        public int Workers { get; set; }
        public string Param { get; set; }
        public string A_Param { get; set; }
        public double Kf_interval { get; set; }
        public Segment[] Chunks { get; set; }
        public List<int> Order { get; set; }
        public string Job { get; set; }
        public string A_Job { get; set; }
        public bool Finished { get; set; }
        public double Merge { get; set; }
        public string Fps_filter { get; set; }
        public List<string> Status { get; set; }
        public bool Running { get; set; }
        public double Abr { get; set; }
        public double Peak_br { get; set; }
        public int Counter { get; set; }
        public static string Encoding_file { get; set; }
        public int Progress
        {
            get
            {
                if (Counter > 5)
                {
                    Counter = 0;
                    if (Chunks != null && Splits != null)
                    {
                        double pct = 0;
                        foreach (var chunk in Chunks.ToList())
                            pct += chunk.Completed ? chunk.Length : chunk.Progress;
                        progress = pct * (double)100 / (Double.Parse(Splits[Splits.Count - 1]) - Double.Parse(Splits[0]));
                        if (initial_progress == 0 && progress > 0)
                            initial_progress = progress;
                        return (int)Math.Ceiling(progress);
                    }
                    return 0;
                }
                else
                {
                    Counter++;
                    return (int)Math.Ceiling(progress);
                }
            }
        }
        public double Estimated
        {
            get
            {
                if (Counter > 5)
                {
                    if (Chunks != null && Splits != null && progress > 0)
                    {
                        long sum = 0;
                        int n = 0;
                        long bsum = 0;
                        foreach (var chunk in Chunks.ToList())
                        {
                            if (chunk.Completed || chunk.Encoding)
                            {
                                n++;
                                bsum += (long)chunk.Bitrate;
                            }
                            sum += chunk.Size;
                            if (chunk.Bitrate > Peak_br)
                                Peak_br = chunk.Bitrate;
                        }
                        double duration = Double.Parse(Splits[Splits.Count - 1]) - Double.Parse(Splits[0]);
                        double max = (Peak_br + (audio_size * 8.0 / 1024.0) / duration) * duration / 8.0 / 1024.0;
                        double size = ((bsum / (double)n) + (audio_size / 1024.0 * 8.0 / duration)) * duration / 8.0 / 1024.0;
                        sum += audio_size * (long)progress / (long)100;
                        double time = progress * duration / 100.0;
                        Abr = sum / 1024.0 * 8.0 / time;
                        double quadp = Math.Sqrt(progress / 100.0);
                        double quad2p = Math.Pow(progress / 100.0, 0.25);
                        video_size = Math.Round((size * quad2p + (max + size) / 2.0 * (1.0 - quad2p)) * (1.0 - quadp) + (sum / 1024.0 * 100.0 / 1024.0 / progress) * quadp, 1);
                        return video_size;
                    }
                    return 0;
                } else
                    return video_size;
            }
        }

        public double Speed
        {
            get
            {
                if (Counter > 5)
                {
                    if (Chunks != null && Splits != null)
                    {
                        int f = 0;
                        foreach (var chunk in Chunks.ToList())
                            f += chunk.Frames;
                        speed = Math.Round((double)f / watch.ElapsedMilliseconds * 1000.0, 2);
                        return speed;
                    }
                    return 0;
                }
                else
                {
                    return speed;
                }
            }
        }

        public int Segments_left
        {
            get
            {
                if (Chunks != null && Splits != null)
                {
                    int f = 0;
                    foreach (var chunk in Chunks.ToList())
                    {
                        if (!chunk.Encoding && !chunk.Completed)
                            f++;
                    }
                    return f;
                }
                return 0;
            }
        }

        public TimeSpan Remaining
        {
            get
            {
                if (Counter > 5 && progress > 0 && progress != initial_progress)
                {
                    double total = (100.0 - initial_progress) * watch.ElapsedMilliseconds / (progress - initial_progress);
                    remaining = TimeSpan.FromMilliseconds(total - watch.ElapsedMilliseconds);
                }
                return remaining;
            }
        }

        public Encode()
        {
            Split_min_time = 6;
            Status = new List<string>();
            watch = new Stopwatch();
            remaining = new TimeSpan();
            bitrate = 0;
        }

        public void Start_encode(string dir, string file, double ss, double to, double credits, double credits_end, double timebase, double kf_t, bool kf_f, bool audio, double delay = 0, int br = 0)
        {
            Encoding_file = file;
            track_delay = delay;
            Dir = dir == "" ? Path.GetDirectoryName(file) + "\\" : dir + "\\";
            File = file;
            Name = tempdir + Path.GetFileNameWithoutExtension(file);
            if (!Directory.Exists(Name))
                Directory.CreateDirectory(Name);
            else
            {
                Form1.Dialogo = true;
                string[] files = Directory.GetFiles(Name);
                if (files.Length > 0 && MessageBox.Show("There are some files from a previous encoding, do you want to resume it?", "Resume", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                {
                    foreach (FileInfo f in new DirectoryInfo(Name).GetFiles())
                        f.Delete();
                }
                Form1.Dialogo = false;
            }
            bitrate = br;
            bool vbr = br > 0;

            Kf_interval = kf_t;
            double seek = ss - Kf_interval;
            string ss1 = seek > 0 ? " -ss " + seek.ToString() : "";
            string ss2 = ss > 0 ? " -ss " + ss.ToString() : "";
            string audiofile = Name + "\\audio." + A_Job;

            if (audio && A_Param != "")
            {
                if (!System.IO.File.Exists(audiofile))
                {
                    Status.Add("Encoding audio...");
                    Process ffaudio = new Process();
                    Func.Setinicial(ffaudio, 3, " -copyts -start_at_zero -y" + ss1 + " -i \"" + file + "\"" + ss2 + " -to " + to.ToString() + A_Param + " \"" + audiofile + "\"");
                    ffaudio.Start();
                    string aout = ffaudio.StartInfo.Arguments + Environment.NewLine;
                    BackgroundWorker abw = new BackgroundWorker();
                    abw.DoWork += (s, e) =>
                    {
                        while (!ffaudio.HasExited && Can_run)
                            aout += ffaudio.StandardError.ReadLine() + Environment.NewLine;
                        if (!Can_run)
                        {
                            while (System.IO.File.Exists(audiofile))
                            {
                                try { 
                                    ffaudio.Kill();
                                    Thread.Sleep(1000);
                                    System.IO.File.Delete(audiofile);
                                } catch { }
                            }
                        }
                    };
                    abw.RunWorkerCompleted += (s, e) =>
                    {
                        System.IO.File.WriteAllText(Name + "\\audio.log", aout);
                        Status.Remove("Encoding audio...");
                        if (System.IO.File.Exists(audiofile))
                            audio_size = new FileInfo(audiofile).Length;
                    };
                    abw.RunWorkerAsync();
                }
                else
                    audio_size = new FileInfo(audiofile).Length;
            } 

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                if (!System.IO.File.Exists(Name + "\\segments.txt") || (vbr && !System.IO.File.Exists(Name + "\\complexity.txt")))
                {
                    Status.Add("Detecting scenes...");
                    var trim_time = new List<string>{ ss.ToString() };
                    Splits = trim_time;
                    Process ffmpeg = new Process();
                    Func.Setinicial(ffmpeg, 3);
                    double final = credits > 0 && !vbr ? credits - (double)Split_min_time : to;
                    if(kf_f || vbr || Fps_filter != "")
                        ffmpeg.StartInfo.Arguments = (vbr ? " -loglevel debug" : "") + " -copyts -start_at_zero" + ss1 + " -i \"" + file + "\"" + ss2 + " -to " + final.ToString() + " -filter:v \"" + Fps_filter + "select='gt(scene,0.1)',select='isnan(prev_selected_t)+gte(t-prev_selected_t\\," + Split_min_time.ToString() + ")',showinfo\" -an -f null - ";
                    else
                        ffmpeg.StartInfo.Arguments = " -copyts -start_at_zero -skip_frame nokey" + ss1 + " -i \"" + file + "\"" + ss2 + " -to " + final.ToString() + " -filter:v showinfo -an -f null - ";
                    ffmpeg.Start();
                    ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
                    string output;
                    Regex t_regex = new Regex("pts:[ ]*([0-9]{2}[0-9]*)");
                    Regex scene_regex = new Regex("scene:([0-9.]+) -> select:([0-1]+).[0]+");
                    double score = 0;
                    int frames = 0;
                    var scenes_complex = new List<string>();
                    double new_timebase = 0;
                    while (!ffmpeg.HasExited && Can_run)
                    {
                        output = ffmpeg.StandardError.ReadLine();
                        if (output != null)
                        {
                            Match compare = t_regex.Match(output);
                            if (compare.Success)
                            {
                                double time = Double.Parse(compare.Groups[1].ToString()) * (new_timebase > 0 ? new_timebase : timebase);
                                Match scene_compare = scene_regex.Match(output);
                                if (!vbr || (scene_compare.Success && scene_compare.Groups[2].ToString() == "1"))
                                {
                                    if (time > final)
                                    {
                                        time = final;
                                        break;
                                    }
                                    else if (time > ss)
                                    {
                                        if (trim_time.Count > 0 && (time - Double.Parse(trim_time[trim_time.Count - 1])) > Split_min_time)
                                        {
                                            trim_time.Add(time.ToString());
                                            Splits = trim_time;

                                            if (frames > 0)
                                            {
                                                scenes_complex.Add((score / (double)frames).ToString());
                                                score = 0;
                                                frames = 0;
                                            }
                                        }
                                    }
                                }
                                else if (scene_compare.Success)
                                {
                                    score += Double.Parse(scene_compare.Groups[1].ToString());
                                    frames++;
                                }
                            } 
                            else
                            {
                                if (Fps_filter != "" && new_timebase == 0)
                                {
                                    Regex tb_regex = new Regex("time_base:[ ]*([0-9]+)/([0-9]+)");
                                    compare = tb_regex.Match(output);
                                    if (compare.Success)
                                        new_timebase = Double.Parse(compare.Groups[1].ToString()) / Double.Parse(compare.Groups[2].ToString());
                                }
                            }
                        }
                    }
                    if (!Can_run)
                    {
                        try { ffmpeg.Kill(); } catch { }
                    } else {
                        if (credits > 0 && !vbr)
                        {
                            trim_time.Add("000" + credits.ToString());
                            if (credits_end > 0)
                                trim_time.Add(credits_end.ToString());
                        }
                        trim_time.Add(to.ToString());
                        if (vbr)
                        {
                            if (frames > 0)
                                scenes_complex.Add((score / (double)frames).ToString());
                            System.IO.File.WriteAllLines(Name + "\\complexity.txt", scenes_complex.ToArray());
                        }
                        Splits = trim_time;
                        System.IO.File.WriteAllLines(Name + "\\segments.txt", trim_time.ToArray());
                    }
                }
            };
            bw.RunWorkerCompleted += (s, e) => {
                Status.Remove("Detecting scenes...");
                if (Can_run)
                {
                    Status.Add("Encoding video...");
                    watch.Start();
                    Encoding();
                }
            };
            bw.RunWorkerAsync();
        }

        public void Encoding()
        {
            Running = true;
            if (Splits == null)
                Splits = new List<string>(System.IO.File.ReadAllLines(Name + "\\segments.txt"));
            if (bitrate > 0 && Complexity == null)
                Complexity = new List<string>(System.IO.File.ReadAllLines(Name + "\\complexity.txt"));
            if (Chunks == null)
            {
                Chunks = new Segment[Splits.Count - 1];
                List<KeyValuePair<int, double>> Sorted = new List<KeyValuePair<int, double>>();
                Order = new List<int>();
                double avg_scene_score = 0;
                double duration = Double.Parse(Splits[Splits.Count - 1]);
                double min_scene_duration = duration;
                if (bitrate > 0 && Complexity != null)
                {
                    for (int i = 0; i < Splits.Count - 1; i++)
                    {
                        avg_scene_score += Double.Parse(Complexity[i]);
                        double scene_duration = Double.Parse(Splits[i + 1]) - Double.Parse(Splits[i]);
                        if (scene_duration < min_scene_duration)
                            min_scene_duration = scene_duration;
                    }
                    avg_scene_score /= Complexity.Count;
                }
                for (int i = 0; i < Splits.Count - 1; i++)
                {
                    double start = Double.Parse(Splits[i]);
                    double seek = start - Kf_interval;
                    string pathfile = Name + "\\" + i.ToString("00000") + "." + Job;
                    double scene_duration = Double.Parse(Splits[i + 1]) - start;
                    int scene_bitrate = bitrate;
                    if (avg_scene_score > 0)
                    {
                        double scene_score = Double.Parse(Complexity[i]);
                        scene_bitrate = (int)(((scene_score * 0.6 + avg_scene_score * 0.4) / avg_scene_score * (double)bitrate - (double)bitrate) / (scene_duration / min_scene_duration) + (double)bitrate);
                    }
                    Chunks[i] = new Segment
                    {
                        Arguments = Replace_times(Param, File, pathfile, Splits[i + 1], seek, start, scene_bitrate),
                        Pathfile = pathfile,
                        Length = scene_duration,
                        Credits = Splits[i].Length > 3 && Splits[i].Substring(0, 3) == "000"
                    };
                    Sorted.Add(new KeyValuePair<int, double>(i, Chunks[i].Length));
                }
                Sorted.Sort((x, y) => (y.Value.CompareTo(x.Value)));
                while(Sorted.Count > 0)
                {
                    if (Order.Count % 2 == 0)
                    {
                        Order.Add(Sorted[0].Key);
                        Sorted.RemoveAt(0);
                    }
                    else
                    {
                        Order.Add(Sorted[Sorted.Count - 1].Key);
                        Sorted.RemoveAt(Sorted.Count - 1);
                    }
                }
            }
            int instances = 0;
            int done = 0;
            for (int i = 0; i < Order.Count; i++)
            {
                Segment chunk = Chunks[Order[i]];
                bool skip = true;
                if (chunk.Encoding)
                    instances++;
                if (!chunk.Completed && !chunk.Encoding && chunk.Progress == 0 && Can_run)
                {
                    if (System.IO.File.Exists(chunk.Pathfile) && new FileInfo(chunk.Pathfile).Length > 500 && !System.IO.File.Exists(chunk.Pathfile + ".txt"))
                    {
                        skip = false;
                        chunk.Completed = true;
                        chunk.Progress = chunk.Progress == 0 ? 1 : chunk.Progress;
                        chunk.Size = new FileInfo(chunk.Pathfile).Length;
                        chunk.Bitrate = chunk.Size / (double)1024 * (double)8 / chunk.Length;
                    }
                    else
                    {
                        instances++;
                        chunk.Encoding = true;
                        Thread thread = new Thread(() => Background(chunk));
                        thread.Start();
                    }
                }
                if (chunk.Completed)
                    done++;
                if (instances >= Workers && skip)
                    break;
            }
            if (done == Chunks.Length)
            {
                watch.Stop();
                watch.Reset();
                Status.RemoveAll(s => s.StartsWith("Encoding video"));
                Thread trd = new Thread(new ThreadStart(Concatenate))
                {
                    IsBackground = true
                };
                trd.Start();
            }
            Running = false;
        }

        private void Concatenate()
        {
            while (Status.Count > 0)
            {
                Thread.Sleep(500);
            }
            Status.Add("Merging segments...");
            var files = new List<string>();
            for (int i = 0; i < Chunks.Length; i++)
                files.Add("file '" + i.ToString("00000").ToString() + "." + Job + "'");
            System.IO.File.WriteAllLines(Name + "\\concat.txt", files.ToArray());
            Process ffconcat = new Process();
            Func.Setinicial(ffconcat, 3);
            string b = A_Job == "m4a" ? "-bsf:a aac_adtstoasc " : "";
            b += Extension == "mp4" ? "-movflags faststart " : "";
            if (System.IO.File.Exists(Name + "\\audio." + A_Job))
                ffconcat.StartInfo.Arguments = " -y -f concat -safe 0 -i \"" + Name + "\\concat.txt" + "\"" + (track_delay < 0 ? " -itsoffset " + track_delay : "") + " -i \"" + Name + "\\audio." + A_Job + "\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0? -map_metadata -1 " + b + "\"" + Dir + Path.GetFileNameWithoutExtension(Name) + "_Av1ador." + Extension + "\"";
            else
                ffconcat.StartInfo.Arguments = " -y -f concat -safe 0 -i \"" + Name + "\\concat.txt" + "\" -c:v copy -an -map 0:v:0 -map_metadata -1 " + b + "\"" + Dir + Path.GetFileNameWithoutExtension(Name) + "_Av1ador." + Extension + "\"";
            ffconcat.Start();
            Regex regex = new Regex("time=([0-9]{2}):([0-9]{2}):([0-9]{2}.[0-9]{2})");
            Match compare;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                string output;
                while (!ffconcat.HasExited)
                {
                    output = ffconcat.StandardError.ReadLine();
                    if (output != null)
                    {
                        compare = regex.Match(output);
                        if (compare.Success)
                        {
                            double d = Double.Parse(compare.Groups[1].ToString()) * 3600 + Double.Parse(compare.Groups[2].ToString()) * 60 + Double.Parse(compare.Groups[3].ToString());
                            if (d > Merge)
                                Merge = d;
                        }
                    }
                }
            };
            bw.RunWorkerCompleted += (s, e) => {
                Status.Remove("Merging segments...");
                Cleanup();
                Finished = true;
            };
            bw.RunWorkerAsync();
        }

        private void Cleanup()
        {
            try
            {
                long size = new FileInfo(Dir + Path.GetFileName(Name) + "_Av1ador." + Extension).Length;
                if (size > 500 && !System.IO.File.Exists("debug"))
                    Directory.Delete(Name, true);
            }
            catch { }
            Encoding_file = null;
        }

        public void Background(Segment chunk)
        {
            string log = chunk.Start();
            if (log != "" && !Failed)
            {
                Failed = true;
                if (MessageBox.Show(log, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                {
                    Status = new List<string>
                    {
                        "Failed"
                    };
                    Failed = false;
                    Chunks = null;
                }
            }
            if (!Running && !Failed)
                Encoding();
        }

        public string Replace_times(string str, string file, string name, string to, [Optional] double seek, [Optional] double ss, [Optional] int bitrate)
        {
            string ss1 = seek > 0 ? "-ss " + seek.ToString() : "";
            str = str.Replace("!file!", file).Replace("!duration!", "-to " + to);
            string ss2 = ss > 0 ? "-ss " + ss.ToString() : "";
            str = str.Replace("!seek!", ss1).Replace("!start!", ss2);
            if (bitrate > 0)
            {
                str = str.Replace("!bitrate!", bitrate.ToString());
                str = str.Replace("!log!", name);
            }
            return str.Replace("!name!", name).Replace("transforms.trf", name.Replace(@"\", @"\\") + ".trf");
        }

        public int Get_segment(int w, int x, double duration)
        {
            int time = x * (int)duration / w;
            for (int i = 0; i < Splits.Count - 1; i++)
            {
                if (time < (int)Double.Parse(Splits[i + 1]) && time >= (int)Double.Parse(Splits[i]))
                    return i;
            }
            return -1;
        }

        public void Set_fps_filter(List<string> vf)
        {
            string str = "";
            foreach(string f in vf)
            {
                if (f.Contains("fps=fps=") || f.Contains("framestep="))
                    str += f + ",";
            }
            Fps_filter = str;
        }

        public void Clear_splits(string file)
        {
            string name = tempdir + Path.GetFileNameWithoutExtension(file);
            if (System.IO.File.Exists(name + "\\segments.txt"))
                System.IO.File.Delete(name + "\\segments.txt");
        }

        public void Set_state(bool stop = false)
        {
            if (stop)
            {
                watch.Stop();
                watch.Reset();
            }
            if (Chunks != null)
                foreach (Segment chunk in Chunks)
                {
                    chunk.Stop = stop;
                    chunk.Frames = 0;
                }
        }
    }
    internal class Segment
    {
        public bool Completed { get; set; }
        public bool Encoding { get; set; }
        public long Size { get; set; }
        public double Bitrate { get; set; }
        public double Length { get; set; }
        public int Frames { get; set; }
        public double Progress { get; set; }
        public string Arguments { get; set; }
        public string Pathfile { get; set; }
        public bool Stop { get; set; }
        public bool Credits { get; set; }
        private string output;

        public string Start()
        {
            Encoding = true;
            Process ffmpeg = new Process();
            Func.Setinicial(ffmpeg, 3, Credits ? Func.Worsen_crf(Func.Replace_gs(Arguments, 0)) : Arguments);
            bool multi = ffmpeg.StartInfo.Arguments.Contains("&& ffmpeg");
            if (multi)
            {
                string pass = ffmpeg.StartInfo.Arguments;
                int pos1 = pass.IndexOf("&& ffmpeg");
                while (pos1 > -1)
                {
                    ffmpeg.StartInfo.Arguments = pass.Substring(0, pos1);
                    pass = pass.Substring(pos1 + 9);
                    pos1 = pass.IndexOf("&& ffmpeg");
                    ffmpeg.Start();
                    ffmpeg.StandardError.ReadToEnd();
                }
                ffmpeg.StartInfo.Arguments = pass;
            }
            File.WriteAllText(Pathfile + ".txt", "ffmpeg" + Arguments);
            Stopwatch watch = Stopwatch.StartNew();
            watch.Start();
            ffmpeg.Start();
            ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
            Regex regex = new Regex("time=([0-9]{2}):([0-9]{2}):([0-9]{2}.[0-9]{2})", RegexOptions.RightToLeft);
            Regex regex_frames = new Regex("frame=[ ]*([0-9]+) ", RegexOptions.RightToLeft);
            Match compare;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                bool jump = false;
                while (!Stop && !jump)
                {
                    Process crash = new Process();
                    try
                    {
                        crash = Process.GetProcessById(ffmpeg.Id);
                    }
                    catch
                    {
                        jump = true;
                    }
                    if (ffmpeg.HasExited || (crash != null && crash.HasExited))
                        jump = true;
                    output += jump ? ffmpeg.StandardError.ReadToEnd() : ffmpeg.StandardError.ReadLine();
                    Thread.Sleep(100);
                }
            };
            bw.RunWorkerAsync();
            while (!Stop)
            {
                bool jump = false;
                Process crash = new Process();
                try
                {
                    crash = Process.GetProcessById(ffmpeg.Id);
                }
                catch
                { 
                    jump = true;
                }
                if (jump || ffmpeg.HasExited || (crash != null && crash.HasExited))
                {
                    jump = true;
                    Thread.Sleep(1000);
                }

                if (output != null)
                {
                    if (output.Length > 1500)
                        output = output.Substring(output.Length - 1500);

                    compare = regex.Match(output);
                    if (compare.Success)
                    {
                        double d = Double.Parse(compare.Groups[1].ToString()) * 3600 + Double.Parse(compare.Groups[2].ToString()) * 60 + Double.Parse(compare.Groups[3].ToString());
                        if (d > Progress)
                        {
                            Progress = d;
                            compare = regex_frames.Match(output);
                            if (compare.Success)
                                Frames = int.Parse(compare.Groups[1].ToString());
                        }
                    }
                }
                if (File.Exists(Pathfile))
                {
                    Size = new FileInfo(Pathfile).Length;
                    Bitrate = Size / (double)1024 * (double)8 / Length;
                }
                if (jump)
                    break;
                Thread.Sleep(250);
            }
            Encoding = false;
            if (Stop)
            {
                while (File.Exists(Pathfile))
                {
                    try
                    { 
                        ffmpeg.Kill();
                        Thread.Sleep(1000);
                        File.Delete(Pathfile);
                        break;
                    }
                    catch { }
                }
                Progress = 0;
                Stop = false;
            }
            else
            {
                Completed = true;
                if (File.Exists(Pathfile))
                {
                    Size = new FileInfo(Pathfile).Length;
                    Bitrate = Size / (double)1024 * (double)8 / Length;
                }
                if (Progress == 0 && Size < 500)
                {
                    File.AppendAllText(Pathfile + ".txt", output);
                    return output;
                }
            }
            File.Delete(Pathfile + ".txt");
            if (multi)
            {
                if (File.Exists(Pathfile + "-0.log"))
                    File.Delete(Pathfile + "-0.log");
                if (File.Exists(Pathfile + "-0.log.mbtree"))
                    File.Delete(Pathfile + "-0.log.mbtree");
                if (File.Exists(Pathfile + ".trf"))
                    File.Delete(Pathfile + ".trf");
            }
            return "";
        }
    }
}
