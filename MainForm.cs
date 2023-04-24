/******************************************************************************
*
* Example program:
*   ContAcqVoltageSamples_IntClk
*
* Category:
*   AI
*
* Description:
*   This example demonstrates how to acquire a continuous amount of data using
*   the DAQ device's internal clock.
*
* Instructions for running:
*   1.  Select the physical channel corresponding to where your signal is input
*       on the DAQ device.
*   2.  Enter the minimum and maximum voltage values.Note: For better accuracy,
*       try to match the input range to the expected voltage level of the
*       measured signal.
*   3.  Set the rate of the acquisition.Note: The rate should be at least twice
*       as fast as the maximum frequency component of the signal being acquired.
*       Also, in order to avoid Error -50410 (buffer overflow) it is important
*       to make sure the rate and the number of samples to read per iteration
*       are set such that they don't fill the buffer too quickly. If this error
*       occurs, try reducing the rate or increasing the number of samples to
*       read per iteration.
*
* Steps:
*   1.  Create a new analog input task.
*   2.  Create an analog input voltage channel.
*   3.  Set up the timing for the acquisition. In this example, we use the DAQ
*       device's internal clock to continuously acquire samples.
*   4.  Call AnalogMultiChannelReader.BeginReadWaveform to install a callback
*       and begin the asynchronous read operation.
*   5.  Inside the callback, call AnalogMultiChannelReader.EndReadWaveforme to
*       retrieve the data from the read operation.  
*   6.  Call AnalogMultiChannelReader.BeginMemoryOptimizedReadWaveform
*   7.  Dispose the Task object to clean-up any resources associated with the
*       task.
*   8.  Handle any DaqExceptions, if they occur.
*
*   Note: This example sets SynchronizeCallback to true. If SynchronizeCallback
*   is set to false, then you must give special consideration to safely dispose
*   the task and to update the UI from the callback. If SynchronizeCallback is
*   set to false, the callback executes on the worker thread and not on the main
*   UI thread. You can only update a UI component on the thread on which it was
*   created. Refer to the How to: Safely Dispose Task When Using Asynchronous
*   Callbacks topic in the NI-DAQmx .NET help for more information.
*
* I/O Connections Overview:
*   Make sure your signal input terminal matches the physical channel I/O
*   control. In the default case (differential channel ai0) wire the positive
*   lead for your signal to the ACH0 pin on your DAQ device and wire the
*   negative lead for your signal to the ACH8 pin on you DAQ device.  For more
*   information on the input and output terminals for your device, open the
*   NI-DAQmx Help, and refer to the NI-DAQmx Device Terminals and Device
*   Considerations books in the table of contents.
*
* Microsoft Windows Vista User Account Control
*   Running certain applications on Microsoft Windows Vista requires
*   administrator privileges, 
*   because the application name contains keywords such as setup, update, or
*   install. To avoid this problem, 
*   you must add an additional manifest to the application that specifies the
*   privileges required to run 
*   the application. Some Measurement Studio NI-DAQmx examples for Visual Studio
*   include these keywords. 
*   Therefore, all examples for Visual Studio are shipped with an additional
*   manifest file that you must 
*   embed in the example executable. The manifest file is named
*   [ExampleName].exe.manifest, where [ExampleName] 
*   is the NI-provided example name. For information on how to embed the manifest
*   file, refer to http://msdn2.microsoft.com/en-us/library/bb756929.aspx.Note: 
*   The manifest file is not provided with examples for Visual Studio .NET 2003.
*
******************************************************************************/

using System;
using System.Linq;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.IO.Ports;
using System.IO;

using NationalInstruments.DAQmx;
using System.Collections.Generic;
using System.Timers;
using System.Text;
using System.Diagnostics;
using System.Threading;


namespace NationalInstruments.Examples.ContAcqVoltageSamples_IntClk
{
    /// <summary>
    /// Summary description for MainForm.
    /// </summary>
    public class MainForm : System.Windows.Forms.Form
    {
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.GroupBox channelParametersGroupBox;
        private System.Windows.Forms.Label maximumLabel;
        private System.Windows.Forms.Label minimumLabel;
        private System.Windows.Forms.Label physicalChannelLabel;
        private System.Windows.Forms.Label rateLabel;
        private System.Windows.Forms.Label samplesLabel;
        private System.Windows.Forms.Label resultLabel;

        private AnalogMultiChannelReader analogInReader;
        private Task myTask;
        private Task runningTask;
        private AsyncCallback analogCallback;

        private AnalogWaveform<double>[] data;
        private DataColumn[] dataColumn = null;
        private DataTable dataTable = null;
        private System.Windows.Forms.GroupBox timingParametersGroupBox;
        private System.Windows.Forms.GroupBox acquisitionResultGroupBox;
        private System.Windows.Forms.DataGrid acquisitionDataGrid;
        private System.Windows.Forms.NumericUpDown rateNumeric;
        private System.Windows.Forms.NumericUpDown samplesPerChannelNumeric;
        internal System.Windows.Forms.NumericUpDown minimumValueNumeric;
        internal System.Windows.Forms.NumericUpDown maximumValueNumeric;
        private System.Windows.Forms.ComboBox physicalChannelComboBox;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;
        private System.Windows.Forms.DataVisualization.Charting.Chart outputChart;
        private List<double> RMS_list0 = new List<double>();
        private List<double> RMS_list1 = new List<double>();
        private List<double> RMS_list2 = new List<double>();
        private List<double> RMS_list3 = new List<double>();
        private List<double> RMS_list4 = new List<double>();
        private List<double> RMS_list5 = new List<double>();
        private List<double> RMS_list6 = new List<double>();

