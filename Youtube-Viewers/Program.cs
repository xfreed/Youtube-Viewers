using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using Leaf.xNet;
using Youtube_Viewers.Helpers;
using HttpRequest = Leaf.xNet.HttpRequest;
using HttpResponse = Leaf.xNet.HttpResponse;

namespace Youtube_Viewers
{
    internal class Program
    {
        private static string _id;
        private static int _threadsCount;

        private static int _pos;

        private static ProxyQueue _scraper;
        private static ProxyType _proxyType;
        private static bool _updateProxy;

        private static int _botted;
        private static int _errors;

        private static string _viewers = "Connecting";
        private static string _title = "Connecting";

        public static string[] Urls =
        {
            "https://raw.githubusercontent.com/clarketm/proxy-list/master/proxy-list-raw.txt",
            "https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/socks4.txt",
            "https://api.proxyscrape.com/?request=getproxies&proxytype=socks4&timeout=9000&ssl=yes",
            "https://api.proxyscrape.com/v2/?request=getproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all"
        };

        private const string Intro = @"
$$\     $$\ $$$$$$$$\ $$\    $$\ $$\                                             
\$$\   $$  |\__$$  __|$$ |   $$ |\__|                                            
 \$$\ $$  /    $$ |   $$ |   $$ |$$\  $$$$$$\  $$\  $$\  $$\  $$$$$$\   $$$$$$\  
  \$$$$  /     $$ |   \$$\  $$  |$$ |$$  __$$\ $$ | $$ | $$ |$$  __$$\ $$  __$$\ 
   \$$  /      $$ |    \$$\$$  / $$ |$$$$$$$$ |$$ | $$ | $$ |$$$$$$$$ |$$ |  \__|
    $$ |       $$ |     \$$$  /  $$ |$$   ____|$$ | $$ | $$ |$$   ____|$$ |      
    $$ |       $$ |      \$  /   $$ |\$$$$$$$\ \$$$$$\$$$$  |\$$$$$$$\ $$ |      
    \__|       \__|       \_/    \__| \_______| \_____\____/  \_______|\__|      
";

        private const string GitRepo = "https://github.com/xfreed/Youtube-Viewers";

