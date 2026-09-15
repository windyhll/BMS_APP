using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BMS上位机
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            this.Load += new EventHandler(Form2_Load);
        }

        public string str;
        public string Str
        {
            get { return this.str; }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            str = this.comboBox1.Text;
            if (str != "")
            {
                this.DialogResult = DialogResult.OK;
            }
        }
        private void Form2_Load(object sender, EventArgs e)
        {
            this.ControlBox = false;
            // comboBox1.Items.Add("XRT-19S");
            comboBox1.Items.Add("4S");
            // comboBox1.Items.Add("JALEI-24S");
            // comboBox1.Items.Add("JALEI-21S");
            // comboBox1.Items.Add("JALEI-17-20S");
            // comboBox1.Items.Add("XRT-19S");
            // comboBox1.Items.Add("XRT-19S-TEST");
            // comboBox1.Items.Add("XRT-24S");
            // comboBox1.Items.Add("XRT-30S");
            // comboBox1.Items.Add("XRT-20S");
            // comboBox1.Items.Add("XRT-8S-CAN");
            // comboBox1.Items.Add("XRT-30S-TO-15S");
            // comboBox1.Items.Add("XRT-24S-TO-19S");
            // comboBox1.Text = "JALEI-21S";
            comboBox1.Text = "4S";
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
