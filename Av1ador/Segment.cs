using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
        private double progress;
        private double initial_progress;
        private double video_size;
        private long audio_size;
        private double speed;
        private TimeSpan remaining;
        private readonly Stopwatch watch;
        private int bitrate;
        private double track_delay;
        private int[] fps = new int[0];
        private int frames_last;
        public string Tempdir { get; } = "temp\\";
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
        public int SubIndex { get; set; }
        public bool Finished { get; set; }
        public double Merge { get; set; }
        public string Fps_filter { get; set; }
        public double Spd { get; set; }
        public List<string> Status { get; set; }
        public bool Running { get; set; }
        public double Abr { get; set; }
        public double Peak_br { get; set; }
        public int Counter { get; set; }
        public uint Clean { get; set; }
        public string Cv { get; set; }
        public string Ca { get; set; }
        private bool EncodeStart { get; set; } = true;
        public static BackgroundWorker[] Bw { get; set; }
        private string LogFilePath { get; set; }

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
                        if (initial_progress == 0 && progress > 0 && !Running)
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

        private int lastLoggedPercentage = 0;

        private void LogEstimatesToFile( string message )
        {
            try
            {
                using StreamWriter writer = new StreamWriter(Name + LogFilePath, append: true);
                writer.WriteLine( message );
            }
            catch (Exception ex)
            {
                // Handle potential file writing exceptions (e.g., file access issues)
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        public double Estimated
        {
            get
            {
                if (EncodeStart)
                {
                    LogEstimatesToFile($"[{DateTime.Now:dd-MM-yyyy HH:mm}] Encoding started...");
                    EncodeStart = false;
                    return 0;
                }

                if (Counter <= 5)
                {
                    return video_size;
                }

                if (Chunks == null || Splits == null || progress <= 0)
                {
                    return 0;
                }

                // Ensure Splits has at least two elements for duration calculation
                if (Splits.Count < 2)
                {
                    return 0; // Fallback if duration cannot be determined
                }

                // Check if at least 2% of all chunks are completed
                double threshold = 0.02;
                int totalChunks = Chunks.Count();
                int minCompletedChunks = (int)Math.Ceiling(totalChunks * threshold);

                int completedChunksCount = Chunks.Count(chunk => chunk.Completed);
                if (completedChunksCount < minCompletedChunks)
                {
                    return 0; // Insufficient data to calculate estimation
                }

                double totalSize = 0;
                double completedBitrateSum = 0;

                // Process all chunks
                foreach (var chunk in Chunks.ToList())
                {
                    if (chunk.Completed)
                    {
                        completedBitrateSum += chunk.Bitrate;
                    }

                    totalSize += chunk.Size;

                    // Track the highest bitrate
                    if (chunk.Bitrate > Peak_br)
                    {
                        Peak_br = chunk.Bitrate;
                    }
                }

                // Calculate duration
                double duration = Double.Parse(Splits[Splits.Count - 1]) - Double.Parse(Splits[0]);
                if (duration <= 0)
                {
                    return 0; // Fallback if duration is invalid
                }

                // Calculate max and average sizes
                double maxBitrateSize = (Peak_br + (audio_size * 8.0 / duration / 1024.0)) * duration / 8.0 / 1024.0;
                double avgBitrate = completedChunksCount > 0 ? completedBitrateSum / completedChunksCount : 0;
                double avgBitrateSize = (avgBitrate + (audio_size * 8.0 / duration / 1024.0)) * duration / 8.0 / 1024.0;

                // Include progress-adjusted audio size
                totalSize += audio_size * progress / 100;
                
                // Calculate time elapsed
                double timeElapsed = progress * duration / 100.0;

                // Compute average bitrate so far
                Abr = totalSize / 1024.0 * 8.0 / timeElapsed;

                video_size = Math.Round(avgBitrate * duration / 8 / 1024);   // simple calculation of final video size based on average bitrate calculation, with no infering

                // Check if we’ve crossed the next 10% threshold
                int currentProgressPercentage = (int)Math.Floor(progress);

                if (currentProgressPercentage / 10 > lastLoggedPercentage / 10)
                {
                    string crfValue = Param.Split(new string[] { "-crf " }, StringSplitOptions.None)[1].Split(' ')[0];
                    LogEstimatesToFile($"[{DateTime.Now:dd-MM-yyyy HH:mm}] {(int)progress}%" + $" -> {(video_size + audio_size / 1024 / 1024 / 1024) / 1024 * Globals.overhead:F2} GB (CRF {crfValue}, {(totalSize / 1024.0 * 8.0 / timeElapsed) * Globals.overhead:F0} Kbps)");
                    lastLoggedPercentage = currentProgressPercentage;
                }

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
                        int frames_new = f - frames_last;
                        frames_last = f;
                        if (frames_new >= 0)
                        {
                            if (fps.Count() >= 40)
                                fps = fps.Skip(1).ToArray();
                            fps = fps.Concat(new int[] { frames_new }).ToArray();
                        }

                        speed = Math.Round((double)fps.Sum() / ((double)fps.Length * 1.5), 2);
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

        public TimeSpan Elapsed
        {
            get
            {
                return TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds);
            }
        }

        public Encode()
        {
            Status = new List<string>();
            watch = new Stopwatch();
            remaining = new TimeSpan();
            LogFilePath = Name + "\\size_estimation.log";
            bitrate = 0;
        }

        public void Start_encode(string dir, Video v, bool audio, bool audioPassthru, double delay = 0, int br = 0, double spd = 1)
        {
            // determines the minimum chunk length (in seconds)
            Split_min_time = 10;

            track_delay = delay;
            Dir = dir == "" ? Path.GetDirectoryName(v.File) + "\\" : dir + "\\";
            File = v.File;
            Name = Tempdir + Path.GetFileNameWithoutExtension(v.File);
            if (!Directory.Exists(Name))
                Directory.CreateDirectory(Name);
            Spd = spd;
            bitrate = br;
            bool vbr = br > 0;

            Kf_interval = v.Kf_interval;
            double seek = v.StartTime - Kf_interval;
            string ss1 = seek > 0 ? " -ss " + seek.ToString() : "";
            string ss2 = v.StartTime > 0 ? " -ss " + v.StartTime.ToString() : "";
            double to = v.EndTime != v.Duration ? v.EndTime : v.Duration + 1;
            double final = v.CreditsTime > 0 && !vbr ? v.CreditsTime - (double)Split_min_time : to;
            double ato = (to - v.StartTime) * Spd;

            if (audioPassthru)
                A_Job = "mkv";   // we use mkv extension to trick ffmpeg into muxing whatever the audio codec is in the original audio

            if (audio && A_Param != "")
            {
                string audiofile = Name + "\\audio." + A_Job;

                if (!System.IO.File.Exists(audiofile))
                {
                    Status.Add("Encoding audio + ");
                    string ssa = v.StartTime > 0 ? " -ss " + v.StartTime.ToString() : "";
                    double toa = (to - v.StartTime) * Spd;
                    Process ffaudio = new Process();
                    Func.Setinicial(ffaudio, 3, " -y" + ssa + " -i \"" + v.File + "\" -t " + toa.ToString() + A_Param + " \"" + audiofile + "\"");
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
                                try
                                {
                                    ffaudio.Kill();
                                    Thread.Sleep(1000);
                                    System.IO.File.Delete(audiofile);
                                }
                                catch { }
                            }
                        }
                    };

                    abw.RunWorkerCompleted += (s, e) =>
                    {
                        System.IO.File.WriteAllText(Name + "\\audio.log", aout);
                        Status.Remove("Encoding audio + ");
                        if (System.IO.File.Exists(audiofile))
                            audio_size = new FileInfo(audiofile).Length;
                    };
                    abw.RunWorkerAsync();
                }
                else
                    audio_size = new FileInfo(audiofile).Length;
            }

            if (!System.IO.File.Exists(Name + "\\chunks.txt") || (vbr && !System.IO.File.Exists(Name + "\\complexity.txt")))
            {
                Status.Add("Detecting scenes...");
                int workers = 4;
                if (v.Width > 1920)
                    workers = 3;

                double tdist = (final - v.StartTime) / (double)workers;
                Bw = new BackgroundWorker[workers];
                List<string>[] trim_time = new List<string>[workers];
                List<string>[] scenes_complex = new List<string>[workers];
                Splits = trim_time[0];
                Regex t_regex = new Regex("pts:[ ]*([0-9]{2}[0-9]*)");
                Regex scene_regex = new Regex("scene:([0-9.]+) -> select:([0-1]+).[0]+");
                Regex tb_regex = new Regex("time_base:[ ]*([0-9]+)/([0-9]+)");

                for (int i = 0; i < workers; i++)
                {
                    trim_time[i] = new List<string> { v.StartTime.ToString() };
                    scenes_complex[i] = new List<string>();
                    Bw[i] = new BackgroundWorker();
                    Bw[i].DoWork += (s, e) =>
                    {
                        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                        object[] param = e.Argument as object[];
                        int j = (int)param[0];
                        double seek = v.StartTime + (double)j * tdist;
                        string ss1 = (seek - Kf_interval) > 0 ? " -ss " + (seek - Kf_interval).ToString() : "";
                        string ss2 = v.StartTime > 0 ? " -ss " + v.StartTime.ToString() : "";
                        double final2 = final - (double)(workers - (j + 1)) * tdist;
                        if (j > 0)
                            ss2 = " -ss " + seek.ToString();
                        Process ffmpeg = new Process();
                        Func.Setinicial(ffmpeg, 3);
                        if ((v.Width <= 1920 || v.Kf_fixed) || vbr || Fps_filter != "")
                            ffmpeg.StartInfo.Arguments = (vbr ? " -loglevel debug" : "") + " -copyts -start_at_zero" + ss1 + " -i \"" + v.File + "\"" + ss2 + " -to " + final2.ToString() + " -filter:v \"" + Fps_filter + "select='gt(scene,0.1)+isnan(prev_selected_t)+gte(t-prev_selected_t\\," + Split_min_time.ToString() + ")',showinfo\" -an -f null - ";
                            //ffmpeg.StartInfo.Arguments = (vbr ? " -loglevel debug" : "") + " -copyts -start_at_zero" + ss1 + " -i \"" + v.File + "\"" + ss2 + " -to " + final2.ToString() + " -filter:v \"" + Fps_filter + "select='gt(scene,0.1)',select='isnan(prev_selected_t)+gte(t-prev_selected_t\\," + Split_min_time.ToString() + ")',showinfo\" -an -f null - ";
                        else
                            ffmpeg.StartInfo.Arguments = " -copyts -start_at_zero -skip_frame nokey" + ss1 + " -i \"" + v.File + "\"" + ss2 + " -to " + final2.ToString() + " -filter:v showinfo -an -f null - ";
                        ffmpeg.Start();
                        ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
                        string output;
                        double score = 0;
                        int frames = 0;
                        double new_timebase = 0;
                        while (!ffmpeg.HasExited && Can_run)
                        {
                            output = ffmpeg.StandardError.ReadLine();
                            if (output != null)
                            {
                                Match compare = t_regex.Match(output);
                                if (compare.Success)
                                {
                                    double time = Double.Parse(compare.Groups[1].ToString()) * (new_timebase > 0 ? new_timebase : v.Timebase);
                                    Match scene_compare = scene_regex.Match(output);
                                    if (!vbr || (scene_compare.Success && scene_compare.Groups[2].ToString() == "1"))
                                    {
                                        if (time > final)
                                        {
                                            time = final;
                                            break;
                                        }
                                        else if (time > seek)
                                        {
                                            if (trim_time[j].Count > 0 && (time - Double.Parse(trim_time[j][trim_time[j].Count - 1])) > Split_min_time)
                                            {
                                                trim_time[j].Add(time.ToString());
                                                Splits = Func.Concat(trim_time);

                                                if (frames > 0)
                                                {
                                                    scenes_complex[j].Add((score / (double)frames).ToString());
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
                        }
                        else
                        {
                            if (vbr && j + 1 == workers)
                            {
                                if (frames > 0)
                                    scenes_complex[j].Add((score / (double)frames).ToString());
                            }
                        }
                    };
                    Bw[i].RunWorkerCompleted += (s, e) =>
                    {
                        bool busy = false;
                        foreach (BackgroundWorker w in Bw)
                        {
                            busy = w.IsBusy;
                            if (busy)
                                break;
                        }
                        if (!busy)
                        {
                            Splits = Func.Concat(trim_time).Distinct().ToList();
                            if (v.CreditsTime > 0 && !vbr)
                            {
                                Splits.Add("000" + v.CreditsTime.ToString());
                                if (v.CreditsEndTime > 0)
                                    Splits.Add(v.CreditsEndTime.ToString());
                            }
                            Splits.Add(to.ToString());
                            System.IO.File.WriteAllLines(Name + "\\chunks.txt", Splits.ToArray());
                            if (vbr)
                                System.IO.File.WriteAllLines(Name + "\\complexity.txt", Func.Concat(scenes_complex, false).ToArray());

                            Begin();
                        }
                    };
                }
                for (int i = 0; i < workers; i++)
                {
                    object[] parameters = new object[] { i };
                    Bw[i].RunWorkerAsync(parameters);
                }
            }
            else
                Begin();
        }

        public void Begin()
        {
            Status.Remove("Detecting scenes...");
            if (Can_run)
            {
                Status.Add("Encoding video...");
                watch.Start();
                Encoding();
            }
        }

        public static List<int> GetEvenlyDistributedOrder(List<KeyValuePair<int, double>> sorted)
        {
            // First, sort the list by key (assumed to represent temporal order)
            sorted.Sort((a, b) => a.Key.CompareTo(b.Key));

            // Remove the very last chunk (end credits) and hold it aside.
            KeyValuePair<int, double> endCredit = sorted[sorted.Count - 1];
            sorted.RemoveAt(sorted.Count - 1);

            // Compute the new processing order using recursive median selection.
            List<int> order = RecursiveEvenOrder(sorted);

            // Append the held-out end-credit chunk as the final element.
            order.Add(endCredit.Key);

            return order;
        }

        /// <summary>
        /// Recursively produces an ordering that picks the median element first,
        /// then interleaves the orders from the left and right sublists.
        /// This yields an order in which early selections are well distributed
        /// along the timeline.
        /// </summary>
        private static List<int> RecursiveEvenOrder(List<KeyValuePair<int, double>> list)
        {
            if (list.Count == 0)
                return new List<int>();
            if (list.Count == 1)
                return new List<int> { list[0].Key };

            // Choose the median index; for even counts, choose the lower median.
            int mid = (list.Count - 1) / 2;
            var order = new List<int> { list[mid].Key };

            // Divide the list into left and right halves.
            var leftSublist = list.GetRange(0, mid);
            var rightSublist = list.GetRange(mid + 1, list.Count - mid - 1);

            // Recursively obtain orders for the sublists.
            var leftOrder = RecursiveEvenOrder(leftSublist);
            var rightOrder = RecursiveEvenOrder(rightSublist);

            // Interleave the two orders.
            var interleaved = InterleaveLists(leftOrder, rightOrder);
            order.AddRange(interleaved);

            return order;
        }

        /// <summary>
        /// Interleaves two lists: taking one element from the left list,
        /// then one from the right, and so on.
        /// </summary>
        private static List<int> InterleaveLists(List<int> left, List<int> right)
        {
            var result = new List<int>();
            int i = 0, j = 0;
            while (i < left.Count || j < right.Count)
            {
                if (i < left.Count)
                {
                    result.Add(left[i]);
                    i++;
                }
                if (j < right.Count)
                {
                    result.Add(right[j]);
                    j++;
                }
            }
            return result;
        }

        public void Encoding()
        {
            if (Running)
                return;
            Running = true;
            Splits ??= new List<string>(System.IO.File.ReadAllLines(Name + "\\chunks.txt"));
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
                    // this is where the fun begins and the parameters for the encoded chunk are created
                    Chunks[i] = new Segment
                    {
                        Arguments = Replace_times(Param, File, pathfile, Splits[i + 1], seek, start, scene_bitrate),
                        Pathfile = pathfile,
                        Length = scene_duration,
                        Credits = Splits[i].Length > 3 && Splits[i].Substring(0, 3) == "000",
                        Last = i + 1 == Splits.Count() - 1
                    };
                    Sorted.Add(new KeyValuePair<int, double>(i, Chunks[i].Length));
                }

                // here we determine the order of the chunks to be encoded
                // Check if the order file exists
                if (System.IO.File.Exists(Name + "\\chunks_order.txt"))
                {
                    // Load the order from the file
                    Order = System.IO.File.ReadAllLines(Name + "\\chunks_order.txt")
                                .Select(line => int.Parse(line))
                                .ToList();
                }
                else
                {
                    Order = GetEvenlyDistributedOrder(Sorted);
                    System.IO.File.WriteAllLines(Name + "\\chunks_order.txt", Order.Select(i => i.ToString()));
                }
            }

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (ss, e) =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                int instances = 0;
                int done = 0;
                for (int i = 0; i < Order.Count; i++)
                {
                    Segment chunk = Chunks[Order[i]];
                    bool skip = true;
                    if (chunk.Encoding)
                        instances++;
                    else if (!chunk.Completed && chunk.Progress == 0 && Can_run)
                    {
                        bool present = System.IO.File.Exists(chunk.Pathfile);
                        if (present && new FileInfo(chunk.Pathfile).Length > 500 && !System.IO.File.Exists(chunk.Pathfile + ".txt")
                            && (!chunk.Compare || Math.Abs(Video.Get_duration(Video.Get_info(chunk.Pathfile), out string _, chunk.Pathfile) - chunk.Length) < 2 || chunk.Last))
                        {
                            skip = false;
                            chunk.Completed = true;
                            chunk.Progress = chunk.Progress == 0 ? 1 : chunk.Progress;
                            chunk.Size = new FileInfo(chunk.Pathfile).Length;
                            chunk.Bitrate = chunk.Size / (double)1024 * (double)8 / chunk.Length;
                            if (watch.ElapsedMilliseconds > 500)
                            {
                                for (int j = 0; j < Order.Count; j++)
                                    Chunks[Order[j]].Frames = 0;
                                if (Entry.elapsed_add == null && watch.ElapsedMilliseconds - Entry.Lastsave > 10000)
                                    Entry.elapsed_add = new object[] { watch.ElapsedMilliseconds - Entry.Lastsave, File };
                            }
                            fps = new int[0];
                            frames_last = 0;
                            Entry.Lastsave = 0;
                            watch.Reset();
                            watch.Start();
                        }
                        else
                        {
                            if (present)
                            try
                            {
                                System.IO.File.Delete(chunk.Pathfile);
                            }
                            catch { }
                            instances++;
                            chunk.Encoding = true;
                            Thread thread = new Thread(() => Background(chunk));
                            thread.Start();
                            Thread.Sleep(100);
                        }
                    }
                    if (chunk.Completed)
                        done++;
                    if (instances >= Workers && skip)
                    {
                        if (Entry.elapsed_add == null && watch.ElapsedMilliseconds - Entry.Lastsave > 300000)
                            Entry.elapsed_add = new object[] { watch.ElapsedMilliseconds - Entry.Lastsave, File };
                        break;
                    }
                }
                if (done == Chunks.Length)
                {
                    watch.Reset();
                    Status.RemoveAll(s => s.StartsWith("Encoding video"));
                    Thread trd = new Thread(new ThreadStart(Concatenate))
                    {
                        IsBackground = true
                    };
                    trd.Start();
                }
                Running = false;
            };
            bw.RunWorkerAsync();
        }

        private string BeautifyOutputName(string output)
        {
            //TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            //string new_output = textInfo.ToTitleCase(output.Replace(".", " ").ToLower());
            return output.Replace(".", " ");
        }

        public string GetEncoderInfo( string encoder, bool video = true )
        {
            Process ffmpeg = new Process();
            if (video)
                Func.Setinicial(ffmpeg, 0, "-hide_banner -f lavfi -i nullsrc -frames:v 1 -c:v " + encoder + " -f null -");
            else
                Func.Setinicial(ffmpeg, 0, "-hide_banner -f lavfi -i anullsrc -frames:a 1 -c:a " + encoder + " -f null -");
            ffmpeg.Start();
            return ffmpeg.StandardError.ReadToEnd();
        }

        private void Concatenate()
        {
            while (Status.Count > 0)
            {
                Thread.Sleep(500);
            }
            Status.Add("Merging chunks...");
            var files = new List<string>();
            for (int i = 0; i < Chunks.Length; i++)
                files.Add("file '" + Name.Replace(Tempdir, "").Replace("'", "\'\\'\'") + "\\" + i.ToString("00000").ToString() + "." + Job + "'");
            System.IO.File.WriteAllLines(Tempdir + "concat.txt", files.ToArray());
            Process ffconcat = new Process();
            Func.Setinicial(ffconcat, 3);
            string b = A_Job == "m4a" ? "-bsf:a aac_adtstoasc " : "";
            b += Extension == "mp4" ? "-movflags faststart " : "";
            string f = Spd != 1 ? " -itsscale " + Spd : "";

            // add encode information metadata to output file
            string videoCodecParams = "-metadata VIDEO_ENCODER_PARAMS=\"" + Param.Replace("\"", "'").Replace("-y !seek! -i '!file!' !start! !duration! ", "").Replace("'!name!'", "") + "\" ";
            string audioCodecParams = "";
            if ( A_Param != null)
                audioCodecParams = "-metadata AUDIO_ENCODER_PARAMS=\"" + A_Param.Replace("\"", "'") + "\" ";
            
            string videoCodecVersion = GetEncoderInfo(Cv);

            Regex vRegex = new Regex("SVT \\[version\\]:\\W(?<encoder_version>.*[l,L]ib*.*)");
            Regex aRegex = new Regex("encoder[ ]+: (?<audio_codec>.*lib.*)");
            Match vCompare;

            vCompare = vRegex.Match(videoCodecVersion);

            if (vCompare.Success)
                videoCodecVersion = "-metadata VIDEO_CODEC_VERSION=\"" + vCompare.Groups[1].ToString() + "\" ";

            string encoderMetadata = (videoCodecVersion + videoCodecParams + audioCodecParams).Replace("\r", "");

            if (System.IO.File.Exists(Name + "\\audio." + A_Job))
                if (SubIndex > -1)
                    ffconcat.StartInfo.Arguments = " -y -f concat -safe 0" + f + " -i \"" + Tempdir + "concat.txt" + "\"" + (track_delay < 0 ? " -itsoffset " + track_delay + "ms" : "") + " -i \"" + Name + "\\audio." + A_Job + "\" -i \"" + File + "\" -async 1 -vsync 1 -c:v copy -c:a copy -c:s copy -map 0:v:0 -map 1:a:0 -map 2:s:" + SubIndex + " -disposition:s:0 default -metadata:s:s:0 language=eng " + b + encoderMetadata + "\"" + Dir + BeautifyOutputName(Path.GetFileName(Name)) + "_Av1ador." + Extension + "\"";
                else
                    ffconcat.StartInfo.Arguments = " -y -f concat -safe 0" + f + " -i \"" + Tempdir + "concat.txt" + "\"" + (track_delay < 0 ? " -itsoffset " + track_delay + "ms" : "") + " -i \"" + Name + "\\audio." + A_Job + "\" -i \"" + File + "\" -async 1 -vsync 1 -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 " + b + encoderMetadata + "\"" + Dir + BeautifyOutputName(Path.GetFileName(Name)) + "_Av1ador." + Extension + "\"";
            else
                ffconcat.StartInfo.Arguments = " -y -f concat -safe 0" + f + "  -i \"" + Tempdir + "concat.txt" + "\" -async 1 -vsync 1 -c:v copy -an -map 0:v:0 -map_metadata -1 " + b + encoderMetadata + "\"" + Dir + BeautifyOutputName(Path.GetFileNameWithoutExtension(Name)) + "_Av1ador." + Extension + "\"";
            ffconcat.Start();
            Regex regex = new Regex("time=([0-9]{2}):([0-9]{2}):([0-9]{2}.[0-9]{2})");
            Match compare;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
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
            bw.RunWorkerCompleted += (s, e) =>
            {
                Status.Remove("Merging chunks...");
                Cleanup();
                Finished = true;
            };
            bw.RunWorkerAsync();
        }
        private void Cleanup()
        {
            try
            {
                if (Clean == 15)
                    Directory.Delete(Name, true);
                else
                {
                    if ((Clean & 2) != 0)
                    {
                        System.IO.File.Delete(Name + "\\chunks.txt");
                        System.IO.File.Delete(Name + "\\chunks_order.txt");
                    }
                    if ((Clean & 4) != 0)
                        foreach (string f in Directory.GetFiles(Name, "*." + Job).Where(i => i.EndsWith("." + Job)))
                            System.IO.File.Delete(f);
                    if ((Clean & 8) != 0)
                        System.IO.File.Delete(Name + "\\audio." + A_Job);
                }
            }
            catch { }
            try
            {
                System.IO.File.Delete(Tempdir + "concat.txt");
            }
            catch { }
        }
        public void Background(Segment chunk)
        {
            string log = chunk.Start();
            if (log == "Retry")
                log = chunk.Start();
            if (log.Trim() != "" && log != "Retry" && !Failed)
            {
                Failed = true;
                Set_state(true);
                if (MessageBox.Show(log, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                {
                    Status = new List<string>
                    {
                        "Failed"
                    };
                    Failed = false; // reset failed state so that we can continue encoding after ffmpeg crash
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
                str = str.Replace("!bitrate!", bitrate.ToString());
            str = str.Replace("!log!.log", name.Replace("\\", "\\\\") + ".log");
            str = str.Replace("!log!", name);
            return str.Replace("!name!", name).Replace("transforms.trf", name.Replace(@"\", @"\\") + ".trf");
        }

        public int Get_segment(int w, int x, double duration, double time)
        {
            time = time > 0 ? time : (double)x * duration / (double)w;
            for (int i = 0; i < Splits.Count - 1; i++)
            {
                if (time < Double.Parse(Splits[i + 1]) && time >= Double.Parse(Splits[i]))
                    return i;
            }
            return -1;
        }

        public void Set_fps_filter(List<string> vf)
        {
            string str = "";
            foreach (string f in vf)
            {
                if (f.Contains("fps=fps=") || f.Contains("framestep="))
                    str += f + ",";
            }
            Fps_filter = str;
        }

        public void Clear_splits(string file)
        {
            string name = Tempdir + Path.GetFileNameWithoutExtension(file);
            if (System.IO.File.Exists(name + "\\chunks.txt"))
                System.IO.File.Delete(name + "\\chunks.txt");
        }

        public void Set_state(bool stop = false)
        {
            if (stop)
                watch.Reset();
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
        public int Retry { get; set; }
        public bool Last { get; set; }
        public bool Compare { get; set; }
        private bool retry;
        private string output;

        public string Start()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Encoding = true;
            Process ffmpeg = new Process();
            Func.Setinicial(ffmpeg, 3, Credits ? Func.Worsen_crf(Func.Replace_gs(Arguments, 0)) : Arguments);
            System.IO.File.WriteAllText(Pathfile + ".txt", "ffmpeg" + ffmpeg.StartInfo.Arguments + "\r\n");
            if (Arguments.Contains("libaom-av1"))
            {
                retry = true;
                if (Retry > 0)
                {
                    output = "";
                    ffmpeg.StartInfo.Arguments = Func.Param_replace(ffmpeg.StartInfo.Arguments, "threads", 1.ToString());
                }
            }
            bool multi = ffmpeg.StartInfo.Arguments.Contains("&& ffmpeg");
            if (multi)
            {
                string pass = ffmpeg.StartInfo.Arguments;
                int pos1 = pass.IndexOf("&& ffmpeg");
                while (pos1 > -1 && !Stop)
                {
                    ffmpeg.StartInfo.Arguments = pass.Substring(0, pos1);
                    pass = pass.Substring(pos1 + 9);
                    pos1 = pass.IndexOf("&& ffmpeg");
                    ffmpeg.Start();
                    ffmpeg.StandardError.ReadToEnd();
                }
                ffmpeg.StartInfo.Arguments = pass;
            }
            System.IO.File.WriteAllText(Pathfile + ".txt", "ffmpeg" + Arguments + "\r\n");
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
                Stopwatch stopwatch = new Stopwatch();
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
                    stopwatch.Start();
                    output += jump ? ffmpeg.StandardError.ReadToEnd() : ffmpeg.StandardError.ReadLine() + "\r\n";
                    stopwatch.Stop();
                    Thread.Sleep((int)Math.Min(stopwatch.ElapsedMilliseconds / 4, 100));
                    stopwatch.Reset();
                }
            };
            bw.RunWorkerAsync();
            int frames_before = 0;
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
                    if (retry && Retry < 2 && (output.Contains("error while decoding") || output.Contains("slice in a frame missing")))
                    {
                        Retry++;
                        if (Retry == 1)
                        {
                            Stop = true;
                            break;
                        }
                    }
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
                            {
                                int total_frames = int.Parse(compare.Groups[1].ToString());
                                Frames += total_frames - frames_before;
                                frames_before = total_frames;
                            }
                        }
                    }
                }
                if (System.IO.File.Exists(Pathfile))
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
                while (System.IO.File.Exists(Pathfile))
                {
                    try
                    {
                        System.IO.File.Delete(Pathfile);
                        break;
                    }
                    catch
                    {
                        try
                        {
                            ffmpeg.Kill();
                        }
                        catch { }
                        Thread.Sleep(1000);
                    }
                }
                Progress = 0;
                Stop = false;
                if (Retry == 1)
                    return "Retry";
            }
            else
            {
                Completed = true;
                if (System.IO.File.Exists(Pathfile))
                {
                    Size = new FileInfo(Pathfile).Length;
                    Bitrate = Size / (double)1024 * (double)8 / Length;
                }
                if (Progress == 0 && Size < 500)
                {
                    System.IO.File.AppendAllText(Pathfile + ".txt", output);
                    return output;
                }
                if (Compare && Math.Abs(Video.Get_duration(Video.Get_info(Pathfile), out string _, Pathfile) - Length) > 2 && !Last)
                    return Pathfile + "\r\nThe segment has finished but there is a mismatch in the duration.\r\nUsually this means that ffmpeg crashed.";
            }
            System.IO.File.Delete(Pathfile + ".txt");
            if (multi)
            {
                if (System.IO.File.Exists(Pathfile + "-0.log"))
                    System.IO.File.Delete(Pathfile + "-0.log");
                if (System.IO.File.Exists(Pathfile + "-0.log.mbtree"))
                    System.IO.File.Delete(Pathfile + "-0.log.mbtree");
                if (System.IO.File.Exists(Pathfile + ".trf"))
                    System.IO.File.Delete(Pathfile + ".trf");
                if (System.IO.File.Exists(Pathfile + ".log.reuse"))
                    System.IO.File.Delete(Pathfile + ".log.reuse");
                if (System.IO.File.Exists(Pathfile + ".log.reuse.temp"))
                    System.IO.File.Delete(Pathfile + ".log.reuse.temp");
            }
            return "";
        }
    }
}