        private System.Timers.Timer timer;
        private ComboBox comComboBox;
        private Label comLabel;
        private int hasel_count = 1;
        private DateTime _lastCalledTime;
        private int elapsedPeriod = 1;
        private int bufferSize = 7;
        private int dataSize = 7;
        public List<List<double>> mavgData = new List<List<double>>(); // A list of lists to hold the data
        private List<double> filteredValues = new List<double>(); // A list with the 7 filtered values
        private CheckBox checkBox1;
        private bool useRMS = false; // use voltage instead of impedance

        public MainForm()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            stopButton.Enabled = false;
            dataTable = new DataTable();

            physicalChannelComboBox.Items.AddRange(DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External));
            if (physicalChannelComboBox.Items.Count > 0)
                physicalChannelComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                if (myTask != null)
                {
                    runningTask = null;
                    myTask.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series4 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series5 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series6 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series7 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series8 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Title title1 = new System.Windows.Forms.DataVisualization.Charting.Title();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.channelParametersGroupBox = new System.Windows.Forms.GroupBox();
            this.physicalChannelComboBox = new System.Windows.Forms.ComboBox();
            this.minimumValueNumeric = new System.Windows.Forms.NumericUpDown();
            this.maximumValueNumeric = new System.Windows.Forms.NumericUpDown();
            this.maximumLabel = new System.Windows.Forms.Label();
            this.minimumLabel = new System.Windows.Forms.Label();
            this.physicalChannelLabel = new System.Windows.Forms.Label();
            this.timingParametersGroupBox = new System.Windows.Forms.GroupBox();
            this.rateNumeric = new System.Windows.Forms.NumericUpDown();
            this.samplesLabel = new System.Windows.Forms.Label();
            this.rateLabel = new System.Windows.Forms.Label();
            this.samplesPerChannelNumeric = new System.Windows.Forms.NumericUpDown();
            this.startButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.acquisitionResultGroupBox = new System.Windows.Forms.GroupBox();
            this.resultLabel = new System.Windows.Forms.Label();
            this.acquisitionDataGrid = new System.Windows.Forms.DataGrid();
            this.outputChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.comComboBox = new System.Windows.Forms.ComboBox();
            this.comLabel = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.channelParametersGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minimumValueNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maximumValueNumeric)).BeginInit();
            this.timingParametersGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rateNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.samplesPerChannelNumeric)).BeginInit();
            this.acquisitionResultGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.acquisitionDataGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.outputChart)).BeginInit();
            this.SuspendLayout();
            // 
            // channelParametersGroupBox
            // 
            this.channelParametersGroupBox.Controls.Add(this.physicalChannelComboBox);
            this.channelParametersGroupBox.Controls.Add(this.minimumValueNumeric);
            this.channelParametersGroupBox.Controls.Add(this.maximumValueNumeric);
            this.channelParametersGroupBox.Controls.Add(this.maximumLabel);
            this.channelParametersGroupBox.Controls.Add(this.minimumLabel);
            this.channelParametersGroupBox.Controls.Add(this.physicalChannelLabel);
            this.channelParametersGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.channelParametersGroupBox.Location = new System.Drawing.Point(8, 8);
            this.channelParametersGroupBox.Name = "channelParametersGroupBox";
            this.channelParametersGroupBox.Size = new System.Drawing.Size(224, 120);
            this.channelParametersGroupBox.TabIndex = 2;
            this.channelParametersGroupBox.TabStop = false;
            this.channelParametersGroupBox.Text = "Channel Parameters";
            // 
            // physicalChannelComboBox
            // 
            this.physicalChannelComboBox.Location = new System.Drawing.Point(120, 24);
            this.physicalChannelComboBox.Name = "physicalChannelComboBox";
            this.physicalChannelComboBox.Size = new System.Drawing.Size(96, 21);
            this.physicalChannelComboBox.TabIndex = 1;
            this.physicalChannelComboBox.Text = "COM5";
            // 
            // minimumValueNumeric
            // 
            this.minimumValueNumeric.DecimalPlaces = 2;
            this.minimumValueNumeric.Location = new System.Drawing.Point(120, 56);
            this.minimumValueNumeric.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.minimumValueNumeric.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            -2147483648});
            this.minimumValueNumeric.Name = "minimumValueNumeric";
            this.minimumValueNumeric.Size = new System.Drawing.Size(96, 20);
            this.minimumValueNumeric.TabIndex = 3;
            this.minimumValueNumeric.Value = new decimal(new int[] {
            100,
            0,
            0,
            -2147418112});
            // 
            // maximumValueNumeric
            // 
            this.maximumValueNumeric.DecimalPlaces = 2;
            this.maximumValueNumeric.Location = new System.Drawing.Point(120, 88);
            this.maximumValueNumeric.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.maximumValueNumeric.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            -2147483648});
            this.maximumValueNumeric.Name = "maximumValueNumeric";
            this.maximumValueNumeric.Size = new System.Drawing.Size(96, 20);
            this.maximumValueNumeric.TabIndex = 5;
            this.maximumValueNumeric.Value = new decimal(new int[] {
            100,
            0,
            0,
            65536});
            // 
            // maximumLabel
            // 
            this.maximumLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.maximumLabel.Location = new System.Drawing.Point(16, 88);
            this.maximumLabel.Name = "maximumLabel";
            this.maximumLabel.Size = new System.Drawing.Size(112, 16);
            this.maximumLabel.TabIndex = 4;
            this.maximumLabel.Text = "Maximum Value (V):";
            // 
            // minimumLabel
            // 
            this.minimumLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.minimumLabel.Location = new System.Drawing.Point(16, 56);
            this.minimumLabel.Name = "minimumLabel";
            this.minimumLabel.Size = new System.Drawing.Size(104, 15);
            this.minimumLabel.TabIndex = 2;
            this.minimumLabel.Text = "Minimum Value (V):";
            // 
            // physicalChannelLabel
            // 
            this.physicalChannelLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.physicalChannelLabel.Location = new System.Drawing.Point(16, 26);
            this.physicalChannelLabel.Name = "physicalChannelLabel";
            this.physicalChannelLabel.Size = new System.Drawing.Size(96, 16);
            this.physicalChannelLabel.TabIndex = 0;
            this.physicalChannelLabel.Text = "Physical Channel:";
            // 
            // timingParametersGroupBox
            // 
            this.timingParametersGroupBox.Controls.Add(this.rateNumeric);
            this.timingParametersGroupBox.Controls.Add(this.samplesLabel);
            this.timingParametersGroupBox.Controls.Add(this.rateLabel);
            this.timingParametersGroupBox.Controls.Add(this.samplesPerChannelNumeric);
            this.timingParametersGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.timingParametersGroupBox.Location = new System.Drawing.Point(8, 136);
            this.timingParametersGroupBox.Name = "timingParametersGroupBox";
            this.timingParametersGroupBox.Size = new System.Drawing.Size(224, 92);
            this.timingParametersGroupBox.TabIndex = 3;
            this.timingParametersGroupBox.TabStop = false;
            this.timingParametersGroupBox.Text = "Timing Parameters";
            // 
            // rateNumeric
            // 
            this.rateNumeric.DecimalPlaces = 2;
            this.rateNumeric.Location = new System.Drawing.Point(120, 56);
            this.rateNumeric.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.rateNumeric.Name = "rateNumeric";
            this.rateNumeric.Size = new System.Drawing.Size(96, 20);
            this.rateNumeric.TabIndex = 3;
            this.rateNumeric.Value = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            // 
            // samplesLabel
            // 
            this.samplesLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.samplesLabel.Location = new System.Drawing.Point(16, 26);
            this.samplesLabel.Name = "samplesLabel";
            this.samplesLabel.Size = new System.Drawing.Size(104, 16);
            this.samplesLabel.TabIndex = 0;
            this.samplesLabel.Text = "Samples/Channel:";
            // 
            // rateLabel
            // 
            this.rateLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.rateLabel.Location = new System.Drawing.Point(16, 58);
            this.rateLabel.Name = "rateLabel";
            this.rateLabel.Size = new System.Drawing.Size(56, 16);
            this.rateLabel.TabIndex = 2;
            this.rateLabel.Text = "Rate (Hz):";
            // 
            // samplesPerChannelNumeric
            // 
            this.samplesPerChannelNumeric.Location = new System.Drawing.Point(120, 24);
            this.samplesPerChannelNumeric.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.samplesPerChannelNumeric.Name = "samplesPerChannelNumeric";
            this.samplesPerChannelNumeric.Size = new System.Drawing.Size(96, 20);
            this.samplesPerChannelNumeric.TabIndex = 1;
            this.samplesPerChannelNumeric.Value = new decimal(new int[] {
            3000,
            0,
            0,
            0});
            // 
            // startButton
            // 
            this.startButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.startButton.Location = new System.Drawing.Point(24, 240);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(80, 24);
            this.startButton.TabIndex = 0;
            this.startButton.Text = "Start";
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // stopButton
            // 
            this.stopButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.stopButton.Location = new System.Drawing.Point(136, 240);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(80, 24);
            this.stopButton.TabIndex = 1;
            this.stopButton.Text = "Stop";
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // acquisitionResultGroupBox
            // 
            this.acquisitionResultGroupBox.Controls.Add(this.resultLabel);
            this.acquisitionResultGroupBox.Controls.Add(this.acquisitionDataGrid);
            this.acquisitionResultGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.acquisitionResultGroupBox.Location = new System.Drawing.Point(240, 8);
            this.acquisitionResultGroupBox.Name = "acquisitionResultGroupBox";
            this.acquisitionResultGroupBox.Size = new System.Drawing.Size(304, 256);
            this.acquisitionResultGroupBox.TabIndex = 4;
            this.acquisitionResultGroupBox.TabStop = false;
            this.acquisitionResultGroupBox.Text = "Acquisition Results";
            // 
            // resultLabel
            // 
            this.resultLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.resultLabel.Location = new System.Drawing.Point(8, 16);
            this.resultLabel.Name = "resultLabel";
            this.resultLabel.Size = new System.Drawing.Size(112, 16);
            this.resultLabel.TabIndex = 0;
            this.resultLabel.Text = "Acquisition Data (V):";
            // 
            // acquisitionDataGrid
            // 
            this.acquisitionDataGrid.AllowSorting = false;
            this.acquisitionDataGrid.DataMember = "";
            this.acquisitionDataGrid.HeaderForeColor = System.Drawing.SystemColors.ControlText;
            this.acquisitionDataGrid.Location = new System.Drawing.Point(16, 32);
            this.acquisitionDataGrid.Name = "acquisitionDataGrid";
            this.acquisitionDataGrid.ParentRowsVisible = false;
            this.acquisitionDataGrid.ReadOnly = true;
            this.acquisitionDataGrid.Size = new System.Drawing.Size(280, 216);
            this.acquisitionDataGrid.TabIndex = 1;
            this.acquisitionDataGrid.TabStop = false;
            this.acquisitionDataGrid.Navigate += new System.Windows.Forms.NavigateEventHandler(this.acquisitionDataGrid_Navigate);
            // 
            // outputChart
            // 
            chartArea1.Name = "ChartArea1";
            this.outputChart.ChartAreas.Add(chartArea1);
            this.outputChart.Cursor = System.Windows.Forms.Cursors.No;
            legend1.Name = "Legend1";
            this.outputChart.Legends.Add(legend1);
            this.outputChart.Location = new System.Drawing.Point(587, 12);
            this.outputChart.Name = "outputChart";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series1.Color = System.Drawing.Color.Blue;
            series1.Legend = "Legend1";
            series1.Name = "outputSeries-1";
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series2.Color = System.Drawing.Color.Lime;
            series2.Legend = "Legend1";
            series2.Name = "outputSeries-2";
            series3.ChartArea = "ChartArea1";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastLine;
            series3.Color = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            series3.Legend = "Legend1";
            series3.Name = "outputSeries-3";
            series4.ChartArea = "ChartArea1";
            series4.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series4.Color = System.Drawing.Color.Fuchsia;
            series4.Legend = "Legend1";
            series4.Name = "outputSeries-4";
            series5.ChartArea = "ChartArea1";
            series5.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series5.Color = System.Drawing.Color.Red;
            series5.Legend = "Legend1";
            series5.Name = "outputSeries-5";
            series6.ChartArea = "ChartArea1";
            series6.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series6.Color = System.Drawing.Color.Yellow;
            series6.Legend = "Legend1";
            series6.Name = "outputSeries-6";
            series7.ChartArea = "ChartArea1";
            series7.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series7.Color = System.Drawing.Color.Black;
            series7.Legend = "Legend1";
            series7.Name = "outputSeries-7";
            series7.YValuesPerPoint = 2;
            series8.ChartArea = "ChartArea1";
            series8.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series8.Legend = "Legend1";
            series8.Name = "outputSeries";
            this.outputChart.Series.Add(series1);
            this.outputChart.Series.Add(series2);
            this.outputChart.Series.Add(series3);
            this.outputChart.Series.Add(series4);
            this.outputChart.Series.Add(series5);
            this.outputChart.Series.Add(series6);
            this.outputChart.Series.Add(series7);
            this.outputChart.Series.Add(series8);
            this.outputChart.Size = new System.Drawing.Size(548, 300);
            this.outputChart.TabIndex = 5;
            this.outputChart.Text = "chart1";
            title1.Name = "Title1";
            title1.Text = "Impedance";
            this.outputChart.Titles.Add(title1);
            this.outputChart.Click += new System.EventHandler(this.outputChart_Click);
            // 
            // comComboBox
            // 
            this.comComboBox.FormattingEnabled = true;
            this.comComboBox.Items.AddRange(new object[] {
            "COM1",
            "COM2",
            "COM3",
            "COM5",
            "COM6"});
            this.comComboBox.Location = new System.Drawing.Point(71, 306);
            this.comComboBox.Name = "comComboBox";
            this.comComboBox.Size = new System.Drawing.Size(121, 21);
            this.comComboBox.TabIndex = 6;
            // 
            // comLabel
            // 
            this.comLabel.AutoSize = true;
            this.comLabel.Location = new System.Drawing.Point(12, 309);
            this.comLabel.Name = "comLabel";
            this.comLabel.Size = new System.Drawing.Size(56, 13);
            this.comLabel.TabIndex = 7;
            this.comLabel.Text = "COM Port:";
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(978, 163);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(157, 17);
            this.checkBox1.TabIndex = 8;
            this.checkBox1.Text = "Change Measurement Type";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // MainForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(1250, 400);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.comLabel);
            this.Controls.Add(this.comComboBox);
            this.Controls.Add(this.outputChart);
            this.Controls.Add(this.acquisitionResultGroupBox);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.timingParametersGroupBox);
            this.Controls.Add(this.channelParametersGroupBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Continuous Acquisition of Voltage Samples - Internal Clock";
            this.channelParametersGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.minimumValueNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maximumValueNumeric)).EndInit();
            this.timingParametersGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rateNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.samplesPerChannelNumeric)).EndInit();
            this.acquisitionResultGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.acquisitionDataGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.outputChart)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.DoEvents();
            Application.Run(new MainForm());
        }

        private void startButton_Click(object sender, System.EventArgs e)
        {
            if (runningTask == null)
            {
                try
                {

                    stopButton.Enabled = true;
                    startButton.Enabled = false;

                    // Initialize the sensor data matrix with empty lists
                    for (int i = 0; i < bufferSize; i++)
                    {
                        List<double> row = new List<double>();
                        for (int j = 0; j < dataSize; j++)
                        {
                            row.Add(0.0);
                        }
                        mavgData.Add(row);
                    }

                    // Initialize the filtered data vector
                    for (int i = 0; i < dataSize; i++)
                    {
                        filteredValues.Add(0.0);
                    }

                    Console.WriteLine(filteredValues);

                    Console.WriteLine(comComboBox.Text);

                    // Set up the serial port


                    // Start the timer

                    // Create a new task
                    myTask = new Task();

                    // Create a virtual channel
                    myTask.AIChannels.CreateVoltageChannel(physicalChannelComboBox.Text, "",
                        (AITerminalConfiguration)(-1), Convert.ToDouble(minimumValueNumeric.Value),
                        Convert.ToDouble(maximumValueNumeric.Value), AIVoltageUnits.Volts);

                    // Create the second virtual channel
                    myTask.AIChannels.CreateVoltageChannel("myDAQ1/ai1", "",
                        (AITerminalConfiguration)(-1), Convert.ToDouble(minimumValueNumeric.Value),
                        Convert.ToDouble(maximumValueNumeric.Value), AIVoltageUnits.Volts);

                    // Configure the timing parameters
                    myTask.Timing.ConfigureSampleClock("", Convert.ToDouble(rateNumeric.Value),
                        SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, 1000);

                    // Verify the Task
                    myTask.Control(TaskAction.Verify);

                    // Prepare the table for Data
                  //  InitializeDataTable(myTask.AIChannels, ref dataTable);
                  //  acquisitionDataGrid.DataSource = dataTable;

                    runningTask = myTask;
                    analogInReader = new AnalogMultiChannelReader(myTask.Stream);
                    analogCallback = new AsyncCallback(AnalogInCallback);

                    // Use SynchronizeCallbacks to specify that the object 
                    // marshals callbacks across threads appropriately.
                    analogInReader.SynchronizeCallbacks = true;
                    // Call the BeginReadWaveform method to start reading data asynchronously
                    analogInReader.BeginReadWaveform(Convert.ToInt32(samplesPerChannelNumeric.Value),
                        analogCallback, myTask);
                }
                catch (DaqException exception)
                {
                    // Display Errors
                    MessageBox.Show(exception.Message);
                    runningTask = null;
                    myTask.Dispose();
                    stopButton.Enabled = false;
                    startButton.Enabled = true;
                }
            }
        }


        private void AnalogInCallback(IAsyncResult ar)
        {
            try
            {
                if (runningTask != null && runningTask == ar.AsyncState)
                {
                 
                    for(int i = 1; i < 8; i++)
                    {
                        outputChart.Series[$"outputSeries-{i}"].Points.Clear();
                    }
                    outputChart.Series["outputSeries"].Points.Clear();


                    //outputChart.Series["borderSeries"].Points.Clear();


                    // Read the available data from the channels
                    data = analogInReader.EndReadWaveform(ar);
                    
                    //Max' code start
                    double[] samples = data[0].GetRawData();
                    Console.WriteLine(samples[0] + "first measurement");
                    double[] samples_curr = data[1].GetRawData();
                     Console.WriteLine(samples_curr[0] + "first measurement");
                    
                    int numSamples = samples.Length;
              
                    int index = 0;

                    // Logic for identifiying Hasels

                   // double threshold = 2.8; // Threshold voltage which determines not connected mux
                    int cutoffLeft = 150;  // Samples omitted from left side (bigger because more subsceptible to error)
                    int cutoffRight = 50; // Samples omitted from right side
                    int samples_control = 200; // Number of samples of control --> maybe TODO implement automatically identified
                    int hbatch_length = 400; // number of samples per hasel
                    int hbatch_usable_length = hbatch_length - cutoffLeft - cutoffRight; // number of usable samples
                    List<double> List_RMS = new List<double>(); // Array for RMS values
                    List<double> List_RMS_curr = new List<double>(); // Array for RMS values
                    List<double> List_RMS_inverse = new List<double>(); // List for RMS values on left side, inverse direction
                    List<double> List_RMS_curr_inverse = new List<double>();  // List for RMS values on left side, inverse direction
                    int threshold_robustness = numSamples - samples_control; // Threshold if not connected mux (control) too close to edge of batch
                    double RMS_split = 0; // Initialize rms for split hasel
                    double RMS_curr_split = 0; // Initialize rms for split hasel

                    double threshold = 0.05;
                    int threshold_window = 10; // Change the window size as needed
                    index = -1;

                    // Get index of rightmost not connected signal
                    for (int i = numSamples - 1; i >= 0; i--)
                    {
                        if (i + threshold_window < numSamples)
                        {
                            bool values_below_threshold = true;
                            for (int j = i; j < i + threshold_window; j++)
                            {
                                if (Math.Abs(samples[j]) >= threshold)
                                {
                                    values_below_threshold = false;
                                    break;
                                }
                            }
                            if (values_below_threshold)
                            {
                                index = i + threshold_window - 1;
                                break;
                            }
                        }
                    }


                    /*

                    // Get index of rightmost not connected signal
                    for (int i = numSamples-1; i >= 0; i--) {
                        if(samples[i] > threshold)
                        {
                            index = i;
                            break;
                        } 
                    }
                    */
                    if (index == -1) { Console.WriteLine("No value above " + threshold.ToString() + " found!"); }

                    //edge case index handling
                    if (index > threshold_robustness)
                    // if there is overlap over edge then control within samples_control from left side
                    {
                        for (int i = samples_control-1; i >= 0; i--)
                        {
                            if (i + threshold_window < numSamples)
                            {
                                bool values_below_threshold = true;
                                for (int j = i; j < i + threshold_window; j++)
                                {
                                    if (Math.Abs(samples[j]) >= threshold)
                                    {
                                        values_below_threshold = false;
                                        break;
                                    }
                                }
                                if (values_below_threshold)
                                {
                                    index = i + threshold_window - 1;
                                    break;
                                }
                            }

                            //if index not found then index > threshold was correct
                        }
                    }


                    int index_control = index;
                    
                    Console.WriteLine("index is:" + index);
                    
                    while(index < numSamples - hbatch_length + 1)
                    {
                        double batchRMS = 0;
                        double batchRMS_curr = 0;
                        for (int i = index + cutoffLeft; i < index + hbatch_length - cutoffRight; i++)
                        // Go through sample to calculate RMS, exclude the cutOffSamples
                        {
                            batchRMS += Math.Pow(samples[i], 2);
                            batchRMS_curr += Math.Pow(samples_curr[i], 2);
                        }
                        batchRMS = batchRMS / hbatch_usable_length;
                        batchRMS = Math.Sqrt(batchRMS);
                        batchRMS_curr = batchRMS_curr / hbatch_usable_length;
                        batchRMS_curr = Math.Sqrt(batchRMS_curr);
                        if (useRMS)
                        {
                            List_RMS.Add(batchRMS);                 // voltage measurement code
                        }
                        else
                        {
                            //List_RMS.Add(batchRMS_curr);                   // current measurement code
                            List_RMS.Add(batchRMS/batchRMS_curr);       // impedance measurement code
                        }


                        index += hbatch_length;
                    }
                    // add cutOff for split sample
                    index += cutoffLeft;

                    //debugging
                    /*
                    foreach (double item in List_RMS)
                    {
                        Console.Write(item + ", ");
                    }
                    Console.WriteLine("New array");
                    */


                    // handle first part of split sample
                    int split_batch_length = 0;
                    while (index < numSamples - 50)
                    {
                        for(int i = index; i < index + 50; i++)
                        {
                            RMS_split += Math.Pow(samples[i], 2);
                            RMS_curr_split += Math.Pow(samples_curr[i], 2);
                        }
                        index += 50;
                        split_batch_length += 50;
                    }
                    if(split_batch_length != 0)
                    {
                        RMS_split = RMS_split / split_batch_length;
                        RMS_curr_split = RMS_curr_split / split_batch_length;
                    }
                    RMS_split = Math.Sqrt(RMS_split);
                    RMS_curr_split = Math.Sqrt(RMS_curr_split);

                    //Get to left of control
                    index = index_control - samples_control;

                    // Go through left side -> inverse logic
                    while(index > hbatch_length)
                    {
                        double batchRMS = 0;
                        double batchRMS_curr = 0;
                        for (int i = index - cutoffRight; i > index - hbatch_length + cutoffLeft; i--)
                        // Go through sample to calculate RMS, exclude the cutOffSamples
                        {
                            batchRMS += Math.Pow(samples[i], 2);
                            batchRMS_curr += Math.Pow(samples_curr[i], 2);
                        }
                        batchRMS = batchRMS / hbatch_usable_length;
                        batchRMS = Math.Sqrt(batchRMS);
                        batchRMS_curr = batchRMS_curr / hbatch_usable_length;
                        batchRMS_curr = Math.Sqrt(batchRMS_curr);
                        if (useRMS)
                        {
                            List_RMS_inverse.Add(batchRMS);                         // voltage measurement code
                        }
                        else
                        {
                            //List_RMS_inverse.Add(batchRMS_curr);                  // current measurement code
                            List_RMS_inverse.Add(batchRMS/batchRMS_curr);           // impedance measurement code  
                        }

                        index -= hbatch_length;
                    }
                    index -= cutoffRight;

                    //Split left side
                    split_batch_length = 0;
                    double RMS_split_left = 0;
                    double RMS_curr_split_left = 0;

                    while (index >= 50)
                    {
                        for (int i = index - 1; i >= index - 50; i--)
                        {
                            RMS_split_left += Math.Pow(samples[i], 2);
                            RMS_curr_split_left += Math.Pow(samples_curr[i], 2);
                        }
                        index -= 50;
                        split_batch_length += 50;
                    }
                    if(split_batch_length != 0)
                    {
                        RMS_split_left = RMS_split_left / split_batch_length;
                        RMS_curr_split_left = RMS_curr_split_left / split_batch_length;
                    }
                    RMS_split_left = Math.Sqrt(RMS_split_left);
                    RMS_curr_split_left = Math.Sqrt(RMS_curr_split_left);
                    // Debugging

                    Console.WriteLine("RMS_split: " + RMS_split);
                    Console.WriteLine("RMS_split_left: " + RMS_split_left);
                    Console.WriteLine("RMS_curr_split: " + RMS_curr_split);
                    Console.WriteLine("RMS_curr_split_left: " + RMS_curr_split_left);
                    if (RMS_split == 0)
                    {
                        RMS_split = RMS_split_left;
                        RMS_curr_split = RMS_curr_split_left;
                    }
                    else if(RMS_split_left != 0)
                    {
                        RMS_split = (RMS_split + RMS_split_left) / 2; // Get RMS of split by averaging left and right split
                        RMS_curr_split = (RMS_curr_split + RMS_curr_split_left) / 2;
                    }
                    
                    //handle array concetanation
                    List_RMS_inverse.Reverse();
                    if (useRMS)
                    {
                        List_RMS.Add(RMS_split);                    // voltage measurement code
                    }
                    else
                    {
                        //List_RMS.Add(RMS_curr_split);             // current measurement code
                        List_RMS.Add(RMS_split/RMS_curr_split);     // impedance measurement code
                    }

                    List_RMS.AddRange(List_RMS_inverse);
                   

                    //Debugging

                    foreach (double item in List_RMS)
                    {
                        Console.Write(item + ", ");
                    }
                    Console.WriteLine("\n");

                    foreach (double item in List_RMS)
                    {
                        Console.Write(item + ", ");
                    }
                    Console.WriteLine("\n");

                    Console.WriteLine("New array");


                    /*
                                        int[] borderLines = new int[numSamples];

                                        for (int i = index; i < numSamples; i += 400)
                                        {
                                            borderLines[i] = 3;
                                        }
                                        if(index >= 200)
                                        {
                                            for (int i = index - 200; i >= 0; i -= 400)
                                            {
                                                borderLines[i] = 3;
                                            }
                                        }

                    */
                    List<double> List_RMS_short7 = new List<double>(); // Array for RMS values


                    if (List_RMS.Count > 7)
                    {
                        for(int i = 0; i < 7; i++)
                        {
                            List_RMS_short7.Add(List_RMS[i]);
                            Console.Write(List_RMS[i]);
                        }
                      //  Console.WriteLine("\n");
                    }
                    else
                    {
                        List_RMS_short7 = List_RMS;
                    }
                    
                    Transpose(ref mavgData);
                    for (int i = 0; i < dataSize; i++)
                    {

                        double value = List_RMS_short7[i];
                        
                        mavgData[i].RemoveAt(0);

                        //data[i].Add(value);
                        mavgData[i].Add(value);
                    }
                    Transpose(ref mavgData);



                    // Apply the moving average filter to the data
                    for (int i = 0; i < dataSize; i++)
                    {
                        filteredValues[i] = MovingAverage(mavgData, i);
                    }

                    // Write into txt file
                    string filePath = @"C:\tmp\values_muxes.txt";
                    using (StreamWriter writer = new StreamWriter(filePath, false))
                    {
                        string fileContent = string.Join(",", List_RMS_short7);
                        writer.Write(fileContent);
                        //writer.Write(string.Join(",", List_RMS_short7));
                    }
                    
                    for (int j = 0; j < 7; j++)
                    {
                        
                        switch (j)
                        {
                            case 0:
                                //find average of filtered signal
                                if (RMS_list0.Count < 200)
                                {
                                    RMS_list0.Add(filteredValues[0]);
                                }
                                else
                                {
                                    RMS_list0.RemoveAt(0);
                                    RMS_list0.Add(filteredValues[0]);
                                }

                                for (int i = 0; i < RMS_list0.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list0.Count) / 2;

                                    outputChart.Series["outputSeries-1"].Points.AddXY(time, RMS_list0[i]);
                                }

                                break;
                            case 1:
                                //find average of filtered signal
                                if (RMS_list1.Count < 200)
                                {
                                    RMS_list1.Add(filteredValues[1]);
                                }
                                else
                                {
                                    RMS_list1.RemoveAt(0);
                                    RMS_list1.Add(filteredValues[1]);
                                }

                                for (int i = 0; i < RMS_list1.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list1.Count) / 2;

                                   // outputChart.Series["outputSeries-2"].Points.AddXY(time, RMS_list1[i]);
                                }

                                break;
                            case 2:
                                //find average of filtered signal
                                if (RMS_list2.Count < 200)
                                {
                                    RMS_list2.Add(filteredValues[2]);
                                }
                                else
                                {
                                    RMS_list2.RemoveAt(0);
                                    RMS_list2.Add(filteredValues[2]);
                                }

                                for (int i = 0; i < RMS_list2.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list2.Count) / 2;

                                    //outputChart.Series["outputSeries-3"].Points.AddXY(time, RMS_list2[i]);
                                }

                                break;
                            case 3:
                                //find average of filtered signal
                                if (RMS_list3.Count < 200)
                                {
                                    RMS_list3.Add(filteredValues[3]);
                                }
                                else
                                {
                                    RMS_list3.RemoveAt(0);
                                    RMS_list3.Add(filteredValues[3]);
                                }

                                for (int i = 0; i < RMS_list3.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list3.Count) / 2;

                                    //outputChart.Series["outputSeries-4"].Points.AddXY(time, RMS_list3[i]);
                                }

                                break;
                            case 4:
                                //find average of filtered signal
                                if (RMS_list4.Count < 200)
                                {
                                    RMS_list4.Add(filteredValues[4]);
                                }
                                else
                                {
                                    RMS_list4.RemoveAt(0);
                                    RMS_list4.Add(filteredValues[4]);
                                }

                                for (int i = 0; i < RMS_list4.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list4.Count) / 2;

                                    //outputChart.Series["outputSeries-5"].Points.AddXY(time, RMS_list4[i]);
                                }

                                break;
                            case 5:
                                //find average of filtered signal
                                if (RMS_list5.Count < 200)
                                {
                                    RMS_list5.Add(filteredValues[5]);
                                }
                                else
                                {
                                    RMS_list5.RemoveAt(0);
                                    RMS_list5.Add(filteredValues[5]);
                                }

                                for (int i = 0; i < RMS_list5.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list5.Count) / 2;

                                   // outputChart.Series["outputSeries-6"].Points.AddXY(time, RMS_list5[i]);
                                }

                                break;
                            case 6:
                                //find average of filtered signal
                                if (RMS_list6.Count < 200)
                                {
                                    RMS_list6.Add(filteredValues[6]);
                                }
                                else
                                {
                                    RMS_list6.RemoveAt(0);
                                    RMS_list6.Add(filteredValues[6]);
                                }

                                for (int i = 0; i < RMS_list6.Count; i++)
                                {
                                    double time = ((i + 1.0) / RMS_list6.Count) / 2;

                                  //  outputChart.Series["outputSeries-7"].Points.AddXY(time, RMS_list6[i]);
                                }

                                break;
                           
     
                            default:
                                Console.WriteLine("Value is not 1, 2, ... or 8");
                                break;
                        }

                    }

                    for (int i = 0; i < numSamples; i++)
                    {

                        double time = ((i + 1.0) / numSamples) / 2;
                        if(samples_curr[i] < -6)
                        {
                            samples_curr[i] = 0;
                        }
                        //outputChart.Series["outputSeries"].Points.AddXY(time, samples_curr[i]);
                        //outputChart.Series["borderSeries"].Points.AddXY(time, borderLines[i]);
                        //batchRMS += Math.Pow(samples[i], 2);
                    }


                    /*
                    
                    for (int i = 0; i < numSamples; i++)
                    {

                        double time = ((i + 1.0) / numSamples) / 2;
                        outputChart.Series["outputSeries"].Points.AddXY(time, samples[i]);
                        //outputChart.Series["borderSeries"].Points.AddXY(time, borderLines[i]);
                        //batchRMS += Math.Pow(samples[i], 2);
                    }

                   // batchRMS = batchRMS / numSamples;
                   // batchRMS = Math.Sqrt(batchRMS);
                   // Console.WriteLine(batchRMS);
                    */
                    // Plot your data here
                    //dataToDataTable(data, ref dataTable);
                    analogInReader.BeginMemoryOptimizedReadWaveform(Convert.ToInt32(samplesPerChannelNumeric.Value), analogCallback, myTask, data);

                    //byte[] asciiBytes = Encoding.ASCII.GetBytes(1.ToString()); // Convert integer to ASCII bytes
                    //serialPort.Write(asciiBytes, 0, asciiBytes.Length);
            
                    
                    //MAIN FILE

                }
            }
            catch (DaqException exception)
            {
                // Display Errors
                MessageBox.Show(exception.Message);
                runningTask = null;
                myTask.Dispose();
                stopButton.Enabled = false;
                startButton.Enabled = true;
            }
        }

        private double MovingAverage(List<List<double>> data, int colIndex)
        {
            double sum = 0;
            for (int i = 0; i < data.Count; i++)
            {
                sum += data[i][colIndex];
            }
            sum /= data.Count;
            return sum;
        }

        public static void Transpose<T>(ref List<List<T>> matrix)
        {
            int numRows = matrix.Count;
            int numCols = matrix[0].Count;

            var transposed = new List<List<T>>();

            for (int i = 0; i < numCols; i++)
            {
                var row = new List<T>();

                for (int j = 0; j < numRows; j++)
                {
                    row.Add(matrix[j][i]);
                }

                transposed.Add(row);
            }

            matrix = transposed;
        }


        private void stopButton_Click(object sender, System.EventArgs e)
        {
            if (runningTask != null)
            {
                // Dispose of the task
                runningTask = null;
                myTask.Dispose();
                stopButton.Enabled = false;
                startButton.Enabled = true;
            }
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
            }
        }


        /*
        private void dataToDataTable(AnalogWaveform<double>[] sourceArray, ref DataTable dataTable)
        {
            // Iterate over channels
            int currentLineIndex = 0;
            foreach (AnalogWaveform<double> waveform in sourceArray)
            {
                for (int sample = 0; sample < waveform.Samples.Count; ++sample)
                {
                    if (sample == 10)
                        break;

                    dataTable.Rows[sample][currentLineIndex] = waveform.Samples[sample].Value;
                }
                currentLineIndex++;
            }
        }

        public void InitializeDataTable(AIChannelCollection channelCollection, ref DataTable data)
        {
            int numOfChannels = channelCollection.Count;
            data.Rows.Clear();
            data.Columns.Clear();
            dataColumn = new DataColumn[numOfChannels];
            int numOfRows = 10;

            for (int currentChannelIndex = 0; currentChannelIndex < numOfChannels; currentChannelIndex++)
            {
                dataColumn[currentChannelIndex] = new DataColumn();
                dataColumn[currentChannelIndex].DataType = typeof(double);
                dataColumn[currentChannelIndex].ColumnName = channelCollection[currentChannelIndex].PhysicalName;
            }

            data.Columns.AddRange(dataColumn);

            for (int currentDataIndex = 0; currentDataIndex < numOfRows; currentDataIndex++)
            {
                object[] rowArr = new object[numOfChannels];
                data.Rows.Add(rowArr);
            }
        }
        */
        private void acquisitionDataGrid_Navigate(object sender, NavigateEventArgs ne)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (useRMS == false)
            {
                useRMS = true;
                outputChart.Titles[0].Text = "Voltage";
            }
            else
            {
                useRMS = false;
                outputChart.Titles[0].Text = "Impedance";
            }
        }

        private void outputChart_Click(object sender, EventArgs e)
        {

        }
    }
}
