/******************************************************************************
*
* Example program:
*   ContAcqVoltSmpls_EveryNSamplesEvent
*
* Category:
*   AI
*
* Description:
*   This example demonstrates how to use Every N Samples events to acquire a
*   continuous amount of data using the DAQ device's internal clock. The Every N
*   Samples events indicate when data is available from DAQmx.
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
*   4.  Set the Samples per Channel control. This will determine how many
*       samples are read each time.
*
* Steps:
*   1.  Create a new analog input task.
*   2.  Create an analog input voltage channel.
*   3.  Set up the timing for the acquisition. In this example, we use the DAQ
*       device's internal clock to continuously acquire samples.
*   4.  Register a callback to receive the Every N Samples event which occurs
*       each time the specified number of samples are transferred from the
*       device to the DAQmx driver.
*   5.  Dispose the Task object to clean-up any resources associated with the
*       task.
*   6.  Handle any DaqExceptions, if they occur.
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
using System.Drawing;
using System.Linq;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Windows.Forms.DataVisualization.Charting;
using NationalInstruments.DAQmx;
// used for complex numbers
using System.Numerics;
using System.IO;
using System.Text;

//used for generating sine waveforms
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Collections.Generic;

using System.IO.Ports;

namespace NationalInstruments.Examples.ContAcqVoltSmpls_EveryNSamplesEvent
{
    /// <summary>
    /// Summary description for MainForm.
    /// </summary>
    public class main : System.Windows.Forms.Form
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
        

        private AnalogWaveform<double>[] data;

        private DataColumn[] dataColumn =null;
        private DataTable dataTable=null;
        private System.Windows.Forms.GroupBox timingParametersGroupBox;
        private System.Windows.Forms.GroupBox acquisitionResultGroupBox;
        private System.Windows.Forms.DataGrid acquisitionDataGrid;
        private System.Windows.Forms.NumericUpDown rateNumeric;
        private System.Windows.Forms.NumericUpDown samplesPerChannelNumeric;
        internal System.Windows.Forms.NumericUpDown minimumValueNumeric;
        internal System.Windows.Forms.NumericUpDown maximumValueNumeric;
        private System.Windows.Forms.ComboBox physicalChannelComboBox;
        private Chart FFTChart;

        //FFT variables
        private double mag;
        private double threshhold;
        private Chart outputChart;
        private List<double> avgFFT1 = new List<double>();
        private List<double> avgFFT2 = new List<double>();
        private List<double> avgFFT3 = new List<double>();
        private List<double> avgFFT4 = new List<double>();
        private List<double> avgFFT5 = new List<double>();
        private List<double> avgFFT6 = new List<double>();
        private List<double> avgFFT7 = new List<double>();
        private List<double> avgFFT8 = new List<double>();

        private TrackBar threshholdTrackBar;
        private Label threshholdLabel;
        private CheckBox checkBox1;
        private double batchAverage = 0;
        private double batchRMS = 0;
        private string outputSeries;
        private double[] values = new double[8];
        SerialPort port = new SerialPort("COM5", 9600);
        private Label switchLabel;
        private TrackBar switchBar;
        private int switchNumber = 1;
        private int hasel_count = 1;



        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        public main()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            stopButton.Enabled = false;
            dataTable= new DataTable();

            physicalChannelComboBox.Items.AddRange(DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External));
            if (physicalChannelComboBox.Items.Count > 0)
                physicalChannelComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
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
            base.Dispose( disposing );
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
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea2 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend2 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series4 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series5 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series6 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series7 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series8 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series9 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series10 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(main));
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
            this.switchLabel = new System.Windows.Forms.Label();
            this.switchBar = new System.Windows.Forms.TrackBar();
            this.threshholdLabel = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.threshholdTrackBar = new System.Windows.Forms.TrackBar();
            this.FFTChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.resultLabel = new System.Windows.Forms.Label();
            this.acquisitionDataGrid = new System.Windows.Forms.DataGrid();
            this.outputChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.channelParametersGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minimumValueNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maximumValueNumeric)).BeginInit();
            this.timingParametersGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rateNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.samplesPerChannelNumeric)).BeginInit();
            this.acquisitionResultGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.switchBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.threshholdTrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.FFTChart)).BeginInit();
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
            this.physicalChannelComboBox.Text = "lol";
            this.physicalChannelComboBox.SelectedIndexChanged += new System.EventHandler(this.physicalChannelComboBox_SelectedIndexChanged);
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
            200000,
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
            400,
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
            this.acquisitionResultGroupBox.Controls.Add(this.switchLabel);
            this.acquisitionResultGroupBox.Controls.Add(this.switchBar);
            this.acquisitionResultGroupBox.Controls.Add(this.threshholdLabel);
            this.acquisitionResultGroupBox.Controls.Add(this.checkBox1);
            this.acquisitionResultGroupBox.Controls.Add(this.threshholdTrackBar);
            this.acquisitionResultGroupBox.Controls.Add(this.FFTChart);
            this.acquisitionResultGroupBox.Controls.Add(this.resultLabel);
            this.acquisitionResultGroupBox.Controls.Add(this.acquisitionDataGrid);
            this.acquisitionResultGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.acquisitionResultGroupBox.Location = new System.Drawing.Point(240, 8);
            this.acquisitionResultGroupBox.Name = "acquisitionResultGroupBox";
            this.acquisitionResultGroupBox.Size = new System.Drawing.Size(754, 574);
            this.acquisitionResultGroupBox.TabIndex = 4;
            this.acquisitionResultGroupBox.TabStop = false;
            this.acquisitionResultGroupBox.Text = "Acquisition Results";
            this.acquisitionResultGroupBox.Enter += new System.EventHandler(this.acquisitionResultGroupBox_Enter);
            // 
            // switchLabel
            // 
            this.switchLabel.AutoSize = true;
            this.switchLabel.Location = new System.Drawing.Point(700, 449);
            this.switchLabel.Name = "switchLabel";
            this.switchLabel.Size = new System.Drawing.Size(35, 13);
            this.switchLabel.TabIndex = 8;
            this.switchLabel.Text = "label1";
            this.switchLabel.Click += new System.EventHandler(this.switchLabel_Click);
            // 
            // switchBar
            // 
            this.switchBar.LargeChange = 1;
            this.switchBar.Location = new System.Drawing.Point(590, 435);
            this.switchBar.Maximum = 8;
            this.switchBar.Minimum = 1;
            this.switchBar.Name = "switchBar";
            this.switchBar.Size = new System.Drawing.Size(104, 45);
            this.switchBar.TabIndex = 7;
            this.switchBar.Value = 1;
            this.switchBar.Scroll += new System.EventHandler(this.switchBar_Scroll);
            // 
            // threshholdLabel
            // 
            this.threshholdLabel.AutoSize = true;
            this.threshholdLabel.Location = new System.Drawing.Point(147, 417);
            this.threshholdLabel.Name = "threshholdLabel";
            this.threshholdLabel.Size = new System.Drawing.Size(82, 13);
            this.threshholdLabel.TabIndex = 6;
            this.threshholdLabel.Text = "threshholdLabel";
            this.threshholdLabel.Click += new System.EventHandler(this.threshholdLabel_Click);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(508, 58);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(74, 17);
            this.checkBox1.TabIndex = 6;
            this.checkBox1.Text = "right Chart";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // threshholdTrackBar
            // 
            this.threshholdTrackBar.Location = new System.Drawing.Point(37, 417);
            this.threshholdTrackBar.Maximum = 200;
            this.threshholdTrackBar.Name = "threshholdTrackBar";
            this.threshholdTrackBar.Size = new System.Drawing.Size(104, 45);
            this.threshholdTrackBar.TabIndex = 5;
            this.threshholdTrackBar.Scroll += new System.EventHandler(this.threshholdTrackBar_Scroll);
            // 
            // FFTChart
            // 
            chartArea1.Name = "ChartArea1";
            this.FFTChart.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            this.FFTChart.Legends.Add(legend1);
            this.FFTChart.Location = new System.Drawing.Point(11, 126);
            this.FFTChart.Name = "FFTChart";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series1.Legend = "Legend1";
            series1.Name = "Frequency";
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series2.Legend = "Legend1";
            series2.Name = "Thresh";
            this.FFTChart.Series.Add(series1);
            this.FFTChart.Series.Add(series2);
            this.FFTChart.Size = new System.Drawing.Size(491, 261);
            this.FFTChart.TabIndex = 2;
            this.FFTChart.Text = "chart1";
            this.FFTChart.Click += new System.EventHandler(this.chart1_Click);
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
            this.acquisitionDataGrid.Location = new System.Drawing.Point(119, 0);
            this.acquisitionDataGrid.Name = "acquisitionDataGrid";
            this.acquisitionDataGrid.ParentRowsVisible = false;
            this.acquisitionDataGrid.ReadOnly = true;
            this.acquisitionDataGrid.Size = new System.Drawing.Size(185, 120);
            this.acquisitionDataGrid.TabIndex = 1;
            this.acquisitionDataGrid.TabStop = false;
            // 
            // outputChart
            // 
            chartArea2.CursorX.AutoScroll = false;
            chartArea2.Name = "ChartArea1";
            this.outputChart.ChartAreas.Add(chartArea2);
            legend2.Name = "Legend1";
            this.outputChart.Legends.Add(legend2);
            this.outputChart.Location = new System.Drawing.Point(748, 119);
            this.outputChart.Name = "outputChart";
            series3.ChartArea = "ChartArea1";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series3.Legend = "Legend1";
            series3.Name = "outputSeries-1";
            series4.ChartArea = "ChartArea1";
            series4.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series4.Legend = "Legend1";
            series4.Name = "outputSeries-2";
            series5.ChartArea = "ChartArea1";
            series5.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series5.Legend = "Legend1";
            series5.Name = "outputSeries-3";
            series6.ChartArea = "ChartArea1";
            series6.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series6.Legend = "Legend1";
            series6.Name = "outputSeries-4";
            series7.ChartArea = "ChartArea1";
            series7.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series7.Legend = "Legend1";
            series7.Name = "outputSeries-5";
            series7.YValuesPerPoint = 2;
            series8.ChartArea = "ChartArea1";
            series8.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series8.Legend = "Legend1";
            series8.Name = "outputSeries-6";
            series9.ChartArea = "ChartArea1";
            series9.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series9.Legend = "Legend1";
            series9.Name = "outputSeries-7";
            series10.ChartArea = "ChartArea1";
            series10.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series10.Legend = "Legend1";
            series10.Name = "outputSeries-8";
            this.outputChart.Series.Add(series3);
            this.outputChart.Series.Add(series4);
            this.outputChart.Series.Add(series5);
            this.outputChart.Series.Add(series6);
            this.outputChart.Series.Add(series7);
            this.outputChart.Series.Add(series8);
            this.outputChart.Series.Add(series9);
            this.outputChart.Series.Add(series10);
            this.outputChart.Size = new System.Drawing.Size(658, 300);
            this.outputChart.TabIndex = 3;
            this.outputChart.Text = "chart2";
            this.outputChart.Click += new System.EventHandler(this.chart2_Click);
            // 
            // main
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(1418, 594);
            this.Controls.Add(this.outputChart);
            this.Controls.Add(this.acquisitionResultGroupBox);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.timingParametersGroupBox);
            this.Controls.Add(this.channelParametersGroupBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Continuous Acquisition of Voltage Samples - Internal Clock";
            this.channelParametersGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.minimumValueNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maximumValueNumeric)).EndInit();
            this.timingParametersGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rateNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.samplesPerChannelNumeric)).EndInit();
            this.acquisitionResultGroupBox.ResumeLayout(false);
            this.acquisitionResultGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.switchBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.threshholdTrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.FFTChart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.acquisitionDataGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.outputChart)).EndInit();
            this.ResumeLayout(false);

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
            //Application.Run(new Form2());
           // Application.Run(new Form1());
            Application.Run(new main());
        }

        private void startButton_Click(object sender, System.EventArgs e)
        {
            if(runningTask == null)
            {
                try 
                {   
                    stopButton.Enabled = true;
                    startButton.Enabled = false;

                    // Open the serial port
                    port.Open();

                    // Create a new task
                    myTask = new Task();
  

                    // Create a virtual channel
                    myTask.AIChannels.CreateVoltageChannel(physicalChannelComboBox.Text, "",
                        (AITerminalConfiguration)(-1), Convert.ToDouble(minimumValueNumeric.Value),
                        Convert.ToDouble(maximumValueNumeric.Value), AIVoltageUnits.Volts);


                    // Configure the timing parameters
                    myTask.Timing.ConfigureSampleClock("", Convert.ToDouble(rateNumeric.Value),
                        SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, Convert.ToInt32(samplesPerChannelNumeric.Value) * 10);

 
                    // Configure the Every N Samples Event
                    myTask.EveryNSamplesReadEventInterval = Convert.ToInt32(samplesPerChannelNumeric.Value);
                    myTask.EveryNSamplesRead += new EveryNSamplesReadEventHandler(myTask_EveryNSamplesRead);


                    // Verify the Task
                    myTask.Control(TaskAction.Verify);


                    // Prepare the table for Data
                    //InitializeDataTable(myTask.AIChannels,ref dataTable); 
                    //acquisitionDataGrid.DataSource=dataTable;   
                    
                    runningTask = myTask;
                    analogInReader = new AnalogMultiChannelReader(myTask.Stream);
                    runningTask.SynchronizeCallbacks = true;
              
                    runningTask.Start();
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

        void myTask_EveryNSamplesRead(object sender, EveryNSamplesReadEventArgs e)
        {
            // Write the data to the serial port

            
            // Close the serial port
            //outputSeries = $"outputSeries-{switchNumber}";
            outputSeries = $"outputSeries-{hasel_count}";

  
            try
            {
           
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //EDITED SECTION:
                // Read the available data from the channels
                outputChart.Series[outputSeries].Points.Clear();
                

                //FFTChart.Series["Frequency"].Points.Clear();
                data = analogInReader.ReadWaveform(Convert.ToInt32(samplesPerChannelNumeric.Value));
                double[] samplesReal = data[0].GetRawData();

                int numSamples = samplesReal.Length;

                /*
                Complex[] samples = new Complex[numSamples];
                

                for (int i = 0; i < numSamples; i++)
                {
                    samples[i] = new Complex(samplesReal[i], 0);
                }

                Fourier.Forward(samples, FourierOptions.Default);

                //you only need the bottom half of the samples
                for (int i = samples.Length/4 +199; i <samples.Length; i++)
                {
            
                    //Get the magnitude of each FFT sample:
                    // = abs[sqrt(r^2 + i^2)]
                    mag = (2.0 / numSamples) * (Math.Abs(Math.Sqrt(Math.Pow(samples[i].Real, 2) + Math.Pow(samples[i].Imaginary, 2))));

                    //determine how many HZ represented by each sample
                    double sampleRate = 100000;
                    double hzperSample = sampleRate / numSamples;

                    if (checkBox1.Checked == false)
                    {
                        //FFTChart.Series["Frequency"].Points.AddXY(hzperSample * i, mag);
                        //FFTChart.Series["Thresh"].Points.AddY( threshhold);    
                    }


                    if (mag < threshhold)
                    {
                        samples[i] = 0.0;
                    }
                    else
                    {
                        samples[i] *= 2.0;
                    }
                    if (i > samples.Length / 2)
                    {
                        samples[i] = 0.0;
                    }

                }

                Fourier.Inverse(samples, FourierOptions.Default);

                */

                for (int i = numSamples-50; i < numSamples; i++)
                {
                    //samplesReal[i] = samples[i].Real;
                    batchRMS += Math.Pow(samplesReal[i], 2);
                }

                batchRMS = batchRMS / 50;
                batchRMS = Math.Sqrt(batchRMS);

                switch (hasel_count)
                {
                    case 1:
                        //find average of filtered signal
                        if (avgFFT1.Count < 200)
                        {
                            avgFFT1.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT1.RemoveAt(0);
                            avgFFT1.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT1.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT1.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT1[i]);
                        }

                        break;
                    case 2:
                        //find average of filtered signal
                        if (avgFFT2.Count < 200)
                        {
                            avgFFT2.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT2.RemoveAt(0);
                            avgFFT2.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT2.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT2.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT2[i]);
                        }

                        break;
                    case 3:
                        //find average of filtered signal
                        if (avgFFT3.Count < 200)
                        {
                            avgFFT3.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT3.RemoveAt(0);
                            avgFFT3.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT3.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT3.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT3[i]);
                        }

                        break;
                    case 4:
                        //find average of filtered signal
                        if (avgFFT4.Count < 200)
                        {
                            avgFFT4.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT4.RemoveAt(0);
                            avgFFT4.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT4.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT4.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT4[i]);
                        }

                        break;
                    case 5:
                        //find average of filtered signal
                        if (avgFFT5.Count < 200)
                        {
                            avgFFT5.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT5.RemoveAt(0);
                            avgFFT5.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT5.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT5.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT5[i]);
                        }

                        break;
                    case 6:
                        //find average of filtered signal
                        if (avgFFT6.Count < 200)
                        {
                            avgFFT6.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT6.RemoveAt(0);
                            avgFFT6.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT6.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT6.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT6[i]);
                        }

                        break;
                    case 7:
                        //find average of filtered signal
                        if (avgFFT7.Count < 200)
                        {
                            avgFFT7.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT7.RemoveAt(0);
                            avgFFT7.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT7.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT7.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT7[i]);
                        }

                        break;
                    case 8:
                        //find average of filtered signal
                        if (avgFFT8.Count < 200)
                        {
                            avgFFT8.Add(batchRMS);
                        }
                        else
                        {
                            avgFFT8.RemoveAt(0);
                            avgFFT8.Add(batchRMS);
                        }

                        for (int i = 0; i < avgFFT8.Count; i++)
                        {
                            double time = ((i + 1.0) / avgFFT8.Count) / 2;

                            outputChart.Series[outputSeries].Points.AddXY(time, avgFFT8[i]);
                        }

                        break;
                    default:
                        Console.WriteLine("Value is not 1, 2, ... or 8");
                        break;
                }

                //byte[] asciiBytes = Encoding.ASCII.GetBytes(switchNumber.ToString()); // Convert integer to ASCII bytes
                byte[] asciiBytes = Encoding.ASCII.GetBytes((hasel_count).ToString()); // Convert integer to ASCII bytes
                port.Write(asciiBytes, 0, asciiBytes.Length);

                /*
                string path = @"c:\tmp\Average.txt";

                using (var writer = new StreamWriter(path))
                {
                    values[HASEL_number-1] = filteredSamplesMaxFFT;
                    writer.Write(string.Join(",", values));
                }
                */
                
                string path = @"c:\tmp\Average.txt"
