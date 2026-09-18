using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BMS上位机
{
    /// <summary>
    /// 极简本地 HTTP/SSE 桥接服务：把 BMS 实时数据提供给展会演示网页。
    /// 用 TcpListener 手写 HTTP，免管理员权限 / 免 URL ACL。
    /// 路由：GET /          → 演示网页(太阳能充电系统工作原理演示.html)
    ///       GET /api/bms   → 最新数据 JSON（轮询用）
    ///       GET /events    → SSE 实时推送
    /// 电流单位与上位机界面一致（mA）；SOC 为 %。
    /// </summary>
    public static class BmsBridge
    {
        private static TcpListener _listener;
        private static Thread _accept;
        private static volatile bool _running;
        private static volatile string _json = "{\"ok\":false}";
        private static int _port = 8712;
        private static string _pagePath;      // 命中的页面路径缓存

        public static int Port { get { return _port; } }
        public static bool Running { get { return _running; } }

        /// <summary>启动服务；端口被占用时自动向后顺延。</summary>
        public static void Start(int port)
        {
            if (_running) return;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    TcpListener l = new TcpListener(IPAddress.Loopback, port + i);
                    l.Start();
                    _listener = l;
                    _port = port + i;
                    break;
                }
                catch { }
            }
            if (_listener == null) return;
            _running = true;
            _accept = new Thread(AcceptLoop);
            _accept.IsBackground = true;
            _accept.Start();
        }

        public static void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); }
            catch { }
            _listener = null;
        }

        /// <summary>
        /// 上位机每次解析完 BMS 数据后调用。
        /// 电流单位 = mA（与界面显示一致）；SOC=%。
        /// </summary>
        public static void Update(double solar, double mains, double load,
                                  double soc, double rsoc, double fcc,
                                  double vpack, double psolar, double pload,
                                  double temp, double tOn, double tOff)
        {
            long ts = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            StringBuilder sb = new StringBuilder(224);
            sb.Append('{');
            sb.Append("\"ts\":").Append(ts.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"t\":\"").Append(DateTime.Now.ToString("HH:mm:ss")).Append("\",");
            sb.Append("\"unit\":\"mA\",");
            sb.Append("\"solar\":").Append(Num(solar)).Append(',');   // 光伏电流（≥0）
            sb.Append("\"mains\":").Append(Num(mains)).Append(',');   // 市电电流（≥0）
            sb.Append("\"load\":").Append(Num(load)).Append(',');     // 负载电流（上位机的"电流(Current)"字段）
            sb.Append("\"soc\":").Append(Num(soc)).Append(',');
            sb.Append("\"rsoc\":").Append(Num(rsoc)).Append(',');     // 剩余容量(mAh)
            sb.Append("\"fcc\":").Append(Num(fcc)).Append(',');       // 满充容量(mAh)
            sb.Append("\"vpack\":").Append(Num(vpack)).Append(',');
            sb.Append("\"psolar\":").Append(Num(psolar)).Append(',');
            sb.Append("\"pload\":").Append(Num(pload)).Append(',');
            sb.Append("\"temp\":").Append(Num(temp)).Append(',');     // 电池温度 ℃（多路 NTC 取最小值，供加热毯判定）
            sb.Append("\"tOn\":").Append(Num(tOn)).Append(',');       // BMS 设定：开始加热温度 ℃
            sb.Append("\"tOff\":").Append(Num(tOff));                // BMS 设定：停止加热温度 ℃
            sb.Append('}');
            _json = sb.ToString();
        }

        private static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client = null;
                try { client = _listener.AcceptTcpClient(); }
                catch { if (!_running) break; Thread.Sleep(50); continue; }

                TcpClient c = client;
                Thread t = new Thread(delegate () { Handle(c); });
                t.IsBackground = true;
                t.Start();
            }
        }

        private static void Handle(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                NetworkStream ns = client.GetStream();

                string requestLine = ReadLine(ns);
                if (string.IsNullOrEmpty(requestLine)) return;
                string line;                                   // 丢弃其余请求头
                while (!string.IsNullOrEmpty(line = ReadLine(ns))) { }

                string path = "/";
                string[] parts = requestLine.Split(' ');
                if (parts.Length >= 2) path = parts[1];
                int q = path.IndexOf('?');
                if (q >= 0) path = path.Substring(0, q);

                if (path == "/api/bms" || path == "/api/data") { ServeJson(ns); return; }
                if (path == "/events" || path == "/sse") { ServeSse(ns); return; }
                if (path == "/" || path == "/index.html") { ServePage(ns); return; }
                ServeText(ns, "404 Not Found", "not found");
            }
            catch { }
            finally { try { client.Close(); } catch { } }
        }

        private static string ReadLine(NetworkStream ns)
        {
            List<byte> buf = new List<byte>(128);
            int b = -1;
            while (buf.Count < 8192 && (b = ns.ReadByte()) != -1)
            {
                if (b == '\n') break;
                if (b != '\r') buf.Add((byte)b);
            }
            if (buf.Count == 0 && b == -1) return null;
            return Encoding.ASCII.GetString(buf.ToArray());
        }

        private static void ServeJson(NetworkStream ns)
        {
            byte[] body = Encoding.UTF8.GetBytes(_json);
            WriteResponse(ns, "200 OK", "application/json; charset=utf-8", body);
        }

        private static void ServeText(NetworkStream ns, string status, string text)
        {
            byte[] body = Encoding.UTF8.GetBytes(text);
            WriteResponse(ns, status, "text/plain; charset=utf-8", body);
        }

        private static void ServePage(NetworkStream ns)
        {
            string p = ResolvePage();
            if (p == null)
            {
                byte[] msg = Encoding.UTF8.GetBytes(
                    "未找到演示页。请把 太阳能充电系统工作原理演示.html 放在 exe 同级目录，或工程根目录下的“太阳能充电演示”文件夹内。");
                WriteResponse(ns, "404 Not Found", "text/plain; charset=utf-8", msg);
                return;
            }
            byte[] body = File.ReadAllBytes(p);       // 每次读取，改完刷新即生效
            WriteResponse(ns, "200 OK", "text/html; charset=utf-8", body);
        }

        private static void WriteResponse(NetworkStream ns, string status, string contentType, byte[] body)
        {
            string head = "HTTP/1.1 " + status + "\r\n"
                + "Content-Type: " + contentType + "\r\n"
                + "Content-Length: " + body.Length + "\r\n"
                + "Access-Control-Allow-Origin: *\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(head);
            ns.Write(hb, 0, hb.Length);
            if (body.Length > 0) ns.Write(body, 0, body.Length);
            ns.Flush();
        }

        private static void ServeSse(NetworkStream ns)
        {
            string head = "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/event-stream; charset=utf-8\r\n"
                + "Cache-Control: no-store\r\n"
                + "Access-Control-Allow-Origin: *\r\n"
                + "Connection: keep-alive\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(head);
            ns.Write(hb, 0, hb.Length);
            ns.Flush();
            try
            {
                while (_running)
                {
                    byte[] data = Encoding.UTF8.GetBytes("data: " + _json + "\n\n");
                    ns.Write(data, 0, data.Length);
                    ns.Flush();
                    Thread.Sleep(150);
                }
            }
            catch { }
        }

        /// <summary>在 exe 同级 / 工程根目录等处查找演示页（优先新版「太阳能充电系统工作原理演示.html」）。</summary>
        private static string ResolvePage()
        {
            if (_pagePath != null && File.Exists(_pagePath)) return _pagePath;

            string exe = AppDomain.CurrentDomain.BaseDirectory;
            string[] names = { "太阳能充电系统工作原理演示.html", "index.html" };
            List<string> dirs = new List<string>();
            dirs.Add(Path.Combine(exe, "太阳能充电演示"));
            dirs.Add(Path.Combine(exe, "web"));
            dirs.Add(exe);
            try
            {
                DirectoryInfo d = new DirectoryInfo(exe);
                for (int i = 0; i < 6 && d != null; i++)
                {
                    d = d.Parent;
                    if (d == null) break;
                    dirs.Add(Path.Combine(d.FullName, "太阳能充电演示"));
                    dirs.Add(d.FullName);
                }
            }
            catch { }

            List<string> cands = new List<string>();
            foreach (string dir in dirs)
                foreach (string nm in names)
                    cands.Add(Path.Combine(dir, nm));

            foreach (string c in cands)
            {
                try { if (File.Exists(c)) { _pagePath = c; return c; } }
                catch { }
            }
            return null;
        }
    }
}
