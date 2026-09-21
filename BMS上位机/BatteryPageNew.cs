using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VBPP = Microsoft.VisualBasic.PowerPacks;

namespace BMS上位机
{
    // ==================================================================================
    //  新版「电池信息」页 —— 整页控件化 + Dock 布局（2026-09-18）
    //
    //  三条原则：
    //   1) 新界面**只读取**原有控件的值来显示（cell_N / BLAN_N / vPack… / NTCn / Faut* / MOS 形状 / SOCIN…），
    //      原来的串口解析与刷新代码一行都不改 → 功能风险最低；旧控件整体搬进隐藏容器，随时可回退。
    //   2) 布局全部用 Dock + 百分比列宽 → 最大化 / 换分辨率 / 换 DPI 全部自动伸缩，不再需要手写缩放。
    //   3) 回退开关：把 USE_NEW_BATT_PAGE 改成 false，即恢复旧的"背景图 + 绝对坐标"版本。
    //
    //  四列：①电芯表 ②电池状态(参数 + SOC + 电池图形 + NTC 温度 + 放电/充电 MOS)
    //        ③光伏和市电状态(形象图 + 常显 MOS + 参数) ④保护标志
    //  另外两项外壳改动：
    //    · StyleTabs()          —— 顶部标签条改自绘（36px，选中白底+顶部蓝条，三态）
    //    · BuildTopRightActions() —— 「保存到文件」胶囊开关 +「网页互动」科技按钮搬到窗口右上角
    //      （原来挤在③列/④列底部的 54px 行里，那行已删除；生产日期不再上屏）
    // ==================================================================================
    public partial class Form1
    {
        private static readonly bool USE_NEW_BATT_PAGE = true;

        private bool _bpBuilt;
        private Panel _bpOldHolder;                 // 旧控件（背景图那一套）搬进来隐藏
        private TableLayoutPanel _bpRoot;           // 四列根布局
        private DataGridView _bpCells;              // ①电芯表
        private DataGridView _bpBattParams;         // ②电池参数
        private DataGridView _bpPowerParams;        // ③光伏 / 市电参数
        private DataGridView _bpFaults;             // ④保护标志
        private readonly bool[] _bpFaultOn = new bool[13];   // ④保护标志：每行是否有告警
        private Label _bpSocPct, _bpChgDsg;
        private TableLayoutPanel _bpTempRow;                  // NTC 温度行（居中容器）
        private TableLayoutPanel _bpTempInner;                // 温度行内容（AutoSize）
        private readonly Label[] _bpTempVals = new Label[8];   // 各路温度数值标签（刷新只改这几个）
        private int _bpTempN = -1;                            // 温度行已生成的路数
        private MosView _bpDsgMos, _bpChgMos, _bpMainMos, _bpPwmMos;
        private IconView _bpSolarIcon, _bpGridIcon;           // ③列：光伏 / 市电 形象图
        private TableLayoutPanel _bpDbgRow;         // 调试 MOS 行（市电MOS / PWMMOS）
        private BatteryView _bpBatt;
        // 电芯表的行定义：Kind 0=电芯 2=调试（25~36）
        private class RowDef
        {
            public int Kind;
            public int Idx;
            public string Num;
        }
        private readonly List<RowDef> _bpRows = new List<RowDef>();

        // 数据来源（旧控件 / 形状），建页时缓存一次
        private Label[] _srcCells;
        private Label[] _srcNtc;
        private VBPP.OvalShape[] _srcBlans;
        private VBPP.OvalShape[] _srcFauts;

        // ② 电池参数（11 项）
        private static readonly string[] BATT_PARAM_NAMES = new string[]
        {
            "电池总电压", "平均电压", "最高电压", "最低电压", "最大压差", "电流",
            "满充容量", "剩余容量", "SOC", "循环次数", "软件版本"
        };
        private static readonly string[] BATT_PARAM_UNITS = new string[]
        {
            "mV", "mV", "mV", "mV", "mV", "mA", "mAh", "mAh", "%", "次", ""
        };
        // ③ 光伏 / 市电参数（5 项）
        private static readonly string[] POWER_PARAM_NAMES = new string[]
        {
            "光伏电压", "光伏电流", "光伏功率", "市电电流", "负载功率"
        };
        private static readonly string[] POWER_PARAM_UNITS = new string[]
        {
            "mV", "mA", "W", "mA", "W"
        };
        private static readonly string[] FAULT_NAMES = new string[]
        {
            "单节过压", "单节欠压", "充电高温", "充电低温", "放电高温", "放电低温",
            "充电过流", "放电过流", "短路保护", "断线保护", "MOS超温", "AFE错误", "FLASH错误"
        };

        // ---------------------------------------------------------------- 建页
        private void BuildBatteryPage()
        {
            if (_bpBuilt) return;
            if (电池信息 == null) return;
            _bpBuilt = true;
            try
            {
                // 1) 旧控件（含 shapeContainer1 与背景图那一套）整体搬进隐藏容器
                _bpOldHolder = new Panel();
                _bpOldHolder.Name = "_bpOldHolder";
                _bpOldHolder.Visible = false;
                _bpOldHolder.Size = new Size(8, 8);
                _bpOldHolder.Location = new Point(-6000, -6000);
                this.Controls.Add(_bpOldHolder);

                List<Control> old = new List<Control>();
                foreach (Control c in 电池信息.Controls) old.Add(c);
                for (int i = 0; i < old.Count; i++) _bpOldHolder.Controls.Add(old[i]);

                // 2) 不再使用背景图（旧的自绘缩放逻辑随之失效，也不再调用 InitArtLayout）
                电池信息.BackgroundImage = null;
                电池信息.BackgroundImageLayout = ImageLayout.None;

                // 3) 数据来源缓存
                _srcCells = new Label[]
                {
                    cell_1, cell_2, cell_3, cell_4, cell_5, cell_6, cell_7, cell_8, cell_9, cell_10,
                    cell_11, cell_12, cell_13, cell_14, cell_15, cell_16, cell_17, cell_18, cell_19, cell_20,
                    cell_21, cell_22, cell_23, cell_24, cell_25, cell_26, cell_27, cell_28, cell_29, cell_30,
                    cell_31, cell_32, cell_33, cell_34, cell_35, cell_36, cell_37, cell_38, cell_39, cell_40
                };
                _srcBlans = new VBPP.OvalShape[]
                {
                    BLAN_1, BLAN_2, BLAN_3, BLAN_4, BLAN_5, BLAN_6, BLAN_7, BLAN_8, BLAN_9, BLAN_10,
                    BLAN_11, BLAN_12, BLAN_13, BLAN_14, BLAN_15, BLAN_16, BLAN_17, BLAN_18, BLAN_19, BLAN_20,
                    BLAN_21, BLAN_22, BLAN_23, BLAN_24, BLAN_25, BLAN_26, BLAN_27, BLAN_28, BLAN_29, BLAN_30,
                    BLAN_31, BLAN_32, BLAN_33, BLAN_34, BLAN_35, BLAN_36, BLAN_37, BLAN_38, BLAN_39, BLAN_40
                };
                _srcNtc = new Label[] { NTC1, NTC2, NTC3, NTC4, NTC5, NTC6, NTC7, NTC8 };
                // 与「保护标志」13 行一一对应
                _srcFauts = new VBPP.OvalShape[]
                {
                    FautOV, FautUV, FautOTC, FautUTC, FautOTD, FautUTD,
                    FautOCC, FautOCD, FautSC, FautWireOpen, FautMOSOT, FautAFE, FlashErr
                };

                // 4) 四列根布局：①电芯表 ②电池 ③电源与开关 ④保护标志
                _bpRoot = new TableLayoutPanel();
                _bpRoot.Dock = DockStyle.Fill;
                _bpRoot.ColumnCount = 4;
                _bpRoot.RowCount = 1;
                _bpRoot.Padding = new Padding(4);
                _bpRoot.BackColor = SystemColors.Control;
                _bpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
                _bpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
                _bpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23f));
                _bpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
                _bpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                _bpRoot.Controls.Add(BuildCellsGrid(), 0, 0);
                _bpRoot.Controls.Add(BuildBatteryColumn(), 1, 0);
                _bpRoot.Controls.Add(BuildPowerColumn(), 2, 0);
                _bpRoot.Controls.Add(BuildFaultGrid(), 3, 0);

                电池信息.Controls.Add(_bpRoot);
                _bpRoot.BringToFront();

