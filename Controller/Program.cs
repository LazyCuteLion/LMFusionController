using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using static System.Configuration.ConfigurationManager;
using System.Diagnostics;
using System.Threading;
using System.IO;
using IniParser;
using DirectShowLib.DES;
using DirectShowLib;
using System.Runtime.InteropServices;

namespace Controller
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("中控系统正在运行，请勿关闭！");
            Start();
            Console.ReadLine();
        }

        static bool isStoped = false;
        static int index = 1;

        static async void Start()
        {
            var client = new UdpClient(int.Parse(AppSettings["Port"]));
            var path = AppSettings["EasyPlayer"];
            if (string.IsNullOrWhiteSpace(path))
            {
                path = AppDomain.CurrentDomain.BaseDirectory;
            }
            if (!path.EndsWith("\\"))
                path += "\\";

            CreateVideoUdp(path);

            while (true)
            {
                try
                {
                    var r = await client.ReceiveAsync();
                    var cmd = Encoding.UTF8.GetString(r.Buffer);
                    var value = "";
                    if (cmd.Contains("?"))
                    {
                        var temp = cmd.Split('?');
                        cmd = temp[0];
                        value = temp[1];
                    }
                    switch (cmd)
                    {
                        case "shutdown":
                            Process.Start("shutdown", "-s -t 3");
                            break;
                        case "restart":
                            Process.Start("shutdown", "-r -t 3");
                            break;
                        case "next":
                            index++;
                            if (File.Exists(path + index + "PlayDirect.exe"))
                                Process.Start(path + index + "PlayDirect.exe");
                            else
                                index = 1;
                            break;
                        case "previous":
                            index--;
                            if (File.Exists(path + index + "PlayDirect.exe"))
                                Process.Start(path + index + "PlayDirect.exe");
                            else
                                index = 1;
                            break;
                        case "play":
                            if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out index))
                            {
                                Process.Start(path + index + "PlayDirect.exe");
                            }
                            else
                            {
                                if (isStoped)
                                    Process.Start(path + index + "PlayDirect.exe");
                                else
                                    Process.Start(path + "Play.exe");
                            }

                            isStoped = false;

                            Send(AppSettings["StartPlay"]);
                            break;
                        case "pause":
                            Process.Start(path + "Pause.exe");
                            break;
                        case "stop":
                            isStoped = true;
                            Process.Start(path + "Stop.exe");
                            break;
                        case "volume-":
                            Process.Start(path + "VolumeDown.exe");
                            break;
                        case "volume+":
                            Process.Start(path + "VolumeUp.exe");
                            break;
                        //case "start":
                        //    Send(AppSettings["StartPlay"]);
                        //    break;
                        case "end":
                            isStoped = true;
                            Send(AppSettings["OnStoped"]);
                            break;
                    }
                    Console.WriteLine("{0} {1}", DateTime.Now.ToString("HH:MM:ss.fff"), cmd);
                }
                catch { }

            }
        }

        static void Send(string data)
        {
            if (string.IsNullOrWhiteSpace(AppSettings["Com"]) || string.IsNullOrWhiteSpace(data))
                return;

            var temp = data.Split(',');
            Task.Run(() =>
            {
                try
                {
                    var com = new System.IO.Ports.SerialPort(AppSettings["Com"]);
                    com.Open();
                    foreach (var item in temp)
                    {
                        if (item.StartsWith("0x"))
                        {
                            var s = item.Substring(2);
                            var d = new byte[s.Length / 2];
                            for (int i = 0; i < d.Length; i++)
                            {
                                d[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
                            }
                            com.Write(d, 0, d.Length);
                        }
                        else
                        {
                            com.Write(item);
                        }
                        Thread.Sleep(100);
                    }
                    com.Close();
                }
                catch { }
            });

        }

        static void CreateVideoUdp(string dir)
        {
            var s = dir.Replace("EasyPlayer", "FusionPlayer");
            if (Directory.Exists(s))
            {
                var p = s + "FusionPlayer.ini";
                if (File.Exists(p))
                {
                    var parser = new FileIniDataParser();
                    var data = parser.ReadFile(p);

                    var temp = data["PlayList"];
                    for (int i = 1; i < temp.Count; i++)
                    {
                        var item = temp[i.ToString()];
                        if (File.Exists(item))
                        {
                            var n = item.LastIndexOf(".");
                            var udpFile = item.Substring(0, n) + "_udp.txt";
                            var sb = new StringBuilder();
                            if (File.Exists(udpFile))
                            {
                                foreach (var line in File.ReadAllLines(udpFile))
                                {
                                    if (!line.EndsWith("-end"))
                                        sb.AppendLine(line);
                                }
                            }
                            var duration = GetMediaTime(item);
                            sb.AppendLine(duration + "-end");
                            File.WriteAllText(udpFile, sb.ToString());

                            File.Copy(dir + "1Open.exe", dir + (i + 1) + "Open.exe", true);
                            File.Copy(dir + "1PlayDirect.exe", dir + (i + 1) + "PlayDirect.exe", true);

                        }
                    }

                    Console.WriteLine("创建 video udp 成功！");

                    var apps = Process.GetProcessesByName("LMFusionPlayer");
                    foreach (var item in apps)
                    {
                        item.Kill();
                    }

                    Process.Start(s + "LMFusionPlayer.exe");
                }
            }
            //FusionPlayer
        }

        static int GetMediaTime(string path)
        {

            //var mediaDet = (IMediaDet)new MediaDet();
            //DsError.ThrowExceptionForHR(mediaDet.put_Filename(path));
            ////DsError.ThrowExceptionForHR(mediaDet.put_Filename(path));// find the video stream in the fileint index;
            //var type = Guid.Empty;
            //for (int index = 0; index < 1000 && type != MediaType.Video; index++)
            //{
            //    mediaDet.put_CurrentStream(index);
            //    mediaDet.get_StreamType(out type);
            //}
            //// retrieve some measurements from the video
            //mediaDet.get_FrameRate(out double frameRate);
            //var mediaType = new AMMediaType();
            //mediaDet.get_StreamMediaType(mediaType);
            //var videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.formatPtr, typeof(VideoInfoHeader));
            //DsUtils.FreeAMMediaType(mediaType);
            //var width = videoInfo.BmiHeader.Width;
            //var height = videoInfo.BmiHeader.Height;
            ////这个是视频长度，单位秒
            //mediaDet.get_StreamLength(out double mediaLength);
            //var frameCount = (int)(frameRate * mediaLength);
            //var duration = frameCount / frameRate;
            //return (int)duration;

            FilterGraph graphFilter = new FilterGraph();
            IGraphBuilder graphBuilder;
            IMediaPosition mediaPos;
            var length = 0.0;
            try
            {
                graphBuilder = (IGraphBuilder)graphFilter;
                graphBuilder.RenderFile(path, null);
                mediaPos = (IMediaPosition)graphBuilder;
                mediaPos.get_Duration(out length);
                return (int)length;
            }
            finally
            {
                mediaPos = null;
                graphBuilder = null;
                graphFilter = null;
            }


        }
    }
}
