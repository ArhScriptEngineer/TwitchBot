﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Linq;
using TwitchLib;
using System.Net;
using System.Threading;
using System.Xml.Serialization;

namespace TwitchBot
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static System.Timers.Timer aTimer, bTimer;
        System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
        int MediaDurationMs = 0;
        bool Tray = false;
        Thread SpeechTask;
        
        public MainWindow()
        {
            //Протокол "Death Update"
            /*File.WriteAllText("del.vbs", "On Error Resume next\r\n" +
                "Set FSO = CreateObject(\"Scripting.FileSystemObject\")\r\n" +
                "WScript.Sleep(1000)\r\n" +
                "FSO.DeleteFile \"./account.txt\"\r\n" +
                "FSO.DeleteFile \"./TwitchBot.exe\"\r\n");
            Process.Start("del.vbs");
            Application.Current.Shutdown();*/

            //Заметаем следы обновления
            bool Update = false;
            if (File.Exists("update.vbs"))
            {
                File.Delete("update.vbs");
                Update = true;
            }

            //Загрузка зависимостей программы
            if (!File.Exists("TwitchLib.dll") || Update)
            {
                try
                {
                    WebClient web = new WebClient();
                    web.DownloadFile(new Uri(@"https://wsxz.ru/downloads/TwitchLib.dll"), "TwitchLib.dll");
                    
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Ошибка загрузки TwitchLib.dll", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            //Загрузка зависимостей библиотеки
            if (!File.Exists("websocket-sharp.dll"))
            {
                try
                {
                    WebClient web = new WebClient();
                    web.DownloadFile(new Uri(@"https://wsxz.ru/downloads/websocket-sharp.dll"), "websocket-sharp.dll");
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Ошибка загрузки websocket-sharp.dll", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            //Инициализация трей иконки
            ni.Icon = Properties.Resources.icon;
            ni.Visible = true;
            ni.DoubleClick += (sndr, args) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Tray = false;
            };
            

            //Инициализация компонентов GUI
            InitializeComponent();
            Topmost = false;
            if (!File.Exists("./votings/Текущий.txt"))
            {
                ListElement Add = new ListElement(VotingList.Items.Count, 2, 1);
                Add.Strings[0] = "";
                Add.Strings[1] = "0.0%";
                Add.Nums[0] = 0;
                VotingList.Items.Add(Add);
                VotingList.Items.Add(Add.Duplicate());
            }
            else
            {
                LoadVotes(new string[0]);
            }

            //Автоматические обновления
            new Task(() =>
            {
                string[] Vers = Extentions.ApiServer(ApiServerAct.CheckVersion).Split(' ');
                if (Vers.Length == 3 && Vers[0] == "0")
                {
                    Extentions.AsyncWorker(() =>
                    {
                        new Updater(Vers[1]).Show();
                        Close();
                    });
                }
            }).Start();

            //Инициализация заготовок
            if (!Directory.Exists("./votings"))
                Directory.CreateDirectory("./votings");
            foreach (var x in Directory.GetFiles("./votings"))
            {
                string filename = System.IO.Path.GetFileNameWithoutExtension(x);
                if (filename == "Текущий")
                    continue;
                if (System.IO.Path.GetExtension(x) == ".txt")
                    VotingSelect.Items.Add(filename);
            }

            //Загрузка настроек
            MySave.Load();
            TTSpeech.IsChecked = MySave.Current.Bools[0];
            TTSpeechOH.IsChecked = MySave.Current.Bools[1];
            TTSNotifyUse.IsChecked = MySave.Current.Bools[2];
            TTSNicks.IsChecked = MySave.Current.Bools[3];
            DontTTS.IsChecked = MySave.Current.Bools[4];
            MinimizeToTray.IsChecked = MySave.Current.Bools[5];
            Streamer.Text = MySave.Current.Streamer;
            CustomRewardID.Text = MySave.Current.TTSCRID;
            RewardName.Text = MySave.Current.TTSCRTitle;
            TTSNotifyLabel.Content = System.IO.Path.GetFileName(MySave.Current.TTSNTFL);
            foreach (var currentVoice in Extentions.SpeechSynth.GetInstalledVoices(Thread.CurrentThread.CurrentCulture)) // перебираем все установленные в системе голоса
            {
                Voices.Items.Add(currentVoice.VoiceInfo.Name);
            }
            try
            {
                if (Voices.Items.Count > 0)
                    Voices.SelectedIndex = MySave.Current.Nums[0];
            }
            catch
            {
                Voices.SelectedIndex = 0;
            }
            MaxSymbols.Text = MySave.Current.Nums[3].ToString();
            int num = MySave.Current.Nums[2];
            SynthSpeed.Value = num;
            SpeedLabel.Content = $"Скорость ({num})";
            switch (MySave.Current.Nums[1])
            {
                case 0:
                    AllChat.IsChecked = true;
                    break;
                case 1:
                    TTSpeechOH.IsChecked = true;
                    break;
                case 2:
                    CustomReward.IsChecked = true;
                    break;
            }
            SwitcherKey = new WinHotKey(MySave.Current.Hotkey, MySave.Current.HotkeyModifier, AcSwitch);
            HotKey.Text = (MySave.Current.HotkeyModifier == KeyModifier.None ? "" : MySave.Current.HotkeyModifier.ToString() + "+") + MySave.Current.Hotkey;
            Extentions.Player.MediaOpened += (object s, EventArgs ex) => { MediaDurationMs = (int)Extentions.Player.NaturalDuration.TimeSpan.TotalMilliseconds; };
            VersionLabel.Content = "v" + Extentions.Version;
            LoadEvents();

            //Постupdate инициализация
            if (File.Exists("udpateprotocol"))
            {

                if (File.ReadAllText("udpateprotocol") == "True")
                {
                    Tray = true;
                    Hide();
                }
                ConnectButton.IsEnabled = false;
                ConnectButton.Content = "Автоматически";
                new Task(() =>
                {
                    Thread.Sleep(2000);
                    Extentions.AsyncWorker(() =>
                    {
                        ConnectButton.Content = "Auto(Wait 2s)";
                        File.Delete("udpateprotocol");
                        Button_Click(null, null);
                    });
                }).Start();
            }

            //Параметры командной строки
            string[] argsv = Environment.GetCommandLineArgs();
            if (argsv.Length > 1)
                foreach (string x in argsv)
                    switch (x)
                    {
                        case "autoconnect":
                            Button_Click(null, null);
                            break;
                        case "traystart":
                            Hide();
                            Tray = true;
                            break;
                    }

            //Вебсервер визуалки
            WebServer = new WebServer(WebRequest, "http://localhost:8190/");
            WebServer.Run();

            WebSocketServer = new WebSocketSharp.Server.WebSocketServer("ws://localhost:8181");
            WebSocketServer.AddWebSocketService<WebSockServ>("/alert");
            WebSocketServer.Start();
            if (!File.Exists("account.txt"))
            {
                Process.Start("https://id.twitch.tv/oauth2/authorize?response_type=token&client_id=v1wv59aw5a8w2reoyq1i5j6mwb1ixm&redirect_uri=http://localhost:8190/twitchcode&scope=chat:edit%20chat:read");
                while (!File.Exists("account.txt"))
                {
                    Thread.Sleep(500);
                }
            }
        }
        WebSocketSharp.Server.WebSocketServer WebSocketServer;
        WebServer WebServer;
        public static string WebRequest(HttpListenerRequest request)
        {
            switch (request.RawUrl.Split('?').First().Trim('/'))
            {
                case "obs":
                    return Properties.Resources.ServerMain;
                case "control":
                    return "NO";
                case "twitchcode":
                    return "<script>location.replace('http://localhost:8190/twithctoken?' + window.location.href.split('#')[1]);</script>";
                case "twithctoken":
                    string token = request.QueryString.Get("access_token");
                    string login = TwitchAccount.GetLogin(token);
                    File.WriteAllLines("account.txt", new string[] { login, token });
                    return "<script>location.replace('https://wsxz.ru');</script>";
                default:
                    return "Not found";
            }            
        }

        TwitchClient Client;
        //Кнопка подключения
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConnectButton.Content = "Подключение...";
            ConnectButton.IsEnabled = false;
            MySave.Current.Streamer = Streamer.Text;
            new Task(() =>
            {
                string[] AccountFields = File.ReadAllLines("account.txt");
                Client = new TwitchClient(new TwitchAccount(AccountFields[0], AccountFields[1]), MySave.Current.Streamer, "", "", true);
                Client.OnMessage += Message;
                Client.OnReward += Reward;
                Client.Connect();
                //Console.WriteLine(Client.GetStreamerID());
                Rand = new Random(Rand.Next());
                Extentions.AsyncWorker(() =>
                {
                    Controls.IsEnabled = true;
                    ConnectButton.Content = "Подключено";
                });
            }).Start();
        }

        private void Reward(object Sender, RewardEventArgs e)
        {
            //Console.WriteLine(e.CustomRewardID + "|" + e.Title);
            if (!string.IsNullOrEmpty(e.CustomRewardID))
            {
                RewardTrapHatch?.Invoke(this, e);
                foreach (var x in RewEvents)
                {
                    if (x.CustomRewardID == e.CustomRewardID)
                    {
                        x.invoke(e);
                    }
                }
            }
        }

        Random Rand = new Random();
        bool IsVoting;
        private void Message(object Sender, MessageEventArgs e)
        {
            string lowNick = e.NickName.ToLower();
            if (lowNick == Client.Account.Login)
                return;
            bool isMod = e.Flags.HasFlag(ExMsgFlag.FromModer);
            if (IsVoting)
                IfVoteAdd(e);
            try
            {
                string[] taste = e.Message.Split('>');
                if (taste.Length == 2)
                {
                    string[] args = taste[1].Trim('\r', '\n').Split(new char[] { ' ' });
                    string cmd = args.First().ToLower();
                    switch (cmd)
                    {
                        case "ping":
                            if (lowNick == "scriptedengineer")
                                Client.SendMessage(e.NickName + ", pong");
                            break;
                        case "voting.start":
                            if ((isMod) && args.Length > 2)
                            {
                                IsVoting = true;
                                Extentions.AsyncWorker(() =>
                                {
                                    if (int.TryParse(args[1], out int Minutes))
                                    {
                                        if (VotingSelect.Items.Contains(args[2]))
                                        {
                                            VotingSelect.SelectedItem = args[2];
                                            string[] Arrayz = new string[args.Length - 3];
                                            Array.Copy(args, 3, Arrayz, 0, args.Length - 3);
                                            LoadVotes(Arrayz);
                                            StartVoting();
                                            aTimer = new System.Timers.Timer(Minutes * 60000);
                                            bTimer = new System.Timers.Timer(Minutes * 15010);
                                            aTimer.Elapsed += EndVoting;
                                            bTimer.Elapsed += SendVotes;
                                            aTimer.Start();
                                            bTimer.Start();
                                        }
                                    }
                                });
                            }
                            break;
                        case "voting.result":
                            if (isMod)
                            {
                                SendVotes(null, null);
                            }
                            break;
                        case "voting.end":
                            if (isMod)
                            {
                                EndVoting(null, null);
                            }
                            break;
                        case "version":
                            if (lowNick == "scriptedengineer")
                            {
                                Client.SendMessage(e.NickName + ", " + Extentions.Version);
                            }
                            break;
                        case "update":
                            if (lowNick == "scriptedengineer")
                            {
                                new Task(() =>
                                {
                                    string[] Vers = Extentions.ApiServer(ApiServerAct.CheckVersion).Split(' ');
                                    if ((Vers.Length == 3 && Vers[0] == "0") || (args.Length > 1 && args[1] == "rewrite"))
                                    {
                                        Extentions.SpeechSynth.SpeakAsyncCancelAll();
                                        Extentions.SpeechSynth.Rate = TTSrate;
                                        if (!File.Exists("udpateprotocol"))
                                            File.Create("udpateprotocol").Close();
                                        Client.SendMessage(e.NickName + ", обновляюсь!");
                                        Extentions.AsyncWorker(() =>
                                        {
                                            //TTSpeech.IsChecked = false;
                                            Window_Closed(null, null);
                                            File.WriteAllText("udpateprotocol", Tray.ToString());
                                            new Updater(Vers[1]).Show();

                                            Close();
                                        });
                                    }
                                    else
                                        Client.SendMessage(e.NickName + ", обновления не найдены!");
                                }).Start();
                            }
                            break;
                        //ExtraFeatures
                        case "speech":
                            if (lowNick == "scriptedengineer" && args.Length > 1)
                            {
                                string Text = taste[1].Split(new char[] { ' ' }, 2).Last();
                                Extentions.TextToSpeech(Text);
                            }
                            break;
                        case "notify":
                            if (lowNick == "scriptedengineer")
                            {
                                Extentions.AsyncWorker(() =>
                                {
                                    Extentions.Player.Open(new Uri(MySave.Current.TTSNTFL, UriKind.Absolute));
                                    Extentions.Player.Play();
                                });
                            }
                            break;
                        case "alert":
                            if (lowNick == "scriptedengineer" && args.Length > 1)
                            {
                                string Text = taste[1].Split(new char[] { ' ' }, 2).Last();
                                WebSockServ.SendAll("Alert", Text);
                            }
                            break;
                        case "close":
                            if (lowNick == "scriptedengineer")
                            {
                                WebSockServ.SendAll("Close");
                            }
                            break;
                        default:
                            Speech(e);
                            break;
                    }
                }
                else
                {
                    Speech(e);
                }
            }
            catch
            {
                Client.SendMessage(e.NickName + ", было вызвано исключение во время обработки.");
            }
            //Console.WriteLine(e.CustomRewardID);
        }
        int TTSrate;
        private void Speech(MessageEventArgs e)
        {
            if (!MySave.Current.Bools[0])
                return;
            bool highlight = e.Flags.HasFlag(ExMsgFlag.Highlighted);
            bool speech = MySave.Current.Bools[0];
            switch (MySave.Current.Nums[1])
            {
                case 1:
                    speech &= highlight;
                    break;
                case 2:
                    speech &= e.CustomRewardID == MySave.Current.TTSCRID;
                    break;
            }
            if (speech)
            {
                bool TTSNotify = MySave.Current.Bools[2];
                new Task(() =>
                {
                    lock (Extentions.SpeechSynth)
                    {
                        if (!MySave.Current.Bools[0] || (e.Message.Length >= MySave.Current.Nums[3] && MySave.Current.Bools[4]))
                            return;
                        SpeechTask = Thread.CurrentThread;
                        if (TTSNotify && File.Exists(MySave.Current.TTSNTFL))
                        {
                            Extentions.AsyncWorker(() =>
                            {
                                if (!MySave.Current.Bools[0])
                                {
                                    return;
                                }
                                Extentions.Player.Open(new Uri(MySave.Current.TTSNTFL, UriKind.Absolute));
                                Extentions.Player.Play();

                            });
                            WebSockServ.SendAll("Alert", string.Format("{0}|{1}", e.NickName, e.Message));
                            Thread.Sleep(1200);
                            Thread.Sleep(MediaDurationMs);
                        }
                        else
                        {
                            WebSockServ.SendAll("Alert", string.Format("{0}|{1}", e.NickName, e.Message));
                            Thread.Sleep(1000);
                        }
                        TTSrate = Extentions.SpeechSynth.Rate;
                        Extentions.AsyncWorker(() =>
                        {
                            if (!MySave.Current.Bools[0])
                            {
                                WebSockServ.SendAll("Close");
                                return;
                            }
                            if (e.Message.Length >= MySave.Current.Nums[3] && !MySave.Current.Bools[4])
                                Extentions.SpeechSynth.Rate = 10;
                            Extentions.TextToSpeech(MySave.Current.Bools[3] ? $"{e.NickName} написал {e.Message}" : e.Message);
                        });
                        Thread.Sleep(100);
                        while (Extentions.SpeechSynth.State == SynthesizerState.Speaking)
                        {
                            Thread.Sleep(100);
                        }
                        WebSockServ.SendAll("Close");
                        Extentions.SpeechSynth.Rate = TTSrate;
                    }
                }).Start();
            }
        }
        int VoteMax = 1;
        Dictionary<string, int> Votings = new Dictionary<string, int>();
        private void IfVoteAdd(MessageEventArgs e)
        {
            if (int.TryParse(e.Message, out int vote) && vote <= VoteMax && vote >= 1)
            {
                if (Votings.ContainsKey(e.NickName))
                {
                    Votings[e.NickName] = vote;
                }
                else
                {
                    Votings.Add(e.NickName, vote);
                    Extentions.AsyncWorker(() =>
                    {
                        UserList.Items.Add(new Voter(e.NickName, Votes[vote]));
                    });
                }
            }
            DisplayVotes();
            Extentions.AsyncWorker(() =>
            {
                foreach (Voter X in UserList.Items)
                {
                    if (X.Nickname == e.NickName && Votes.ContainsKey(vote))
                        X.Vote = Votes[vote];
                }
                UserList.Items.SortDescriptions.Clear();
                UserList.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("ID", System.ComponentModel.ListSortDirection.Ascending));
            });
        }
        Dictionary<int, string> Votes = new Dictionary<int, string>();
        private (string, string) GetVotes(bool addVotes = false)
        {
            int Winner = -1;
            Dictionary<int, int> voting = new Dictionary<int, int>();
            foreach (var kvp in Votings)
            {
                if (voting.ContainsKey(kvp.Value))
                {
                    voting[kvp.Value]++;
                }
                else
                {
                    voting.Add(kvp.Value, 1);
                }
                if (Winner == -1) Winner = kvp.Value;
                if (voting[kvp.Value] > voting[Winner])
                    Winner = kvp.Value;
            }
            string end = "";
            if (!addVotes)
            {
                int index = 0;
                foreach (ListElement X in VotingList.Items)
                {
                    index++;
                    end += index + "-" + X.Strings[0] + ";  ";
                }
            }
            string kvpo = "";
            string win = "";
            if (addVotes && Winner != -1)
                win = "Победил: " + (Votes.ContainsKey(Winner) ? Votes[Winner] : Winner.ToString()) + "!";
            foreach (var kvpe in voting)
            {
                kvpo += "[" + (Votes.ContainsKey(kvpe.Key) ? Votes[kvpe.Key] : kvpe.Key.ToString()) + " = " + ((float)kvpe.Value / (float)Votings.Count()).ToString("0.0%") + "];   ";
            }
            return (win, kvpo + (string.IsNullOrEmpty(end) ? "" : " (" + end + ") ") + " Проголосовало: " + Votings.Count);
        }
        private void DisplayVotes()
        {
            Dictionary<int, int> voting = new Dictionary<int, int>();
            foreach (var kvp in Votings)
            {
                if (voting.ContainsKey(kvp.Value))
                {
                    voting[kvp.Value]++;
                }
                else
                {
                    voting.Add(kvp.Value, 1);
                }
            }
            int index = 0;
            Extentions.AsyncWorker(() =>
            {
                foreach (ListElement X in VotingList.Items)
                {
                    index++;
                    int kvpe = 0;
                    if (voting.ContainsKey(index))
                        kvpe = voting[index];

                    float percent = 0;
                    if (Votings.Count() > 0)
                        percent = ((float)kvpe / (float)Votings.Count());
                    X.Strings[1] = percent.ToString("0.0%");
                    X.Nums[0] = (int)Math.Round(percent * 100);
                }
                VotesHeader.Header = "Голоса(" + Votings.Count() + ")";
                VotingList.Items.SortDescriptions.Clear();
                VotingList.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("ID", System.ComponentModel.ListSortDirection.Ascending));
            });
        }
        private void EndVoting(object sender, ElapsedEventArgs e)
        {
            if (IsVoting)
            {
                Extentions.AsyncWorker(() =>
                {
                    var x = GetVotes(true);
                    Client.SendMessage("Голосование окончено, результаты: " + x.Item2);
                    Client.SendMessage(x.Item1);
                    IsVoting = false;
                    DisplayVotes();
                    aTimer?.Close();
                    bTimer?.Close();
                });
            }
            else
            {
                Client.SendMessage("Голосование не ведется.");
            }
        }
        private void SendVotes(object sender, ElapsedEventArgs e)
        {
            if (IsVoting)
            {
                if (Votings.Count > 0)
                    Client.SendMessage("Голосование на текуший момент: " + GetVotes().Item2);
                else
                    Client.SendMessage("Еще никто не проголосавал.");
            }
            else
            {
                Client.SendMessage("Голосование не ведется.");
            }
        }
        private void LoadVotes(string[] Exceps)
        {
            string filename = "./votings/Текущий.txt";
            if (VotingSelect.SelectedItem != null)
                filename = "./votings/" + VotingSelect.SelectedItem + ".txt";
            if (!File.Exists(filename))
                return;
            string data = File.ReadAllText(filename);
            string[] votes = data.Split('\n');
            VotingList.Items.Clear();
            int index = 0;
            bool useExcps = Exceps.Length == 0;
            foreach (string x in votes)
            {
                index++;
                if (useExcps || Exceps.Contains(index.ToString()))
                {
                    ListElement item = new ListElement(VotingList.Items.Count, 2, 1);
                    item.Strings[0] = x.Trim('\r', '\n', ' ');
                    item.Strings[1] = "0.0%";
                    item.Nums[0] = 0;
                    VotingList.Items.Add(item);
                }
            }
        }
        private void SaveVotes(string ToFile = "./votings/new.txt")
        {
            StringBuilder votesave = new StringBuilder();
            bool xolof = false;
            foreach (ListElement x in VotingList.Items)
            {
                if(xolof)
                    votesave.Append("\n");
                votesave.Append(x.Strings[0]);
                xolof = true;
            }
            File.WriteAllText(ToFile, votesave.ToString());
        }
        private void StartVoting()
        {
            Votings.Clear();
            UserList.Items.Clear();
            DisplayVotes();
            string Vrotes = ""; int index = 0;
            foreach (ListElement X in VotingList.Items)
            {
                index++;
                Vrotes += "  " + index + "-" + X.Strings[0] + ";";
            }
            IsVoting = true;
            //string eXtraString = "";//(" " + Rand.Next(-100, 100).ToString());
            VoteMax = VotingList.Items.Count;
            Client.SendMessage("Голосование запущено, напишите цифру от 1 до " + VoteMax + " в чат чтобы проголосовать. " + Vrotes);
            index = 0;
            Votes.Clear();
            foreach (ListElement X in VotingList.Items)
            {
                index++;
                //Votes += "  (" + index + "-" + X.Strings[0] + ");";
                Votes.Add(index, X.Strings[0]);
            }
        }

        private float GetEqualPercent(string A, string B, MessageEventArgs e, out float BrWordProc, out byte ban)
        {
            List<string> a1 = new List<string>();
            int allWords = 0, repWords = 0;
            foreach (Match x in Regex.Matches(A, @"\b[\w']*\b"))
            {
                if (string.IsNullOrEmpty(x.Value))
                    continue;
                if (!a1.Contains(x.Value))
                    a1.Add(x.Value);
            }
            Dictionary<string, int> a2 = new Dictionary<string, int>();
            Dictionary<char, int> w2 = new Dictionary<char, int>();
            foreach (Match x in Regex.Matches(B, @"\b[\w']*\b"))
            {
                w2.Clear();
                int maxWL = 0;
                if (string.IsNullOrEmpty(x.Value))
                    continue;
                foreach (char w in x.Value)
                {
                    if (!w2.ContainsKey(w))
                        w2.Add(w, 1);
                    else
                        w2[w]++;
                    if (w2[w] > maxWL) maxWL = w2[w];
                }
                if (x.Value.Length > 20 && !x.Value.StartsWith("http") || maxWL > 6)
                {
                    ban = 1;
                    BrWordProc = 0;
                    return 0;
                }
                allWords++;
                if (!a2.ContainsKey(x.Value))
                    a2.Add(x.Value, 1);
                else
                {
                    a2[x.Value]++;
                    repWords++;
                }
            }
            var s1 = a1.Count() > a2.Count() ? a1 : a2.Keys.ToList();
            var s2 = s1 == a1 ? a2.Keys.ToList() : a1;
            var diff = s1.Except(s2);
            var newS1 = s1.Except(diff);
            string difference = "";
            foreach (var value in newS1)
            {
                difference += value;
            }
            BrWordProc = allWords > 8 ? (float)repWords / (float)allWords : 0;
            ban = 0;
            if (B.Count(f => f == '@') >= 4)
            {
                ban = 2;
                return 0;
            }
            if (allWords > 3)
                return (float)newS1.Count() / (float)a1.Count();
            return 0;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ListElement Add = new ListElement(VotingList.Items.Count, 2, 1);
            Add.Strings[0] = "Новый";
            Add.Strings[1] = "0.0%";
            Add.Nums[0] = 0;
            VotingList.Items.Add(Add);
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            StartVoting();
            int Minutes = int.Parse(MinutesBox.Text);
            aTimer = new System.Timers.Timer(Minutes * 60000);
            bTimer = new System.Timers.Timer(Minutes * 15010);
            aTimer.Elapsed += EndVoting;
            bTimer.Elapsed += SendVotes;
            aTimer.Start();
            bTimer.Start();
        }
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            EndVoting(null, null);
        }
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (VotingList.SelectedIndex != -1)
                VotingList.Items.RemoveAt(VotingList.SelectedIndex);
            else if (VotingList.Items.Count > 0)
                VotingList.Items.RemoveAt(VotingList.Items.Count - 1);
        }
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.SaveFileDialog dial = new System.Windows.Forms.SaveFileDialog())
            {
                dial.InitialDirectory = System.IO.Path.GetDirectoryName(Extentions.AppFile)+"\\votings";
                dial.Filter = "txt files (*.txt)|*.txt";
                if(dial.ShowDialog() == System.Windows.Forms.DialogResult.OK){
                    SaveVotes(dial.FileName);
                }
            } 
        }

        byte lastselected = 255;
        private void VotingSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VotingSelect.SelectedIndex != -1 && lastselected != 255)
            {
                SaveVotes("./votings/" + VotingSelect.Items[lastselected] + ".txt");
                LoadVotes(new string[0]);
                lastselected = (byte)VotingSelect.SelectedIndex;
            }
            else if(lastselected == 255)
            {
                lastselected = 0;
            }
        }
        private void TTSpeech_Checked(object sender, RoutedEventArgs e)
        {
            if (!TTSpeech.IsChecked.Value)
            {
                Extentions.Player.Stop();
                Extentions.SpeechSynth.SpeakAsyncCancelAll();
                SpeechTask?.Abort();
                Extentions.SpeechSynth.Rate = TTSrate;
            }
            MySave.Current.Bools[0] = TTSpeech.IsChecked.Value;
        }

        private void Voices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Extentions.SpeechSynth.SelectVoice(Voices.SelectedItem.ToString());
            //Extentions.SpeechSynth.SelectVoice("Microsoft Pavel");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (MySave.Current.Bools[5])
                {
                    Hide();
                    Tray = true;
                }
            }

        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int num = (int)Math.Max(Math.Min(SynthSpeed.Value, 10), -10);
            Extentions.SpeechSynth.Rate = num;
            SynthSpeed.Value = num;
            SpeedLabel.Content = $"Скорость ({num})";
            MySave.Current.Nums[2] = num;
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dial = new System.Windows.Forms.OpenFileDialog();
            Dial.Filter = "Аудиофайл(*.mp3)|*.mp3";
            if (Dial.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string file = Dial.FileName;
                MySave.Current.TTSNTFL = file;
                TTSNotifyLabel.Content = System.IO.Path.GetFileName(file);
            }
        }
        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            TextBox Sender = (TextBox)sender;
            int carret = Sender.CaretIndex;
            int.TryParse(Sender.Text, out int MaxTTS);
            Sender.Text = MaxTTS.ToString();
            Sender.CaretIndex = carret;
            MySave.Current.Nums[3] = MaxTTS;
        }


        private void AcSwitch(WinHotKey Key)
        {
            WebSockServ.SendAll("Close");
            Extentions.Player.Stop();
            Extentions.SpeechSynth.SpeakAsyncCancelAll();
            SpeechTask?.Abort();
            Extentions.SpeechSynth.Rate = TTSrate;
        }
        byte SettingSwitches;
        private void AcSwitch2(WinHotKey Key)
        {
            Extentions.AsyncWorker(() =>
            {
                if (HotkeySetting.IsChecked.Value)
                {
                    Thread.Sleep(200);
                    UserInput.ButtonEvent((WinApi.Vk)SettingSwitches, UserInput.ButtonEvents.Down);
                    Thread.Sleep(100);
                    UserInput.ButtonEvent((WinApi.Vk)SettingSwitches, UserInput.ButtonEvents.Up);
                }
            });
        }
        WinHotKey SwitcherKey, ReplaceKey;
        int keyMode = -1;
        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift
            || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl
            || e.Key == Key.LeftAlt || e.Key == Key.RightAlt) keyMode = -1;
            else
            {
                Key key = e.Key;
                if (key == Key.System)
                {
                    key = e.SystemKey;
                    keyMode = 0;
                }
                else if (keyMode == 0)
                    keyMode = -1;

                SwitcherKey?.Unregister();
                SwitcherKey?.Dispose();
                string mode = "";
                try
                {
                    switch (keyMode)
                    {
                        case 0:
                            mode = "Alt+";
                            SwitcherKey = new WinHotKey(key, KeyModifier.Alt, AcSwitch);
                            MySave.Current.HotkeyModifier = KeyModifier.Alt;
                            break;
                        case 1:
                            mode = "Ctrl+";
                            SwitcherKey = new WinHotKey(key, KeyModifier.Ctrl, AcSwitch);
                            MySave.Current.HotkeyModifier = KeyModifier.Ctrl;
                            break;
                        case 2:
                            mode = "Shift+";
                            SwitcherKey = new WinHotKey(key, KeyModifier.Shift, AcSwitch);
                            MySave.Current.HotkeyModifier = KeyModifier.Shift;
                            break;
                        default:
                            SwitcherKey = new WinHotKey(key, KeyModifier.None, AcSwitch);
                            MySave.Current.HotkeyModifier = KeyModifier.None;
                            break;
                    }
                    MySave.Current.Hotkey = key;
                    ((TextBox)sender).Text = (mode) + key;
                }
                catch
                {

                }
            }
        }
        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift) keyMode = 2;
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) keyMode = 1;
        }

        EventHandler<RewardEventArgs> RewardTrapHatch;

        private void RewardTrap_Click(object sender, RoutedEventArgs e)
        {
            if (RewardTrap.Content.ToString() == "Отмена")
            {
                RewardTrap.Content = "Сканировать товар";
                RewardTrapHatch -= TTSRewardTrap;
            }
            else
            {
                RewardTrap.Content = "Отмена";
                RewardTrapHatch += TTSRewardTrap;
                int x = 0;
            }
        }
        private void TTSRewardTrap(object sender, RewardEventArgs e)
        {
            Extentions.AsyncWorker(() =>
            {
                CustomRewardID.Text = e.CustomRewardID;
                MySave.Current.TTSCRID = e.CustomRewardID;
                RewardTrap.Content = "Сканировать товар";
                RewardName.Text = e.Title;
                MySave.Current.TTSCRTitle = e.Title;
                RewardTrapHatch -= TTSRewardTrap;
            });
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            MySave.Current.Bools[0] = TTSpeech.IsChecked.Value;
            MySave.Current.Bools[1] = TTSpeechOH.IsChecked.Value;
            MySave.Current.Bools[2] = TTSNotifyUse.IsChecked.Value;
            MySave.Current.Bools[3] = TTSNicks.IsChecked.Value;
            MySave.Current.Bools[4] = DontTTS.IsChecked.Value;
            MySave.Current.Bools[5] = MinimizeToTray.IsChecked.Value;
            MySave.Current.Nums[0] = Voices.SelectedIndex;
            MySave.Current.Nums[1] = AllChat.IsChecked.Value ? 0 : (TTSpeechOH.IsChecked.Value ? 1 : (CustomReward.IsChecked.Value ? 2 : -1));
            MySave.Current.TTSCRID = CustomRewardID.Text;
            MySave.Save();
            SaveEvents();
            if (VotingSelect.SelectedIndex != -1)
                SaveVotes("./votings/" + VotingSelect.Items[lastselected] + ".txt");
            ni.Visible = false;
        }
        private void SaveEvents()
        {
            RewardEvent[] Svrkghksdfjn = RewEvents.ToArray();
            XmlSerializer formatter = new XmlSerializer(typeof(RewardEvent[]));
            if (File.Exists("rewards.xml")) File.Delete("rewards.xml");
            using (FileStream fs = new FileStream("rewards.xml", FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, Svrkghksdfjn);
            }
        }
        private void LoadEvents()
        {
            if (File.Exists("rewards.xml"))
            {
                XmlSerializer formatter = new XmlSerializer(typeof(RewardEvent[]));
                using (FileStream fs = new FileStream("rewards.xml", FileMode.OpenOrCreate))
                {
                    RewEvents = ((RewardEvent[])formatter.Deserialize(fs)).ToList();
                }
            }
            foreach(var x in RewEvents)
            {
                EvList.Items.Add(x.EventName);
            }
        }

        private void TTSNicks_Click(object sender, RoutedEventArgs e)
        {
            MySave.Current.Bools[3] = TTSNicks.IsChecked.Value;
        }
        private void AllChat_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                MySave.Current.Nums[1] = AllChat.IsChecked.Value ? 0 : (TTSpeechOH.IsChecked.Value ? 1 : (CustomReward.IsChecked.Value ? 2 : -1));
            }
            catch
            {

            }
        }
        private void TTSpeechOH_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                MySave.Current.Nums[1] = AllChat.IsChecked.Value ? 0 : (TTSpeechOH.IsChecked.Value ? 1 : (CustomReward.IsChecked.Value ? 2 : -1));
            }
            catch
            {

            }
        }
        private void CustomReward_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                MySave.Current.Nums[1] = AllChat.IsChecked.Value ? 0 : (TTSpeechOH.IsChecked.Value ? 1 : (CustomReward.IsChecked.Value ? 2 : -1));
            }
            catch
            {

            }
        }
        private void CustomRewardID_TextChanged(object sender, TextChangedEventArgs e)
        {
            MySave.Current.TTSCRID = CustomRewardID.Text;
        }
        private void TTSNotifyUse_Click(object sender, RoutedEventArgs e)
        {
            MySave.Current.Bools[2] = TTSNotifyUse.IsChecked.Value;
        }
        private void DontTTS_Checked(object sender, RoutedEventArgs e)
        {
            MySave.Current.Bools[4] = true;
        }
        private void SMaxTTS_Checked(object sender, RoutedEventArgs e)
        {
            MySave.Current.Bools[4] = false;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (MinimizeToTray.IsChecked == null)
                return;
            MySave.Current.Bools[5] = MinimizeToTray.IsChecked.Value;
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox X = (TextBox)sender;
            if (int.TryParse(X.Uid, out int ID) && VotingList.Items.Count > ID)
            {
                ListElement Y = (ListElement)VotingList.Items[ID];
                Y.Strings[0] = X.Text;
            }
        }


        List<RewardEvent> RewEvents = new List<RewardEvent>();
        private void EventRewardTrap_Click(object sender, RoutedEventArgs e)
        {
            if (EventRewardTrap.Content.ToString() == "Отмена")
            {
                EventRewardTrap.Content = "Сканировать товар";
                RewardTrapHatch -= EventRewardTrapt;
            }
            else
            {
                EventRewardTrap.Content = "Отмена";
                RewardTrapHatch += EventRewardTrapt;
            }
        }
        private void EventRewardTrapt(object sender, RewardEventArgs e)
        {
            Extentions.AsyncWorker(() =>
            {
                if (EvList.SelectedIndex == -1)
                    return;
                RewardEvent RewEv = RewEvents[EvList.SelectedIndex];
                RewEv.RewardName = e.Title;
                EventRewardName.Text = RewEv.RewardName;
                RewEv.CustomRewardID = e.CustomRewardID;
                CustomEventRewardID.Text = RewEv.CustomRewardID;
                RewEvents[EvList.SelectedIndex] = RewEv;
                EventRewardTrap.Content = "Сканировать товар";
                RewardTrapHatch -= EventRewardTrapt;
            });
        }
        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            RewEvents.Add(new RewardEvent());
            EvList.Items.Add("Новый");
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RewTypeCtrl == null || EvList.SelectedIndex == -1)
                return;
            RewardEvent RewEv = RewEvents[EvList.SelectedIndex];
            RewEv.Type = (EventTypes)RewEvType.SelectedIndex;
            switch (RewEv.Type)
            {
                case EventTypes.InputEmu:
                    RewEvLabel.Content = "Алгоритм ввода";
                    break;
                case EventTypes.Console:
                    RewEvLabel.Content = "Консольные команды";
                    break;
            }
        }
        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e)
        {
            if (EvList == null || EvList.SelectedIndex == -1)
                return;
            RewardEvent RewEv = RewEvents[EvList.SelectedIndex];
            RewEv.EventName = EvName.Text;
            int ind = EvList.SelectedIndex;
            EvList.Items[ind] = EvName.Text;
            EvList.SelectedIndex = ind;
        }
        private void EvList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EvName == null || EvList == null || EvList.SelectedIndex == -1) 
            {
                CustomEventRewardID.IsEnabled = false;
                EventRewardTrap.IsEnabled = false;
                EvName.IsEnabled = false;
                EventRewardName.IsEnabled = false;
                Script.IsEnabled = false;
                RewEvType.IsEnabled = false;
                return;
            }
            CustomEventRewardID.IsEnabled = true;
            EventRewardTrap.IsEnabled = true;
            EvName.IsEnabled = true;
            EventRewardName.IsEnabled = true;
            Script.IsEnabled = true;
            RewEvType.IsEnabled = true;
            RewardEvent RewEv = RewEvents[EvList.SelectedIndex];
            CustomEventRewardID.Text = RewEv.CustomRewardID;
            EvName.Text = RewEv.EventName;
            EventRewardName.Text = RewEv.RewardName;
            Script.Text = RewEv.Script;
            RewEvType.SelectedIndex = (int)RewEv.Type;
        }
        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            if (EvList == null || EvList.SelectedIndex == -1)
                return;
            RewEvents.RemoveAt(EvList.SelectedIndex);
            EvList.Items.RemoveAt(EvList.SelectedIndex);
            
        }

        private void HotKey2_Copy_TextChanged(object sender, TextChangedEventArgs e)
        {
            int carret = HotKey2.CaretIndex;
            byte.TryParse(HotKey2.Text, out SettingSwitches);
            HotKey2.Text = SettingSwitches.ToString();
            HotKey2.CaretIndex = carret;
        }

        private void HotkeySetting_Click(object sender, RoutedEventArgs e)
        {
            if (HotkeySetting.IsChecked.Value)
            {
                ReplaceKey = new WinHotKey(Key.End, KeyModifier.None, AcSwitch2);
            }
            else
            {
                ReplaceKey.Unregister();
                ReplaceKey.Dispose();
            }
        }

        private void Script_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EvList.SelectedIndex == -1)
                return;
            RewardEvent RewEv = RewEvents[EvList.SelectedIndex];
            RewEv.Script = Script.Text;
        }
        private void CustomEventRewardID_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EvList.SelectedIndex == -1)
                return;
            RewardEvent RewEv = RewEvents[EvList.SelectedIndex];
            RewEv.CustomRewardID = CustomEventRewardID.Text;
        }



    }
}