;
                using (var fileStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    Console.WriteLine(batchRMS);
                    byte[] info = new UTF8Encoding(true).GetBytes(batchRMS.ToString());
                    fileStream.Write(info, 0, info.Length);
                }
                

                //  string createText = filteredSamplesMaxFFT.ToString();
                //    File.WriteAllText(path, createText);

                // Create a file to write to.





                // Display results

                // create a series for each line


                //Series series1 = new Series("Group A");
                //    series1.Points.DataBindY(samplesReal);
                //    series1.ChartType = SeriesChartType.FastLine;

                //    // add each series to the chart
                //    chart1.Series.Clear();
                //    chart1.Series.Add(series1);

                //    // additional styling
                //    chart1.ResetAutoValues();
                //    chart1.Titles.Clear();
                //    chart1.Titles.Add($"Fast Line Plot ({100000:N0} points per series)");
                //    chart1.ChartAreas[0].AxisX.Title = "Horizontal Axis Label";
                //    chart1.ChartAreas[0].AxisY.Title = "Vertical Axis Label";
                //    chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
                //    chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.LightGray;

                // Save data file


                ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


                // Plot your data here
                // dataToDataTable(data, ref dataTable);
                hasel_count++;
                if (hasel_count > 8)
                {
                    hasel_count = 1;
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


        private void stopButton_Click(object sender, System.EventArgs e)
        {
            if(runningTask != null)
            {
                port.Close();
                runningTask.Stop();
                // Dispose of the task
                runningTask = null;
                myTask.Dispose();
                stopButton.Enabled = false;
                startButton.Enabled = true;
            }
        }
    

        //private void dataToDataTable(AnalogWaveform<double>[] sourceArray, ref DataTable dataTable)
        //{
        //    // Iterate over channels
        //    int currentLineIndex = 0;
        //    foreach (AnalogWaveform<double> waveform in sourceArray)
        //    {
        //        for (int sample = 0; sample < waveform.Samples.Count; ++sample)
        //        {
        //            if (sample == 10)
        //                break;

        //            dataTable.Rows[sample][currentLineIndex] = waveform.Samples[sample].Value;
        //        }
        //        currentLineIndex++;
        //    }
        //}

        //public void InitializeDataTable(AIChannelCollection channelCollection,ref DataTable data)
        //{
        //    int numOfChannels = channelCollection.Count;
        //    data.Rows.Clear();
        //    data.Columns.Clear();
        //    dataColumn = new DataColumn[numOfChannels];
        //    int numOfRows= 10;

        //    for(int currentChannelIndex = 0; currentChannelIndex < numOfChannels; currentChannelIndex++)
        //    {   
        //        dataColumn[currentChannelIndex] = new DataColumn();
        //        dataColumn[currentChannelIndex].DataType = typeof(double);
        //        dataColumn[currentChannelIndex].ColumnName = channelCollection[currentChannelIndex].PhysicalName;
        //    }

        //    data.Columns.AddRange(dataColumn); 

        //    for (int currentDataIndex = 0; currentDataIndex < numOfRows; currentDataIndex++)             
        //    {
        //        object[] rowArr = new object[numOfChannels];
        //        data.Rows.Add(rowArr);              
        //    }
        //}

        private void physicalChannelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void acquisitionResultGroupBox_Enter(object sender, EventArgs e)
        {

        }

        private void chart2_Click(object sender, EventArgs e)
        {

        }

        private void threshholdTrackBar_Scroll(object sender, EventArgs e)
        {
            double barValue = threshholdTrackBar.Value / 1000.0;
            threshholdLabel.Text = threshholdTrackBar.Value.ToString("F0");
            threshhold = barValue;
        }

        private void threshholdLabel_Click(object sender, EventArgs e)
        {
             
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void switchBar_Scroll(object sender, EventArgs e)
        {
            int switchValue = switchBar.Value;
            switchLabel.Text = switchBar.Value.ToString("F0");
            switchNumber = switchValue;
        }

        private void switchLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