                RefreshBatteryPage();
            }
            catch { }
        }

        // ① 电芯表：每行 = 一节电芯：序号 / 电压(mV) / 均衡（温度已由②列的 NTC 热力胶囊显示）
        private Control BuildCellsGrid()
        {
            DataGridView g = NewGrid();
            g.Columns.Add(NewCol("序号", 48, DataGridViewContentAlignment.MiddleCenter));
            g.Columns.Add(NewCol("电压 (mV)", 0, DataGridViewContentAlignment.MiddleRight));     // Fill：吸收剩余宽度
            g.Columns.Add(NewCol("均衡", 56, DataGridViewContentAlignment.MiddleCenter));
            _bpCells = g;
            return WrapInGroup("电芯电压 · 均衡", g);
        }

        // ② 电池：标题 + SOC + 电池图形 + NTC 温度 + 放电/充电 MOS + 参数表
        //    （原来底部还有「保存到文件 + 生产日期」一行，现整行删除 → 参数表吃掉全部剩余高度）
        private Control BuildBatteryColumn()
        {
            TableLayoutPanel col = NewColumn();
            col.RowCount = 6;
            // 顺序：标题 → 电池信息（SOC / 电池图形 / NTC 温度）→ 放电·充电MOS → 参数表
            // 30 + 40 + 88 + 34 = 192 = 第③列 (30 + 形象图 162) → 两列 MOS 行水平对齐
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));    // 标题
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));    // SOC 大字 + 充放电指示
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));    // 电池图形（含外圈热力环带）
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));    // NTC 温度数值
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));   // 放电 / 充电 MOS
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));    // 电池参数表（吃掉剩余高度）

            // 电池参数（项目 / 数值 / 单位）
            DataGridView g = NewGrid();
            g.Columns.Add(NewCol("项目", 0, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(NewCol("数值", 104, DataGridViewContentAlignment.MiddleRight));
            g.Columns.Add(NewCol("单位", 52, DataGridViewContentAlignment.MiddleLeft));
            for (int i = 0; i < BATT_PARAM_NAMES.Length; i++)
                g.Rows.Add(BATT_PARAM_NAMES[i], "", BATT_PARAM_UNITS[i]);
            _bpBattParams = g;

            // 放电 MOS / 充电 MOS（常显，随实际状态变色）
            TableLayoutPanel mosRow = new TableLayoutPanel();
            mosRow.Dock = DockStyle.Fill;
            mosRow.ColumnCount = 2;
            mosRow.RowCount = 1;
            mosRow.Margin = new Padding(0);
            mosRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mosRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mosRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // 必须加，否则行高塌成 0
            _bpDsgMos = NewMos("放电MOS");
            _bpChgMos = NewMos("充电MOS");
            mosRow.Controls.Add(_bpDsgMos, 0, 0);
            mosRow.Controls.Add(_bpChgMos, 1, 0);

            // SOC 大字（左）+ 充放电指示（右）
            TableLayoutPanel socRow = new TableLayoutPanel();
            socRow.Dock = DockStyle.Fill;
            socRow.ColumnCount = 2;
            socRow.RowCount = 1;
            socRow.Margin = new Padding(0);
            socRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            socRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            socRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _bpSocPct = new Label();
            _bpSocPct.Dock = DockStyle.Fill;
            _bpSocPct.TextAlign = ContentAlignment.MiddleCenter;
            _bpSocPct.Font = new Font("Microsoft YaHei", 15F, FontStyle.Bold);
            _bpSocPct.ForeColor = Color.FromArgb(13, 59, 40);
            _bpSocPct.Text = "0%";

            _bpChgDsg = new Label();
            _bpChgDsg.AutoSize = true;
            _bpChgDsg.Anchor = AnchorStyles.Right;
            _bpChgDsg.Margin = new Padding(0, 0, 12, 0);
            _bpChgDsg.TextAlign = ContentAlignment.MiddleRight;
            _bpChgDsg.ForeColor = Color.FromArgb(85, 85, 85);
            _bpChgDsg.Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold);   // 加大加粗
            _bpChgDsg.Text = "";
            socRow.Controls.Add(_bpSocPct, 0, 0);
            socRow.Controls.Add(_bpChgDsg, 1, 0);

            // 电池图形（外圈热力环带：左半 NTC1 / 右半 NTC2；充电时内部有流动效果）
            _bpBatt = new BatteryView();
            _bpBatt.Dock = DockStyle.Fill;          // 在所在格内水平居中（OnPaint 自行居中绘制）

            // ---- 电池图形下方一行：NTC 温度数值 ----
            // 拆成「固定文字 + 独立数值标签」，数值用等宽字体 + 固定列宽：
            //   ① 数字变化时只重绘那一个标签（不再整行重画）
            //   ② 整行宽度恒定，居中位置不会随数字变化而左右抖动（"偶尔闪一下"的根因）
            _bpTempInner = new TableLayoutPanel();
            _bpTempInner.AutoSize = true;
            _bpTempInner.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _bpTempInner.Anchor = AnchorStyles.None;
            _bpTempInner.Margin = new Padding(0);
            _bpTempInner.RowCount = 1;

            _bpTempRow = new TableLayoutPanel();
            _bpTempRow.Dock = DockStyle.Fill;
            _bpTempRow.Margin = new Padding(0);
            _bpTempRow.ColumnCount = 1;
            _bpTempRow.RowCount = 1;
            _bpTempRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _bpTempRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _bpTempRow.Controls.Add(_bpTempInner, 0, 0);

            col.Controls.Add(SectionTitle("电池状态"), 0, 0);
            col.Controls.Add(socRow, 0, 1);
            col.Controls.Add(_bpBatt, 0, 2);
            col.Controls.Add(_bpTempRow, 0, 3);
            col.Controls.Add(mosRow, 0, 4);
            col.Controls.Add(g, 0, 5);                   // 参数表：去掉分组标题，直接铺表格
            return col;
        }

        // ④ 保护标志
        private Control BuildFaultGrid()
        {
            DataGridView g = NewGrid();
            g.Columns.Add(NewCol("保护标志", 0, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(NewCol("状态", 62, DataGridViewContentAlignment.MiddleCenter));
            for (int i = 0; i < FAULT_NAMES.Length; i++) g.Rows.Add(FAULT_NAMES[i], "");
            // 状态列自绘圆点：告警 = 大号红点，正常 = 灰点
            g.CellPainting += delegate(object s, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
                if (e.RowIndex >= _bpFaultOn.Length) return;
                e.PaintBackground(e.CellBounds, true);
                int d = Math.Min(16, Math.Min(e.CellBounds.Width - 8, e.CellBounds.Height - 6));
                if (d < 6) d = 6;
                int cx = e.CellBounds.X + (e.CellBounds.Width - d) / 2;
                int cy = e.CellBounds.Y + (e.CellBounds.Height - d) / 2;
                bool on = _bpFaultOn[e.RowIndex];
                Color fill = on ? Color.FromArgb(226, 60, 60) : Color.FromArgb(206, 206, 206);
                Color edge = on ? Color.FromArgb(160, 32, 32) : Color.FromArgb(176, 176, 176);
                using (SolidBrush b = new SolidBrush(fill)) e.Graphics.FillEllipse(b, cx, cy, d, d);
                using (Pen p = new Pen(edge)) e.Graphics.DrawEllipse(p, cx, cy, d, d);
                e.Handled = true;
            };
            _bpFaults = g;

            // 第④列：保护标志表格（吃掉整列高度）
            // 「网页互动」按钮已改到顶部工具条右端（BuildTopRightActions），此处不再放按钮
            TableLayoutPanel col = NewColumn();
            col.RowCount = 1;
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            col.Controls.Add(WrapInGroup("保护标志", g), 0, 0);
            return col;
        }

        // ③ 电源与开关：MOS + 光伏 / 市电参数
        private Control BuildPowerColumn()
        {
            TableLayoutPanel col = NewColumn();
            col.RowCount = 4;
            // 30 + 162 = 192 = 第②列 (30 + 40 + 88 + 34) → 两列 MOS 行水平对齐
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));    // 标题
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 162f));   // 光伏 / 市电 形象图（左光伏 右市电）
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));   // 市电MOS / PWMMOS（常显）
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));    // 光伏 / 市电参数
            // 原来的第 5 行「保存到文件 + 生产日期」已删除：
            //   保存到文件 → 顶部工具条右端的胶囊开关；生产日期 → 不再上屏（参数设置页里已有）

            Label title = SectionTitle("光伏和市电状态");

            // 左「光伏」/ 右「市电」形象图（与②列电池信息区同高 → 两列 MOS 对齐）
            TableLayoutPanel iconRow = new TableLayoutPanel();
            iconRow.Dock = DockStyle.Fill;
            iconRow.ColumnCount = 2;
            iconRow.RowCount = 1;
            iconRow.Margin = new Padding(0);
            iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            iconRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _bpSolarIcon = new IconView();
            _bpSolarIcon.Solar = true;
            _bpSolarIcon.Caption = "光伏";
            // 必须 Dock=Fill + 与 MosView 相同的 Margin：图标格与下方 MOS 格同宽同中心
            _bpSolarIcon.Dock = DockStyle.Fill;
            _bpSolarIcon.Margin = new Padding(2);
            _bpGridIcon = new IconView();
            _bpGridIcon.Solar = false;
            _bpGridIcon.Caption = "市电";
            _bpGridIcon.Dock = DockStyle.Fill;
            _bpGridIcon.Margin = new Padding(2);
            iconRow.Controls.Add(_bpSolarIcon, 0, 0);
            iconRow.Controls.Add(_bpGridIcon, 1, 0);

            // 市电MOS(市电开关) / PWMMOS(太阳能开关)—— 常显，不再依赖调试开关；仅"调试显示"打开时出现（行高在外层切换）
            _bpMainMos = NewMos("市电MOS");
            _bpPwmMos = NewMos("PWMMOS");
            _bpDbgRow = new TableLayoutPanel();
            _bpDbgRow.Dock = DockStyle.Fill;
            _bpDbgRow.ColumnCount = 2;
            _bpDbgRow.RowCount = 1;
            _bpDbgRow.Margin = new Padding(0);
            _bpDbgRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _bpDbgRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _bpDbgRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            // 左 = 光伏（PWMMOS 即太阳能开关）；右 = 市电（市电MOS）—— 与上方形象图一一对应
            _bpDbgRow.Controls.Add(_bpPwmMos, 0, 0);
            _bpDbgRow.Controls.Add(_bpMainMos, 1, 0);

            // 光伏 / 市电参数表（项目 / 数值 / 单位）
            DataGridView g = NewGrid();
            g.Columns.Add(NewCol("项目", 0, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(NewCol("数值", 96, DataGridViewContentAlignment.MiddleRight));
            g.Columns.Add(NewCol("单位", 52, DataGridViewContentAlignment.MiddleLeft));
            for (int i = 0; i < POWER_PARAM_NAMES.Length; i++)
                g.Rows.Add(POWER_PARAM_NAMES[i], "", POWER_PARAM_UNITS[i]);
            _bpPowerParams = g;

            col.Controls.Add(title, 0, 0);
            col.Controls.Add(iconRow, 0, 1);
            col.Controls.Add(_bpDbgRow, 0, 2);
            col.Controls.Add(g, 0, 3);                   // 参数表：去掉分组标题，直接铺表格
            return col;
        }

        // ---------------------------------------------------------------- 刷新（只读旧控件）
        private void RefreshBatteryPage()
        {
            if (!_bpBuilt || _bpRoot == null) return;
            if (this.IsDisposed) return;
            try
            {
                // ① 电芯表：每行 = 一节电芯（序号 → 电压 → 均衡 → 温度，一一对应）
                int n = m_cellNum;
                if (n < 0) n = 0;
                if (n > 40) n = 40;
                bool dbg = (g_display_test == 1);
                EnsureCellRows(n, dbg);
                for (int r = 0; r < _bpRows.Count && r < _bpCells.Rows.Count; r++)
                {
                    RowDef d = _bpRows[r];
                    DataGridViewRow row = _bpCells.Rows[r];
                    int k = d.Idx;
                    if (k < 0 || k >= _srcCells.Length) continue;
                    row.Cells[1].Value = _srcCells[k].Text;
                    row.Cells[2].Value = "●";
                    row.Cells[1].Style.ForeColor = (_srcCells[k].ForeColor);
                    if (d.Kind == 2)                            // 调试行：数值灰显、均衡灰点
                    {
                        row.Cells[1].Style.ForeColor = Color.FromArgb(120, 120, 120);
                        row.Cells[2].Style.ForeColor = Color.Gainsboro;
                    }
                    else                                        // 真实电芯
                    {
                        row.Cells[2].Style.ForeColor =
                            (_srcBlans[k].FillColor != Color.Transparent)
                            ? Color.MediumSeaGreen : Color.Gainsboro;
                    }
                }
                if (_bpCells.SelectedCells.Count > 0) _bpCells.ClearSelection();

                // ② 电池参数（11 项）
                string[] bvals = new string[]
                {
                    vPack.Text, vAve.Text, vMax.Text, vMin.Text, vMaxDiff.Text, Current.Text,
                    FCC.Text, RSOC.Text, SOC.Text, CYCLE.Text, VER.Text
                };
                for (int i = 0; i < bvals.Length && i < _bpBattParams.Rows.Count; i++)
                {
                    if (!object.Equals(_bpBattParams.Rows[i].Cells[1].Value, bvals[i]))
                        _bpBattParams.Rows[i].Cells[1].Value = bvals[i];
                }
                if (_bpBattParams.SelectedCells.Count > 0) _bpBattParams.ClearSelection();

                // ② SOC / 充放电指示 / 剩余满充 / NTC 热力胶囊
                int pct = 0;
                int.TryParse(SOCIN.Text.Replace("%", "").Trim(), out pct);
                if (pct < 0) pct = 0;
                if (pct > 100) pct = 100;
                _bpBatt.Pct = pct;
                string ps = pct + "%";
                if (_bpSocPct.Text != ps) _bpSocPct.Text = ps;
                string ds = labelChgDsg.Text;
                if (_bpChgDsg.Text != ds) _bpChgDsg.Text = ds;
                // 电池图形下方一行：各路 NTC 温度数值
                // 只改「数值标签」；无数据一律显示 —（不再跳过整路），整行宽度恒定 → 不会整行抖动
                int tn = m_ntcNum;
                if (tn < 0) tn = 0;
                if (tn > _srcNtc.Length) tn = _srcNtc.Length;
                if (tn > _bpTempVals.Length) tn = _bpTempVals.Length;
                EnsureTempLine(tn);
                for (int i = 0; i < tn; i++)
                {
                    double tv = NtcValue(i);
                    string s = double.IsNaN(tv) ? "—" : tv.ToString("F1");
                    Label vl = _bpTempVals[i];
                    if (vl != null && vl.Text != s) vl.Text = s;
                }
                if (_bpBatt != null)
                {
                    _bpBatt.Temp1 = NtcValue(0);          // 外圈热力环带：左半 = NTC1
                    _bpBatt.Temp2 = NtcValue(1);          // 外圈热力环带：右半 = NTC2
                    // 充电中（含"边充边放"）→ 电池格子：亮格数"满格 → 当前格"递减循环，当前格闪烁
                    _bpBatt.Charging = (ds != null &&
                        (ds.IndexOf("充电") >= 0 || ds.IndexOf("边充边放") >= 0));
                }

                // ③ 光伏 / 市电参数（5 项）
                string[] pvals = new string[]
                {
                    vSolar.Text, iSolar.Text, pSolar.Text, iMainSupply.Text, pLoad.Text
                };
                for (int i = 0; i < pvals.Length && i < _bpPowerParams.Rows.Count; i++)
                {
                    if (!object.Equals(_bpPowerParams.Rows[i].Cells[1].Value, pvals[i]))
                        _bpPowerParams.Rows[i].Cells[1].Value = pvals[i];
                }
                if (_bpPowerParams.SelectedCells.Count > 0) _bpPowerParams.ClearSelection();

                // ③ MOS
                _bpDsgMos.On = (DSGMOS.FillColor == Color.MediumSeaGreen);
                _bpChgMos.On = (CHGMOS.FillColor == Color.MediumSeaGreen);
                // 市电MOS(市电开关) / PWMMOS(太阳能开关)：常显，颜色由 Form1 无条件刷新
                _bpMainMos.On = (MainMOS.FillColor == Color.MediumSeaGreen);
                _bpPwmMos.On = (PWMMOS.FillColor == Color.MediumSeaGreen);

                // ③列形象图高亮：光伏有输出（iSolar）/ 市电已接入（iMainSupply）；没输出就整幅灰掉
                if (_bpSolarIcon != null) _bpSolarIcon.On = HasCurrent(iSolar.Text);
                if (_bpGridIcon != null) _bpGridIcon.On = HasCurrent(iMainSupply.Text);

                // ④ 保护标志：告警 = 大号红点，正常 = 灰点（单元格自绘）
                for (int i = 0; i < _srcFauts.Length && i < _bpFaultOn.Length; i++)
                    _bpFaultOn[i] = (_srcFauts[i].FillColor != Color.Transparent);
                if (_bpFaults != null)
                {
                    if (_bpFaults.SelectedCells.Count > 0) _bpFaults.ClearSelection();
                    _bpFaults.Invalidate();
                }
            }
            catch { }
        }

        // 重排行：电芯 1..N → 调试行 25~36（调试开启且 N<25 时）
        private void EnsureCellRows(int n, bool dbg)
        {
            if (_bpCells == null) return;
            List<RowDef> defs = new List<RowDef>();
            for (int i = 1; i <= n; i++) defs.Add(MkRow(0, i - 1, i.ToString()));
            if (dbg && n < 25)
                for (int i = 25; i <= 36; i++) defs.Add(MkRow(2, i - 1, i.ToString()));

            bool same = (_bpRows.Count == defs.Count);
            if (same)
            {
                for (int i = 0; i < defs.Count; i++)
                {
                    if (_bpRows[i].Kind != defs[i].Kind || _bpRows[i].Idx != defs[i].Idx
                        || _bpRows[i].Num != defs[i].Num) { same = false; break; }
                }
            }
            if (same && _bpCells.Rows.Count == defs.Count) return;

            _bpCells.SuspendLayout();
            _bpCells.Rows.Clear();
            for (int r = 0; r < defs.Count; r++)
            {
                int idx = _bpCells.Rows.Add(defs[r].Num, "", "");
                if (idx < 0 || idx >= _bpCells.Rows.Count) continue;
                DataGridViewRow row = _bpCells.Rows[idx];
                if (defs[r].Kind == 2) row.Cells[0].Style.ForeColor = Color.FromArgb(191, 122, 22);  // 调试：橙
                else row.Cells[0].Style.ForeColor = Color.FromArgb(26, 26, 26);
                row.Cells[1].Style.ForeColor = Color.FromArgb(26, 26, 26);
                row.Cells[2].Style.ForeColor = Color.Gainsboro;
            }
            _bpCells.ClearSelection();
            _bpCells.ResumeLayout();
            _bpRows.Clear();
            _bpRows.AddRange(defs);
        }

        // NTC 热力胶囊：取第 idx 路 NTC 的温度（无效则显示 —）
        // 备注：电池两侧的 NTC 胶囊已按用户要求去掉（温度改由电池外圈热力环带表达）；
        //       NtcBadge 类保留备用 —— 若以后要加回胶囊，用它 + NtcValue(idx) 即可。
        private void UpdateNtc(NtcBadge b, int idx)
        {
            if (b == null) return;
            b.Temp = NtcValue(idx);
        }

        private static RowDef MkRow(int kind, int idx, string num)
        {
            RowDef d = new RowDef();
            d.Kind = kind;
            d.Idx = idx;
            d.Num = num;
            return d;
        }

        // ---------------------------------------------------------------- 主窗体自适应
        //  让 TabControl 填满"串口工具条以下、底部状态条以上"的区域 → 页面随窗口伸缩，
        //  最大化也能铺满；底部状态条强制贴底并置顶，避免被 TabControl 盖住。
        //  ⚠️ 只在"新版页面"下生效：旧版页面是"背景图 + 绝对坐标"，一变形就会错位。
        private void LayoutMain()
        {
            if (!USE_NEW_BATT_PAGE) return;
            try
            {
                int cw = this.ClientSize.Width;
                int ch = this.ClientSize.Height;
                if (cw < 100 || ch < 100) return;

                if (tabControl1 != null)
                {
                    int left = tabControl1.Left;
                    int top = tabControl1.Top;
                    int w = cw - left * 2;
                    int h = ch - top - 46;
                    if (w > 300 && h > 200 && (tabControl1.Width != w || tabControl1.Height != h))
                        tabControl1.Bounds = new Rectangle(left, top, w, h);
                }

                int y = ch - 32;
                if (y < 0) y = 0;
                if (comInfo != null) { comInfo.Top = y; comInfo.BringToFront(); }
                if (label225 != null) { label225.Top = y; label225.BringToFront(); }
                if (timeAndDate != null)
                {
                    timeAndDate.Top = y;
                    timeAndDate.Left = Math.Max(24, cw - timeAndDate.Width - 16);
                    timeAndDate.BringToFront();
                }

                LayoutTopRight();        // 顶部工具条右端的「保存到文件」开关 /「网页互动」按钮
            }
            catch { }
        }

        // ---------------------------------------------------------------- 顶部标签条：自绘
        //  原来的系统标签只有 24px 高、选中仅一个虚线焦点框，四个页签糊成一片。
        //  这里改成 OwnerDrawFixed + 36px：选中＝白底 + 顶部 3px 蓝条 + 加粗 + 左侧圆点，
        //  未选＝浅灰，悬停＝淡蓝。只在"新版页面"下启用，Designer 不动。
        //  ⚠️ 标签条 24→36px 会让页面客户区少 12px；实测 tabPage1 最底控件 y=894，
        //     运行时页面高 ≈937px，仍留 31px 余量，不会被裁。
        private Font _tabFontOn;
        private int _tabHover = -1;

        private void StyleTabs()
        {
            if (tabControl1 == null) return;
            try
            {
                _tabFontOn = new Font("Microsoft YaHei", 10.5F, FontStyle.Bold);
                tabControl1.Font = new Font("Microsoft YaHei", 10.5F, FontStyle.Regular);
                tabControl1.SizeMode = TabSizeMode.Fixed;
                tabControl1.ItemSize = new Size(134, 36);
                tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl1.DrawItem += TabControl1_DrawItem;
                tabControl1.MouseMove += delegate(object s, MouseEventArgs e)
                {
                    int i = TabIndexAt(e.Location);
                    if (i != _tabHover) { _tabHover = i; InvalidateTabStrip(); }
                };
                tabControl1.MouseLeave += delegate
                {
                    if (_tabHover != -1) { _tabHover = -1; InvalidateTabStrip(); }
                };
                InvalidateTabStrip();
            }
            catch { }
        }

        // 只重画标签条那一条（不要 Invalidate 整个 TabControl，避免带动页面重绘）
        private void InvalidateTabStrip()
        {
            if (tabControl1 == null) return;
            tabControl1.Invalidate(new Rectangle(0, 0, Math.Max(1, tabControl1.Width),
                Math.Max(1, tabControl1.ItemSize.Height + 10)));
        }

        private int TabIndexAt(Point p)
        {
            if (tabControl1 == null) return -1;
            for (int i = 0; i < tabControl1.TabCount; i++)
                if (tabControl1.GetTabRect(i).Contains(p)) return i;
            return -1;
        }

        private void TabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = sender as TabControl;
            if (tc == null) return;
            if (e.Index < 0 || e.Index >= tc.TabCount) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = tc.GetTabRect(e.Index);
            if (r.Width < 6 || r.Height < 6) return;
            bool sel = (tc.SelectedIndex == e.Index);
            bool hov = (_tabHover == e.Index) && !sel;

            Color cStrip = SystemColors.Control;                      // 与 TabControl 自身底色一致，未覆盖区不露馅
            Color cOff = Color.FromArgb(228, 231, 235);
            Color cHov = Color.FromArgb(230, 241, 251);
            Color cEdge = Color.FromArgb(214, 218, 224);
            Color cLine = Color.FromArgb(211, 209, 199);
            Color cAccent = Color.FromArgb(47, 128, 237);
            Color cTxtOff = Color.FromArgb(95, 94, 90);
            Color cTxtHov = Color.FromArgb(24, 95, 165);
            Color cTxtOn = Color.FromArgb(22, 32, 46);

            using (SolidBrush b = new SolidBrush(cStrip)) g.FillRectangle(b, r);

            // 页签实体：左右各留 2px、顶部留 4px，相邻页签之间就有 4px 缝，不再糊成一片
            Rectangle body = new Rectangle(r.X + 2, r.Y + 4, r.Width - 4, r.Height - 4);
            using (GraphicsPath p = RoundTopRect(body, 8))
            {
                using (SolidBrush b = new SolidBrush(sel ? Color.White : (hov ? cHov : cOff)))
                    g.FillPath(b, p);
                if (!sel)
                    using (Pen pen = new Pen(cEdge)) g.DrawPath(pen, p);
            }
            if (sel)   // 顶部 3px 强调条
            {
                using (GraphicsPath p2 = RoundTopRect(new Rectangle(body.X, body.Y, body.Width, 3), 2))
                using (SolidBrush b = new SolidBrush(cAccent)) g.FillPath(b, p2);
            }

            // 左侧小圆点
            int dot = 8;
            int dx = body.X + 15;
            int dy = body.Y + body.Height / 2 - dot / 2;
            using (SolidBrush b = new SolidBrush(sel ? cAccent : (hov ? Color.FromArgb(55, 138, 221) : Color.FromArgb(180, 178, 169))))
                g.FillEllipse(b, dx, dy, dot, dot);

            // 文字
            Font f = (sel && _tabFontOn != null) ? _tabFontOn : tc.Font;
            using (SolidBrush b = new SolidBrush(sel ? cTxtOn : (hov ? cTxtHov : cTxtOff)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                float tx = dx + dot + 8;
                g.DrawString(tc.TabPages[e.Index].Text, f, b,
                    new RectangleF(tx, body.Y, Math.Max(8f, body.Right - tx - 6f), body.Height), sf);
            }

            // 底部 1px 分隔线；选中页签用白线抹掉，与下方页面连成一片
            int by = body.Bottom;
            using (Pen pen = new Pen(cLine))
            {
                g.DrawLine(pen, body.Left, by, body.Right, by);
                if (e.Index == 0) g.DrawLine(pen, 0, by, body.Left, by);
                if (e.Index == tc.TabCount - 1) g.DrawLine(pen, body.Right, by, tc.ClientSize.Width, by);
            }
            if (sel)
                using (Pen pen = new Pen(Color.White, 3f)) g.DrawLine(pen, body.Left, by, body.Right, by);
        }

        // 上半圆角、下方直角的矩形（标签页签形状）
        private static GraphicsPath RoundTopRect(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad < 1) rad = 1;
            if (rad * 2 > r.Width) rad = Math.Max(1, r.Width / 2);
            if (rad * 2 > r.Height) rad = Math.Max(1, r.Height / 2);
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddLine(r.X + rad, r.Y, r.Right - rad, r.Y);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddLine(r.Right, r.Y + rad, r.Right, r.Bottom);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.AddLine(r.X, r.Bottom, r.X, r.Y + rad);
            p.CloseFigure();
            return p;
        }

        // ---------------------------------------------------------------- 顶部工具条右端：保存开关 + 网页互动
        //  原来这两个东西挤在③列/④列底部的 54px 行里，现在搬到窗口右上角空白区
        //  （串口工具条只占到 x≈1430，右侧 1450~1844 是空的）。
        private ToggleSwitch _bpSaveToggle;

        private void BuildTopRightActions()
        {
            if (_bpSaveToggle != null) return;
            try
            {
                _bpSaveToggle = new ToggleSwitch();
                _bpSaveToggle.Caption = "保存到文件";
                _bpSaveToggle.Size = new Size(158, 38);
                _bpSaveToggle.On = (checkBoxSaveData != null) && checkBoxSaveData.Checked;
                _bpSaveToggle.Click += delegate
                {
                    if (checkBoxSaveData == null) return;
                    // 交给原有复选框逻辑处理；它若拒绝（例如文件打不开）会把 Checked 改回来
                    checkBoxSaveData.Checked = !checkBoxSaveData.Checked;
                    _bpSaveToggle.On = checkBoxSaveData.Checked;
                };
                if (checkBoxSaveData != null)
                {
                    checkBoxSaveData.CheckedChanged += delegate
                    {
                        if (_bpSaveToggle != null) _bpSaveToggle.On = checkBoxSaveData.Checked;
                    };
                }

                // 「网页互动」按钮：原来是 ③/④列底部的蓝色方块，这里换成深色科技按钮
                if (m_btnOpenDemo != null)
                {
                    m_btnOpenDemo.Size = new Size(146, 40);
                    this.Controls.Add(m_btnOpenDemo);
                }
                this.Controls.Add(_bpSaveToggle);
                if (m_btnOpenDemo != null) m_btnOpenDemo.BringToFront();
                _bpSaveToggle.BringToFront();
                LayoutTopRight();
            }
            catch { }
        }

        private void LayoutTopRight()
        {
            if (!USE_NEW_BATT_PAGE) return;
            try
            {
                int cw = this.ClientSize.Width;
                if (cw < 400) return;
                int y = 17;                                   // 与串口工具条同一水平线（y 16~62）
                int right = cw - 14;
                if (m_btnOpenDemo != null)
                {
                    m_btnOpenDemo.Location = new Point(right - m_btnOpenDemo.Width, y);
                    m_btnOpenDemo.BringToFront();
                    right = m_btnOpenDemo.Left - 12;
                }
                if (_bpSaveToggle != null)
                {
                    _bpSaveToggle.Location = new Point(right - _bpSaveToggle.Width, y + 1);
                    _bpSaveToggle.BringToFront();
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------- 小工具
        private static TableLayoutPanel NewColumn()
        {
            TableLayoutPanel p = new TableLayoutPanel();
            p.Dock = DockStyle.Fill;
            p.ColumnCount = 1;
            p.Margin = new Padding(0, 0, 4, 0);
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return p;
        }

        private static DataGridView NewGrid()
        {
            DataGridView g = new DataGridView();
            g.Dock = DockStyle.Fill;
            g.Font = new Font("Microsoft YaHei", 10F);          // 整体大一号（原 9F）
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Color.FromArgb(224, 224, 224);
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.RowHeadersVisible = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 34;                          // 字号大了，表头也加高
            g.RowTemplate.Height = 29;                           // 行高同步加高
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(234, 234, 234);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 51, 51);
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.DefaultCellStyle.BackColor = Color.White;
            g.DefaultCellStyle.ForeColor = Color.FromArgb(26, 26, 26);
            g.DefaultCellStyle.SelectionBackColor = Color.White;
            g.DefaultCellStyle.SelectionForeColor = Color.FromArgb(26, 26, 26);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 246, 246);
            return g;
        }

        private static DataGridViewTextBoxColumn NewCol(string head, int width, DataGridViewContentAlignment align)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = head;
            c.ReadOnly = true;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            if (width <= 0)
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                c.FillWeight = 100f;
            }
            else
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                c.Width = width;
            }
            c.DefaultCellStyle.Alignment = align;
            return c;
        }

        // 取第 idx 路 NTC 的温度（℃）；无效返回 double.NaN
        // 列标题（新版四列统一的灰底标题条）
        private static Label SectionTitle(string text)
        {
            Label t = new Label();
            t.Text = text;
            t.Dock = DockStyle.Fill;
            t.TextAlign = ContentAlignment.MiddleLeft;
            t.Padding = new Padding(8, 0, 0, 0);
            t.Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold);
            t.BackColor = Color.FromArgb(232, 232, 232);
            t.Margin = new Padding(0, 0, 0, 2);
            return t;
        }

        // 温度行：按 NTC 路数重建结构（仅路数变化时重建；平时刷新只改数值标签的文本）
        private void EnsureTempLine(int n)
        {
            if (_bpTempInner == null || _bpTempN == n) return;
            _bpTempInner.SuspendLayout();
            _bpTempInner.Controls.Clear();
            _bpTempInner.ColumnStyles.Clear();
            _bpTempInner.ColumnCount = Math.Max(1, n * 2 - 1);
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    Label sep = new Label();
                    sep.Text = "｜";
                    sep.AutoSize = true;
                    sep.Anchor = AnchorStyles.None;
                    sep.Margin = new Padding(16, 0, 16, 0);
                    sep.ForeColor = Color.FromArgb(170, 170, 170);
                    sep.Font = new Font("Microsoft YaHei", 10F);
                    _bpTempInner.Controls.Add(sep, i * 2 - 1, 0);
                }

                TableLayoutPanel one = new TableLayoutPanel();
                one.AutoSize = true;
                one.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                one.Margin = new Padding(0);
                one.RowCount = 1;
                one.ColumnCount = 3;
                one.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                one.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58f));   // 数值列固定宽
                one.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                one.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                Label cap = new Label();
                cap.Text = "NTC" + (i + 1);
                cap.AutoSize = true;
                cap.Anchor = AnchorStyles.None;
                cap.Margin = new Padding(0, 0, 4, 0);
                cap.ForeColor = Color.FromArgb(85, 85, 85);
                cap.Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold);

                Label val = new Label();
                val.Text = "—";
                val.Dock = DockStyle.Fill;
                val.TextAlign = ContentAlignment.MiddleRight;
                val.Margin = new Padding(0);
                val.ForeColor = Color.FromArgb(51, 51, 51);
                val.Font = new Font("Consolas", 12F, FontStyle.Bold);   // 等宽数字：宽度恒定
                _bpTempVals[i] = val;

                Label unit = new Label();
                unit.Text = "℃";
                unit.AutoSize = true;
                unit.Anchor = AnchorStyles.None;
                unit.Margin = new Padding(3, 0, 0, 0);
                unit.ForeColor = Color.FromArgb(85, 85, 85);
                unit.Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold);

                one.Controls.Add(cap, 0, 0);
                one.Controls.Add(val, 1, 0);
                one.Controls.Add(unit, 2, 0);
                _bpTempInner.Controls.Add(one, i * 2, 0);
            }
            if (n == 0)
            {
                Label none = new Label();
                none.Text = "温度 —";
                none.AutoSize = true;
                none.Anchor = AnchorStyles.None;
                none.Margin = new Padding(0);
                none.ForeColor = Color.FromArgb(120, 120, 120);
                none.Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold);
                _bpTempInner.Controls.Add(none, 0, 0);
            }
            _bpTempN = n;
            _bpTempInner.ResumeLayout();
        }

        // 电流文本是否"有值"（> 5 mA，滤掉零点噪声）—— 用于光伏/市电形象图高亮
        private static bool HasCurrent(string s)
        {
            double v;
            return double.TryParse(s, out v) && v > 5;
        }

        private double NtcValue(int idx)
        {
            double t = 0;
            if (idx >= 0 && idx < m_ntcNum && idx < _srcNtc.Length && _srcNtc[idx] != null
                && double.TryParse(_srcNtc[idx].Text, out t))
                return t;
            return double.NaN;
        }

        private static Label MakeLabel(string text, bool bold)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(6, 6, 0, 0);
            l.ForeColor = Color.FromArgb(51, 51, 51);
            l.Font = new Font("Microsoft YaHei", 10F,     // 整体大一号（原 9F）
                bold ? FontStyle.Bold : FontStyle.Regular);
            return l;
        }

        private static MosView NewMos(string caption)
        {
            MosView m = new MosView();
            m.Caption = caption;
            m.Dock = DockStyle.Fill;      // 必须设：否则控件默认 0×0，画不出来
            m.Margin = new Padding(2);
            return m;
        }

        // 给控件套一个带标题的方框（TableLayoutPanel 单格，边框由外层 CellBorderStyle 提供）
        private static TableLayoutPanel WrapInGroup(string title, Control inner)
        {
            TableLayoutPanel box = new TableLayoutPanel();
            box.Dock = DockStyle.Fill;
            box.ColumnCount = 1;
            box.RowCount = 2;
            box.Margin = new Padding(0, 0, 4, 0);
            box.BackColor = Color.White;
            box.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            box.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            box.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label hd = new Label();
            hd.Text = title;
            hd.Dock = DockStyle.Fill;
            hd.TextAlign = ContentAlignment.MiddleLeft;
            hd.Padding = new Padding(8, 0, 0, 0);
            hd.BackColor = Color.FromArgb(232, 232, 232);
            hd.Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold);   // 整体大一号（原 9F）
            hd.Margin = new Padding(0);

            inner.Dock = DockStyle.Fill;
            inner.Margin = new Padding(0);
            box.Controls.Add(hd, 0, 0);
            box.Controls.Add(inner, 0, 1);
            return box;
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0f || r.Width <= d || r.Height <= d)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        // ---------------------------------------------------------------- 自绘：MOS 管
        // ---------------------------------------------------------------- 自绘：光伏 / 市电 形象图
        private class IconView : Control
        {
            private string _cap = "";
            private bool _solar = true;          // true = 光伏（太阳 + 板 + 充电箭头）；false = 市电（输电塔）
            private bool _on = true;             // 有输出 / 已接入 → 高亮；否则整幅灰掉

            public IconView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }
            protected override Size DefaultSize { get { return new Size(140, 150); } }

            public string Caption { get { return _cap; } set { _cap = value; Invalidate(); } }
            public bool Solar { get { return _solar; } set { _solar = value; Invalidate(); } }
            /// <summary>true = 高亮（光伏有输出 / 市电已接入）；false = 整幅灰掉</summary>
            public bool On
            {
                get { return _on; }
                set { if (_on == value) return; _on = value; Invalidate(); }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush bg = new SolidBrush(this.BackColor))
                    g.FillRectangle(bg, e.ClipRectangle);

                float capH = 24f;
                // 「可用区」算法与 MosView 完全一致，且图形宽度 = 该可用区直径、中心 = 该圆中心
                // → 形象图左右位置与下方 MOS 圆一一对齐，两图之间的间距也等于两个 MOS 的间距
                float avail = Math.Min(Width - 10f, Height - capH - 10f);
                float cx = Width / 2f;                 // 与 MosView 同一个 cx → 水平位置一一对齐
                float cy = (Height - capH) / 2f;       // 在上方"图形区"内垂直居中，不侵占标题行

                // 熄灯时整幅降为灰色调
                Color line = _on ? Color.FromArgb(31, 122, 77) : Color.FromArgb(176, 178, 182);
                Color fill = _on ? Color.FromArgb(225, 245, 238) : Color.FromArgb(237, 239, 241);
                Color sun = _on ? Color.FromArgb(239, 159, 39) : Color.FromArgb(196, 198, 201);
                Color blue = _on ? Color.FromArgb(24, 95, 165) : Color.FromArgb(176, 178, 182);
                Color gray = _on ? Color.FromArgb(136, 135, 128) : Color.FromArgb(202, 204, 207);
                Color capC = _on ? Color.FromArgb(60, 60, 60) : Color.FromArgb(168, 170, 174);

                if (avail >= 26f)
                {
                    float maxH = Math.Max(20f, Height - capH - 4f);
                    if (_solar) DrawSolar(g, cx, cy, avail, maxH, line, fill, sun);
                    else DrawMains(g, cx, cy, avail, maxH, blue, gray);
                }

                if (!string.IsNullOrEmpty(_cap))
                {
                    using (Font f = new Font("Microsoft YaHei", 10F, _on ? FontStyle.Bold : FontStyle.Regular))
                    using (SolidBrush b = new SolidBrush(capC))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(_cap, f, b, new RectangleF(0f, Height - capH, Width, capH), sf);
                    }
                }
            }

            // 把「基准图形内容框」等比缩放后放到 (cx, cy) 为心的区域：
            //   宽度 = target（= MOS 圆直径），高度不超 maxH，超出则按高度收紧
            //   bw/bh = 内容框宽高；bx/by = 内容框左上角（基准坐标系内）
            private static bool BeginStd(Graphics g, float cx, float cy, float target, float maxH,
                                         float bw, float bh, float bx, float by, out GraphicsState st)
            {
                st = null;
                float k = target / Math.Max(bw, bh);
                if (k <= 0.05f) return false;
                if (bh * k > maxH) k = maxH / bh;
                if (k <= 0.05f) return false;
                float ox = cx - bw * k / 2f - bx * k;
                float oy = cy - bh * k / 2f - by * k;
                st = g.Save();
                g.TranslateTransform(ox, oy);
                g.ScaleTransform(k, k);
                return true;
            }

            // 光伏：太阳 + 光伏板（带网格）+ 向下充电箭头。内容框 x 1..106，y 5..80
            private static void DrawSolar(Graphics g, float cx, float cy, float target, float maxH,
                                          Color line, Color fill, Color sun)
            {
                GraphicsState st;
                if (!BeginStd(g, cx, cy, target, maxH, 105f, 75f, 1f, 5f, out st)) return;

                using (SolidBrush b = new SolidBrush(sun)) g.FillEllipse(b, 10f, 14f, 24f, 24f);
                using (Pen p = new Pen(sun, 2f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    for (int i = 0; i < 8; i++)
                    {
                        double ang = i * Math.PI / 4.0;
                        float scx = 22f, scy = 26f, r1 = 16f, r2 = 21f;
                        g.DrawLine(p,
                            scx + (float)(Math.Cos(ang) * r1), scy + (float)(Math.Sin(ang) * r1),
                            scx + (float)(Math.Cos(ang) * r2), scy + (float)(Math.Sin(ang) * r2));
                    }
                }

                RectangleF panel = new RectangleF(44f, 6f, 62f, 38f);
                using (SolidBrush b = new SolidBrush(fill)) g.FillRectangle(b, panel);
                using (Pen p = new Pen(line, 1.6f)) g.DrawRectangle(p, panel.X, panel.Y, panel.Width, panel.Height);
                using (Pen p = new Pen(line, 1f))
                {
                    for (int i = 1; i < 3; i++)
                        g.DrawLine(p, panel.X + panel.Width * i / 3f, panel.Y,
                                      panel.X + panel.Width * i / 3f, panel.Bottom);
                    g.DrawLine(p, panel.X, panel.Y + panel.Height / 2f, panel.Right, panel.Y + panel.Height / 2f);
                }

                using (Pen p = new Pen(line, 1.8f)) g.DrawLine(p, 75f, 46f, 75f, 66f);
                using (SolidBrush b = new SolidBrush(line))
                    g.FillPolygon(b, new PointF[] {
                        new PointF(68f, 63f), new PointF(82f, 63f), new PointF(75f, 80f) });

                g.Restore(st);
            }

            // 市电：输电塔 + 两侧导线。内容框 x 14..116，y 6..100
            private static void DrawMains(Graphics g, float cx, float cy, float target, float maxH,
                                          Color blue, Color gray)
            {
                GraphicsState st;
                if (!BeginStd(g, cx, cy, target, maxH, 102f, 94f, 14f, 6f, out st)) return;

                using (Pen p = new Pen(blue, 2.2f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, 65f, 6f, 45f, 100f);      // 左主杆
                    g.DrawLine(p, 65f, 6f, 85f, 100f);      // 右主杆
                    g.DrawLine(p, 34f, 26f, 96f, 26f);      // 上横担
                    g.DrawLine(p, 42f, 48f, 88f, 48f);      // 下横担
                    g.DrawLine(p, 45f, 100f, 85f, 100f);    // 塔底
                }
                using (Pen p = new Pen(blue, 1.2f))
                {
                    g.DrawLine(p, 65f, 28f, 55f, 84f);
                    g.DrawLine(p, 65f, 28f, 75f, 84f);
                }
                using (Pen p = new Pen(gray, 1.2f))
                {
                    g.DrawBezier(p, 34f, 26f, 24f, 30f, 18f, 36f, 14f, 46f);
                    g.DrawBezier(p, 96f, 26f, 106f, 30f, 112f, 36f, 116f, 46f);
                }

                g.Restore(st);
            }
        }

        // ---------------------------------------------------------------- 自绘：胶囊开关 / 科技按钮
        // 全圆角矩形（浮点版，开关胶囊与按钮圆角共用）
        private static GraphicsPath RoundRectF(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad < 0.5f) rad = 0.5f;
            if (rad * 2f > r.Width) rad = r.Width / 2f;
            if (rad * 2f > r.Height) rad = r.Height / 2f;
            p.AddArc(r.X, r.Y, rad * 2f, rad * 2f, 180f, 90f);
            p.AddArc(r.Right - rad * 2f, r.Y, rad * 2f, rad * 2f, 270f, 90f);
            p.AddArc(r.Right - rad * 2f, r.Bottom - rad * 2f, rad * 2f, rad * 2f, 0f, 90f);
            p.AddArc(r.X, r.Bottom - rad * 2f, rad * 2f, rad * 2f, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        // 「保存到文件」胶囊开关：外观自绘，状态仍然绑定原来的 checkBoxSaveData（老控件藏在隐藏容器里）
        private class ToggleSwitch : Control
        {
            private bool _on;
            private bool _hover;
            private string _cap = "";

            public ToggleSwitch()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                    | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
                BackColor = SystemColors.Control;
                Font = new Font("Microsoft YaHei", 10F);
                Cursor = Cursors.Hand;
            }
            protected override Size DefaultSize { get { return new Size(158, 38); } }
            public bool On { get { return _on; } set { if (_on == value) return; _on = value; Invalidate(); } }
            public string Caption { get { return _cap; } set { _cap = value; Invalidate(); } }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                float h = Height, pw = 44f, ph = 22f, px = 6f, py = (h - ph) / 2f;
                Color track = _on ? Color.FromArgb(29, 158, 117) : Color.FromArgb(200, 204, 210);
                if (_hover) track = _on ? Color.FromArgb(24, 138, 102) : Color.FromArgb(184, 189, 196);
                using (GraphicsPath p = RoundRectF(new RectangleF(px, py, pw, ph), ph / 2f))
                using (SolidBrush b = new SolidBrush(track)) g.FillPath(b, p);

                float kr = ph / 2f - 3f;
                float kx = _on ? (px + pw - ph / 2f) : (px + ph / 2f);
                using (SolidBrush b = new SolidBrush(Color.White))
                    g.FillEllipse(b, kx - kr, py + ph / 2f - kr, kr * 2f, kr * 2f);

                using (SolidBrush b = new SolidBrush(_on ? Color.FromArgb(15, 110, 86) : Color.FromArgb(95, 94, 90)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(_cap, this.Font, b,
                        new RectangleF(px + pw + 10f, 0f, Math.Max(10f, Width - px - pw - 14f), h), sf);
                }
            }
        }

        // 「网页互动」科技按钮：深墨底 + 青色描边 + 地球图标 + 悬停光圈 + 按下下沉
        private class TechButton : Control
        {
            private bool _hover, _down;

            public TechButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                    | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
                BackColor = SystemColors.Control;
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold);
                Cursor = Cursors.Hand;
            }
            protected override Size DefaultSize { get { return new Size(146, 40); } }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e)
            { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e)
            { if (_down) { _down = false; Invalidate(); } base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                float dy = _down ? 1f : 0f;
                RectangleF r = new RectangleF(1.5f, 1.5f + dy, Width - 3f, Height - 4f);
                Color fill = _down ? Color.FromArgb(10, 27, 42)
                                  : (_hover ? Color.FromArgb(20, 50, 74) : Color.FromArgb(14, 36, 56));
                Color edge = _hover ? Color.FromArgb(127, 227, 255) : Color.FromArgb(53, 208, 255);

                if (_hover)   // 悬停：外侧再套一圈淡色光圈
                {
                    RectangleF o = new RectangleF(r.X - 2f, r.Y - 2f, r.Width + 4f, r.Height + 4f);
                    using (GraphicsPath p = RoundRectF(o, 10f))
                    using (Pen pen = new Pen(Color.FromArgb(90, 53, 208, 255)))
                        g.DrawPath(pen, p);
                }
                using (GraphicsPath p = RoundRectF(r, 8f))
                {
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                    using (Pen pen = new Pen(edge, 1.2f)) g.DrawPath(pen, p);
                }

                // 地球图标（纯线条：圆 + 竖向椭圆 + 赤道线）
                float cx = r.X + 22f, cy = r.Y + r.Height / 2f, rad = 7f;
                using (Pen pen = new Pen(edge, 1.3f))
                {
                    g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2f, rad * 2f);
                    g.DrawEllipse(pen, cx - rad * 0.45f, cy - rad, rad * 0.9f, rad * 2f);
                    g.DrawLine(pen, cx - rad, cy, cx + rad, cy);
                }

                using (SolidBrush b = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(this.Text, this.Font, b,
                        new RectangleF(r.X + 40f, r.Y, Math.Max(10f, r.Width - 46f), r.Height), sf);
                }
            }
        }

        private class MosView : Control
        {
            private bool _on = false;
            private string _cap = "";
            public MosView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
                Font = new Font("Microsoft YaHei", 10F);    // 整体大一号（原 9F）
            }
            public bool On
            {
                get { return _on; }
                set { if (_on != value) { _on = value; Invalidate(); } }
            }
            public string Caption
            {
                get { return _cap; }
                set { _cap = value; Invalidate(); }
            }
            // 给控件一个默认尺寸：否则默认 0×0，任何布局下都画不出来
            protected override Size DefaultSize
            {
                get { return new Size(120, 130); }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);
                int capH = 24;                              // 字号大了，标题行加高（原 22）
                using (SolidBrush b = new SolidBrush(Color.FromArgb(51, 51, 51)))
                using (StringFormat f = new StringFormat())
                {
                    f.Alignment = StringAlignment.Center;
                    f.LineAlignment = StringAlignment.Center;
                    g.DrawString(_cap, this.Font, b, new RectangleF(0f, 0f, Width, capH), f);
                }
                float avail = Math.Min(Width - 10f, Height - capH - 10f);
                if (avail < 26f) return;
                float r = avail / 2f;
                float cx = Width / 2f;
                float cy = capH + (Height - capH) / 2f;
                Color c = _on ? Color.FromArgb(29, 158, 117) : Color.FromArgb(214, 69, 69);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(30, c.R, c.G, c.B)))
                    g.FillEllipse(bg, cx - r, cy - r, r * 2f, r * 2f);
                using (Pen ring = new Pen(Color.FromArgb(195, 204, 212), 1f))
                    g.DrawEllipse(ring, cx - r, cy - r, r * 2f, r * 2f);

                float s = (r * 2f * 0.74f) / 64f;
                float ox = cx - 32f * s;
                float oy = cy - 32f * s;
                using (Pen p = new Pen(c, Math.Max(1.5f, 2.6f * s)))
                using (SolidBrush br = new SolidBrush(c))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    DrawMosSymbol(g, p, br, ox, oy, s);
                }
            }
        }

        // 归一化 64×64 的 N 沟道 MOSFET 符号
        private static void DrawMosSymbol(Graphics g, Pen p, SolidBrush br, float ox, float oy, float s)
        {
            g.DrawLine(p, ox + 7f * s, oy + 32f * s, ox + 17f * s, oy + 32f * s);     // 栅极引线
            g.DrawLine(p, ox + 17f * s, oy + 15f * s, ox + 17f * s, oy + 49f * s);    // 栅极板
            g.DrawLine(p, ox + 24f * s, oy + 15f * s, ox + 24f * s, oy + 24f * s);    // 沟道三段
            g.DrawLine(p, ox + 24f * s, oy + 28f * s, ox + 24f * s, oy + 36f * s);
            g.DrawLine(p, ox + 24f * s, oy + 40f * s, ox + 24f * s, oy + 49f * s);
            g.DrawLine(p, ox + 24f * s, oy + 19f * s, ox + 40f * s, oy + 19f * s);    // 漏极
            g.DrawLine(p, ox + 40f * s, oy + 19f * s, ox + 40f * s, oy + 9f * s);
            g.DrawLine(p, ox + 24f * s, oy + 45f * s, ox + 40f * s, oy + 45f * s);    // 源极
            g.DrawLine(p, ox + 40f * s, oy + 45f * s, ox + 40f * s, oy + 55f * s);
            g.DrawLine(p, ox + 24f * s, oy + 32f * s, ox + 40f * s, oy + 32f * s);    // 衬底
            g.DrawLine(p, ox + 40f * s, oy + 32f * s, ox + 40f * s, oy + 45f * s);
            g.FillPolygon(br, new PointF[]
            {
                new PointF(ox + 31f * s, oy + 32f * s),
                new PointF(ox + 38f * s, oy + 27.5f * s),
                new PointF(ox + 38f * s, oy + 36.5f * s)
            });
            g.FillEllipse(br, ox + 37f * s, oy + 6f * s, 6f * s, 6f * s);
            g.FillEllipse(br, ox + 37f * s, oy + 52f * s, 6f * s, 6f * s);
        }

        // ---------------------------------------------------------------- 温度 → 热力色
        //  t ≤ -10℃ 绿(120°) → 25℃ 黄(60°) → t ≥ 60℃ 红(0°)
        private static Color HeatColor(double t)
        {
            const double lo = -10.0, hi = 60.0;
            double x = (t - lo) / (hi - lo);
            if (x < 0) x = 0;
            if (x > 1) x = 1;
            return FromHsl(120.0 * (1.0 - x), 0.78, 0.44);
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double hp = h / 60.0;
            double xx = c * (1.0 - Math.Abs(hp % 2.0 - 1.0));
            double r = 0, g = 0, b = 0;
            if (hp < 1) { r = c; g = xx; }
            else if (hp < 2) { r = xx; g = c; }
            else if (hp < 3) { g = c; b = xx; }
            else if (hp < 4) { g = xx; b = c; }
            else if (hp < 5) { r = xx; b = c; }
            else { r = c; b = xx; }
            double m = l - c / 2.0;
            int R = (int)Math.Round((r + m) * 255.0);
            int G = (int)Math.Round((g + m) * 255.0);
            int B = (int)Math.Round((b + m) * 255.0);
            if (R < 0) R = 0; if (R > 255) R = 255;
            if (G < 0) G = 0; if (G > 255) G = 255;
            if (B < 0) B = 0; if (B > 255) B = 255;
            return Color.FromArgb(R, G, B);
        }

        // ---------------------------------------------------------------- 自绘：NTC 温度胶囊
        private class NtcBadge : Control
        {
            private string _cap = "NTC";
            private double _t = double.NaN;
            public NtcBadge()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold);
            }
            public string Caption
            {
                get { return _cap; }
                set { _cap = value; Invalidate(); }
            }
            /// <summary>温度 ℃；double.NaN = 无数据</summary>
            public double Temp
            {
                get { return _t; }
                set
                {
                    if (double.IsNaN(_t) && double.IsNaN(value)) return;
                    if (!double.IsNaN(_t) && !double.IsNaN(value)
                        && Math.Abs(_t - value) < 0.01) return;
                    _t = value;
                    Invalidate();
                }
            }
            protected override Size DefaultSize
            {
                get { return new Size(84, 46); }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                bool has = !double.IsNaN(_t);
                Color back = has ? HeatColor(_t) : Color.FromArgb(170, 178, 186);

                RectangleF r = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
                using (GraphicsPath path = RoundRect(r, 9f))
                {
                    using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.FromArgb(60, 0, 0, 0)))
                        g.DrawPath(p, path);
                }

                string line1 = _cap;
                string line2 = has ? _t.ToString("0.0") + " ℃" : "—";
                using (SolidBrush fg = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    using (Font f1 = new Font("Microsoft YaHei", 8.5F, FontStyle.Bold))
                        g.DrawString(line1, f1, fg,
                            new RectangleF(0f, 3f, Width, 17f), sf);
                    using (Font f2 = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold))
                        g.DrawString(line2, f2, fg,
                            new RectangleF(0f, 19f, Width, 22f), sf);
                }
            }
        }

        // ---------------------------------------------------------------- 自绘：电池
        private class BatteryView : Control
        {
            private int _pct;
            private double _t1 = double.NaN, _t2 = double.NaN;   // NTC1（左半环）/ NTC2（右半环）
            private bool _charging;
            private int _step;                                   // 充电动画步进（每 tick +1）
            private Rectangle _cellRect = Rectangle.Empty;        // 格子区域（动画只重绘这块）
            private System.Windows.Forms.Timer _anim;            // 充电动画节拍

            public BatteryView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }

            /// <summary>充电中（含"边充边放"）时置 true：电池内部格子按"满格→当前格"递减循环，当前格闪烁</summary>
            public bool Charging
            {
                get { return _charging; }
                set
                {
                    if (_charging == value) return;
                    _charging = value;
                    if (_charging)
                    {
                        if (_anim == null)
                        {
                            _anim = new System.Windows.Forms.Timer();
                            _anim.Interval = 60;             // 节拍；每 4 拍变一格、每 2 拍切换闪烁相位
                            _anim.Tick += delegate(object s, EventArgs ev)
                            {
                                _step++;
                                if (_step > 1000000) _step = 0;
                                // 只让"格子"那一小块失效 → 环带、外壳等不跟着重画
                                if (_cellRect.IsEmpty) Invalidate();
                                else Invalidate(_cellRect);
                            };
                        }
                        _anim.Start();
                    }
                    else if (_anim != null)
                    {
                        _anim.Stop();
                        _step = 0;
                    }
                    Invalidate();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _anim != null)
                {
                    _anim.Stop();
                    _anim.Dispose();
                    _anim = null;
                }
                base.Dispose(disposing);
            }
            public int Pct
            {
                get { return _pct; }
                set
                {
                    int v = value;
                    if (v < 0) v = 0;
                    if (v > 100) v = 100;
                    if (_pct != v) { _pct = v; Invalidate(); }
                }
            }
            /// <summary>电池左侧温度（℃）；NaN = 无数据</summary>
            public double Temp1
            {
                get { return _t1; }
                set { if (SameTemp(_t1, value)) return; _t1 = value; Invalidate(); }
            }
            /// <summary>电池右侧温度（℃）；NaN = 无数据</summary>
            public double Temp2
            {
                get { return _t2; }
                set { if (SameTemp(_t2, value)) return; _t2 = value; Invalidate(); }
            }
            private static bool SameTemp(double a, double b)
            {
                if (double.IsNaN(a) && double.IsNaN(b)) return true;
                if (double.IsNaN(a) || double.IsNaN(b)) return false;
                return Math.Abs(a - b) < 0.01;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // 只擦本次"失效区域"（动画时就是格子那一小块）—— 不整块重画，观感更自然
                using (SolidBrush bgBrush = new SolidBrush(this.BackColor))
                    g.FillRectangle(bgBrush, e.ClipRectangle);

                float ringW = 8f;                        // 电池外面那一圈"热力环带"的厚度
                float padX = 10f + ringW, padY = 6f + ringW;
                float capW = 9f, gap = 4f;
                float bh = Math.Min(Height - padY * 2f,
                                    (Width - padX * 2f - capW - gap - ringW * 2f) / 2.6f);
                bh = Math.Min(bh, 60f);                  // 限高：电池图形别太大
                if (bh < 16f) return;
                float bw = Math.Min(Width - padX * 2f - capW - gap - ringW * 2f, bh * 2.6f);
                float bx = (Width - bw - capW - gap - ringW * 2f) / 2f + ringW;
                float by = (Height - bh) / 2f;

                RectangleF body = new RectangleF(bx, by, bw, bh);

                // 1) 电池外面套一圈热力环带：左半 = NTC1、右半 = NTC2
                DrawHeatRing(g, body, ringW);

                // 2) 正极凸起（画在环带外侧）
                RectangleF cap = new RectangleF(bx + bw + ringW + gap, by + bh * 0.34f, capW, bh * 0.32f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 107, 122)))
                    g.FillRectangle(b, cap);

                // 3) 电池主体
                using (GraphicsPath path = RoundRect(body, 7f))
                {
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.FromArgb(90, 107, 122), 2.4f)) g.DrawPath(p, path);
                }

                // 4) 电量格子：5 格满；充电时亮格数在"当前格 ↔ 满格"之间一格一格地增减
                const int CELLS = 5;
                float inX = 5f, inY = 5f;
                float maxW = bw - inX * 2f - 2f;
                float maxH = bh - inY * 2f;
                int cur = (int)Math.Ceiling(_pct * CELLS / 100.0);   // 当前格数（5 格满）
                if (cur < 0) cur = 0;
                if (cur > CELLS) cur = CELLS;
                // 充电中：亮格数从"当前格"一格一格推进到"满格"，到顶后跳回当前格循环；
                // 闪烁格 = 最靠右的那个亮格（满格时无推进空间 → 最后一格单独闪）
                int lit = cur;
                if (_charging) lit = cur + (_step / 4) % (CELLS - cur + 1);
                bool blinkOn = ((_step / 2) % 2) == 0;               // 闪烁相位

                float cgap = 3f;                                     // 格间距（上面已有 gap = 正极凸起间距）
                float cw = (maxW - cgap * (CELLS - 1)) / CELLS;
                if (cw > 1f)
                {
                    Color colOn = Color.FromArgb(46, 184, 114);      // 亮格
                    Color colDim = Color.FromArgb(168, 230, 200);    // 闪烁格的暗相
                    Color colOff = Color.FromArgb(232, 236, 239);    // 空格底
                    for (int i = 0; i < CELLS; i++)
                    {
                        float x = bx + inX + i * (cw + cgap);
                        RectangleF cell = new RectangleF(x, by + inY, cw, maxH);
                        Color c = (i < lit) ? colOn : colOff;
                        if (_charging && i == lit - 1)               // 闪烁格 = 最靠右的亮格
                            c = blinkOn ? colOn : colDim;
                        using (GraphicsPath pth = RoundRect(cell, 3f))
                        using (SolidBrush b = new SolidBrush(c))
                            g.FillPath(b, pth);
                    }
                }

                // 记下格子区域：动画只需重绘这一小块，环带/外壳等不必跟着重画
                _cellRect = Rectangle.Round(new RectangleF(
                    bx + inX - 2f, by + inY - 2f, maxW + 4f, maxH + 4f));
            }

            // 热力环带：以电池中线为界，左半用 t1 的颜色、右半用 t2 的颜色
            private void DrawHeatRing(Graphics g, RectangleF body, float w)
            {
                Color c1 = double.IsNaN(_t1) ? Color.FromArgb(176, 184, 192) : HeatColor(_t1);
                Color c2 = double.IsNaN(_t2) ? Color.FromArgb(176, 184, 192) : HeatColor(_t2);

                RectangleF outer = RectangleF.Inflate(body, w, w);
                float mid = outer.X + outer.Width / 2f;
                RectangleF clipL = new RectangleF(outer.X - 1f, outer.Y - 1f, outer.Width / 2f + 1f, outer.Height + 2f);
                RectangleF clipR = new RectangleF(mid, outer.Y - 1f, outer.Width / 2f + 1f, outer.Height + 2f);

                GraphicsState st = g.Save();
                g.SetClip(clipL);
                FillRing(g, outer, body, c1);
                g.Restore(st);

                st = g.Save();
                g.SetClip(clipR);
                FillRing(g, outer, body, c2);
                g.Restore(st);
            }

            // 画一圈"外扩轮廓 − 内轮廓"的环，内圈掏空
            private void FillRing(Graphics g, RectangleF outer, RectangleF body, Color c)
            {
                using (GraphicsPath po = RoundRect(outer, 9f))
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, po);
                using (GraphicsPath pi = RoundRect(body, 7f))
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, pi);
            }
        }
    }
}
