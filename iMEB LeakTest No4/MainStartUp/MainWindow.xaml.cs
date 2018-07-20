using NationalInstruments.Controls;
using NationalInstruments.Analysis.Math;
using NationalInstruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MahApps.Metro.Controls;

using Notifications.Wpf;                        // Notify Message

using System.ComponentModel;
using System.Windows.Threading;

using iMEB_LeakTest_No4.KMTSLIBS;
using iMEB_LeakTest_No4.Config;
using iMEB_LeakTest_No4.SubScreen;

using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;


using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;
using TLog;
using TLog.Properties;

using System.Windows.Data;

namespace iMEB_LeakTest_No4
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {

        public static LeakTest     SystemLT                                 = new LeakTest();
        private System.Windows.Threading.DispatcherTimer   UITimer          = new System.Windows.Threading.DispatcherTimer();
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private DispatcherTimer    MainControlTimer = new DispatcherTimer();                         // 메인 자동 운전관련 제어 타이머
        private MetroWindow        ThisSubForm;                                                      // 시스템 환경 설정용 화면 연결용
        public Window              RefMetroMainWindow;                                               // 다른 서브 폼에서 메인 윈도우 참조시 핸들 정보 저장용

        // 로그 표시관련
        private readonly IEnumerable<EncodingInfo>                  _encodings    = Encoding.GetEncodings().OrderBy(e => e.DisplayName);
        private readonly ObservableCollection<FileMonitorViewModel> _fileMonitors = new ObservableCollection<FileMonitorViewModel>();
        private System.Threading.Timer _refreshTimer;
        private string                 _font;
        private DateTime?              _lastUpdateDateTime;
        private string                 _lastUpdated;
        private FileMonitorViewModel   _lastUpdatedViewModel;
        private FileMonitorViewModel   _selectedItem;

        private string CurLogFilePath = "";                    // 현재 표시되고 있는 로그 파일의 경로 정보를 저장

        // 외부 UI 델리게이트 처리
        
        // 내부 클래스에서 메인 화면 업데이트용 델리게이트 설정.

        // 컬러 변수
        private Brush _StripBar_BackColor;
        private Brush _Lamp_BackColor;


        // 통신데이터 실시간 그래프 표시관련
        const int MAX_COSMO_RECEIVE_DATA_COUNTS = 500;   // 초당 10개씩 받을수 있으므로 최대 50초의 데이터를 표시 할수 있도록
        const int MAX_DAQ_DATA_COUNTS           = 500000; // 초당 1000(1Khz)로 60초데이터 분량
        private ChartCollection<double, double>[] GraphData  = new[] {new ChartCollection<double,double>(MAX_COSMO_RECEIVE_DATA_COUNTS),   // plot : 테스트 압력 표시
                                                                      new ChartCollection<double,double>(MAX_COSMO_RECEIVE_DATA_COUNTS)    // plot : 리크량 표시 
                                                                     };
        private ChartCollection<double, double>[] GraphData1 = new[] {new ChartCollection<double,double>(MAX_COSMO_RECEIVE_DATA_COUNTS),   // plot : 테스트 압력 표시
                                                                      new ChartCollection<double,double>(MAX_COSMO_RECEIVE_DATA_COUNTS)    // plot : 리크량 표시 
                                                                     };
        private ChartCollection<double, double>[] GraphData2 = new[] {new ChartCollection<double,double>(MAX_COSMO_RECEIVE_DATA_COUNTS),   // plot : 테스트 압력 표시
                                                                      new ChartCollection<double,double>(MAX_COSMO_RECEIVE_DATA_COUNTS)    // plot : 리크량 표시 
                                                                     };
        private ChartCollection<double, double>[] GraphData3 = new[] {new ChartCollection<double,double>(MAX_DAQ_DATA_COUNTS),             // plot : 모터 전류량
                                                                      new ChartCollection<double,double>(MAX_DAQ_DATA_COUNTS),             // plot : 모터 위치  
                                                                      new ChartCollection<double,double>(MAX_DAQ_DATA_COUNTS),             // plot : MC12 진공량 
                                                                      new ChartCollection<double,double>(MAX_DAQ_DATA_COUNTS)              // plot : MC34 진공량
                                                                     };



        // 메인 자동운전 관련 쓰레드 및 기타 관련 변수
        private Thread AutomaticThread = null;
        private Thread MBCReadThread = null;



        private bool _NGDisplay = false; // NG시발생시 메세지 표시 팝업이 표시되었는지 여부
        private void NGShow(string msg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                string curMsg = NGMessage1.Text;
                NGMessage1.Text = curMsg + "\r\n" + msg;
                
                NGPopup.IsOpen = true;
                _NGDisplay = true;
            }));
        }
        private void NGHide()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                NGPopup.IsOpen = false;
                NGMessage1.Text = "";
                _NGDisplay = false;
            }));
        }


        // KS : OK 발생시 메시지 표시 팝업이 표시되는었는지 여부
        private bool _OKDisplay = false;
        private void OKShow(string msg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                string curMsg = OKMessage1.Text;
                OKMessage1.Text = curMsg + "\r\n" + msg;
                
                OKPopup.IsOpen = true;
                _OKDisplay = true;
            }));
        }
        private void OKHide()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                OKPopup.IsOpen = false;
                OKMessage1.Text = "";
                _OKDisplay = false;
            }));
        }




        /// <summary>
        /// iMEB LEAK TEST APPLICATION 메인진입부
        /// </summary>
        public MainWindow()
        {
            Process CurrentProcess = Process.GetCurrentProcess();

            foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
            {
                if (p.Id != CurrentProcess.Id)
                {
                    if (p.ProcessName == "iMEB LeakTest No4")
                    {
                        p.Kill();
                    }
                }
            }




            InitializeComponent();
            this.DataContext = this;
            // 메인화면에 사용되는 이미지 맵핑

            // 전역 델리게이트 중비
            SystemLT.M_Cosmo.MainUIUpdate_ComState += new SerialComState_UI_Refresh_Delegate(StripState_COM);
            SystemLT.M_Cosmo.MainGraphUpdate_Cosmo += new SerialComState_GRAPH_Refresh_Delegate(CosmoGraphRefresh);
            SystemLT.CosmoGraph1CallBack           += new CosmoGraph1Delegate(dele_Cosmo1Graph);                             // 고진공(700mmHg이상) 리크 시험 결과 판단
            SystemLT.CosmoGraph2CallBack           += new CosmoGraph2Delegate(dele_Cosmo2Graph);                             // 1.5바 리크 시험 결과 판단
            SystemLT.CosmoGraph3CallBack           += new CosmoGraph3Delegate(dele_Cosmo3Graph);                             // 5.0바 리크 시험 결곽 판단
            SystemLT.EcuDTC1CallBack               += new EcuDTC1Delegate(dele_EcuDTC1);                                     // DTC 시험 관련 결과 표시용
            SystemLT.EcuDTC2CallBack               += new EcuDTC2Delegate(dele_EcuDTC2);                                     // DTC 시험 결관 판단
            SystemLT.EcuMotorGraph1CallBack        += new EcuMotorGraph1Delegate(dele_EcuMotor1Graph);                       // ECU LEAK TEST 시험 결과 판단
            SystemLT.MainUIUpdate1                 += new MainUpdate_Delegate(MainUIUpdate_CurAutoModeStep);                 // 메인 화면에 현재 진행되는 시험 스텝 표시용
            SystemLT.AutomodeClear                 += new Automode_Clear_Delegate(Automode_Clear);                           // 자동운전 시작시 화면 정리용
            SystemLT.ErrorNotifyCallBack           += new ErrorNotifyDelegate(ErrorNotify);                                  // 시험 중간 에러 표시용 노티파이 화면
            SystemLT.MBCCallBack                   += new MBC_Delegate(dele_MBCDataView);                                    // ECU LEAK TEST시 모터 정보(전류/위치) 표시용
            SystemLT.LDS_StartCallBack             += new LDS_StartDelegate(dele_LDSStart);                                  // LDS(바코드 정보 읽기) 작업시 시작관련 처리용
            SystemLT.LDS_StopCallBack              += new LDS_StopDelegate(dele_LDSStop);                                    // LDS작업시 종료 결과 전송용
            SystemLT.EcuLeakTest2CallBack          += new EcuLeakTest2Delegate(dele_EcuLeakTest2);                           // 모터 전류 표시용

            SystemLT.NGMsg_CallBack                += new NGMessage(NGShow);
            SystemLT.NGMsgHide_CallBack            += new NGMessageHide(NGHide);
            SystemLT.TotalResult_CallBack          += new TotalResult(dele_TotalResult);
            SystemLT.OKMsg_CallBack                += new OKMessage(OKShow);
            SystemLT.OKMsgHide_CallBack            += new OKMessageHide(OKHide);

            NGShow("최초 실행시 표시됩니다. 자동실행 혹은 수모모드 전환시 자동으로 사라집니다.");
            // Log 표시 관련 초기화 
            _refreshTimer  = CreateRefreshTimer();
            CurLogFilePath = System.Environment.CurrentDirectory + "\\Logs\\" + System.DateTime.Now.ToShortDateString() + ".log";
            if (!File.Exists(CurLogFilePath))
            {  
                // System Init
                LT_Init();
            }
            var monitorViewModel      = new FileMonitorViewModel(CurLogFilePath, GetFileNameForPath(CurLogFilePath), "UTF-8", false);
            monitorViewModel.Renamed += MonitorViewModelOnRenamed;
            monitorViewModel.Updated += MonitorViewModelOnUpdated;
            FileMonitors.Add(monitorViewModel);
            SelectedItem              = monitorViewModel;            
            // System Init
            LT_Init();
            // 메인 윈도우 핸들 저장
            RefMetroMainWindow        = MetroWindow.GetWindow(this);

            // 시험 결과 그리그 설정
            this.TestResultGrid.Loaded += SetMinWidths;
            SystemLT.CurTestResult.Clear();           

            this.TestResultGrid.ItemsSource = SystemLT.CurTestResult;
            this.TestResultGrid.Items.Refresh();

            SystemLT.CurDTCResult.Clear();

            this.EcuDtcGrid.ItemsSource = SystemLT.CurDTCResult;
            this.EcuDtcGrid.Items.Refresh();

            // 설정 배경색 및 전경색 기억용
            _StripBar_BackColor = this.SB_Main.Background;
            _Lamp_BackColor     = this.Test_Lamp_0.Background;

            // 그래프 데이터 소스 맵핑
            this.ExtLeakGraph.DataSource = GraphData;
            GraphData[0].Clear();
            GraphData[1].Clear();
            this.ExtLeakGraph.Refresh();

            this.IntLeakGraph.DataSource = GraphData1;
            GraphData1[0].Clear();
            GraphData1[1].Clear();
            this.IntLeakGraph.Refresh();

            this.IntLeakGraph5.DataSource = GraphData2;
            GraphData2[0].Clear();
            GraphData2[1].Clear();
            this.IntLeakGraph5.Refresh();

            this.EcuMotorGraph.DataSource = GraphData3;
            GraphData3[0].Clear();
            GraphData3[1].Clear();
            GraphData3[2].Clear();
            GraphData3[3].Clear();
            this.EcuMotorGraph.Refresh();

            //MBCData[0].Clear();
            PolarGraph1.DataSource = MBCData1;
            PolarGraph1.Refresh();


            SystemLT.DTC_ReadInfo.HWVersion         = "";
            SystemLT.DTC_ReadInfo.SWVersion         = "";
            SystemLT.DTC_ReadInfo.PartNumber        = "";
            SystemLT.DTC_ReadInfo.Model             = "";
            SystemLT.DTC_ReadInfo.MOBIS_HSWVersion  = "";
            SystemLT.DTC_ReadInfo.ECUReset          = false;
  


        }
       
        /// <summary>
        /// ECU LEAK TEST2
        /// 전류 평가관련 표시 및 결과
        /// </summary>
        private void dele_EcuLeakTest2()
        {
            string filename = _EcuLeakTestFileName;  
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (filename.Length <= 0)
                {
                    Test_Analay1.Background = Brushes.Red;
                }
                else
                {
                    masterLength1         = this.A2_RPM_mm.Value;
                    masterStartPos        = this.A2_Start_mm.Value;
                    masterEndPos          = this.A2_End_mm.Value;
                    masterMovingAvg       = (int)this.A2_MV_counts.Value;
                    MainTab.SelectedIndex = 8;
                    SystemLT.Log.Info("[전류분석화일이름=" + filename);
                    try
                    {
                        MBC_FileToChart1(filename);
                    }
                    catch (Exception e1) { Test_Analay1.Background = Brushes.Red; }
                }
            }));
           
        }

        /// <summary>
        /// 시험 시작시 LDS 정보저장
        /// 작업일시, 바코드, 제품코드등을 기록
        /// </summary>
        private void dele_LDSStart()
        {
            SystemLT.CurMesSpec.LocalVar.StartDate = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
            SystemLT.CurMesSpec.LocalVar.BarCode   = SystemLT.CurMesSpec.SPEC.ImsiBarCode;
            SystemLT.CurMesSpec.LocalVar.ModelCode = SystemLT.CurMesSpec.SPEC.LastProductCode;            
        }
        /// <summary>
        /// 시험종료 결과를 LDS 서버에 전송
        /// 네트워크 에러시 로컬에 저장
        /// </summary>
        /// <param name="result"></param>
        /// <param name="resultcode"></param>
        private void dele_LDSStop(bool result,int resultcode)
        {
            if (_IsBarCodeRead != true)
            {
                // 바코드 읽기 모드가 없을 경우 바이패스
                return;
            }

            string resultMsg = "|";
            try
            {   // 실제 시험 순서와 확인요함.
                //resultMsg = "|" + "Vac_Main," + SystemLT.CurTestResult[0].Measurement;
                
                
                resultMsg =  "Vac_Main," + SystemLT.CurTestResult[0].Measurement;

                resultMsg = resultMsg + "|" + "Leak_1.5bar,"     + SystemLT.CurTestResult[1].Measurement;
                resultMsg = resultMsg + "|" + "Leak_5.0bar,"     + SystemLT.CurTestResult[2].Measurement;

                resultMsg = resultMsg + "|" + "FWD_Leak,"        + SystemLT.CurTestResult[3].Measurement;
                resultMsg = resultMsg + "|" + "FWD_Peak,"        + SystemLT.CurTestResult[5].Measurement;
                resultMsg = resultMsg + "|" + "FWD_Avg,"         + SystemLT.CurTestResult[7].Measurement;
                resultMsg = resultMsg + "|" + "FWD_Diva,"        + SystemLT.CurTestResult[9].Measurement;

                resultMsg = resultMsg + "|" + "BWD_Leak,"        + SystemLT.CurTestResult[4].Measurement;
                resultMsg = resultMsg + "|" + "BWD_Peak,"        + SystemLT.CurTestResult[6].Measurement;
                resultMsg = resultMsg + "|" + "BWD_Avg,"         + SystemLT.CurTestResult[8].Measurement;
                resultMsg = resultMsg + "|" + "BWD_Diva,"        + SystemLT.CurTestResult[10].Measurement;

                resultMsg = resultMsg + "|" + "Motor_C,"         + SystemLT.CurTestResult[11].Measurement;

                resultMsg = resultMsg + "|" + "ECU_MODEL,"       + SystemLT.CurTestResult[14].Message;
                resultMsg = resultMsg + "|" + "ECU_Parts_No,"    + SystemLT.CurTestResult[15].Message;
                resultMsg = resultMsg + "|" + "OEM_HW_VER,"      + SystemLT.CurTestResult[16].Message;
                resultMsg = resultMsg + "|" + "OEM_SW_VER,"      + SystemLT.CurTestResult[17].Message;
                resultMsg = resultMsg + "|" + "MS_SW_HW_VER,"    + SystemLT.CurTestResult[18].Message;
                resultMsg = resultMsg + "|" + "ECU_Error_RESET," + SystemLT.CurTestResult[19].Message;

                resultMsg = resultMsg + "|" + "CMOT1,0.0" + "|"; ; // 현재 시험 없음.SystemLT.CurTestResult[18].Measurement;
            }
            catch (Exception)
            {
                resultMsg = "|PC ERROR - 시험항목 수가 틀립니다.";
                SystemLT.Log.Error("[LDSStop] LDS 서버에 시험결과를 업데이트시 시험항목 수가 틀립니다.(시험모드를 바이패스할 경우)");
            }

            //string testresultmsg = "|Leak_15,0.5|Leak_500mmHg,0.45|Motor_Leak_FWD_Avg,1.234|";// 실제 세부 시험 결과 내용 임시적용
            SystemLT.CurMesSpec.LocalVar.EndDate = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
            if (result)
            {                
                bool totalChk = true;
                foreach (iMEB.TestResultTable sel in SystemLT.CurTestResult)
                {
                    string resultStr = sel.Result;
                    int cc = resultStr.CompareTo("OK");
                    int c1 = resultStr.CompareTo("Pass");
                    int kk = cc;
                    if (cc != 0)
                    {
                        if (c1!=0) totalChk = totalChk & false;
                    }
                }
                if (totalChk)   SystemLT.CurMesSpec.LocalVar.TestResult = "01POK";
                else SystemLT.CurMesSpec.LocalVar.TestResult = "01PNG";
            }
            else SystemLT.CurMesSpec.LocalVar.TestResult = "01PNG";

            SystemLT.CurMesSpec.LocalVar.TestResult = SystemLT.CurMesSpec.LocalVar.TestResult + resultMsg;

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                string retMsg   = "";
                int    retCode  = 0;
                string retMsg1  = "";
                int    retCode1 = 0;
                // 시험 결과 전공
                bool chk = SystemLT.M_MesLds.LDS_Result(
                                                        SystemLT.CurMesSpec.LocalVar.BarCode,
                                                        SystemLT.CurMesSpec.LocalVar.StartDate,
                                                        SystemLT.CurMesSpec.LocalVar.EndDate,
                                                        SystemLT.CurMesSpec.LocalVar.TestResult,
                                                        ref retMsg,
                                                        ref retCode
                                                        );
                if (retCode!=91)
                {
                    // 에러 처리....
                }
                // 설비 상태 읽기
                string readPLCStatus = "";
                string LDSPLCStatus  = "";
                bool chk3 = SystemLT.M_Plc.CMD_Read_SystemStatus(0.5, ref readPLCStatus);
                SystemLT.Log.Info("[MES] PLC -> PC 설비 상태 읽기 = " + readPLCStatus + ", Function = " + chk3.ToString());
                if ((chk3)&(readPLCStatus.Length!=0)) LDSPLCStatus = readPLCStatus;
                else      LDSPLCStatus = "0";
                // 설비 상태 전송
                bool chk1 = SystemLT.M_MesLds.LDS_Status(
                                                        SystemLT.CurMesSpec.LocalVar.BarCode,
                                                        SystemLT.CurMesSpec.LocalVar.StartDate,
                                                        SystemLT.CurMesSpec.LocalVar.EndDate,
                                                        LDSPLCStatus,
                                                        ref retMsg1,
                                                        ref retCode1
                                                        );
            }));
            _IsBarCodeRead = false;
        }




        private void dele_MBCDataView(double pos,double amp,bool readchk,long stopwatchtime)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                this.MBC_Pos.Content           = string.Format("POS: {0:F2}", pos);
                this.Pb_Motor.Value            = (pos != 0.0) ? (Math.Abs(pos) / 33.0 * 100.0) : 0.0;
                this.MBC_Amp.Content           = string.Format("AMP: {0:F2}", amp);
                this.MBC_ReadCheck.Content     = (readchk) ? "OK" : "Fails";
                this.MBC_StopWatchTime.Content = string.Format("t: {0:D}", stopwatchtime);
            }));
        }
        public enum CurTestMode:int
        {
            ExtLeakTest       = 1,
            IntLeakTest15     = 2,
            IntLeakTest50     = 3,
            LeakMotor         = 4,
            DTC               = 5,
            ExtLeakTestPASS   = 11,
            IntLeakTest15PASS = 21,
            IntLeakTest50PASS = 31,
            LeakMotorPASS     = 41,
            DTCPASS           = 51

        }
        private bool dele_TotalResult(CurTestMode mode)
        {
            bool OkNg = false;
            string chkStr = "";
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {

                switch (mode)
                {
                    case CurTestMode.ExtLeakTest:
                        chkStr = (string)LTV_Result.Content;
                        if (string.Compare(chkStr, "OK") == 0) OkNg = true;
                        break;
                    case CurTestMode.IntLeakTest15:
                        chkStr = (string)LT15_Result.Content;
                        if (string.Compare(chkStr, "OK") == 0) OkNg = true;
                        break;
                    case CurTestMode.IntLeakTest50:
                        chkStr = (string)LT50_Result.Content;
                        if (string.Compare(chkStr, "OK") == 0) OkNg = true;
                        break;
                    case CurTestMode.LeakMotor:
                        chkStr = (string)LTM_Result.Content;
                        if (string.Compare(chkStr, "OK") == 0) OkNg = true;
                        break;
                    case CurTestMode.DTC:
                        chkStr = (string)DTCResultMsg.Content;
                        if (string.Compare(chkStr, "OK") == 0) OkNg = true;
                        break;
                    case CurTestMode.IntLeakTest15PASS:
                        LT15_Result.Content = "Pass";
                        OkNg = true;
                        break;
                }
            }));
            return OkNg;
        }
        private void dele_Cosmo1Graph()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                double _TimeIndex       = 0.0;
                int    avgCount         = 0;
                double avgLV1           = 0.0;
                double LastTestPressure = 0.0;

                double TP_Range_Min = 750.0;
                double TP_Range_Max = 0.0;
                // 그리드 출력용 기본 변수들
                string TestName     = "고진공 리크 시험";
                double Low          = SystemLT.CurMesSpec.SPEC.InternalLeak_LeakMin;   // SystemLT.CurExternalLeakTest.Check_Low;
                double High         = SystemLT.CurMesSpec.SPEC.InternalLeak_LeakMax;   // SystemLT.CurExternalLeakTest.Check_High;
                double Measurement  = avgLV1;
                string Unit         = "mmHg";
                string ResultMsg    = "OK";
                string Description  = "";

                int GetDataCount = SystemLT.M_Cosmo.Cosmo1DataIndex;
                if (GetDataCount <= 0)
                {
                    SystemLT.Log.Info("[고진공 리크 시험] 코스모 장치에서 에러(대리크...)발생으로 데이터 없음" );
                    ResultMsg   = "NG";
                    Description = "코스모 장치에서 에러 발생(대리크 혹은 LLNG),판별할수 있는 데이터를 수신하지 못했습니다.";
                    SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                    return;
                }



                GraphData[0].Clear();
                GraphData[1].Clear();
                #region 데이터 저장
                string FilePath         = SystemLT.IMSI_DataFullPath + SystemLT.CurMesSpec.SPEC.ImsiBarCode + "_High_Vacuum_LeakTest.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);

                outputfile.WriteLine("(코스모 에어리크 테스터 장비에서 통신으로 읽음)데이터 샘플링 속도 : 10hz\n");
                outputfile.WriteLine("TestName,TestPressure(mmHg),Leak Value(mmHg)\n");
                double _CheckDETFirstTime = 0.0;
                bool _CheckDERFirst = false;
                for (int i = 0; i < GetDataCount; i++)
                {
                    string sFmt = "";
                    sFmt = string.Format("{0},{1:F3},{2:F3}",
                                        SystemLT.M_Cosmo.Cosmo1Data[i].TN,
                                        Math.Abs(SystemLT.M_Cosmo.Cosmo1Data[i].TP),
                                        SystemLT.M_Cosmo.Cosmo1Data[i].LV
                                        );
                    outputfile.WriteLine(sFmt + "\n");
                    // TP Range 확인용
                    if (Math.Abs(SystemLT.M_Cosmo.Cosmo1Data[i].TP) > TP_Range_Max) TP_Range_Max = Math.Abs( SystemLT.M_Cosmo.Cosmo1Data[i].TP);
                    if (Math.Abs(SystemLT.M_Cosmo.Cosmo1Data[i].TP) < TP_Range_Min) TP_Range_Min = Math.Abs( SystemLT.M_Cosmo.Cosmo1Data[i].TP);
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TN.Replace(" ","") == "DET")
                    {
                        if (_CheckDERFirst==false)
                        {
                            _CheckDETFirstTime = i / 10.0;
                            _CheckDERFirst = true;
                        }
                        avgLV1           = SystemLT.M_Cosmo.Cosmo1Data[i].LV;
                        LastTestPressure = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                        avgCount++;
                    }
                    GraphData[0].Append(_TimeIndex, Math.Abs(SystemLT.M_Cosmo.Cosmo1Data[i].TP));
                    GraphData[1].Append(_TimeIndex, SystemLT.M_Cosmo.Cosmo1Data[i].LV);
                    _TimeIndex = _TimeIndex + 0.1; // 통신 데이터는 10Hz임.
                }
                outputfile.Close();
                #endregion

                Measurement = avgLV1;

                #region 코스모 장치 에러 확인
                bool CosmoALT = false;  // COSMO AIR LEAK TESTER = 하나만 존재
                string CosmosErrName = "";
                bool chk = SystemLT.M_Plc.PLC_CosmoErrorCheck(2.0, ref CosmoALT, ref CosmosErrName);
                if (chk)
                {
                    if (CosmoALT)
                    {
                        Description            = Description + " 코스모 에러="+CosmosErrName;
                        ResultMsg              = "NG";
                        Test_Lamp_0.Background = Brushes.Red;
                    }
                }
                else
                {
                    Description            = Description + " 코스모 에러 체크 통신안됨";
                    ResultMsg              = "NG";
                    Test_Lamp_0.Background = Brushes.Red;
                    CosmoALT = true;
                }
                #endregion

                // 그래프 표시
                this.Ext_TestPressure.Range = new Range<double>(TP_Range_Min*1.05, TP_Range_Max*1.05);
                this.ExtLeakGraph.DataSource = GraphData;
                this.ExtLeakGraph.Refresh();

                if ((Measurement > Low) && (Measurement < High) && (avgCount>0) && (!CosmoALT))
                {
                    ResultMsg = "OK";
                    Test_Lamp_0.Background = Brushes.Green;
       
                }
                else
                {
                    ResultMsg = "NG";
                    Test_Lamp_0.Background = Brushes.Red;
                }
                // 텍스트 결과 표시

                LTV_TestPressure.Content = string.Format("{0:F1}", Math.Abs(LastTestPressure));
                LTV_LeakMin.Content      = string.Format("{0:F2}", Low);
                LTV_LeakMax.Content      = string.Format("{0:F2}", High);               
                LTV_Leak.Content         = string.Format("{0:F3}", Measurement);
                LTV_TestTime.Content     = string.Format("{0:F2}", SystemLT._SubTestTimer.ResultTime(LeakTest.TestTime.TESTNAME.LEAKTEST_HIGH_VACUUM));

                if (Test_Lamp_0.Background == Brushes.Green)
                {
                    LTV_Result.Foreground = Brushes.Blue;
                }
                else
                {
                    LTV_Result.Foreground = Brushes.Red;
                }
                LTV_Result.Content = ResultMsg;

                // Log 
                SystemLT.Log.Info("[고진공 리크 시험]"+ResultMsg);
                SystemLT.Log.Info("[SPEC] Low " + string.Format("{0:F2}", Low));
                SystemLT.Log.Info("[SPEC] High " + string.Format("{0:F2}", High));
                SystemLT.Log.Info("[측정값] "+string.Format("{0:F1}", Measurement)+"mmHg");
                SystemLT.Log.Info("[비고] "+Description);
                //
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                this.TestResultGrid.Items.Refresh();
                EXT_Leak.AxisValue = _CheckDETFirstTime;
                MainTab.SelectedIndex = 0;
                // 
            }));
            // 결과 판단


        }
        private void dele_Cosmo2Graph(bool tf)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (tf == false)
                {
                    LT15_TestPressure.Content = "-";
                    LT15_LeakMin.Content = "-";
                    LT15_LeakMax.Content = "-";
                    LT15_Leak.Content = "-";
                    LT15_TestTime.Content = "-";

                    // Log 
                    SystemLT.Log.Info("[공압 리크 시험(1.5바)] - PASS" );

                    LT15_Result.Content = "Pass";


                    SystemLT.CurTestResult.Add(new iMEB.TestResultTable("1.5바 리크시험", 0.0, 0.0, 0.0, "bar", "Pass", ""));
                    this.TestResultGrid.Items.Refresh();
                    INT_Leak15.AxisValue = 0.0;
                    MainTab.SelectedIndex = 1;
                    this.IntLeakGraph.Refresh();
                    return;
                }
                int GetDataCount = SystemLT.M_Cosmo.Cosmo1DataIndex;

                double _TimeIndex       = 0.0;
                int    avgCount         = 0;
                double avgLV1           = 0.0;
                double TP_Range_Min     = 1.5;
                double TP_Range_Max     = 0.0;
                double LastTestPressure = 0.0;

                string TestName    = "공압 리크 시험(1.5바)";
                double Low         = SystemLT.CurMesSpec.SPEC.ExternalLeak_15LeakMin;// 0.0; //SystemLT.CurInternalLeakTest.;
                double High        = SystemLT.CurMesSpec.SPEC.ExternalLeak_15LeakMax;//10.0; // SystemLT.CurInternalLeakTest.Check_High;
                double Measurement = avgLV1;
                string Unit        = "mbar";
                string ResultMsg   = "OK";
                string Description = "";

                if (GetDataCount <= 0)
                {
                    SystemLT.Log.Info("[1.5바 리크 시험] 코스모 장치에서 에러(대리크...)발생으로 데이터 없음");
                    ResultMsg   = "NG";
                    Description = "코스모 장치에서 에러 발생(대리크 혹은 LLNG),판별할수 있는 데이터를 수신하지 못했습니다.";
                    SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                    return;
                }
                GraphData1[0].Clear();
                GraphData1[1].Clear();

                #region 데이터 저장
                string FilePath = SystemLT.IMSI_DataFullPath + SystemLT.CurMesSpec.SPEC.ImsiBarCode+ "_AIR_LeakTest15.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);

                outputfile.WriteLine("(코스모 에어리크 테스터 장비에서 통신으로 읽음)데이터 샘플링 속도 : 10hz\n");
                outputfile.WriteLine("TestName,TestPressure(bar),Leak Value(mbar)\n");
                double _CheckDETFirstTime = 0.0;
                bool _CheckDERFirst = false;
                for (int i = 0; i < GetDataCount; i++)
                {
                    string sFmt = "";
                    sFmt = string.Format("{0},{1:F3},{2:F3}",
                                        SystemLT.M_Cosmo.Cosmo1Data[i].TN,
                                        SystemLT.M_Cosmo.Cosmo1Data[i].TP,
                                        SystemLT.M_Cosmo.Cosmo1Data[i].LV*0.01
                                        );
                    outputfile.WriteLine(sFmt + "\n");
                    // TP Range 확인용
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TP > TP_Range_Max) TP_Range_Max = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TP < TP_Range_Min) TP_Range_Min = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TN.Replace(" ", "") == "DET")
                    {
                        if (_CheckDERFirst == false)
                        {
                            _CheckDETFirstTime = i / 10.0;
                            _CheckDERFirst = true;
                        }
                        avgLV1 = SystemLT.M_Cosmo.Cosmo1Data[i].LV*0.01;  // pa -> mbar
                        avgCount++;
                        LastTestPressure = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                    }
                    GraphData1[0].Append(_TimeIndex, SystemLT.M_Cosmo.Cosmo1Data[i].TP);
                    GraphData1[1].Append(_TimeIndex, SystemLT.M_Cosmo.Cosmo1Data[i].LV*0.01);
                    _TimeIndex = _TimeIndex + 0.1; // 통신 데이터는 10Hz임.
                }
                outputfile.Close();
                #endregion
                // 그래프 표시
                //this.In_TestPressure.Range = new Range<double>(0.0, TP_Range_Max*1.05);
                this.IntLeakGraph.DataSource = GraphData1;
                this.IntLeakGraph.Refresh();

                Measurement = avgLV1;

                // Log 
                SystemLT.Log.Info("[공압 리크 시험(1.5바)]" + ResultMsg);
                SystemLT.Log.Info("[SPEC] Low " + string.Format("{0:F2}", Low));
                SystemLT.Log.Info("[SPEC] High " + string.Format("{0:F2}", High));
                SystemLT.Log.Info("[측정값] " + string.Format("{0:F1}", Measurement) + "mbar");
                SystemLT.Log.Info("[비고] " + Description);

                #region 코스모 장치 에러 확인
                bool   CosmoALT      = false;  // COSMO AIR LEAK TESTER = 하나만 존재
                string CosmosErrName = "";
                bool chk = SystemLT.M_Plc.PLC_CosmoErrorCheck(2.0, ref CosmoALT, ref CosmosErrName);
                if (chk)
                {
                    if (CosmoALT)
                    {
                        Description = Description + " 코스모 에러=" + CosmosErrName;
                        ResultMsg = "NG";
                        Test_Lamp_1.Background = Brushes.Red;
                    }
                }
                else
                {
                    Description = Description + " 코스모 에러 체크 통신안됨";
                    ResultMsg = "NG";
                    Test_Lamp_1.Background = Brushes.Red;
                    CosmoALT = true;
                }
                #endregion
                if ((Measurement > Low) && (Measurement < High) && (avgCount > 0) && (!CosmoALT))
                {
                    ResultMsg = "OK";
                    Test_Lamp_1.Background = Brushes.Green;
                }
                else
                {
                    ResultMsg = "NG";
                    Test_Lamp_1.Background = Brushes.Red;
                }
                LT15_TestPressure.Content = string.Format("{0:F1}", Math.Abs(LastTestPressure));
                LT15_LeakMin.Content      = string.Format("{0:F2}", Low);
                LT15_LeakMax.Content      = string.Format("{0:F2}", High);
                LT15_Leak.Content         = string.Format("{0:F3}", Measurement);
                LT15_TestTime.Content     = string.Format("{0:F2}", SystemLT._SubTestTimer.ResultTime(LeakTest.TestTime.TESTNAME.LEAKTEST_AIR_15));

                if (Test_Lamp_1.Background == Brushes.Green)
                {
                    LT15_Result.Foreground = Brushes.Blue;
                }
                else
                {
                    LT15_Result.Foreground = Brushes.Red;
                }
                LT15_Result.Content = ResultMsg;


                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                this.TestResultGrid.Items.Refresh();
                INT_Leak15.AxisValue = _CheckDETFirstTime;
                MainTab.SelectedIndex = 1;
               
                this.IntLeakGraph.Refresh();

                // 
            }));
            // 결과 판단


        }
        private void dele_Cosmo3Graph()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {

                int GetDataCount = SystemLT.M_Cosmo.Cosmo1DataIndex;
   

                double _TimeIndex       = 0.0;
                int    avgCount         = 0;
                double avgLV1           = 0.0;
                double TP_Range_Min     = 5.0;
                double TP_Range_Max     = 0.0;
                double LastTestPressure = 0.0;

                string TestName    = "공압 리크 시험(5.0바)";
                double Low         = SystemLT.CurMesSpec.SPEC.ExternalLeak_50LeakMin;//0.0;// SystemLT.CurExternalLeakTest.Check_Low;
                double High        = SystemLT.CurMesSpec.SPEC.ExternalLeak_50LeakMax;//10.0;// SystemLT.CurExternalLeakTest.Check_High;
                double Measurement = avgLV1;
                string Unit        = "mbar";
                string ResultMsg   = "OK";
                string Description = "";

                if (GetDataCount <= 0)
                {
                    SystemLT.Log.Info("[5.0바 리크 시험] 코스모 장치에서 에러(대리크...)발생으로 데이터 없음");
                    ResultMsg = "NG";
                    Description = "코스모 장치에서 에러 발생(대리크 혹은 LLNG),판별할수 있는 데이터를 수신하지 못했습니다.";
                    SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                    return;
                }

                GraphData2[0].Clear();
                GraphData2[1].Clear();
                #region 데이터 저장
                string FilePath = SystemLT.IMSI_DataFullPath + SystemLT.CurMesSpec.SPEC.ImsiBarCode + "_AIR_LeakTest50.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);

                outputfile.WriteLine("(코스모 에어리크 테스터 장비에서 통신으로 읽음)데이터 샘플링 속도 : 10hz\n");
                outputfile.WriteLine("TestName,TestPressure(bar),Leak Value(mbar)\n");
                double _CheckDETFirstTime = 0.0;
                bool _CheckDERFirst = false;
                for (int i = 0; i < GetDataCount; i++)
                {
                    string sFmt = "";
                    sFmt = string.Format("{0},{1:F3},{2:F3}",
                                        SystemLT.M_Cosmo.Cosmo1Data[i].TN,
                                        SystemLT.M_Cosmo.Cosmo1Data[i].TP,
                                        SystemLT.M_Cosmo.Cosmo1Data[i].LV*0.01
                                        );
                    outputfile.WriteLine(sFmt + "\n");
                    // TP Range 확인용
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TP > TP_Range_Max) TP_Range_Max = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TP < TP_Range_Min) TP_Range_Min = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                    if (SystemLT.M_Cosmo.Cosmo1Data[i].TN.Replace(" ", "") == "DET")
                    {
                        if (_CheckDERFirst == false)
                        {
                            _CheckDETFirstTime = i / 10.0;
                            _CheckDERFirst = true;
                        }
                        avgLV1 = SystemLT.M_Cosmo.Cosmo1Data[i].LV*0.01;
                        avgCount++;
                        LastTestPressure = SystemLT.M_Cosmo.Cosmo1Data[i].TP;
                    }
                    GraphData2[0].Append(_TimeIndex, SystemLT.M_Cosmo.Cosmo1Data[i].TP);
                    GraphData2[1].Append(_TimeIndex, SystemLT.M_Cosmo.Cosmo1Data[i].LV*0.01);
                    _TimeIndex = _TimeIndex + 0.1; // 통신 데이터는 10Hz임.
                }
                outputfile.Close();
                // 그래프 표시
                //this.In_TestPressure5.Range = new Range<double>(TP_Range_Min * 1.05, TP_Range_Max * 1.05);
                this.IntLeakGraph5.DataSource = GraphData2;
                this.IntLeakGraph5.Refresh();

                Measurement = avgLV1;

                // Log 
                SystemLT.Log.Info("[공압 리크 시험(5.0바)]" + ResultMsg);
                SystemLT.Log.Info("[SPEC] Low " + string.Format("{0:F2}", Low));
                SystemLT.Log.Info("[SPEC] High " + string.Format("{0:F2}", High));
                SystemLT.Log.Info("[측정값] " + string.Format("{0:F1}", Measurement) + "mbar");
                SystemLT.Log.Info("[비고] " + Description);

                #region 코스모 장치 에러 확인
                bool   CosmoALT      = false;  // COSMO AIR LEAK TESTER = 하나만 존재
                string CosmosErrName = "";                
                bool chk = SystemLT.M_Plc.PLC_CosmoErrorCheck(2.0, ref CosmoALT,ref CosmosErrName);
                if (chk)
                {
                    if (CosmoALT)
                    {
                        Description = Description + " 코스모 에러=" + CosmosErrName;
                        ResultMsg   = "NG";
                        Test_Lamp_1_1.Background = Brushes.Red;
                    }
                }
                else
                {
                    IntLeakGraph.Foreground = Brushes.Red;
                    Description = Description + " 코스모 에러 체크 통신안됨";
                    ResultMsg = "NG";
                    Test_Lamp_1_1.Background = Brushes.Red;
                    CosmoALT = true;
                }
                #endregion


                if ((Measurement > Low) && (Measurement < High) && (avgCount > 0) && (!CosmoALT))
                {
                    ResultMsg = "OK";
                    Test_Lamp_1_1.Background = Brushes.Green;
                }
                else
                {
                    ResultMsg = "NG";
                    Test_Lamp_1_1.Background = Brushes.Red;
                }

                LT50_TestPressure.Content = string.Format("{0:F1}", Math.Abs(LastTestPressure));
                LT50_LeakMin.Content      = string.Format("{0:F2}", Low);
                LT50_LeakMax.Content      = string.Format("{0:F2}", High);
                LT50_Leak.Content         = string.Format("{0:F3}", Measurement);
                LT50_TestTime.Content     = string.Format("{0:F2}", SystemLT._SubTestTimer.ResultTime(LeakTest.TestTime.TESTNAME.LEAKTEST_AIR_50));

                if (Test_Lamp_1_1.Background == Brushes.Green)
                {
                    LT50_Result.Foreground = Brushes.Blue;
                }
                else
                {
                    LT50_Result.Foreground = Brushes.Red;
                }
                LT50_Result.Content = ResultMsg;

                INT_Leak50.AxisValue = _CheckDETFirstTime;
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                this.TestResultGrid.Items.Refresh();

                MainTab.SelectedIndex = 11;

                this.IntLeakGraph5.Refresh();


                // 
            }));
            // 결과 판단


        }



        private bool _DTC_InfoCheck = true;

        private void dele_EcuDTC1(string msg,int index)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                //List<string> DTCs_String
                string sMsg = msg;
                switch(index)
                {
                    case 0:
                        _DTC_InfoCheck = true;
                        this.TB_Msg0.Text = sMsg;
                        break;
                    case 1: // Report String
                        this.TB_Msg1.Text = sMsg;
                        break;
                    case 2:
                        this.TB_Msg2.Text = sMsg;
                        break;    
                    case 3:               
                        this.TB_Msg3.Text = "Model : " + sMsg + "("+ SystemLT.CurMesSpec.SPEC.DTC_ECU_MODEL + ")";
                        if (!string.Equals(SystemLT.DTC_ReadInfo.Model, SystemLT.CurMesSpec.SPEC.DTC_ECU_MODEL, StringComparison.OrdinalIgnoreCase))
                        {
                            _DTC_InfoCheck = _DTC_InfoCheck & false;
                            this.TB_Msg3.Background = Brushes.Red;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC MODEL", 0.0, 0.0, 0.0, "DTC", "NG", "MODEL = " + SystemLT.DTC_ReadInfo.Model));
                        }
                        else
                        {
                            this.TB_Msg3.Background = Brushes.White;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC MODEL", 0.0, 0.0, 0.0, "DTC", "OK", "MODEL = " + SystemLT.DTC_ReadInfo.Model));
                        }
                        break;
                    case 4:
                        this.TB_Msg4.Text = "Part Number : " + sMsg + "(" + SystemLT.CurMesSpec.SPEC.DTC_ECU_PartNumber + ")";
                        if (!string.Equals(SystemLT.DTC_ReadInfo.PartNumber, SystemLT.CurMesSpec.SPEC.DTC_ECU_PartNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            _DTC_InfoCheck = _DTC_InfoCheck & false;
                            this.TB_Msg4.Background = Brushes.Red;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC Part Number", 0.0, 0.0, 0.0, "DTC", "NG", "Part Number = " + SystemLT.DTC_ReadInfo.PartNumber));
                        }
                        else
                        {
                            this.TB_Msg4.Background = Brushes.White;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC Part Number", 0.0, 0.0, 0.0, "DTC", "OK", "Part Number = " + SystemLT.DTC_ReadInfo.PartNumber));
                        }
                        break;
                    case 5:
                        this.TB_Msg5.Text = "HW Version : " + sMsg + "(" + SystemLT.CurMesSpec.SPEC.DTC_ECU_HW_Version + ")";
                        if (!string.Equals(SystemLT.DTC_ReadInfo.HWVersion, SystemLT.CurMesSpec.SPEC.DTC_ECU_HW_Version, StringComparison.OrdinalIgnoreCase))
                        {
                            _DTC_InfoCheck = _DTC_InfoCheck & false;
                            this.TB_Msg5.Background = Brushes.Red;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC HW Version", 0.0, 0.0, 0.0, "DTC", "NG", "HW Version = " + SystemLT.DTC_ReadInfo.HWVersion));
                        }
                        else
                        {
                            this.TB_Msg5.Background = Brushes.White;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC HW Version", 0.0, 0.0, 0.0, "DTC", "OK", "HW Version = " + SystemLT.DTC_ReadInfo.HWVersion));
                        }
                        break;
                    case 6:
                        this.TB_Msg6.Text = "SW Version : " + sMsg + "(" + SystemLT.CurMesSpec.SPEC.DTC_ECU_SW_Version + ")";

                        if (!string.Equals(SystemLT.DTC_ReadInfo.SWVersion, SystemLT.CurMesSpec.SPEC.DTC_ECU_SW_Version, StringComparison.OrdinalIgnoreCase))
                        {
                            _DTC_InfoCheck = _DTC_InfoCheck & false;
                            this.TB_Msg6.Background = Brushes.Red;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC SW Version", 0.0, 0.0, 0.0, "DTC", "NG", "SW Version = " + SystemLT.DTC_ReadInfo.SWVersion));
                        }
                        else
                        {
                            this.TB_Msg6.Background = Brushes.White;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC SW Version", 0.0, 0.0, 0.0, "DTC", "OK", "SW Version = " + SystemLT.DTC_ReadInfo.SWVersion));
                        }
                        break;
                    case 7:
                        this.TB_Msg7.Text = "MOBIS H&SW Version : " + sMsg + "(" + SystemLT.CurMesSpec.SPEC.DTC_ECU_HSW_Version + ")";

                        if (!string.Equals(SystemLT.DTC_ReadInfo.MOBIS_HSWVersion, SystemLT.CurMesSpec.SPEC.DTC_ECU_HSW_Version, StringComparison.OrdinalIgnoreCase))
                        {
                            _DTC_InfoCheck = _DTC_InfoCheck & false;
                            this.TB_Msg7.Background = Brushes.Red;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC MS_SW_HW_VER", 0.0, 0.0, 0.0, "DTC", "NG", "MOBIS HWSW Version = " + SystemLT.DTC_ReadInfo.MOBIS_HSWVersion));
                        }
                        else
                        {
                            this.TB_Msg7.Background = Brushes.White;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC MS_SW_HW_VER", 0.0, 0.0, 0.0, "DTC", "OK", "MOBIS HWSW Version = " + SystemLT.DTC_ReadInfo.MOBIS_HSWVersion));
                        }
                        break;
                    case 8:                       
                        this.TB_Msg8.Text = "ECU Reset : " + sMsg + "(" + SystemLT.CurMesSpec.SPEC.DTC_ECU_Reset + ")";    
                        if (SystemLT.DTC_ReadInfo.ECUReset)
                        {
                            this.TB_Msg8.Background = Brushes.White;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC ECU Reset", 0.0, 0.0, 0.0, "DTC", "OK", "ECU Reset Result = 1"));
                        }
                        else
                        {
                            this.TB_Msg8.Background = Brushes.Red;
                            _DTC_InfoCheck = _DTC_InfoCheck & false;
                            SystemLT.CurTestResult.Add(new iMEB.TestResultTable("DTC ECU Reset", 0.0, 0.0, 0.0, "DTC", "NG", "ECU Reset Result = ?"));
                        }
                        if (!_DTC_InfoCheck)
                        {
                            Test_Lamp_2.Background = Brushes.Red;
                            DTCResultMsg.Content = "NG";
                            DTCResultMsg.Foreground = Brushes.Red;
                        }
                        if (SystemLT.EcuDtcResultCheck != 1)
                        {
                            Test_Lamp_2.Background = Brushes.Red;
                            DTCResultMsg.Content = "NG";
                            DTCResultMsg.Foreground = Brushes.Red;
                        }
                        break;
                    case 9:
                        this.TB_Msg9.Text = sMsg;
                        break;
                }
                Delay(5);
                MainTab.SelectedIndex = 2;

                // 
            }));
            // 결과 판단


        }
        private void dele_EcuDTC2(M_ECU.DTC_RESULT dtcresult,int index)
        {
            int i2 = 0;
            SystemLT.EcuDtcResultCheck = 0;
            
            // XML 화일에 설정된 Ignore Code 읽기
            string[] IgnoreCodeString = new string[100];
            try
            {
                IgnoreCodeString = SystemLT.CurEcuDtc.DTC_Ignore_Codes.Split(',');
            }
            catch (Exception) { }

            Int32[] IgnoreCodeInt = new Int32[100];
            foreach (string dtcCode in IgnoreCodeString)
            {

                SystemLT.Log.Info("[DTC] 기존 입력된 Ignore Codes = " + dtcCode);
                try
                {
                    if (dtcCode.Length != 0)
                    {
                        Int32 ConHex = Convert.ToInt32(dtcCode, 16);
                        IgnoreCodeInt[i2] = ConHex;
                    }
                }
                catch (Exception e1)
                {
                    SystemLT.Log.Info("[DTC] Conver.ToInt32 Error = " + e1.Message);
                }
                i2++;
            }

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {

                int     _count = dtcresult.Counts;
                int     sValue  = 0;
                bool    DTCCodeCheck  = true;

                string TestName    = "DTC 시험";
                double Low         = 0.0;
                double High        = 0.0;
                double Measurement = 0.0;
                string Unit        = "DTC";
                string ResultMsg   = "OK";
                string Description = "";
                // DTC INFO 확인( DTC_ECU_MODEL, DTC_ECU_PartNumber, DTC_ECU_HW_Version, DTC_ECU_SW_Version, DTC_ECU_HSW_Version, DTC_ECU_Reset, DTC_ECU_Motor_A_Min, DTC_ECU_Motor_A_Max
                // DTC_ECU_Motor_A_Min, DTC_ECU_Motor_A_Max 항목은 현재 시험안함.
                bool _ECU_InfoCheck = true;
                Delay(100);

                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("모터 전류", 0.0, 0.0, 0.0, "A", "OK", "현재 테스트 안함"));

                for (int i = 0; i < _count; i++)
                {
                    sValue = dtcresult.Codes[i];
                    if (sValue < 0x400000)
                    {
                        bool checkIn = false;
                        for (int i3 = 0; i3 < i2; i3++ )
                        {
                            if (IgnoreCodeInt[i3] == sValue)
                            {
                                checkIn = true;
                                SystemLT.CurDTCResult.Add(new iMEB.DTCResultTable(sValue, dtcresult.Status[i], "Ignore"));
                                break;
                            }
                        }
                        if (checkIn == false)
                        {
                            SystemLT.CurDTCResult.Add(new iMEB.DTCResultTable(sValue, dtcresult.Status[i], "Unknown"));
                            Description = Description + string.Format("C{0:D6}", sValue) + "/";
                            SystemLT.Log.Info("[DTC] Undefin Ignore DTC Code = " + string.Format("{0:X6}",sValue));
                        }
                        if (!checkIn) DTCCodeCheck = false;
                    }
                }
                // 결과 텍스트 표시
                this.DTC_PSUVoltage.Content = string.Format("{0:F2}", SystemLT.CurEcuDtc.MasterPSUVoltsSet);
                this.IgnoreCount.Content    = (i2 - 1).ToString();
                this.ReadCount.Content      = (_count - 1).ToString();
                this.DTCTestTime.Content    = string.Format("{0:F2}", SystemLT._SubTestTimer.ResultTime(LeakTest.TestTime.TESTNAME.ECUDTCTEST));

                if (DTCCodeCheck)
                {
                    DTCResultMsg.Content       = "OK";
                    DTCResultMsg.Foreground    = Brushes.Blue;
                    Test_Lamp_2.Background     = Brushes.Green;
                    Description                = "DTC 시험 : 미승인된  DTC코드는 없습니다.";
                    ResultMsg                  = "OK";
                    SystemLT.EcuDtcResultCheck = 1;
                }
                else
                {
                    DTCResultMsg.Content       = "NG";
                    DTCResultMsg.Foreground    = Brushes.Red;
                    Test_Lamp_2.Background     = Brushes.Red;
                    Description = Description + " <== 미승인된 DTC 코드";
                    ResultMsg                  = "NG";
                    SystemLT.EcuDtcResultCheck = -1;
                }

                /*
                if (_ECU_InfoCheck)
                {
                    Description = Description + ", DTC Info 리스트 이상없음";
                }
                else
                {
                    DTCResultMsg.Content       = "NG";
                    DTCResultMsg.Foreground    = Brushes.Red;
                    Test_Lamp_2.Background     = Brushes.Red;
                    Description                = Description + ", DTC Info 리스트와 MES정보 불일치 발생";
                    ResultMsg                  = "NG";
                    SystemLT.EcuDtcResultCheck = -1;
                }
                */
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));
                // DataGrid Row Color Change

                this.EcuDtcGrid.Items.Refresh();
                MainTab.SelectedIndex = 2;


                // 최종 판단 로직 추가...........................

            }));
            // 결과 판단


        }

        public class EcuMotorSpec
        {
            double FWD_Leak;            // 전진 진공 누설량
            double SPEC_FWD_Leak_Start; // 전진 명령후 측정 시작 시간
            double SPEC_FWD_Leak_End;   // 전진 명령후 측정 종료 시간

            double FWD_PtP_Avg_A;       // 전진 후 평균전류값
            double SPEC_FWD_PtP_Avg_Start;
            double SPEC_FWD_PtP_Avg_End;

            double FWD_PtP_Dif_A;       // 전진 전류 편차
            double SPEC_FWD_PtP_Dif_Start;
            double SPEC_FWD_PtP_Dif_End;

            double BWD_Leak;            // 전진 진공 누설량
            double SPEC_BWD_Leak_Start; // 전진 명령후 측정 시작 시간
            double SPEC_BWD_Leak_End;   // 전진 명령후 측정 종료 시간

            double BWD_PtP_Avg_A;       // 전진 후 평균전류값
            double SPEC_BWD_PtP_Avg_Start;
            double SPEC_BWD_PtP_Avg_End;

            double FWD_PtP_Dif_A1;       // 전진 전류 편차
            double SPEC_BWD_PtP_Dif_Start;
            double SPEC_BWD_PtP_Dif_End;

            //public EcuMotorSpec()
        }
        /// <summary>
        /// 모터 리크 테스트 결과 확인 및 그래프 출력
        /// </summary>
        /// <param name="FWDStartTime"></param>
        /// <param name="FWDEndTime"></param>
        /// <param name="BWDStartTime"></param>
        /// <param name="BWDEndTime"></param>
        private string _EcuLeakTestFileName = ""; // 마지막으로 사용된 Ecu결과 화일이름 저장용....

        private void dele_EcuMotor1Graph(double FWDStartTime,double FWDEndTime,double BWDStartTime,double BWDEndTime,bool DurationTestNG)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Send, new ThreadStart(delegate
            {
            bool LogEnable = true;

            GraphData3[0].Clear();
            GraphData3[1].Clear();
            GraphData3[2].Clear();
            GraphData3[3].Clear();

            LTM_TestTime.Content = string.Format("{0:F2}", SystemLT._SubTestTimer.ResultTime(LeakTest.TestTime.TESTNAME.MOTORLEAKTEST));

            int GetDAQDataCount = SystemLT.M_Daq.RTGraph.CurDataIndex;
            int GetMBCDataCount = SystemLT.M_Daq.RTGraph.CurDataIndexMBC;
            #region 데이터 정상 여부 확인
            if (DurationTestNG)
            {
                // 시험 도중 NG 발생시.... 
                Test_Lamp_3.Background = Brushes.Red;
                LTM_Result.Foreground = Brushes.Red;
                LTM_Result.Content = "NG";

                L_FWD_Avg.Content = "--";
                L_FWD_Avg.Foreground = Brushes.Gray;
                L_FWD_Diva.Content = "--";
                L_FWD_Diva.Foreground = Brushes.Gray;
                L_FWD_Leak.Content = "--";
                L_FWD_Leak.Foreground = Brushes.Gray;
                L_FWD_Peak.Content = "--";
                L_FWD_Peak.Foreground = Brushes.Gray;


                L_BWD_Avg.Content = "--";
                L_BWD_Avg.Foreground = Brushes.Gray;
                L_BWD_Diva.Content = "--";
                L_BWD_Diva.Foreground = Brushes.Gray;
                L_BWD_Leak.Content = "--";
                L_BWD_Leak.Foreground = Brushes.Gray;
                L_BWD_Peak.Content = "--";
                L_BWD_Peak.Foreground = Brushes.Gray;
                // LDS 에러 방지용
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails1", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails2", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails3", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails4", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails5", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails6", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails7", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails8", 0.0, 0.0, 0.0, "", "", ""));
                return;
            }
            if ((GetDAQDataCount <= 0) || (GetMBCDataCount <= 0))
            {
                // 측정 데이터가 없을 경우 
                Test_Lamp_3.Background = Brushes.Red;
                LTM_Result.Foreground = Brushes.Red;
                LTM_Result.Content = "NG";
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails1", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails2", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails3", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails4", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails5", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails6", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails7", 0.0, 0.0, 0.0, "", "", ""));
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("LEAKTEST Fails8", 0.0, 0.0, 0.0, "", "", ""));
                return;
            }
            #endregion
            double SetNewInterval   = 0.01; // 0.01초로 그래프 데이터 변환
            double CurDAQSampleRate = 1000.0;
            double CheckDAQDataTime = GetDAQDataCount / CurDAQSampleRate;
            int DaqDataGetInterval  = (int)(CurDAQSampleRate * SetNewInterval);

            double _TimeIndex = 0.0;

            // SPEC 처리 관련 변수 
            bool TotalTest_OK = true;   // 리크테스트 세부항목 결과 확인용
                                        //double SPEC_FWD_Leak               = 10.0;   // 10 mmHg 이하
            double SPEC_FWD_LeakCheck_StartPos = 2.2;    // 전진행정중 리크량 측정 시작 위치
            double SPEC_FWD_LeakCheck_EndPos   = 30.0;   // 전진행정중 리크량 측정 종료 위치
            double Result_FWD_Leak             = 0.0;

            double SPEC_FWD_Peak_A = SystemLT.CurMesSpec.SPEC.LeakII_FWDPeakMax;// 4.0;    // 전진 기동시 PEAK 전류는 4.0A 이하
                                                                                //double SPEC_FWD_PeakCheck_StartPos = 2.0;
                                                                                //double SPEC_FWD_PeakCheck_EndTPos  = 33.0;
            double Result_FWD_Peak_A = 0.0;

            //double SPEC_FWD_Avg_A              = 0.3;
            //double SPEC_FWD_AvgCheck_StartPos  = 2.0;
            //double SPEC_FWD_AvgCheck_EndPos    = 33.0;
            double Result_FWD_Avg_A = 0.0;
            double Result_FWD_Diva_A = 0.0;    // 표준편차

            //double SPEC_BWD_Leak               = 10.0;   // 10 mmHg 이하
            double SPEC_BWD_LeakCheck_StartPos = 30.0;   // 전진행정중 리크량 측정 시작 위치
            double SPEC_BWD_LeakCheck_EndPos = 2.2;    // 전진행정중 리크량 측정 종료 위치
            double Result_BWD_Leak = 0.0;

            //double SPEC_BWD_Peak_A             = 4.0;    // 전진 기동시 PEAK 전류는 4.0A 이하
            //double SPEC_BWD_PeakCheck_StartPos = 33.0;
            //double SPEC_BWD_PeakCheck_EndTPos  = 2.0;
            double Result_BWD_Peak_A = 0.0;

            //double SPEC_BWD_Avg_A              = 0.3;
            //double SPEC_BWD_AvgCheck_StartPos  = 33.0;
            //double SPEC_BWD_AvgCheck_EndPos    = 2.0;
            double Result_BWD_Avg_A = 0.0;
            double Result_BWD_Diva_A = 0.0;    // 표준편차

                /*
                 * 2018-07-18 : StringBuilder 이용하여 처리 속도 개선
                 * 
                 * 
                 *
                 */

                StringBuilder SBBuffer = new StringBuilder();

            string FilePath1 = SystemLT.IMSI_DataFullPath + SystemLT.CurMesSpec.SPEC.ImsiBarCode + "_ECU_PistonMovingStrokeInternalLeakText_MBC_Data.txt";
            _EcuLeakTestFileName = FilePath1;
            StreamWriter outputfile1 = new StreamWriter(FilePath1);

                SBBuffer.Append("ECU에서 CAN 통신으로 데이터 수집").AppendLine();
                SBBuffer.Append("Time(sec),Ampere(A),Position(mm)").AppendLine();
            //outputfile1.WriteLine("ECU에서 CAN 통신으로 데이터 수집\n");
            //outputfile1.WriteLine("Time(sec),Ampere(A),Position(mm)\n");

            bool FirstFWDPos_Find  = false;
            bool SecondFWDPos_Find = false;
            bool FirstBWDPos_Find  = false;
            bool SecondBWDPos_Find = false;

            double FirstFWD_StartTime = 0.0;
            double SecondFWD_EndTime  = 0.0;
            double FirstBWD_StartTime = 0.0;
            double SecondBWD_EndTime  = 0.0;

            #region 각 위치에서의 시작시간와 종료시간을 구함, 데이터 저장
    

            for (int i=0; i<GetMBCDataCount; i++)
            {
                double Amp  = SystemLT.M_Daq.RTGraph.RealMBCData[1, i];
                double Pos  = SystemLT.M_Daq.RTGraph.RealMBCData[2, i];
                double Time = SystemLT.M_Daq.RTGraph.RealMBCData[0, i];

                GraphData3[0].Append(Time, Amp);  // Current 
                GraphData3[1].Append(Time, Pos);  // Position
                SBBuffer.Append(string.Format("{0:F3},{1:F2},{2:F2}", Time, Amp, Pos)).AppendLine();
                    // 누설량 측정 기준 시간 계산
                    if ((Pos > SPEC_FWD_LeakCheck_StartPos) && (!FirstFWDPos_Find) && (Time > FWDStartTime - 0.5))
                    {
                        FirstFWD_StartTime = Time;
                        CFWD_S1.AxisValue  = Time;
                        FirstFWDPos_Find   = true;
                    }
                    if ((Pos > SPEC_FWD_LeakCheck_EndPos) && (!SecondFWDPos_Find) && (FirstFWDPos_Find))
                    {
                        SecondFWD_EndTime  = Time;
                        CFWD_S2.AxisValue  = Time;
                        SecondFWDPos_Find  = true;
                    }
                    if ((FirstFWDPos_Find) && (SecondFWDPos_Find) && (Time > (SecondFWD_EndTime + 3.0)))  // 전진관련후 3.0초 이상의 데이터에서 검색
                    {
                        if ((Pos < SPEC_BWD_LeakCheck_StartPos) && (!FirstBWDPos_Find))
                        {
                            FirstBWD_StartTime = Time;
                            CBWD_S1.AxisValue  = Time;
                            FirstBWDPos_Find   = true;
                        }
                        if ((Pos < SPEC_BWD_LeakCheck_EndPos) && (!SecondBWDPos_Find) && (FirstBWDPos_Find))
                        {
                            SecondBWD_EndTime  = Time;
                            CBWD_S2.AxisValue  = Time;
                            SecondBWDPos_Find  = true;
                        }
                    }
            }
            outputfile1.Write(SBBuffer.ToString());                
            outputfile1.Close();


            if (LogEnable)
            {
                SystemLT.Log.Info("[ECU MOTOR LEAK] FirstFWD Start Time : " + string.Format("{0:F2}", FirstFWD_StartTime));
                SystemLT.Log.Info("[ECU MOTOR LEAK] SecondFWD End Time  : " + string.Format("{0:F2}", SecondFWD_EndTime));
                SystemLT.Log.Info("[ECU MOTOR LEAK] FirstBWD Start Time : " + string.Format("{0:F2}", FirstBWD_StartTime));
                SystemLT.Log.Info("[ECU MOTOR LEAK] SecondBWD End Time  : " + string.Format("{0:F2}", SecondBWD_EndTime));
            }
            #endregion
            #region 전류 피크 및 평균값 계산
            int FWDAvgCount = 0;
            int BWDAvgCount = 0;
            Result_FWD_Avg_A = 0.0;
            Result_BWD_Avg_A = 0.0;
            // 표준변차 계산용
            List<double> FWD_Data = new List<double>();
            List<double> BWD_Data = new List<double>();

            for (int i = 0; i < GetMBCDataCount; i++)
            {
                // 피크값 및 평균값 계산
                double Amp  = SystemLT.M_Daq.RTGraph.RealMBCData[1, i];
                double Pos  = SystemLT.M_Daq.RTGraph.RealMBCData[2, i];
                double Time = SystemLT.M_Daq.RTGraph.RealMBCData[0, i];

                if ((Time > FirstFWD_StartTime) && (Time < SecondFWD_EndTime))
                {
                    if ((Time > FirstFWD_StartTime) && (Time < FirstFWD_StartTime + 0.5))
                    {
                        if (Amp > Result_FWD_Peak_A) Result_FWD_Peak_A = Amp;
                    }
                    Result_FWD_Avg_A = Result_FWD_Avg_A + Amp;
                    FWDAvgCount++;
                }

                if ((Time > FirstBWD_StartTime) && (Time < SecondBWD_EndTime))
                {
                    if ((Time > FirstBWD_StartTime) && (Time < FirstBWD_StartTime + 0.5))
                    {
                        if (Math.Abs(Amp) > Math.Abs(Result_BWD_Peak_A)) Result_BWD_Peak_A = Amp; // 반대방향으로 전류값은 - 부호를 가짐.
                    }
                    Result_BWD_Avg_A = Result_BWD_Avg_A + Amp;
                    BWDAvgCount++;
                }

                // 표준편차 구하기 위해서.. 병합
                    if ((Time > FirstFWD_StartTime) && (Time < SecondFWD_EndTime))
                    {
                        FWD_Data.Add(Amp);
                    }

                    if ((Time > FirstBWD_StartTime) && (Time < SecondBWD_EndTime))
                    {
                        BWD_Data.Add(Amp);
                    }

                }
            
            Result_FWD_Avg_A = (FWDAvgCount > 0) ? Result_FWD_Avg_A / FWDAvgCount : Result_FWD_Avg_A;
            Result_BWD_Avg_A = (BWDAvgCount > 0) ? Result_BWD_Avg_A / BWDAvgCount : Result_BWD_Avg_A;
            // Log 기록
            if (LogEnable)
            {
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result FWD Peak A   : " + string.Format("{0:F2}", Result_FWD_Peak_A));
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result BWD Peak A   : " + string.Format("{0:F2}", Result_BWD_Peak_A));
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result FWD Avg  A   : " + string.Format("{0:F2}", Result_FWD_Avg_A));
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result BWD Avg  A   : " + string.Format("{0:F2}", Result_BWD_Avg_A));
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result FWD AvgCount : " + string.Format("{0:N}", FWDAvgCount));
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result BWD AvgCount : " + string.Format("{0:N}", BWDAvgCount));
            }
            #endregion
            #region 표준편차 계산
            /*
            // List<double> FWD_Data = new List<double>();
            // List<double> BWD_Data = new List<double>();                      
            for (int i = 0; i < GetMBCDataCount; i++)
            {
                // 표준편차 계산
                double Amp  = SystemLT.M_Daq.RTGraph.RealMBCData[1, i];
                double Pos  = SystemLT.M_Daq.RTGraph.RealMBCData[2, i];
                double Time = SystemLT.M_Daq.RTGraph.RealMBCData[0, i];

                if ((Time > FirstFWD_StartTime) && (Time < SecondFWD_EndTime))
                {
                    FWD_Data.Add(Amp);
                }

                if ((Time > FirstBWD_StartTime) && (Time < SecondBWD_EndTime))
                {
                    BWD_Data.Add(Amp);
                }
            }
            */
            int FWD_DataCount = 0;
            int BWD_DataCount = 0;

            FWD_DataCount = FWD_Data.Count;
            BWD_DataCount = BWD_Data.Count;

            double[] FWD_RealData = new double[FWD_DataCount];
            double[] BWD_RealData = new double[BWD_DataCount];

            FWD_RealData = FWD_Data.ToArray();
            BWD_RealData = BWD_Data.ToArray();

            bool FindSD_FWD = false;
            bool FindSD_BWD = false;
            try
            {
                Result_FWD_Diva_A = Statistics.StandardDeviation(FWD_RealData);
                FindSD_FWD = true;
            }
            catch (Exception e1) { Result_FWD_Diva_A = -1.0; }
            try
            {
                Result_BWD_Diva_A = Statistics.StandardDeviation(BWD_RealData);
                FindSD_BWD = true;
            }
            catch (Exception e1) { Result_BWD_Diva_A = -1.0; }
            // Log 기록
            if (LogEnable)
            {
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result FWD A DataCount : " + string.Format("{0:N}", FWD_DataCount));
                SystemLT.Log.Info("[ECU MOTOR LEAK] Result BWD A DataCount : " + string.Format("{0:N}", BWD_DataCount));
                if (FindSD_FWD) SystemLT.Log.Info("[ECU MOTOR LEAK] Result Standard Deviation FWD A : " + string.Format("{0:F2}", Result_FWD_Diva_A));
                else SystemLT.Log.Info("[ECU MOTOR LEAK] Result Standard Deviation FWD A : 계산실패 ");
                if (FindSD_BWD) SystemLT.Log.Info("[ECU MOTOR LEAK] Result Standard Deviation BWD A : " + string.Format("{0:F2}", Result_BWD_Diva_A));
                else SystemLT.Log.Info("[ECU MOTOR LEAK] Result Standard Deviation BWD A : 계산실패");
            }
            #endregion
            #region DAQ 데이터 저장 및 리크량 계산

                SBBuffer.Clear();
            string FilePath = SystemLT.IMSI_DataFullPath + "ECU_PistonMovingStrokeInternalLeakText_VacuumData.txt";
            StreamWriter outputfile = new StreamWriter(FilePath);
                SBBuffer.Append("데이터 샘플링 속도 : 1Khz").AppendLine();
                SBBuffer.Append("Time(sec).Vacuum MC12(mmHg),Vacuum MC34(mmHg)").AppendLine();
            //outputfile.WriteLine("데이터 샘플링 속도 : 1Khz\n");
            //outputfile.WriteLine("Time(sec).Vacuum MC12(mmHg),Vacuum MC34(mmHg)\n");

            //bool FirstFWD_LeakCheck = false;
            //bool SecondFWD_LeakCheck = false;
            //bool FirstBWD_LeakCheck = false;
            //bool SecondBWD_LeakCheck = false;
            double FWD_S1 = 0.0;
            double FWD_S2 = 0.0;
            double BWD_S1 = 0.0;
            double BWD_S2 = 0.0;
            _TimeIndex = 0;
                
                for (int i=0; i<GetDAQDataCount; i++)
                {
                    double Vac1 = SystemLT.M_Daq.RTGraph.RealData[2, i];
                    double Vac2 = SystemLT.M_Daq.RTGraph.RealData[3, i];
                    GraphData3[2].Append(_TimeIndex, Vac1); // VAC1
                    GraphData3[3].Append(_TimeIndex, Vac2); // VAC2
                    SBBuffer.Append(string.Format("{0:F3},{1:F2},{2:F2}", _TimeIndex, Vac1, Vac2)).AppendLine();
                    _TimeIndex = _TimeIndex + 0.001;
                    //outputfile.WriteLine(string.Format("{0:F3},{1:F2},{2:F2}",_TimeIndex,Vac1,Vac2) + "\n");                   
                    /*                          
                    // 리크값 계산
                    if ((_TimeIndex > FirstFWD_StartTime) && (!FirstFWD_LeakCheck))
                    {
                        FWD_S1             = Vac1;
                        FirstFWD_LeakCheck = true;
                    }
                    if ((_TimeIndex > SecondFWD_EndTime) && (!SecondFWD_LeakCheck))
                    {
                        FWD_S2              = Vac1;
                        SecondFWD_LeakCheck = true;
                    }
                    // 리크값 계산
                    if ((_TimeIndex > FirstBWD_StartTime) && (!FirstBWD_LeakCheck))
                    {
                        BWD_S1             = Vac2;
                        FirstBWD_LeakCheck = true;
                    }
                    if ((_TimeIndex > SecondBWD_EndTime) && (!SecondBWD_LeakCheck))
                    {
                        BWD_S2              = Vac2;
                        SecondBWD_LeakCheck = true;
                    }
                    */
                }
                outputfile.Write(SBBuffer.ToString());
                outputfile.Close();

                int    TimeIndex = 0;
                TimeIndex = (int)(FirstFWD_StartTime*1000.0);
                FWD_S1 = SystemLT.M_Daq.RTGraph.RealData[2, TimeIndex];
                TimeIndex = (int)(SecondFWD_EndTime * 1000.0);
                FWD_S2 = SystemLT.M_Daq.RTGraph.RealData[2, TimeIndex];

                TimeIndex = (int)(FirstBWD_StartTime * 1000.0);
                BWD_S1 = SystemLT.M_Daq.RTGraph.RealData[3, TimeIndex];
                TimeIndex = (int)(SecondBWD_EndTime * 1000.0);
                BWD_S2 = SystemLT.M_Daq.RTGraph.RealData[3, TimeIndex];
                //Result_FWD_Leak = Math.Abs(FWD_S1 - FWD_S2);
                //Result_BWD_Leak = Math.Abs(BWD_S1 - BWD_S2);

                // OSEV P1 대응 - (OSEV P1) 전진 행정 스트로크 2~30mm 구간 중 MC1 포트에서 측정된 VACUUM 량은 180mmHg 이상 일 것 
                //                (OSEV P1) 복귀 행정 스트로크 30~2mm 구간 중 MC2 포트에서 측정 된 VACUUM 량은 300mmHg 이상 일 것 . 
                Result_FWD_Leak = (FWD_S1 > FWD_S2) ? FWD_S2 : FWD_S1;
                Result_BWD_Leak = (FWD_S1 > BWD_S2) ? BWD_S2 : BWD_S1;

         
                // Log 기록
                if (LogEnable)
                {
                    SystemLT.Log.Info("[ECU MOTOR LEAK] Result FWD Leak(mmHg) : " + string.Format("{0:F2}", Result_FWD_Leak));
                    SystemLT.Log.Info("[ECU MOTOR LEAK] Result BWD Leak(mmHg) : " + string.Format("{0:F2}", Result_BWD_Leak));
                }
                #endregion
                // OK/NG 판단
                #region 결과 확인
                double FWD_StartTime = FWDStartTime;
                double FWD_EndTime   = FWDEndTime;
                double BWD_StartTime = BWDStartTime;
                double BWD_EndTime   = BWDEndTime;                              
                // 진공 누설량 측정

                #endregion
                // X 축 시간 계산
                double DAQMaxTime = GetDAQDataCount / 1000.0;
                double MBCMaxTime = SystemLT.M_Daq.RTGraph.RealMBCData[0, GetMBCDataCount - 1];
                if (DAQMaxTime > MBCMaxTime)
                {
                    TimeAxis.Range = new Range<double>(0.0, DAQMaxTime);
                }
                else
                {
                    TimeAxis.Range = new Range<double>(0.0, MBCMaxTime);
                }
                this.EcuMotorGraph.DataSource = GraphData3;
                this.EcuMotorGraph.Refresh();
                Delay(50);
                // 리크 정보 그리드 출력
                string TestName    = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                double Low         = SystemLT.CurMesSpec.SPEC.LeakII_MC12LeakMin;// 0.0;
                double High        = SystemLT.CurMesSpec.SPEC.LeakII_MC12LeakMax; //180; // OSEV P1용  400.0; ;
                double Measurement = Result_FWD_Leak;
                string Unit        = "mmHg";
                string ResultMsg   = "OK";
                string Description = "진공 누설량 - FWD 행정";
                if ( (Result_FWD_Leak >=Low)&&(Result_FWD_Leak<=High))
                {
                    ResultMsg = "OK";
                    L_FWD_Leak.Foreground = Brushes.Blue;
                }
                else
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_FWD_Leak.Foreground = Brushes.Red;
                }
                L_FWD_Leak.Content   = string.Format("{0:F1}", Measurement);
                LTM_LeakMaxF.Content = string.Format("{0:F1}", Low); 
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description)); 

                TestName           = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low                = SystemLT.CurMesSpec.SPEC.LeakII_MC34LeakMin;//0.0;
                High               = SystemLT.CurMesSpec.SPEC.LeakII_MC34LeakMax; //300.0; // OSEV P1대응
                Measurement        = Result_BWD_Leak;
                Unit               = "mmHg";
                ResultMsg          = "OK";
                Description        = "진공 누설량 - BWD 행정";
                if ((Result_BWD_Leak >= Low)&&(Result_BWD_Leak <=High))
                {
                    ResultMsg = "OK";
                    L_BWD_Leak.Foreground = Brushes.Blue;
                }
                else
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_BWD_Leak.Foreground = Brushes.Red;
                }
                L_BWD_Leak.Content = string.Format("{0:F1}", Measurement);
                LTM_LeakMaxB.Content = string.Format("{0:F1}", Low); 
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));

                TestName           = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low                = SystemLT.CurMesSpec.SPEC.LeakII_FWDPeakMin;//0.0;
                High               = SystemLT.CurMesSpec.SPEC.LeakII_FWDPeakMax;//4.0; ;
                Measurement        = Result_FWD_Peak_A;
                Unit               = "A";
                ResultMsg          = "OK";
                Description        = "피크 전류량 - FWD 행정";
                if ((Result_FWD_Peak_A <= High)&&(Result_FWD_Peak_A>=Low))
                {
                    ResultMsg = "OK";
                    L_FWD_Peak.Foreground = Brushes.Blue;
                }
                else 
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_FWD_Peak.Foreground = Brushes.Red;
                }
                L_FWD_Peak.Content = string.Format("{0:F1}", Measurement);
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));

                TestName    = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low         = SystemLT.CurMesSpec.SPEC.LeakII_BWDPeakMin;//0.0;
                High        = SystemLT.CurMesSpec.SPEC.LeakII_BWDPeakMax;//4.0; ;
                Measurement = Result_BWD_Peak_A;
                Unit        = "A";
                ResultMsg   = "OK";
                Description = "피크 전류량 - BWD 행정";
                if  ( (Result_BWD_Peak_A<= High) && (Result_BWD_Peak_A>=Low) )
                {
                    ResultMsg = "OK";
                    L_BWD_Peak.Foreground = Brushes.Blue;
                }
                else
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_BWD_Peak.Foreground = Brushes.Red;
                }
                L_BWD_Peak.Content = string.Format("{0:F1}", Measurement);
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));

                TestName    = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low         = SystemLT.CurMesSpec.SPEC.LeakII_FWDAvgMin;//0.0;
                High        = SystemLT.CurMesSpec.SPEC.LeakII_FWDAvgmax;//2.5;
                Measurement = Result_FWD_Avg_A;
                Unit        = "A";
                ResultMsg   = "OK";
                Description = "평균 - FWD 행정";
                if ((Result_FWD_Avg_A>=Low)&&(Result_FWD_Avg_A <= High))
                {
                    ResultMsg = "OK";
                    L_FWD_Avg.Foreground = Brushes.Blue;
                }
                else
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_FWD_Avg.Foreground = Brushes.Red;
                }
                L_FWD_Avg.Content = string.Format("{0:F1}", Measurement);
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));


                TestName    = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low         = SystemLT.CurMesSpec.SPEC.LeakII_BWDAvgMin;//0.0;
                High        = SystemLT.CurMesSpec.SPEC.LeakII_BWDAvgmax;//2.5;
                Measurement = Result_BWD_Avg_A;
                Unit        = "A";
                ResultMsg   = "OK";
                Description = "평균 - BWD 행정";
                if ((Result_BWD_Avg_A>=Low)&&(Result_BWD_Avg_A <= High))
                {
                    ResultMsg = "OK";
                    L_BWD_Avg.Foreground = Brushes.Blue;
                }
                else 
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_BWD_Avg.Foreground = Brushes.Red;
                }
                L_BWD_Avg.Content = string.Format("{0:F1}", Measurement);
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));

                TestName    = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low         = SystemLT.CurMesSpec.SPEC.LeakII_FWDDivaMin;//0.0;
                High        = SystemLT.CurMesSpec.SPEC.LeakII_FWDDivaMax; //1.0;
                Measurement = Result_FWD_Diva_A;
                Unit        = "A";
                ResultMsg   = "OK";
                Description = "표준편차 - FWD 행정";
                if ((Result_FWD_Diva_A>=Low)&& (Result_FWD_Diva_A <= High))
                {
                    ResultMsg = "OK";
                    L_FWD_Diva.Foreground = Brushes.Blue;
                }
                else 
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_FWD_Diva.Foreground = Brushes.Red;
                }
                L_FWD_Diva.Content = string.Format("{0:F1}", Measurement);
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));

                TestName    = "iMEB Ass'y PISTON MOVING,PISTON STROKE 및 내부 리크테스트";
                Low         = SystemLT.CurMesSpec.SPEC.LeakII_BWDDivaMin;//0.0;
                High        = SystemLT.CurMesSpec.SPEC.LeakII_BWDDivaMax; //1.0;
                Measurement = Result_BWD_Diva_A;
                Unit        = "A";
                ResultMsg   = "OK";
                Description = "표준편차 - BWD 행정";
                if ((Result_BWD_Diva_A>=Low)&&(Result_BWD_Diva_A <= High))
                {
                    ResultMsg = "OK";
                    L_BWD_Diva.Foreground = Brushes.Blue;
                }
                else
                {
                    ResultMsg = "NG"; TotalTest_OK = TotalTest_OK & false;
                    L_BWD_Diva.Foreground = Brushes.Red;
                }
                L_BWD_Diva.Content = string.Format("{0:F1}", Measurement);
                //SPEC_FWD_LeakCheck_StartPos
                L_Start.Content = string.Format("{0:F1}",SPEC_FWD_LeakCheck_EndPos);// SPEC_FWD_LeakCheck_StartPos);
                L_End.Content   = string.Format("{0:F1}", SPEC_FWD_LeakCheck_StartPos);
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable(TestName, Low, Measurement, High, Unit, ResultMsg, Description));

                if (TotalTest_OK)
                {
                    Test_Lamp_3.Background = Brushes.Green;
                    LTM_Result.Foreground = Brushes.Blue;
                    LTM_Result.Content = "OK";
                }
                else
                {
                    Test_Lamp_3.Background = Brushes.Red;
                    LTM_Result.Foreground = Brushes.Red;
                    LTM_Result.Content = "NG";
                }
                this.TestResultGrid.Items.Refresh();
                //Delay(50);
                this.LT_Volt.Content = string.Format("{0:F2}", SystemLT.CurECULeakTest.MasterPSUVoltsSet);
                LTM_TestTime.Content = string.Format("{0:F2}", SystemLT._SubTestTimer.ResultTime(LeakTest.TestTime.TESTNAME.MOTORLEAKTEST));

                MainTab.SelectedIndex = 3;




                // 
            }));
            // 결과 판단


        }
        private void CosmoGraphRefresh(M_COSMO.COSMO_D_Format Data)
        {
            M_COSMO.COSMO_D_Format _Temp = new M_COSMO.COSMO_D_Format();
            int CurrentViewGraph = 0;

            switch(CurrentViewGraph)
            {
                case 0:
                    
                    break;
            }
        }
        private void StripState_COM(bool send,bool receive)
        {

                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    //string lastmsg = this.SB_COMSTATE.Text;
                    string msg = "";
                    if (send) msg = "SEND";
                    else msg = "";
                    if (receive) msg = "RECV";
                    else msg = "";

                    //int chkComp = string.Compare(lastmsg, msg, true);
                    //if (chkComp!=0)  this.SB_COMSTATE.Text = msg;
                    this.SB_COMSTATE.Text = msg;
                    Thread.Sleep(1);
                }));

        }        
        private void MainUIUpdate_CurAutoModeStep(string curMode,string someMsg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                this.SB_Msg.Text = curMode + " / " + someMsg;
            }));
        }
        private void Automode_Clear(int mode)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
               // 화면 정리 및 기타 정리
                switch (mode)
                {
                    case 0:
                        // Graph's Clear
                        this.ExtLeakGraph.DataSource = GraphData;
                        GraphData[0].Clear();
                        GraphData[1].Clear();
                        this.ExtLeakGraph.Refresh();

                        this.IntLeakGraph.DataSource = GraphData1;
                        GraphData1[0].Clear();
                        GraphData1[1].Clear();
                        this.IntLeakGraph.Refresh();

                        this.IntLeakGraph5.DataSource = GraphData2;
                        GraphData2[0].Clear();
                        GraphData2[1].Clear();
                        this.IntLeakGraph5.Refresh();

                        this.EcuMotorGraph.DataSource = GraphData3;
                        GraphData3[0].Clear();
                        GraphData3[1].Clear();
                        GraphData3[2].Clear();
                        GraphData3[3].Clear();
                        this.EcuMotorGraph.Refresh();

                        this.TB_Msg0.Text = "";
                        this.TB_Msg1.Text = "";
                        this.TB_Msg2.Text = "";
                        this.TB_Msg3.Text = "";
                        this.TB_Msg4.Text = "";
                        this.TB_Msg5.Text = "";
                        this.TB_Msg6.Text = "";
                        this.TB_Msg7.Text = "";
                        this.TB_Msg8.Text = "";
                        this.TB_Msg9.Text = "";

                        this.EcuDtcGrid.ItemsSource = null;
                        // Data Grid's Clear
                        SystemLT.CurTestResult.Clear();
                        SystemLT.CurDTCResult.Clear();
                        this.EcuDtcGrid.ItemsSource = SystemLT.CurDTCResult;
                        this.EcuDtcGrid.Items.Refresh();
                        this.TestResultGrid.ItemsSource = SystemLT.CurTestResult;
                        this.TestResultGrid.Items.Refresh();

                        Test_Lamp_0.Background   = _Lamp_BackColor;
                        Test_Lamp_1.Background   = _Lamp_BackColor;
                        Test_Lamp_1_1.Background = _Lamp_BackColor;
                        Test_Lamp_2.Background   = _Lamp_BackColor;
                        Test_Lamp_3.Background   = _Lamp_BackColor;
                        Test_Analay1.Background  = _Lamp_BackColor;

                        LT_Volt.Content       = "?";
                        L_FWD_Avg.Content     = "--";
                        L_FWD_Avg.Foreground  = Brushes.Gray;
                        L_FWD_Diva.Content    = "--";
                        L_FWD_Diva.Foreground = Brushes.Gray;
                        L_FWD_Leak.Content    = "--";
                        L_FWD_Leak.Foreground = Brushes.Gray;
                        L_FWD_Peak.Content    = "--";
                        L_FWD_Peak.Foreground = Brushes.Gray;


                        L_BWD_Avg.Content     = "--";
                        L_BWD_Avg.Foreground  = Brushes.Gray;
                        L_BWD_Diva.Content    = "--";
                        L_BWD_Diva.Foreground = Brushes.Gray;
                        L_BWD_Leak.Content    = "--";
                        L_BWD_Leak.Foreground = Brushes.Gray;
                        L_BWD_Peak.Content    = "--";
                        L_BWD_Peak.Foreground = Brushes.Gray;

                        L_Start.Content       = "--";
                        L_Start.Foreground    = Brushes.Gray;
                        L_End.Content         = "--";
                        L_End.Foreground      = Brushes.Gray;


                        // 추가된 표시 클리어
                        LTV_TestPressure.Content = "?";
                        LTV_Leak.Content         = ".";
                        LTV_TestTime.Content     = ".";
                        LTV_Result.Content       = ".";
                        LTV_Result.Foreground    = Brushes.Black;

                        LT15_TestPressure.Content = "?";
                        LT15_Leak.Content         = ".";
                        LT15_TestTime.Content     = ".";
                        LT15_Result.Content       = ".";
                        LT15_Result.Foreground    = Brushes.Black;

                        LT50_TestPressure.Content = "?";
                        LT50_Leak.Content         = ".";
                        LT50_TestTime.Content     = ".";
                        LT50_Result.Content       = ".";
                        LT50_Result.Foreground    = Brushes.Black;

                                        // 결과 텍스트 표시
                        DTC_PSUVoltage.Content = "?";
                        IgnoreCount.Content    = ".";
                        ReadCount.Content      = ".";
                        DTCTestTime.Content    = ".";
                        DTCResultMsg.Content   = ".";
                        DTCResultMsg.Foreground = Brushes.Black;

                        MainTab.SelectedIndex = 0;

                        SystemLT.DTC_ReadInfo.HWVersion        = "";
                        SystemLT.DTC_ReadInfo.SWVersion        = "";
                        SystemLT.DTC_ReadInfo.PartNumber       = "";
                        SystemLT.DTC_ReadInfo.Model            = "";
                        SystemLT.DTC_ReadInfo.MOBIS_HSWVersion = "";
                        SystemLT.DTC_ReadInfo.ECUReset         = false;

                        break;
                }
            }));
        }
        /// <summary>
        /// 에러 발생시 쓰레드에서 호출하는 Notify Msg
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void ErrorNotify(string eMsg,int eCode)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                // Notify Msg.
                var notificationManager = new NotificationManager();
                notificationManager.Show(new NotificationContent
                {
                    Title   = "알림 : "+eCode.ToString(),
                    Message = eMsg,
                    Type    = NotificationType.Information
                });
            }));
        }
        #endregion


        public void SetMinWidths(object source, EventArgs e)
        {
            foreach (var column in TestResultGrid.Columns)
            {
                column.MinWidth = column.ActualWidth;
                column.Width    = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
        }

        private bool LT_Init()
        {
            bool chk = false;
            // 전체 시스템 초기화 관련
            try
            {
                chk = SystemLT.System_Initialize();
            }
            catch (Exception e1) { }
            // UI Part Update
            this.lbl_ProgramVersion.Content = SystemLT.CurConfig.SW_Version + " R ";

            
            // 메인 UI Timer Start
            // 메인 화면 업데이트용 타이머 설정
            double Setms1 = SystemLT.CurConfig.UI_RefreshTimerValue;
            //if ((Setms1 > 50.0) && (Setms1 < 250.0))  UITimer.Interval = TimeSpan.FromMilliseconds(Setms1);
            //else                                      UITimer.Interval = TimeSpan.FromMilliseconds(50);
            UITimer.Interval = TimeSpan.FromMilliseconds(100);
            UITimer.Tick += new EventHandler(UITimer_Tick);
            //UITimer.Stop();
            // PerformanceCOunter 설정
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");  
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            _UITimerOnly.Reset();
            _UITimerOnly.Start();
            UITimer.Start();

            // 메인 제어 타이머 설정
            double Setms2 = SystemLT.CurConfig.MC_LoopTimerValue;
            if ((Setms2>9.0)&&(Setms2<101.0))  MainControlTimer.Interval = TimeSpan.FromMilliseconds(Setms2);
            else                               MainControlTimer.Interval = TimeSpan.FromMilliseconds(20);
            MainControlTimer.Tick += new EventHandler(MainControlTimer_Tick);
            //MainControlTimer.Stop();
            
            // 시작 조건 확인 후 타이머 시작
            MainControlTimer.Start();

            // MES 정보 읽기
            string _MesModelCode = "";
            chk =SystemLT.M_Plc.CMD_Read_ModelCode(2.0, ref _MesModelCode);
            SystemLT.Log.Info("[MES] PLC Model = " + _MesModelCode + " PC Model = " + SystemLT.CurMesSpec.SPEC.LastProductCode);
            if (chk)
            {
                if (!string.Equals(_MesModelCode, SystemLT.CurMesSpec.SPEC.LastProductCode, StringComparison.CurrentCultureIgnoreCase))
                {
                    bool _chk = MESUpdate(_MesModelCode,ref SystemLT.CurMesSpec);
                }
                else
                {
                    SystemLT.Log.Info("[MES] MES 사양정보 업데이트 안함.");
                }
            }
            else
            {
                SystemLT.Log.Info("[MES] PLC에서 모델정보를 읽지 못했습니다.(통신오류) ");
            }
            return true;
        }
        /// <summary>
        /// MES에서 해당 모델 사양정보 수신
        /// </summary>
        /// <param name="modelcode"></param>
        /// <param name="messpec"></param>
        /// <returns></returns>
        private bool MESUpdate(string modelcode,ref MESSPEC messpec)
        {
            string barcode       = ""; 
            string typestr      = modelcode;
            string resultmsg    = "";
            int    resultcode   = 0;

            if (SystemLT.CurMesSpec.SPEC.ImsiBarCode==null)
            {
                barcode = "01234567890";
            }
            else
            {
                barcode = SystemLT.CurMesSpec.SPEC.ImsiBarCode;
            }
            if (barcode.Length != 11) barcode = "01234567890";

            SystemLT.Log.Info("[MES] 모델 정보 요청");
            SystemLT.Log.Info("[MES] 모델코드 = "+modelcode);
            bool chk = SystemLT.M_MesLds.MES_RequestType(barcode, typestr, ref resultmsg, ref resultcode);
            if (!chk)
            {
                SystemLT.Log.Info("[MES] 모델 정보 요청시 응답이 없습니다.");
            }

            SystemLT.Log.Info("[MES] 모델 세부 사양 정보 요청 " + modelcode);
            SystemLT.Log.Info("[MES] 모델코드 = " + modelcode);
            chk = SystemLT.M_MesLds.MES_RequestSpec(barcode, modelcode, ref resultmsg, ref resultcode);
            if (!chk) SystemLT.Log.Info("[MES] 모델 세부 사양 정보 요청시 응답이 없습니다. = " + modelcode);
            else
            {
                // 업데이트 mes spec
                if (resultcode==92)
                {
                    bool updatechk = CurSpecUpdate(resultmsg);
                }

            }

            //
            D_ProductCode.Content = modelcode;
            D_BarCode.Content = barcode;
            return true;
        }

        private bool CurSpecUpdate(string msg)
        {
            Char[] delimiters = { '|' };
            Char[] delimitersComma = { ',' };
            double[] minValue = new double[100];
            double[] maxValue = new double[100];
            string[] DTCVlaue = new string[10]; // DTC 테스트의 경우 Nominal Value와 비교하여 판단.
            double minvalue   = 0.0;
            double maxvalue   = 0.0;
            try
            {
                string[] wordsSplit = msg.Split(delimiters, 20);
                for (int i=1; i<=12; i++) // 첫번째는 무시
                {
                    string[] subwordsSplit = wordsSplit[i].Split(delimitersComma, 4);
                    string name  = subwordsSplit[0];
                    string min   = subwordsSplit[2];
                    string nom   = subwordsSplit[1];
                    string max   = subwordsSplit[3];
                    bool chkminv = double.TryParse(min, out minValue[i]);
                    bool chkmaxv = double.TryParse(max, out maxValue[i]);
                }
                // 12.Motor_C,0,0.5,1              => 파형 검사
                string[]  subwordssplit = wordsSplit[13].Split(delimitersComma, 4);
                DTCVlaue[0]   = subwordssplit[1];
                //13.ECU_MODEL,0,PS ESC,0         => Code & type check
                subwordssplit = wordsSplit[14].Split(delimitersComma, 4);
                DTCVlaue[1]   = subwordssplit[1];
                //14.ECU_Parts_No,0,3920-B2506,0  => ECU Part Number
                subwordssplit = wordsSplit[15].Split(delimitersComma, 4);
                DTCVlaue[2]   = subwordssplit[1];
                //15.OEM_HW_VER,1,1,1             => Customer HW Version check
                subwordssplit = wordsSplit[16].Split(delimitersComma, 4);
                DTCVlaue[3]   = subwordssplit[1];
                //16.OEM_SW_VER,4,4,4             => Customer SW Version check
                subwordssplit = wordsSplit[17].Split(delimitersComma, 4);
                DTCVlaue[4]   = subwordssplit[1];
                //17.MS_SW_HW_VER,0,D01140803,0   => eeprom MOBIS H&SW Version check
                subwordssplit = wordsSplit[18].Split(delimitersComma, 4);
                DTCVlaue[5]   = subwordssplit[1];
                //18.ECU_Error_RESET,1,1,1        => ECU Self test routine result
                subwordssplit = wordsSplit[19].Split(delimitersComma, 4);
                DTCVlaue[6]   = subwordssplit[1];

                //19.CMOT1,1500,4650,7800         => motor current check
                subwordssplit = wordsSplit[20].Split(delimitersComma, 4);
                string min1 = subwordssplit[2];
                string nom1 = subwordssplit[1];
                string max1 = subwordssplit[3];
                double.TryParse(min1, out minvalue);
                double.TryParse(max1, out maxvalue);
            }
            catch (Exception e1)
            {
               // return false;
            }
            /*
            M1iMEB ASSY ENGLISH/FRENCH CAP|
            1. PVacuum_Leak,1.875,0,3.75|   => 압력강하량
            2. Leak_1.5bar,5,0,10           => 공압 1.5바 리크
            3. Leak_5.0bar,5,0,10           => 공압 5.0바 리크
            4. FWD_Leak,352.5,180,525       => 진공 누설량 설정값
            5. FWD_Peak,2,0,4               => 기동 전류값            
            6. FWD_Avg,1.25,0,2.5           => 평균 전류값
            7. FWD_Diva,0.5,0,1             => 전류 편차값
            8. BWD_Leak,402.5,280,525       => 진공 누설량 설정값
            9. BWD_Peak,2,0,4               => 기동 전류값
            10.BWD_Avg,1.25,0,2.5           => 평균 전류값
            11.BWD_Diva,0.5,0,1             => 전류 편차값
            // 2017-12-01 추가분
            12.Motor_C,0,0.5,1              => 파형 검사
            13.ECU_MODEL,0,PS ESC,0         => Code & type check
            14.ECU_Parts_No,0,3920-B2506,0  => ECU Part Number
            15.OEM_HW_VER,1,1,1             => Customer HW Version check
            16.OEM_SW_VER,4,4,4             => Customer SW Version check
            17.MS_SW_HW_VER,0,D01140803,0   => eeprom MOBIS H&SW Version check
            18.ECU_Error_RESET,1,1,1        => ECU Self test routine result
            19.CMOT1,1500,4650,7800         => motor current check
            */
            // 내부 테이블(사양) 맵핑
            SystemLT.CurMesSpec.SPEC.InternalLeak_LeakMin   = minValue[1];
            SystemLT.CurMesSpec.SPEC.InternalLeak_LeakMax   = maxValue[1];
            SystemLT.CurMesSpec.SPEC.ExternalLeak_15LeakMin = minValue[2];
            SystemLT.CurMesSpec.SPEC.ExternalLeak_15LeakMax = maxValue[2];
            SystemLT.CurMesSpec.SPEC.ExternalLeak_50LeakMin = minValue[3];
            SystemLT.CurMesSpec.SPEC.ExternalLeak_50LeakMax = maxValue[3];

            SystemLT.CurMesSpec.SPEC.LeakII_MC12LeakMin     = minValue[4];
            SystemLT.CurMesSpec.SPEC.LeakII_MC12LeakMax     = maxValue[4];
            SystemLT.CurMesSpec.SPEC.LeakII_FWDPeakMin      = minValue[5];
            SystemLT.CurMesSpec.SPEC.LeakII_FWDPeakMax      = maxValue[5];
            SystemLT.CurMesSpec.SPEC.LeakII_FWDAvgMin       = minValue[6];
            SystemLT.CurMesSpec.SPEC.LeakII_FWDAvgmax       = maxValue[6];
            SystemLT.CurMesSpec.SPEC.LeakII_FWDDivaMin      = minValue[7];
            SystemLT.CurMesSpec.SPEC.LeakII_FWDDivaMax      = maxValue[7];

            SystemLT.CurMesSpec.SPEC.LeakII_MC34LeakMin     = minValue[8];
            SystemLT.CurMesSpec.SPEC.LeakII_MC34LeakMax     = maxValue[8];
            SystemLT.CurMesSpec.SPEC.LeakII_BWDPeakMin      = minValue[9];
            SystemLT.CurMesSpec.SPEC.LeakII_BWDPeakMax      = maxValue[9];
            SystemLT.CurMesSpec.SPEC.LeakII_BWDAvgMin       = minValue[10];
            SystemLT.CurMesSpec.SPEC.LeakII_BWDAvgmax       = maxValue[10];
            SystemLT.CurMesSpec.SPEC.LeakII_BWDDivaMin      = minValue[11];
            SystemLT.CurMesSpec.SPEC.LeakII_BWDDivaMax      = maxValue[11];

            SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Min     = minValue[12];
            SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Max     = maxValue[12];
            FWD_Diff.Value = maxValue[12];
            BWD_Diff.Value = maxValue[12];
            SystemLT.CurMesSpec.SPEC.DTC_ECU_MODEL          = DTCVlaue[0];
            SystemLT.CurMesSpec.SPEC.DTC_ECU_PartNumber     = DTCVlaue[1];
            SystemLT.CurMesSpec.SPEC.DTC_ECU_HW_Version     = DTCVlaue[2];
            SystemLT.CurMesSpec.SPEC.DTC_ECU_SW_Version     = DTCVlaue[3];
            SystemLT.CurMesSpec.SPEC.DTC_ECU_HSW_Version    = DTCVlaue[4];
            SystemLT.CurMesSpec.SPEC.DTC_ECU_Reset          = DTCVlaue[5];

            SystemLT.CurMesSpec.SPEC.DTC_ECU_Motor_A_Min    = minvalue;            
            SystemLT.CurMesSpec.SPEC.DTC_ECU_Motor_A_Max    = maxvalue;

            return true;
        }


        #region 자동 로그 표시용 내부처리 함수들...
        public IEnumerable<EncodingInfo> Encodings
        {
            get { return _encodings; }
        }

        public string LastUpdated
        {
            get { return _lastUpdated; }
            set
            {
                if (value == _lastUpdated) return;
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }

        public FileMonitorViewModel SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileMonitorViewModel> FileMonitors
        {
            get { return _fileMonitors; }
        }

        public string Font
        {
            get { return _font; }
            set
            {
                if (value == _font) return;
                _font = value;
                OnPropertyChanged();
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private System.Threading.Timer CreateRefreshTimer()
        {
            var timer = new System.Threading.Timer(state => RefreshLastUpdatedText());
            timer.Change((DateTime.Now.Date.AddDays(1) - DateTime.Now), TimeSpan.FromDays(1));
            this.Closing += DisposeTimer;
            return timer;
        }

        private void DisposeTimer(object s, CancelEventArgs e)
        {
            try
            {
                _refreshTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _refreshTimer.Dispose();
            }
            catch (Exception e1)
            {

            }
        }

        private void AddFileMonitor(string filepath)
        {
            var existingMonitor = FileMonitors.FirstOrDefault(m => string.Equals(m.FilePath, filepath, StringComparison.CurrentCultureIgnoreCase));

            if (existingMonitor != null)
            {
                // Already being monitored
                SelectedItem = existingMonitor;
                return;
            }

            var monitorViewModel = new FileMonitorViewModel(filepath, GetFileNameForPath(filepath), "String", false);
            monitorViewModel.Renamed += MonitorViewModelOnRenamed;
            monitorViewModel.Updated += MonitorViewModelOnUpdated;

            FileMonitors.Add(monitorViewModel);
            SelectedItem = monitorViewModel;
        }

        private void MonitorViewModelOnUpdated(FileMonitorViewModel obj)
        {
            _lastUpdateDateTime = DateTime.Now;
            _lastUpdatedViewModel = obj;
            RefreshLastUpdatedText();
        }

        private void MonitorViewModelOnRenamed(FileMonitorViewModel renamedViewModel)
        {
            var filepath = renamedViewModel.FilePath;

            renamedViewModel.FileName = GetFileNameForPath(filepath);
        }

        private static string GetFileNameForPath(string filepath)
        {
            return System.IO.Path.GetFileName(filepath);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            PromptForFile();
        }

        private void PromptForFile()
        {
            var openFileDialog = new OpenFileDialog { CheckFileExists = false, Multiselect = true };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    foreach (var fileName in openFileDialog.FileNames)
                    {
                        AddFileMonitor(fileName);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Error: " + exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void RefreshLastUpdatedText()
        {
            if (_lastUpdateDateTime != null)
            {
                var dateTime = _lastUpdateDateTime.Value;
                var datestring = dateTime.Date != DateTime.Now.Date ? " on " + dateTime : " at " + dateTime.ToLongTimeString();
                LastUpdated = _lastUpdatedViewModel.FilePath + datestring;
            }
        }
        #endregion
        #region 자동 제어 관련
        private static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                  new Action(delegate { }));
        }
        private static DateTime Delay(int MS)
        {
            DateTime ThisMoment = DateTime.Now;
            TimeSpan duration = new TimeSpan(0, 0, 0, 0, MS);
            DateTime AfterWards = ThisMoment.Add(duration);
            while (AfterWards >= ThisMoment)
            {
                DoEvents();
                ThisMoment = DateTime.Now;
            }
            return DateTime.Now;
        }
        private int _RetryDAQCount        = 0;
        private int _LogDateCheckInterval = 0; // 날짜가 변경되는 것을 확인하기 위함.
        private bool _ModelChangeRunning = false;
        private bool DO_ModelChange()
        {
            // MES 정보 읽기
            bool chkCom = false;
            string _MesModelCode = "";
            _ModelChangeRunning = true;
            SystemLT.Log.Info("[MES] PLC->PC 모델 체인지 요청");
            chkCom = SystemLT.M_Plc.CMD_Read_ModelCode(0.5, ref _MesModelCode);
            SystemLT.Log.Info("[MES] PLC Model = " + _MesModelCode + "  기존 PC Model = " + SystemLT.CurMesSpec.SPEC.LastProductCode);

            if (chkCom)
            {

                if (SystemLT.M_Plc.CMD_Write_ModelBusy(0.5, 1))
                {
                    if (MESUpdate(_MesModelCode, ref SystemLT.CurMesSpec)) SystemLT.Log.Info("[MES] PLC->PC 모델 체인지 요청이 정상적으로 완료 되었습니다.");
                    else                                                   SystemLT.Log.Info("[MES] PLC->PC 모델 체인지 에러  = "+_MesModelCode);
                }                
                SystemLT.M_Plc.CMD_Write_ModelResult(0.5, 1);
                SystemLT.M_Plc.CMD_Write_ModelBusy(0.5, 0);
            }
            else
            {
                SystemLT.Log.Info("[MES] PLC에서 모델정보를 읽지 못했습니다.(통신오류) ");
            }
            _ModelChangeRunning = false;
            return true;
        }
        private bool _LDS_Check_Running       = false;
        private bool _IsBarCodeRead           = false;  // 자동 운전중 PLC측에서 Barcode 읽기 모드를 수행했는지 여부, 시험 종료시 반드시 false할것.
        private bool _PLCModeChange           = false;  // PLC에서 수동->자동으로 변경시 모델정보를 읽고 MES에서 업데이트를 수행하기 위한 플래그
        private void MainControlTimer_Tick(object sender, EventArgs e)
        {
            #region PLC->PC 운전관련
            bool           Chk        = false;
            bool           Chk2       = false;
            bool           tmpResult  = false;
            bool           IsLDSJoin  = false;
            string         BarCode    = "";
            string         ResultMsg  = "";
            int            ResultCode = 0;
            M_PLC.PLCSTATE _Plc       = new M_PLC.PLCSTATE();

            try 
            {
                Chk  = SystemLT.M_Plc.CMD_CurrentState();                                                                             // GS :  PLC->PC 자동운전,수동운전, 알람(에러), 제품로딩 신호를 읽오옴. --> PLCState정보저장               
                _Plc = SystemLT.M_Plc.PLCState;
                if (SystemLT.M_Plc.CMD_Read_LDSSignal(ref tmpResult)) IsLDSJoin = tmpResult;                                          // LDS 신호 확인                
                if (Chk)
                {
                    #region PLC 신호가 에러일 경우
                    if (_Plc.Error) { PLC_Error_Display(); }
                    #endregion
                    #region PLC에서 수동->자동으로 변경되었는지를 확인후 MES모델체인지 수행
                    if ( (!_Plc.Error)&&(!_Plc.Auto) ) // PLC 에러가 아니고 수동모드일경우 
                    {
                        if (_PLCModeChange==true) _PLCModeChange = false;
                        if (_OKDisplay == true) OKHide();
                        if (_NGDisplay == true) NGHide();
                    }
                    if ( (_PLCModeChange==false)&& (!_Plc.Error) && (_Plc.Auto) )
                    {
                        // MES 업데이트
                        DO_ModelChange();
                        _PLCModeChange = true;
                    }
                    #endregion
                    #region 판넬에서 리셋버튼을 누를시
                    if (_Plc.Reset)
                    {
                        PLC_RESET_Go();
                        if (_OKDisplay == true) OKHide();
                        if (_NGDisplay == true) NGHide();
                    }
                    #endregion
                    #region 에러가 없고 매뉴얼일때 모델체인지 신호를 확인하여 처리
                    if ((!_Plc.Error)&&(_Plc.Manual))
                        // 에러 없고 매뉴얼 상화
                    {
                        int checkModelChange = 0;
                        SystemLT.M_Plc.CMD_Read_ModelChange(0.5, ref checkModelChange);
                        if  ((checkModelChange==1)&&(_ModelChangeRunning ==false))
                        {
                            DO_ModelChange();
                        }
                    }
                    #endregion
                    #region 에러가 없고 자동운전모드이고 자동쓰레드 프로그램이 시작전일때
                    if ((!_Plc.Error)&&(_Plc.Auto)&&(!SystemLT.IsAutoModeRun))
                    {   // 자동운전 시작
                        if (_Plc.LeakMode) { PLC_LEAKTEST_Go(); }                      
                        if ((IsLDSJoin)&&(!_LDS_Check_Running))
                        {
                            if (_OKDisplay == true) OKHide();
                            if (_NGDisplay == true) NGHide();
                            _IsBarCodeRead     = true;
                            _LDS_Check_Running = true;
                            bool _chk2 = SystemLT.M_Plc.CMD_Write_ModelLDSResult(0.2, 0);  // PLC에게 LDS결과를 알려줌 1=OK,2=NG, 0=초기화
                            SystemLT.Log.Info("[MES] LDS Result Clear = " + _chk2.ToString());
                            bool _chk1 = SystemLT.M_Plc.CMD_Write_ModelLDSBusy(0.2, 1);
                            SystemLT.Log.Info("[MES] LDS Busy set = " + _chk1.ToString());
                            Chk2 = SystemLT.M_Plc.CMD_Read_BarCode(1.0, ref BarCode);
                            SystemLT.Log.Info("[MES] BarCode(PLC->PC) = " + BarCode);
                            SystemLT.Log.Info("[MES] BarCode read fuction = " + Chk2.ToString());
                            // 모델 정보 일기
                            string modelcode = "";
                            bool _chk5 = SystemLT.M_Plc.CMD_Read_ModelCode(0.2, ref modelcode);
                            SystemLT.CurMesSpec.SPEC.LastProductCode = modelcode;

                            if (BarCode.Length==11)
                            {
                                this.D_BarCode.Content = BarCode;
                                bool _chk = SystemLT.M_MesLds.LDS_RequestInfomation(BarCode, ref ResultMsg, ref ResultCode);
                                this.D_MES.Content = ResultMsg + " result code=" +string.Format("{0:D}", ResultCode);
                                if ( (ResultMsg.Contains("OK Preprocess"))&& (!ResultMsg.Contains("NOK Preprocess")) || (ResultMsg.Contains("Re-operation")) )
                                {
                                    // 모델 확인(바코드 정보와 현재 작업 모델확인
                                    bool _chk3=false;
                                    //if (BarCode.Contains(modelcode))
                                    //{
                                        _chk3 = SystemLT.M_Plc.CMD_Write_ModelLDSResult(0.2, 1);
                                        SystemLT.Log.Info("[MES] LDS Result Set(1=OK,2=NG) = 1, fuction = " + _chk3.ToString());
                                    //}
                                    //else
                                    //{
                                     //   NGShow("모델 정보 불일치  " + modelcode + " / " + BarCode);
                                      //  _chk3 = SystemLT.M_Plc.CMD_Write_ModelLDSResult(0.2, 2);
                                     //   SystemLT.Log.Info("[MES] LDS Result Set(1=OK,2=NG) = 2, 모델정보 불일치 = " + modelcode + " / " + BarCode);
                                    //}                                   
                                }
                                else
                                {
                                    NGShow("바코드 알람 : " + ResultMsg);
                                    bool _chk3 = SystemLT.M_Plc.CMD_Write_ModelLDSResult(0.2, 2);
                                    SystemLT.Log.Info("[MES] LDS Result Set(1=OK,2=NG) = 2, 바코드 알람" + ResultMsg);
                                    _IsBarCodeRead = false;
                                }

                                SystemLT.CurMesSpec.SPEC.ImsiBarCode = BarCode;
                            }
                            else
                            {
                                this.D_BarCode.Content = (BarCode.Length > 0) ? BarCode : "?";
                                this.D_MES.Content = "BarCode Read Error.";
                                bool _chk3 = SystemLT.M_Plc.CMD_Write_ModelLDSResult(0.2, 2);
                                SystemLT.Log.Info("[MES] LDS Result Set(1=OK,2=NG) = 2, 바코드 없음 = ");
                                SystemLT.CurMesSpec.SPEC.ImsiBarCode = "99999999999";
                            }
                            bool _chk4 = SystemLT.M_Plc.CMD_Write_ModelLDSBusy(0.2, 0);
                            SystemLT.Log.Info("[MES] LDS Busy Clear set = " + _chk1.ToString());
                            _LDS_Check_Running = false;
                        }
                        if ( (_Plc.Loading)&&(!SystemLT.IsAutoModeRun) )
                        {
                            if (AutomaticThread != null)
                            {
                                AutomaticThread.Abort();
                                AutomaticThread = null;
                                Delay(50);
                            }
                            if (MBCReadThread != null)
                            {
                                MBCReadThread.Abort();
                                MBCReadThread = null;
                                Delay(50);
                            }
                            if (_NGDisplay == true) NGHide();
                            if (_OKDisplay == true) OKHide();
                            SystemLT._MBCReadFunction.ECUSet(ref SystemLT.M_Ecu);
                            SystemLT._MBCReadFunction.RealTimeGraphViewSet(ref SystemLT.M_Daq.RTGraph);
                            SystemLT._MBCReadFunction.RequestEnd = false;         
                            SystemLT._MBCReadFunction.Pause = true;
                            SystemLT._MBCReadFunction.CheckTimeMs = 5;
                            MBCReadThread = new Thread(new ThreadStart(SystemLT._MBCReadFunction.DoWork));
                            //MBCReadThread.Priority = ThreadPriority.AboveNormal;
                            MBCReadThread.Start();
                            AutomaticThread = new Thread(new ThreadStart(SystemLT.DoAutoWork));
                            //AutomaticThread.Priority = ThreadPriority.AboveNormal;
                            AutomaticThread.Start();
                        }

                    }
                    #endregion
                    #region 자동쓰레드 프로그램이 동작중일때
                    if (SystemLT.IsAutoModeRun)
                    {
                        // 자동운전중 에러 확인
                    }
                    #endregion
                }
            }
            catch (Exception e1)
            {
                SystemLT.Log.Info("[MainControlTimer] Error = " + e1.Message);
            }
            #endregion
                   
            #region Log Date Check
            // Log 화일 보기 관련 날짜 변경 체크, 10초에 한번씩 화일 변경을 체크함.
            _LogDateCheckInterval++;
            if (_LogDateCheckInterval > 100)
            {
                string NowLogFilePath = System.Environment.CurrentDirectory + "\\Logs\\" + System.DateTime.Now.ToShortDateString() + ".log";
                bool ComResult = NowLogFilePath.Equals(CurLogFilePath, StringComparison.OrdinalIgnoreCase);
                if (!ComResult)
                {
                    SystemLT.Log.Info("[자동운전]일자변경으로 로그 기능을 다시 시작합니다.");
                    CurLogFilePath = NowLogFilePath;
                    FileMonitors.Clear();
                    var monitorViewModel = new FileMonitorViewModel(NowLogFilePath, GetFileNameForPath(NowLogFilePath), "UTF-8", false);
                    monitorViewModel.Renamed += MonitorViewModelOnRenamed;
                    monitorViewModel.Updated += MonitorViewModelOnUpdated;
                    FileMonitors.Add(monitorViewModel);
                    SelectedItem = monitorViewModel;
                }
                _LogDateCheckInterval = 0;
            }
            #endregion

            //Thread.Sleep(1);
        }
        private void PLC_RESET_Go()
        {
            SystemLT.M_Plc.CMD_ClearWorkArea(1.0);
            // 메모리 가베지 콜렉션 강제 수행
            System.GC.Collect(0, GCCollectionMode.Forced);
            System.GC.WaitForFullGCComplete();
            SystemLT.M_Daq.RTGraph.StopDAQSave();
            #region DAQ 에러시 재시작 관련
            // DAQ STOP 확인 재시작
            try
            {
                if (SystemLT.M_Daq.HSrunningTask == null)
                {

                    bool chk = SystemLT.M_Daq.Init(SystemLT.Log, 1000.0, SystemLT.CurConfig);
                    _RetryDAQCount++;
                    if (chk)
                    {
                        if (_RetryDAQCount > 10)
                        {
                            SystemLT.Log.Info("DAQ 재접속 횟수가 10회를 진행하였습니다.");
                            _RetryDAQCount = 0;
                        }
                    }
                }
            }
            catch (Exception e1)
            {

            }
            #endregion
        }
        private void PLC_LEAKTEST_Go()
        {
            // PLC 모드 : 리크 테스트 모드
        }
        private bool _PLC_Error_Display_Flicker = true;
        private Stopwatch _PLC_Error_StopWatch  = new Stopwatch();

        private void PLC_Error_Display()
        {
            if (_PLC_Error_Display_Flicker)
            {
                    _PLC_Error_StopWatch.Restart();
                    this.SB_MODE.Text          = "PLC ERROR";
                    this.SB_MODE.Background    = Brushes.Red;
                    _PLC_Error_Display_Flicker = false;             
            }
            else
            {
                if (_PLC_Error_StopWatch.ElapsedMilliseconds > 500)
                {
                    _PLC_Error_StopWatch.Restart();
                    this.SB_MODE.Text          = "PLC";
                    this.SB_MODE.Background    = _StripBar_BackColor;
                    _PLC_Error_Display_Flicker = true;
                }
            }
            // PLC 에러 발생 : PC화면에 에러 표시
            // Notify Msg.
            //var notificationManager = new NotificationManager();
            //notificationManager.Show(new NotificationContent
            //{
            //    Title = "iMEB LEAK TEST",
            //    Message = "PLC ERROR가 발생하였습니다.",
            //    Type = NotificationType.Error
            //});
        }
        #endregion
        #region UI 관련 타이머 및 기타
        private Stopwatch _UITimerOnly = new Stopwatch();
        private const int USAGEDISPLAY_TIME = 500;
        private bool _PLC_Trigger = false;
        private void UITimer_Tick(object sender, EventArgs e)
        {
            // Application Usage Display
            if (_UITimerOnly.ElapsedMilliseconds > USAGEDISPLAY_TIME)
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                Pb_CPU.Value    = (int)cpuCounter.NextValue();
                Pb_MEMORY.Value = (int)(ramCounter.NextValue() / 29.1);
                Pb_HDD.Value    = (int)((allDrives[1].TotalSize - allDrives[1].TotalFreeSpace) / (allDrives[1].TotalSize * 0.01));
                // PC ALIVE Signal
                if (SystemLT.PLC_isLive)
                {
                    bool chk = SystemLT.M_Plc.CMD_PCRunSignlaSet(_PLC_Trigger);
                    if (chk) _PLC_Trigger = !_PLC_Trigger;
                }
                _UITimerOnly.Restart();
            }
            // Status Bar Display
            M_PLC.PLCSTATE _Plc = SystemLT.M_Plc.PLCState;
            StripStatusUpdate_PLC(_Plc);

            // System Check
            bool DaqLive = SystemLT.DAQ_isLive;
            bool PLCLive = SystemLT.PLC_isLive;
            bool COMLive = SystemLT.COSMO_isLive;
            bool CANLive = SystemLT.ECU_isLive;

            StripStatusUpdate_Live(DaqLive, PLCLive, COMLive,CANLive);
            
            // COM Port Number Display            
            this.SB_COMPORT.Text = "COM" + SystemLT.CurConfig.COSMO_PortNumber.ToString();

            // Current Step Display
            //if (_Plc.Auto) StripStatusUpdate_Operate("IDLE");

            // Analog Input Data Display
            try
            {
                double Vc0, Vc1, Vac1, Vac2;
                Vc0 = SystemLT.M_Daq.RTGraph.CurCH[0];
                Vc1 = SystemLT.M_Daq.RTGraph.CurCH[1];
                Vac1 = SystemLT.M_Daq.RTGraph.CurCH[2];
                Vac2 = SystemLT.M_Daq.RTGraph.CurCH[3];
                this.L_CH0.Content = string.Format("{0:F3}", Vc0);
                this.L_CH1.Content = string.Format("{0:F3}", Vc1);
                this.L_VAC1.Content = string.Format("{0:F2}", Vac1);
                this.L_VAC2.Content = string.Format("{0:F2}", Vac2);
            }
            catch (Exception e1) { }
            if (SystemLT.M_Daq.IsDAQHighSpeedRun)
            {
                DAQ_Panel.Background = Brushes.LightGreen;
            }
            else
            {
                DAQ_Panel.Background = Brushes.Red;
            }
            Thread.Sleep(5);
        }

        private void StripStatusUpdate_Live(bool daqlive, bool plclive, bool comlive,bool canlive)
        {
            //Brush _daqcolor = this.SB_DAQ.Foreground;
            //Brush _plccolor = this.SB_PLC.Foreground;
            //Brush _comcolor = this.SB_COMPORT.Foreground;

            Brush _setdaq = Brushes.Green;
            Brush _setplc = Brushes.Green;
            Brush _setcom = Brushes.Green;
            Brush _setcan = Brushes.Green;
            if (daqlive) _setdaq = Brushes.Green;
            else         _setdaq = Brushes.DarkGray;

            if (plclive) _setplc = Brushes.Green;
            else         _setplc = Brushes.DarkGray;

            if (comlive) _setcom = Brushes.Green;
            else         _setcom = Brushes.DarkGray;

            if (canlive) _setcan = Brushes.Green;
            else         _setcan = Brushes.DarkGray;

            this.SB_DAQ.Foreground     = _setdaq;
            this.SB_PLC.Foreground     = _setplc;
            this.SB_COMPORT.Foreground = _setcom;
            this.SB_CAN.Foreground     = _setcan;
        }
        private void StripStatusUpdate_PLC(M_PLC.PLCSTATE plc)
        {
            string lastmsg = this.SB_MODE.Text;
            string setmsg = "COM ERROR";

            if (plc.Auto)     setmsg = "AUTO";
            if (plc.Manual)   setmsg = "MANUAL";
            if (plc.Loading)  setmsg = "LOADING";
            if (plc.Error)    setmsg = "PLC ERROR";
            if (plc.LeakMode) setmsg = setmsg + " - LEAKMODE";

            int comChk = string.Compare(lastmsg, setmsg, true);
            if (comChk != 0)
            {
                this.SB_MODE.Text = setmsg;
            }
            if (plc.Error)
            {
                this.SB_MODE.Background = Brushes.Red;
            }
            else
            {
                this.SB_MODE.Background = _StripBar_BackColor;
            }
        }
        #endregion
        #region 각 수동 버튼 및 추가 기능에 관련된 펑션(릴리즈 버전에서는 사용안함)
    

        #endregion
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Notify Msg.
            var notificationManager = new NotificationManager();
            notificationManager.Show(new NotificationContent
                            {
                                Title   = "iMEB LEAK TEST",
                                Message = "iMEB 리크 시험 프로그램이 시작되었습니다.",
                                Type    = NotificationType.Information
                            });


        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            // Notify Msg.
            var notificationManager = new NotificationManager();
            notificationManager.Show(new NotificationContent
            {
                Title   = "iMEB LEAK TEST",
                Message = "iMEB 리크 시험 프로그램을 종료합니다.",
                Type    = NotificationType.Information
            });
            // 메인 폼 클로징시 정리 사항
            var res = MessageBox.Show("iMEB Leak Test 프로그램을 종료할까요?", "프로그램 종료", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (res == MessageBoxResult.No)
            {
                notificationManager.Show(new NotificationContent
                {
                    Title   = "iMEB LEAK TEST",
                    Message = "iMEB 리크 시험 프로그램을 종료 취소합니다.",
                    Type    = NotificationType.Information
                });
                e.Cancel = true;
            }
            else
            {
                if (cpuCounter != null) cpuCounter.Dispose();
                if (ramCounter != null) ramCounter.Dispose();
                // 만약 다른 프로세서 실행중이면 종료 후 어플리케이션 종료함.
                if (AutomaticThread!=null)
                {
                    SystemLT.RequestStop();
                    Delay(50);
                }
                SystemLT.CurMesSpec.SaveConfigData();
               
                SystemLT.M_Plc.Dispose();
                SystemLT.M_Daq.Dispose();
                SystemLT.M_Cosmo.Dispose();
                SystemLT.M_MesLds.Dispose();
                
                Application.Current.Shutdown();
            }
        }



        private void Test_Lamp_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Border ChkBorder = (Border)sender;
            string selectedBorder = ChkBorder.Name;
            switch(selectedBorder)
            {
                case "Test_Lamp_0": // 외부 리크 테스트
                    this.MainTab.SelectedIndex = 0;                    
                    break;
                case "Test_Lamp_1": // 외부 리크 테스트 1.5
                    //this.MainTab.SelectedIndex = 1;
                    break;
                case "Test_Lamp_1_1": // 외부 리크 테스트 5.0
                    this.MainTab.SelectedIndex = 11;
                    break;
                case "Test_Lamp_2": // ECU DTC
                    this.MainTab.SelectedIndex = 2;
                    break;
                case "Test_Lamp_3": // ECU MOTOR
                    this.MainTab.SelectedIndex = 3;
                    break;
                case "Test_Lamp_3_1": // ECU MOTOR
                    this.MainTab.SelectedIndex = 12;
                    break;
                case "Test_Lamp_4": // ECU MOTOR
                    this.MainTab.SelectedIndex = 4;
                    break;
                case "Test_Analay":
                    this.MainTab.SelectedIndex = 7;
                    break;
                case "Test_Analay1":
                    this.MainTab.SelectedIndex = 8;
                    break;
            }
        }

        private void SystemConfig_Click(object sender, RoutedEventArgs e)
        {
            // 상단 관리자 메뉴 클릭
            if (SystemLT.CurrentMode == LeakTest.MODE.AUTO)
            {
                MessageBox.Show("자동운전중에는 시스템 설정이 불가합니다. 매뉴얼 상태에서 다시 하십시오.", "알림", MessageBoxButton.OK);
                return;
            }

            // 메인 메뉴 상단 우측 - 시스템 설정 버튼 
            if (ThisSubForm == null)
            {
                ThisSubForm = new SystemEnviroment(SystemLT);
            }
            ThisSubForm.Owner = this;
            ThisSubForm.Closed += (o, args) => ThisSubForm = null;
            ThisSubForm.Left = this.ActualWidth / 4.0;  // this.Left + this.ActualWidth / 2.0;
            ThisSubForm.Top = this.ActualHeight / 4.0; // this.Top + this.ActualHeight / 2.0;
            ThisSubForm.Width = 800;
            ThisSubForm.Height = 600;
            ThisSubForm.Show();
        }

        private DateTime _Test1 = DateTime.Now;
        private void TouchKEY_Click(object sender, RoutedEventArgs e)
        {
            //TimeSpan _ts =  DateTime.Now - _Test1;
            //this.TestLabel.Content = string.Format("{0:F3}", SystemLT.M_Daq.RTGraph.CurCH[0]);
            //this.TestLabel.Content = string.Format("{0:F3}", _ts.TotalSeconds);
            // 터치 키보드 버튼
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)(sender);
            var fileMonitorViewModel = ((FileMonitorViewModel)textBox.DataContext);

            if (fileMonitorViewModel != null)
            {
                if (!fileMonitorViewModel.IsFrozen)
                {
                    textBox.ScrollToEnd();
                }
                textBox.ScrollToEnd();
            }
        }

        private void Btn_Log_Click(object sender, RoutedEventArgs e)
        {
            // 메인 윈도루 - 로그 버튼 클릭시
            this.MainTab.SelectedIndex = 5;
        }

        private void Btn_RESULT_Click(object sender, RoutedEventArgs e)
        {
            // 메인 윈도우 - 결과 버튼 클릭시
            this.MainTab.SelectedIndex = 6;
        }

        private void Btn_QUIT_Click(object sender, RoutedEventArgs e)
        {
            CancelEventArgs ce = new CancelEventArgs();

            ce.Cancel = true;
            this.MetroWindow_Closing(sender, ce);

        }

        private void B_DiagOpen_Click(object sender, RoutedEventArgs e)
        {

        }

        private void B_DiagClose_Click(object sender, RoutedEventArgs e)
        {

        }



        private void B_PSUPower_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Btn_USER_Click(object sender, RoutedEventArgs e)
        {
            LogIn _LogIn = new LogIn(SystemLT) { Owner = this };
            _LogIn.ShowMaxRestoreButton = false;
            _LogIn.ShowMinButton = false;
            _LogIn.ShowDialog();

        }

        private void Title_Image_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void EcuDtcGrid_SourceUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {

        }

        
        #region MBC 데이터 읽기 및 그래프 표시 전용
        private ChartCollection<Point> MBCData1 = new ChartCollection<Point>(20000);
        private Point[] _MBCData1 = new Point[20000];
        private Point[] MBCData = new Point[20000];
        private double[,] IntensityData = new double[360, 9];
        private int[] LsatSameAngleDataIndex = new int[360];
 
        private bool MBC_FileToChart(string fileaname,double masterLength)
        {

            int    PointIndex = 0;
            double Time       = 0.0;
            double Position   = 0.0;
            double Ampera     = 0.0;
            double PosToAngle = 0.0;
            double Quotient   = 0.0;
            double Calc       = 0.0;
            double TempPos    = 0.0;
            //string line       = "";
            char   sp         =',';
            bool   FileConversionOK = false;

            IntensityData.Initialize();
            MBCData1.Clear();
            _MBCData1.Initialize();

            for (int i = 0; i < 360; i++ )
            {
                IntensityData[i, 0] = 0.0;
            }
            Stopwatch n1 = new Stopwatch();
            n1.Restart();
            string[] AllLines = new string[600000];
            AllLines = File.ReadAllLines(fileaname);
            long n1c = n1.ElapsedMilliseconds;
            n1.Stop();
            MBCReadDataCount.Content = "데이터읽기 - "+string.Format("{0:F3}sec",n1c/1000.0);
            #region 일반 화일 릭기 모드 
            n1.Restart();
            if (AllLines.Length <= 0) return false;
            if ( (!AllLines[0].Contains("ECU"))&&(!AllLines[1].Contains("ECU")) )
            {
                MBCReadDataCount.Content = "화일선택오류";
                return false;
            }
            for (int i = 0; i < AllLines.Length; i++)
            {
                string[] spStr = AllLines[i].Split(sp);
                if (spStr.Length == 3)
                {
                    double.TryParse(spStr[0], out Time);
                    double.TryParse(spStr[1], out Ampera);
                    double.TryParse(spStr[2], out Position);

                    Calc = Position / masterLength;
                    Quotient = System.Math.Truncate(Calc);
                    if (Quotient > 0)
                    {
                        TempPos = Position - (Quotient * masterLength);
                        PosToAngle = (TempPos / masterLength) * 360.0;
                    }
                    else
                    {
                        PosToAngle = (Position / masterLength) * 360.0;
                    }
                    _MBCData1[PointIndex] = new Point(PosToAngle, Ampera);
                    int AngleToInt = (int)PosToAngle;
                    if (AngleToInt < 0) AngleToInt = 360 + AngleToInt;
                    double NowValue = IntensityData[AngleToInt, (int)Quotient];
                    if (NowValue != 0.0) IntensityData[AngleToInt, (int)Quotient] = (Math.Abs(Ampera) + NowValue) / 2.0;
                    else IntensityData[AngleToInt, (int)Quotient] = Math.Abs(Ampera);
                    PointIndex++;
                    //MBCReadDataCount.Content = string.Format("{0:D}", PointIndex);
                    //Delay(1);
                }
            }
            n1c = n1.ElapsedMilliseconds;
            n1.Stop();
            MBCReadDataCount.Content = "데이터변환 - " + string.Format("{0:F3}sec", n1c / 1000.0);
            #endregion
            n1.Restart();
            MBCData1.Append(_MBCData1);
            PolarGraph1.DataSource = MBCData1;
            IntensityGraph1.DataSource = IntensityData;

            Cir_Radius.Radius = 5.0;
            IntensityGraph1.Refresh();
            PolarGraph1.Refresh();
            n1c = n1.ElapsedMilliseconds;
            n1.Stop();
            MBCReadDataCount.Content = "그래프변환 - " + string.Format("{0:F3}sec", n1c / 1000.0);
            Delay(200);
            MBCReadDataCount.Content = string.Format("{0:D}-END", PointIndex);

            return FileConversionOK;
        }
        private bool MBC_AnalysisGraph_Refresh()
        {
            return true;
        }
        #endregion
        private void MBCFileLoad_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = "";
            double _MasterLength = 0.0;
            _MasterLength = this.MasterLength.Value;
            // 모터 리크 테스트 결과 화일 불러오기
            Microsoft.Win32.OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt       = "*.txt";
            dlg.Multiselect      = false;
            dlg.Filter           = "MBC Data Files (*.txt)|*.txt";
            dlg.InitialDirectory = SystemLT.IMSI_DataFullPath;

            Nullable<bool> result = dlg.ShowDialog();

            if(result==true)
            {
                selectedFile = dlg.FileName;
                if (MBC_FileToChart(selectedFile, _MasterLength)) MBC_AnalysisGraph_Refresh();
            }
        }

        private void MBC_Scale_3_Click(object sender, RoutedEventArgs e)
        {
            Cir_Radius.Radius = 3.0;
            PolarGraph1.Refresh();
        }

        private void MBC_Scale_5_Click(object sender, RoutedEventArgs e)
        {
            Cir_Radius.Radius = 5.0;
            PolarGraph1.Refresh();
        }

        private void MBC_Scale_10_Click(object sender, RoutedEventArgs e)
        {
            Cir_Radius.Radius = 10.0;
            PolarGraph1.Refresh();
        }
        #region MBC 데이터 읽기 및 그래프 표시 전용 - 분석 2번째꺼용
        private const int MAX_MBC_DATA_COUNTS=20000;
        private ChartCollection<double, double>[] MBCData_FWD = new[] {new ChartCollection<double,double>(MAX_MBC_DATA_COUNTS),    // plot : 전류(단방향)
                                                                       new ChartCollection<double,double>(MAX_MBC_DATA_COUNTS)     // plot : 이동 평균                                                                                                                              
                                                                       };
        private double[] MBCData_ALL_Data       = new double[50000];
        private double[] MBCData_ALL_FWD        = new double[20000];
        private double[] MBCData_ALL_BWD        = new double[20000];
        private double[] MBCData_ALL_Filter_FWD = new double[20000];
        private double[] MBCData_ALL_Filter_BWD = new double[20000];

        private Point[] MBCData_FWD_Tmp  = new Point[MAX_MBC_DATA_COUNTS];
        private Point[] MBCData_FWD_Tmp1 = new Point[MAX_MBC_DATA_COUNTS];
        private Point[] MBCData_FWD_Tmp2 = new Point[MAX_MBC_DATA_COUNTS];
        private Point[] MBCData_BWD_Tmp  = new Point[MAX_MBC_DATA_COUNTS];
        private Point[] MBCData_BWD_Tmp1 = new Point[MAX_MBC_DATA_COUNTS];
        private Point[] MBCData_BWD_Tmp2 = new Point[MAX_MBC_DATA_COUNTS];
        private ChartCollection<double, double>[] MBCData_BWD = new[] {new ChartCollection<double,double>(MAX_MBC_DATA_COUNTS),    // plot : 전류(단방향)
                                                                       new ChartCollection<double,double>(MAX_MBC_DATA_COUNTS)    // plot : 이동 평균                                                                       
                                                                     };
        private const int PART_COUNTS       = 8;
        private const int PART_DATACOUNTS   = 2000;
        private Point[,]  MBCData_FWD_Sub   = new Point[PART_COUNTS, PART_DATACOUNTS];
        private Point[,]  MBCData_BWD_Sub   = new Point[PART_COUNTS, PART_DATACOUNTS];
        private int[]     FWD_Sub_DataCount = new int[PART_COUNTS];
        private int[]     BWD_Sub_DataCount = new int[PART_COUNTS];

        private ChartCollection<double, double>[] MBCData_FWDSubT = new[] { new ChartCollection<double,double>(PART_DATACOUNTS),   
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),   
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),   
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),    
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),   
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),   
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),  
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS)    
                                                                          };
        private ChartCollection<double, double>[] MBCData_BWDSubT = new[] { new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS),
                                                                            new ChartCollection<double,double>(PART_DATACOUNTS)
                                                                          };

        private ChartCollection<double, double>[] MBCData_SUBFWD1 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD2 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD3 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD4 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD5 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD6 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD7 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBFWD8 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };

        private ChartCollection<double, double>[] MBCData_SUBBWD1 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD2 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD3 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD4 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD5 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD6 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD7 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };
        private ChartCollection<double, double>[] MBCData_SUBBWD8 = new[] { new ChartCollection<double, double>(PART_DATACOUNTS) };

        private double masterLength1        = 0.0;
        private double masterStartPos       = 0.0;
        private double masterEndPos         = 0.0;
        private int    masterMovingAvg      = 0;
        private int    masterFWDIndexCounts = 0;
        private int    masterBWDIndexCounts = 0;
        private string LastselectedFile     = "";

        private bool MBC_FileToChart1(string fileaname)
        {
            double Time              = 0.0;
            double Position          = 0.0;
            double Ampera            = 0.0;
            //string line       = "";
            char    sp               = ',';
            bool    FileConversionOK = false;

            string FWDMsg = "";
            string BWDMsg = "";

            
            string[] AllLines = new string[300000];
            AllLines = File.ReadAllLines(fileaname);          
            #region 일반 화일 읽기 모드         
            if (AllLines.Length <= 0) return false;
            if ((!AllLines[0].Contains("ECU")) && (!AllLines[1].Contains("ECU")))
            {                
                return false;
            }

            // filteredwave = besselLowpass.FilterData(waveform);

            masterFWDIndexCounts = 0;
            masterBWDIndexCounts = 0;
            MBCData_FWD[0].Clear();
            MBCData_FWD[1].Clear();
           
            MBCData_BWD[0].Clear();
            MBCData_BWD[1].Clear();
          
            bool _FWDEnd = false;
            bool _BWDEnd = false;
            int _allDataIndex = 0;
            for (int i=0; i< MAX_MBC_DATA_COUNTS; i++)
            {
                MBCData_FWD_Tmp[i] = new Point(0.0, 0.0);
                MBCData_BWD_Tmp[i] = new Point(0.0, 0.0);
            }
            // 데이터 정방향/역방향 읽기
            for (int i = 0; i < AllLines.Length; i++)
            {
                string[] spStr = AllLines[i].Split(sp);
                if (spStr.Length == 3)
                {
                    double.TryParse(spStr[0], out Time);
                    double.TryParse(spStr[1], out Ampera);
                    double.TryParse(spStr[2], out Position);
                    if ((Position >= masterStartPos )&&(Position<=masterEndPos)&&(!_FWDEnd))
                    {
                        MBCData_FWD_Tmp[masterFWDIndexCounts] = new Point(Position, Ampera);
                        MBCData_ALL_FWD[masterFWDIndexCounts] = Ampera;
                        masterFWDIndexCounts++;
                    }
                    if (Position >= masterEndPos)
                    {
                        if(!_FWDEnd) _FWDEnd = true;
                    }

                    if ((_FWDEnd)&&(Position<= masterEndPos)&&(Position >= masterStartPos))
                    {
                        MBCData_BWD_Tmp[masterBWDIndexCounts] = new Point(Position, Ampera);
                        MBCData_ALL_BWD[masterBWDIndexCounts] = Ampera;
                        masterBWDIndexCounts++;
                    }
                    MBCData_ALL_Data[_allDataIndex] = Ampera;
                    _allDataIndex++;
                }
            }

            #region 데이터 정렬
            // 정방향/역방향 데이터 정렬
            if ((masterFWDIndexCounts>0)&&(masterBWDIndexCounts>0))
            {
                MBCData_FWD_Tmp1 = MBCData_FWD_Tmp.Where(x => x.X >= masterStartPos).OrderBy(x => x.X).ToArray();
                MBCData_BWD_Tmp1 = MBCData_BWD_Tmp.Where(x => x.X >= masterStartPos).OrderBy(x => x.X).ToArray();
                GetMovingAverager(MBCData_FWD_Tmp1, ref MBCData_FWD_Tmp2, masterFWDIndexCounts, masterMovingAvg);
                GetMovingAverager(MBCData_BWD_Tmp1, ref MBCData_BWD_Tmp2, masterBWDIndexCounts, masterMovingAvg);
            }
            #endregion

            // graph 표시
            for (int i=0; i< masterFWDIndexCounts; i++)
            {
                MBCData_FWD[0].Append(MBCData_FWD_Tmp1[i].X, MBCData_FWD_Tmp1[i].Y);
                MBCData_FWD[1].Append(MBCData_FWD_Tmp2[i].X, MBCData_FWD_Tmp2[i].Y);            
            }            
            Graph_FWDMain.DataSource = MBCData_FWD;
            Graph_FWDMain.Refresh();
            Delay(10);
            // graph 표시
            for (int i = 0; i < masterBWDIndexCounts; i++)
            {
                MBCData_BWD[0].Append(MBCData_BWD_Tmp1[i].X, MBCData_BWD_Tmp1[i].Y);
                MBCData_BWD[1].Append(MBCData_BWD_Tmp2[i].X, MBCData_BWD_Tmp2[i].Y);               
            }
            Graph_BWDMain.DataSource = MBCData_BWD;
            Graph_BWDMain.Refresh();
            Delay(10);

            for (int i2=0; i2< PART_COUNTS; i2++)
            {
                FWD_Sub_DataCount[i2] = 0;
                BWD_Sub_DataCount[i2] = 0;
            }
            // 서브 그래프 표시

                for (int fwd_index=0; fwd_index< masterFWDIndexCounts; fwd_index++)
                {
                    double pos = MBCData_FWD_Tmp2[fwd_index].X;
                    double amp = MBCData_FWD_Tmp2[fwd_index].Y;

                    int datalocation = (int)(System.Math.Truncate(pos / masterLength1));
                    if (datalocation < PART_COUNTS)
                    {
                        double newPosition = pos - (double)(datalocation * masterLength1);
                        MBCData_FWD_Sub[datalocation, FWD_Sub_DataCount[datalocation]] = new Point(newPosition, amp);
                        FWD_Sub_DataCount[datalocation]++;
                    }
                }
          

            MBCData_SUBFWD1[0].Clear();
            MBCData_SUBFWD2[0].Clear();
            MBCData_SUBFWD3[0].Clear();
            MBCData_SUBFWD4[0].Clear();
            MBCData_SUBFWD5[0].Clear();
            MBCData_SUBFWD6[0].Clear();
            MBCData_SUBFWD7[0].Clear();
            MBCData_SUBFWD8[0].Clear();

            MBCData_FWDSubT[0].Clear();
            MBCData_FWDSubT[1].Clear();
            MBCData_FWDSubT[2].Clear();
            MBCData_FWDSubT[3].Clear();
            MBCData_FWDSubT[4].Clear();
            MBCData_FWDSubT[5].Clear();
            MBCData_FWDSubT[6].Clear();
            MBCData_FWDSubT[7].Clear();

            MBCData_BWDSubT[0].Clear();
            MBCData_BWDSubT[1].Clear();
            MBCData_BWDSubT[2].Clear();
            MBCData_BWDSubT[3].Clear();
            MBCData_BWDSubT[4].Clear();
            MBCData_BWDSubT[5].Clear();
            MBCData_BWDSubT[6].Clear();
            MBCData_BWDSubT[7].Clear();

            #region 서브 그래프
            double min = 100.0;
            double max = -100.0;
            double checkValue = 0;
            double deltaMaxMin = 0.0;
            double SubAverage = 0.0;
            int SubCount = 0;

            double Diff_FWD  = FWD_Diff.Value;  // 평균의 차
            double Diff_BWD  = BWD_Diff.Value;
            Diff_FWD         = SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Max;
            Diff_BWD         = SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Max;
            bool[] FWD_Check = new bool[8];
            bool[] BWD_Check = new bool[8];

            FWD_Check[0] = (bool)FC_1.IsChecked;
            FWD_Check[1] = (bool)FC_2.IsChecked;
            FWD_Check[2] = (bool)FC_3.IsChecked;
            FWD_Check[3] = (bool)FC_4.IsChecked;
            FWD_Check[4] = (bool)FC_5.IsChecked;
            FWD_Check[5] = (bool)FC_6.IsChecked;
            FWD_Check[6] = (bool)FC_7.IsChecked;
            FWD_Check[7] = (bool)FC_8.IsChecked;
            BWD_Check[0] = (bool)BC_1.IsChecked;
            BWD_Check[1] = (bool)BC_2.IsChecked;
            BWD_Check[2] = (bool)BC_3.IsChecked;
            BWD_Check[3] = (bool)BC_4.IsChecked;
            BWD_Check[4] = (bool)BC_5.IsChecked;
            BWD_Check[5] = (bool)BC_6.IsChecked;
            BWD_Check[6] = (bool)BC_7.IsChecked;
            BWD_Check[7] = (bool)BC_8.IsChecked;

            bool _AvgCheck = true;  // 전체 구간에서 선택된 영역에서 평균값이 MES 스펙에 만족하는지 여부를 확인

            for (int i=0; i< FWD_Sub_DataCount[0];i++)
            {
                checkValue = MBCData_FWD_Sub[0, i].Y;
                MBCData_SUBFWD1[0].Append(MBCData_FWD_Sub[0, i].X, MBCData_FWD_Sub[0, i].Y);
                MBCData_FWDSubT[0].Append(MBCData_FWD_Sub[0, i].X, MBCData_FWD_Sub[0, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg +"1:D="+ string.Format("{0:F2}", deltaMaxMin) +"A="+ string.Format("{0:F2}", SubAverage) +"m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[0])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub1.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub1.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_FWD_Sub1.PlotAreaBackground = Brushes.White;
            }

            double _FirstDiff = 0.0;
            min        = 100.0;
            max        = -100.0;
            SubAverage = 0.0;
            SubCount   = 0;
            for (int i = 0; i < FWD_Sub_DataCount[1]; i++)
            {
                checkValue = MBCData_FWD_Sub[1, i].Y;
                MBCData_SUBFWD2[0].Append(MBCData_FWD_Sub[1, i].X, MBCData_FWD_Sub[1, i].Y);
                MBCData_FWDSubT[1].Append(MBCData_FWD_Sub[1, i].X, MBCData_FWD_Sub[1, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage  = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            _FirstDiff = deltaMaxMin;  // 화면 결과 표시용 첫번째꺼만..........
            FWDMsg = FWDMsg + "2:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[1])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub2.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub2.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_FWD_Sub2.PlotAreaBackground = Brushes.White;
            }


            min        = 100.0;
            max        = -100.0;
            SubAverage = 0.0;
            SubCount   = 0;
            for (int i = 0; i < FWD_Sub_DataCount[2]; i++)
            {
                checkValue = MBCData_FWD_Sub[2, i].Y;
                MBCData_SUBFWD3[0].Append(MBCData_FWD_Sub[2, i].X, MBCData_FWD_Sub[2, i].Y);
                MBCData_FWDSubT[2].Append(MBCData_FWD_Sub[2, i].X, MBCData_FWD_Sub[2, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage  = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg + "3:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[2])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub3.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub3.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_FWD_Sub3.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < FWD_Sub_DataCount[3]; i++)
            {
                checkValue = MBCData_FWD_Sub[3, i].Y;
                MBCData_SUBFWD4[0].Append(MBCData_FWD_Sub[3, i].X, MBCData_FWD_Sub[3, i].Y);
                MBCData_FWDSubT[3].Append(MBCData_FWD_Sub[3, i].X, MBCData_FWD_Sub[3, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg + "4:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[3])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub4.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub4.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_FWD_Sub4.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < FWD_Sub_DataCount[4]; i++)
            {
                checkValue = MBCData_FWD_Sub[4, i].Y;
                MBCData_SUBFWD5[0].Append(MBCData_FWD_Sub[4, i].X, MBCData_FWD_Sub[4, i].Y);
                MBCData_FWDSubT[4].Append(MBCData_FWD_Sub[4, i].X, MBCData_FWD_Sub[4, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg + "5:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[4])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub5.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub5.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
                }
            else
            {
                Graph_FWD_Sub5.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < FWD_Sub_DataCount[5]; i++)
            {
                checkValue = MBCData_FWD_Sub[5, i].Y;
                MBCData_SUBFWD6[0].Append(MBCData_FWD_Sub[5, i].X, MBCData_FWD_Sub[5, i].Y);
                MBCData_FWDSubT[5].Append(MBCData_FWD_Sub[5, i].X, MBCData_FWD_Sub[5, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg + "6:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[5])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub6.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub6.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
                }
            else
            {
                Graph_FWD_Sub6.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < FWD_Sub_DataCount[6]; i++)
            {
                checkValue = MBCData_FWD_Sub[6, i].Y;
                MBCData_SUBFWD7[0].Append(MBCData_FWD_Sub[6, i].X, MBCData_FWD_Sub[6, i].Y);
                MBCData_FWDSubT[6].Append(MBCData_FWD_Sub[6, i].X, MBCData_FWD_Sub[6, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg + "7:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[6])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub7.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub7.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_FWD_Sub7.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < FWD_Sub_DataCount[7]; i++)
            {
                checkValue = MBCData_FWD_Sub[7, i].Y;
                MBCData_SUBFWD8[0].Append(MBCData_FWD_Sub[7, i].X, MBCData_FWD_Sub[7, i].Y);
                MBCData_FWDSubT[7].Append(MBCData_FWD_Sub[7, i].X, MBCData_FWD_Sub[7, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            FWDMsg = FWDMsg + "8:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (FWD_Check[7])
            {
                if (deltaMaxMin <= Diff_FWD) Graph_FWD_Sub8.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_FWD_Sub8.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_FWD_Sub8.PlotAreaBackground = Brushes.White;
            }


            TB_FWD.Text = FWDMsg;

            Graph_FWD_Sub1.DataSource = MBCData_SUBFWD1;
            Graph_FWD_Sub2.DataSource = MBCData_SUBFWD2;
            Graph_FWD_Sub3.DataSource = MBCData_SUBFWD3;
            Graph_FWD_Sub4.DataSource = MBCData_SUBFWD4;
            Graph_FWD_Sub5.DataSource = MBCData_SUBFWD5;
            Graph_FWD_Sub6.DataSource = MBCData_SUBFWD6;
            Graph_FWD_Sub7.DataSource = MBCData_SUBFWD7;
            Graph_FWD_Sub8.DataSource = MBCData_SUBFWD8;

            Graph_FWDMainTotal.DataSource = MBCData_FWDSubT;
            Graph_FWDMainTotal.Refresh();

            Graph_FWD_Sub1.Refresh();
            Graph_FWD_Sub2.Refresh();
            Graph_FWD_Sub3.Refresh();
            Graph_FWD_Sub4.Refresh();
            Graph_FWD_Sub5.Refresh();
            Graph_FWD_Sub6.Refresh();
            Graph_FWD_Sub7.Refresh();
            Graph_FWD_Sub8.Refresh();
            #endregion

            for (int bwd_index = 0; bwd_index < masterBWDIndexCounts; bwd_index++)
                {
                    double pos = MBCData_BWD_Tmp2[bwd_index].X;
                    double amp = MBCData_BWD_Tmp2[bwd_index].Y;

                    int datalocation = (int)(System.Math.Truncate(pos / masterLength1));
                    if (datalocation < PART_COUNTS)
                    {
                        double newPosition = pos - (double)(datalocation * masterLength1);
                        MBCData_BWD_Sub[datalocation, BWD_Sub_DataCount[datalocation]] = new Point(newPosition, amp);
                        BWD_Sub_DataCount[datalocation] = BWD_Sub_DataCount[datalocation] + 1;
                    }
                }

            MBCData_SUBBWD1[0].Clear();
            MBCData_SUBBWD2[0].Clear();
            MBCData_SUBBWD3[0].Clear();
            MBCData_SUBBWD4[0].Clear();
            MBCData_SUBBWD5[0].Clear();
            MBCData_SUBBWD6[0].Clear();
            MBCData_SUBBWD7[0].Clear();
            MBCData_SUBBWD8[0].Clear();

            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[0]; i++)
            {
                checkValue = MBCData_BWD_Sub[0, i].Y;
                MBCData_SUBBWD1[0].Append(MBCData_BWD_Sub[0, i].X, MBCData_BWD_Sub[0, i].Y);
                MBCData_BWDSubT[0].Append(MBCData_BWD_Sub[0, i].X, MBCData_BWD_Sub[0, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "1:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[0])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub1.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub1.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_BWD_Sub1.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[1]; i++)
            {
                checkValue = MBCData_BWD_Sub[1, i].Y;
                MBCData_SUBBWD2[0].Append(MBCData_BWD_Sub[1, i].X, MBCData_BWD_Sub[1, i].Y);
                MBCData_BWDSubT[1].Append(MBCData_BWD_Sub[1, i].X, MBCData_BWD_Sub[1, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "2:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[1])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub2.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub2.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_BWD_Sub2.PlotAreaBackground = Brushes.White;
            }

            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[2]; i++)
            {
                checkValue = MBCData_BWD_Sub[2, i].Y;
                MBCData_SUBBWD3[0].Append(MBCData_BWD_Sub[2, i].X, MBCData_BWD_Sub[2, i].Y);
                MBCData_BWDSubT[2].Append(MBCData_BWD_Sub[2, i].X, MBCData_BWD_Sub[2, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "3:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[2])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub3.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub3.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_BWD_Sub3.PlotAreaBackground = Brushes.White;
            }



            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[3]; i++)
            {
                checkValue = MBCData_BWD_Sub[3, i].Y;
                MBCData_SUBBWD4[0].Append(MBCData_BWD_Sub[3, i].X, MBCData_BWD_Sub[3, i].Y);
                MBCData_BWDSubT[3].Append(MBCData_BWD_Sub[3, i].X, MBCData_BWD_Sub[3, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "4:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[3])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub4.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub4.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_BWD_Sub4.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[4]; i++)
            {
                checkValue = MBCData_BWD_Sub[4, i].Y;
                MBCData_SUBBWD5[0].Append(MBCData_BWD_Sub[4, i].X, MBCData_BWD_Sub[4, i].Y);
                MBCData_BWDSubT[4].Append(MBCData_BWD_Sub[4, i].X, MBCData_BWD_Sub[4, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "5:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[4])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub5.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub5.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
                }
            else
            {
                Graph_BWD_Sub5.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[5]; i++)
            {
                checkValue = MBCData_BWD_Sub[5, i].Y;
                MBCData_SUBBWD6[0].Append(MBCData_BWD_Sub[5, i].X, MBCData_BWD_Sub[5, i].Y);
                MBCData_BWDSubT[5].Append(MBCData_BWD_Sub[5, i].X, MBCData_BWD_Sub[5, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "6:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[5])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub6.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub6.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
                }
            else
            {
                Graph_BWD_Sub6.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[6]; i++)
            {
                checkValue = MBCData_BWD_Sub[6, i].Y;
                MBCData_SUBBWD7[0].Append(MBCData_BWD_Sub[6, i].X, MBCData_BWD_Sub[6, i].Y);
                MBCData_BWDSubT[6].Append(MBCData_BWD_Sub[6, i].X, MBCData_BWD_Sub[6, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "7:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[6])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub7.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub7.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
                }
            else
            {
                Graph_BWD_Sub7.PlotAreaBackground = Brushes.White;
            }


            min = 100.0;
            max = -100.0;
            SubAverage = 0.0;
            SubCount = 0;
            for (int i = 0; i < BWD_Sub_DataCount[7]; i++)
            {
                checkValue = MBCData_BWD_Sub[7, i].Y;
                MBCData_SUBBWD8[0].Append(MBCData_BWD_Sub[7, i].X, MBCData_BWD_Sub[7, i].Y);
                MBCData_BWDSubT[7].Append(MBCData_BWD_Sub[7, i].X, MBCData_BWD_Sub[7, i].Y);
                if (min > checkValue) min = checkValue;
                if (max < checkValue) max = checkValue;
                SubAverage = SubAverage + checkValue;
                SubCount++;
            }
            SubAverage = SubAverage / SubCount;
            deltaMaxMin = Math.Abs(max - min);
            BWDMsg = BWDMsg + "8:D=" + string.Format("{0:F2}", deltaMaxMin) + "A=" + string.Format("{0:F2}", SubAverage) + "m=" + string.Format("{0:F2}", min) + " M=" + string.Format("{0:F2}", max) + "\r\n";
            if (BWD_Check[7])
            {
                if (deltaMaxMin <= Diff_BWD) Graph_BWD_Sub8.PlotAreaBackground = Brushes.SlateBlue;
                else { Graph_BWD_Sub8.PlotAreaBackground = Brushes.Orange; _AvgCheck = false; }
            }
            else
            {
                Graph_BWD_Sub8.PlotAreaBackground = Brushes.White;
            }


            TB_BWD.Text = BWDMsg;

            Graph_BWD_Sub1.DataSource = MBCData_SUBBWD1;
            Graph_BWD_Sub2.DataSource = MBCData_SUBBWD2;
            Graph_BWD_Sub3.DataSource = MBCData_SUBBWD3;
            Graph_BWD_Sub4.DataSource = MBCData_SUBBWD4;
            Graph_BWD_Sub5.DataSource = MBCData_SUBBWD5;
            Graph_BWD_Sub6.DataSource = MBCData_SUBBWD6;
            Graph_BWD_Sub7.DataSource = MBCData_SUBBWD7;
            Graph_BWD_Sub8.DataSource = MBCData_SUBBWD8;

            Graph_BWDMainTotal.DataSource = MBCData_BWDSubT;
            Graph_BWDMainTotal.Refresh();

            Graph_BWD_Sub1.Refresh();
            Graph_BWD_Sub2.Refresh();
            Graph_BWD_Sub3.Refresh();
            Graph_BWD_Sub4.Refresh();
            Graph_BWD_Sub5.Refresh();
            Graph_BWD_Sub6.Refresh();
            Graph_BWD_Sub7.Refresh();
            Graph_BWD_Sub8.Refresh();
            #endregion
            if (_AvgCheck)
            {
                Test_Analay1.Background = Brushes.Green;
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("모터 섹션별 평균값 테스트",
                                                                    SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Min,
                                                                    _FirstDiff,
                                                                    SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Max,
                                                                    "A", "OK", "첫번째 섹션의 평균값 차이"));
            }
            else
            {
                Test_Analay1.Background = Brushes.Red;
                SystemLT.CurTestResult.Add(new iMEB.TestResultTable("모터 섹션별 평균값 테스트",
                                                    SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Min,
                                                    SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Max+1.0,
                                                    SystemLT.CurMesSpec.SPEC.LeakII_Motor_C_Max,
                                                    "A", "NG", "해당 섹션의 평균 차이를 확인 하십시오"));
            }
            return FileConversionOK;
        }
        private bool MBC_AnalysisGraph_Refresh1()
        {
            return true;
        }
        private int GetMovingAverager(Point[] data,ref Point[] averages, int size, int periods)
        {


            double sum = 0;
            for (int i = 0; i < size; i++)
            {
                if (i < periods)
                {
                    sum = sum +  data[i].Y;
                    averages[i].Y =  (i!=0) ? sum/(i+1):data[0].Y;
                }
                else
                {
                    sum = sum - data[i - periods].Y + data[i].Y;
                    averages[i].Y = sum / (double)periods;
                }
                averages[i].X = data[i].X;
            }
            return (size - periods + 1 > 0) ? size - periods + 1 : 0;

        }
        #endregion


        private void AN2MBCFileOpen_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = "";   
            // 모터 리크 테스트 결과 화일 불러오기
            Microsoft.Win32.OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = "*.txt";
            dlg.Multiselect = false;
            dlg.Filter = "MBC Data Files (*.txt)|*.txt";
            dlg.InitialDirectory = SystemLT.IMSI_DataFullPath;

            masterLength1   = this.A2_RPM_mm.Value;
            masterStartPos  = this.A2_Start_mm.Value;
            masterEndPos    = this.A2_End_mm.Value;
            masterMovingAvg = (int)this.A2_MV_counts.Value;

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                selectedFile = dlg.FileName;
                if (MBC_FileToChart1(selectedFile)) MBC_AnalysisGraph_Refresh1();
                LastselectedFile = selectedFile;
            }
        }

        private void AN2YMax_ValueChanged(object sender, ValueChangedEventArgs<double> e)
        {

        }

        private void okpopup_close_Click(object sender, RoutedEventArgs e)
        {
            OKHide();
        }
    }







    }
