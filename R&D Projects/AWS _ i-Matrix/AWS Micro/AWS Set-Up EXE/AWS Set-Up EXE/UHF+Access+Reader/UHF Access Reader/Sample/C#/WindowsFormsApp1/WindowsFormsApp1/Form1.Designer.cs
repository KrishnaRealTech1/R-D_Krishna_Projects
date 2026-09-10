namespace WindowsFormsApp1
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.groupNet = new System.Windows.Forms.GroupBox();
            this.btnDisConnect = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.txbPort = new System.Windows.Forms.TextBox();
            this.rflabel32 = new System.Windows.Forms.Label();
            this.txbIPAddr = new System.Windows.Forms.TextBox();
            this.rflabel33 = new System.Windows.Forms.Label();
            this.gpbCom = new System.Windows.Forms.GroupBox();
            this.cmbComBaud = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSportClose = new System.Windows.Forms.Button();
            this.btnSportOpen = new System.Windows.Forms.Button();
            this.cmbComPort = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnSetTxPower = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.cmbTxPower = new System.Windows.Forms.ComboBox();
            this.cmbWorkmode = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.btnSetWorkMode = new System.Windows.Forms.Button();
            this.lsvTagsActive = new System.Windows.Forms.ListView();
            this.colNum = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCode = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCodeLen = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCount = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRssi = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCw = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.sfd = new System.Windows.Forms.SaveFileDialog();
            this.btnInventory = new System.Windows.Forms.Button();
            this.btninvStop = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.stdSerialData = new System.Windows.Forms.TextBox();
            this.std = new System.Windows.Forms.OpenFileDialog();
            this.groupNet.SuspendLayout();
            this.gpbCom.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupNet
            // 
            this.groupNet.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupNet.Controls.Add(this.btnDisConnect);
            this.groupNet.Controls.Add(this.btnConnect);
            this.groupNet.Controls.Add(this.txbPort);
            this.groupNet.Controls.Add(this.rflabel32);
            this.groupNet.Controls.Add(this.txbIPAddr);
            this.groupNet.Controls.Add(this.rflabel33);
            this.groupNet.Location = new System.Drawing.Point(10, 125);
            this.groupNet.Name = "groupNet";
            this.groupNet.Size = new System.Drawing.Size(267, 98);
            this.groupNet.TabIndex = 16;
            this.groupNet.TabStop = false;
            this.groupNet.Text = "Net Connect";
            // 
            // btnDisConnect
            // 
            this.btnDisConnect.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnDisConnect.Location = new System.Drawing.Point(138, 69);
            this.btnDisConnect.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnDisConnect.Name = "btnDisConnect";
            this.btnDisConnect.Size = new System.Drawing.Size(104, 28);
            this.btnDisConnect.TabIndex = 5;
            this.btnDisConnect.Text = "DISCONNECT(&D)";
            this.btnDisConnect.UseVisualStyleBackColor = true;
            this.btnDisConnect.Click += new System.EventHandler(this.btnDisConnect_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnConnect.Location = new System.Drawing.Point(26, 69);
            this.btnConnect.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(104, 28);
            this.btnConnect.TabIndex = 4;
            this.btnConnect.Text = "CONNECT(&C)";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // txbPort
            // 
            this.txbPort.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txbPort.Location = new System.Drawing.Point(66, 42);
            this.txbPort.Name = "txbPort";
            this.txbPort.Size = new System.Drawing.Size(194, 21);
            this.txbPort.TabIndex = 3;
            this.txbPort.Text = "2022";
            // 
            // rflabel32
            // 
            this.rflabel32.AutoSize = true;
            this.rflabel32.Location = new System.Drawing.Point(7, 45);
            this.rflabel32.Name = "rflabel32";
            this.rflabel32.Size = new System.Drawing.Size(41, 12);
            this.rflabel32.TabIndex = 2;
            this.rflabel32.Text = "port：";
            // 
            // txbIPAddr
            // 
            this.txbIPAddr.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txbIPAddr.Location = new System.Drawing.Point(66, 14);
            this.txbIPAddr.Name = "txbIPAddr";
            this.txbIPAddr.Size = new System.Drawing.Size(194, 21);
            this.txbIPAddr.TabIndex = 1;
            this.txbIPAddr.Text = "192.168.1.120";
            // 
            // rflabel33
            // 
            this.rflabel33.AutoSize = true;
            this.rflabel33.Location = new System.Drawing.Point(7, 18);
            this.rflabel33.Name = "rflabel33";
            this.rflabel33.Size = new System.Drawing.Size(59, 12);
            this.rflabel33.TabIndex = 0;
            this.rflabel33.Text = "IP Addr：";
            // 
            // gpbCom
            // 
            this.gpbCom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gpbCom.Controls.Add(this.cmbComBaud);
            this.gpbCom.Controls.Add(this.label3);
            this.gpbCom.Controls.Add(this.btnSportClose);
            this.gpbCom.Controls.Add(this.btnSportOpen);
            this.gpbCom.Controls.Add(this.cmbComPort);
            this.gpbCom.Controls.Add(this.label2);
            this.gpbCom.Location = new System.Drawing.Point(10, 11);
            this.gpbCom.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.gpbCom.Name = "gpbCom";
            this.gpbCom.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.gpbCom.Size = new System.Drawing.Size(267, 113);
            this.gpbCom.TabIndex = 15;
            this.gpbCom.TabStop = false;
            this.gpbCom.Text = "Serial Connect";
            // 
            // cmbComBaud
            // 
            this.cmbComBaud.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbComBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbComBaud.FormattingEnabled = true;
            this.cmbComBaud.Items.AddRange(new object[] {
            "9600",
            "19200",
            "38400",
            "57600",
            "115200"});
            this.cmbComBaud.Location = new System.Drawing.Point(77, 49);
            this.cmbComBaud.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbComBaud.Name = "cmbComBaud";
            this.cmbComBaud.Size = new System.Drawing.Size(184, 20);
            this.cmbComBaud.TabIndex = 5;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 52);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 4;
            this.label3.Text = "buad：";
            // 
            // btnSportClose
            // 
            this.btnSportClose.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnSportClose.Location = new System.Drawing.Point(152, 77);
            this.btnSportClose.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnSportClose.Name = "btnSportClose";
            this.btnSportClose.Size = new System.Drawing.Size(74, 28);
            this.btnSportClose.TabIndex = 3;
            this.btnSportClose.Text = "CLOSE(&C)";
            this.btnSportClose.UseVisualStyleBackColor = true;
            this.btnSportClose.Click += new System.EventHandler(this.btnSportClose_Click);
            // 
            // btnSportOpen
            // 
            this.btnSportOpen.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnSportOpen.Location = new System.Drawing.Point(40, 77);
            this.btnSportOpen.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnSportOpen.Name = "btnSportOpen";
            this.btnSportOpen.Size = new System.Drawing.Size(74, 28);
            this.btnSportOpen.TabIndex = 2;
            this.btnSportOpen.Text = "OPEN(&O)";
            this.btnSportOpen.UseVisualStyleBackColor = true;
            this.btnSportOpen.Click += new System.EventHandler(this.btnSportOpen_Click);
            // 
            // cmbComPort
            // 
            this.cmbComPort.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbComPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbComPort.FormattingEnabled = true;
            this.cmbComPort.Location = new System.Drawing.Point(77, 17);
            this.cmbComPort.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbComPort.Name = "cmbComPort";
            this.cmbComPort.Size = new System.Drawing.Size(184, 20);
            this.cmbComPort.TabIndex = 1;
            this.cmbComPort.DropDown += new System.EventHandler(this.cmbComPort_DropDown);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(6, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "port：";
            // 
            // btnSetTxPower
            // 
            this.btnSetTxPower.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSetTxPower.Location = new System.Drawing.Point(513, 20);
            this.btnSetTxPower.Name = "btnSetTxPower";
            this.btnSetTxPower.Size = new System.Drawing.Size(75, 23);
            this.btnSetTxPower.TabIndex = 25;
            this.btnSetTxPower.Text = "Set";
            this.btnSetTxPower.UseVisualStyleBackColor = true;
            this.btnSetTxPower.Click += new System.EventHandler(this.btnSetTxPower_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(304, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(101, 12);
            this.label1.TabIndex = 22;
            this.label1.Text = "RfPower（dbm）：";
            // 
            // cmbTxPower
            // 
            this.cmbTxPower.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbTxPower.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTxPower.FormattingEnabled = true;
            this.cmbTxPower.Items.AddRange(new object[] {
            "10",
            "11",
            "12",
            "13",
            "14",
            "15",
            "16",
            "17",
            "18",
            "19",
            "20",
            "21",
            "22",
            "23",
            "24",
            "25",
            "26",
            "27",
            "28",
            "29",
            "30",
            "31",
            "32",
            "33"});
            this.cmbTxPower.Location = new System.Drawing.Point(416, 22);
            this.cmbTxPower.Name = "cmbTxPower";
            this.cmbTxPower.Size = new System.Drawing.Size(82, 20);
            this.cmbTxPower.TabIndex = 23;
            // 
            // cmbWorkmode
            // 
            this.cmbWorkmode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbWorkmode.DisplayMember = "2";
            this.cmbWorkmode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbWorkmode.FormattingEnabled = true;
            this.cmbWorkmode.Items.AddRange(new object[] {
            "AnswerMode",
            "ActiveMode"});
            this.cmbWorkmode.Location = new System.Drawing.Point(415, 60);
            this.cmbWorkmode.Name = "cmbWorkmode";
            this.cmbWorkmode.Size = new System.Drawing.Size(82, 20);
            this.cmbWorkmode.TabIndex = 27;
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(332, 63);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(71, 12);
            this.label10.TabIndex = 26;
            this.label10.Text = "WorkModde：";
            // 
            // btnSetWorkMode
            // 
            this.btnSetWorkMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSetWorkMode.Location = new System.Drawing.Point(513, 57);
            this.btnSetWorkMode.Name = "btnSetWorkMode";
            this.btnSetWorkMode.Size = new System.Drawing.Size(75, 23);
            this.btnSetWorkMode.TabIndex = 28;
            this.btnSetWorkMode.Text = "Set";
            this.btnSetWorkMode.UseVisualStyleBackColor = true;
            this.btnSetWorkMode.Click += new System.EventHandler(this.btnSetWorkMode_Click);
            // 
            // lsvTagsActive
            // 
            this.lsvTagsActive.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lsvTagsActive.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colNum,
            this.colCode,
            this.colCodeLen,
            this.colCount,
            this.colRssi,
            this.colCw});
            this.lsvTagsActive.FullRowSelect = true;
            this.lsvTagsActive.GridLines = true;
            this.lsvTagsActive.HideSelection = false;
            this.lsvTagsActive.Location = new System.Drawing.Point(10, 229);
            this.lsvTagsActive.Name = "lsvTagsActive";
            this.lsvTagsActive.Size = new System.Drawing.Size(584, 265);
            this.lsvTagsActive.TabIndex = 64;
            this.lsvTagsActive.UseCompatibleStateImageBehavior = false;
            this.lsvTagsActive.View = System.Windows.Forms.View.Details;
            // 
            // colNum
            // 
            this.colNum.Tag = "activecolNum";
            this.colNum.Text = "No.";
            this.colNum.Width = 25;
            // 
            // colCode
            // 
            this.colCode.Tag = "activecolCode";
            this.colCode.Text = "Data";
            this.colCode.Width = 230;
            // 
            // colCodeLen
            // 
            this.colCodeLen.Tag = "activecolCodeLen";
            this.colCodeLen.Text = "Len";
            this.colCodeLen.Width = 40;
            // 
            // colCount
            // 
            this.colCount.Tag = "activecolCount";
            this.colCount.Text = "Cnt(Ant1/2/3/4)";
            this.colCount.Width = 80;
            // 
            // colRssi
            // 
            this.colRssi.Tag = "colRssia";
            this.colRssi.Text = "RSSI(dBm)";
            // 
            // colCw
            // 
            this.colCw.Tag = "activecolCw";
            this.colCw.Text = "Channel";
            // 
            // sfd
            // 
            this.sfd.Filter = "*.txt|*.txt|*.*|*.*";
            // 
            // btnInventory
            // 
            this.btnInventory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInventory.Font = new System.Drawing.Font("黑体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnInventory.Location = new System.Drawing.Point(334, 125);
            this.btnInventory.Name = "btnInventory";
            this.btnInventory.Size = new System.Drawing.Size(90, 65);
            this.btnInventory.TabIndex = 65;
            this.btnInventory.Text = "Start";
            this.btnInventory.UseVisualStyleBackColor = true;
            this.btnInventory.Click += new System.EventHandler(this.btnInventoryActive_Click);
            // 
            // btninvStop
            // 
            this.btninvStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btninvStop.Font = new System.Drawing.Font("黑体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btninvStop.Location = new System.Drawing.Point(488, 125);
            this.btninvStop.Name = "btninvStop";
            this.btninvStop.Size = new System.Drawing.Size(90, 65);
            this.btninvStop.TabIndex = 66;
            this.btninvStop.Text = "Stop";
            this.btninvStop.UseVisualStyleBackColor = true;
            this.btninvStop.Click += new System.EventHandler(this.btninvStop_Click);
            // 
            // btnClear
            // 
            this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClear.Location = new System.Drawing.Point(505, 499);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(88, 23);
            this.btnClear.TabIndex = 73;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // stdSerialData
            // 
            this.stdSerialData.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.stdSerialData.Location = new System.Drawing.Point(598, 10);
            this.stdSerialData.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.stdSerialData.Multiline = true;
            this.stdSerialData.Name = "stdSerialData";
            this.stdSerialData.Size = new System.Drawing.Size(183, 521);
            this.stdSerialData.TabIndex = 74;
            // 
            // std
            // 
            this.std.Filter = "*.bin|*.BIN";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(782, 533);
            this.Controls.Add(this.stdSerialData);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.btninvStop);
            this.Controls.Add(this.btnInventory);
            this.Controls.Add(this.lsvTagsActive);
            this.Controls.Add(this.btnSetWorkMode);
            this.Controls.Add(this.cmbWorkmode);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.btnSetTxPower);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmbTxPower);
            this.Controls.Add(this.groupNet);
            this.Controls.Add(this.gpbCom);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupNet.ResumeLayout(false);
            this.groupNet.PerformLayout();
            this.gpbCom.ResumeLayout(false);
            this.gpbCom.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupNet;
        private System.Windows.Forms.Button btnDisConnect;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.TextBox txbPort;
        private System.Windows.Forms.Label rflabel32;
        private System.Windows.Forms.TextBox txbIPAddr;
        private System.Windows.Forms.Label rflabel33;
        private System.Windows.Forms.GroupBox gpbCom;
        private System.Windows.Forms.ComboBox cmbComBaud;
        private System.Windows.Forms.Label label3;
        public System.Windows.Forms.Button btnSportClose;
        public System.Windows.Forms.Button btnSportOpen;
        public System.Windows.Forms.ComboBox cmbComPort;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnSetTxPower;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbTxPower;
        private System.Windows.Forms.ComboBox cmbWorkmode;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Button btnSetWorkMode;
        private System.Windows.Forms.ListView lsvTagsActive;
        private System.Windows.Forms.ColumnHeader colNum;
        private System.Windows.Forms.ColumnHeader colCode;
        private System.Windows.Forms.ColumnHeader colCodeLen;
        private System.Windows.Forms.ColumnHeader colCount;
        private System.Windows.Forms.ColumnHeader colRssi;
        private System.Windows.Forms.ColumnHeader colCw;
        private System.Windows.Forms.SaveFileDialog sfd;
        private System.Windows.Forms.Button btnInventory;
        private System.Windows.Forms.Button btninvStop;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox stdSerialData;
        private System.Windows.Forms.OpenFileDialog std;
    }
}

