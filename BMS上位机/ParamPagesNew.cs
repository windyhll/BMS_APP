using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BMS上位机
{
    // ==================================================================================
    //  「基本参数」/「校准控制」两页重排（2026-09-22 第三版）
    //
    //  与「电池信息」页同一套做法：**只用 Dock / 百分比 / 统一像素步距，不做坐标缩放**。
    //
    //   1) 每个原有 groupBox → 一张卡片，**高度由内容决定**（标题 + 行数 × 行距），不再按设计比例拉伸
    //      ⇒ 卡片不留白；整页高度 = 各卡片需要高之和；比视口矮就靠字号放大填满、比视口高就出滚动条。
    //   2) 卡片内部自动转网格：
    //        · 按 Y 聚"行"（容差 = 控件平均高的 0.55）；
    //        · 列结构取**控件最多的那一行**（参考行）——设计稿里那一行才是完整的；
    //        · 其它行的控件按"落在哪一列的区间里"归位，不再用序号硬套
    //          （上一版用序号，导致"充电高温次数"落到窄列、"循环次数"折成两行）。
    //   3) **行距等距**：卡片内部"行高 = 行数等分可用高"（每行最多差 1px），
    //      且行高总和 == 表格高度 ⇒ 表格里没有余量。
    //      ⚠️ 这里有个必须踩过的坑（2026-09-22 逐像素量用户截图才定位到）：
    //         行高总和 < 表格高度时，TableLayoutPanel 会把"剩下的一截"**全部塞给最后一行**
    //         ⇒ 最后一行被顶到底部，看起来就是"行间距突然变大"。
    //         （截图实测：某卡片前 4 行间距 37、末行 71；另一张 37/37/37/37/93，每张卡片都如此）
    //         ⇒ 所以表格高度必须**恰好等于行高之和**，一行余量都不许留。
    //   4) **字号随窗口缩放**：窗口变大 → 字跟着放大（1.00~1.45），先把窗口填满；
    //      字号到顶还有富余才用行距吸收（每行最多 +6px）⇒ 窗口变大是"整体放大"，不是"留一片空白"。
    //   5) **文字不再被裁**：
    //        · Label 关掉 AutoSize（AutoSize 的 Label 放进比文字窄的单元格里会被
    //          TableLayoutPanel 折行 —— 上一版"循环次数"被折成两行的根因），
    //          宽度按实测文字放宽；
    //        · 按钮/标签高度在布局时按所在行高收口，不会超出单元格被切掉
    //          （上一版窗口变矮时"电流校准"四个按钮上下都被切）。
    //
    //  回退：USE_NEW_PARAM_PAGES = false，两页立刻恢复成 Designer 里的老样子。
    // ==================================================================================
    public partial class Form1
    {
        private static readonly bool USE_NEW_PARAM_PAGES = true;

        // ---------------------------------------------------------------- 统一规格
        private const string PP_FONT = "Microsoft YaHei";   // 全页统一字体（正文 + 标题）
        private const float PP_FONT_SIZE = 9f;              // 控件基准字号
        private const float PP_TITLE_SIZE = 9.5f;           // 卡片标题基准字号
        private const int PP_TITLE_H = 22;                  // 卡片标题条高度
        private const int PP_PAD = 4;                       // 卡片内容与卡片边框的间距
        private const float PP_PITCH = 40f;                 // 设计行距参考值（px，仅用于"设计稿是否大间距"判断）
        private const float PP_ROW_GAP = 8f;                // **全页统一的行间距**：行距 = 控件高 + 该值
                                                            //  ⚠️ 它同时决定"输入框圆角框之间的缝"：
                                                            //     缝 = PP_ROW_GAP - 2×PP_FIELD_PAD
                                                            //     6 与 2×3 相等时缝为 0 ⇒ 框会**连成一片**（2026-09-23 用户截图）
        private const float PP_MIN_ROW = 28f;               // 每行"至少要放得下输入框"的高度（设计 px，9pt 时）
        private const float PP_KMIN = 1.00f;                // 字号缩放下限 —— **已停用**（字号固定，见 ApplyUiScale）
        private const float PP_KMAX = 1.00f;                // 字号缩放上限 —— **已停用**；同时用于"标签宽度按几号字量"
        private const float PP_FILL_GAP = 6f;               // 内容比视口矮时 → 用行距吸收余量（每行最多 +6px，全页统一 ⇒ 仍等距）
        private const int PP_MIN_W = 1300;                  // 页面小于这个宽度就出横向滚动条
        private const int PP_DESIGN_H = 906;                // 设计页高（滚动的基准）

        private static readonly Color PP_BG = Color.FromArgb(240, 242, 245);      // 页面底色
        private static readonly Color PP_CARD = Color.White;                      // 卡片底
        private static readonly Color PP_EDGE = Color.FromArgb(222, 228, 236);    // 卡片边
        private static readonly Color PP_TITLE = Color.FromArgb(38, 50, 64);      // 卡片标题
        private static readonly Color PP_ACCENT = Color.FromArgb(47, 128, 237);   // 强调色
        private static readonly Color PP_BTN_EDGE = Color.FromArgb(199, 210, 222);
        private static readonly Color PP_FIELD_EDGE = Color.FromArgb(184, 196, 210);   // 输入框/下拉框的圆角边
        private const float PP_FIELD_PAD = 2f;          // 圆角框比控件外扩多少 px（框之间的缝 = PP_ROW_GAP - 2×本值）
        private const float PP_FIELD_RADIUS = 3f;       // 圆角框半径
        private const int PP_FIELD_NUDGE = 0;           // 框的垂直微调（px，正 = 往下）——实机若还差一两像素就改这里

        private class ParamCard
        {
            public Control Card;
            public TableLayoutPanel Table;
            public bool HasTitle;
            public int Rows;
            public float Pitch = PP_PITCH;             // 本卡片的设计行距（各行等距；首行见 ApplyCardGeometry）
            public bool BigPitch;                      // 设计稿行距远大于控件高（左列 4 个大按钮）⇒ 按设计行距铺开
            public List<RowStyle> RowStyles = new List<RowStyle>();   // 直接持有对象改，避免动 RowStyles 索引器
            public List<Control> Kids = new List<Control>();
            public Control ParentGrid;                 // 外层网格（同一网格同一列的卡片要对齐列宽）
            public int GridCol;
            public int ColCount;
            public float[] ColW;                       // 列宽权重（建页时算好，供兄弟卡片对齐用）
            public TabPage Page;                       // 属于哪一页（算页高用）
            public int DesignTopPage;                  // 卡片在设计稿里的 Y（相对原父，AddCard 时抓）
            public string HeaderText;                  // 需要额外补的"说明行"文字（见 PP_HEADER_FIX）；空 = 不补
            public float DesignFullH;                  // 卡片的设计高度（算页高用）
            // 上次铺过的可用高/宽/字号：三者都没变 ⇒ 这张卡片不用重铺（省掉重排 + 重画）
            public int LastFillH = -1, LastFillW = -1;
            public float LastFillKf = -1f;
            public Dictionary<Control, Font> BaseFont = new Dictionary<Control, Font>();   // 基准字号（未缩放）
            public Dictionary<Control, float> DesignH = new Dictionary<Control, float>();  // 设计高度
            public Dictionary<Control, int> RowOf = new Dictionary<Control, int>();        // 所在行号
            public Dictionary<Control, int> ColOf = new Dictionary<Control, int>();        // 所在列号（算输入框宽度用）
            public int[] LastRh;                                                           // 上次算出的行高（对齐控件用）
        }

        private readonly List<ParamCard> _paramCards = new List<ParamCard>();
        private TableLayoutPanel _ppPage1Root, _ppPage3Root;    // 两页的根网格（尺寸由 ApplyCardGeometry 定：不小于设计内容）
        private readonly Dictionary<Control, string> _cardTitles = new Dictionary<Control, string>();
        private readonly Dictionary<string, Font> _scaledFonts = new Dictionary<string, Font>();
        private float _fontKApplied = 1f;
        private int _basePageH, _basePageW;
        private bool _paramBuilt, _pagesFixed;
        private float _pitchNow = 30f;      // 当前行距（铺的时候要用）
        // 行距 / 根高的缓存：`GridNeedH()` 是递归算法，行距没变就不必重算（拖窗口时省掉最大的一块）
        private float _basePitch;           // 不含"行距吸收"的基准行距（= 本页最高输入框高 + PP_ROW_GAP）
        private int _baseRootH;             // 上面那个行距对应的根网格高
        private float _lastPitch;           // 含吸收量的行距
        private int _rootHForPitch;         // 该行距对应的根网格高
        private float _uiKf = 1f;           // **三个页面共用**的缩放系数（只由窗口大小决定，与当前在哪一页无关）
        private Font _tabFontBase, _tabFontOnBase;   // 顶部标签条的基准字号（也一起缩放）
        private readonly Dictionary<Control, Font> _battFonts = new Dictionary<Control, Font>();  // 电池信息页的基准字号
        private readonly Dictionary<Control, ParamCard> _cardByCtl = new Dictionary<Control, ParamCard>();
        // 上次处理过的视口尺寸：尺寸没变就直接跳过（拖窗口时 Resize 连发几十次，省掉重复重排）
        private readonly Dictionary<Control, Size> _lastPageSize = new Dictionary<Control, Size>();
        private bool _geoScheduled;
        private System.Windows.Forms.Timer _layoutTimer;      // 防抖：拖窗口/最大化时只做轻量更新，尺寸稳定后再重排一次
        private bool _mainSizePending;                        // TabControl 尺寸变更待应用（跟上面同一个防抖）
        private readonly Dictionary<Control, float> _inputHCache = new Dictionary<Control, float>();  // PageInputH 结果缓存

        // ---------------------------------------------------------------- 主窗体尺寸（TabControl + 状态条）
        // 🚨 这个动作是"重活"：改 tabControl1 尺寸会让**整棵控件树**重排一次（里面还挂着电池信息页的
        //    4 个 DataGridView，每个都要重算列宽/行高）。拖窗口时 Resize 连发几十次 ⇒ 每次都全排一遍 = 卡。
        //    ⇒ 交给 140ms 防抖（与参数页同一趟），拖拽期间只更新状态条那种"零成本"的东西。
        //    ⚠️ 必须在 LayoutParamPages() **之前**执行：后者要读 tabControl1.DisplayRectangle 和 page.ClientSize。
        private void ApplyMainSize()
        {
            if (!USE_NEW_BATT_PAGE) return;
            try
            {
                int cw = this.ClientSize.Width;
                int ch = this.ClientSize.Height;
                if (cw < 100 || ch < 100) return;
                if (tabControl1 == null) return;

                int left = tabControl1.Left;
                int top = tabControl1.Top;
                int w = cw - left * 2;
                int h = ch - top - 46;
                if (w > 300 && h > 200 && (tabControl1.Width != w || tabControl1.Height != h))
                    tabControl1.Bounds = new Rectangle(left, top, w, h);
            }
            catch { }
        }

        // ---------------------------------------------------------------- 建页
        // ⚠️ 故意不放在 Form1 构造函数里：Visual Studio 有时会用旧的内存副本覆盖 Form1.cs，
        //    把构造函数里加的调用抹掉（2026-09-21 就发生过）。改成由 LayoutParamPages() 首次调用时
        //    自触发，本文件之外一行都不用改。
        private void BuildParamPages()
        {
            if (_paramBuilt || !USE_NEW_PARAM_PAGES) return;
            _paramBuilt = true;
            // 花屏是"半成品画面被画出来"，所以整条链上的容器都要开双缓冲
            EnableDoubleBuffer(this);
            EnableDoubleBuffer(tabControl1);
            EnableDoubleBuffer(tabPage1);
            EnableDoubleBuffer(tabPage3);
            // 切页时补铺一次（因为只铺当前可见页）：第一次显示/尺寸变了都要重新铺
            try
            {
                tabControl1.SelectedIndexChanged += delegate
                {
                    if (tabControl1.SelectedTab == tabPage1 || tabControl1.SelectedTab == tabPage3)
                    {
                        _lastPageSize.Clear();
                        ScheduleGeometry();
                    }
                };
            }
            catch { }
            try
            {
                // 窗体真正显示时（AutoScale/DPI 都已应用）把页面高度定成字号基准，
                // 这样无论在什么缩放环境下启动，初始字号都正好是设计大小。
                this.Shown += delegate
                {
                    _basePageH = 0;
                    _basePageW = 0;
                    _fontKApplied = 1f;
                    _lastPageSize.Clear();          // 基准变了 ⇒ 上次尺寸缓存作废，必须重排一次
                    ApplyMainSize();                // 启动时直接到位（不等防抖）
                    LayoutParamPages();
                };
            }
            catch { }
            try { BuildBasicParamsPage(); } catch { }
            try { BuildCaliParamsPage(); } catch { }
            try { AlignSiblingColumns(); } catch { }      // 同一网格同一列的卡片（均衡参数/其他参数）列宽对齐
            try { AlignSiblingRows(); } catch { }         // 设计稿缺说明行的卡片：补一行说明（见 PP_HEADER_FIX）
        }

        // 同一外层网格、同一列的卡片，如果列数一样，就把列宽权重取最大值 ⇒ 上下两张卡片的框框对齐
        //（例：均衡参数 与 其他参数 同列堆叠，设计稿里 input 起点差 15px，看着就是"没对齐"）
        private void AlignSiblingColumns()
        {
            List<List<ParamCard>> groups = new List<List<ParamCard>>();
            List<Control> gGrid = new List<Control>();
            List<int> gCol = new List<int>();
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                // 归属页在这里统一补：建卡片时嵌套网格可能还没挂进页面（会被漏掉）
                Control up = pc.ParentGrid;
                pc.Page = null;
                while (up != null)
                {
                    TabPage tp = up as TabPage;
                    if (tp != null) { pc.Page = tp; break; }
                    up = up.Parent;
                }
                if (pc.ParentGrid == null || pc.ColW == null) continue;
                int gi = -1;
                for (int k = 0; k < groups.Count; k++)
                    if (object.ReferenceEquals(gGrid[k], pc.ParentGrid) && gCol[k] == pc.GridCol) { gi = k; break; }
                if (gi < 0)
                {
                    groups.Add(new List<ParamCard>());
                    gGrid.Add(pc.ParentGrid);
                    gCol.Add(pc.GridCol);
                    gi = groups.Count - 1;
                }
                groups[gi].Add(pc);
            }
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<ParamCard> lst = groups[gi];
                if (lst.Count < 2) continue;
                int n = lst[0].ColCount;
                bool same = n > 0;
                for (int i = 1; i < lst.Count && same; i++)
                    if (lst[i].ColCount != n) same = false;
                if (!same) continue;                  // 结构不同就不硬凑（会把某一列撑得很怪）
                for (int j = 0; j < n; j++)
                {
                    float mx = 0f;
                    for (int i = 0; i < lst.Count; i++)
                        if (lst[i].ColW[j] > mx) mx = lst[i].ColW[j];
                    for (int i = 0; i < lst.Count; i++)
                    {
                        lst[i].ColW[j] = mx;
                        if (lst[i].Table != null && j < lst[i].Table.ColumnStyles.Count)
                            lst[i].Table.ColumnStyles[j].Width = mx;
                    }
                }
            }
        }

        // 设计稿里"缺一行说明"的卡片：这里给它在最前面补一行**带文字的说明行**。
        //  key = 卡片控件名（Designer 里的 Name），value = 那一行显示的文字。
        //  实测（用户截图逐像素量，2026-09-23）：校准控制页三张卡片的文字行中心 ——
        //    工厂模式卡：On/OFF(表头) → 均衡 78.5 → 加热 112.5 …（行距 34）
        //    电流校准卡：电流(表头)   → 静态校准 79  → 充电校准 113 …
        //    开关控制卡：**没有表头**  → 市电优先开关 **44.5** → 开关自锁功能 78.5 …
        //  ⇒ 开关控制卡的数据整整比旁边两卡高**一行**（44.5 vs 78.5/79，因为它那张卡的设计稿里
        //    没有说明行）。⇒ 补一行说明文字（居中、跨所有列），数据行就与旁边齐平，
        //    其余各行依次往下、间距不变。
        private static readonly Dictionary<string, string> PP_HEADER_FIX =
            new Dictionary<string, string>
            {
                { "groupBox7", "模式" },     // 校准页「开关控制」：市电优先开关 / 开关自锁功能 / 电量学习 / …
            };

        private void AlignSiblingRows()
        {
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                if (pc.Card == null || pc.Table == null || pc.Rows < 2) continue;
                string txt;
                if (PP_HEADER_FIX.TryGetValue(pc.Card.Name, out txt) && !string.IsNullOrEmpty(txt))
                    pc.HeaderText = txt;
            }
        }
        // ① 「基本参数」：左列 4 个大按钮 + 上半 3 块 + 下半 3 块
        private void BuildBasicParamsPage()
        {
            if (tabPage1 == null) return;
            tabPage1.BackColor = PP_BG;
            tabPage1.AutoScroll = true;                  // 页面比内容小 → 出滚动条（内容不再被压扁/切掉）

            TableLayoutPanel root = NewParamGrid(
                new float[] { 173, 831, 257, 372 },     // ①左按钮列 ②基本参数 ③功能设置 ④容量参数
                new float[] { 524, 382 });              // ①上半区  ②下半区
            root.Padding = new Padding(6);
            root.BackColor = PP_BG;
            root.Dock = DockStyle.None;                  // 不用 Fill：内容比视口大时才能出滚动条
            root.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            root.Location = new Point(0, 0);
            _ppPage1Root = root;
            tabPage1.Controls.Add(root);

            AddCard(root, groupBox8, 0, 0, 1, 2);
            AddCard(root, groupBox10, 1, 0);
            AddCard(root, groupBox13, 2, 0);
            AddCard(root, groupBox11, 3, 0);

            TableLayoutPanel low = NewParamGrid(
                new float[] { 393, 395 },
                new float[] { 129, 202 });
            AddCard(low, groupBox5, 0, 0, 1, 2);
            AddCard(low, groupBox12, 1, 0);
            AddCard(low, groupBox14, 1, 1);
            root.Controls.Add(low, 1, 1);

            // ⚠️ Designer 里「日」的 Y 写错了（落在"过压次数"那一行），先归位到"生产日期"行，
            //    否则它会跟着那一行跑、并且给"日"单开一列。
            PutIntoRow(label36, label35);

            AddCard(root, groupBox9, 2, 1, 2, 1);       // 辅助参数：跨③④两列
        }

        // ② 「校准控制」：三列竖排。「电流校准」「SOC修正」里有 16 个控件其实挂在页面上，
        //     只是视觉上落在框里 —— 这里按坐标换算并回各自的卡片。
        private void BuildCaliParamsPage()
        {
            if (tabPage3 == null) return;
            tabPage3.BackColor = PP_BG;
            tabPage3.AutoScroll = true;

            TableLayoutPanel root = NewParamGrid(new float[] { 520, 580, 533 }, new float[] { 906 });
            root.Padding = new Padding(6);
            root.BackColor = PP_BG;
            root.Dock = DockStyle.None;
            root.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            root.Location = new Point(0, 0);
            _ppPage3Root = root;
            tabPage3.Controls.Add(root);

            TableLayoutPanel c0 = NewParamGrid(new float[] { 520 }, new float[] { 339, 567 });
            AddCard(c0, groupBox6, 0, 0);
            root.Controls.Add(c0, 0, 0);

            TableLayoutPanel c1 = NewParamGrid(new float[] { 580 }, new float[] { 332, 10, 147, 417 });
            AddCard(c1, groupBox7, 0, 0);
            AddCard(c1, groupBoxID, 0, 2);
            root.Controls.Add(c1, 1, 0);

            TableLayoutPanel c2 = NewParamGrid(new float[] { 533 }, new float[] { 250, 8, 147, 10, 147, 8, 243, 93 });
            AddCard(c2, currentCali, 0, 0, 1, 1, new Control[]
            {
                label40, btnZeroCurrentCali, CaliZeroCurr, label44,
                btnChgCurrentCali, CaliChgCurr, label138,
                btnDsgCurrentCali, CaliDsgCurr, label139
            });
            AddCard(c2, groupBox1, 0, 2, 1, 1, new Control[]
            {
                CaliView_Soc, label41, label43, btnSocCali, CaliModify_SOC, label42
            });
            AddCard(c2, groupBox3, 0, 4);
            AddCard(c2, groupBox4, 0, 6);
            root.Controls.Add(c2, 2, 0);
        }

        private static TableLayoutPanel NewParamGrid(float[] cols, float[] rows)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.Margin = new Padding(0);
            t.Padding = new Padding(0);
            t.ColumnCount = cols.Length;
            t.RowCount = rows.Length;
            for (int i = 0; i < cols.Length; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, cols[i]));
            for (int i = 0; i < rows.Length; i++) t.RowStyles.Add(new RowStyle(SizeType.Percent, rows[i]));
            EnableDoubleBuffer(t);              // 嵌套网格也要双缓冲（NewParamGrid 也用于列容器）
            return t;
        }

        private void AddCard(TableLayoutPanel parent, Control card, int col, int row,
                             int colSpan = 1, int rowSpan = 1, Control[] extras = null)
        {
            if (parent == null || card == null) return;
            int before = _paramCards.Count;
            int dTop = card.Top;                         // 设计稿里的 Y（此时还没换父，是页面坐标）
            ConvertCard(card, extras);
            if (_paramCards.Count > before)              // 记住它是挂在哪个网格的哪一列（列宽对齐用）
            {
                ParamCard pc = _paramCards[_paramCards.Count - 1];
                pc.ParentGrid = parent;
                pc.GridCol = col;
                pc.DesignTopPage = dTop;                 // 同行卡片"行对齐"用（见 AlignSiblingRows）
                Control p = parent;                      // 往上找所属 TabPage（算页高用）
                while (p != null && !(p is TabPage)) p = p.Parent;
                pc.Page = p as TabPage;
            }
            // ⚠️ 不能 Dock=Fill：卡片高度改由"内容"决定（FillCard 里设），
            //    否则会被拉到整行高 → 行数少的卡片行距被撑得很大（用户反馈过的"行距忽大忽小"）
            card.Dock = DockStyle.None;
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Margin = new Padding(3);
            parent.Controls.Add(card, col, row);
            if (colSpan > 1) parent.SetColumnSpan(card, colSpan);
            if (rowSpan > 1) parent.SetRowSpan(card, rowSpan);
        }

        // 把 me 挪到 like 所在的那一行（Designer 里个别控件 Y 写错时的兜底）
        private static void PutIntoRow(Control like, Control me)
        {
            if (like == null || me == null || like.Parent != me.Parent) return;
            me.Top = like.Top;
        }

        // ---------------------------------------------------------------- 卡片内部自动转网格
        private void ConvertCard(Control card, Control[] extras)
        {
            GroupBox gb = card as GroupBox;
            bool hasTitle = gb != null && !string.IsNullOrEmpty(gb.Text);
            if (hasTitle)
            {
                _cardTitles[card] = gb.Text;
                gb.Text = "";                       // 标题自己画，去掉系统那个
                gb.FlatStyle = FlatStyle.Flat;
            }
            card.BackColor = PP_CARD;
            EnableDoubleBuffer(card);
            card.Paint += ParamCardPaint;

            Size design = card.ClientSize;          // ⚠️ 必须在 Dock=Fill 之前记（缩放基准）
            Point cOff = card.ClientRectangle.Location;

            List<Control> kids = new List<Control>();
            foreach (Control c in card.Controls) kids.Add(c);
            if (extras != null)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    Control f = extras[i];
                    if (f == null) continue;
                    // 散件位置是相对"页面"的，换成相对卡片客户区
                    Point rel = new Point(f.Left - card.Left - cOff.X, f.Top - card.Top - cOff.Y);
                    card.Controls.Add(f);
                    f.Location = rel;
                    kids.Add(f);
                }
            }

            List<Control> vis = new List<Control>();
            foreach (Control c in kids)
                if (c != null && c.Width >= 2 && c.Height >= 2) vis.Add(c);   // 丢掉 0 宽占位 Label
            if (vis.Count == 0) return;

            ParamCard pc = new ParamCard();
            pc.Card = card;
            pc.HasTitle = hasTitle;
            _cardByCtl[card] = pc;               // 反向索引：铺的时候要按控件反查卡片

            // ---- 1) 统一字体 + 美化（记录基准字号，后面按窗口缩放）
            //  ⚠️ 量文字宽度要用"最大字号下"的字体：标签宽度只在建页时算一次，
            //     而字号后面会随窗口放大到 1.3 倍 —— 按 9pt 量就会截字（实测"单节过压"被截成"单节过"）。
            Font bf = GetFont(PP_FONT, PP_FONT_SIZE, FontStyle.Regular);
            Font bfMax = GetFont(PP_FONT, PP_FONT_SIZE * PP_KMAX, FontStyle.Regular);
            for (int i = 0; i < vis.Count; i++)
            {
                Control c = vis[i];
                pc.BaseFont[c] = bf;
                pc.DesignH[c] = c.Height;
                c.Font = bf;
                RestyleKid(c);
            }

            // ---- 2) 按 Y 聚行
            float refH = 0;
            for (int i = 0; i < vis.Count; i++) refH += vis[i].Height;
            refH /= vis.Count;
            float tol = Math.Max(8f, refH * 0.55f);

            vis.Sort(delegate(Control a, Control b)
            {
                int ca = a.Top + a.Height / 2, cb = b.Top + b.Height / 2;
                return (ca != cb) ? (ca - cb) : (a.Left - b.Left);
            });

            List<List<Control>> rows = new List<List<Control>>();
            List<float> cy = new List<float>();
            for (int i = 0; i < vis.Count; i++)
            {
                Control c = vis[i];
                float y = c.Top + c.Height / 2f;
                if (rows.Count == 0 || y - cy[cy.Count - 1] > tol) { rows.Add(new List<Control>()); cy.Add(y); }
                rows[rows.Count - 1].Add(c);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Sort(delegate(Control a, Control b) { return a.Left - b.Left; });
                float s = 0;
                for (int k = 0; k < rows[i].Count; k++) s += rows[i][k].Top + rows[i][k].Height / 2f;
                cy[i] = s / rows[i].Count;
            }

            // ---- 3) 列结构：取"控件最多的那一行"当参考行
            int refRow = 0;
            for (int i = 1; i < rows.Count; i++)
                if (rows[i].Count > rows[refRow].Count) refRow = i;
            List<Control> refCtl = rows[refRow];
            int nCol = refCtl.Count;
            if (nCol < 1) return;

            float[] start = new float[nCol];
            for (int j = 0; j < nCol; j++) start[j] = refCtl[j].Left;
            for (int j = 1; j < nCol; j++)
                if (start[j] < start[j - 1] + 6f) start[j] = start[j - 1] + 6f;   // 保证单调递增

            float W0 = design.Width;
            if (W0 < 80) W0 = start[nCol - 1] + 120;
            float[] colW = new float[nCol];
            for (int j = 0; j < nCol; j++)
                colW[j] = Math.Max(10f, ((j < nCol - 1) ? start[j + 1] : W0) - start[j]);

            // ---- 4) 每个控件归到哪一列：先看"落在哪一列区间里"，再退化为就近取空列
            int[][] colOf = new int[rows.Count][];
            for (int i = 0; i < rows.Count; i++)
            {
                colOf[i] = new int[rows[i].Count];
                bool[] used = new bool[nCol];
                for (int j = 0; j < rows[i].Count; j++)
                {
                    float lf = rows[i][j].Left;
                    int k = nCol - 1;
                    if (lf < start[0]) k = 0;
                    else
                        for (int q = 0; q < nCol - 1; q++)
                            if (lf >= start[q] && lf < start[q + 1]) { k = q; break; }
                    // 贴着下一列起点（设计稿里差几个像素）时归到下一列，否则"mS"这类会被算进上一列
                    if (k < nCol - 1 && start[k + 1] - lf <= 4f) k++;
                    if (used[k])
                    {
                        int best = -1; float bd = float.MaxValue;
                        for (int q = 0; q < nCol; q++)
                        {
                            if (used[q]) continue;
                            float d = Math.Abs(start[q] - lf);
                            if (d < bd) { bd = d; best = q; }
                        }
                        if (best >= 0) k = best;
                    }
                    used[k] = true;
                    colOf[i][j] = k;
                }
            }

            // ---- 4b) 列宽兜底：任何一列都不能比"该列里最宽的控件（按最大字号量）"更窄
            //      （标签宽度是按 11.7pt 量的，列按 9pt 的设计宽度给就会截字）
            float[] need = new float[nCol];
            for (int j = 0; j < nCol; j++) need[j] = 10f;
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < rows[i].Count; j++)
                {
                    Control c = rows[i][j];
                    int k = colOf[i][j];
                    float w = c.Width;
                    Label lb0 = c as Label;
                    if (lb0 != null) w = Math.Max(w, MeasuredTextW(lb0.Text, bfMax) + 8);
                    // 按钮的文字也要量：列被压窄时按钮文字会被切（用户看到"PWM校准"变"WM校准"）
                    Button bt0 = c as Button;
                    if (bt0 != null) w = Math.Max(w, MeasuredTextW(bt0.Text, bfMax) + 18);
                    if (w > need[k]) need[k] = w;
                }
            for (int j = 0; j < nCol; j++)
                if (colW[j] < need[j]) colW[j] = need[j];

            // 加宽后总和可能超卡片宽度 → 先从"有富余的列"里扣（保文字列不被压扁）
            float avail = Math.Max(60f, design.Width - PP_PAD * 2);
            float tot = 0;
            for (int j = 0; j < nCol; j++) tot += colW[j];
            if (tot > avail)
            {
                float over = tot - avail, slack = 0;
                for (int j = 0; j < nCol; j++) slack += Math.Max(0f, colW[j] - need[j]);
                if (slack > over + 1f)
                {
                    for (int j = 0; j < nCol; j++)
                    {
                        float sl = Math.Max(0f, colW[j] - need[j]);
                        if (sl > 0f) colW[j] -= over * sl / slack;
                    }
                }
                else
                {
                    float s = avail / tot;
                    for (int j = 0; j < nCol; j++) colW[j] *= s;
                }
            }

            // ---- 5) 建表（行高先用"统一行距"的百分比，实际像素在 ApplyCardGeometry 里定死）
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.None;
            t.Margin = new Padding(0);
            t.Padding = new Padding(0);
            t.BackColor = PP_CARD;
            t.ColumnCount = nCol;
            t.RowCount = rows.Count;
            for (int j = 0; j < nCol; j++)
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, colW[j]));
            for (int i = 0; i < rows.Count; i++)
            {
                RowStyle rs = new RowStyle(SizeType.Percent, 100f);
                t.RowStyles.Add(rs);
                pc.RowStyles.Add(rs);            // 记下来，布局时直接改这个对象（不去动 RowStyles 索引器）
            }

            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < rows[i].Count; j++)
                {
                    Control c = rows[i][j];
                    int k = colOf[i][j];
                    c.Dock = DockStyle.None;

                    Label lb = c as Label;
                    if (lb != null)
                    {
                        // AutoSize 的 Label 一旦落在比文字窄的单元格里就会被折行/截字
                        // ⇒ 关掉 AutoSize；宽度按"最大字号"实测放宽（列宽已保证不小于它）
                        lb.AutoSize = false;
                        int w = Math.Max(MeasuredTextW(lb.Text, bfMax) + 6, lb.Width);
                        lb.Width = w;
                    }

                    // 🚨 `Anchor = None` 是**故意的**：这样 `TableLayoutPanel` 会把控件在单元格里
                    //    **水平+垂直都居中**（原生行为、且后续任何一次容器重排都保持这个结果）。
                    //    改成 `Left|Right` 时容器会把控件**贴单元格上边**摆 —— 那就是用户看到的
                    //    "字不在框框中心 / 字和框没对齐"（2026-09-23 实测：框中心 24、文字中心 17~20）。
                    //    宽度不靠 Anchor 拉伸，由 `FillCardWidths()` 按实际列宽显式设（见该方法说明）。
                    c.Anchor = AnchorStyles.None;

                    pc.Kids.Add(c);
                    pc.RowOf[c] = i;
                    pc.ColOf[c] = k;
                    t.Controls.Add(c, k, i);
                }

            int top = hasTitle ? PP_TITLE_H + 6 : 6;
            t.Location = new Point(PP_PAD, top);
            t.Size = new Size(Math.Max(40, design.Width - PP_PAD * 2),
                              Math.Max(30, design.Height - top - 6));
            t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            card.Controls.Add(t);
            t.BringToFront();

            EnableDoubleBuffer(t);
            // 输入框 / 下拉框的圆角外框：挂在**表格自己**的 Paint 上（画在卡片上会被表格白底盖掉）
            t.Paint += TablePaintFrames;
            pc.Table = t;
            pc.Rows = rows.Count;
            pc.ColCount = nCol;
            pc.ColW = colW;
            pc.DesignFullH = design.Height;

            // 本卡片的设计行距 = 各行间距的中位数（末行不参与，它后面没有下一行）
            // 用途：判断这张卡片在设计稿上是不是"大间距"（例如左侧那列 4 个大按钮，间距 ~165px）——
            // 是的话布局时让它铺满整列；否则一律用全页统一行距（控件高 + PP_ROW_GAP）。
            if (rows.Count >= 2)
            {
                List<float> gaps = new List<float>();
                for (int i = 0; i + 1 < rows.Count; i++) gaps.Add(cy[i + 1] - cy[i]);
                gaps.Sort();
                pc.Pitch = Clamp(gaps[gaps.Count / 2], 24f, 170f);
            }

            // 设计稿本身就是"大间距"的卡片：目前只有左列那 4 个大按钮（设计行距 ≈165px）。
            // 这种卡片要按**设计行距**铺开，否则 4 个按钮会挤在顶部、下面空一大片
            // （用户实测："左边为什么被改掉了"）。
            pc.BigPitch = (pc.Pitch > CardCtrlH(pc) + 40f);

            _paramCards.Add(pc);
        }

        // 实测文字宽度（不带 GDI 的额外内边距，便于精确算列宽）
        private static int MeasuredTextW(string text, Font f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            try
            {
                return TextRenderer.MeasureText(text, f, new Size(2000, 100),
                                                TextFormatFlags.NoPadding).Width;
            }
            catch
            {
                // 兜底：按"一个汉字 ≈ 0.9 个字高"估（原先写死 ×9，字号大了会严重低估 → 标签被截字）
                return text.Length * (int)Math.Ceiling(f.Height * 0.9f);
            }
        }

        // 卡片自绘：铺页面底色盖掉系统 3D 边框 → 内缩 4px 画圆角白卡 + 细边 + 左上蓝条 + 标题
        private void ParamCardPaint(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle b = c.ClientRectangle;
            if (b.Width < 16 || b.Height < 16) return;

            using (SolidBrush bg = new SolidBrush(PP_BG)) g.FillRectangle(bg, b);

            const int pad = PP_PAD;
            Rectangle r = new Rectangle(b.X + pad, b.Y + pad,
                                        Math.Max(4, b.Width - pad * 2), Math.Max(4, b.Height - pad * 2));
            using (GraphicsPath p = RoundRectF(r, 6f))
            {
                using (SolidBrush w = new SolidBrush(PP_CARD)) g.FillPath(w, p);
                using (Pen pen = new Pen(PP_EDGE)) g.DrawPath(pen, p);
            }

            // ⚠️ 输入框/下拉框的圆角框**不能在这里画**（卡片自绘会被内层表格的白底盖掉，
            //    见 `TablePaintFrames` 的注释）—— 已改到表格自己的 Paint 里画。

            string title;
            if (_cardTitles.TryGetValue(c, out title) && !string.IsNullOrEmpty(title))
            {
                // ⚠️ 字体来自缓存，不能 using（否则把缓存里的共享字体给 Dispose 了）
                float k = _fontKApplied <= 0f ? 1f : _fontKApplied;
                Font f = GetFont(PP_FONT, PP_TITLE_SIZE * k, FontStyle.Bold);
                using (SolidBrush tb = new SolidBrush(PP_TITLE))
                using (SolidBrush ab = new SolidBrush(PP_ACCENT))
                {
                    g.FillRectangle(ab, r.X + 8f, r.Y + 6f, 3f, 13f);
                    g.DrawString(title, f, tb, r.X + 16f, r.Y + 4f);
                }
            }

            // 「说明行」（设计稿缺的那一行，见 PP_HEADER_FIX）：画在**标题下方、表格上方**那块空行里。
            //  ⚠️ 只能画在这里、不能放控件：表格白底会把卡片自绘盖掉（见 `TablePaintFrames` 注释）；
            //     而这一行在表格**上方**（FillCard 把 t.Top 下移了一行），所以不会被盖。
            ParamCard pcH = FindCardOf(c);
            if (pcH != null && !string.IsNullOrEmpty(pcH.HeaderText))
            {
                int y0 = pcH.HasTitle ? PP_TITLE_H + 6 : 6;
                int y1 = (pcH.Table != null) ? pcH.Table.Top : 0;
                if (y1 > y0 + 6)
                {
                    Font hf = GetFont(PP_FONT, PP_FONT_SIZE, FontStyle.Regular);
                    using (SolidBrush hb = new SolidBrush(Color.FromArgb(95, 94, 90)))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        RectangleF hr = new RectangleF(r.X + 4f, y0, Math.Max(20f, r.Width - 8f), y1 - y0);
                        g.DrawString(pcH.HeaderText, hf, hb, hr, sf);
                    }
                }
            }
        }

        // 输入框 / 下拉框的**圆角外框**（画在**内层表格自己**的 Paint 上，基准是**控件自己的 Bounds**）。
        //  🚨 2026-09-23 两个坑都踩过，别再动这两点：
        //  ① **不能画在卡片上**（ParamCardPaint）：WinForms 绘制顺序是「父 OnPaint → 子 OnPaint」，
        //     卡片画的框会被内层表格那层不透明的白底**整块抹掉** ⇒ 用户看到"框框都不显示了"。
        //  ② **不能按"行/列"累加去算框的位置**（`GetRowHeights()` 那套）：那是表格的内部值，
        //     和控件实际被摆在哪儿不保证一致 ⇒ 框与文字整体错开几个像素（"改了 5 版都一样"）。
        //     现在直接以 `c.Bounds`（表格布局后的**真实值**）为中心外扩 `PP_FIELD_PAD`：
        //     控件由容器（`Anchor = None`）在单元格内居中、控件高度又贴合文字
        //     ⇒ 框、控件、文字三者构造上同心。
        //  坐标：`e.Graphics` 原点就是表格客户区左上角，`c.Bounds` 同坐标系，直接用。
        private void TablePaintFrames(object sender, PaintEventArgs e)
        {
            if (!PP_ROUND_FIELDS) return;
            TableLayoutPanel t = sender as TableLayoutPanel;
            if (t == null || t.IsDisposed) return;

            Graphics g = e.Graphics;
            Rectangle clip = t.ClientRectangle;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(PP_FIELD_EDGE, 1f))
            {
                for (int i = 0; i < t.Controls.Count; i++)
                {
                    Control c = t.Controls[i];
                    if (c == null || c.IsDisposed || !c.Visible) continue;
                    if (!(c is TextBox) && !(c is ComboBox)) continue;

                    RectangleF fr = RectangleF.Inflate(c.Bounds, PP_FIELD_PAD, PP_FIELD_PAD);
                    fr.Y += PP_FIELD_NUDGE;

                    // 夹在表格客户区内（别压到相邻格子、也别顶出卡片）
                    if (fr.X < 0f) { fr.Width += fr.X; fr.X = 0f; }
                    if (fr.Y < 0f) { fr.Height += fr.Y; fr.Y = 0f; }
                    if (fr.Right > clip.Width) fr.Width = clip.Width - fr.X;
                    if (fr.Bottom > clip.Height) fr.Height = clip.Height - fr.Y;
                    if (fr.Width < 8f || fr.Height < 8f) continue;

                    using (GraphicsPath p = RoundRectF(fr, PP_FIELD_RADIUS)) g.DrawPath(pen, p);
                }
            }
        }

        // 输入框 / 下拉框样式开关：
        //   true  = **自绘圆角框**（原生控件不画边框，框由 `TablePaintFrames` 画在表格上）
        //   false = 用系统原生方框（`FixedSingle` / `FlatStyle.Standard`）——自绘出问题时一键回退
        private const bool PP_ROUND_FIELDS = true;

        // 统一美化：输入框圆角、下拉扁平、按钮扁平浅色（不覆盖按钮自身 BackColor）
        private void RestyleKid(Control c)
        {
            // 🚨 标签文字必须在标签内部**垂直居中**：Designer 里有些 Label 是 `TopLeft`（文字贴顶），
            //    和同一行的框一比就"偏上 3~5px"（用户实测"左右字体和框中心坐标有偏差"）。
            //    这里只把 `Top*` 改成对应的 `Middle*` —— **水平位置保持不变**（On/OFF、电流 那些
            //    居中的表头不要被改成左对齐）。
            Label lb0 = c as Label;
            if (lb0 != null)
            {
                ContentAlignment a0 = lb0.TextAlign;
                ContentAlignment a1 = a0;
                switch (a0)
                {
                    case ContentAlignment.TopLeft: a1 = ContentAlignment.MiddleLeft; break;
                    case ContentAlignment.TopCenter: a1 = ContentAlignment.MiddleCenter; break;
                    case ContentAlignment.TopRight: a1 = ContentAlignment.MiddleRight; break;
                }
                if (a1 != a0) lb0.TextAlign = a1;
                return;
            }

            TextBox tb = c as TextBox;
            if (tb != null)
            {
                // 圆角样式：原生 TextBox 画不出圆角 ⇒ 去掉它自己的方框，
                // 边框由**表格**自绘（见 `TablePaintFrames`）⇒ 视觉上是 4px 圆角
                tb.BorderStyle = PP_ROUND_FIELDS ? BorderStyle.None : BorderStyle.FixedSingle;
                tb.BackColor = Color.White;
                return;
            }
            ComboBox cb = c as ComboBox;
            if (cb != null)
            {
                // ⚠️ `FlatStyle.Flat` 的 ComboBox **不画边框、不画背景**（只画右侧那个小箭头）。
                //    ⇒ 只有在"框由我们自己画"（PP_ROUND_FIELDS=true，见 `TablePaintFrames`）时才用它；
                //       否则必须用 `Standard`，不然用户看到的就是"下拉框没有外框、只剩一个小箭头"
                //       （2026-09-22 实测截图）。
                cb.FlatStyle = PP_ROUND_FIELDS ? FlatStyle.Flat : FlatStyle.Standard;
                // 🚨 Designer 里这些下拉框设了 `RightToLeft = Yes`（共 44 处）⇒ 文字靠**右**、箭头在左，
                //    用户看到的就是"字不在框框中间"（2026-09-22）。这里统一改回标准方向：
                //    文字靠左、下拉箭头在右，视觉上就是"字在框里居中"。
                cb.RightToLeft = RightToLeft.No;
                if (cb.BackColor == SystemColors.Window || cb.BackColor == SystemColors.Control)
                    cb.BackColor = Color.White;
                return;
            }
            Button bt = c as Button;
            if (bt != null)
            {
                bt.FlatStyle = FlatStyle.Flat;
                bt.FlatAppearance.BorderSize = 1;
                bt.FlatAppearance.BorderColor = PP_BTN_EDGE;
                bt.FlatAppearance.MouseOverBackColor = Color.FromArgb(234, 243, 253);
                bt.FlatAppearance.MouseDownBackColor = Color.FromArgb(219, 234, 250);
                bt.Cursor = Cursors.Hand;
            }
        }

        // ---------------------------------------------------------------- 自适应（只排几何，不动字号）
        //  🚨 2026-09-22 定稿：**字号固定、不随窗口缩放**。
        //     之前每趟重排都要遍历三个页面 350+ 个控件改 Font（每个 Font 赋值都会让 TextBox/ComboBox
        //     重算高度 → 级联重排父网格 → 卡片 → 页面 → 窗体），一趟 300~600ms，
        //     Resize 连发几十次就是"卡好几秒"。改成固定字号后，一趟里**只剩几何**，跟着降到几十毫秒。
        //     布局仍然随窗口放大：外层是按设计比例的 TableLayoutPanel（全 Percent），
        //     卡片 Anchor=Left|Right 跟着格子变宽 —— 跟「电池信息」页完全同一套做法。
        private void LayoutParamPages()
        {
            if (!USE_NEW_PARAM_PAGES) return;
            if (!_paramBuilt) BuildParamPages();          // 首次调用时自建页面（不依赖 Form1.cs 的调用）
            if (_paramCards.Count == 0) return;
            try
            {
                // 基准取 **TabControl 的显示区**：三个页面共用同一个尺寸，
                // 而且不受"某个页面有没有滚动条"影响
                Rectangle dr = tabControl1.DisplayRectangle;
                if (dr.Width >= 100 && dr.Height >= 100 && _basePageH <= 0)
                {
                    _basePageW = dr.Width;
                    _basePageH = dr.Height;
                }
            }
            catch { }
            ScheduleGeometry();
        }

        // 应用字号（顺带让卡片重画标题）
        private void ApplyFonts(float kf)
        {
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                TableLayoutPanel t = pc.Table;
                if (t != null && !t.IsDisposed) t.SuspendLayout();   // ⚠️ 必须挂起：
                // 字体一变，TextBox/ComboBox 会自己改高 → 触发所在网格重排 → 再级联到卡片/页面/窗体。
                // 100 多个控件 × 整条父链重排 = 一次 Resize 上千次重排（用户实测"卡好几秒"）。
                foreach (KeyValuePair<Control, Font> kv in pc.BaseFont)
                {
                    Control c = kv.Key;
                    if (c == null || c.IsDisposed) continue;
                    c.Font = ScaleFont(kv.Value, kf);
                }
                if (t != null && !t.IsDisposed) t.ResumeLayout(false);   // 真正的重排留给 FillCard 里的 PerformLayout
                if (pc.Card != null && !pc.Card.IsDisposed) pc.Card.Invalidate();
            }
        }

        // ---------------------------------------------------------------- 三页共用的缩放
        // 缩放系数只用"窗口大小"算（用 TabControl 的显示区，三页同一个尺寸），
        // **与当前在哪一页无关** ⇒ 不论在哪一页最大化，三页结果一致。
        // ---- 防抖入口：Resize 期间只做轻量更新，等尺寸稳定（140ms 没再变）才做整趟重排 ----
        // 🚨 之前是"每个 Resize 事件跑一趟完整重排"：一趟 50~150ms（改字号 + 铺两页 + 重画两页），
        //    拖窗口/最大化时 Resize 连发几十次 ⇒ 消息队列里堆了几十趟 ⇒ 界面卡好几秒（用户实测）。
        //    ⇒ 尺寸变化期间不做重活，稳定后只做一趟。
        private void ScheduleLayoutParamPages()
        {
            if (!USE_NEW_PARAM_PAGES) return;
            try
            {
                if (_layoutTimer == null)
                {
                    _layoutTimer = new System.Windows.Forms.Timer();
                    _layoutTimer.Interval = 140;
                    _layoutTimer.Tick += delegate
                    {
                        _layoutTimer.Stop();
                        if (_mainSizePending) { _mainSizePending = false; ApplyMainSize(); }
                        LayoutParamPages();
                    };
                }
                _layoutTimer.Stop();
                _layoutTimer.Start();
            }
            catch
            {
                if (_mainSizePending) { _mainSizePending = false; ApplyMainSize(); }
                LayoutParamPages();
            }
        }

        private float ComputeUiKf()
        {
            int vw = 0, vh = 0;
            try
            {
                Rectangle dr = tabControl1.DisplayRectangle;
                vw = dr.Width; vh = dr.Height;
            }
            catch { }
            if (vw < 100 || vh < 100)
            {
                Control p = (tabPage1 != null) ? (Control)tabPage1 : (Control)tabPage3;
                if (p == null) return 1f;
                vw = p.ClientSize.Width; vh = p.ClientSize.Height;
            }
            if (_basePageW <= 0 || _basePageH <= 0) return 1f;
            float kf = Math.Min((float)vw / _basePageW, (float)vh / _basePageH);
            kf = Clamp(kf, PP_KMIN, PP_KMAX);
            return (float)Math.Round(kf / 0.05f) * 0.05f;
        }

        // 算一次、一起应用（参数页 + 校准页 + 电池信息页）
        // ⛔ **已停用（2026-09-22）**：字号改为固定，不再随窗口缩放。
        //    原因：一趟里要改三个页面 350+ 个控件的 Font，每个赋值都会让 TextBox/ComboBox
        //    重算高度 → 级联重排父网格 → 卡片 → 页面 → 窗体（窗体里还有电池页 4 个 DataGridView）
        //    ⇒ 一趟 300~600ms，Resize 连发几十次 = "卡好几秒"。
        //    现在布局靠 Dock + Percent 自己跟随窗口（卡片 Anchor=Left|Right），
        //    与「电池信息」页完全同一套做法 ⇒ 一趟只剩几何，且几何量本身没变时整趟跳过。
        //    方法名保留：防止历史调用点复活时又悄悄跑起来。
        private void ApplyUiScale()
        {
        }

        // 顶部标签条的字号也跟同一个系数（原来写死 10.5F：页面缩了它不缩 = 两套比例）
        private void ApplyTabFonts(float kf)
        {
            if (_tabFontBase == null && tabControl1 != null) _tabFontBase = tabControl1.Font;
            if (_tabFontOnBase == null) _tabFontOnBase = _tabFontOn;
            if (tabControl1 != null && _tabFontBase != null) tabControl1.Font = ScaleFont(_tabFontBase, kf);
            if (_tabFontOnBase != null) _tabFontOn = ScaleFont(_tabFontOnBase, kf);
            if (tabControl1 != null) tabControl1.Invalidate();
        }

        // 电池信息页：原来是写死的字号（8.5~15F，完全不跟窗口缩放）⇒ 跟参数页不一致。
        // 这里把整页控件的字号按同一个系数缩放（基准字号第一次访问时记下来）。
        private void ApplyBatteryFonts(float kf)
        {
            Control root = 电池信息;
            if (root == null || root.IsDisposed) return;
            root.SuspendLayout();
            try { ScaleBatteryFonts(root, true); }
            finally { root.ResumeLayout(true); }
        }

        // 只在字号真的不一样时才写（避免整表失效重排）
        private static void StyleFont(DataGridViewCellStyle st, Font f)
        {
            if (st == null || f == null) return;
            if (st.Font == null || Math.Abs(st.Font.Size - f.Size) > 0.01f) st.Font = f;
        }

        private void ScaleBatteryFonts(Control c, bool isRoot)
        {
            if (c == null || c.IsDisposed) return;
            if (object.ReferenceEquals(c, _bpOldHolder)) return;      // 隐藏的旧版控件不用管

            Font baseF;
            if (!_battFonts.TryGetValue(c, out baseF))
            {
                baseF = c.Font;
                _battFonts[c] = baseF;
            }
            if (!isRoot && baseF != null)
            {
                Font nf = ScaleFont(baseF, _uiKf);
                if (nf != null && (c.Font == null || Math.Abs(c.Font.Size - nf.Size) > 0.01f))
                    c.Font = nf;
            }

            DataGridView g = c as DataGridView;      // 表格的文字走 Style，得单独设
            if (g != null && c.Font != null)
            {
                // ⚠️ 必须"真的变了才写"：给 DataGridView 挂 Style 会整表失效重排，无条件写会反复重排
                StyleFont(g.ColumnHeadersDefaultCellStyle, c.Font);
                StyleFont(g.DefaultCellStyle, c.Font);
                for (int i = 0; i < g.Columns.Count; i++)
                    if (g.Columns[i].DefaultCellStyle.Font != null) StyleFont(g.Columns[i].DefaultCellStyle, c.Font);
            }

            foreach (Control ch in c.Controls) ScaleBatteryFonts(ch, false);
        }

        // 本卡片里"最高的非标签控件"的高度（字体驱动 ⇒ 天然跟随系统 DPI / 字体设置 / 字号缩放）
        private float CardCtrlH(ParamCard pc)
        {
            float h = 0f;
            for (int i = 0; i < pc.Kids.Count; i++)
            {
                Control c = pc.Kids[i];
                if (c == null || c.IsDisposed || c is Label) continue;
                if (c.Height > h) h = c.Height;
            }
            return h < 16f ? 23f : h;
        }

        // 本页统一"控件高"：取本页所有卡片里最高的 ⇒ 全页行距完全一致
        private float PageCtrlH(TabPage page)
        {
            float h = 0f;
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                if (pc.Page == null || page == null || !object.ReferenceEquals(pc.Page, page)) continue;
                float c = CardCtrlH(pc);
                if (c > h) h = c;
            }
            return h < 16f ? 23f : h;
        }

        // 本页"输入框高"：TextBox / ComboBox 的高度（字体驱动，≈22~28）
        //  🚨 行距基准要用**它**，而不是"本页最高控件"（2026-09-22 用户："为什么看起来间距这么大"）：
        //     按钮的设计高度很大（左列 4 个大按钮设计高 58），用它当基准会把行距撑到 40+，
        //     而输入框只有 22 ⇒ 上下留白 18px ≈ 输入框本身的高度，看着就是"间距太大、边框很粗"的错觉。
        //     （实测用户截图：输入框 22px、行距 40px、间隙 18px；边框其实是 1px，不粗。）
        private float PageInputH(TabPage page)
        {
            // 结果缓存：控件高度由字体决定，而字号是固定的 ⇒ 这个值算一次就够
            //  （原来是每趟都遍历本页 150 个控件，纯浪费）
            float cached;
            if (page != null && _inputHCache.TryGetValue(page, out cached)) return cached;

            float h = 0f;
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                if (pc.Page == null || page == null || !object.ReferenceEquals(pc.Page, page)) continue;
                // 🚨 大间距卡片（左列那几个高按钮）用的是**它自己的设计行距**，不参与"全页统一行距"的
                //    计算 —— 否则它那个 58px 高的按钮会把整页行距顶到 66（用户会看到"间距又变大了"）。
                if (pc.BigPitch) continue;
                for (int k = 0; k < pc.Kids.Count; k++)
                {
                    Control c = pc.Kids[k];
                    if (c == null || c.IsDisposed) continue;
                    // 🚨 这里算的是"控件将会有多高"，**不要用当前的 `c.Height`** ——
                    //    `FillCard()` 会把**所有**控件（含按钮）的高度统一收口到 `Font.Height + 6`，
                    //    用同一个公式算才能与它一致（缓存也不会过期）。
                    if (c is TextBox || c is ComboBox || c is Label || c is Button)
                    {
                        float n = c.Font.Height + 10f;      // 与 FillCard 的收口公式保持一致（+10 才不裁字）
                        if (n > h) h = n;
                    }
                }
            }
            float r = (h < 16f) ? 24f : h;
            if (page != null) _inputHCache[page] = r;
            return r;
        }

        // 打开双缓冲（Control.DoubleBuffered 是 protected，只能反射设）
        //  🚨 不打开的话，"改尺寸 → 重画"期间会把中间状态画出来：控件被白底盖住、文字被吃掉、
        //     输入框只剩两个角 —— 就是用户看到的"花屏 / 字体被框吃掉"（2026-09-22 实测截图）。
        private static void EnableDoubleBuffer(Control c)
        {
            if (c == null) return;
            try
            {
                System.Reflection.PropertyInfo pi = typeof(Control).GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(c, true, null);
            }
            catch { }
        }

        private ParamCard FindCardOf(Control c)
        {
            ParamCard pc;
            return (c != null && _cardByCtl.TryGetValue(c, out pc)) ? pc : null;
        }

        // 卡片里的"固定高"：标题条 + 上下留白
        private static int CardFixedH(ParamCard pc)
        {
            return (pc.HasTitle ? PP_TITLE_H + 6 : 6) + 6;
        }

        // 卡片按给定行距"需要的高度"
        private static int CardNeedH(ParamCard pc, float pitch)
        {
            int rows = pc.Rows < 1 ? 1 : pc.Rows;
            // 大间距卡片（左列 4 个大按钮）按**设计行距**撑开，否则会被压成一堆
            float p = (pc.BigPitch && pc.Pitch > pitch) ? pc.Pitch : pitch;
            float extra = string.IsNullOrEmpty(pc.HeaderText) ? 0f : p;    // 补出来的"说明行"也要占一行
            return CardFixedH(pc) + (int)Math.Ceiling(rows * p + extra);
        }

        // 某控件"按内容需要的高度"：卡片 → 标题 + 行数×行距；嵌套网格 → 各行之和；其它 → 自身高
        private float ControlNeedH(Control c, float pitch)
        {
            if (c == null) return 0f;
            TableLayoutPanel t = c as TableLayoutPanel;
            if (t != null) return GridNeedH(t, pitch, null) + t.Padding.Vertical;
            ParamCard pc = FindCardOf(c);
            if (pc != null) return CardNeedH(pc, pitch);
            return c.Height;
        }

        // 网格"按内容需要的高度"；顺便把每一行需要的高度写进 need[]
        private float GridNeedH(TableLayoutPanel t, float pitch, float[] need)
        {
            if (t == null) return 0f;
            int n = t.RowCount;
            if (n <= 0) return 0f;
            float[] rn = (need != null && need.Length >= n) ? need : new float[n];
            for (int i = 0; i < n; i++) rn[i] = 0f;

            List<Control> span = null;
            foreach (Control c in t.Controls)
            {
                if (c == null) continue;
                int r = t.GetRow(c);
                if (r < 0 || r >= n) continue;
                float h = ControlNeedH(c, pitch) + c.Margin.Vertical;
                if (t.GetRowSpan(c) <= 1)
                {
                    if (h > rn[r]) rn[r] = h;
                }
                else
                {
                    if (span == null) span = new List<Control>();
                    span.Add(c);
                }
            }
            for (int i = 0; i < n; i++) if (rn[i] < 6f) rn[i] = 6f;      // 空行 = 卡片之间的间隔

            // 跨行控件（左列 4 个大按钮跨 2 行）：需求超过所跨行之和时，差额补给它跨的最后一行
            if (span != null)
            {
                for (int i = 0; i < span.Count; i++)
                {
                    Control c = span[i];
                    int r = t.GetRow(c), sp = t.GetRowSpan(c);
                    int last = Math.Min(n - 1, r + sp - 1);
                    float want = ControlNeedH(c, pitch) + c.Margin.Vertical;
                    float has = 0f;
                    for (int k = r; k <= last; k++) has += rn[k];
                    if (want > has) rn[last] += (want - has);
                }
            }
            float total = 0f;
            for (int i = 0; i < n; i++) total += rn[i];
            return total;
        }

        // 卡片归属页（建页时嵌套网格还没挂进页面，pc.Page 会漏 ⇒ 这里按控件树统一补一次）
        private void EnsurePages()
        {
            if (_pagesFixed) return;
            _pagesFixed = true;
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                Control p = pc.Card == null ? null : pc.Card.Parent;
                while (p != null && !(p is TabPage)) p = p.Parent;
                pc.Page = p as TabPage;
            }
        }

        // 铺：把"每一行自己的内容需要高"写进行高里（**不撑满**）
        //   · 卡片高度由内容决定（FillCard 里设 Size）⇒ 行数少的卡片不会被拉长 ⇒ 全页行距一致
        //   · 网格里多余的高度就留成空白（在卡片外面，不是卡片里面）
        private void FillContainer(Control c, int availH, int availW)
        {
            TableLayoutPanel t = c as TableLayoutPanel;
            if (t != null)
            {
                int n = t.RowCount;
                if (n <= 0 || availH < 12) return;

                float[] need = new float[n];
                GridNeedH(t, _pitchNow, need);
                int[] rh = new int[n];
                for (int i = 0; i < n; i++)
                {
                    rh[i] = Math.Max(6, (int)Math.Round(need[i]));
                    RowStyle st = t.RowStyles[i];
                    if (st.SizeType != SizeType.Absolute || Math.Abs(st.Height - rh[i]) > 0.4f)
                    {
                        st.SizeType = SizeType.Absolute;
                        st.Height = rh[i];
                    }
                }
                // ⚡ **不在这里调 `t.PerformLayout()` / `GetColumnWidths()`**：
                //    每个网格都布局一次 ⇒ 一趟 ~20 次原生布局，实测占 220ms 的大头
                //    （perf.log："重排参数页 233 ms"）。宽度不再需要精确值 ——
                //    `FillCard` 只拿它做缓存判断，真正宽度由 `ApplyCardGeometry()` 收尾的
                //    `FillAllWidths()` 按**实际列宽**设 ⇒ 这里传 0。
                int[] cw = null;

                foreach (Control ch in t.Controls)
                {
                    if (ch == null) continue;
                    int r = t.GetRow(ch);
                    if (r < 0 || r >= n) continue;
                    int last = Math.Min(n - 1, r + t.GetRowSpan(ch) - 1);
                    int h = 0;
                    for (int q = r; q <= last; q++) h += rh[q];
                    h -= ch.Margin.Vertical;
                    int w = 0;
                    if (cw != null)
                    {
                        int c0 = t.GetColumn(ch), cs = t.GetColumnSpan(ch);
                        for (int q = c0; q < c0 + cs && q < cw.Length; q++) w += cw[q];
                        w -= ch.Margin.Horizontal;
                    }
                    if (h < 12) h = 12;
                    FillContainer(ch, h, w);
                }
                return;
            }
            ParamCard pc = FindCardOf(c);
            if (pc != null) FillCard(pc, availH, availW);
        }

        // 卡片：高 = 标题 + 行数 × 行距（内容说了算），行高全部相等、总和 == 表格高
        private void FillCard(ParamCard pc, int availH, int availW)
        {
            Control card = pc.Card;
            TableLayoutPanel t = pc.Table;
            if (card == null || card.IsDisposed || t == null || pc.Rows < 1) return;

            float kfNow = _fontKApplied <= 0f ? 1f : _fontKApplied;
            bool hChanged = (pc.LastFillH != availH) || (Math.Abs(pc.LastFillKf - kfNow) > 0.001f);
            bool wChanged = (pc.LastFillW != availW);
            if (!hChanged && !wChanged) return;           // 高/宽/字号都没变 → 保持原样，别白排一遍
            if (!hChanged)
            {
                // 只改了宽度（拖窗口左右边、或最大化）：行高和控件高度都不用动，
                // 只把输入框/按钮的宽度按新列宽重设一遍 ⇒ 一趟只有几次属性写，几毫秒
                pc.LastFillW = availW;
                FillCardWidths(pc);
                return;
            }
            pc.LastFillH = availH; pc.LastFillW = availW; pc.LastFillKf = kfNow;

            int rows = pc.Rows;
            // 🚨 行高一律用**全页统一行距**（而不是"卡片可用高 ÷ 行数"）：
            //    后者会让每张卡片的行高都不一样（被大按钮撑大的行更宽）⇒ **三列的行无法横向对齐**
            //    （用户实测："三列里面行对齐下"）。卡片高度由"标题 + 行数 × 行距"决定，
            //    多出来的高度留在卡片**外**面，所以统一行距不会造成重叠。
            int p = (int)Math.Round(_pitchNow);
            if (p < 8) p = 8;
            // 大间距卡片（左列 4 个大按钮，设计行距 ≈165）按**设计行距**铺开
            //（与 CardNeedH 保持一致，否则卡片高和行高对不上）
            if (pc.BigPitch)
            {
                int bp = (int)Math.Round(pc.Pitch);
                if (bp > p) p = bp;
            }

            // 🚨 关键性能点（2026-09-22 定位）：下面每一次 `st.Height = …` / `c.Height = …` /
            //    `t.Bounds = …` 都会让 TableLayoutPanel **立刻重排一次**（遍历它全部子控件）。
            //    一张 11 行的卡片 = 十几次级联重排，15 张卡片 ≈ 200 次 ⇒ 一趟几百毫秒，
            //    Resize 连发就变成"卡好几秒"。⇒ 整段挂起，最后只排一次。
            t.SuspendLayout();
            try
            {
                int[] rh = new int[rows];
                int total = 0;
                for (int r = 0; r < rows; r++)
                {
                    rh[r] = p;
                    total += rh[r];
                    RowStyle st = pc.RowStyles[r];
                    if (st.SizeType != SizeType.Absolute || Math.Abs(st.Height - rh[r]) > 0.4f)
                    {
                        st.SizeType = SizeType.Absolute;
                        st.Height = rh[r];
                    }
                }
                pc.LastRh = rh;                  // 记下来：AlignAllRows() 要按它把每个控件垂直居中

                // 卡片自身也只占内容需要的高度（+2px 余量吸收 GroupBox 边框差）
                //  ⚠️ **只设高度，宽度交给 Anchor(Left|Right)** —— 否则拖窗口时宽度一变就要重铺所有卡片；
                //     宽度走原生布局是零成本的。
                //  ⚠️ 若这张卡片要补"说明行"（`HeaderText`），再让出一行的高度、并把表格整体下移一行
                //     —— 说明文字由 `ParamCardPaint` 画在**表格上方**那块空行里（不会被表格白底盖住）。
                int hdrH = string.IsNullOrEmpty(pc.HeaderText) ? 0 : p;
                int wantH = CardFixedH(pc) + total + hdrH + 2;
                if (Math.Abs(card.Height - wantH) > 1) card.Height = wantH;

                int top = pc.HasTitle ? PP_TITLE_H + 6 : 6;
                if (hdrH > 0) top += hdrH;
                if (t.Left != PP_PAD) t.Left = PP_PAD;
                if (t.Top != top) t.Top = top;
                if (t.Height != total) t.Height = total;      // 宽度同样交给 Anchor

                // 固定高度控件（按钮 / 标签）超出所在行会被切 → 按行高收口。
                // ⚠️ 这里**不再**复量文字宽度：字号已固定（不分档缩放），宽度在建页时按同一字号量过一次；
                //    每次布局都调 TextRenderer.MeasureText（150 个标签）是纯浪费（每趟 100~300ms）。
                float kf = _fontKApplied <= 0f ? 1f : _fontKApplied;
                for (int i = 0; i < pc.Kids.Count; i++)
                {
                    Control c = pc.Kids[i];
                    if (c == null || c.IsDisposed) continue;
                    int r;
                    if (!pc.RowOf.TryGetValue(c, out r)) continue;
                    if (r < 0 || r >= rows) continue;

                    float dh;
                    pc.DesignH.TryGetValue(c, out dh);
                    if (dh <= 0) dh = c.Height;

                    // 🚨 2026-09-23 定稿：**控件高度一律"贴合文字"**（不再"撑满行高"）。
                    //    原因：Windows 的单行 EDIT（TextBox）会把多出来的高度**加在下方**（文字靠上），
                    //    Label 的 `TextAlign` 若是 TopLeft 也一样 —— 控件比文字高得越多，字看着就越"偏上"，
                    //    与同行的框不同心。用户实测（放大截图逐像素量）：框中心 y=24，而数字墨迹中心 20、
                    //    标签文字中心 17 ⇒ "明显不对"。⇒ 高度收到 `Font.Height + 6`，让文字几乎占满控件，
                    //    再由容器（`Anchor = None` ⇒ 单元格内居中）把控件摆正 ⇒ 字、控件、框三者同心。
                    int cap = rh[r] - 2;
                    int want;
                    // 🚨 2026-09-23 第二轮：**所有控件都用同一个高度公式**（`Font.Height + 6`）——包括按钮。
                    //    之前按钮用"设计高"（28）、输入框按字体（16）⇒ 高度不同，在同一行里中心对不上
                    //    （用户实测：按钮中心 85.5、右边的输入框中心 82，**差 3.5px** → "上下有点偏了"）。
                    //    高度一致以后，无论容器怎么居中，中心必然相同。
                    //    例外：左列那 4 个"大按钮"（`BigPitch` 卡片）本来就是设计稿的高按钮，
                    //          必须按设计高，压成一行小高度就毁了（用户反馈过"左边为什么被改掉了"）。
                    //    ⚠️ `Font.Height` 是 int，别套 `Math.Ceiling`（会 CS0121 二义）。
                    if (pc.BigPitch && c is Button)
                    {
                        want = (int)Math.Round(dh * kf);
                    }
                    else
                    {
                        // ⚠️ 余量从 +6 提到 **+10**：+6 时按钮/标签的文字**下半截会被裁**（用户实测
                        //    "框框内文字被吃了一部分"）。`Font.Height` 是字体行高，控件再留 10px
                        //    上下各 5px 的呼吸空间，从构造上不会再裁字。
                        want = c.Font.Height + 10;
                    }
                    if (want > cap) want = cap;
                    if (want < 8) want = 8;
                    if (c.Height != want) c.Height = want;
                }
            }
            finally
            {
                t.ResumeLayout(false);
                t.PerformLayout();                          // 整张卡片只排这一次
            }
            FillCardWidths(pc);                             // 输入框/按钮宽度按新列宽显式设一遍
        }

        // 按"当前列宽"把输入框 / 下拉框 / 按钮的宽度设准。
        //  🚨 为什么不只靠 `Anchor=Left|Right`：实测这些控件在这个嵌套 `TableLayoutPanel` 里
        //     **没有被可靠地拉伸** —— 用户看到的是"下拉框只剩右边一个小箭头、右边一大片空白，
        //     单位离得有点远"。自己按"列权重 × 表格宽"算出来设，结果确定、与 Anchor 无关。
        private void FillCardWidths(ParamCard pc)
        {
            Control card = pc.Card;
            TableLayoutPanel t = pc.Table;
            if (card == null || card.IsDisposed || t == null || pc.ColCount < 1) return;

            // 🚨 必须用**表格的实际列宽** `GetColumnWidths()`，不要用"ColW 权重 × 卡片宽"去估：
            //    估出来的列宽常常只有实际的一半 ⇒ 输入框只占格子的一半、右边空一大片，
            //    用户看到的就是"单位和框框之间距离还是太大了"
            //    （2026-09-22 实测：框只有 70px，而它所在列的实际宽是 144px）。
            //    ⚠️ `GetColumnWidths()` 要等布局完成才准 ⇒ 必须在 `root.PerformLayout()` **之后**调
            //    （`ApplyCardGeometry()` 的收尾就是按这个顺序排的）。
            int[] cw = null;
            try { cw = t.GetColumnWidths(); } catch { cw = null; }
            if (cw == null || cw.Length < pc.ColCount) return;

            for (int i = 0; i < pc.Kids.Count; i++)
            {
                Control c = pc.Kids[i];
                if (c == null || c.IsDisposed) continue;
                if (!(c is TextBox) && !(c is ComboBox) && !(c is Button)) continue;
                int k;
                if (!pc.ColOf.TryGetValue(c, out k) || k < 0 || k >= pc.ColCount) continue;
                int w = cw[k] - c.Margin.Horizontal;
                if (w < 20) w = 20;
                if (Math.Abs(c.Width - w) > 1) c.Width = w;
            }
        }

        // 整页只更新宽度（拖窗口左右边/最大化时走这条：行高、控件高度一律不动）
        private void FillAllWidths(TabPage page)
        {
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                if (pc.Page == null || page == null || !object.ReferenceEquals(pc.Page, page)) continue;
                FillCardWidths(pc);
            }
        }

        // 把每个控件**垂直居中到它所在行的中心**（显式摆，不依赖 TableLayoutPanel 的居中算法）。
        //  🚨 为什么必须自己做：同一行里 Label 和 TextBox/ComboBox 的高度、上下边距都不同，
        //     交给 `TableLayoutPanel` 自己居中时会出现偏差 —— 用户实测图："生产日期 / 年 / 月 / 日"
        //     和"功能设置"里的标签都比旁边的输入框偏上 10 多像素（"字和框没有对齐，偏了"）。
        //     自己按"行中心 − 控件高/2"摆，结果一定对齐。
        //  ⚠️ 必须在 `ResumeLayout/PerformLayout` **之后**调用（否则又被容器的布局覆盖）。
        private void AlignAllRows()
        {
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                Control card = pc.Card;
                if (card == null || card.IsDisposed) continue;
                int[] rh = pc.LastRh;
                if (rh == null || rh.Length == 0 || rh.Length != pc.Rows) continue;

                int[] rt = new int[rh.Length];
                int y = 0;
                for (int r = 0; r < rh.Length; r++) { rt[r] = y; y += rh[r]; }

                for (int k = 0; k < pc.Kids.Count; k++)
                {
                    Control c = pc.Kids[k];
                    if (c == null || c.IsDisposed) continue;
                    int r;
                    if (!pc.RowOf.TryGetValue(c, out r) || r < 0 || r >= rh.Length) continue;
                    int top = rt[r] + (rh[r] - c.Height) / 2;
                    if (top < rt[r]) top = rt[r];
                    if (c.Top != top) c.Top = top;
                }
            }
        }

        // 几何必须在"父级布局完成之后"再算，否则读到的是上一帧的尺寸。
        // ⚠️ 不能直接算：Form.Resize 事件比子控件的布局更早触发。
        private void ScheduleGeometry()
        {
            if (_geoScheduled) return;
            if (!IsHandleCreated) { ApplyCardGeometry(); return; }
            _geoScheduled = true;
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    _geoScheduled = false;
                    ApplyCardGeometry();
                }));
            }
            catch
            {
                _geoScheduled = false;
                ApplyCardGeometry();
            }
        }

        private void ApplyCardGeometry()
        {
            if (!USE_NEW_PARAM_PAGES) return;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            this.SuspendLayout();        // 整趟挂起窗体布局：中途不给任何控件触发"往上递归重排"的机会
            try
            {
                EnsurePages();
                // 只铺**当前可见**的那一页 → 开销直接减半；隐藏页等切过去时再铺（见 SelectedIndexChanged）
                bool third = (tabControl1 != null && tabPage3 != null && tabControl1.SelectedTab == tabPage3);
                if (third) LayoutOnePage(tabPage3, _ppPage3Root);
                else LayoutOnePage(tabPage1, _ppPage1Root);
            }
            catch { }
            finally
            {
                // ⚠️ 用 `ResumeLayout(false)` 而不是 `true`：`true` 会**立刻**把挂起的布局跑完，
                //    而参数页和电池信息页在同一个窗体上（那边挂着 4 个 DataGridView）—— 会一起重排。
                //    `false` 只解除挂起，真正的布局交给下面这一次显式调用。
                this.ResumeLayout(false);

                // 1) 让外层网格按新宽度排一次（Percent 列宽 + 卡片 Anchor）——
                //    整趟**只有这一次真正布局**，之后控件的尺寸/位置都由我们显式设。
                try { if (_ppPage1Root != null && !_ppPage1Root.IsDisposed) _ppPage1Root.PerformLayout(); }
                catch { }
                try { if (_ppPage3Root != null && !_ppPage3Root.IsDisposed) _ppPage3Root.PerformLayout(); }
                catch { }

                // 2) 按**实际列宽**把输入框/下拉框/按钮的宽度设准
                //    （不设的话它们只占格子的一半，右边空一片 ⇒ 用户："单位和框框之间距离还是太大了"）
                TabPage pg = (tabControl1 != null) ? tabControl1.SelectedTab : null;
                if (pg != null) FillAllWidths(pg);

                // 3) 最后把每个控件垂直居中到行中心（覆盖容器自己的居中 ⇒ "字和框对齐"）
                AlignAllRows();

                sw.Stop();
                PerfLog("重排参数页", sw.ElapsedMilliseconds);
            }
        }

        // 参数写入侧用：判断一个"开启/关闭"下拉框当前是不是"开启"。
        //  🚨 原来写入代码只认 `cb.Text == "开启"` 这个**精确匹配**：一旦 Text 意外变成 ""、
        //     带前后空格、或用户手输了个别的字，就会**静默写成"关闭"**（用户报"写下去的值有问题"）。
        //     ⇒ 加 `Trim()` + `SelectedIndex` 兜底（Items[0] 就是"开启"，见 Form1 构造函数）。
        internal static bool ParamOn(ComboBox cb)
        {
            if (cb == null) return false;
            string t = cb.Text;
            if (t != null)
            {
                t = t.Trim();
                if (t == "开启") return true;
                if (t == "关闭") return false;
            }
            return cb.SelectedIndex == 0;
        }

        // 性能探针：只在"明显能感觉到"（>120ms）时往 exe 目录的 `perf.log` 追加一行。
        //  万一界面上还有卡顿：复现一次，把 `bin\Release\perf.log` 发我，就能直接看出是哪一步慢，
        //  不用再靠猜。平时（正常几十毫秒）**不会产生这个文件**。
        internal static void PerfLog(string tag, long ms)
        {
            if (ms < 120) return;
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "perf.log");
                System.IO.File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + tag + "  " + ms + " ms\r\n");
            }
            catch { }
        }

        // 一页：字号随窗口缩放 → 算"内容自然高度" → 铺
        //   · 卡片高度 = 标题 + 行数 × 行距（**内容说了算**，不按设计比例拉伸 ⇒ 卡片内不留白）
        //   · 字号跟着窗口一起放大（上限 PP_KMAX）⇒ 窗口变大是"整体放大"，不是"留一片空白"
        //   · 内容比窗口高 ⇒ 字号保持 1.0，根网格比视口高 ⇒ 出滚动条（不压缩、不裁字）
        private void LayoutOnePage(TabPage page, TableLayoutPanel root)
        {
            if (page == null || root == null || root.IsDisposed) return;

            // ⚠️ 视口高度要**预留竖向滚动条**：滚动条"一出现一消失"会让可用高度来回变 17px
            //    ⇒ 布局结果跟着抖 ⇒ 屏幕上一片残影（用户报的"最大化后花屏"）。
            //    这里统一按"假设有滚动条"的尺寸算，结果就与滚动条状态无关，不会自激振荡。
            int vw = page.ClientSize.Width, vh = page.ClientSize.Height;
            if (!page.VerticalScroll.Visible) vh -= SystemInformation.HorizontalScrollBarHeight;
            if (vw < 50 || vh < 50) return;

            // 视口尺寸没变 → 上一次的结果仍然有效，直接跳过（拖窗口时 Resize 会连发几十次）
            Size last;
            if (_lastPageSize.TryGetValue(page, out last) && last.Width == vw && last.Height == vh) return;
            _lastPageSize[page] = new Size(vw, vh);

            bool hChanged = false, wChanged = false;
            page.SuspendLayout();          // 这一趟会改很多尺寸：先挂起布局/重绘，画完再一次性呈现
            root.SuspendLayout();
            try
            {
                int w = Math.Max(vw, PP_MIN_W);
                wChanged = (root.Width != w);
                if (wChanged) root.Width = w;

                // 字号**固定**（不分档缩放，见 ApplyUiScale 上方说明）：这一趟里没有任何 Font 赋值，
                // 只算几何 ⇒ 一趟几十毫秒而不是几百毫秒，拖窗口/最大化不再卡。

                // ---- 2) 行距与"内容自然高"
                //   ⚠️ `GridNeedH` 是**递归**算法（每个控件都要 `GetRow`/`GetRowSpan` 这类线性查找），
                //      没必要每趟都跑：行距没变时结果就是上一次那个 ⇒ 缓存。
                float basePitch = PageInputH(page) + PP_ROW_GAP;
                if (_baseRootH <= 0 || Math.Abs(basePitch - _basePitch) > 0.01f)
                {
                    _basePitch = basePitch;
                    _baseRootH = Math.Max(40, (int)Math.Round(GridNeedH(root, basePitch, null) + root.Padding.Vertical));
                }
                int baseH = _baseRootH;

                // ---- 3) 内容比视口矮 → 用行距吸收余量（全页统一 +，最多每行 +PP_FILL_GAP）
                //    ⚠️ add 必须**量化到 1px**：否则拖窗口时每趟行距都差零点几像素 ⇒ 行距变
                //       ⇒ 所有卡片的几何缓存（LastFillH）全部失效 ⇒ 每趟都重排整页。
                int left = vh - 4 - baseH;
                int rows = PageRows(page);
                float add = 0f;
                if (left > 8 && rows > 0)
                    add = (float)Math.Floor(Math.Min(PP_FILL_GAP, (float)left / rows));

                float newPitch = basePitch;
                int rootH = baseH;
                if (add >= 1f)
                {
                    newPitch = basePitch + add;
                    if (_rootHForPitch <= 0 || Math.Abs(newPitch - _lastPitch) > 0.01f)
                    {
                        _lastPitch = newPitch;
                        _rootHForPitch = Math.Max(40, (int)Math.Round(GridNeedH(root, newPitch, null) + root.Padding.Vertical));
                    }
                    rootH = _rootHForPitch;
                }
                if (rootH < 40) rootH = 40;

                // 高度（行距/根高）没变，就只可能有宽度变化 ⇒ 只更新输入框宽度，整趟几乎零成本
                hChanged = (Math.Abs(_pitchNow - newPitch) > 0.01f) || (root.Height != rootH);
                _pitchNow = newPitch;
                if (root.Height != rootH) root.Height = rootH;

                if (hChanged) FillContainer(root, rootH, -1);
                else if (wChanged) FillAllWidths(page);
            }
            finally
            {
                root.ResumeLayout(true);
                page.ResumeLayout(true);
                // 几何没变就别整页重画（重画 150 个控件 + 15 张自绘卡片 ≈ 几十毫秒）
                if (hChanged || wChanged) page.Invalidate(true);
            }
        }

        // 本页所有卡片的行数之和（估"行距吸收余量"用）
        private int PageRows(TabPage page)
        {
            int n = 0;
            for (int i = 0; i < _paramCards.Count; i++)
            {
                ParamCard pc = _paramCards[i];
                if (pc.Page == null || page == null || !object.ReferenceEquals(pc.Page, page)) continue;
                n += (pc.Rows < 1 ? 1 : pc.Rows);
            }
            return n;
        }

        private Font ScaleFont(Font src, float k)
        {
            if (src == null) return null;
            float size = src.Size * k;
            if (size < 6f) size = 6f;
            if (size > 26f) size = 26f;
            return GetFont(src.FontFamily.Name, size, src.Style);
        }

        // 字号缓存：拖动窗口时不会反复 new Font（否则 GDI 句柄会爆）
        private Font GetFont(string family, float size, FontStyle style)
        {
            string key = family + "|" + size.ToString("0.0") + "|" + (int)style;
            Font f;
            if (_scaledFonts.TryGetValue(key, out f)) return f;
            try { f = new Font(family, size, style); }
            catch { f = new Font("Microsoft YaHei", size, style); }
            _scaledFonts[key] = f;
            return f;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
