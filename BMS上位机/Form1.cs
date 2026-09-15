using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Ports;
using System.Threading;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.HPSF;
using NPOI.XSSF.UserModel;
using NPOI.SS.Util;
using NPOI.SS.Formula.Functions;
using System.Timers;
using SysTimer = System.Timers.Timer;
using FileConvert;
using System.Collections;
using System.Diagnostics;

//using mscorlib.dll;


///> 2025.06.16 15.16 V2.1 解决30转15s最低电压0 BUG

namespace BMS上位机
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            this.Load += new EventHandler(Form1_Load);
        }
        [DllImport("kernel32.dll",
            CallingConvention = CallingConvention.Winapi)]
        extern static int GetTickCount();
        public bool[] m_bFaut = new bool[32];
        public byte m_afeConf;
        public byte m_afeStatus1;
        public byte m_afeStatus2;
        public byte m_afeStatus3;
        public byte m_afeFlag1;
        public byte m_afeFlag2;
        public string xslFilePath;
        public string bmsStyle;
        int alarm32 = 0;
        int chgFlg = 0;
        int dsgFlg = 0;
        int pwmmosFlg = 0;
        int mainmosFlg = 0;
        //bool m_readBatt = false;
        int m_cellNum = 20;
        int m_cellNumTrue = 4;
        int m_ntcNum = 2;
        int m_commTimeout = 0;
        int m_factorTimeout = 0;
        public const int READ_CMD = 0xa5;
        public const int WRITE_CMD = 0x5a;
        public const int READ_BATT_DATA_CMD = 0x01;
        public const int READ_AFE_E2_CMD = 0x02;
        public const int READ_MCU_E2_CMD = 0x23;
        public const int READ_UART_BAUD_CMD = 0x04;
        public const int READ_PARA_CMD = 0x05;
        public const int WRITE_AFE_E2_CMD = 0x20;
        public const int WRITE_MCU_E2_CMD = 0x32;
        public const int WRITE_UART_BAUD_CMD = 0x10;
        public const int WRITE_PARA_CMD = 0x55;
        public const int CALI_VOLTTEMP_CMD = 0x40;
        public const int CALI_ZEROCURR_CMD = 0x50;
        public const int CALI_CHGCURR_CMD = 0x60;
        public const int CALI_PWM_CHGCURR_CMD = 0x90;
        public const int CALI_DSGCURR_CMD = 0x70;
        public const int CALI_SOC_CMD = 0x80;
        int mcuE2num = 153;
        public int ptr = 0;
        byte g_display_test = 0;
        byte g_para_first = 0;
        int g_bat_low_index = 0;
        int g_bat_max_index = 0;

        int Four_Bytes_Read(byte[] data, int ptr)
        {
            return (data[ptr + 3] << 24 | data[ptr + 2] << 16 | data[ptr + 1] << 8 | data[ptr]);
        }

        int Two_Bytes_Read(byte[] data, int ptr)
        {
            return (data[ptr + 1] << 8 | data[ptr]);
        }

        int Two_Bytes_Read_temp(byte[] data, int ptr)
        {
            return ((data[ptr + 1] << 8 | data[ptr]) - 2731) / 10;
        }

        private void Four_Bytes_Write(byte[] data, int ptr, int div, string txt)
        {
            int tmp = int.Parse(txt) / div;
            data[ptr] = (byte)tmp;
            data[ptr + 1] = (byte)(tmp >> 8);
            data[ptr + 2] = (byte)(tmp >> 16);
            data[ptr + 3] = (byte)(tmp >> 24);
        }

        private void Two_Bytes_Write(byte[] data, int ptr, int div, string txt)
        {
            data[ptr] = (byte)(UInt16.Parse(txt) / div);
            data[ptr + 1] = (byte)((UInt16.Parse(txt) / div) >> 8);
        }

        private void Two_Bytes_Write_Index(byte[] data, int ptr, int index)
        {
            data[ptr] = (byte)(index);
            data[ptr + 1] = (byte)(index >> 8);
        }

        private void Two_Bytes_Write_Temp(byte[] data, int ptr, string txt)
        {
            data[ptr] = (byte)((int)(double.Parse(txt) * 10) + 2731);
            data[ptr + 1] = (byte)((int)((double.Parse(txt) * 10) + 2731) >> 8);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            Form2 f2 = new Form2();
            f2.ShowDialog();
            if (f2.DialogResult == DialogResult.OK)
            {
                bmsStyle = f2.str;
            }
            this.MaximizeBox = false;
            if ("4S" == bmsStyle)
            {
                m_cellNumTrue = 4;
                m_ntcNum = 2;
            }
            if ("XRT-19S" == bmsStyle)
            {
                m_cellNum = 19;
                m_ntcNum = 3;
            }
            if ("XRT-8S-CAN" == bmsStyle)
            {
                m_cellNum = 8;
                m_ntcNum = 5;
            }
            if ("XRT-20S" == bmsStyle)
            {
                m_cellNum = 20;
                m_ntcNum = 5;
                groupBox3.Visible = true;
                groupBox4.Visible = true;
            }
            if ("JALEI-17-20S" == bmsStyle)
            {
                m_cellNum = 20;
                m_ntcNum = 3;
                groupBox3.Visible = true;
                groupBox4.Visible = true;
            }
            if ("XRT-30S" == bmsStyle)
            {
                m_cellNum = 30;
                m_ntcNum = 5;
            }
            if ("XRT-30S-TO-15S" == bmsStyle)
            {
                m_cellNum = 15;
                m_ntcNum = 5;
            }
            if ("XRT-19S-TEST" == bmsStyle)
            {
                m_cellNum = 6;
                m_ntcNum = 3;
            }
            if ("XRT-24S" == bmsStyle)
            {
                m_cellNum = 24;
                m_ntcNum = 5;
            }
            if ("JALEI-21S" == bmsStyle)
            {
                m_cellNum = 21;
                m_ntcNum = 3;
                groupBoxID.Visible = true;
                groupBox3.Visible = true;
                groupBox4.Visible = true;
                label60.Visible = true;
                buttonMaintOn.Visible = true;
                buttonMaintOff.Visible = true;
            }
            if ("JALEI-24S" == bmsStyle)
            {
                m_cellNum = 24;
                m_ntcNum = 3;
                groupBoxID.Visible = true;
                groupBox3.Visible = true;
                groupBox4.Visible = true;
                label60.Visible = true;
                buttonMaintOn.Visible = true;
                buttonMaintOff.Visible = true;
                label61.Visible = true;
                buttonPreDsgOn.Visible = true;
                buttonPreDsgOff.Visible = true;
            }
            if ("XRT-24S-TO-19S" == bmsStyle)
            {
                m_cellNum = 19;
                m_ntcNum = 5;
            }

            //
            //串口初始化
            //
            string[] commPorts = System.IO.Ports.SerialPort.GetPortNames();
            System.Array.Sort(commPorts);
            cboPortName.Items.AddRange(commPorts);
            cboPortName.SelectedIndex = commPorts.Length - 1;
            //serialPort1.ReadTimeout = 500;     //读取数据的超时时间，引发ReadExisting异常
            //serialPort1.WriteTimeout = 500;
            //serialPort1.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);
            if (serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            serialPort1.PortName = cboPortName.Text;
            serialPort1.BaudRate = 9600;
            serialPort1.Parity = System.IO.Ports.Parity.None;
            serialPort1.DataBits = 8;
            serialPort1.StopBits = System.IO.Ports.StopBits.One;
            serialPort1.WriteBufferSize = 4096;

            //波特率
            comboBAUD.Items.Add("1200");
            comboBAUD.Items.Add("2400");
            comboBAUD.Items.Add("4800");
            comboBAUD.Items.Add("9600");
            comboBAUD.Items.Add("19200");
            comboBAUD.Items.Add("38400");
            comboBAUD.Items.Add("115200");
            comboBAUD.Text = "9600";

            //BalanEN
            BalanEN.Items.Add("开启");
            BalanEN.Items.Add("关闭");
            BalanEN.Text = "";

            //ChgBalanSelect
            ChgBalanSelect.Items.Add("开启");
            ChgBalanSelect.Items.Add("关闭");
            ChgBalanSelect.Text = "";
     
            //comboSWAlways
            comboSWAlways.Items.Add("开启");
            comboSWAlways.Items.Add("关闭");
            comboSWAlways.Text = "";
            //comboMainFirst
            comboMainFirst.Items.Add("开启");
            comboMainFirst.Items.Add("关闭");
            comboMainFirst.Text = "";
            //comboSOCLEARN
            comboSOCLEARN.Items.Add("开启");
            comboSOCLEARN.Items.Add("关闭");
            comboSOCLEARN.Text = "";

            //comboSleep
            comboSleep.Items.Add("开启");
            comboSleep.Items.Add("关闭");
            comboSleep.Text = "";

            //OccRelease
            OccRelease.Items.Add("开启");
            OccRelease.Items.Add("关闭");
            OccRelease.Text = "";

            //ScRelease
            ScRelease.Items.Add("开启");
            ScRelease.Items.Add("关闭");
            ScRelease.Text = "";

            // OCC
            
            // OCCT
            comboBoxOCCT.Items.Add("140");
            comboBoxOCCT.Items.Add("280");
            comboBoxOCCT.Items.Add("490");
            comboBoxOCCT.Items.Add("980");
            comboBoxOCCT.Items.Add("2030");
            comboBoxOCCT.Items.Add("3010");
            comboBoxOCCT.Items.Add("4970");
            comboBoxOCCT.Items.Add("10010");
            comboBoxOCCT.Text = "";

            // OVT
            comboBoxOVT.Items.Add("140");
            comboBoxOVT.Items.Add("280");
            comboBoxOVT.Items.Add("490");
            comboBoxOVT.Items.Add("980");
            comboBoxOVT.Items.Add("2030");
            comboBoxOVT.Items.Add("3010");
            comboBoxOVT.Items.Add("4970");
            comboBoxOVT.Items.Add("10010");
            comboBoxOVT.Text = "";

            // UVT
            comboBoxUVT.Items.Add("490");
            comboBoxUVT.Items.Add("770");
            comboBoxUVT.Items.Add("980");
            comboBoxUVT.Items.Add("1470");
            comboBoxUVT.Items.Add("2030");
            comboBoxUVT.Items.Add("3010");
            comboBoxUVT.Items.Add("4970");
            comboBoxUVT.Items.Add("10010");
            comboBoxUVT.Text = "";

            // OCD1
            
            // OCD1T
            if ("JALEI-21S" == bmsStyle || "JALEI-24S" == bmsStyle)
            {
                label30.Visible = false;
                label73.Visible = false;
                comboBoxOCD1T.Items.Add("10");
                comboBoxOCD1T.Items.Add("20");
                comboBoxOCD1T.Items.Add("30");
                comboBoxOCD1T.Items.Add("40");
                comboBoxOCD1T.Items.Add("50");
                comboBoxOCD1T.Items.Add("60");
                comboBoxOCD1T.Items.Add("70");
                comboBoxOCD1T.Items.Add("80");
            }
            else
            {
                comboBoxOCD1T.Items.Add("140");
                comboBoxOCD1T.Items.Add("280");
                comboBoxOCD1T.Items.Add("490");
                comboBoxOCD1T.Items.Add("980");
                comboBoxOCD1T.Items.Add("2030");
                comboBoxOCD1T.Items.Add("3010");
                comboBoxOCD1T.Items.Add("4970");
                comboBoxOCD1T.Items.Add("10010");
            }
            comboBoxOCD1T.Text = "";


            // OCD2
            

            // OCD2T
            if ("JALEI-21S" == bmsStyle || "JALEI-24S" == bmsStyle)
            {
                label30.Visible = false;
                label73.Visible = false;
                comboBoxOCD2T.Items.Add("5");
                comboBoxOCD2T.Items.Add("10");
                comboBoxOCD2T.Items.Add("15");
                comboBoxOCD2T.Items.Add("20");
                comboBoxOCD2T.Items.Add("25");
                comboBoxOCD2T.Items.Add("30");
                comboBoxOCD2T.Items.Add("35");
                comboBoxOCD2T.Items.Add("40");
                comboBoxOCD2T.Items.Add("45");
                comboBoxOCD2T.Items.Add("50");
                comboBoxOCD2T.Items.Add("55");
                comboBoxOCD2T.Items.Add("60");
                comboBoxOCD2T.Items.Add("65");
                comboBoxOCD2T.Items.Add("70");
                comboBoxOCD2T.Items.Add("75");
                comboBoxOCD2T.Items.Add("80");
            }
            else
            {
                comboBoxOCD2T.Items.Add("25");
                comboBoxOCD2T.Items.Add("50");
                comboBoxOCD2T.Items.Add("75");
                comboBoxOCD2T.Items.Add("100");
                comboBoxOCD2T.Items.Add("125");
                comboBoxOCD2T.Items.Add("150");
                comboBoxOCD2T.Items.Add("175");
                comboBoxOCD2T.Items.Add("200");
                comboBoxOCD2T.Items.Add("225");
                comboBoxOCD2T.Items.Add("250");
                comboBoxOCD2T.Items.Add("275");
                comboBoxOCD2T.Items.Add("300");
                comboBoxOCD2T.Items.Add("325");
                comboBoxOCD2T.Items.Add("350");
                comboBoxOCD2T.Items.Add("375");
                comboBoxOCD2T.Items.Add("400");
            }
            comboBoxOCD2T.Text = "";

            //Vchg_th
            Vchg_th.Items.Add("192.5");
            Vchg_th.Items.Add("330");
            Vchg_th.Items.Add("467.5");
            Vchg_th.Items.Add("605");
            Vchg_th.Items.Add("742.5");
            Vchg_th.Items.Add("880");
            Vchg_th.Items.Add("1017.5");
            Vchg_th.Items.Add("1155");
            Vchg_th.Text = "";

            //Vdsg_th
            Vdsg_th.Items.Add("0.125");
            Vdsg_th.Items.Add("0.250");
            Vdsg_th.Items.Add("0.375");
            Vdsg_th.Items.Add("0.500");
            Vdsg_th.Items.Add("0.625");
            Vdsg_th.Items.Add("0.750");
            Vdsg_th.Items.Add("0.875");
            Vdsg_th.Items.Add("1.000");
            Vdsg_th.Items.Add("1.125");
            Vdsg_th.Items.Add("1.250");
            Vdsg_th.Items.Add("1.375");
            Vdsg_th.Items.Add("1.500");
            Vdsg_th.Items.Add("1.625");
            Vdsg_th.Items.Add("1.750");
            Vdsg_th.Items.Add("1.875");
            Vdsg_th.Items.Add("2.000");

            // SC
            // softSC.Items.Add("x2");
            // softSC.Items.Add("x3");
            // softSC.Items.Add("x4");
            // softSC.Items.Add("x6");
            softSC.Items.Add("2x");
            softSC.Items.Add("3x");
            softSC.Items.Add("4x");
            softSC.Items.Add("6x");
            softSC.Text = "";

            //softSCT
            softSCT.Items.Add("0");
            softSCT.Items.Add("32");
            softSCT.Items.Add("64");
            softSCT.Items.Add("96");
            softSCT.Items.Add("128");
            softSCT.Items.Add("192");
            softSCT.Items.Add("224");
            softSCT.Items.Add("256");
            softSCT.Items.Add("288");
            softSCT.Items.Add("320");
            softSCT.Items.Add("384");
            softSCT.Items.Add("448");
            softSCT.Items.Add("480");
            softSCT.Items.Add("512");
            softSCT.Items.Add("544");
            softSCT.Items.Add("576");
            softSCT.Text = "";

            Vchg_th.Text = "";


            labelChgDsg.Text = "";


            DesignFCC.Text = "";
            FullFCC.Text = "";
            CycleFCC.Text = "";

            Fcc4Volt.Text = "";
            Fcc20Volt.Text = "";
            Fcc40Volt.Text = "";
            Fcc60Volt.Text = "";
            Fcc80Volt.Text = "";
            Fcc100Volt.Text = "";

            SelfDsgRatio.Text = "";
            FullChgCurrTld.Text = "";
            BalanDiffVolt.Text = "";
            SleepDelay.Text = "";
            Resistent.Text = "";

            ManuYear.Text = "";
            ManuMonth.Text = "";
            ManuDay.Text = "";

            SC_Cnt.Text = "";
            OCC_Cnt.Text = "";
            OCD_Cnt.Text = "";
            OV_Cnt.Text = "";
            UV_Cnt.Text = "";
            OTC_Cnt.Text = "";
            UTC_Cnt.Text = "";
            OTD_Cnt.Text = "";
            UTD_Cnt.Text = "";
            CycleCnt.Text = "";

            Display_Clear();

            comInfo.Text = "";
            timeAndDate.Text = "";

            label225.Text = "未连接!";
            label225.ForeColor = Color.Red; ;

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            comInfo.Text = "串口号：" + cboPortName.Text + "，波特率：" + serialPort1.BaudRate + ", 数据位：8";
            timeAndDate.Text = DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss");
            comboBAUD.Enabled = !serialPort1.IsOpen;
            if (tabControl1.SelectedTab.Text == "电池信息" | tabControl1.SelectedTab.Text == "校准")
            {
                if (serialPort1.IsOpen)
                {
                    Read_Batt_Data();
                    if (g_para_first == 0)
                    {
                        Read_McuE2_Data();
                        g_para_first = 1;
                    }
                }
                if (m_factorTimeout > 0)
                {
                    m_factorTimeout--;
                }
                else
                {
                    Factor0.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor1.Enabled = false;
                    Factor2.Enabled = false;
                    Factor3.Enabled = false;
                    Factor4.Enabled = false;
                    Factor5.Enabled = false;
                    Factor6.Enabled = false;
                    Factor7.Enabled = false;
                    Factor8.Enabled = false;
                    Factor9.Enabled = false;
                    Factor10.Enabled = false;
                    Factor11.Enabled = false;
                    Factor12.Enabled = false;
                    Factor13.Enabled = false;
                    Factor14.Enabled = false;
                    Factor15.Enabled = false;
                    Factor1.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor2.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor3.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor4.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor5.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor6.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor7.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor8.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor9.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor10.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor11.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor12.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor13.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor14.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor15.ForeColor = System.Drawing.SystemColors.Highlight;
                }
                if (m_commTimeout > 0)
                {
                    m_commTimeout--;
                    label225.Text = "已连接!";
                    label225.ForeColor = Color.Green;
                }
                else
                {
                    label225.Text = "未连接!";
                    label225.ForeColor = Color.Red;
                    Display_Clear();
                }
            }
            else
            {
                label225.Text = "";
            }
        }

        private void timerWriteExcel_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                // rowsCnt++;
                ISheet sheet = null;
                NPOI.SS.UserModel.IWorkbook workbook = null;
                FileStream fs = null;
                try
                {
                    fs = new FileStream(xslFilePath, FileMode.Open, FileAccess.Read);
                    if (xslFilePath.IndexOf(".xlsx") > 0) // 2007版本
                    {
                        workbook = new NPOI.XSSF.UserModel.XSSFWorkbook(fs);
                    }
                    else if (xslFilePath.IndexOf(".xls") > 0) // 2003版本
                    {
                        workbook = new NPOI.HSSF.UserModel.HSSFWorkbook(fs);
                    }
                    if (workbook == null)
                    {
                        return;
                    }
                    sheet = workbook.GetSheetAt(0);
                    IRow rows = sheet.CreateRow(sheet.LastRowNum + 1/* (sheet.LastRowNum == 0 ? 0 : 1) */);

                    ICellStyle style = workbook.CreateCellStyle();//创建样式
                    //style.VerticalAlignment = VerticalAlignment.Justify;//垂直居中 方法1 
                    style.Alignment = NPOI.SS.UserModel.HorizontalAlignment.CenterSelection;//设置居中 方法2
                    //style.Alignment = HorizontalAlignment.Center;//设置居中 方法3 

                    System.Windows.Forms.Label[] myLabel = new System.Windows.Forms.Label[25];

                    int i = new int();
                    i = 0;

                    //日期
                    rows.CreateCell(i++).SetCellValue(DateTime.Today.ToString("yyyy-MM-dd"));
                    //时间
                    rows.CreateCell(i++).SetCellValue(DateTime.Now.ToString("T"));

                    //总电压
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(vPack.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //电流
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(Current.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //
                    //太阳能电压
                    //
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(vSolar.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //
                    //太阳能电流
                    //
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(iSolar.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //
                    //市电电流
                    //
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(iMainSupply.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //
                    //电压
                    //
                    myLabel[0] = cell_1;
                    myLabel[1] = cell_2;
                    myLabel[2] = cell_3;
                    myLabel[3] = cell_4;
                    myLabel[4] = cell_5;
                    myLabel[5] = cell_6;
                    myLabel[6] = cell_7;
                    myLabel[7] = cell_8;
                    myLabel[8] = cell_9;
                    myLabel[9] = cell_10;
                    myLabel[10] = cell_11;
                    myLabel[11] = cell_12;
                    myLabel[12] = cell_13;
                    myLabel[13] = cell_14;
                    myLabel[14] = cell_15;
                    myLabel[15] = cell_16;
                    myLabel[16] = cell_17;
                    myLabel[17] = cell_18;
                    myLabel[18] = cell_19;
                    myLabel[19] = cell_20;
                    myLabel[20] = cell_21;
                    myLabel[21] = cell_22;
                    myLabel[22] = cell_23;
                    myLabel[23] = cell_24;

                    for (int j = 0; j < m_cellNum; j++)
                    {
                        try
                        {
                            rows.CreateCell(i).SetCellValue(int.Parse(myLabel[j].Text));
                        }
                        catch
                        {
                            rows.CreateCell(i).SetCellValue(0);
                        }
                        i++;
                    }

                    //
                    //温度
                    //
                    myLabel[0] = NTC1;
                    myLabel[1] = NTC2;
                    myLabel[2] = NTC3;
                    myLabel[3] = NTC4;
                    myLabel[4] = NTC5;
                    myLabel[5] = NTC6;
                    myLabel[6] = NTC7;
                    myLabel[7] = NTC8;

                    for (int j = 0; j < m_ntcNum; j++)
                    {
                        try
                        {
                            rows.CreateCell(i).SetCellValue(double.Parse(myLabel[j].Text));
                        }
                        catch
                        {
                            rows.CreateCell(i).SetCellValue(0);
                        }
                        i++;
                    }

                    //平均电压
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(vAve.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //最高电压串数
                    try
                    {
                        rows.CreateCell(i).SetCellValue(g_bat_max_index);
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //最高电压
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(vMax.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //g_bat_low_index g_bat_max_index
                    //最低电压串数
                    try
                    {
                        rows.CreateCell(i).SetCellValue(g_bat_low_index);
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;
                    //最低电压
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(vMin.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //压差
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(vMaxDiff.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //SOC
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(SOC.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //剩余容量
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(RSOC.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //满充容量
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(FCC.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //循环次数
                    try
                    {
                        rows.CreateCell(i).SetCellValue(int.Parse(CYCLE.Text));
                    }
                    catch
                    {
                        rows.CreateCell(i).SetCellValue(0);
                    }
                    i++;

                    //充电MOS
                    rows.CreateCell(i++).SetCellValue(((chgFlg == 1) ? "ON" : "OFF"));

                    //放电MOS
                    rows.CreateCell(i++).SetCellValue(((dsgFlg == 1) ? "ON" : "OFF"));

                    // //MCU MOS
                    // for (int u = 0; u < 2; u++)
                    // {
                    //     rows.CreateCell(i++).SetCellValue(((m_bFaut[u + 15] == true) ? "ON" : "OFF"));
                    // }

                    if ("JTLS1-15S2x-30A11565-11" == bmsStyle)
                    {
                        //m_afeConf
                        rows.CreateCell(i++).SetCellValue("0x" + m_afeConf.ToString("X2"));

                        //m_afeStatus1
                        rows.CreateCell(i++).SetCellValue("0x" + m_afeStatus1.ToString("X2"));

                        //m_afeStatus2
                        rows.CreateCell(i++).SetCellValue("0x" + m_afeStatus2.ToString("X2"));

                        //m_afeStatus3
                        rows.CreateCell(i++).SetCellValue("0x" + m_afeStatus3.ToString("X2"));

                        //m_afeFlag1
                        rows.CreateCell(i++).SetCellValue("0x" + m_afeFlag1.ToString("X2"));

                        //m_afeFlag2
                        rows.CreateCell(i++).SetCellValue("0x" + m_afeFlag2.ToString("X2"));
                    }

                    //报警BIT
                    for (int u = 0; u < 11; u++)
                    {
                        rows.CreateCell(i++).SetCellValue(((m_bFaut[u] == true) ? "1" : "0"));
                    }

                    for (int j = 0; j < i; j++)
                    {
                        rows.GetCell(j).CellStyle = style;
                    }

                    MemoryStream stream = new MemoryStream();
                    workbook.Write(stream);
                    var buf = stream.ToArray();
                    fs = new FileStream(xslFilePath, FileMode.Create, FileAccess.Write);
                    fs.Write(buf, 0, buf.Length);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(null, ex.Message, "信息提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                        fs.Close();
                    }
                    sheet = null;
                    workbook = null;
                }
            }
            else
            {
                checkBoxSaveData.Checked = false;
            }
        }

        private byte BCC_Check(byte[] data)//BCC校验
        {
            byte bccData = 0;
            for (int i = 1; i < data.Length; i++)
            {
                bccData ^= data[i - 1];
            }
            return bccData;
        }

        public static ushort CalculateCRC(byte[] data, int length)//CRC校验
        {
            ushort crc = 0xFFFF;
            // foreach (byte b in data)
            for (int j = 0; j < length; j++)
            {
                crc ^= data[j];
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }

        private void Display_Clear()
        {
            cell_1.Text = "";
            cell_2.Text = "";
            cell_3.Text = "";
            cell_4.Text = "";
            cell_5.Text = "";
            cell_6.Text = "";
            cell_7.Text = "";
            cell_8.Text = "";
            cell_9.Text = "";
            cell_10.Text = "";
            cell_11.Text = "";
            cell_12.Text = "";
            cell_13.Text = "";
            cell_14.Text = "";
            cell_15.Text = "";
            cell_16.Text = "";
            cell_17.Text = "";
            cell_18.Text = "";
            cell_19.Text = "";
            cell_20.Text = "";
            cell_21.Text = "";
            cell_22.Text = "";
            cell_23.Text = "";
            cell_24.Text = "";
            cell_25.Text = "";
            cell_26.Text = "";
            cell_27.Text = "";
            cell_28.Text = "";
            cell_29.Text = "";
            cell_30.Text = "";
            cell_31.Text = "";
            cell_32.Text = "";
            cell_33.Text = "";
            cell_34.Text = "";
            cell_35.Text = "";
            cell_36.Text = "";
            cell_37.Text = "";
            cell_38.Text = "";
            cell_39.Text = "";
            cell_40.Text = "";
            NTC1.Text = "";
            NTC2.Text = "";
            NTC3.Text = "";
            NTC4.Text = "";
            NTC5.Text = "";
            NTC6.Text = "";
            vPack.Text = "";
            vAve.Text = "";
            vMax.Text = "";
            vMin.Text = "";
            vMaxDiff.Text = "";
            Current.Text = "";
            Current.ForeColor = Color.Blue;
            FCC.Text = "";
            RSOC.Text = "";
            SOC.Text = "";
            SOCIN.Text = "";
            CYCLE.Text = "";
            VER.Text = "";
            YEAR.Text = "";
            MONTH.Text = "";
            DATE.Text = "";
            vSolar.Text = "";
            iSolar.Text = "";
            pSolar.Text = "";
            iMainSupply.Text = "";
            pLoad.Text = "";

            Invoke(new MethodInvoker(delegate() { SOCBAR.Value = 0; }));


            BLAN_1.FillColor = Color.Transparent;
            BLAN_1.FillGradientColor = Color.Transparent;

            BLAN_2.FillColor = Color.Transparent;
            BLAN_2.FillGradientColor = Color.Transparent;

            BLAN_3.FillColor = Color.Transparent;
            BLAN_3.FillGradientColor = Color.Transparent;

            BLAN_4.FillColor = Color.Transparent;
            BLAN_4.FillGradientColor = Color.Transparent;

            BLAN_5.FillColor = Color.Transparent;
            BLAN_5.FillGradientColor = Color.Transparent;

            BLAN_6.FillColor = Color.Transparent;
            BLAN_6.FillGradientColor = Color.Transparent;

            BLAN_7.FillColor = Color.Transparent;
            BLAN_7.FillGradientColor = Color.Transparent;

            BLAN_8.FillColor = Color.Transparent;
            BLAN_8.FillGradientColor = Color.Transparent;

            BLAN_9.FillColor = Color.Transparent;
            BLAN_9.FillGradientColor = Color.Transparent;

            BLAN_10.FillColor = Color.Transparent;
            BLAN_10.FillGradientColor = Color.Transparent;

            BLAN_11.FillColor = Color.Transparent;
            BLAN_11.FillGradientColor = Color.Transparent;

            BLAN_12.FillColor = Color.Transparent;
            BLAN_12.FillGradientColor = Color.Transparent;

            BLAN_13.FillColor = Color.Transparent;
            BLAN_13.FillGradientColor = Color.Transparent;

            BLAN_14.FillColor = Color.Transparent;
            BLAN_14.FillGradientColor = Color.Transparent;

            BLAN_15.FillColor = Color.Transparent;
            BLAN_15.FillGradientColor = Color.Transparent;

            BLAN_16.FillColor = Color.Transparent;
            BLAN_16.FillGradientColor = Color.Transparent;

            BLAN_17.FillColor = Color.Transparent;
            BLAN_17.FillGradientColor = Color.Transparent;

            BLAN_18.FillColor = Color.Transparent;
            BLAN_18.FillGradientColor = Color.Transparent;

            BLAN_19.FillColor = Color.Transparent;
            BLAN_19.FillGradientColor = Color.Transparent;

            BLAN_20.FillColor = Color.Transparent;
            BLAN_20.FillGradientColor = Color.Transparent;

            BLAN_21.FillColor = Color.Transparent;
            BLAN_21.FillGradientColor = Color.Transparent;

            BLAN_22.FillColor = Color.Transparent;
            BLAN_22.FillGradientColor = Color.Transparent;

            BLAN_23.FillColor = Color.Transparent;
            BLAN_23.FillGradientColor = Color.Transparent;

            BLAN_24.FillColor = Color.Transparent;
            BLAN_24.FillGradientColor = Color.Transparent;

            BLAN_25.FillColor = Color.Transparent;
            BLAN_25.FillGradientColor = Color.Transparent;

            BLAN_26.FillColor = Color.Transparent;
            BLAN_26.FillGradientColor = Color.Transparent;

            BLAN_27.FillColor = Color.Transparent;
            BLAN_27.FillGradientColor = Color.Transparent;

            BLAN_28.FillColor = Color.Transparent;
            BLAN_28.FillGradientColor = Color.Transparent;

            BLAN_29.FillColor = Color.Transparent;
            BLAN_29.FillGradientColor = Color.Transparent;

            BLAN_30.FillColor = Color.Transparent;
            BLAN_30.FillGradientColor = Color.Transparent;

            BLAN_31.FillColor = Color.Transparent;
            BLAN_31.FillGradientColor = Color.Transparent;

            BLAN_32.FillColor = Color.Transparent;
            BLAN_32.FillGradientColor = Color.Transparent;

            BLAN_33.FillColor = Color.Transparent;
            BLAN_33.FillGradientColor = Color.Transparent;

            BLAN_34.FillColor = Color.Transparent;
            BLAN_34.FillGradientColor = Color.Transparent;

            BLAN_35.FillColor = Color.Transparent;
            BLAN_35.FillGradientColor = Color.Transparent;

            BLAN_36.FillColor = Color.Transparent;
            BLAN_36.FillGradientColor = Color.Transparent;

            BLAN_37.FillColor = Color.Transparent;
            BLAN_37.FillGradientColor = Color.Transparent;

            BLAN_38.FillColor = Color.Transparent;
            BLAN_38.FillGradientColor = Color.Transparent;

            BLAN_39.FillColor = Color.Transparent;
            BLAN_39.FillGradientColor = Color.Transparent;

            BLAN_40.FillColor = Color.Transparent;
            BLAN_40.FillGradientColor = Color.Transparent;

            DSGMOS.FillColor = Color.Transparent;
            DSGMOS.FillGradientColor = Color.Transparent;

            CHGMOS.FillColor = Color.Transparent;
            CHGMOS.FillGradientColor = Color.Transparent;

            // ovalShapePchg.FillColor = Color.Transparent;
            // ovalShapePchg.FillGradientColor = Color.Transparent;


            FautOV.FillColor = Color.Transparent;
            FautOV.FillGradientColor = Color.Transparent;

            FautUV.FillColor = Color.Transparent;
            FautUV.FillGradientColor = Color.Transparent;

            FautOTC.FillColor = Color.Transparent;
            FautOTC.FillGradientColor = Color.Transparent;

            FautUTC.FillColor = Color.Transparent;
            FautUTC.FillGradientColor = Color.Transparent;

            FautOTD.FillColor = Color.Transparent;
            FautOTD.FillGradientColor = Color.Transparent;

            FautUTD.FillColor = Color.Transparent;
            FautUTD.FillGradientColor = Color.Transparent;

            FautOCC.FillColor = Color.Transparent;
            FautOCC.FillGradientColor = Color.Transparent;

            FautOCD.FillColor = Color.Transparent;
            FautOCD.FillGradientColor = Color.Transparent;

            FautSC.FillColor = Color.Transparent;
            FautSC.FillGradientColor = Color.Transparent;

            FautWireOpen.FillColor = Color.Transparent;
            FautWireOpen.FillGradientColor = Color.Transparent;

            FautAFE.FillColor = Color.Transparent;
            FautAFE.FillGradientColor = Color.Transparent;

            FlashErr.FillColor = Color.Transparent;
            FlashErr.FillGradientColor = Color.Transparent;

            FautMOSOT.FillColor = Color.Transparent;
            FautMOSOT.FillGradientColor = Color.Transparent;
        }

        bool Read_Batt_Data() // 读电池信息
        {
            byte[] data = new byte[512];
            byte[] rcvBuf = new byte[1024];
            byte[] tmpBuf = new byte[1024];
            int[] tmp32 = new int[40];
            int temp;
            int max = 0;
            int min = 5000;
            int sum = 0;
            int ave = 0;
            int diff = 0;
            int addr = 0;
            int totalLength = 0;
            int rcvLength = 0;
            bool rcved_num = false;
            int crcLentgh = 0;

            data[0] = 0xcc;                     //start
            data[1] = READ_CMD;                 //read
            data[2] = READ_BATT_DATA_CMD;       //读电池信息
            data[3] = 7;                        //长度
            temp = data[0] + data[1] + data[2] + data[3];
            temp = 0x10000 - temp;
            data[4] = (byte)(temp >> 8);
            data[5] = (byte)temp;
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                totalLength = serialPort1.BytesToRead;
                if (totalLength > 0)
                {
                    for (int i = 0; i < totalLength; i++)
                    {
                        serialPort1.ReadByte();
                    }
                }
                try//send
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(300);
                totalLength = serialPort1.BytesToRead;
                if ("XRT-30S" == bmsStyle || "XRT-30S-TO-15S" == bmsStyle)
                {
                    rcvLength = 105 + 32; // 137
                    if (totalLength >= rcvLength)//数据长度105byte
                    {
                        if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                        {
                            for (int i = 0; i < (totalLength - (rcvLength - 1)); i++)
                            {
                                if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == 0xa5) && (rcvBuf[i + 2] == READ_BATT_DATA_CMD) && (rcvBuf[i + 3] == rcvLength) && (rcvBuf[i + (rcvLength - 1)] == 0x77))
                                {
                                    rcved_num = true;
                                    addr = i;
                                }
                            }
                            if (rcved_num == true)
                            {
                                rcved_num = false;
                                for (int i = 0; i < rcvLength; i++)
                                {
                                    rcvBuf[i] = rcvBuf[i + addr];
                                }
                                temp = 0;
                                for (int i = 0; i < (rcvLength - 3); i++)
                                {
                                    temp += tmpBuf[i];
                                }
                                temp = 0x10000 - temp;
                                if ((((temp >> 8) & 0xff) == tmpBuf[rcvLength - 3]) && ((temp & 0xff) == tmpBuf[rcvLength - 2]))//校验OK
                                {
                                    // System.Windows.Forms.TextBox[] myTextBox = new System.Windows.Forms.TextBox[30];
                                    System.Windows.Forms.Label[] myLabel = new System.Windows.Forms.Label[40];
                                    Microsoft.VisualBasic.PowerPacks.OvalShape[] myShape = new Microsoft.VisualBasic.PowerPacks.OvalShape[40];
                                    m_commTimeout = 3;
                                    //
                                    //电压
                                    //
                                    myLabel[0] = cell_1;
                                    myLabel[1] = cell_2;
                                    myLabel[2] = cell_3;
                                    myLabel[3] = cell_4;
                                    myLabel[4] = cell_5;
                                    myLabel[5] = cell_6;
                                    myLabel[6] = cell_7;
                                    myLabel[7] = cell_8;
                                    myLabel[8] = cell_9;
                                    myLabel[9] = cell_10;
                                    myLabel[10] = cell_11;
                                    myLabel[11] = cell_12;
                                    myLabel[12] = cell_13;
                                    myLabel[13] = cell_14;
                                    myLabel[14] = cell_15;
                                    myLabel[15] = cell_16;
                                    myLabel[16] = cell_17;
                                    myLabel[17] = cell_18;
                                    myLabel[18] = cell_19;
                                    myLabel[19] = cell_20;
                                    myLabel[20] = cell_21;
                                    myLabel[21] = cell_22;
                                    myLabel[22] = cell_23;
                                    myLabel[23] = cell_24;
                                    myLabel[24] = cell_25;
                                    myLabel[25] = cell_26;
                                    myLabel[26] = cell_27;
                                    myLabel[27] = cell_28;
                                    myLabel[28] = cell_29;
                                    myLabel[29] = cell_30;
                                    myLabel[30] = cell_31;
                                    myLabel[31] = cell_32;
                                    myLabel[32] = cell_33;
                                    myLabel[33] = cell_34;
                                    myLabel[34] = cell_35;
                                    myLabel[35] = cell_36;
                                    myLabel[36] = cell_37;
                                    myLabel[37] = cell_38;
                                    myLabel[38] = cell_39;
                                    myLabel[39] = cell_40;
                                    if ("XRT-30S-TO-15S" == bmsStyle) // 2025.06.16 15.40
                                    {
                                        sum = 0;
                                        for (int i = 0; i < m_cellNum; i++)
                                        {
                                            tmp32[i] = rcvBuf[i * 2 + 4] | rcvBuf[i * 2 + 5] << 8;
                                            sum += tmp32[i];
                                            if (max < tmp32[i])
                                            {
                                                max = tmp32[i];
                                            }
                                            if (min > tmp32[i])
                                            {
                                                min = tmp32[i];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        sum = 0;
                                        for (int i = 0; i < 24; i++)
                                        {
                                            tmp32[i] = rcvBuf[i * 2 + 4] | rcvBuf[i * 2 + 5] << 8;
                                            sum += tmp32[i];
                                            if (max < tmp32[i])
                                            {
                                                max = tmp32[i];
                                            }
                                            if (min > tmp32[i])
                                            {
                                                min = tmp32[i];
                                            }
                                        }
                                        for (int i = 0; i < (m_cellNum - 24); i++)
                                        {
                                            tmp32[i + 24] = rcvBuf[i * 2 + 102] | rcvBuf[i * 2 + 103] << 8;
                                            sum += tmp32[i + 24];
                                            if (max < tmp32[i + 24])
                                            {
                                                max = tmp32[i + 24];
                                            }
                                            if (min > tmp32[i + 24])
                                            {
                                                min = tmp32[i + 24];
                                            }
                                        }
                                    }
                                    ave = sum / m_cellNum;
                                    diff = max - min;

                                    for (int i = 0; i < m_cellNum; i++)
                                    {
                                        myLabel[i].Text = tmp32[i].ToString();
                                        // myTextBox[i].Text = tmp32[i].ToString();

                                        //最高电压 86~87
                                        if (tmp32[i] == max)
                                        {
                                            myLabel[i].ForeColor = Color.Red;
                                        }
                                        else if (tmp32[i] == min)
                                        {
                                            myLabel[i].ForeColor = Color.Blue;
                                        }
                                        else
                                        {
                                            myLabel[i].ForeColor = Color.Black;
                                        }
                                    }
                                    //
                                    //温度
                                    //
                                    myLabel[0] = NTC1;
                                    myLabel[1] = NTC2;
                                    myLabel[2] = NTC3;
                                    myLabel[3] = NTC4;
                                    myLabel[4] = NTC5;
                                    myLabel[5] = NTC6;
                                    myLabel[6] = NTC7;
                                    myLabel[7] = NTC8;
                                    for (int i = 0; i < m_ntcNum; i++)
                                    {
                                        temp = rcvBuf[i * 2 + 53];
                                        temp <<= 8;
                                        temp |= rcvBuf[i * 2 + 52];
                                        tmp32[i] = temp;
                                        double[] flt1 = new double[m_ntcNum];
                                        flt1[i] = ((double)tmp32[i] - 2731) / 10;
                                        myLabel[i].Text = flt1[i].ToString("#0.0");
                                    }
                                    //
                                    //电流 68~69
                                    //
                                    temp = (int)((Int16)((rcvBuf[68] | rcvBuf[69] << 8))) * 10;
                                    Current.Text = temp.ToString();
                                    // CaliZeroCurr.Text = temp.ToString();
                                    //
                                    //均衡 70~73
                                    //
                                    myShape[0] = BLAN_1;
                                    myShape[1] = BLAN_2;
                                    myShape[2] = BLAN_3;
                                    myShape[3] = BLAN_4;
                                    myShape[4] = BLAN_5;
                                    myShape[5] = BLAN_6;
                                    myShape[6] = BLAN_7;
                                    myShape[7] = BLAN_8;
                                    myShape[8] = BLAN_9;
                                    myShape[9] = BLAN_10;
                                    myShape[10] = BLAN_11;
                                    myShape[11] = BLAN_12;
                                    myShape[12] = BLAN_13;
                                    myShape[13] = BLAN_14;
                                    myShape[14] = BLAN_15;
                                    myShape[15] = BLAN_16;
                                    myShape[16] = BLAN_17;
                                    myShape[17] = BLAN_18;
                                    myShape[18] = BLAN_19;
                                    myShape[19] = BLAN_20;
                                    myShape[20] = BLAN_21;
                                    myShape[21] = BLAN_22;
                                    myShape[22] = BLAN_23;
                                    myShape[23] = BLAN_24;
                                    myShape[24] = BLAN_25;
                                    myShape[25] = BLAN_26;
                                    myShape[26] = BLAN_27;
                                    myShape[27] = BLAN_28;
                                    myShape[28] = BLAN_29;
                                    myShape[29] = BLAN_30;
                                    myShape[30] = BLAN_31;
                                    myShape[31] = BLAN_32;
                                    myShape[32] = BLAN_33;
                                    myShape[33] = BLAN_34;
                                    myShape[34] = BLAN_35;
                                    myShape[35] = BLAN_36;
                                    myShape[36] = BLAN_37;
                                    myShape[37] = BLAN_38;
                                    myShape[38] = BLAN_39;
                                    myShape[39] = BLAN_40;
                                    temp = ((int)rcvBuf[70] | (int)rcvBuf[71] << 8 | (int)rcvBuf[72] << 16 | rcvBuf[73] << 24);
                                    for (int i = 0; i < m_cellNum; i++)
                                    {
                                        // if (((temp >> i) & (int)0x01) != 0)
                                        if ((temp & (int)0x01 << i) != 0)
                                        {
                                            myShape[i].FillColor = Color.MediumSeaGreen;
                                            myShape[i].FillGradientColor = Color.White;
                                        }
                                        else
                                        {
                                            myShape[i].FillColor = Color.Transparent;
                                            myShape[i].FillGradientColor = Color.Transparent;
                                        }
                                    }

                                    myShape[0] = FautOV;
                                    myShape[1] = FautUV;
                                    myShape[2] = FautWireOpen;
                                    myShape[3] = FautOCD;
                                    myShape[4] = FautOTC;
                                    myShape[5] = FautUTC;
                                    myShape[6] = FautOTD;
                                    myShape[7] = FautUTD;
                                    myShape[8] = FautOCC;
                                    myShape[9] = FautAFE;
                                    myShape[10] = FautSC;
                                    // myShape[11] = FautWireOpen;
                                    //
                                    //开关量 74~77
                                    //
                                    temp = ((int)rcvBuf[74] | (int)rcvBuf[75] << 8 | (int)rcvBuf[76] << 16 | rcvBuf[77] << 24);
                                    alarm32 = temp;
                                    for (int i = 0; i < 11; i++)
                                    {
                                        if ((temp & (int)0x01 << i) != 0)
                                        {
                                            m_bFaut[i] = true;
                                            myShape[i].FillColor = Color.Red;
                                            myShape[i].FillGradientColor = Color.White;
                                        }
                                        else
                                        {
                                            m_bFaut[i] = false;
                                            myShape[i].FillColor = Color.Transparent;
                                            myShape[i].FillGradientColor = Color.Transparent;
                                        }
                                    }
                                    // flash err
                                    if ((temp & (int)0x01 << 13) != 0)
                                    {
                                        FlashErr.FillColor = Color.Red;
                                        FlashErr.FillGradientColor = Color.White;
                                    }
                                    else
                                    {
                                        FlashErr.FillColor = Color.Transparent;
                                        FlashErr.FillGradientColor = Color.Transparent;
                                    }
                                    // mos fault
                                    if ((temp & (int)0x01 << 15) != 0)
                                    {
                                        FautMOSOT.FillColor = Color.Red;
                                        FautMOSOT.FillGradientColor = Color.White;
                                    }
                                    else
                                    {
                                        FautMOSOT.FillColor = Color.Transparent;
                                        FautMOSOT.FillGradientColor = Color.Transparent;
                                    }
                                    if (((temp & (int)0x01 << 11) != 0) && ((temp & (int)0x01 << 12) != 0))
                                    {
                                        labelChgDsg.Text = "边充边放中";
                                    }
                                    else if ((temp & (int)0x01 << 11) != 0)
                                    {
                                        labelChgDsg.Text = "放电中……";
                                    }
                                    else if ((temp & (int)0x01 << 12) != 0)
                                    {
                                        labelChgDsg.Text = "充电中……";
                                    }
                                    else
                                    {
                                        labelChgDsg.Text = "";
                                    }
                                    // chg mos
                                    if ((temp & (int)0x01 << 19) != 0)
                                    {
                                        CHGMOS.FillColor = Color.MediumSeaGreen;
                                        chgFlg = 1;
                                    }
                                    else
                                    {
                                        CHGMOS.FillColor = Color.Red;
                                        chgFlg = 0;
                                    }
                                    // dsg mos
                                    if ((temp & (int)0x01 << 20) != 0)
                                    {
                                        DSGMOS.FillColor = Color.MediumSeaGreen;
                                        dsgFlg = 1;
                                    }
                                    else
                                    {
                                        DSGMOS.FillColor = Color.Red;
                                        dsgFlg = 0;
                                    }

                                    //
                                    //版本号 78
                                    //
                                    double flt;
                                    flt = (double)rcvBuf[78] / 10;
                                    VER.Text = flt.ToString("0.0");
                                    //
                                    //SOC 79
                                    //
                                    temp = rcvBuf[79];
                                    if (temp >= 0 && temp <= 100)
                                    {
                                        Invoke(new MethodInvoker(delegate() { SOCBAR.Value = temp; }));
                                        SOC.Text = temp.ToString();
                                        CaliView_Soc.Text = temp.ToString();
                                        SOCIN.Text = temp.ToString() + "%";
                                    }

                                    //
                                    //电流 80~83
                                    //
                                    temp = rcvBuf[80] | rcvBuf[81] << 8 | rcvBuf[82] << 16 | rcvBuf[83] << 24;
                                    // Current.Text = temp.ToString();
                                    CaliZeroCurr.Text = temp.ToString();



                                    //
                                    //最低电压 
                                    //
                                    // vMin.Text = (Two_Bytes_Read(rcvBuf, 80)).ToString();
                                    vMin.Text = min.ToString();
                                    vMin.ForeColor = Color.Blue;
                                    //
                                    //总电压 
                                    //
                                    vPack.Text = sum.ToString();
                                    //
                                    //复位次数 84~85
                                    //
                                    //textRstSum.Text = (Two_Bytes_Read(rcvBuf, 84)).ToString();

                                    //
                                    //平均电压 
                                    //
                                    vAve.Text = ave.ToString();
                                    //
                                    //最高电压 
                                    //
                                    vMax.Text = max.ToString();
                                    vMax.ForeColor = Color.Red;
                                    //
                                    //压差 
                                    //
                                    vMaxDiff.Text = diff.ToString();
                                    //
                                    //循环次数 90~91
                                    //
                                    CYCLE.Text = (Two_Bytes_Read(rcvBuf, 90)).ToString();
                                    //
                                    //剩余容量 92~93
                                    //
                                    RSOC.Text = (Two_Bytes_Read(rcvBuf, 92) * 10).ToString();
                                    //
                                    //满充容量 94~95
                                    //
                                    FCC.Text = (Two_Bytes_Read(rcvBuf, 94) * 10).ToString();
                                    //
                                    //生产日期
                                    //
                                    DATE.Text = rcvBuf[96].ToString();
                                    MONTH.Text = rcvBuf[97].ToString();
                                    YEAR.Text = (Two_Bytes_Read(rcvBuf, 98)).ToString();
                                    //
                                    //串数
                                    //                           
                                    // CellNum.Text = rcvBuf[100].ToString();
                                    return true;
                                }
                            }
                        }
                    }
                }
                /*
                 * if (totalLength >= rcvLength)//数据长度105byte
                {
                    if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                    {
                        for (int i = 0; i < (totalLength - (rcvLength - 1)); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == 0xa5) && (rcvBuf[i + 2] == READ_BATT_DATA_CMD) && (rcvBuf[i + 3] == rcvLength) && (rcvBuf[i + (rcvLength - 1)] == 0x77))
                            {
                                rcved_num = true;
                                addr = i;
                            }
                        }
                 * */
                else
                {
                    rcvLength = 105 + 24;
                    if (totalLength >= rcvLength)//数据长度105byte
                    {
                        
                        if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                        {
                            for (int i = 0; i < (totalLength - (rcvLength - 1)); i++)
                            {
                                if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == 0xa5) && (rcvBuf[i + 2] == READ_BATT_DATA_CMD) && (rcvBuf[i + rcvLength - 1] == 0x77))
                                {
                                    rcved_num = true;
                                    crcLentgh = rcvBuf[i + 3];
                                    addr = i;
                                }
                            }
                            if (rcved_num == true)
                            {
                                rcved_num = false;
                                for (int i = 0; i < crcLentgh; i++)
                                {
                                    rcvBuf[i] = rcvBuf[i + addr];
                                }
                                temp = 0;
                                for (int i = 0; i < (crcLentgh - 3); i++)
                                {
                                    temp += tmpBuf[i];
                                }
                                temp = 0x10000 - temp;
                                if ((((temp >> 8) & 0xff) == tmpBuf[crcLentgh - 3]) && ((temp & 0xff) == tmpBuf[crcLentgh - 2]))//校验OK
                                {
                                    System.Windows.Forms.TextBox[] myTextBox = new System.Windows.Forms.TextBox[40];
                                    System.Windows.Forms.Label[] myLabel = new System.Windows.Forms.Label[40];
                                    Microsoft.VisualBasic.PowerPacks.OvalShape[] myShape = new Microsoft.VisualBasic.PowerPacks.OvalShape[40];
                                    m_commTimeout = 3;

                                    //
                                    //串数
                                    //                           
                                    textBoxCellNum.Text = rcvBuf[100].ToString();

                                    
                                    m_cellNum = rcvBuf[100];
                                   
                                    //
                                    //电压
                                    //
                                    myLabel[0] = cell_1;
                                    myLabel[1] = cell_2;
                                    myLabel[2] = cell_3;
                                    myLabel[3] = cell_4;
                                    myLabel[4] = cell_5;
                                    myLabel[5] = cell_6;
                                    myLabel[6] = cell_7;
                                    myLabel[7] = cell_8;
                                    myLabel[8] = cell_9;
                                    myLabel[9] = cell_10;
                                    myLabel[10] = cell_11;
                                    myLabel[11] = cell_12;
                                    myLabel[12] = cell_13;
                                    myLabel[13] = cell_14;
                                    myLabel[14] = cell_15;
                                    myLabel[15] = cell_16;
                                    myLabel[16] = cell_17;
                                    myLabel[17] = cell_18;
                                    myLabel[18] = cell_19;
                                    myLabel[19] = cell_20;
                                    myLabel[20] = cell_21;
                                    myLabel[21] = cell_22;
                                    myLabel[22] = cell_23;
                                    myLabel[23] = cell_24;
                                    myLabel[24] = cell_25;
                                    myLabel[25] = cell_26;
                                    myLabel[26] = cell_27;
                                    myLabel[27] = cell_28;
                                    myLabel[28] = cell_29;
                                    myLabel[29] = cell_30;
                                    myLabel[30] = cell_31;
                                    myLabel[31] = cell_32;
                                    myLabel[32] = cell_33;
                                    myLabel[33] = cell_34;
                                    myLabel[34] = cell_35;
                                    myLabel[35] = cell_36;
                                    myLabel[36] = cell_37;
                                    myLabel[37] = cell_38;
                                    myLabel[38] = cell_39;
                                    myLabel[39] = cell_40;

                                    myTextBox[0] = CaliView_cell_1;
                                    myTextBox[1] = CaliView_cell_2;
                                    myTextBox[2] = CaliView_cell_3;
                                    myTextBox[3] = CaliView_cell_4;
                                    myTextBox[4] = CaliView_cell_5;
                                    myTextBox[5] = CaliView_cell_6;
                                    myTextBox[6] = CaliView_cell_7;
                                    myTextBox[7] = CaliView_cell_8;
                                    myTextBox[8] = CaliView_cell_9;
                                    myTextBox[9] = CaliView_cell_10;
                                    myTextBox[10] = CaliView_cell_11;
                                    myTextBox[11] = CaliView_cell_12;
                                    myTextBox[12] = CaliView_cell_13;
                                    myTextBox[13] = CaliView_cell_14;
                                    myTextBox[14] = CaliView_cell_15;
                                    myTextBox[15] = CaliView_cell_16;
                                    myTextBox[16] = CaliView_cell_17;
                                    myTextBox[17] = CaliView_cell_18;
                                    myTextBox[18] = CaliView_cell_19;
                                    myTextBox[19] = CaliView_cell_20;
                                    myTextBox[20] = CaliView_cell_21;
                                    myTextBox[21] = CaliView_cell_22;
                                    myTextBox[22] = CaliView_cell_23;
                                    myTextBox[23] = CaliView_cell_24;
                                    sum = 0;
                                    for (int i = 0; i < m_cellNum; i++)
                                    {
                                        tmp32[i] = rcvBuf[i * 2 + 4] | rcvBuf[i * 2 + 5] << 8;
                                        sum += tmp32[i];
                                        if (max < tmp32[i])
                                        {
                                            max = tmp32[i];
                                        }
                                        if (min > tmp32[i])
                                        {
                                            min = tmp32[i];
                                        }
                                    }

                                    //for (int i = m_cellNumTrue; i < m_cellNum; i++)
                                    //{
                                    //    tmp32[i] = rcvBuf[i * 2 + 4] | rcvBuf[i * 2 + 5] << 8;
                                    //}
                                  
                                    ave = sum / m_cellNum;
                                    diff = max - min;

                                    for (int i = 0; i < m_cellNum; i++)
                                    {
                                        myLabel[i].Text = tmp32[i].ToString();
                                        myTextBox[i].Text = tmp32[i].ToString();

                                        //最高电压 86~87
                                        if (tmp32[i] == max)
                                        {
                                            myLabel[i].ForeColor = Color.Red;
                                            g_bat_max_index = i+1;
                                        }
                                        else if (tmp32[i] == min)
                                        {
                                            myLabel[i].ForeColor = Color.Blue;
                                            g_bat_low_index = i+1;
                                        }
                                        else
                                        {
                                            myLabel[i].ForeColor = Color.Black;
                                        }
                                    
                                     }
                                    if (g_display_test == 1)
                                    {
                                        for (int i = 0; i < 12; i++)
                                        {
                                            tmp32[i + 24] = rcvBuf[i * 2 + 102] | rcvBuf[i * 2 + 103] << 8;
                                            myLabel[i + 24].Text = tmp32[i + 24].ToString();
                                            // myTextBox[i+24].Text = tmp32[i+24].ToString();
                                        }
                                        this.MainMOS.Visible = true;
                                        this.PWMMOS.Visible = true;
                                        this.label15.Visible = true;
                                        this.label31.Visible = true;
                                        if ((rcvBuf[116] & (int)0x01 << 7) == 0x80)
                                        {
                                            
                                            PWMMOS.FillColor = Color.MediumSeaGreen;
                                            pwmmosFlg = 1;
                                        }
                                        else
                                        {
                                            PWMMOS.FillColor = Color.Red;
                                            pwmmosFlg = 0;
                                        }
                                        if ((rcvBuf[117] & (int)0x01 << 2) == 0x04)
                                        {
                                            
                                            MainMOS.FillColor = Color.MediumSeaGreen;
                                            mainmosFlg = 1;
                                        }
                                        else
                                        {
                                            MainMOS.FillColor = Color.Red;
                                            mainmosFlg = 0;
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < 12; i++)
                                        {
                                            
                                            myLabel[i + 24].Text = "";
                                            // myTextBox[i+24].Text = tmp32[i+24].ToString();
                                        }
                                        this.MainMOS.Visible = false;
                                        this.PWMMOS.Visible = false;
                                        this.label15.Visible = false;
                                        this.label31.Visible = false;
                                    }

                                    // dsg mos
                                   
                                    //
                                    //温度
                                    //
                                    myLabel[0] = NTC1;
                                    myLabel[1] = NTC2;
                                    myLabel[2] = NTC3;
                                    myLabel[3] = NTC4;
                                    myLabel[4] = NTC5;
                                    myLabel[5] = NTC6;
                                    myLabel[6] = NTC7;
                                    myLabel[7] = NTC8;
                                    myTextBox[0] = CaliView_NTC1;
                                    myTextBox[1] = CaliView_NTC2;
                                    myTextBox[2] = CaliView_NTC3;
                                    myTextBox[3] = CaliView_NTC4;
                                    myTextBox[4] = CaliView_NTC5;
                                    myTextBox[5] = CaliView_NTC6;
                                    myTextBox[6] = CaliView_NTC7;
                                    myTextBox[7] = CaliView_NTC8;
                                    for (int i = 0; i < m_ntcNum; i++)
                                    {
                                        temp = rcvBuf[i * 2 + 53];
                                        temp <<= 8;
                                        temp |= rcvBuf[i * 2 + 52];
                                        tmp32[i] = temp;
                                        // tmp32[i] = ((int)(rcvBuf[i * 2 + 52] | rcvBuf[i * 2 + 53] << 8) - 2731) / 10;
                                        double[] flt1 = new double[m_ntcNum];
                                        flt1[i] = ((double)tmp32[i] - 2731) / 10;
                                        myLabel[i].Text = flt1[i].ToString("#0.0");
                                        myTextBox[i].Text = flt1[i].ToString("#0.0");
                                    }

                                    temp = rcvBuf[102] | rcvBuf[103] << 8;
                                    vSolar.Text = temp.ToString();
                                    temp = rcvBuf[104] | rcvBuf[105] << 8;
                                    iSolar.Text = temp.ToString();
                                    temp = ((rcvBuf[106] | rcvBuf[107] << 8) + (rcvBuf[108] | rcvBuf[109] << 8) * 65536);
                                    pSolar.Text = temp.ToString();
                                    temp = rcvBuf[110] | rcvBuf[111] << 8;
                                    iMainSupply.Text = temp.ToString();
                                    temp = ((rcvBuf[112] | rcvBuf[113] << 8) + (rcvBuf[114] | rcvBuf[115] << 8) * 65536);
                                    pLoad.Text = temp.ToString();
                                    temp = rcvBuf[122];
                                    if ((temp & 0x01) == 0x01)   //市电优先
                                    {
                                        MainOff.ForeColor = System.Drawing.SystemColors.ControlText;
                                        MainOn.ForeColor = System.Drawing.SystemColors.Highlight;
                                    }
                                    else
                                    {
                                        MainOff.ForeColor = System.Drawing.SystemColors.Highlight;
                                        MainOn.ForeColor = System.Drawing.SystemColors.ControlText;
                                    }
                                    if ((temp & 0x02) == 0x02)   //自锁
                                    {
                                        SwitchOff.ForeColor = System.Drawing.SystemColors.ControlText;
                                        button1.ForeColor = System.Drawing.SystemColors.Highlight;
                                    }
                                    else
                                    {
                                        SwitchOff.ForeColor = System.Drawing.SystemColors.Highlight;
                                        button1.ForeColor = System.Drawing.SystemColors.ControlText;
                                    }
                                    if ((temp & 0x04) == 0x04)   //learn
                                    {
                                        soc_learn_on.ForeColor = System.Drawing.SystemColors.Highlight;
                                        soc_learn_off.ForeColor = System.Drawing.SystemColors.ControlText;
                                    }
                                    else
                                    {
                                        soc_learn_on.ForeColor = System.Drawing.SystemColors.ControlText;
                                        soc_learn_off.ForeColor = System.Drawing.SystemColors.Highlight;
                                    }
                                    if ((temp & 0x10) == 0x10)   //learn
                                    {
                                        chg_auto_off.ForeColor = System.Drawing.SystemColors.Highlight;
                                        chg_auto_on.ForeColor = System.Drawing.SystemColors.ControlText;
                                    }
                                    else
                                    {
                                        chg_auto_off.ForeColor = System.Drawing.SystemColors.ControlText;
                                        chg_auto_on.ForeColor = System.Drawing.SystemColors.Highlight;
                                    }
                                    if ((temp & 0x08) == 0x08)   //learn
                                    {
                                        dsg_auto_off.ForeColor = System.Drawing.SystemColors.Highlight;
                                        dsg_auto_on.ForeColor = System.Drawing.SystemColors.ControlText;
                                    }
                                    else
                                    {
                                        dsg_auto_off.ForeColor = System.Drawing.SystemColors.ControlText;
                                        dsg_auto_on.ForeColor = System.Drawing.SystemColors.Highlight;
                                    }
                                    //
                                    //电流 68~69
                                    //
                                    temp = (int)((Int16)((rcvBuf[68] | rcvBuf[69] << 8))) * 10;
                                    Current.Text = temp.ToString();
                                    // CaliZeroCurr.Text = temp.ToString();
                                    //
                                    //均衡 70~73
                                    //
                                    myShape[0] = BLAN_1;
                                    myShape[1] = BLAN_2;
                                    myShape[2] = BLAN_3;
                                    myShape[3] = BLAN_4;
                                    myShape[4] = BLAN_5;
                                    myShape[5] = BLAN_6;
                                    myShape[6] = BLAN_7;
                                    myShape[7] = BLAN_8;
                                    myShape[8] = BLAN_9;
                                    myShape[9] = BLAN_10;
                                    myShape[10] = BLAN_11;
                                    myShape[11] = BLAN_12;
                                    myShape[12] = BLAN_13;
                                    myShape[13] = BLAN_14;
                                    myShape[14] = BLAN_15;
                                    myShape[15] = BLAN_16;
                                    myShape[16] = BLAN_17;
                                    myShape[17] = BLAN_18;
                                    myShape[18] = BLAN_19;
                                    myShape[19] = BLAN_20;
                                    myShape[20] = BLAN_21;
                                    myShape[21] = BLAN_22;
                                    myShape[22] = BLAN_23;
                                    myShape[23] = BLAN_24;
                                    temp = ((int)rcvBuf[70] | (int)rcvBuf[71] << 8 | (int)rcvBuf[72] << 16 | rcvBuf[73] << 24);
                                    for (int i = 0; i < m_cellNum; i++)
                                    {
                                        // if (((temp >> i) & (int)0x01) != 0)
                                        if ((temp & (int)0x01 << i) != 0)
                                        {
                                            myShape[i].FillColor = Color.MediumSeaGreen;
                                            myShape[i].FillGradientColor = Color.White;
                                        }
                                        else
                                        {
                                            myShape[i].FillColor = Color.Transparent;
                                            myShape[i].FillGradientColor = Color.Transparent;
                                        }
                                    }

                                    myShape[0] = FautOV;
                                    myShape[1] = FautUV;
                                    myShape[2] = FautWireOpen;
                                    myShape[3] = FautOCD;
                                    myShape[4] = FautOTC;
                                    myShape[5] = FautUTC;
                                    myShape[6] = FautOTD;
                                    myShape[7] = FautUTD;
                                    myShape[8] = FautOCC;
                                    myShape[9] = FautAFE;
                                    myShape[10] = FautSC;
                                    // myShape[11] = FautWireOpen;
                                    //
                                    //开关量 74~77
                                    //
                                    temp = ((int)rcvBuf[74] | (int)rcvBuf[75] << 8 | (int)rcvBuf[76] << 16 | rcvBuf[77] << 24);
                                    alarm32 = temp;
                                    for (int i = 0; i < 11; i++)
                                    {
                                        if ((temp & (int)0x01 << i) != 0)
                                        {
                                            m_bFaut[i] = true;
                                            myShape[i].FillColor = Color.Red;
                                            myShape[i].FillGradientColor = Color.White;
                                        }
                                        else
                                        {
                                            m_bFaut[i] = false;
                                            myShape[i].FillColor = Color.Transparent;
                                            myShape[i].FillGradientColor = Color.Transparent;
                                        }
                                    }

                                    if ((temp & (int)0x01 << 13) != 0)
                                    {
                                        FlashErr.FillColor = Color.Red;
                                        FlashErr.FillGradientColor = Color.White;
                                    }
                                    else
                                    {
                                        FlashErr.FillColor = Color.Transparent;
                                        FlashErr.FillGradientColor = Color.Transparent;
                                    }

                                    if ((temp & (int)0x01 << 15) != 0)
                                    {
                                        FautMOSOT.FillColor = Color.Red;
                                        FautMOSOT.FillGradientColor = Color.White;
                                    }
                                    else
                                    {
                                        FautMOSOT.FillColor = Color.Transparent;
                                        FautMOSOT.FillGradientColor = Color.Transparent;
                                    }
                                    if (((temp & (int)0x01 << 11) != 0) && ((temp & (int)0x01 << 12) != 0))
                                    {
                                        labelChgDsg.Text = "边充边放中";
                                    }
                                    else if ((temp & (int)0x01 << 11) != 0)
                                    {
                                        labelChgDsg.Text = "放电中……";
                                    }
                                    else if ((temp & (int)0x01 << 12) != 0)
                                    {
                                        labelChgDsg.Text = "充电中……";
                                    }
                                    else
                                    {
                                        labelChgDsg.Text = "";
                                    }

                                    if ((temp & (int)0x01 << 19) != 0)
                                    {
                                        CHGMOS.FillColor = Color.MediumSeaGreen;
                                        chgFlg = 1;
                                    }
                                    else
                                    {
                                        CHGMOS.FillColor = Color.Red;
                                        chgFlg = 0;
                                    }

                                    if ((temp & (int)0x01 << 20) != 0)
                                    {
                                        DSGMOS.FillColor = Color.MediumSeaGreen;
                                        dsgFlg = 1;
                                    }
                                    else
                                    {
                                        DSGMOS.FillColor = Color.Red;
                                        dsgFlg = 0;
                                    }

                                    //
                                    //版本号 78
                                    //
                                    double flt;
                                    flt = (double)rcvBuf[78] / 10;
                                    VER.Text = flt.ToString("0.0");
                                    //
                                    //SOC 79
                                    //
                                    temp = rcvBuf[79];
                                    if (temp >= 0 && temp <= 100)
                                    {
                                        Invoke(new MethodInvoker(delegate() { SOCBAR.Value = temp; }));
                                        SOC.Text = temp.ToString();
                                        CaliView_Soc.Text = temp.ToString();
                                        SOCIN.Text = temp.ToString() + "%";
                                    }

                                    //
                                    //电流 80~83
                                    //
                                    temp = rcvBuf[80] | rcvBuf[81] << 8 | rcvBuf[82] << 16 | rcvBuf[83] << 24;
                                    // Current.Text = temp.ToString();
                                    CaliZeroCurr.Text = temp.ToString();

                                    if ("JTLS1-15S2x-30A11565-11" == bmsStyle)
                                    {
                                        m_afeConf = rcvBuf[84];
                                        m_afeStatus1 = rcvBuf[85];
                                        m_afeStatus2 = rcvBuf[86];
                                        m_afeStatus3 = rcvBuf[87];
                                        m_afeFlag1 = rcvBuf[88];
                                        m_afeFlag2 = rcvBuf[89];

                                    }


                                    //
                                    //最低电压 
                                    //
                                    // vMin.Text = (Two_Bytes_Read(rcvBuf, 80)).ToString();
                                    vMin.Text = min.ToString();
                                    vMin.ForeColor = Color.Blue;
                                    //
                                    //总电压 
                                    //
                                    vPack.Text = sum.ToString();
                                    //
                                    //复位次数 84~85
                                    //
                                    //textRstSum.Text = (Two_Bytes_Read(rcvBuf, 84)).ToString();

                                    //
                                    //平均电压 
                                    //
                                    vAve.Text = ave.ToString();
                                    //
                                    //最高电压 
                                    //
                                    vMax.Text = max.ToString();
                                    vMax.ForeColor = Color.Red;
                                    //
                                    //压差 
                                    //
                                    vMaxDiff.Text = diff.ToString();
                                    //
                                    //循环次数 90~91
                                    //
                                    CYCLE.Text = (Two_Bytes_Read(rcvBuf, 90)).ToString();
                                    //
                                    //剩余容量 92~93
                                    //
                                    RSOC.Text = (Two_Bytes_Read(rcvBuf, 92) * 10).ToString();
                                    //
                                    //满充容量 94~95
                                    //
                                    FCC.Text = (Two_Bytes_Read(rcvBuf, 94) * 10).ToString();
                                    //
                                    //生产日期
                                    //
                                    DATE.Text = rcvBuf[96].ToString();
                                    MONTH.Text = rcvBuf[97].ToString();
                                    YEAR.Text = (Two_Bytes_Read(rcvBuf, 98)).ToString();
                                    //
                                    //串数
                                    //                           
                                    // textBoxCellNum.Text = rcvBuf[100].ToString();
                                    // if ("JALEI-17-20S" == bmsStyle)
                                    // {
                                    //     m_cellNum = rcvBuf[100];
                                    // }
                                    return true;
                                }
                            }
                        }
                    }
                }

            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Read_McuE2_Data() // 读参数
        {
            byte[] data = new byte[512];
            byte[] rcvBuf = new byte[512];
            byte[] tmpBuf = new byte[512];
            int[] tmp32 = new int[20];
            int temp;
            int addr = 0;
            int totalLength = 0;
            bool rcved_num = false;

            data[0] = 0xcc;                 //start
            data[1] = READ_CMD;             //read
            data[2] = READ_MCU_E2_CMD;      //读MCU E2
            data[3] = 7;                    //长度
            temp = data[0] + data[1] + data[2] + data[3];
            temp = 0x10000 - temp;
            data[4] = (byte)(temp >> 8);
            data[5] = (byte)temp;
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try//send
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(200);
                totalLength = serialPort1.BytesToRead;
                if (totalLength >= mcuE2num)
                {
                    if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                    {
                        for (int i = 0; i < (totalLength - (mcuE2num - 1)); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == 0xa5) && (rcvBuf[i + 2] == READ_MCU_E2_CMD) && (rcvBuf[i + 3] == mcuE2num) && (rcvBuf[i + mcuE2num - 1] == 0x77))
                            {
                                rcved_num = true;
                                addr = i;
                            }
                        }
                        if (rcved_num == true)
                        {
                            for (int i = 0; i < mcuE2num; i++)
                            {
                                rcvBuf[i] = rcvBuf[i + addr];
                            }
                            temp = 0;
                            for (int i = 0; i < (mcuE2num - 3); i++)
                            {
                                temp += rcvBuf[i];
                            }
                            temp = 0x10000 - temp;
                            if ((((temp >> 8) & 0xff) == rcvBuf[mcuE2num - 3]) && ((temp & 0xff) == rcvBuf[mcuE2num - 2]))//校验OK
                            {
                                //设计容量
                                DesignFCC.Text = (Four_Bytes_Read(rcvBuf, 4)).ToString();

                                //满充容量
                                FullFCC.Text = (Four_Bytes_Read(rcvBuf, 8)).ToString();

                                //循环容量
                                CycleFCC.Text = (Four_Bytes_Read(rcvBuf, 12)).ToString();

                                //4%容量电压
                                Fcc4Volt.Text = (Two_Bytes_Read(rcvBuf, 16)).ToString();

                                //20%容量电压
                                Fcc20Volt.Text = (Two_Bytes_Read(rcvBuf, 18)).ToString();

                                //40%容量电压
                                Fcc40Volt.Text = (Two_Bytes_Read(rcvBuf, 20)).ToString();

                                //60%容量电压
                                Fcc60Volt.Text = (Two_Bytes_Read(rcvBuf, 22)).ToString();

                                //80%容量电压
                                Fcc80Volt.Text = (Two_Bytes_Read(rcvBuf, 24)).ToString();

                                //100%容量电压
                                Fcc100Volt.Text = (Two_Bytes_Read(rcvBuf, 26)).ToString();

                                //自放电率
                                SelfDsgRatio.Text = (Two_Bytes_Read(rcvBuf, 28)).ToString();

                                //满充截止电流
                                FullChgCurrTld.Text = (Two_Bytes_Read(rcvBuf, 30)).ToString();

                                //均衡精度
                                BalanDiffVolt.Text = (Two_Bytes_Read(rcvBuf, 32)).ToString();

                                //睡眠延时
                                SleepDelay.Text = (Two_Bytes_Read(rcvBuf, 34)).ToString();

                                //检流电阻
                                Resistent.Text = (Two_Bytes_Read(rcvBuf, 36)).ToString();

                                //负载短路次数
                                SC_Cnt.Text = (Two_Bytes_Read(rcvBuf, 38)).ToString();

                                //充电高温次数
                                OTC_Cnt.Text = (Two_Bytes_Read(rcvBuf, 40)).ToString();

                                //充电过流次数
                                OCC_Cnt.Text = (Two_Bytes_Read(rcvBuf, 42)).ToString();

                                //充电低温次数
                                UTC_Cnt.Text = (Two_Bytes_Read(rcvBuf, 44)).ToString();

                                //放电过流次数
                                OCD_Cnt.Text = (Two_Bytes_Read(rcvBuf, 46)).ToString();

                                //放电高温次数
                                OTD_Cnt.Text = (Two_Bytes_Read(rcvBuf, 48)).ToString();

                                //单节过压次数
                                OV_Cnt.Text = (Two_Bytes_Read(rcvBuf, 50)).ToString();

                                //放电低温次数
                                UTD_Cnt.Text = (Two_Bytes_Read(rcvBuf, 52)).ToString();

                                //单节欠压次数
                                UV_Cnt.Text = (Two_Bytes_Read(rcvBuf, 54)).ToString();

                                //循环次数
                                CycleCnt.Text = (Two_Bytes_Read(rcvBuf, 56)).ToString();

                                //零电流阈值
                                textBoxZeroCurr.Text = (Two_Bytes_Read(rcvBuf, 58)).ToString();

                                //日
                                ManuDay.Text = (rcvBuf[60]).ToString();

                                //月
                                ManuMonth.Text = (rcvBuf[61]).ToString();

                                //年
                                ManuYear.Text = (Two_Bytes_Read(rcvBuf, 62)).ToString();

                                //开关量
                                BalanEN.Text = (((int)(rcvBuf[64]) & 1) > 0) ? "开启" : "关闭";
                                ChgBalanSelect.Text = (((int)(rcvBuf[64]) & 2) > 0) ? "开启" : "关闭";
                                comboSleep.Text = (((int)(rcvBuf[64]) & 4) > 0) ? "开启" : "关闭";
                                OccRelease.Text = (((int)(rcvBuf[64]) & 8) > 0) ? "开启" : "关闭";
                                ScRelease.Text = (((int)(rcvBuf[64]) & 16) > 0) ? "开启" : "关闭";
                                comboMainFirst.Text = (((int)(rcvBuf[64]) & 32) > 0) ? "开启" : "关闭";
                                comboSWAlways.Text = (((int)(rcvBuf[64]) & 64) > 0) ? "开启" : "关闭";
                                comboSOCLEARN.Text = (((int)(rcvBuf[64]) & 128) > 0) ? "开启" : "关闭";
                                // 单节过压
                                softOV.Text = (Two_Bytes_Read(rcvBuf, 66)).ToString();

                                // 单节过压释放
                                softOVR.Text = (Two_Bytes_Read(rcvBuf, 68)).ToString();

                                // 单节过压延时
                                comboBoxOVT.SelectedIndex = (Two_Bytes_Read(rcvBuf, 70));

                                // 单节欠压
                                softUV.Text = (Two_Bytes_Read(rcvBuf, 72)).ToString();

                                // 单节欠压释放
                                softUVR.Text = (Two_Bytes_Read(rcvBuf, 74)).ToString();

                                // 单节欠压延时
                                comboBoxUVT.SelectedIndex = (Two_Bytes_Read(rcvBuf, 76));

                                // 充电高温
                                softOTC.Text = (Two_Bytes_Read_temp(rcvBuf, 78)).ToString();

                                // 充电高温释放
                                softOTCR.Text = (Two_Bytes_Read_temp(rcvBuf, 80)).ToString();

                                // 充电高温延时
                                softOTCT.Text = (Two_Bytes_Read(rcvBuf, 82)).ToString();

                                // 充电低温
                                softUTC.Text = (Two_Bytes_Read_temp(rcvBuf, 84)).ToString();

                                // 充电低温释放
                                softUTCR.Text = (Two_Bytes_Read_temp(rcvBuf, 86)).ToString();

                                // 充电低温延时
                                softUTCT.Text = (Two_Bytes_Read(rcvBuf, 88)).ToString();

                                // 放电高温
                                softOTD.Text = (Two_Bytes_Read_temp(rcvBuf, 90)).ToString();

                                // 放电高温释放
                                softOTDR.Text = (Two_Bytes_Read_temp(rcvBuf, 92)).ToString();

                                // 放电高温延时
                                softOTDT.Text = (Two_Bytes_Read(rcvBuf, 94)).ToString();

                                // 放电低温
                                softUTD.Text = (Two_Bytes_Read_temp(rcvBuf, 96)).ToString();

                                // 放电低温释放
                                softUTDR.Text = (Two_Bytes_Read_temp(rcvBuf, 98)).ToString();

                                // 放电低温延时
                                softUTDT.Text = (Two_Bytes_Read(rcvBuf, 100)).ToString();

                                // 充电过流
                                OCC.Text = (Two_Bytes_Read(rcvBuf, 102)).ToString();

                                // 充电过流释放延时
                                softOCCRT.Text = (Two_Bytes_Read(rcvBuf, 104)).ToString();

                                // 充电过流延时
                                comboBoxOCCT.SelectedIndex = (Two_Bytes_Read(rcvBuf, 106));

                                // 放电过流1
                                OCD1.Text = (Two_Bytes_Read(rcvBuf, 108)).ToString();

                                // 放电过流1释放延时
                                softOCD1RT.Text = (Two_Bytes_Read(rcvBuf, 110)).ToString();

                                // 放电过流1延时
                                comboBoxOCD1T.SelectedIndex = (Two_Bytes_Read(rcvBuf, 112));

                                // 短路
                                softSC.SelectedIndex = (Two_Bytes_Read(rcvBuf, 114));

                                // 短路释放延时
                                softSCRT.Text = (Two_Bytes_Read(rcvBuf, 116)).ToString();

                                // 短路延时
                                softSCT.SelectedIndex = (Two_Bytes_Read(rcvBuf, 118));

                                // MOS高温
                                softMosOT.Text = (Two_Bytes_Read_temp(rcvBuf, 120)).ToString();

                                // MOS高温释放
                                softMosOTR.Text = (Two_Bytes_Read_temp(rcvBuf, 122)).ToString();

                                // MOS高温延时
                                softMosOTT.Text = (Two_Bytes_Read(rcvBuf, 124)).ToString();

                                // 均衡电压
                                BlanVolt.Text = (Two_Bytes_Read(rcvBuf, 126)).ToString();

                                // 充电唤醒
                                Vchg_th.SelectedIndex = (Two_Bytes_Read(rcvBuf, 128));

                                // 放电唤醒
                                //Vdsg_th.SelectedIndex = (Two_Bytes_Read(rcvBuf, 130));


                                // 放电过流2
                                OCD2.Text = (Two_Bytes_Read(rcvBuf, 132)).ToString();

                                // 放电过流2释放延时
                                softOCD2RT.Text = (Two_Bytes_Read(rcvBuf, 134)).ToString();

                                // 放电过流2延时
                                comboBoxOCD2T.SelectedIndex = (Two_Bytes_Read(rcvBuf, 136));

                                //新增
                                //市电切换电压
                                mainVol.Text = (Two_Bytes_Read(rcvBuf, 138)).ToString();

                                //UART ADDR
                                pwmVol.Text = (Two_Bytes_Read(rcvBuf, 140)).ToString();

                                tempStart.Text = ((sbyte)rcvBuf[142]).ToString();

                                tempEnd.Text = ((sbyte)rcvBuf[143]).ToString();

                                LowBatLevel.Text = (Two_Bytes_Read(rcvBuf, 144)).ToString();

                                SleepVol.Text = (Two_Bytes_Read(rcvBuf, 146)).ToString();
                                //低压保护报警
                                lowSocAlarm.Text = (Two_Bytes_Read(rcvBuf, 148)).ToString();
                                return true;
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Read_ID() // 读ID
        {
            byte[] data = new byte[512];
            byte[] rcvBuf = new byte[512];
            byte[] tmpBuf = new byte[512];
            int[] tmp32 = new int[20];
            int temp;
            int addr = 0;
            int totalLength = 0;
            bool rcved_num = false;

            data[0] = 0x01;
            data[1] = 0x03;
            data[2] = 0x03;
            data[3] = 0xe8;
            data[4] = 0;
            data[5] = 0x0d;
            data[6] = 0x04;
            data[7] = 0x7f;

            int m_start = GetTickCount();
            do
            {
                try//send
                {
                    serialPort1.Write(data, 0, 8);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(200);
                totalLength = serialPort1.BytesToRead;
                int idNum = 31;
                if (totalLength >= idNum)
                {
                    if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                    {
                        for (int i = 0; i < (totalLength - (idNum - 1)); i++)
                        {
                            if ((rcvBuf[i] == 0x01) && (rcvBuf[i + 1] == 0x03) && (rcvBuf[i + 2] == 0x1a))
                            {
                                rcved_num = true;
                                addr = i;
                            }
                        }
                        if (rcved_num == true)
                        {
                            for (int i = 0; i < idNum; i++)
                            {
                                rcvBuf[i] = rcvBuf[i + addr];
                            }
                            // temp = 0;
                            // for (int i = 0; i < (idNum - 3); i++)
                            // {
                            //     temp += rcvBuf[i];
                            // }
                            // temp = 0x10000 - temp;
                            ushort crcResult = CalculateCRC(rcvBuf, idNum - 2);
                            ushort crcRead = (ushort)(rcvBuf[30] << 8 | rcvBuf[29]);
                            // if ((((crcResult) & 0xff) == rcvBuf[idNum]) && (((crcResult >> 8) & 0xff) == rcvBuf[idNum - 1]))//校验OK
                            if (crcResult == crcRead)//校验OK
                            {
                                // textBoxIDRD.Text = (Four_Bytes_Read(rcvBuf, 4)).ToString();
                                // for (int i = 0; i < 26; i++)
                                // {
                                //     textBoxIDRD[i].Text = rcvBuf[3 + i].ToString();
                                // }
                                textBoxIDRD.Text = Encoding.UTF8.GetString(rcvBuf, 3, 26);
                                return true;
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Write_McuE2_Data() // 写参数
        {
            // int tmp;
            byte[] data = new byte[200];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 200; i++)
            {
                data[i] = 0;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = WRITE_MCU_E2_CMD;     //MCU EEPROM
            try//赋值
            {
                // if ("XRT-19S" == bmsStyle || "BimMaiSi-16s" == bmsStyle)
                // {
                // 设计容量
                ptr = 4;
                Four_Bytes_Write(data, ptr, 1, DesignFCC.Text);//4

                // 满充容量
                ptr += 4;
                Four_Bytes_Write(data, ptr, 1, FullFCC.Text);//8

                // 循环容量
                ptr += 4;
                Four_Bytes_Write(data, ptr, 1, CycleFCC.Text);//12

                // 4%容量电压
                ptr += 4;
                Two_Bytes_Write(data, ptr, 1, Fcc4Volt.Text);//16

                // 20%容量电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, Fcc20Volt.Text);//18

                // 40%容量电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, Fcc40Volt.Text);//20

                // 60%容量电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, Fcc60Volt.Text);

                // 80%容量电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, Fcc80Volt.Text);

                // 100%容量电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, Fcc100Volt.Text);

                // 自放电率
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, SelfDsgRatio.Text);

                // 满充截止电流
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, FullChgCurrTld.Text);//30

                // 均衡压差
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, BalanDiffVolt.Text);

                // 睡眠延时 (UInt16.Parse(txt)
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, SleepDelay.Text);
                if (UInt16.Parse(SleepDelay.Text) == 9527)
                    g_display_test = 1;
                else
                    g_display_test = 0;
                // 检流电阻
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, Resistent.Text);

                // 负载短路次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, SC_Cnt.Text);

                // 充电高温次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, OTC_Cnt.Text);//40

                // 充电过流次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, OCC_Cnt.Text);

                // 充电低温次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, UTC_Cnt.Text);

                // 放电过流次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, OCD_Cnt.Text);

                // 放电高温次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, OTD_Cnt.Text);

                // 单节过压次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, OV_Cnt.Text);//50

                // 放电低温次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, UTD_Cnt.Text);

                // 单节欠压次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, UV_Cnt.Text);

                // 循环次数
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, CycleCnt.Text);

                // 零电流阈值 
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, textBoxZeroCurr.Text);

                // 生产日期 日
                ptr += 2;
                data[ptr] = (byte)(int.Parse(ManuDay.Text));//60

                // 生产日期 月
                ptr += 1;
                data[ptr] = (byte)(int.Parse(ManuMonth.Text));

                // 生产日期 年
                ptr += 1;
                Two_Bytes_Write(data, ptr, 1, ManuYear.Text);//62

                //开关量
                int tmp = 0;
                tmp |= (BalanEN.Text == "开启") ? 1 : 0;
                tmp |= (ChgBalanSelect.Text == "开启") ? 2 : 0;
                tmp |= (comboSleep.Text == "开启") ? 4 : 0;
                tmp |= (OccRelease.Text == "开启") ? 8 : 0;
                tmp |= (ScRelease.Text == "开启") ? 16 : 0;
                tmp |= (comboMainFirst.Text == "开启") ? 32 : 0;
                tmp |= (comboSWAlways.Text == "开启") ? 64 : 0;
                tmp |= (comboSOCLEARN.Text == "开启") ? 128 : 0;

                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, tmp.ToString());//64~65



                // 单节过压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOV.Text);// 66

                // 单节过压释放
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOVR.Text);

                // 单节过压延时
                ptr += 2;
                // Two_Bytes_Write(data, ptr, 1, comboBoxOVT.SelectedIndex);//70
                Two_Bytes_Write_Index(data, ptr, comboBoxOVT.SelectedIndex);

                // 单节欠压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softUV.Text);

                // 单节欠压释放
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softUVR.Text);
                // Two_Bytes_Write(data, ptr, 1, comboBoxUVT.SelectedIndex);
                // 单节欠压延时
                ptr += 2;
                Two_Bytes_Write_Index(data, ptr, comboBoxUVT.SelectedIndex);
                //Two_Bytes_Write(data, ptr, 1, softUVT.Text);

                // 充电高温
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softOTC.Text);

                // 充电高温释放
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softOTCR.Text);//80

                // 充电高温延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOTCT.Text);

                // 充电低温
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softUTC.Text);

                // 充电低温释放
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softUTCR.Text);

                // 充电低温延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softUTCT.Text);

                // 放电高温
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softOTD.Text);//90

                // 放电高温释放
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softOTDR.Text);

                // 放电高温延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOTDT.Text);

                // 放电低温
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softUTD.Text);

                // 放电低温释放
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softUTDR.Text);

                // 放电低温延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softUTDT.Text);//100

                // 充电过流
                ptr += 2;
                // Two_Bytes_Write(data, ptr, 1, comboBoxOCC.SelectedIndex);
                if (!string.IsNullOrEmpty(OCC.Text))
                {
                    try
                    {
                        tmp = Convert.ToInt32(OCC.Text);
                        if (tmp <= 0)
                        {
                            MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                    catch
                    {
                        MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                Two_Bytes_Write(data, ptr, 1,OCC.Text);

                // 充电过流释放延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOCCRT.Text);

                // 充电过流延时
                ptr += 2;
                // Two_Bytes_Write(data, ptr, 1, comboBoxOCCT.SelectedIndex);
                Two_Bytes_Write_Index(data, ptr, comboBoxOCCT.SelectedIndex);

                // 放电过流1
                ptr += 2;
                // Two_Bytes_Write(data, ptr, 1, comboBoxOCD1.SelectedIndex);
                if (!string.IsNullOrEmpty(OCD1.Text))
                {
                    try
                    {
                        tmp = Convert.ToInt32(OCD1.Text);
                        if (tmp <= 0)
                        {
                            MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                    catch
                    {
                        MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                Two_Bytes_Write(data, ptr,1, OCD1.Text);

                // 放电过流1释放延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOCD1RT.Text);//110

                // 放电过流1延时
                ptr += 2;
                // Two_Bytes_Write(data, ptr, 1, comboBoxOCD1T.SelectedIndex);
                Two_Bytes_Write_Index(data, ptr, comboBoxOCD1T.SelectedIndex);

                // 短路
                ptr += 2;
                Two_Bytes_Write_Index(data, ptr, softSC.SelectedIndex);

                // 短路释放延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softSCRT.Text);

                // 短路延时
                ptr += 2;
                Two_Bytes_Write_Index(data, ptr, softSCT.SelectedIndex);

                // MOS超温
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softMosOT.Text);//120

                // MOS超温释放
                ptr += 2;
                Two_Bytes_Write_Temp(data, ptr, softMosOTR.Text);

                // MOS超温延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softMosOTT.Text);

                // 均衡电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, BlanVolt.Text);//126

                // 充电唤醒
                ptr += 2;
                Two_Bytes_Write_Index(data, ptr, Vchg_th.SelectedIndex);

                // 放电唤醒
                ptr += 2;
                Two_Bytes_Write_Index(data, ptr, Vchg_th.SelectedIndex);//130

                // 放电过流2
                ptr += 2;
                //Two_Bytes_Write(data, ptr, 1, comboBoxOCD2.SelectedIndex);
                if (!string.IsNullOrEmpty(OCD2.Text))
                {
                    try
                    {
                        tmp = Convert.ToInt32(OCD2.Text);
                        if (tmp <= 0)
                        {
                            MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                    catch
                    {
                        MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                Two_Bytes_Write(data, ptr, 1, OCD2.Text);

                // 放电过流2释放延时
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, softOCD2RT.Text);

                // 放电过流2延时
                ptr += 2;
                // Two_Bytes_Write(data, ptr, 1, comboBoxOCD2T.SelectedIndex);//136~137
                Two_Bytes_Write_Index(data, ptr, comboBoxOCD2T.SelectedIndex);

                //市电切换电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, mainVol.Text);
                //浮充电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, pwmVol.Text);
                //开始加热温度
                ptr += 2;
                data[ptr] = (byte)(int.Parse(tempStart.Text));
                //停止加热温度
                ptr += 1;
                data[ptr] = (byte)(int.Parse(tempEnd.Text));
                //保护恢复放电%
                ptr += 1;
                Two_Bytes_Write(data, ptr, 1, LowBatLevel.Text);
                //深度休眠电压
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, SleepVol.Text);
                //低压保护报警
                ptr += 2;
                Two_Bytes_Write(data, ptr, 1, lowSocAlarm.Text);

                // }
            }
            catch
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            int temp = 0;//校验
            for (int i = 0; i < ptr + 2; i++) //30
            {
                temp += data[i];
            }
            temp = 0x10000 - temp;

            data[ptr + 2] = (byte)(temp >> 8); // 30
            data[ptr + 3] = (byte)temp;// 139
            data[ptr + 4] = 0x77;// 140

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, mcuE2num);   //mcuE2num
                 }
                catch
                {
                    return false;
                }
                Thread.Sleep(500);
                int total_len = serialPort1.BytesToRead;
                //MessageBox.Show(null, "接收串口数据 error", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (total_len > 6)
                {
                    temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Cali_Voltage_Temperature()
        {
            byte[] data = new byte[100];
            byte[] rcvBuf = new byte[512];
            System.Windows.Forms.TextBox[] myTextBox = new System.Windows.Forms.TextBox[32];

            for (int i = 0; i < 100; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;                 //start
            data[1] = WRITE_CMD;            //write
            data[2] = CALI_VOLTTEMP_CMD;    //电压温度校准
            data[3] = 71;                   //长度

            myTextBox[0] = CaliModify_cell_1;
            myTextBox[1] = CaliModify_cell_2;
            myTextBox[2] = CaliModify_cell_3;
            myTextBox[3] = CaliModify_cell_4;
            myTextBox[4] = CaliModify_cell_5;
            myTextBox[5] = CaliModify_cell_6;
            myTextBox[6] = CaliModify_cell_7;
            myTextBox[7] = CaliModify_cell_8;
            myTextBox[8] = CaliModify_cell_9;
            myTextBox[9] = CaliModify_cell_10;
            myTextBox[10] = CaliModify_cell_11;
            myTextBox[11] = CaliModify_cell_12;
            myTextBox[12] = CaliModify_cell_13;
            myTextBox[13] = CaliModify_cell_14;
            myTextBox[14] = CaliModify_cell_15;
            myTextBox[15] = CaliModify_cell_16;
            myTextBox[16] = CaliModify_cell_17;
            myTextBox[17] = CaliModify_cell_18;
            myTextBox[18] = CaliModify_cell_19;
            myTextBox[19] = CaliModify_cell_20;
            myTextBox[20] = CaliModify_cell_21;
            myTextBox[21] = CaliModify_cell_22;
            myTextBox[22] = CaliModify_cell_23;
            myTextBox[23] = CaliModify_cell_24;

            myTextBox[24] = CaliModify_NTC1;
            myTextBox[25] = CaliModify_NTC2;
            myTextBox[26] = CaliModify_NTC3;
            myTextBox[27] = CaliModify_NTC4;
            myTextBox[28] = CaliModify_NTC5;
            myTextBox[29] = CaliModify_NTC6;
            myTextBox[30] = CaliModify_NTC7;
            myTextBox[31] = CaliModify_NTC8;

            for (int i = 0; i < 24; i++)
            {
                if (!string.IsNullOrEmpty(myTextBox[i].Text))
                {
                    try
                    {
                        Two_Bytes_Write(data, (4 + i * 2), 1, myTextBox[i].Text);
                    }
                    catch
                    {
                        MessageBox.Show(null, "输入电压数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }

            for (int i = 24; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(myTextBox[i].Text))
                {
                    try
                    {
                        Two_Bytes_Write_Temp(data, (4 + i * 2), myTextBox[i].Text);
                    }
                    catch
                    {
                        MessageBox.Show(null, "输入温度数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }

            int temp = 0;//校验
            for (int i = 0; i < 68; i++)
            {
                temp += data[i];
            }
            temp = 0x10000 - temp;
            data[68] = (byte)(temp >> 8);
            data[69] = (byte)temp;
            data[70] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 71);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(200);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Cali_Zero_Current()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;                 //start
            data[1] = WRITE_CMD;            //write
            data[2] = CALI_ZEROCURR_CMD;    //静态电流校准
            data[3] = 7;                    //长度    
            data[4] = 0x33;
            data[5] = 0x44;
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(150);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Cali_Chg_Current()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            if (Convert.ToInt32(CaliChgCurr.Text) > 30000)
            {
                MessageBox.Show(null, "请在0.5~30A范围内校准充电电流", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;                 //start
            data[1] = WRITE_CMD;            //write
            data[2] = CALI_CHGCURR_CMD;     //充电电流校准
            data[3] = 7;                    //长度    
            try
            {
                Two_Bytes_Write(data, 4, 1, CaliChgCurr.Text);
            }
            catch (System.Exception)
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Cali_Pwm_Chg_Current()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            if (Convert.ToInt32(PwmChgCur.Text) > 30000)
            {
                MessageBox.Show(null, "请在0.5~30A范围内校准充电电流", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;                 //start
            data[1] = WRITE_CMD;            //write
            data[2] = CALI_PWM_CHGCURR_CMD;     //充电电流校准
            data[3] = 7;                    //长度    
            try
            {
                Two_Bytes_Write(data, 4, 1, PwmChgCur.Text);
            }
            catch (System.Exception)
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }


        bool Cali_Dsg_Current()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];
            Int16 tmp;

            if (Convert.ToInt32(CaliDsgCurr.Text) > 30000)
            {
                MessageBox.Show(null, "请在0.5~30A范围内校准放电电流", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;                 //start
            data[1] = WRITE_CMD;            //write
            data[2] = CALI_DSGCURR_CMD;     //放电电流校准
            data[3] = 7;                    //长度    
            // Two_Bytes_Write(data, 4, 10, CaliChgCurr.Text);
            // Int16 curr = (Int16)(int.Parse(CaliChgCurr.Text) / 10);
            try
            {
                int tmp1 = Convert.ToInt32(CaliDsgCurr.Text);
                // tmp1 /= -10;
                tmp1 /= -1;
                tmp = (Int16)(tmp1);
                data[4] = (byte)tmp;
                data[5] = (byte)(tmp >> 8);
            }
            catch (System.Exception)
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);



            return false;
        }

        bool Cali_SOC()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = CALI_SOC_CMD;     //静态电流校准
            data[3] = 7;       //长度    
            try
            {
                Two_Bytes_Write(data, 4, 1, CaliModify_SOC.Text);
            }
            catch (System.Exception)
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            data[6] = 0x77;

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Set_Num()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa0;
            data[3] = 7;       //长度    
            data[4] = 0;
            data[5] = (byte)(UInt16.Parse(textBoxCellNumSet.Text));
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    Display_Clear();
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool ChgMos_Off()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa1;
            data[3] = 7;       //长度    
            data[4] = 0;
            data[5] = 0;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool ChgMos_On()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa1;
            data[3] = 7;       //长度    
            data[4] = 0xff;
            data[5] = 0xff;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Maint_On()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa3;
            data[3] = 7;       //长度    
            data[4] = 0xff;
            data[5] = 0xff;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Maint_Off()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa3;
            data[3] = 7;       //长度    
            data[4] = 0;
            data[5] = 0;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool DsgMos_Off()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa2;
            data[3] = 7;       //长度    
            data[4] = 0;
            data[5] = 0;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool DsgMos_On()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa2;
            data[3] = 7;       //长度    
            data[4] = 0xff;
            data[5] = 0xff;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool PreDsg_On()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa6;
            data[3] = 7;       //长度    
            data[4] = 0xff;
            data[5] = 0xff;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool PreDsg_Off()
        {
            byte[] data = new byte[10];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 10; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa6;
            data[3] = 7;       //长度    
            data[4] = 0;
            data[5] = 0;
            data[6] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    int temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Write_ID()
        {
            byte[] data = new byte[100];
            byte[] rcvBuf = new byte[512];

            for (int i = 0; i < 100; i++)
            {
                data[i] = 0xff;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0xa4;
            data[3] = 0x20;
            try
            {
                if ((textBoxID.Text.Length) != 26)
                {
                    MessageBox.Show(null, "输入的字符数必须为26个", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                for (int i = 0; i < 26; i++)
                {
                    data[4 + i] = (byte)textBoxID.Text[i];
                    // data[4 + i] = 0;
                }
            }
            catch (System.Exception)
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            int temp = 0;//校验
            for (int i = 0; i < 30; i++)
            {
                temp += data[i];
            }
            temp = 0x10000 - temp;

            data[30] = (byte)(temp >> 8); // AAAAAAAAAABBBBBBBBBBCCCCCC
            data[31] = (byte)temp;// 139
            data[32] = 0x77;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 33);
                }
                catch
                {
                    return false;
                }

                Thread.Sleep(100);
                int total_len = serialPort1.BytesToRead;
                if (total_len > 6)
                {
                    /* int  */
                    temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        private void butReadMcuE2_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Read_McuE2_Data())
            {
                MessageBox.Show(null, "读取成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "读取失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void butWriteMcuE2_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_McuE2_Data())
            {
                MessageBox.Show(null, "写入成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "写入失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void bntRefreshCommPort_Click(object sender, EventArgs e)
        {
            string[] commPorts = System.IO.Ports.SerialPort.GetPortNames();
            System.Array.Sort(commPorts);
            cboPortName.Items.Clear();
            if (/* commPorts[0] == null &&  */commPorts.Length == 0)
            {
                cboPortName.Text = "COM";
                serialPort1.PortName = cboPortName.Text;
            }
            else
            {
                cboPortName.Items.AddRange(commPorts);
                // cboPortName.SelectedIndex = cboPortName.Items.Count > 0 ? 0 : -1;
                cboPortName.SelectedIndex = commPorts.Length - 1;
                if (serialPort1.IsOpen)
                {
                    try
                    {
                        serialPort1.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                serialPort1.PortName = cboPortName.Text;
                // serialPort1.BaudRate = 9600;//57600;
                serialPort1.Parity = System.IO.Ports.Parity.None;
                serialPort1.DataBits = 8;
                serialPort1.StopBits = System.IO.Ports.StopBits.One;
                serialPort1.WriteBufferSize = 4096;
            }
        }

        private void btnOpenComm_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                try
                {
                    //serialPort1.Close();
                    m_commTimeout = 0;
                    // 关键：取消订阅——直接用方法名，和订阅时匹配，100%有效
                  //  serialPort1.DataReceived -= comPort_DataReceived;

                    try
                    {
                        if (serialPort1.IsOpen)
                        { serialPort1.Close(); }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    // label225.Text = "未连接!";
                    // label225.ForeColor = Color.Red; ;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    checkBoxSaveData.Checked = false;
                    bntRefreshCommPort.Enabled = true;
                    // bntsDisable();
                    btnOpenComm.Enabled = true;
                }
            }
            else
            {
                try
                {
                    serialPort1.DtrEnable = false;
                    //如果为 true，则启用数据终端就绪 (DTR)；否则为 false。 默认为 false。
                    serialPort1.RtsEnable = false;
                    serialPort1.PortName = cboPortName.Text;
                    // comboBAUD.Text = "115200";
                    serialPort1.BaudRate = int.Parse(comboBAUD.Text);
                    //comboBAUD.DisplayMember = "115200";
                  //  serialPort1.DataReceived += comPort_DataReceived;
                    serialPort1.Open();
                    // bntRefreshCommPort.Enabled = False;

                    if (tabControl1.SelectedTab.Text == "电池信息")
                    {

                    }
                    if (serialPort1.IsOpen)
                    {
                        // Thread recieveThread = new Thread(comRecieveThread);
                        // recieveThread.IsBackground = true;
                        // recieveThread.Start();
                        //m_readBatt = true;
                    }
                }
                catch (Exception ex)
                {

                    MessageBox.Show(ex.Message);
                }
            }
            btnOpenComm.Text = serialPort1.IsOpen ? "关闭 CLOSE" : "打开 OPEN";
            cboPortName.Enabled = serialPort1.IsOpen ? false : true;
            Thread.Sleep(100);


        }

        private void btnVoltTempCali_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Cali_Voltage_Temperature())
            {
                MessageBox.Show(null, "校准成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "校准失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnZeroCurrentCali_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Cali_Zero_Current())
            {
                MessageBox.Show(null, "校准成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "校准失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnChgCurrentCali_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!string.IsNullOrEmpty(CaliChgCurr.Text))
            {
                try
                {
                    int tmp = Convert.ToInt32(CaliChgCurr.Text);
                    if (tmp <= 0)
                    {
                        MessageBox.Show(null, "请输入正整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (Cali_Chg_Current())
                {
                    MessageBox.Show(null, "校准成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(null, "校准失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show(null, "输入数据非法,请输入正整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
            }
        }

        private void btnDsgCurrentCali_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!string.IsNullOrEmpty(CaliDsgCurr.Text))
            {
                try
                {
                    int tmp = Convert.ToInt32(CaliDsgCurr.Text);
                    if (tmp >= 0)
                    {
                        MessageBox.Show(null, "请输入负整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            
                if (Cali_Dsg_Current())
                {
                    MessageBox.Show(null, "校准成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(null, "校准失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
             else
                {
                    MessageBox.Show(null, "输入数据非法,请输入正整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
        }

        private void btnSocCali_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!string.IsNullOrEmpty(textBoxCellNumSet.Text))
            {
                try
                {
                    int tmp = Convert.ToInt32(textBoxCellNumSet.Text);
                    if (tmp > 20 || tmp < 17)
                    {
                        MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (Cali_SOC())
            {
                MessageBox.Show(null, "校准成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "校准失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonSetNum_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!string.IsNullOrEmpty(CaliModify_SOC.Text))
            {
                try
                {
                    int tmp = Convert.ToInt32(CaliModify_SOC.Text);
                    if (tmp > 20 || tmp < 17)
                    {
                        MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (Set_Num())
            {
                MessageBox.Show(null, "修改成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "修改失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveMcuE2_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();

            //设置文件类型
            sfd.Filter = "数据文件(*.sbr)|*.sbr";

            //设置默认文件类型显示顺序
            sfd.FilterIndex = 1;

            //保存对话框是否记忆上次打开的目录
            sfd.RestoreDirectory = true;

            //点了保存按钮进入
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string localFilePath = sfd.FileName.ToString(); //获得文件路径
                string fileNameExt = localFilePath.Substring(localFilePath.LastIndexOf("\\") + 1); //获取文件名，不带路径

                StreamWriter sw = new StreamWriter(localFilePath, false, Encoding.GetEncoding("gb2312"));//实例化StreamWriter

                sw.WriteLine(DesignFCC.Text);           //设计容量
                sw.WriteLine(FullFCC.Text);             //满充容量
                sw.WriteLine(CycleFCC.Text);            //循环容量
                sw.WriteLine(Fcc4Volt.Text);            //放空电压
                sw.WriteLine(Fcc20Volt.Text);           //20%容量电压
                sw.WriteLine(Fcc40Volt.Text);           //40%容量电压
                sw.WriteLine(Fcc60Volt.Text);           //60%容量电压
                sw.WriteLine(Fcc80Volt.Text);           //80%容量电压
                sw.WriteLine(Fcc100Volt.Text);          //满充电压
                sw.WriteLine(SelfDsgRatio.Text);        //自放电率
                sw.WriteLine(FullChgCurrTld.Text);      //满充截止电流
                sw.WriteLine(BalanDiffVolt.Text);       //均衡精度
                sw.WriteLine(SleepDelay.Text);          //睡眠延时
                sw.WriteLine(Resistent.Text);           //检流电阻值

                sw.WriteLine(SC_Cnt.Text);              //负载短路次数
                sw.WriteLine(OCC_Cnt.Text);             //充电过流次数
                sw.WriteLine(OCD_Cnt.Text);             //放电过流次数
                sw.WriteLine(OV_Cnt.Text);              //过压次数
                sw.WriteLine(UV_Cnt.Text);              //欠压次数
                sw.WriteLine(OTC_Cnt.Text);             //充电高温次数
                sw.WriteLine(UTC_Cnt.Text);             //充电低温次数
                sw.WriteLine(OTD_Cnt.Text);             //放电高温次数
                sw.WriteLine(UTD_Cnt.Text);             //放电低温次数
                sw.WriteLine(CycleCnt.Text);            //循环次数
                sw.WriteLine(textBoxZeroCurr.Text);            //零电流阈值
                sw.WriteLine(ManuDay.Text);             //日
                sw.WriteLine(ManuMonth.Text);           //月
                sw.WriteLine(ManuYear.Text);            //年

                sw.WriteLine(BalanEN.Text);             //均衡功能
                sw.WriteLine(ChgBalanSelect.Text);      //充电均衡
                sw.WriteLine(comboSleep.Text);          //低功耗模式
                sw.WriteLine(OccRelease.Text);          //充电锁定，
                sw.WriteLine(ScRelease.Text);           //负载锁定

                sw.WriteLine(softOV.Text);
                sw.WriteLine(softOVR.Text);
                sw.WriteLine(comboBoxOVT.Text);
                sw.WriteLine(softUV.Text);
                sw.WriteLine(softUVR.Text);
                sw.WriteLine(comboBoxUVT.Text);
                sw.WriteLine(softOTC.Text);
                sw.WriteLine(softOTCR.Text);
                sw.WriteLine(softOTCT.Text);
                sw.WriteLine(softUTC.Text);
                sw.WriteLine(softUTCR.Text);
                sw.WriteLine(softUTCT.Text);
                sw.WriteLine(softOTD.Text);
                sw.WriteLine(softOTDR.Text);
                sw.WriteLine(softOTDT.Text);
                sw.WriteLine(softUTD.Text);
                sw.WriteLine(softUTDR.Text);
                sw.WriteLine(softUTDT.Text);
                sw.WriteLine(OCC.Text);
                sw.WriteLine(softOCCRT.Text);
                sw.WriteLine(comboBoxOCCT.Text);
                sw.WriteLine(OCD1.Text);
                sw.WriteLine(softOCD1RT.Text);
                sw.WriteLine(comboBoxOCD1T.Text);
                sw.WriteLine(softSC.Text);
                sw.WriteLine(softSCRT.Text);
                sw.WriteLine(softSCT.Text);
                sw.WriteLine(softMosOT.Text);
                sw.WriteLine(softMosOTR.Text);
                sw.WriteLine(softMosOTT.Text);
                sw.WriteLine(BlanVolt.Text);
                sw.WriteLine(Vchg_th.Text);
                sw.WriteLine(Vdsg_th.Text);
                sw.WriteLine(OCD2.Text);
                sw.WriteLine(softOCD2RT.Text);
                sw.WriteLine(comboBoxOCD2T.Text);


                sw.WriteLine(comboMainFirst.Text);      //市电优先
                sw.WriteLine(comboSWAlways.Text);       //开关自锁，
                sw.WriteLine(comboSOCLEARN.Text);           //电量学习

                sw.WriteLine(mainVol.Text);          //
                sw.WriteLine(pwmVol.Text);           //
                sw.WriteLine(tempStart.Text);      //
                sw.WriteLine(tempEnd.Text);       //
                sw.WriteLine(LowBatLevel.Text);           //
                sw.WriteLine(SleepVol.Text);           //
                sw.WriteLine(lowSocAlarm.Text);           //
                sw.Flush();
                sw.Close();
            }
        }

        private void btnReadMcuE2_Click(object sender, EventArgs e)
        {
            string filePath = "";
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.InitialDirectory = System.Windows.Forms.Application.StartupPath;
            openFileDialog1.Filter = "数据文件(*.sbr)|*.sbr";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog1.FileName;
                StreamReader sr = new StreamReader(filePath, Encoding.GetEncoding("gb2312"));

                DesignFCC.Text = sr.ReadLine();           //设计容量
                FullFCC.Text = sr.ReadLine();             //满充容量
                CycleFCC.Text = sr.ReadLine();            //循环容量
                Fcc4Volt.Text = sr.ReadLine();            //放空电压
                Fcc20Volt.Text = sr.ReadLine();           //20%容量电压
                Fcc40Volt.Text = sr.ReadLine();           //40%容量电压
                Fcc60Volt.Text = sr.ReadLine();           //60%容量电压
                Fcc80Volt.Text = sr.ReadLine();           //80%容量电压
                Fcc100Volt.Text = sr.ReadLine();          //满充电压
                SelfDsgRatio.Text = sr.ReadLine();        //自放电率
                FullChgCurrTld.Text = sr.ReadLine();      //满充截止电流
                BalanDiffVolt.Text = sr.ReadLine();       //均衡精度
                SleepDelay.Text = sr.ReadLine();          //睡眠延时
                Resistent.Text = sr.ReadLine();           //检流电阻值

                SC_Cnt.Text = sr.ReadLine();              //负载短路次数
                OCC_Cnt.Text = sr.ReadLine();             //充电过流次数
                OCD_Cnt.Text = sr.ReadLine();             //放电过流次数
                OV_Cnt.Text = sr.ReadLine();              //过压次数
                UV_Cnt.Text = sr.ReadLine();              //欠压次数
                OTC_Cnt.Text = sr.ReadLine();             //充电高温次数
                UTC_Cnt.Text = sr.ReadLine();             //充电低温次数
                OTD_Cnt.Text = sr.ReadLine();             //放电高温次数
                UTD_Cnt.Text = sr.ReadLine();             //放电低温次数
                CycleCnt.Text = sr.ReadLine();            //循环次数
                textBoxZeroCurr.Text = sr.ReadLine();            //零电流阈值
                ManuDay.Text = sr.ReadLine();             //日
                ManuMonth.Text = sr.ReadLine();           //月
                ManuYear.Text = sr.ReadLine();            //年

                BalanEN.Text = sr.ReadLine();             //均衡功能
                ChgBalanSelect.Text = sr.ReadLine();      //充电均衡
                comboSleep.Text = sr.ReadLine();          //低功耗模式
                OccRelease.Text = sr.ReadLine();          //充电锁定
                ScRelease.Text = sr.ReadLine();           //负载锁定

                softOV.Text = sr.ReadLine();
                softOVR.Text = sr.ReadLine();
                comboBoxOVT.Text = sr.ReadLine();
                softUV.Text = sr.ReadLine();
                softUVR.Text = sr.ReadLine();
                comboBoxUVT.Text = sr.ReadLine();
                softOTC.Text = sr.ReadLine();
                softOTCR.Text = sr.ReadLine();
                softOTCT.Text = sr.ReadLine();
                softUTC.Text = sr.ReadLine();
                softUTCR.Text = sr.ReadLine();
                softUTCT.Text = sr.ReadLine();
                softOTD.Text = sr.ReadLine();
                softOTDR.Text = sr.ReadLine();
                softOTDT.Text = sr.ReadLine();
                softUTD.Text = sr.ReadLine();
                softUTDR.Text = sr.ReadLine();
                softUTDT.Text = sr.ReadLine();
                OCC.Text = sr.ReadLine();
                softOCCRT.Text = sr.ReadLine();
                comboBoxOCCT.Text = sr.ReadLine();
                OCD1.Text = sr.ReadLine();
                softOCD1RT.Text = sr.ReadLine();
                comboBoxOCD1T.Text = sr.ReadLine();
                softSC.Text = sr.ReadLine();
                softSCRT.Text = sr.ReadLine();
                softSCT.Text = sr.ReadLine();
                softMosOT.Text = sr.ReadLine();
                softMosOTR.Text = sr.ReadLine();
                softMosOTT.Text = sr.ReadLine();
                BlanVolt.Text = sr.ReadLine();
                Vchg_th.Text = sr.ReadLine();
                Vdsg_th.Text = sr.ReadLine();
                OCD2.Text = sr.ReadLine();
                softOCD2RT.Text = sr.ReadLine();
                comboBoxOCD2T.Text = sr.ReadLine();


                comboMainFirst.Text = sr.ReadLine();      //市电优先
                comboSWAlways.Text = sr.ReadLine();       //开关自锁，
                comboSOCLEARN.Text = sr.ReadLine();        //电量学习

                mainVol.Text = sr.ReadLine();
                pwmVol.Text = sr.ReadLine();
                tempStart.Text = sr.ReadLine();
                tempEnd.Text = sr.ReadLine();
                LowBatLevel.Text = sr.ReadLine();
                SleepVol.Text = sr.ReadLine();
                lowSocAlarm.Text = sr.ReadLine();

                sr.Close();//关闭
            }

        }

        private void checkBoxSaveData_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxSaveData.Checked)
            {
                if (!serialPort1.IsOpen)
                {
                    MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    checkBoxSaveData.Checked = false;
                    return;
                }

                //创建工作薄
                var workbook = new XSSFWorkbook();

                //创建表
                var table = workbook.CreateSheet("电池信息");

                ICellStyle style = workbook.CreateCellStyle();//创建样式
                //style.VerticalAlignment = VerticalAlignment.Justify;//垂直居中 方法1 
                style.Alignment = NPOI.SS.UserModel.HorizontalAlignment.CenterSelection;//设置居中 方法2
                //style.Alignment = HorizontalAlignment.Center;//设置居中 方法3 

                table.SetColumnWidth(0, 13 * 256);
                table.SetColumnWidth(1, 10 * 256);
                table.SetColumnWidth(2, 8 * 256);
                table.SetColumnWidth(3, 8 * 256);

                for (int i = 4; i < 36; i++)
                {
                    table.SetColumnWidth(i, 10 * 256);
                }

                SaveFileDialog sfd = new SaveFileDialog();

                //设置文件类型
                sfd.Filter = "数据文件(*.xlsx)|*.xlsx";

                //设置默认文件类型显示顺序
                sfd.FilterIndex = 1;

                //保存对话框是否记忆上次打开的目录
                sfd.RestoreDirectory = true;

                //点了保存按钮进入
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    //获得文件路径
                    xslFilePath = sfd.FileName.ToString();

                    var row0 = table.CreateRow(0);
                    int i = 0;
                    row0.CreateCell(i++).SetCellValue("日   期");
                    row0.CreateCell(i++).SetCellValue("时间");
                    row0.CreateCell(i++).SetCellValue("总电压");
                    row0.CreateCell(i++).SetCellValue("电流");
                    row0.CreateCell(i++).SetCellValue("太阳能电压");
                    row0.CreateCell(i++).SetCellValue("太阳能电流");
                    row0.CreateCell(i++).SetCellValue("市电电流");
                    for (int j = 0; j < m_cellNum; j++)
                    {
                        table.SetColumnWidth(i, 7 * 256);
                        int tmp = j + 1;
                        string str = "电芯";
                        str += tmp.ToString();
                        row0.CreateCell(i++).SetCellValue(str);
                    }

                    for (int j = 0; j < m_ntcNum; j++)
                    {
                        table.SetColumnWidth(i, 7 * 256);
                        int tmp = j + 1;
                        string str = "温度";
                        str += tmp.ToString();
                        row0.CreateCell(i++).SetCellValue(str);
                    }

                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("平均电压");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("最高电压节数");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("最高电压");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("最低电压节数");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("最低电压");
                    table.SetColumnWidth(i, 5 * 256);
                    row0.CreateCell(i++).SetCellValue("压差");
                    table.SetColumnWidth(i, 5 * 256);
                    row0.CreateCell(i++).SetCellValue("RSOC");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("剩余容量");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("满充容量");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("循环次数");
                    table.SetColumnWidth(i, 8 * 256);
                    row0.CreateCell(i++).SetCellValue("充电MOS");
                    table.SetColumnWidth(i, 8 * 256);
                    row0.CreateCell(i++).SetCellValue("放电MOS");
                    table.SetColumnWidth(i, 8 * 256);
                    // row0.CreateCell(i++).SetCellValue("MCU-DSG");
                    // table.SetColumnWidth(i, 8 * 256);
                    // row0.CreateCell(i++).SetCellValue("MCU-CHG");

                    // table.SetColumnWidth(i, 8 * 256);
                    // row0.CreateCell(i++).SetCellValue("报警");

                    // if ("JTLS1-15S2x-30A11565-11" == bmsStyle)
                    // {
                    //     table.SetColumnWidth(i, 7 * 256);
                    //     row0.CreateCell(i++).SetCellValue("CONF");
                    //     table.SetColumnWidth(i, 10 * 256);
                    //     row0.CreateCell(i++).SetCellValue("BSTATUS1");
                    //     table.SetColumnWidth(i, 10 * 256);
                    //     row0.CreateCell(i++).SetCellValue("BSTATUS2");
                    //     table.SetColumnWidth(i, 10 * 256);
                    //     row0.CreateCell(i++).SetCellValue("BSTATUS3");
                    //     table.SetColumnWidth(i, 8 * 256);
                    //     row0.CreateCell(i++).SetCellValue("BFLAG1");
                    //     table.SetColumnWidth(i, 8 * 256);
                    //     row0.CreateCell(i++).SetCellValue("BFLAG2");
                    // }

                    table.SetColumnWidth(i, 6 * 256);
                    row0.CreateCell(i++).SetCellValue("过压");
                    table.SetColumnWidth(i, 6 * 256);
                    row0.CreateCell(i++).SetCellValue("欠压");
                    table.SetColumnWidth(i, 6 * 256);
                    row0.CreateCell(i++).SetCellValue("断线");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("放电过流");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("充电高温");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("充电低温");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("放电高温");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("放电低温");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("充电过流");
                    table.SetColumnWidth(i, 9 * 256);
                    row0.CreateCell(i++).SetCellValue("芯片错误");
                    table.SetColumnWidth(i, 6 * 256);
                    row0.CreateCell(i++).SetCellValue("短路");


                    for (int j = 0; j < i; j++)
                    {
                        row0.GetCell(j).CellStyle = style;
                    }

                    try
                    {
                        using (FileStream url = File.OpenWrite(xslFilePath))
                        {
                            workbook.Write(url);
                        }
                        timerWriteExcel.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        checkBoxSaveData.Checked = false;
                        timerWriteExcel.Enabled = false;
                        MessageBox.Show(null, ex.Message, "信息提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else// if (sfd.ShowDialog() == DialogResult.Cancel)
                {
                    checkBoxSaveData.Checked = false;
                }
            }
            else
            {
                timerWriteExcel.Enabled = false;
            }
        }

        private void buttonChgMosOff_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (ChgMos_Off())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonChgMosOn_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (ChgMos_On())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonDsgMosOff_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (DsgMos_Off())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonDsgMosOn_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (DsgMos_On())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonMaintOn_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (Maint_On())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonMaintOff_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (Maint_Off())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonPreDsgOn_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (PreDsg_On())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonPreDsgOff_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (PreDsg_Off())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonWriteID_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (Write_ID())
            {
                MessageBox.Show(null, "操作成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(null, "操作失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonRdID_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Read_ID())
            {
                MessageBox.Show(null, "读取成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "读取失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void pSolar_Click(object sender, EventArgs e)
        {

        }

        private void Current_Click(object sender, EventArgs e)
        {

        }

        private void label141_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load_1(object sender, EventArgs e)
        {

        }

        bool Read_UartBaud() // 读参数
        {
            byte[] data = new byte[32];
            byte[] rcvBuf = new byte[32];
            byte[] tmpBuf = new byte[32];
            int temp;
            int addr = 0;
            int mUartLength = 9;
            int totalLength = 0;
            bool rcved_num = false;

            data[0] = 0xcc;                 //start
            data[1] = READ_CMD;             //read
            data[2] = READ_UART_BAUD_CMD;      //读Uart BAUD
            data[3] = 7;                    //长度
            temp = data[0] + data[1] + data[2] + data[3];
            temp = 0x10000 - temp;
            data[4] = (byte)(temp >> 8);
            data[5] = (byte)temp;
            data[6] = 0x77;
            //rcvBuf[4] = 48;
            //textBoxBaud.Text = (rcvBuf[4] * 2400).ToString();
            int m_start = GetTickCount();
            do
            {
                try//send
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(200);
                totalLength = serialPort1.BytesToRead;
                if (totalLength >= mUartLength)
                {
                    if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                    {
                        for (int i = 0; i < (totalLength - (mUartLength - 1)); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == 0xa5) && (rcvBuf[i + 2] == READ_UART_BAUD_CMD) && (rcvBuf[i + 3] == mUartLength) && (rcvBuf[i + mUartLength - 1] == 0x77))
                            {
                                rcved_num = true;
                                addr = i;
                            }
                        }
                        if (rcved_num == true)
                        {
                            for (int i = 0; i < mUartLength; i++)
                            {
                                rcvBuf[i] = rcvBuf[i + addr];
                            }
                            temp = 0;
                            for (int i = 0; i < (mUartLength - 3); i++)
                            {
                                temp += rcvBuf[i];
                            }
                            temp = 0x10000 - temp;
                            if ((((temp >> 8) & 0xff) == rcvBuf[mUartLength - 3]) && ((temp & 0xff) == rcvBuf[mUartLength - 2]))//校验OK
                            {
                                //UART BAUD

                                textBoxBaud.Text = (rcvBuf[4] * 2400).ToString();

                                //UART ADDR
                                TextUartAddr.Text = (rcvBuf[5] & 0x3F).ToString();



                                return true;
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Read_Para_Data() // 读参数
        {
            byte[] data = new byte[32];
            byte[] rcvBuf = new byte[32];
            byte[] tmpBuf = new byte[32];
            int temp;
            int addr = 0;
            int mUartLength = 9;
            int totalLength = 0;
            bool rcved_num = false;

            data[0] = 0xcc;                 //start
            data[1] = READ_CMD;             //read
            data[2] = READ_PARA_CMD;      //读Uart BAUD
            data[3] = 7;                    //长度
            temp = data[0] + data[1] + data[2] + data[3];
            temp = 0x10000 - temp;
            data[4] = (byte)(temp >> 8);
            data[5] = (byte)temp;
            data[6] = 0x77;
            //rcvBuf[4] = 48;
            //textBoxBaud.Text = (rcvBuf[4] * 2400).ToString();
            int m_start = GetTickCount();
            do
            {
                try//send
                {
                    serialPort1.Write(data, 0, 7);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(200);
                totalLength = serialPort1.BytesToRead;
                if (totalLength >= mUartLength)
                {
                    if (serialPort1.Read(rcvBuf, 0, totalLength) != 0)
                    {
                        for (int i = 0; i < (totalLength - (mUartLength - 1)); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == 0xa5) && (rcvBuf[i + 2] == READ_PARA_CMD) && (rcvBuf[i + rcvBuf[i + 3] - 1] == 0x77))
                            {
                                mUartLength = rcvBuf[i + 3];
                                rcved_num = true;
                                addr = i;
                            }
                        }
                        if (rcved_num == true)
                        {
                            for (int i = 0; i < mUartLength; i++)
                            {
                                rcvBuf[i] = rcvBuf[i + addr];
                            }
                            temp = 0;
                            for (int i = 0; i < (mUartLength - 3); i++)
                            {
                                temp += rcvBuf[i];
                            }
                            temp = 0x10000 - temp;
                            if ((((temp >> 8) & 0xff) == rcvBuf[mUartLength - 3]) && ((temp & 0xff) == rcvBuf[mUartLength - 2]))//校验OK
                            {
                                //UART BAUD

                                mainVol.Text = ((rcvBuf[4] * 256 + rcvBuf[5])/10).ToString();

                                //UART ADDR
                                pwmVol.Text = ((rcvBuf[6] * 256 + rcvBuf[7])/10).ToString();

                                tempStart.Text = ((sbyte)rcvBuf[8]).ToString();

                                tempEnd.Text = ((sbyte)rcvBuf[9]).ToString();

                                LowBatLevel.Text = (rcvBuf[10] * 256 + rcvBuf[11]).ToString();

                                SleepVol.Text = (rcvBuf[12] * 256 + rcvBuf[13]).ToString();

                                return true;
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Write_Para_Data() // 写参数
        {
            // int tmp;
            byte[] data = new byte[32];
            byte[] rcvBuf = new byte[32];

            for (int i = 0; i < 32; i++)
            {
                data[i] = 0;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = WRITE_PARA_CMD;//WRITE_UART_BAUD_CMD;     //MCU EEPROM

            ptr = 4;
            //(byte)(int.Parse(ManuMonth.Text))
            try//赋值
            {
                // if ("XRT-19S" == bmsStyle || "BimMaiSi-16s" == bmsStyle)
                // {
                data[ptr++] = (byte)((int.Parse(mainVol.Text)*10) >> 8);
                data[ptr++] = (byte)((int.Parse(mainVol.Text)*10) & 0xFF);

                data[ptr++] = (byte)((int.Parse(pwmVol.Text)*10) >> 8);
                data[ptr++] = (byte)((int.Parse(pwmVol.Text)*10) & 0xFF);

                data[ptr++] = (byte)(int.Parse(tempStart.Text));
                data[ptr++] = (byte)(int.Parse(tempEnd.Text));

                data[ptr++] = (byte)((int.Parse(LowBatLevel.Text))>>8);
                data[ptr++] = (byte)((int.Parse(LowBatLevel.Text))& 0xFF);

                data[ptr++] = (byte)((int.Parse(SleepVol.Text)) >> 8);
                data[ptr++] = (byte)((int.Parse(SleepVol.Text)) & 0xFF);
                // }
            }
            catch
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // data[ptr + 1] = (byte)(ptr + 5);
            int temp = 0;//校验
            data[3] = (byte)(ptr + 3);
            for (int i = 0; i < ptr; i++) //30
            {
                temp += data[i];
            }
            temp = 0x10000 - temp;

            data[ptr++] = (byte)(temp >> 8); // 30
            data[ptr++] = (byte)temp;// 139
            data[ptr++] = 0x77;// 140
            //  data[3] = (byte)ptr;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, ptr);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(300);
                int total_len = serialPort1.BytesToRead;
                //MessageBox.Show(null, "接收串口数据 error", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (total_len > 6)
                {
                    temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        bool Write_Para_Sw(byte addr, byte dat) // 写参数
        {
            // int tmp;
            byte[] data = new byte[32];
            byte[] rcvBuf = new byte[32];

            for (int i = 0; i < 32; i++)
            {
                data[i] = 0;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0x66;//WRITE_UART_BAUD_CMD;     //MCU EEPROM

            ptr = 4;
            //(byte)(int.Parse(ManuMonth.Text))
            try//赋值
            {
                // if ("XRT-19S" == bmsStyle || "BimMaiSi-16s" == bmsStyle)
                // {
                data[ptr++] = addr;
                data[ptr++] = dat;

                //data[ptr++] = (byte)(int.Parse(pwmVol.Text) >> 8);
                //data[ptr++] = (byte)(int.Parse(pwmVol.Text) & 0xFF);

                // }
            }
            catch
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // data[ptr + 1] = (byte)(ptr + 5);
            int temp = 0;//校验
            data[3] = (byte)(ptr + 3);
            for (int i = 0; i < ptr; i++) //30
            {
                temp += data[i];
            }
            temp = 0x10000 - temp;

            data[ptr++] = (byte)(temp >> 8); // 30
            data[ptr++] = (byte)temp;// 139
            data[ptr++] = 0x77;// 140
            //  data[3] = (byte)ptr;
            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, ptr);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(300);
                int total_len = serialPort1.BytesToRead;
                //MessageBox.Show(null, "接收串口数据 error", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (total_len > 6)
                {
                    temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }



        bool Write_UartBaud() // 写参数
        {
            // int tmp;
            byte[] data = new byte[32];
            byte[] rcvBuf = new byte[32];

            for (int i = 0; i < 32; i++)
            {
                data[i] = 0;
            }
            data[0] = 0xcc;     //start
            data[1] = WRITE_CMD;     //write
            data[2] = 0x10;//WRITE_UART_BAUD_CMD;     //MCU EEPROM
            data[3] = 9;

            //(byte)(int.Parse(ManuMonth.Text))
            try//赋值
            {
                // if ("XRT-19S" == bmsStyle || "BimMaiSi-16s" == bmsStyle)
                // {
                data[4] = (byte)(int.Parse(textBoxBaud.Text) / 2400);
                data[5] = (byte)(int.Parse(TextUartAddr.Text));

                // }
            }
            catch
            {
                MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            ptr = 4;
            // data[ptr + 1] = (byte)(ptr + 5);
            int temp = 0;//校验
            for (int i = 0; i < ptr + 2; i++) //30
            {
                temp += data[i];
            }
            temp = 0x10000 - temp;

            data[ptr + 2] = (byte)(temp >> 8); // 30
            data[ptr + 3] = (byte)temp;// 139
            data[ptr + 4] = 0x77;// 140

            int m_start = GetTickCount();
            do
            {
                try
                {
                    serialPort1.Write(data, 0, 9);
                }
                catch
                {
                    return false;
                }
                Thread.Sleep(300);
                int total_len = serialPort1.BytesToRead;
                //MessageBox.Show(null, "接收串口数据 error", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (total_len > 6)
                {
                    temp = serialPort1.Read(rcvBuf, 0, total_len);
                    if (temp != 0)
                    {
                        for (int i = 0; i < (total_len - 6); i++)
                        {
                            if ((rcvBuf[i] == 0xcc) && (rcvBuf[i + 1] == data[1]) && (rcvBuf[i + 2] == data[2]) && (rcvBuf[i + 6] == 0x77))
                            {
                                temp = rcvBuf[i + 0] + rcvBuf[i + 1] + rcvBuf[i + 2] + rcvBuf[i + 3];
                                temp = 0x10000 - temp;
                                if (rcvBuf[i + 4] == (byte)(temp >> 8) && rcvBuf[i + 5] == (byte)temp)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            } while (GetTickCount() - m_start < 2000);

            return false;
        }

        private void UartReflash_Click(object sender, EventArgs e)  //UartReflash
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Read_UartBaud())
            {
                MessageBox.Show(null, "刷新成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "刷新失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void UartSet_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_UartBaud())
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void comboBAUD_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void textBoxIDRD_TextChanged(object sender, EventArgs e)
        {

        }

        private void label146_Click(object sender, EventArgs e)
        {

        }

        

        private void MainOn_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x11, 1))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                MainOff.ForeColor = System.Drawing.SystemColors.ControlText;
                MainOn.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void MainOff_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x11, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                MainOff.ForeColor = System.Drawing.SystemColors.Highlight;
                MainOn.ForeColor = System.Drawing.SystemColors.ControlText;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {

            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Factor0.ForeColor == System.Drawing.SystemColors.ControlText)
            {
                if (Write_Para_Sw(0x12, 250))
                //if(true)
                {
                    MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    //  groupBoxAfe.Visible = true; Transparent
                    m_factorTimeout = 250;
                    Factor0.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor1.Enabled = true;
                    Factor2.Enabled = true;
                    Factor3.Enabled = true;
                    Factor4.Enabled = true;
                    Factor5.Enabled = true;
                    Factor6.Enabled = true;
                    Factor7.Enabled = true;
                    Factor8.Enabled = true;
                    Factor9.Enabled = true;
                    Factor10.Enabled = true;
                    Factor11.Enabled = true;
                    Factor12.Enabled = true;
                    Factor13.Enabled = true;
                    Factor14.Enabled = true;
                    Factor15.Enabled = true;

                    Factor3.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor6.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor9.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor12.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor15.ForeColor = System.Drawing.SystemColors.Highlight;

                }
                else
                {
                    MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (Factor0.ForeColor == System.Drawing.SystemColors.Highlight)
            {
                if (Write_Para_Sw(0x12, 0))
                // if(true)
                {
                    MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    //  groupBoxAfe.Visible = true; Transparent
                    Factor0.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor1.Enabled = false;
                    Factor2.Enabled = false;
                    Factor3.Enabled = false;
                    Factor4.Enabled = false;
                    Factor5.Enabled = false;
                    Factor6.Enabled = false;
                    Factor7.Enabled = false;
                    Factor8.Enabled = false;
                    Factor9.Enabled = false;
                    Factor10.Enabled = false;
                    Factor11.Enabled = false;
                    Factor12.Enabled = false;
                    Factor13.Enabled = false;
                    Factor14.Enabled = false;
                    Factor15.Enabled = false;

                    Factor1.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor2.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor3.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor4.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor5.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor6.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor7.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor8.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor9.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor10.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor11.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor12.ForeColor = System.Drawing.SystemColors.Highlight;
                    Factor13.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor14.ForeColor = System.Drawing.SystemColors.ControlText;
                    Factor15.ForeColor = System.Drawing.SystemColors.Highlight;
                }
                else
                {
                    MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }


        }

        private void Factor1_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x13, 3))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor1.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor2.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor3.ForeColor = System.Drawing.SystemColors.ControlText;

                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor2_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x13, 2))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor1.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor2.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor3.ForeColor = System.Drawing.SystemColors.ControlText;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor3_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x13, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor1.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor2.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor3.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor4_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x14, 3))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor4.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor5.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor6.ForeColor = System.Drawing.SystemColors.ControlText;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor5_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x14, 2))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor4.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor5.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor6.ForeColor = System.Drawing.SystemColors.ControlText;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor6_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x14, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor4.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor5.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor6.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor7_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x15, 3))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;
                Factor7.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor8.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor9.ForeColor = System.Drawing.SystemColors.ControlText;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor8_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x15, 2))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor7.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor8.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor9.ForeColor = System.Drawing.SystemColors.ControlText;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor9_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x15, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor7.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor8.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor9.ForeColor = System.Drawing.SystemColors.Highlight;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor10_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x16, 3))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor10.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor11.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor12.ForeColor = System.Drawing.SystemColors.ControlText;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor11_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x16, 2))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor10.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor11.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor12.ForeColor = System.Drawing.SystemColors.ControlText;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor12_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x16, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor10.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor11.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor12.ForeColor = System.Drawing.SystemColors.Highlight;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor13_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x17, 3))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor13.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor14.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor15.ForeColor = System.Drawing.SystemColors.ControlText;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor14_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x17, 2))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor13.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor14.ForeColor = System.Drawing.SystemColors.Highlight;
                Factor15.ForeColor = System.Drawing.SystemColors.ControlText;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Factor15_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x17, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Factor13.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor14.ForeColor = System.Drawing.SystemColors.ControlText;
                Factor15.ForeColor = System.Drawing.SystemColors.Highlight;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void iSolar_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x19, 2))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SwitchOff.ForeColor = System.Drawing.SystemColors.ControlText;
                button1.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void SwitchOff_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x19, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SwitchOff.ForeColor = System.Drawing.SystemColors.ControlText;
                button1.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void soc_learn_on_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x20, 4))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                soc_learn_on.ForeColor = System.Drawing.SystemColors.ControlText;
                soc_learn_off.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void soc_learn_off_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x20, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                soc_learn_on.ForeColor = System.Drawing.SystemColors.ControlText;
                soc_learn_off.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void dsg_auto_on_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x21, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dsg_auto_on.ForeColor = System.Drawing.SystemColors.ControlText;
                dsg_auto_off.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void dsg_auto_off_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x21, 8))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dsg_auto_on.ForeColor = System.Drawing.SystemColors.ControlText;
                dsg_auto_off.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void chg_auto_on_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x22, 0))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                chg_auto_on.ForeColor = System.Drawing.SystemColors.ControlText;
                chg_auto_off.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void chg_auto_off_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Sw(0x22, 16))
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                chg_auto_on.ForeColor = System.Drawing.SystemColors.ControlText;
                chg_auto_off.ForeColor = System.Drawing.SystemColors.Highlight;
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }




        //


        public delegate void CommModemCallback(Byte[] frameData);
        public delegate void CommProgressDelegate(int total, int currVal);
        public delegate void CommThreadFinishDelegate(en_thread_number_t threadNum, en_trans_status_t threadSta, en_packet_cmd_t threadCmd);

        string m_downloadFileName = string.Empty;
        string m_uploadFileName = string.Empty;
        volatile bool m_transThreadFlag = false;    // Transmit thread running flag
        volatile en_trans_status_t m_transStatus;    // Transmit status trace
        Byte m_transNumber;     // Frame number
        Byte[] m_commRecvData = new Byte[(int)en_frame_para_t.FRAME_MAX_SIZE];
        UInt32 m_appFlashAddr = 0;

        int m_commPortNum;      // Total number of serial ports

        private byte[] _rxBuf = new byte[8192];
        private int _rxLen = 0;

        private int FindHead(byte[] buf, int len)
        {
            // FRAME_HEAD = 0xAC6D，发送时低字节在前：6D AC
            for (int i = 0; i <= len - 2; i++)
                if (buf[i] == 0x6D && buf[i + 1] == 0xAC) return i;
            return -1;
        }

        public enum en_thread_number_t
        {
            TransFileThead = 0x01,
            RecvFileThead = 0x02,
        }

        public enum en_trans_status_t
        {
            TransIdle = 0x00,
            TransBegin = 0x01,
            TransTimeout = 0x02,
            TransFinished = 0x03,
            TransFailed = 0x04,
            TransAbort = 0x05,
            TransAddrError = 0x06,
            TransFileInvalid = 0x07,
        }

        public enum en_packet_type_t
        {
            PACKET_TYPE_CONTROL = 0x11,
            PACKET_TYPE_DATA = 0x12,
        }

        public enum en_packet_cmd_t
        {
            PACKET_CMD_HANDSHAKE = 0x20,
            PACKET_CMD_JUMP_TO_APP = 0x21,
            PACKET_CMD_APP_DOWNLOAD = 0x22,
            PACKET_CMD_APP_UPLOAD = 0x23,
            PACKET_CMD_ERASE_FLASH = 0x24,
            PACKET_CMD_FLASH_CRC = 0x25,
            PACKET_CMD_APP_UPGRADE = 0x26,
        }

        public enum en_packet_status_t
        {
            PACKET_ACK_OK = 0x00,
            PACKET_ACK_ERROR = 0x01,
            PACKET_ACK_ABORT = 0x02,
            PACKET_ACK_TIMEOUT = 0x03,
            PACKET_ACK_ADDR_ERROR = 0x04,
        }

        public enum en_frame_para_t
        {
            FRAME_HEAD = 0xAC6D,
            FRAME_SHELL_SIZE = 8,
            FRAME_NUM_XOR_BYTE = 0xFF,
            FRAME_MAX_SIZE = (FRAME_SHELL_SIZE + en_packet_para_t.PACKET_MAX_SIZE),

            FRAME_HEAD_INDEX = 0x00,
            FRAME_NUM_INDEX = 0x02,
            FRAME_XORNUM_INDEX = 0x03,
            FRAME_LENGTH_INDEX = 0x04,
            FRAME_PACKET_INDEX = 0x06,
        }

        public enum en_packet_para_t
        {
            PACKET_INSTRUCT_SIZE = 10,
            PACKET_DATA_SIZE = 512,
            PACKET_MIN_SIZE = PACKET_INSTRUCT_SIZE,
            PACKET_MAX_SIZE = (PACKET_INSTRUCT_SIZE + PACKET_DATA_SIZE),

            PACKET_CMD_INDEX = (en_frame_para_t.FRAME_PACKET_INDEX + 0x00),
            PACKET_TYPE_INDEX = (en_frame_para_t.FRAME_PACKET_INDEX + 0x01),
            PACKET_RESULT_INDEX = (en_frame_para_t.FRAME_PACKET_INDEX + 0x01),
            PACKET_ADDRESS_INDEX = (en_frame_para_t.FRAME_PACKET_INDEX + 0x02),
            PACKET_DATA_INDEX = (en_frame_para_t.FRAME_PACKET_INDEX + PACKET_INSTRUCT_SIZE),
        }

        //
        public bool IsHexadecimal(string str)
        {
            //const string PATTERN = @"([A-F][a-f][0-9])+$";  // @"[A-Fa-f0-9]+$";
            const string PATTERN = @"[A-Fa-f0-9]+$";
            return System.Text.RegularExpressions.Regex.IsMatch(str, PATTERN);
        }

        public bool IsDecimal(string str)
        {
            const string PATTERN = @"[0-9]+$";
            return System.Text.RegularExpressions.Regex.IsMatch(str, PATTERN);
        }

        private string StringToHexString(string s, Encoding encode)
        {
            byte[] b = encode.GetBytes(s);  //按照指定编码将string编程字节数组
            string result = string.Empty;
            for (int i = 0; i < b.Length; i++) //逐字节变为16进制字符
            {
                result += Convert.ToString(b[i], 16);
            }
            return result;
        }

        public bool getHexString(String sourceStr, ref String[] getStr)
        {
            bool hexValid = false;

            int lower = sourceStr.IndexOf("0x");
            int upper = sourceStr.IndexOf("0X");

            if (lower >= 0)
            {
                getStr = sourceStr.Split('x');
                hexValid = true;
            }
            else if (upper >= 0)
            {
                getStr = sourceStr.Split('X');
                hexValid = true;
            }

            return hexValid;
        }

        public class IniFileHelper
        {
            // 声明INI文件的写操作函数 WritePrivateProfileString()
            [DllImport("kernel32")]             //返回0表示失败，非0为成功
            private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

            // 声明INI文件的读操作函数 GetPrivateProfileString()
            [System.Runtime.InteropServices.DllImport("kernel32")]      //返回取得字符串缓冲区的长度
            private static extern int GetPrivateProfileString(string section, string key, string def, System.Text.StringBuilder retVal, int size, string filePath);

            private string curFilePath = null;
            public IniFileHelper(string path)
            {
                this.curFilePath = path;
            }

            public bool WriteValue(string section, string key, string value, string filePath = null)
            {
                if (filePath == null)
                    filePath = this.curFilePath;

                if (File.Exists(filePath))
                {
                    // section,key,value,path
                    long opResult = WritePrivateProfileString(section, key, " " + value, filePath);
                    if (opResult == 0)
                        return true;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }

            public string ReadValue(string section, string key, string filePath = null)
            {
                if (filePath == null)
                    filePath = this.curFilePath;

                if (File.Exists(filePath))
                {
                    // read byte total
                    System.Text.StringBuilder temp = new System.Text.StringBuilder(1024);
                    // section,key,temp,path
                    GetPrivateProfileString(section, key, "", temp, 1024, filePath);
                    return temp.ToString();
                }
                else
                {
                    return string.Empty;
                }
            }
        }

      

        public void BackupIapConfig(string port, string bps, string addr, string path)
        {
            string exeRootDir = Directory.GetCurrentDirectory();
            string iniFilePath = exeRootDir + "\\IapConfig.ini";
            if (File.Exists(iniFilePath))
            {
                IniFileHelper iapConfig = new IniFileHelper(iniFilePath);

                string comPort = iapConfig.ReadValue("GENERAL", "ComPort");
                if ((comPort != string.Empty) && (comPort.Trim() != ""))
                {
                    if (comPort != port)
                    {
                        iapConfig.WriteValue("GENERAL", "ComPort", port);
                    }
                }
                string destAddr = iapConfig.ReadValue("GENERAL", "DestAddress");
                if ((destAddr != string.Empty) && (destAddr.Trim() != ""))
                {
                    if (destAddr != addr)
                    {
                        iapConfig.WriteValue("GENERAL", "DestAddress", addr);
                    }
                }
                string baudRate = iapConfig.ReadValue("GENERAL", "BaudRate");
                if ((baudRate != string.Empty) && (baudRate.Trim() != ""))
                {
                    if (baudRate != bps)
                    {
                        iapConfig.WriteValue("GENERAL", "BaudRate", bps);
                    }
                }
                string filePath = iapConfig.ReadValue("GENERAL", "FilePath");
                if ((filePath != string.Empty) && (filePath.Trim() != ""))
                {
                    if (filePath != path)
                    {
                        iapConfig.WriteValue("GENERAL", "FilePath", path);
                    }
                }
            }
        }


        /**
         * @brief  Cal CRC16 for Packet
         * @param  data
         * @param  length
         * @retval None
         */
        UInt16 Cal_CRC16(Byte[] p_data, int offset, UInt32 size)
        {
            Byte u8Cnt;
            UInt16 u16CrcResult = 0xA28C;
            UInt32 u32Offset = (UInt32)offset;

            while (size != 0)
            {
                u16CrcResult ^= p_data[u32Offset++];
                for (u8Cnt = 0; u8Cnt < 8; u8Cnt++)
                {
                    if ((u16CrcResult & 0x1) == 0x1)
                    {
                        u16CrcResult >>= 1;
                        u16CrcResult ^= 0x8408;
                    }
                    else
                    {
                        u16CrcResult >>= 1;
                    }
                }
                size--;
            }
            u16CrcResult = (UInt16)(~u16CrcResult);

            return u16CrcResult;
        }
        private Byte CheckSum(Byte[] pData, UInt16 offset, UInt16 len)
        {
            UInt16 i;
            Byte sum = 0;

            for (i = 0; i < len; i++)
            {
                sum += pData[i + offset];
            }

            return sum;
        }
        private void staStripUpdateInfo(string message)
        {
            if (!serialPort1.IsOpen)
            {
                // stsLabel.Text = message;
            }
            else
            {
                // stsLabel.Text = "串口关闭";
            }
        }

 

        public void CommProgress(int total, int currVal)
        {
            //判断是否需要进行唤醒的请求，如果控件与主线程在一个线程内，可以写成 if(!InvokeRequired)
            if (!this.prgBarTransSchedule.InvokeRequired)
            {
                float percent = (float)currVal / total;
                int perValue = (int)(percent * 100);

                lblTransSchedule.Text = perValue.ToString() + "%";
                prgBarTransSchedule.Maximum = total;
                prgBarTransSchedule.Value = currVal;
            }
            else
            {
                CommProgressDelegate otherThread = new CommProgressDelegate(CommProgress);
                this.BeginInvoke(otherThread, new object[] { total, currVal });     //执行唤醒操作
            }
        }

        private bool CommModemPackget(Byte cmd, Byte type, UInt32 addr, Byte[] data, UInt16 length, UInt16 timeout)
        {
            UInt16 index;
            Byte[] txData = new Byte[(int)en_frame_para_t.FRAME_MAX_SIZE];
            UInt16 u16Head = (UInt16)en_frame_para_t.FRAME_HEAD;
            UInt16 frameHeadLength = (UInt16)en_frame_para_t.FRAME_SHELL_SIZE - 2;   //Minus the final CRC
            UInt16 controlLength = (UInt16)en_packet_para_t.PACKET_INSTRUCT_SIZE;
            UInt16 packetHeadLength = (UInt16)(controlLength + frameHeadLength);
            UInt16 totalLength = (UInt16)(length + controlLength);
            UInt16 crc16;

            // Packet
            index = 0;
            txData[index++] = (Byte)(u16Head & 0x00FF);
            txData[index++] = (Byte)((u16Head >> 8) & 0x00FF);

            // Update the serial number after receiving is complete.
            txData[index++] = m_transNumber;
            txData[index++] = (Byte)(m_transNumber ^ (Byte)en_frame_para_t.FRAME_NUM_XOR_BYTE);
            txData[index++] = (Byte)(totalLength & 0x00FF);
            txData[index++] = (Byte)(totalLength >> 8);

            // Content of packet
            txData[index++] = cmd;
            txData[index++] = type;
            txData[index++] = (Byte)(addr & 0x000000FF);
            txData[index++] = (Byte)((addr >> 8) & 0x000000FF);
            txData[index++] = (Byte)((addr >> 16) & 0x000000FF);
            txData[index++] = (Byte)((addr >> 24) & 0x000000FF);
            for (int i = index; i < packetHeadLength; i++)
            {
                txData[i] = 0x00;
            }
            index = packetHeadLength;
            // Copy transfer buffer
            if (length != 0)
            {
                Buffer.BlockCopy(data, 0, txData, index, length);
            }
            index += length;

            // calculate  CRC16
            crc16 = Cal_CRC16(txData, frameHeadLength, totalLength);
            txData[index++] = (Byte)(crc16 & 0x00FF);
            txData[index++] = (Byte)(crc16 >> 8);

            System.Array.Clear(m_commRecvData, 0, m_commRecvData.Length);
            // Send to packet
            return CommModemSendData(txData, index, timeout);
        }

        //字符串转换16进制字节数组
        private byte[] strToHexByte(string hexString)
        {
            hexString = hexString.Replace(" ", "");
            if ((hexString.Length % 2) != 0)
            { hexString += " "; }
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
            { returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2).Replace(" ", ""), 16); }
            return returnBytes;
        }
        private void StopCommTimer()
        {
            //m_commTimer.Enabled = false;
            //m_commTimer.Stop();
        }

        private void StartCommTimer(UInt16 value)
        {
            //m_commTimer.Interval = 1000;
            //m_commTimer.Enabled = true;
            //m_commTimer.Start();
        }

        private bool CommModemSendData(Byte[] transStr, UInt16 length, UInt16 timeout)
        {

            if (false == serialPort1.IsOpen)
            {
                MessageBox.Show("请先打开串口", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                serialPort1.DiscardInBuffer();

                serialPort1.Write(transStr, 0, length);
            }
            catch (Exception ex)
            {
                return false;
            }
            //启动响应超时计数
            StartCommTimer(timeout);
            m_transStatus = en_trans_status_t.TransBegin;

            return true;
       

           
        }
       // private void comPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
       //// private void comPort_DataReceived()
       // {
       //     int cpIndex = 0;
       //     int recvDelay, readCnt;

       //     int bufferSize;
       //     int frameMaxLen = (int)en_frame_para_t.FRAME_MAX_SIZE;
       //     Byte[] ReDatas = new Byte[frameMaxLen];
       //     Byte[] ReceiveData = new Byte[frameMaxLen];
       //     int dataLength = 0, frameHead = 0;
       //     UInt16 crc16;
       //     Thread.Sleep(500);
       //     bufferSize = serialPort1.BytesToRead;
       //     if (bufferSize > frameMaxLen)
       //     {
       //         serialPort1.Read(ReDatas, 0, frameMaxLen);
       //         return;         //丢弃
       //     }

       //     recvDelay = 5;
       //     while (recvDelay > 0)
       //     {
       //         if (bufferSize > 0)
       //         { readCnt = serialPort1.Read(ReDatas, 0, bufferSize); }
       //         else
       //         { readCnt = 0; }

       //         if (readCnt > 0)
       //         {
       //             if ((bufferSize + cpIndex) > frameMaxLen)
       //             { return; }        //丢弃

       //             Buffer.BlockCopy(ReDatas, 0, ReceiveData, cpIndex, bufferSize);
       //             cpIndex += bufferSize;
       //             recvDelay = 5;
       //         }
       //         else
       //         {
       //             recvDelay--;
       //             Thread.Sleep(1);
       //         }
       //         bufferSize = serialPort1.BytesToRead;
       //     }

       //     if (cpIndex != 0) //no empty
       //     {
       //         //packet
       //         frameHead = ReceiveData[(int)en_frame_para_t.FRAME_HEAD_INDEX] + (ReceiveData[(int)en_frame_para_t.FRAME_HEAD_INDEX + 1] << 8);
       //         if (en_frame_para_t.FRAME_HEAD == (en_frame_para_t)frameHead)
       //         {
       //             if (ReceiveData[(int)en_frame_para_t.FRAME_NUM_INDEX] == (ReceiveData[(int)en_frame_para_t.FRAME_XORNUM_INDEX] ^ (Byte)en_frame_para_t.FRAME_NUM_XOR_BYTE))
       //             {
       //                 dataLength = ReceiveData[(int)en_frame_para_t.FRAME_LENGTH_INDEX] + (ReceiveData[(int)en_frame_para_t.FRAME_LENGTH_INDEX + 1] << 8);
       //                 if ((dataLength >= (int)en_packet_para_t.PACKET_MIN_SIZE) && (dataLength <= (int)en_packet_para_t.PACKET_MAX_SIZE))
       //                 {
       //                     crc16 = (UInt16)(ReceiveData[(int)en_frame_para_t.FRAME_PACKET_INDEX + dataLength] + (ReceiveData[(int)en_frame_para_t.FRAME_PACKET_INDEX + dataLength + 1] << 8));
       //                     if (crc16 == Cal_CRC16(ReceiveData, (int)en_frame_para_t.FRAME_PACKET_INDEX, (UInt32)dataLength))
       //                     {
       //                         m_transNumber++;
       //                         if (m_transNumber == 0)
       //                         { m_transNumber = 1; }
       //                         Buffer.BlockCopy(ReceiveData, 0, m_commRecvData, 0, cpIndex - 2);
       //                         this.BeginInvoke(new CommModemCallback(CommModemHandler), new object[] { m_commRecvData });
       //                     }
       //                 }

       //             }
       //         }
       //     }
       // }

        private void tmrPortChackHandle(object sender, EventArgs e)
        {
           string[] ports = SerialPort.GetPortNames();
                if (ports.Length != m_commPortNum)
                {
                    m_commPortNum = ports.Length;
                    if (false == ((IList)ports).Contains(serialPort1.PortName))
                    {
                        // 同样用方法名取消订阅
                      //  serialPort1.DataReceived -= comPort_DataReceived;
                        try
                        {
                            serialPort1.Close();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                      
                    }
                    cboPorts_DropDown(sender, e);
                }
            
        }

        private void CommModemHandler(Byte[] frameData)
        {
            en_packet_status_t retSta;
            en_trans_status_t transSta;

            retSta = (en_packet_status_t)frameData[(int)en_packet_para_t.PACKET_RESULT_INDEX];
            if (m_transStatus == en_trans_status_t.TransBegin)
            {
                StopCommTimer();
                switch (retSta)
                {
                    case en_packet_status_t.PACKET_ACK_OK:
                        transSta = en_trans_status_t.TransFinished;
                        break;
                    case en_packet_status_t.PACKET_ACK_ERROR:
                        transSta = en_trans_status_t.TransFailed;
                        break;
                    case en_packet_status_t.PACKET_ACK_ADDR_ERROR:
                        transSta = en_trans_status_t.TransAddrError;
                        break;
                    default:
                        transSta = en_trans_status_t.TransFailed;
                        break;
                }
                m_transStatus = transSta;
            }
        }

        public class CustomComparer : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                string s1 = (string)x;
                string s2 = (string)y;
                if (s1.Length > s2.Length)
                {
                    return 1;
                }
                if (s1.Length < s2.Length)
                {
                    return -1;
                }
                for (int i = 0; i < s1.Length; i++)
                {
                    if (s1[i] > s2[i])
                    { return 1; }
                    if (s1[i] < s2[i])
                    { return -1; }
                }

                return 0;
            }
        }

        private void cboPorts_DropDown(object sender, EventArgs e)
        {
            string str = null;

            if (cboPortName.Items.Count > 0)
            { str = cboPortName.SelectedItem.ToString(); }
            //get serial port
            cboPortName.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length != 0)
            {
                System.Array.Sort(ports, new CustomComparer());
                foreach (string port in ports)
                {
                    cboPortName.Items.Add(port);
                }

                if (str == null)
                { cboPortName.SelectedIndex = 0; }
                else
                {
                    if (false == ((IList)ports).Contains(str))
                    { cboPortName.SelectedIndex = 0; }
                    else
                    { cboPortName.SelectedIndex = System.Array.IndexOf(ports, str); }
                }
            }
        }
        public void CommThreadFinish(en_thread_number_t threadNum, en_trans_status_t threadSta, en_packet_cmd_t threadCmd)
        {
            //判断是否需要进行唤醒的请求，如果控件与主线程在一个线程内，可以写成 if(!InvokeRequired)
            if (!this.btnBrowserFile.InvokeRequired)
            {
                string title = null, command = null;

                if (threadNum == en_thread_number_t.TransFileThead)
                {
                    btnReadInfo.Enabled = true;
                    btnBrowserFile.Enabled = true;
                    btnWriteInfo.Text = "下载";
                }
                else if (threadNum == en_thread_number_t.RecvFileThead)
                {
                    btnWriteInfo.Enabled = true;
                    btnBrowserFile.Enabled = true;
                    btnReadInfo.Text = "上传";
                }

                if (threadNum == en_thread_number_t.TransFileThead)
                {
                    title = "下载";
                }
                else if (threadNum == en_thread_number_t.RecvFileThead)
                {
                    title = "上传";
                }

                switch (threadCmd)
                {
                    case en_packet_cmd_t.PACKET_CMD_HANDSHAKE:
                        command = "握手";
                        break;
                    case en_packet_cmd_t.PACKET_CMD_JUMP_TO_APP:
                        command = "跳转";
                        break;
                    case en_packet_cmd_t.PACKET_CMD_APP_DOWNLOAD:
                        command = "下载";
                        break;
                    case en_packet_cmd_t.PACKET_CMD_APP_UPLOAD:
                        command = "上传";
                        break;
                    case en_packet_cmd_t.PACKET_CMD_ERASE_FLASH:
                        command = "擦除Flash";
                        break;
                    case en_packet_cmd_t.PACKET_CMD_FLASH_CRC:
                        command = "Flash校验";
                        break;
                    case en_packet_cmd_t.PACKET_CMD_APP_UPGRADE:
                        command = "APP升级";
                        break;
                    default:
                        break;
                }

                switch (threadSta)
                {
                    case en_trans_status_t.TransFinished:
                        staStripUpdateInfo(title + "完成！");
                        break;
                    case en_trans_status_t.TransTimeout:
                        staStripUpdateInfo(title + "程序," + command + "超时," + "请检查设备及连接线是否正常!");
                        break;
                    case en_trans_status_t.TransFileInvalid:
                        staStripUpdateInfo(title + "程序," + "请选择有效的文件!");
                        break;
                    case en_trans_status_t.TransAddrError:
                        staStripUpdateInfo(title + "程序," + command + "地址错误" + "请输入有效地址值!");
                        break;
                    case en_trans_status_t.TransFailed:
                        staStripUpdateInfo(title + "程序," + command + "失败" + "请检查参数是否合法!");
                        break;
                    case en_trans_status_t.TransAbort:
                        staStripUpdateInfo(title + "程序," + "终止!");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                CommThreadFinishDelegate otherThread = new CommThreadFinishDelegate(CommThreadFinish);
                this.BeginInvoke(otherThread, new object[] { threadNum, threadSta, threadCmd });
            }
        }
        // 新增：发送后等待一帧有效响应（同步轮询，不用 DataReceived 事件）
        private bool CommModemWaitResponse(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs && m_transThreadFlag)
            {
                int n = serialPort1.BytesToRead;
                if (n <= 0) { Thread.Sleep(1); continue; }

                // 读入临时缓冲
                byte[] tmp = new byte[Math.Min(n, 1024)];
                int r = serialPort1.Read(tmp, 0, tmp.Length);
                if (r <= 0) continue;

                // 追加到缓存
                if (_rxLen + r > _rxBuf.Length)
                {
                    // 缓存溢出，直接清掉（或做更复杂的滑动窗口）
                    _rxLen = 0;
                }
                Buffer.BlockCopy(tmp, 0, _rxBuf, _rxLen, r);
                _rxLen += r;

                // 在缓存里找帧头并尝试解析
                while (true)
                {
                    int head = FindHead(_rxBuf, _rxLen);
                    if (head < 0)
                    {
                        // 没帧头：为了避免缓存无限长，保留最后1字节（可能是 0x6D）
                        if (_rxLen > 1) { _rxBuf[0] = _rxBuf[_rxLen - 1]; _rxLen = 1; }
                        break;
                    }

                    // 头前面的垃圾丢掉
                    if (head > 0)
                    {
                        Buffer.BlockCopy(_rxBuf, head, _rxBuf, 0, _rxLen - head);
                        _rxLen -= head;
                    }

                    // 至少要有 6 字节头部
                    if (_rxLen < (int)en_frame_para_t.FRAME_PACKET_INDEX) break;

                    int dataLength =
                        _rxBuf[(int)en_frame_para_t.FRAME_LENGTH_INDEX] |
                        (_rxBuf[(int)en_frame_para_t.FRAME_LENGTH_INDEX + 1] << 8);

                    if (dataLength < (int)en_packet_para_t.PACKET_MIN_SIZE || dataLength > (int)en_packet_para_t.PACKET_MAX_SIZE)
                    {
                        // 长度不合理：丢掉一个字节，继续找下一个帧头
                        Buffer.BlockCopy(_rxBuf, 1, _rxBuf, 0, _rxLen - 1);
                        _rxLen -= 1;
                        continue;
                    }

                    int fullFrameLen = (int)en_frame_para_t.FRAME_PACKET_INDEX + dataLength + 2;
                    if (_rxLen < fullFrameLen) break; // 还没收齐

                    // 校验 num/xor
                    byte num = _rxBuf[(int)en_frame_para_t.FRAME_NUM_INDEX];
                    byte xornum = _rxBuf[(int)en_frame_para_t.FRAME_XORNUM_INDEX];
                    if (num != (byte)(xornum ^ (byte)en_frame_para_t.FRAME_NUM_XOR_BYTE))
                    {
                        Buffer.BlockCopy(_rxBuf, 1, _rxBuf, 0, _rxLen - 1);
                        _rxLen -= 1;
                        continue;
                    }

                    UInt16 crc16 = (UInt16)(_rxBuf[(int)en_frame_para_t.FRAME_PACKET_INDEX + dataLength] |
                                           (_rxBuf[(int)en_frame_para_t.FRAME_PACKET_INDEX + dataLength + 1] << 8));

                    UInt16 cal = Cal_CRC16(_rxBuf, (int)en_frame_para_t.FRAME_PACKET_INDEX, (UInt32)dataLength);
                    if (crc16 != cal)
                    {
                        Buffer.BlockCopy(_rxBuf, 1, _rxBuf, 0, _rxLen - 1);
                        _rxLen -= 1;
                        continue;
                    }

                    // 帧有效：拷贝给原逻辑处理（保持你 CommModemHandler 逻辑）
                    System.Array.Clear(m_commRecvData, 0, m_commRecvData.Length);
                    Buffer.BlockCopy(_rxBuf, 0, m_commRecvData, 0, fullFrameLen - 2);

                    // 消费掉这一帧
                    Buffer.BlockCopy(_rxBuf, fullFrameLen, _rxBuf, 0, _rxLen - fullFrameLen);
                    _rxLen -= fullFrameLen;

                    m_transNumber++;
                    if (m_transNumber == 0) m_transNumber = 1;

                    CommModemHandler(m_commRecvData);
                    return true;
                }
            }

            if (m_transStatus == en_trans_status_t.TransBegin)
                m_transStatus = en_trans_status_t.TransTimeout;

            return false;
        }

        public void TransThreadCallback()
        {
            Byte[] txData = new Byte[5];
            en_packet_cmd_t transProcess;
            UInt32 flashAddr = m_appFlashAddr;
            en_trans_status_t threadSta = en_trans_status_t.TransIdle;
            Byte[] filePtr = null;
            int fileLength = 0, fileIndex = 0, transFileSize = 0;
            Byte[] transBuffer = new Byte[(int)en_packet_para_t.PACKET_DATA_SIZE];
            int downTotalEntries = 0, downProgressVal = 0;
            bool txResult = false;
            UInt16 crc16 = 0;

            //Get file
            int fileTypeIndex = m_downloadFileName.LastIndexOf(".");
            String fileType = m_downloadFileName.Substring(fileTypeIndex, m_downloadFileName.Length - fileTypeIndex);
            if (fileType == ".bin")
            {
                FileStream fs = new FileStream(m_downloadFileName, FileMode.Open, FileAccess.Read);
                fileLength = (int)fs.Length;
                filePtr = new Byte[fileLength];
                fs.Read(filePtr, 0, filePtr.Length);
                fs.Close();
            }
            else if (fileType == ".hex")
            {
                UInt32 addr = 0;
                HexToBin hextobin = new HexToBin();
                if (HexToBin.ExecResult.ExecOk == hextobin.HEX_ConvertBin(m_downloadFileName, ref addr, ref filePtr))
                {
                    fileLength = filePtr.Length;
                    /*flashAddr = addr;*/
                }
            }
            else if (fileType == ".srec")
            {
                UInt32 addr = 0;
                HexToBin srectobin = new HexToBin();
                if (HexToBin.ExecResult.ExecOk == srectobin.SREC_ConvertBin(m_downloadFileName, ref addr, ref filePtr))
                {
                    fileLength = filePtr.Length;
                    /*flashAddr = addr;*/
                }
            }

            // Transmit data decompose
            if (fileLength == 0)
            {
                m_transThreadFlag = false;
                threadSta = en_trans_status_t.TransFileInvalid;
            }
            else
            {
                transFileSize = fileLength;
                downTotalEntries = fileLength / (int)en_packet_para_t.PACKET_DATA_SIZE;
                if ((fileLength % (int)en_packet_para_t.PACKET_DATA_SIZE) != 0)
                { downTotalEntries += 1; }
            }

            transProcess = en_packet_cmd_t.PACKET_CMD_APP_UPGRADE;
            CommProgress(100, 0);
            while (m_transThreadFlag)
            {
                switch (transProcess)
                {
                    case en_packet_cmd_t.PACKET_CMD_HANDSHAKE:
                        {
                            txResult = CommModemPackget(
                                (Byte)en_packet_cmd_t.PACKET_CMD_HANDSHAKE,
                                (Byte)en_packet_type_t.PACKET_TYPE_CONTROL,
                                0,
                                null,
                                0,
                                1000);
                            if (txResult)
                            {
                                // while (m_transStatus == en_trans_status_t.TransBegin) ;
                                CommModemWaitResponse(1000);
                                if (m_transStatus == en_trans_status_t.TransFinished)
                                {
                                    transProcess = en_packet_cmd_t.PACKET_CMD_ERASE_FLASH;
                                }
                                
                            }
                            else
                            {
                                m_transStatus = en_trans_status_t.TransFailed;
                                StopCommTimer();
                            }
                        }
                        break;
                    case en_packet_cmd_t.PACKET_CMD_ERASE_FLASH:
                        {
                            transBuffer[0] = (Byte)(transFileSize & 0x000000ff);
                            transBuffer[1] = (Byte)((transFileSize >> 8) & 0x000000ff);
                            transBuffer[2] = (Byte)((transFileSize >> 16) & 0x000000ff);
                            transBuffer[3] = (Byte)((transFileSize >> 24) & 0x000000ff);
                            txResult = CommModemPackget(
                                (Byte)en_packet_cmd_t.PACKET_CMD_ERASE_FLASH,
                                (Byte)en_packet_type_t.PACKET_TYPE_DATA,
                                flashAddr,
                                transBuffer,
                                4,
                                5000);
                            if (txResult)
                            {
                                CommModemWaitResponse(2000);
                                if (m_transStatus == en_trans_status_t.TransFinished)
                                {
                                    transProcess = en_packet_cmd_t.PACKET_CMD_APP_DOWNLOAD;
                                    // Initialize download parameter
                                    downProgressVal = 0;
                                    fileIndex = 0;
                                    Thread.Sleep(500);
                                }
                            }
                            else
                            {
                                m_transStatus = en_trans_status_t.TransFailed;
                                StopCommTimer();
                            }
                        }
                        break;
                    case en_packet_cmd_t.PACKET_CMD_APP_DOWNLOAD:
                        {
                            if (fileLength > (int)en_packet_para_t.PACKET_DATA_SIZE)
                            {
                                Buffer.BlockCopy(filePtr, fileIndex, transBuffer, 0, (int)en_packet_para_t.PACKET_DATA_SIZE);
                                txResult = CommModemPackget((Byte)en_packet_cmd_t.PACKET_CMD_APP_DOWNLOAD,
                                                            (Byte)en_packet_type_t.PACKET_TYPE_DATA, flashAddr, transBuffer,
                                                            (UInt16)en_packet_para_t.PACKET_DATA_SIZE, 2000);
                            }
                            else
                            {
                                Buffer.BlockCopy(filePtr, fileIndex, transBuffer, 0, fileLength);
                                txResult = CommModemPackget((Byte)en_packet_cmd_t.PACKET_CMD_APP_DOWNLOAD,
                                                            (Byte)en_packet_type_t.PACKET_TYPE_DATA, flashAddr, transBuffer,
                                                            (UInt16)fileLength, 2000);
                            }
                            if (txResult)
                            {
                                CommModemWaitResponse(2000);
                                if (m_transStatus == en_trans_status_t.TransFinished)
                                {
                                    if (fileLength > (int)en_packet_para_t.PACKET_DATA_SIZE)
                                    {
                                        fileIndex += (int)en_packet_para_t.PACKET_DATA_SIZE;
                                        fileLength -= (int)en_packet_para_t.PACKET_DATA_SIZE;
                                        flashAddr += (int)en_packet_para_t.PACKET_DATA_SIZE;
                                        downProgressVal++;
                                        CommProgress(downTotalEntries, downProgressVal);
                                    }
                                    else
                                    {
                                        fileLength = 0;
                                        transProcess = en_packet_cmd_t.PACKET_CMD_FLASH_CRC;
                                    }
                                }
                            }
                            else
                            {
                                m_transStatus = en_trans_status_t.TransFailed;
                                StopCommTimer();
                            }
                        }
                        break;
                    case en_packet_cmd_t.PACKET_CMD_JUMP_TO_APP:
                        {
                            txResult = CommModemPackget((Byte)en_packet_cmd_t.PACKET_CMD_JUMP_TO_APP,
                                                        (Byte)en_packet_type_t.PACKET_TYPE_CONTROL,
                                                        0, null, 0, 1000);
                            if (txResult)
                            {
                                CommModemWaitResponse(1000);
                                if (m_transStatus == en_trans_status_t.TransFinished)
                                {
                                    CommProgress(downTotalEntries, downTotalEntries);
                                    threadSta = en_trans_status_t.TransFinished;
                                }
                            }
                            else
                            {
                                m_transStatus = en_trans_status_t.TransFailed;
                                StopCommTimer();
                            }
                        }
                        break;
                    case en_packet_cmd_t.PACKET_CMD_FLASH_CRC:
                        {
                            transBuffer[0] = (Byte)(transFileSize & 0x000000ff);
                            transBuffer[1] = (Byte)((transFileSize >> 8) & 0x000000ff);
                            transBuffer[2] = (Byte)((transFileSize >> 16) & 0x000000ff);
                            transBuffer[3] = (Byte)((transFileSize >> 24) & 0x000000ff);
                            txResult = CommModemPackget((Byte)en_packet_cmd_t.PACKET_CMD_FLASH_CRC,
                                                        (Byte)en_packet_type_t.PACKET_TYPE_DATA,
                                                        m_appFlashAddr, transBuffer, 4, 3000);
                            if (txResult)
                            {
                                CommModemWaitResponse(3000);
                                if (m_transStatus == en_trans_status_t.TransFinished)
                                {
                                    crc16 = (UInt16)(m_commRecvData[(int)en_packet_para_t.PACKET_DATA_INDEX] +
                                                    (m_commRecvData[(int)en_packet_para_t.PACKET_DATA_INDEX + 1] << 8));
                                    if (crc16 == Cal_CRC16(filePtr, 0, (UInt32)transFileSize))
                                    {
                                        transProcess = en_packet_cmd_t.PACKET_CMD_JUMP_TO_APP;
                                    }
                                    else
                                    {
                                        m_transStatus = en_trans_status_t.TransFailed;
                                    }
                                }
                            }
                            else
                            {
                                m_transStatus = en_trans_status_t.TransFailed;
                                StopCommTimer();
                            }
                        }
                        break;
                    case en_packet_cmd_t.PACKET_CMD_APP_UPGRADE:
                        {

                            //for (int i = 0; i < 5; i++)
                            //{
                            //    txData[i] = 0xAA;
                            //}
                            //CommModemSendData(txData, 5, 1000);
                           // Thread.Sleep(1000);

                            txResult = CommModemPackget((Byte)en_packet_cmd_t.PACKET_CMD_APP_UPGRADE,
                                                        (Byte)en_packet_type_t.PACKET_TYPE_CONTROL, 0, null, 0, 2000);
                            if (txResult)
                            {
                                CommModemWaitResponse(2000);
                                if (m_transStatus == en_trans_status_t.TransFinished)
                                {
                                    transProcess = en_packet_cmd_t.PACKET_CMD_HANDSHAKE;
                                    Thread.Sleep(5000);       /* Wait for MCU reset */
                                }
                            }
                            else
                            {
                                m_transStatus = en_trans_status_t.TransFailed;
                                StopCommTimer();
                            }
                        }
                        break;
                    default:
                        break;
                }

                // error
                if ((m_transStatus == en_trans_status_t.TransTimeout) ||
                    (m_transStatus == en_trans_status_t.TransFailed) ||
                    (m_transStatus == en_trans_status_t.TransAbort) ||
                    (m_transStatus == en_trans_status_t.TransAddrError))
                {
                    threadSta = m_transStatus;
                    break;
                }
                // finished
                if (threadSta == en_trans_status_t.TransFinished)
                {
                    break;
                }
            }
            /* Stop timer when thread abort */
            //if (m_commTimer.Enabled == true)
            //{
            //    StopCommTimer();
            //}
            m_transThreadFlag = false;
            CommThreadFinish(en_thread_number_t.TransFileThead, threadSta, transProcess);
        }


        public void RecvThreadCallback()
        {
            while (m_transThreadFlag)
            {
                break;
            }

            // result of execution
            if (m_transThreadFlag)  // failed
            {
                m_transThreadFlag = false;
                MessageBox.Show("上传失败！");
            }
            else
            {
                MessageBox.Show("上传完成！");
            }

            //CommThreadFinish(en_thread_number_t.RecvFileThead);
        }

        

        private void btnBrowserFile_Click_1(object sender, EventArgs e)
        {
            string filePath = string.Empty;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = "hex";
            dlg.RestoreDirectory = true;
            // dlg.Filter = "Bin Files|*.bin|Hex Files|*.hex|Srec Files|*.srec";
            dlg.Filter = "Hex Files|*.hex|Bin Files|*.bin|Srec Files|*.srec";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                filePath = dlg.FileName;
                if (filePath != "")
                {
                    // Save file name
                    txtFilePath.Text = filePath;
                }
            }
        }

        private void btnWriteInfo_Click_1(object sender, EventArgs e)
        {
          

            if (btnWriteInfo.Text == "下载")
            {
                if (false == serialPort1.IsOpen)
                {
                    MessageBox.Show("请先打开串口", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (txtDestAddress.Text.Trim() == "")
                {
                    MessageBox.Show("请设置目标地址值！");
                    return;
                }
                else
                {
                    String[] strAddrBuffer = null;

                    String strAddr = txtDestAddress.Text.ToString();
                    strAddr = strAddr.Replace(" ", "");
                    if ((getHexString(strAddr, ref strAddrBuffer) == true) && (IsHexadecimal(strAddrBuffer[1]) == true))
                    { m_appFlashAddr = Convert.ToUInt32(strAddrBuffer[1], 16); }
                    else if (IsDecimal(strAddr) == true)
                    { m_appFlashAddr = UInt32.Parse(strAddr); }
                    else
                    {
                        MessageBox.Show("请输入有效的目标地址值！");
                        return;
                    }
                }

                if (txtFilePath.Text.Trim() == "")
                {
                    MessageBox.Show("请设置有效的文件路径！");
                    return;
                }
                else
                {
                    String filePath = txtFilePath.Text.ToString();
                    if (File.Exists(filePath))
                    {
                        m_downloadFileName = filePath;
                        //Git file
                        int fileTypeIndex = m_downloadFileName.LastIndexOf(".");
                        String fileType = m_downloadFileName.Substring(fileTypeIndex, m_downloadFileName.Length - fileTypeIndex);
                        if ((fileType == ".bin") || (fileType == ".hex") || (fileType == ".srec"))
                        {
                            // backup download history
                            string port = cboPortName.SelectedItem.ToString();
                            string bps = comboBAUD.SelectedItem.ToString();
                            string addr = txtDestAddress.Text.ToString();
                            string path = txtFilePath.Text.ToString();
                            BackupIapConfig(port, bps, addr, path);
                        }
                        else
                        {
                            MessageBox.Show("请设置有效的文件类型！");
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("请设置有效的文件路径！");
                        return;
                    }
                }

                btnReadInfo.Enabled = false;
                btnBrowserFile.Enabled = false;
                m_transNumber = 1;
                m_transThreadFlag = true;
                m_transStatus = en_trans_status_t.TransIdle;
                btnWriteInfo.Text = "停止";
                staStripUpdateInfo("开始下载");
                Thread transThread = new Thread(new ThreadStart(TransThreadCallback));
                transThread.IsBackground = true;
                transThread.Start();
            }
            else if (btnWriteInfo.Text == "停止")
            {
                btnReadInfo.Enabled = true;
                btnBrowserFile.Enabled = true;
                m_transThreadFlag = false;
                m_transStatus = en_trans_status_t.TransAbort;
                btnWriteInfo.Text = "下载";
                staStripUpdateInfo("终止下载");
            }
        }

        private void btnPwmDsgCurrentCali_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!string.IsNullOrEmpty(PwmChgCur.Text))
            {
                try
                {
                    int tmp = Convert.ToInt32(PwmChgCur.Text);
                    if (tmp <= 0)
                    {
                        MessageBox.Show(null, "请输入正整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(null, "输入数据非法", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (Cali_Pwm_Chg_Current())
                {
                    MessageBox.Show(null, "校准成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(null, "校准失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show(null, "输入数据非法,请输入正整数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OccRelease_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

 

        private void paraReflash_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Read_Para_Data())
            {
                MessageBox.Show(null, "刷新成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;
            }
            else
            {
                MessageBox.Show(null, "刷新失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void paraSet_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show(null, "请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (Write_Para_Data())
            {
                MessageBox.Show(null, "设置成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //  groupBoxAfe.Visible = true;

            }
            else
            {
                MessageBox.Show(null, "设置失败", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label30_Click(object sender, EventArgs e)
        {

        }

 



 
    }
}
 
 