        [STAThread]
        private static void Main()
        {
            if (!File.Exists("proxy_url.txt")) File.AppendAllText("proxy_url.txt", string.Join("\r\n", Urls));

            Console.Title = $"YTViewer | {GitRepo}";
            Logo(ConsoleColor.Cyan);

            _id = Dialog("Enter Video ID");
            _threadsCount = Convert.ToInt32(Dialog("Enter Threads Count (Comfort 2000)"));

            while (true)
            {
                Logo(ConsoleColor.Cyan);

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Select proxy type: \n1. Http`s \n2. Socks \n3. Socks5");

                Console.Write("Your choice: ");
                Console.ForegroundColor = ConsoleColor.Cyan;

                var k = Console.ReadKey().KeyChar;

                try
                {
                    var key = int.Parse(k.ToString());
                    switch (key)
                    {
                        case 1:
                            _proxyType = ProxyType.HTTP;
                            break;
                        case 2:
                            _proxyType = ProxyType.Socks4;
                            break;
                        case 3:
                            _proxyType = ProxyType.Socks5;
                            break;
                        default:
                            throw new Exception();
                    }
                }
                catch
                {
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"\r\nSelected {_proxyType} proxy");

                break;
            }

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Update proxies by urls?:\r\n1. Yes\r\n2. No");

                Console.Write("Your choice: ");

                var k = Console.ReadKey().KeyChar;

                try
                {
                    var pt = int.Parse(k.ToString());
                    switch (pt)
                    {
                        case 1:
                            _updateProxy = true;
                            break;

                        case 2:
                            break;

                        default:
                            throw new Exception();
                    }
                }
                catch
                {
                    continue;
                }

                break;
            }

            reProxy:
            if (_updateProxy)
            {
                Urls = File.ReadAllText("proxy_url.txt").Trim().Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);
                Console.WriteLine("\nProxy links: \n" + string.Join("\r\n", Urls));
                Console.WriteLine("You can set your own links in 'proxy_url.txt' file");

                var totalProxies = string.Empty;

                using (var req = new HttpRequest
                {
                    ConnectTimeout = 3000
                })
                {
                    foreach (var proxyUrl in Urls)
                    {
                        Console.ResetColor();
                        Console.Write($"Downloading proxies from '{proxyUrl}': ");
                        {
                            try
                            {
                                totalProxies += req.Get(proxyUrl) + "\r\n";
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Success");
                                Console.ResetColor();
                            }
                            catch(Exception e)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Error: \n {e.Message}");
                                Console.ResetColor();
                            }
                        }
                    }
                }

                if (totalProxies.Length == 0)
                {
                    MessageBox.Show("Couldn't update proxies by url. You will have to do manually");
                    _updateProxy = false;
                    goto reProxy;
                }

                _scraper = new ProxyQueue(totalProxies, _proxyType);
            }
            else
            {
                Console.WriteLine("Select proxy list");

                var dialog = new OpenFileDialog {Filter = "Proxy list (*.txt)|*.txt"};

                if (dialog.ShowDialog() != DialogResult.OK) return;

                _scraper = new ProxyQueue(File.ReadAllText(dialog.FileName), _proxyType);
            }

            Console.WriteLine($"\nLoaded {_scraper.Length} proxies");

            Logo(ConsoleColor.Green);

            var threads = new List<Thread>();

            var logWorker = new Thread(Log);
            logWorker.Start();
            threads.Add(logWorker);

            if (_updateProxy)
            {
                var proxyWorker = new Thread(ProxyUpdater);
                proxyWorker.Start();
                threads.Add(proxyWorker);
            }

            for (var i = 0; i < _threadsCount; i++)
            {
                var t = new Thread(Worker);
                t.Start();
                threads.Add(t);
            }

            foreach (var t in threads)
                t.Join();

            Console.ReadKey();
        }

        private static string Dialog(string question)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{question}: ");
            Console.ForegroundColor = ConsoleColor.Cyan;

            var val = Console.ReadLine()?.Trim();

            Logo(ConsoleColor.Cyan);
            return val;
        }

        private static void ProxyUpdater()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var sec = 600;
            while (true)
            {
                if (stopwatch.ElapsedTicks * 10 >= sec)
                {
                    var proxies = string.Empty;
                    foreach (var proxyUrl in Urls)
                        using (var req = new HttpRequest())
                        {
                            try
                            {
                                proxies += req.Get(proxyUrl) + "\r\n";
                            }
                            catch
                            {
                                // ignore
                            }
                        }

                    _scraper.SafeUpdate(proxies);
                    sec += 600;
                }

                Thread.Sleep(1000);
            }
        }

        private static void Logo(ConsoleColor color)
        {
            Console.Clear();

            Console.ForegroundColor = color;
            Console.WriteLine(Intro);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("GitHub: ");

            Console.ForegroundColor = color;
            Console.WriteLine(GitRepo);

            _pos = Console.CursorTop;
        }

        private static void Log()
        {
            while (true)
            {
                Console.SetCursorPosition(0, _pos);
                Console.WriteLine(
                    $"Success connections: {_botted}\n" +
                    $"Errors: {_errors}\n" +
                    $"Proxies: {_scraper.Length}\n" +
                    $"Threads: {_threadsCount}\n" +
                    $"Title: {_title}{new string(' ', Console.WindowWidth - _title.Length)}\r\n" +
                    $"\nViewers: {_viewers}{new string(' ', Console.WindowWidth - _viewers.Length)}\r\n"
                );
                Thread.Sleep(250);
            }
        }

        private static string BuildUrl(Dictionary<string, string> args)
        {
            return args.Aggregate("https://s.youtube.com/api/stats/watchtime?", (current, arg) => current + $"{arg.Key}={arg.Value}&");
        }

        private static void Worker()
        {
            var random = new Random();

            while (true)
            {
                try
                {
                    using (var req = new HttpRequest
                    {
                        Proxy = _scraper.Next()
                    })
                    {

                        req.UserAgentRandomize();

                        HttpResponse res = req.Get($"https://www.youtube.com/watch?v={_id}");

                        var sres = res.ToString();
                        var viewersTemp = string.Join("",
                            RegularExpressions.Viewers.Match(sres).Groups[1].Value.Where(char.IsDigit));

                        if (!string.IsNullOrEmpty(viewersTemp))
                            _viewers = viewersTemp;

                        _title = RegularExpressions.Title.Match(sres).Groups[1].Value;

                        var url = RegularExpressions.ViewUrl.Match(sres).Groups[1].Value;
                        url = url.Replace(@"\u0026", "&").Replace("%2C", ",").Replace(@"\/", "/");

                        var query = HttpUtility.ParseQueryString(url);

                        var cl = query.Get(query.AllKeys[0]);
                        var ei = query.Get("ei");
                        var of = query.Get("of");
                        var vm = query.Get("vm");

                        var buffer = new byte[100];

                        random.NextBytes(buffer);

                        var cpn = RegularExpressions.Trash.Replace(Convert.ToBase64String(buffer), "").Substring(0, 16);

                        var st = random.Next(1000, 10000);
                        var et = st + random.Next(200, 700);

                        var rt = random.Next(10, 200);

                        var lact = random.Next(1000, 8000);
                        var rtn = rt + 300;

                        var args = new Dictionary<string, string>
                        {
                            ["ns"] = "yt",
                            ["el"] = "detailpage",
                            ["cpn"] = cpn,
                            ["docid"] = _id,
                            ["ver"] = "2",
                            ["cmt"] = et.ToString(),
                            ["ei"] = ei,
                            ["fmt"] = "243",
                            ["fs"] = "0",
                            ["rt"] = rt.ToString(),
                            ["of"] = of,
                            ["euri"] = "",
                            ["lact"] = lact.ToString(),
                            ["live"] = "dvr",
                            ["cl"] = cl,
                            ["state"] = "playing",
                            ["vm"] = vm,
                            ["volume"] = "100",
                            ["cbr"] = "Firefox",
                            ["cbrver"] = "83.0",
                            ["c"] = "WEB",
                            ["cplayer"] = "UNIPLAYER",
                            ["cver"] = "2.20201210.01.00",
                            ["cos"] = "Windows",
                            ["cosver"] = "10.0",
                            ["cplatform"] = "DESKTOP",
                            ["delay"] = "5",
                            ["hl"] = "en_US",
                            ["rtn"] = rtn.ToString(),
                            ["aftm"] = "140",
                            ["rti"] = rt.ToString(),
                            ["muted"] = "0",
                            ["st"] = st.ToString(),
                            ["et"] = et.ToString()
                        };

                        var urlToGet = BuildUrl(args);

                        req.AcceptEncoding = "gzip, deflate";
                        req.AddHeader("Host", "www.youtube.com");

                        req.Get(urlToGet);
                        Interlocked.Increment(ref _botted);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref _errors);
                }

                Thread.Sleep(1);
            }
        }
    }
}