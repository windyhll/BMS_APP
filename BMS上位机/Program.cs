using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BMS上位机
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            BmsBridge.Start(8712);                     // 启动本地桥接服务（供演示网页取实时数据）
            try { Application.Run(new Form1()); }
            finally { BmsBridge.Stop(); }
        }
    }
}
