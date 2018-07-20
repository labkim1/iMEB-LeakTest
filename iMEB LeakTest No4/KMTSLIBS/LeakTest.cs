using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;


using NLog;
using NLog.Common;

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
namespace iMEB_LeakTest_No4.KMTSLIBS
{
  
    // 전역 델리게이트 
    public delegate void SerialComState_UI_Refresh_Delegate(bool send,bool receive);                                  // 통신중 데이터 수신/전송 상태 표시용 
    public delegate void SerialComState_GRAPH_Refresh_Delegate(M_COSMO.COSMO_D_Format DFormat);                       // 통신중 데이터 실시간 그래프 표시용 
    public delegate void MainUpdate_Delegate(string curMode,string someMsg);                                          // 자동운전중 스텝진행 메세지 표시용
    public delegate void Automode_Clear_Delegate(int mode);                                                           // 자동운전중 최초 UI 클리어 및 관련 변수 정리   
    public delegate void CosmoGraph1Delegate();                                                                       // 진공 리크 검사후 그래프 표시
    public delegate void CosmoGraph2Delegate(bool tf);                                                                // 공압 리크 검사후 그래프 표시(1.5) tf is ture(시험), false(패스)
    public delegate void CosmoGraph3Delegate();                                                                       // 공압 리크 검사후 그래프 표시(5.0)
    public delegate void EcuDTC1Delegate(string msg, int index);                                                      // ECU DTC 검사시 화면 텍스트 블럭에 메세지 출력용 sr=false=보내기, sr=true=받기
    public delegate void EcuDTC2Delegate(M_ECU.DTC_RESULT dtcresult,int index);                                       // ECU DTC DataGrid에 출력용
    public delegate void EcuMotorGraph1Delegate(double fst,double fet,double bst,double bet,bool durationtestng);     // ECU 리크 검사후 그래프 표시 (fst=Forward Start Time....)
    public delegate void ErrorNotifyDelegate(string emsg,int ecode);                                                  // 에러 발생시 노티파이 메세지 출력
    public delegate void MBC_Delegate(double pos, double amp,bool readchk,long stopwatchtime);                        // ECU와 통신중 MBC데이터를 읽은 경우 화면에 업데이트
    public delegate void LDS_StartDelegate();                                                                         // LDS Server 관련 작업 시작시
    public delegate void LDS_StopDelegate(bool result, int resultcode);                                               // LDS Server 관련 시험종료후 결과값 전송시
    public delegate void EcuLeakTest2Delegate();                                                                      // 모터전류결과 표시용


    public delegate void NGMessage(string msg);                                  // NG 발생시 팝업화면 표시용
    public delegate void NGMessageHide();                                        // NG 팝업화면 숨김
    public delegate bool TotalResult(iMEB_LeakTest_No4.MainWindow.CurTestMode mode);                                          // 각 시험별 결과값 확인용
    public delegate void OKMessage(string msg);                                  // OK 발생시 팝업화면 표시용
    public delegate void OKMessageHide();                                        // OK 팝업화면 숨김


    /// <summary>
    /// iMEB LEAK TEST 제어
    /// 2017-10-27
    /// KIM HAK JIN
    /// 
    /// </summary>
    public class LeakTest : IDisposable
    {
       #region Disposable
        private bool disposed = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        if (M_Cosmo != null)  M_Cosmo.Dispose();
                        if (M_Plc != null)    M_Plc.Dispose();
                        if (M_Daq != null)    M_Daq.Dispose();
                        if (M_Ecu != null)    M_Ecu.Dispose();
                        if (M_MesLds != null) M_MesLds.Dispose();
                    }
                    disposed = true;
                }
            }
            ~LeakTest()
            {
                Dispose(false);
            }
        #endregion

        public Logger Log   = null;
        public Logger Debug = null;

        public iMEB.SysConfig             CurConfig           = new iMEB.SysConfig();                                           // 시험기 전반적으로 사용되는 시험정보
        public iMEB.ExternalLeakTest      CurExternalLeakTest = new iMEB.ExternalLeakTest();                                    // 외부 리크 테스트 상세 시험정보
        public iMEB.InternalLeakTest      CurInternalLeakTest = new iMEB.InternalLeakTest();                                    // 내부 1.5/5.0 공압 리크 테스트 상세 시험정보
        public iMEB.ECULeakTest           CurECULeakTest      = new iMEB.ECULeakTest();                                         // ECU MOTOR 리크 테스트 상세 시험정보
        public iMEB.EcuDtc                CurEcuDtc           = new iMEB.EcuDtc();                                              // ECU DTC 테스트 상세 시험정보
        public MESSPEC                    CurMesSpec          = new MESSPEC();                                                  // MES에서 내려 받은 사양정보 저장 및 마지막 사양정보
        public List<iMEB.TestResultTable> CurTestResult       = new System.Collections.Generic.List<iMEB.TestResultTable> { };  // 시험 결과 그리드 표시용 리스트
        public List<iMEB.DTCResultTable>  CurDTCResult        = new System.Collections.Generic.List<iMEB.DTCResultTable> { };   // DTC 결과용

        //2018.01.15 추가
        public event NGMessage            NGMsg_CallBack;             // NG 발생시 메세지 표시용
        public event NGMessageHide        NGMsgHide_CallBack;         // NG 팝업화면 숨김
        public event TotalResult          TotalResult_CallBack;
        public event OKMessage            OKMsg_CallBack;             // OK 발생시 메시지 표시용
        public event OKMessageHide        OKMsgHide_CallBack;         // OK 팝업화면 숨김 
        public enum LogInLevel : int
        {
            KMTS     = 1,
            MOBIS    = 2,
            OPERATOR = 3,
            OTHER    = 0
        }
        private LogInLevel _LogInUser = LogInLevel.OTHER;
        public LogInLevel LogInUser { get { return this._LogInUser; } set { this._LogInUser = value; } }


        public enum MODE : int
        {
            BOOT   = 0,
            MANUAL = 1,
            AUTO   = 2,
            OTHER  = 3
        }
        private MODE _Mode = MODE.BOOT;
        public MODE CurrentMode { set { this._Mode = value; } get { return this._Mode; } }
       
        // PLC 연결(MODBUS-TCP/IP, MX COMPONENT)
        public M_PLC  M_Plc           = new M_PLC();
        private bool  _PLC_isLive     = false;
        public bool   PLC_isLive { get { return this._PLC_isLive; } }
        
        // DAQ 연결(NI PCI-622)
        public M_DAQ  M_Daq           = new M_DAQ();
        private bool  _DAQ_isLive     = false;
        public bool   DAQ_isLive { get { return this._DAQ_isLive; } }

        // COSMO 연결(AIR LEAK TESTE LS-910)
        public M_COSMO  M_Cosmo       = new M_COSMO();             
        private bool    _COSMO_isLive = false;
        public bool     COSMO_isLive { get { return this._COSMO_isLive; } }

        // CAN CARD 연결(KVASER CAN LIB = canlibCLSNET.dll)
        public M_ECU  M_Ecu           = new M_ECU();
        private bool  _ECU_isLive     = false;
        public bool   ECU_isLive { get { return this._ECU_isLive; } }

        // MES/LDS/UDP 연결용
        public MESLDS M_MesLds = new MESLDS();

        #region DoAutoWork 관련 - 자동운전 쓰레드
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

        private volatile bool _ShouldStop;
        private volatile bool _isStart = false;
        public void RequestStop()
        {
            this._ShouldStop = true;
        }
        /// <summary>
        /// 자동운전 쓰레드 동작여부, True=동작중, False=비가동
        /// </summary>
        public bool IsAutoModeRun { get { return this._isStart; } }           
        public enum AUTOMODE_STEP:int // 자동운전시 내부 스텝
        {
                AUTOMODE_Initialize        = 0,
                Device_Check               = 1,
                PC_Acknowldge_Set          = 2,
                Mode_Check                 = 3,

                Test_Vacuum_Leak_RunablCheck = 100,
                Test_Vacuum_Leak_RetryCheck  = 101, 
                Test_Vacuum_Leak             = 102,      // 진공

                Test_Air_Leak_RunablCheck    = 200,
                Test_Air_Leak_RetryCheck     = 201,
                Test_Air_Leak                = 202,      // 1.5바
                Test_Air_Leak_5              = 203,      // 5.0바
               
                Test_ECU_DTC_RunablCheck     = 300,
                Test_ECU_DTC_RetryCheck      = 301,
                Test_ECU_DTC                 = 302,

                Test_ECU_MOTOR_RunableCheck  = 400,
                Test_ECU_MOTOR_RetryCheck    = 401,
                Test_ECU_MOTOR               = 402,

                Test_ECU_MOTOR_RunableCheck1 = 410,
                Test_ECU_MOTOR_RetryCheck1   = 411,
                Test_ECU_MOTOR1              = 412,

                Test_ECU_NVH                 = 500,      // 사용안함.

                Test_END                   = 1000,
                Watting                    = 1001,

                Request_Stop               = 10000,
                PC_Side_Error              = 20000,
                PLC_Side_Error             = 30000,

                unknown = -100
        }
        private AUTOMODE_STEP _CurAutoModeStep = AUTOMODE_STEP.AUTOMODE_Initialize;
        public AUTOMODE_STEP CurAutoModeStep { get { return this._CurAutoModeStep; } }
        private AUTOMODE_STEP _LastAutoModeStep = AUTOMODE_STEP.unknown;

        public enum SUB_STEP : int // 각 시험모드에서 내부 스텝
        {
            // 각 시험에서 사용되는 모든 서브실행목록을 일괄정리하여 사용함.
            Init                         = 0,
            Sol_Set                      = 1,
            Clamp_Set                    = 2,

            PreChargeVacuum_0 = 2001,     // MC12/MC34측 진공가압하여 진공차단효과를 만들기 위함.
            PreChargeVacuum_1 = 2002,

            Sol_Set1                     = 11,  // 모터 리크 테스트시 전진 후 후진시 상태 변경용으로 추가됨.
            Clamp_Set1                   = 12,

            Vacuum_Charge_wait           = 13, 
            Vacuum_Charge_Check          = 14,
            Vacuum_Charge_wait1          = 15,
            Vacuum_Charge_Check1         = 16,

            Cosmo_CH_Set                 = 3,
            Cosmo_RUN_Set                = 4,
            Cosmo_STOP_Set               = 5,
            Cosmo_STOP_Check             = 6,
            Cosmo_Charge_Run             = 61,
            Cosmo_Charge_Check           = 62,
            Cosmo_Charge_Stop            = 63,
            Cosmo_Data_View              = 7,

            DAQ_HighSpeed_Check          = 18,

            Vacuum_Sol_Set               = 32,
            Vacuum_Check                 = 33,
            Vacuum_Check2                = 34,
            Vacuum_Clamp_Set             = 35,
            Vacuum_Check3                = 36,

            MC12_Vacuum_ChargSet         = 40,
            MC12_Vacuum_ChargeCheck      = 41,

            ECU_Motor_Power_On           = 50,
            ECU_Motor_Power_Wait         = 51,
            ECU_IGN_On                   = 52,
            ECU_IGN_On_Wait              = 53,
            ECU_CAN_OPEN                 = 54,
            ECU_CAN_OPEN_Retry           = 55,
            ECU_CAN_MODE_Diag_On         = 56,
            ECU_CAN_InternalMode_Init    = 57, // Motor FWD
            ECU_CAN_MBCREAD_CHECK        = 58, // Check 1
            ECU_CAN_BWD                  = 59, // Motor BWD
            ECU_CAN_MBCREAD_CHECK2       = 60, // Check 2
            ECU_CAN_MBCREAD_PRE          = 90,
            ECU_FWD_SOL_SET              = 91,  // 차징전 
            ECU_BWD_SOL_SET              = 92,
            ECU_ON_WAIT1 = 75,
            ECU_ON_WAIT2 = 76,
            ECU_ON_WAIT3 = 77,


            ECU_Motor_FWD_Run            = 93,
            ECU_Motor_FWD_Check          = 94,
            ECU_Motor_BWD_Run            = 95,
            ECU_Motor_BWD_Check          = 96,

            ECU_POWER_ON                 = 61,
            ECU_POWER_ON_Wait            = 62,

            TEST_ONLY                    = 8000,
            ECU_CAN_MotorInit            = 80,
            ECU_CAN_MotorInit_Wait       = 81,
            FWD_Clamp                    = 82,
            FWD_Charge                   = 83,
            FWD_Charge_Wait              = 84,
            FWD_Charge_STOP              = 841,
            FWD_Charge_STOP_Wait         = 842,
            BWD_Clamp                    = 85,
            BWD_Clamp_SOL                = 851,
            BWD_Charge                   = 86,
            BWD_Charge_Wait              = 87,
            BWD_Charge_STOP              = 871,
            BWD_Charge_STOP_Wait         = 872,
            LeakTest_Measurement_Start  = 200,
            LeakTest_Measurement_End    = 201,
            LeakTest_Measurement_PreDelay = 203, // Dian ON 진입후 일정시간 있다가 통신연결
            Over_Measuremrnt_Start      = 300,
            Over_Measuremrnt_Check      = 301,


            ECU_CAN_MODE_Diag_Off        = 65,

            ECU_CAN_DTC_CodeRead_0       = 69, // CAN DTC Code Read step 1
            ECU_CAN_DTC_CodeRead_1       = 70, // CAN DTC Code Read step 2

            ECU_MOTOR_Operation_Set      = 75,


            CHARGE_5BAR_1                = 1000,
            CHARGE_5BAR_2                = 1001,
            CHARGE_TIME_CHECK            = 1002,
            CHRAHE_TIME_END              = 1003,

            

            Sequence_End                 = 100,
            Error                        = -1
        }
        public enum SUB_STEP_RESULTR : int // 세부스텝 진행중 결과 상태
        {
            Doing    = 1,
            Error    = 2,
            Finished = 3
        }
        private SUB_STEP _CurSubStep = SUB_STEP.Init;
        public SUB_STEP CurSubStep { get { return this._CurSubStep; } }
        private SUB_STEP _LastCurSubStep = SUB_STEP.Error;

        public class Temp_DTC_ReadInfo
        {
            private string _Model;
            private string _PartNumber;
            private string _HWVersion;
            private string _SWVersion;
            private string _MOBIS_HSWVersion;
            private bool _ECUReset;
            public string Model { set { this._Model = value; } get { return this._Model; } }
            public string PartNumber { set { this._PartNumber = value; } get { return this._PartNumber; } }
            public string HWVersion { set { this._HWVersion = value; } get { return this._HWVersion; } }
            public string SWVersion { set { this._SWVersion = value; } get { return this._SWVersion; } }
            public string MOBIS_HSWVersion { set { this._MOBIS_HSWVersion = value; } get { return this._MOBIS_HSWVersion; } }
            public bool ECUReset { set { this._ECUReset = value; } get { return this._ECUReset; } }


            public Temp_DTC_ReadInfo()
            {
                this.Model = "";
                this.PartNumber = "";
                this.HWVersion = "";
                this.SWVersion = "";
                this.MOBIS_HSWVersion = "";
                this.ECUReset = false;
            }
        }
        public Temp_DTC_ReadInfo DTC_ReadInfo = new Temp_DTC_ReadInfo();
        private Stopwatch _Total_Test_Stopwatch = new Stopwatch();  // 시험 전체 시험시간측정용
        //private Stopwatch _Local_Test_Stopwatch = new Stopwatch();  // 단위 시험 시간측정용
        private Stopwatch _Waitter_Stopwatch    = new Stopwatch();  // 세부 기능수행시 특정시간 지연을 확인용 1
        private Stopwatch _Waitter1_Stopwatch   = new Stopwatch();  // 세부 기능수행시 특정시간 지연을 확인용 2
        
        public event Automode_Clear_Delegate AutomodeClear;

        public event CosmoGraph1Delegate CosmoGraph1CallBack;       // 진공 리크
        public event CosmoGraph2Delegate CosmoGraph2CallBack;       // 공압 리크(1.5)
        public event CosmoGraph3Delegate CosmoGraph3CallBack;       // 공압 리크(5.0)

        public event EcuDTC1Delegate EcuDTC1CallBack;               // 텍스트 블럭용 메세지 출력
        public event EcuDTC2Delegate EcuDTC2CallBack;               // 그리드에 데이터 출력용
        private int _EcuDtcResultCheck = 0;                           // 메인 윈도우에서 ECU DTC결과 값을 확인하기 위해서
        public int EcuDtcResultCheck { set { this._EcuDtcResultCheck = value; } get { return this._EcuDtcResultCheck; } }
        public event EcuMotorGraph1Delegate EcuMotorGraph1CallBack; // 모터 리크 


        public event ErrorNotifyDelegate ErrorNotifyCallBack;       // 에러 발생시 노티파이 메세지 출력용

        public event MBC_Delegate MBCCallBack;                      // Main UI에 MBC 데이터 출력

        public event LDS_StartDelegate LDS_StartCallBack;
        public event LDS_StopDelegate LDS_StopCallBack;

        public event EcuLeakTest2Delegate EcuLeakTest2CallBack;     // 모터 전류 평가 화면 표시용.

        private int _ImsiTestIndex = 0;
        public string IMSI_DataFullPath = "";
        //
        #region 단계 시험별로 시간 측정용 클래스
        public class TestTime
        {
            
            public const int MAX_TESTCCOUNTS = 10;
            public enum TESTNAME : int
            {
                LEAKTEST_HIGH_VACUUM    = 0,
                LEAKTEST_AIR_15         = 1,
                LEAKTEST_AIR_50         = 2,
                MOTORLEAKTEST           = 3,
                ECUDTCTEST              = 4,
            }
            private double[]    MeasurementTime = new double[MAX_TESTCCOUNTS];
            private Stopwatch[] _TestTimer     = new Stopwatch[MAX_TESTCCOUNTS];

            public void Start(TESTNAME testname)
            {
                _TestTimer[(int)testname].Restart();
            }
            public void Stop(TESTNAME testname)
            {
                _TestTimer[(int)testname].Stop();
            }
            public double ResultTime(TESTNAME testname)
            {
                double ResultTime;
                ResultTime = _TestTimer[(int)testname].ElapsedMilliseconds / 1000.0;
                return ResultTime;
            }
            public TestTime()
            {
                for (int i=0; i<MAX_TESTCCOUNTS; i++)
                {
                    _TestTimer[i] = new Stopwatch();
                    _TestTimer[i].Stop();
                }
            }
        }
        #endregion
        //
        public TestTime _SubTestTimer = new TestTime();            // 각 단계별 시험의 시간 측정용

        public void DoAutoWork()
        {
            string eMsg = "";
            string substepeMsg="";
            //StringBuilder CurrentJob = new StringBuilder();
            bool   isTest        = false;
            int    SetRetryCount = 0;
            int    NowRetyrCount = 0;
            bool   StopOrGo      = false;
            SUB_STEP subStep;

            this._ShouldStop = false; // 외부에서 쓰레드 종료시 호출
            this._isStart    = true;
            int _NGCode = 0;

            while(_isStart)
            {
                if (_ShouldStop) _CurAutoModeStep = AUTOMODE_STEP.Request_Stop; // 외부에서 쓰레드 종료시 처리
                switch(_CurAutoModeStep)
                {
                    #region 자동모드 스텝 처리
                    case AUTOMODE_STEP.AUTOMODE_Initialize: // 변수 및 그래프 클리어
                        AutomodeClear(0);
                        M_Plc.CMD_ClearWorkArea(1.0);
                        M_Daq.PSU_SETVOLT(0.0);
                        Delay(50);
                        _CurAutoModeStep = AUTOMODE_STEP.Device_Check;
                        break;
                    case AUTOMODE_STEP.Device_Check: // 자동운전 시작 조건 확인
                        _CurAutoModeStep = AUTOMODE_STEP.PC_Acknowldge_Set;
                        break;
                    case AUTOMODE_STEP.PC_Acknowldge_Set: // PC -> PLC Busy Signal Sett
                        M_Plc.CMD_ClearWorkArea(1.0);
                        bool ichk = M_Plc.CMD_PLC_BusySetting();
                        if (ichk)
                        {
                            Log.Info("자동운전 시작");

                            // 로우 데이터 저장 전체 경로 및 서두 수정
                            string RootPath    = "D:\\Data\\";
                            string NowDate     = DateTime.Now.ToShortDateString();
                            string pathString  = System.IO.Path.Combine(RootPath, NowDate);
                            System.IO.Directory.CreateDirectory(pathString);
                            string fileName    = "IMSI_" + string.Format("{0:N3}", _ImsiTestIndex) + "_";
                            IMSI_DataFullPath  = System.IO.Path.Combine(pathString, fileName);

                            Log.Info("임시 데이터 저장 관련 화일이름(해드파트):" + IMSI_DataFullPath);

                            _ImsiTestIndex++;

                            _CurSubStep                   = SUB_STEP.Init;
                            CurExternalLeakTest.TestCount = 0;
                            _CurAutoModeStep              = AUTOMODE_STEP.Test_Vacuum_Leak_RunablCheck;

                            LDS_StartCallBack();
                        }
                        else
                        {
                            eMsg = "PC->PLC Reday,Busy 시그널 설정시 통신에러가 발생하였습니다.";
                            NGMsg_CallBack(eMsg+"\r\n");
                            _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                        }
                        break;
                    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    // 고 진공 리크 테스트 진행여부 검사 및 반복
                    case AUTOMODE_STEP.Test_Vacuum_Leak_RunablCheck:
                        isTest = CurExternalLeakTest.Testable_ExternalLeakTest;
                        if (isTest) _CurAutoModeStep = AUTOMODE_STEP.Test_Vacuum_Leak;
                        else
                        {
                            // PASS일 경우 회색처리
                            _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_RunablCheck;
                        }
                        break;
                    // 500mmHg 리저버 탱크 진공시험
                    case AUTOMODE_STEP.Test_Vacuum_Leak:    
                        SUB_STEP_RESULTR retCode = TestSub_ExternalLeak(out subStep, out substepeMsg);
                        if (retCode == SUB_STEP_RESULTR.Error)
                        {
                            // 각 세부 테스트 코드에서 정지 조건일때 정지환경을 동일하게......
                            StopOrGo = CurExternalLeakTest.Testable_NGQuit;
                            if (StopOrGo)
                            {
                                eMsg             = substepeMsg;
                                NGMsg_CallBack("제어동작중 에러발생 : "+eMsg + "\r\n");
                                _NGCode = 11;
                                _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                            }
                            else
                            {
                                _CurSubStep      = SUB_STEP.Init;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_Vacuum_Leak_RetryCheck;                                
                            }
                        }
                        if (retCode == SUB_STEP_RESULTR.Finished)
                        {
                            Delay(100);
                            bool isNGStop = CurExternalLeakTest.Testable_NGQuit;
                            if (isNGStop)
                            {                               
                                bool _ret = TotalResult_CallBack(MainWindow.CurTestMode.ExtLeakTest);
                                if (_ret == false)
                                {
                                    _NGCode = 10;
                                    NGMsg_CallBack("시험 결과  NG \r\n");
                                    _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                                    break;
                                }
                            }
                            _CurAutoModeStep = AUTOMODE_STEP.Test_Vacuum_Leak_RetryCheck;
                            _CurSubStep      = SUB_STEP.Init;
                        }
                        break;
                    // 반복 횟수 검사
                    case AUTOMODE_STEP.Test_Vacuum_Leak_RetryCheck:                        
                        SetRetryCount = CurExternalLeakTest.Testable_RetryCount;
                        if (SetRetryCount > 0)
                        {
                            NowRetyrCount = CurExternalLeakTest.TestCount;
                            if (NowRetyrCount < SetRetryCount)
                            {
                                CurExternalLeakTest.TestCount++;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_Vacuum_Leak;
                                break;
                            }
                            else
                            {
                                _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_RunablCheck;
                                break;
                            }
                        }
                        else _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_RunablCheck;
                        break;
                    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    case AUTOMODE_STEP.Test_Air_Leak_RunablCheck:
                        isTest = CurInternalLeakTest.Testable_InternalLeakTest;
                        if (isTest) _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak;
                        else        _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC_RunablCheck;
                        break;
                    // 1.5바 리저버 탱크 공압시험
                    case AUTOMODE_STEP.Test_Air_Leak:
                            // 무조건 패스 2018.07.05
                            _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_5;
                            _CurSubStep      = SUB_STEP.Init;
                            Delay(100);
                            break;



                        if (CurInternalLeakTest.Testable_15Pass)
                        {
                            // 1.5 Pass일 경우
                            CosmoGraph2CallBack(false);
                            TotalResult_CallBack(MainWindow.CurTestMode.IntLeakTest15PASS);
                            _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_5;
                            _CurSubStep      = SUB_STEP.Init;
                            Delay(100);
                            break;
                        }
                        retCode = TestSub_InternalLeak(out subStep, out substepeMsg);
                        if (retCode == SUB_STEP_RESULTR.Error)
                        {
                            // 각 세부 테스트 코드에서 정지 조건일때 정지환경을 동일하게......
                            StopOrGo = CurInternalLeakTest.Testable_NGQuit;
                            if (StopOrGo)
                            {
                                eMsg = substepeMsg;
                                NGMsg_CallBack("제어동작중 에러발생 : " + eMsg + "\r\n");
                                _NGCode = 21;
                                _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                            }
                            else
                            {
                                _CurSubStep = SUB_STEP.Init;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_5;
                            }
                        }
                        if (retCode == SUB_STEP_RESULTR.Finished)
                        {
                            Delay(100);
                            bool isNGStop = CurInternalLeakTest.Testable_NGQuit;
                            if (isNGStop)
                            {
                                bool _ret = TotalResult_CallBack(MainWindow.CurTestMode.IntLeakTest15);
                                if (_ret == false)
                                {
                                    _NGCode = 20;
                                    NGMsg_CallBack("시험 결과  NG \r\n");
                                    _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                                    break;
                                }
                            }
                            _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_5;
                            _CurSubStep = SUB_STEP.Init;
                        }
                        break;
                    // 5.0바 리저버 탱크 공압시험
                    case AUTOMODE_STEP.Test_Air_Leak_5:
                        retCode = TestSub_InternalLeak5(out subStep, out substepeMsg);
                        if (retCode == SUB_STEP_RESULTR.Error)
                        {
                            // 각 세부 테스트 코드에서 정지 조건일때 정지환경을 동일하게......
                            StopOrGo = CurInternalLeakTest.Testable_NGQuit;
                            if (StopOrGo)
                            {
                                eMsg = substepeMsg;
                                NGMsg_CallBack("제어동작중 에러발생 : " + eMsg + "\r\n");
                                _NGCode = 31;
                                _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                            }
                            else
                            {
                                _CurSubStep = SUB_STEP.Init;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_RetryCheck;
                            }
                        }
                        if (retCode == SUB_STEP_RESULTR.Finished)
                        {
                            Delay(100);
                            bool isNGStop = CurInternalLeakTest.Testable_NGQuit;
                            if (isNGStop)
                            {
                                bool _ret = TotalResult_CallBack(MainWindow.CurTestMode.IntLeakTest50);
                                if (_ret == false)
                                {
                                    _NGCode = 30;
                                    NGMsg_CallBack("시험 결과  NG \r\n");
                                    _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                                    break;
                                }
                            }
                            _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak_RetryCheck;
                            _CurSubStep = SUB_STEP.Init;
                        }
                        break;
                    // 반복 횟수 검사
                    case AUTOMODE_STEP.Test_Air_Leak_RetryCheck:
                        SetRetryCount = CurInternalLeakTest.Testable_RetryCount;
                        if (SetRetryCount > 0)
                        {
                            NowRetyrCount = CurInternalLeakTest.TestCount;
                            if (NowRetyrCount < SetRetryCount)
                            {
                                CurInternalLeakTest.TestCount++;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_Air_Leak;
                                break;
                            }
                            else
                            {
                                _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_MOTOR_RunableCheck;
                                break;
                            }
                        }
                        else _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_MOTOR_RunableCheck;
                        break;


                    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    case AUTOMODE_STEP.Test_ECU_DTC_RunablCheck:
                        isTest = CurEcuDtc.Testable_ExternalLeakTest;
                        if (isTest) _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC;
                        else        _CurAutoModeStep = AUTOMODE_STEP.Test_END;
                        break;
                    case AUTOMODE_STEP.Test_ECU_DTC:
                        bool LogEnable1 = CurEcuDtc.Log_ECUDTCTest;
                        retCode = TestSub_ECUDTC(out subStep, out substepeMsg, LogEnable1);
                        if (retCode == SUB_STEP_RESULTR.Error)
                        {
                            // 각 세부 테스트 코드에서 정지 조건일때 정지환경을 동일하게......
                            StopOrGo = CurEcuDtc.Testable_NGQuit;
                            if (StopOrGo)
                            {
                                eMsg = substepeMsg;
                                NGMsg_CallBack("제어동작중 에러발생 : " + eMsg + "\r\n");
                                _NGCode = 51;
                                _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                            }
                            else
                            {
                                _CurSubStep      = SUB_STEP.Init;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC_RetryCheck;
                            }
                        }
                        if (retCode == SUB_STEP_RESULTR.Finished)
                        {
                            Delay(100);
                            bool isNGStop = CurEcuDtc.Testable_NGQuit;
                            if (isNGStop)
                            {
                                bool _ret = TotalResult_CallBack(MainWindow.CurTestMode.DTC);
                                if (_ret == false)
                                {
                                    _NGCode = 30;
                                    NGMsg_CallBack("시험 결과  NG \r\n");
                                    _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                                    break;
                                }
                            }
                            _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC_RetryCheck;
                            _CurSubStep      = SUB_STEP.Init;
                        }
                        break;
                    // 반복 횟수 검사
                    case AUTOMODE_STEP.Test_ECU_DTC_RetryCheck:
                        SetRetryCount = CurEcuDtc.Testable_RetryCount;
                        if (SetRetryCount > 0)
                        {
                            NowRetyrCount = CurEcuDtc.TestCount;
                            if (NowRetyrCount < SetRetryCount)
                            {
                                CurEcuDtc.TestCount++;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC;
                                break;
                            }
                            else
                            {
                                _CurAutoModeStep = AUTOMODE_STEP.Test_END;
                                break;
                            }
                        }
                        else _CurAutoModeStep = AUTOMODE_STEP.Test_END;
                        break;

                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    case AUTOMODE_STEP.Test_ECU_MOTOR_RunableCheck:
                        isTest = CurECULeakTest.Testable_ECULeakTest;
                        if (isTest) _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_MOTOR;
                        else        _CurAutoModeStep = AUTOMODE_STEP.Test_END;
                        break;
                    // ECU LEAK TEST
                    case AUTOMODE_STEP.Test_ECU_MOTOR:
                        bool LogEnable = CurECULeakTest.Log_ECULeakTest;
                        retCode = TestSub_ECULeak(out subStep, out substepeMsg, LogEnable);
                        if (retCode == SUB_STEP_RESULTR.Error)
                        {
                            // 각 세부 테스트 코드에서 정지 조건일때 정지환경을 동일하게......
                            StopOrGo = CurECULeakTest.Testable_NGQuit;
                            if (StopOrGo)
                            {
                                eMsg = substepeMsg;
                                NGMsg_CallBack("제어동작중 에러발생 : " + eMsg + "\r\n");
                                _NGCode = 41;
                                _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                            }
                            else
                            {
                                _CurSubStep = SUB_STEP.Init;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_MOTOR_RetryCheck;
                            }
                        }
                        if ((retCode == SUB_STEP_RESULTR.Finished)||(retCode== SUB_STEP_RESULTR.Error))
                        {
                            Delay(100);
                            bool isNGStop = CurECULeakTest.Testable_NGQuit;
                            if (isNGStop)
                            {
                                bool _ret = TotalResult_CallBack(MainWindow.CurTestMode.LeakMotor);
                                if (_ret == false)
                                {
                                    _NGCode = 40;
                                    NGMsg_CallBack("시험 결과  NG \r\n");
                                    _CurAutoModeStep = AUTOMODE_STEP.PC_Side_Error;
                                    break;
                                }
                            }
                            _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_MOTOR_RetryCheck;
                            _CurSubStep = SUB_STEP.Init;
                        }
                        break;
                    // 반복 횟수 검사
                    case AUTOMODE_STEP.Test_ECU_MOTOR_RetryCheck:
                        SetRetryCount = CurInternalLeakTest.Testable_RetryCount;
                        if (SetRetryCount > 0)
                        {
                            NowRetyrCount = CurInternalLeakTest.TestCount;
                            if (NowRetyrCount < SetRetryCount)
                            {
                                CurInternalLeakTest.TestCount++;
                                _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_MOTOR;
                                break;
                            }
                            else
                            {
                                _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC_RunablCheck;
                                break;
                            }
                        }
                        else _CurAutoModeStep = AUTOMODE_STEP.Test_ECU_DTC_RunablCheck;
                        break;
                    case AUTOMODE_STEP.PC_Side_Error:
                        Log.Error("[PC Side]AutoControl Step : "+_CurAutoModeStep.ToString());
                        Log.Error("Description : " + eMsg);
                        // PSU Power OFF
                        M_Daq.PSU_SETVOLT(0.0);
                        M_Ecu.isDiagOnMode = false;                                            
                        M_Plc.CMD_TestEndSet(2, 3.0);
                        M_Plc.PLC_TestEndNGCodeSet(_NGCode);
                        LDS_StopCallBack(false, -1);
                        _CurAutoModeStep = AUTOMODE_STEP.Watting;
                        break;
                    case AUTOMODE_STEP.Request_Stop: // 외부에서 강제 종료시 처리
                        Log.Error("[User Stop]AutoControl Step : "+_CurAutoModeStep.ToString());
                        Log.Error("Description : " + eMsg);
                        _isStart = false;
                        LDS_StopCallBack(false, -2);
                        _CurAutoModeStep = AUTOMODE_STEP.Watting;
                        break;
                    case AUTOMODE_STEP.Test_END:
                        M_Plc.CMD_TestEndSet(1, 3.0);
                        LDS_StopCallBack(true, 0);
                        _CurAutoModeStep = AUTOMODE_STEP.Watting;
                        break;
                    case AUTOMODE_STEP.Watting:
                        _isStart = false;
                        _CurAutoModeStep = AUTOMODE_STEP.AUTOMODE_Initialize;
                        break;
                    #endregion
                }
                if ( (_CurAutoModeStep != AUTOMODE_STEP.AUTOMODE_Initialize)&& (_CurAutoModeStep != AUTOMODE_STEP.Watting))
                {
                    string stepMsg =_CurAutoModeStep.ToString()+"/"+_CurSubStep.ToString();
                    MainUI_Update(stepMsg, eMsg);
                    _LastAutoModeStep = _CurAutoModeStep;
                }
                Thread.Sleep(1); // 2017-10-16 시스템 안정화를 위해
            }
        }
        public event MainUpdate_Delegate MainUIUpdate1;
        private void MainUI_Update(string currentjob,string eMsg)
        {
            string msg = currentjob.Replace("_", " ");
            MainUIUpdate1(msg, eMsg);
        }
        private Stopwatch _ExtLeakSubStopWatch = new Stopwatch();
        private SUB_STEP_RESULTR TestSub_ExternalLeak(out SUB_STEP substep, out string eMsg)
        {
            string errmsg = "";
            M_PLC.SOL Sol;
            M_PLC.CLAMP Clamp;

            bool ichk;

            _LastCurSubStep = _CurSubStep;
            switch(_CurSubStep)
            {

                case SUB_STEP.Init: // 초기 설정(시리얼 통신 포트 관련 정리)
                    _SubTestTimer.Start(TestTime.TESTNAME.LEAKTEST_HIGH_VACUUM);  // 시험 시간 측정용
                    M_Cosmo.Cosmo1_DataIndex_Clear();
                    _CurSubStep = SUB_STEP.Sol_Set;
                    break;
                case SUB_STEP.Sol_Set: // 솔 명령어 정리.   
                    Sol  = M_PLC.SOL.ECU_POWER_OFF | M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE ;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Clamp_Set;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] 솔 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Clamp_Set:
                    Clamp = M_PLC.CLAMP.IGNITION_OFF | M_PLC.CLAMP.ECU_CONNECTOR_BWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_FWD | M_PLC.CLAMP.WORK_CLAMP;
                    ichk = M_Plc.CMD_ClampSetting((int)Clamp,2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_CH_Set;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] 클램프 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;

                case SUB_STEP.PreChargeVacuum_0:
                    Sol  = M_PLC.SOL.ECU_POWER_OFF | M_PLC.SOL.WHEEL_PORT_12_VAC_OPEN | M_PLC.SOL.WHEEL_PORT_34_VAC_OPEN | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_OPEN ;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                    if (ichk)
                    {
                        _ExtLeakSubStopWatch.Restart();
                        _CurSubStep = SUB_STEP.PreChargeVacuum_1;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] MC12,MC34측 진공 솔 오픈 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.PreChargeVacuum_1:
                    if (_ExtLeakSubStopWatch.ElapsedMilliseconds > 3000)
                    {
                        _ExtLeakSubStopWatch.Stop();
                        Sol = M_PLC.SOL.ECU_POWER_OFF | M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                        ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                        if (ichk)
                        {
                            _CurSubStep = SUB_STEP.Cosmo_CH_Set;
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[SOL SET] MC12,MC34측 진공 솔 클로우져 솔 명령어 전송중 에러가 발생하였습니다.";
                        }
                    }
                    break;

                case SUB_STEP.Cosmo_CH_Set:
                    ichk = M_Plc.CMD_CosmoChSetting(0, 3.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_RUN_Set;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO CH SET] 코스모 장치에 채널 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_RUN_Set:
                    ichk = M_Plc.CMD_CosmoRunSetting(3.0);
                    if (ichk)
                    {
                        // COSMO DATA GRAPH VIEW
                        _CurSubStep = SUB_STEP.Cosmo_STOP_Check;
                        _Waitter_Stopwatch.Reset();
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO RUN SET]  코스모 장치에 RUN 명령 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_STOP_Check:
                    short resultCosmoDevice=0;
                    ichk = M_Plc.CMD_CosmoStopChecking(1.0, ref resultCosmoDevice);
                    if (ichk)
                    {
                        if ((resultCosmoDevice & 0x00000100) > 0)
                        {
                            // END
                            _Waitter_Stopwatch.Stop();
                            ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                            if(!ichk)
                            {
                                _CurSubStep = SUB_STEP.Error;
                                errmsg = "[COSMO STOP CHECK] 코스모 장치 STOP 명령이 수행되지 못했습니다.";
                                break;
                            }
                            int _CosmoDataLength = M_Cosmo.Cosmo1DataIndex;
                            if (_CosmoDataLength==0)
                            {
                                _CurSubStep = SUB_STEP.Error;
                                errmsg = "[COSMO Data Read] 코스모 장치에서 데이터를 수집하지 못했습니다.";
                                break;
                            }
                            // 시험 결과 판단.
                            CosmoGraph1CallBack();
                            _CurSubStep = SUB_STEP.Sequence_End;
                            break;
                        }
                        if ((resultCosmoDevice & 0x00000004) > 0)
                        {
                            // ERROR
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[COSMO STOP CHECK] 코스모 장치 ERROR가 발생되었습니다.";
                        }
                    }
                    else
                    {
                        if (_Waitter_Stopwatch.ElapsedMilliseconds > (30 * 1000))
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[COSMO STOP CHECK] 지정된 시간내 코스모 장치에서 리크 테스트를 완료하지 못했습니다..";
                        }
                    }
                    break;



                case SUB_STEP.Cosmo_STOP_Set:
                    ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_STOP_Check;
                        _Waitter_Stopwatch.Reset();
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = " 코스모 장치에 시작 명령 전송 확인중 에러가 발생하였습니다.";
                    }
                    break;



                case SUB_STEP.Error:
                    // COSMO STOP
                    M_Plc.CMD_CosmoStopSetting(3.0);
                    //
                    eMsg    = errmsg;
                    substep = _CurSubStep;
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_HIGH_VACUUM);  // 시험 시간 측정용
                    Log.Error("[고진공리크시험]" + substep.ToString() + "/" + eMsg);
                    ErrorNotifyCallBack(eMsg, 0);
                    //_CurSubStep = SUB_STEP.Sequence_End;
                    //return  SUB_STEP_RESULTR.Error;
                    break;
                case SUB_STEP.Sequence_End:          
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_HIGH_VACUUM);  // 시험 시간 측정용
                    _LastCurSubStep = _CurSubStep;
                    eMsg            = errmsg;
                    substep         = _CurSubStep;
                    return SUB_STEP_RESULTR.Finished;
                    
            }           
            eMsg    = errmsg;
            substep = _CurSubStep;
            if (errmsg.Length > 0) return  SUB_STEP_RESULTR.Error;
            else                   return  SUB_STEP_RESULTR.Doing;
            //Log.Info("[ECU Motor LeakTest] Motor Leak Test(BWD) 명령전송");

        }
        // 2018-07-17 현재 사용하진 않음 1.5바 공압 리크 테스트
        private SUB_STEP_RESULTR TestSub_InternalLeak(out SUB_STEP substep, out string eMsg)
        {
            string errmsg = "";
            M_PLC.SOL Sol;
            M_PLC.CLAMP Clamp;

            bool ichk;

            _LastCurSubStep = _CurSubStep;
            switch (_CurSubStep)
            {

                case SUB_STEP.Init: // 초기 설정(시리얼 통신 포트 관련 정리)
                    M_Cosmo.Cosmo1_DataIndex_Clear();
                    _SubTestTimer.Start(TestTime.TESTNAME.LEAKTEST_AIR_15);  // 시험 시간 측정용
                    _CurSubStep = SUB_STEP.Sol_Set;
                    break;
                case SUB_STEP.Sol_Set: // 솔 명령어 정리.   
                    Sol = M_PLC.SOL.ECU_POWER_OFF | M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Clamp_Set;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] 솔 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Clamp_Set:
                    Clamp = M_PLC.CLAMP.IGNITION_OFF | M_PLC.CLAMP.ECU_CONNECTOR_BWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_FWD | M_PLC.CLAMP.WORK_CLAMP;
                    ichk = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_CH_Set;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] 클램프 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_CH_Set:
                    ichk = M_Plc.CMD_CosmoChSetting(1, 3.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_RUN_Set;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO CH SET] 코스모 장치에 채널 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_RUN_Set:
                    ichk = M_Plc.CMD_CosmoRunSetting(3.0);
                    if (ichk)
                    {
                        // COSMO DATA GRAPH VIEW

                        _CurSubStep = SUB_STEP.Cosmo_STOP_Check;
                        _Waitter_Stopwatch.Reset();
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO RUN SET]  코스모 장치에 RUN 명령 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_STOP_Check:
                    short resultCosmoDevice = 0;
                    ichk = M_Plc.CMD_CosmoStopChecking(1.0, ref resultCosmoDevice);
                    if (ichk)
                    {
                        if ((resultCosmoDevice & 0x00000100) > 0)
                        {
                            // END
                            _Waitter_Stopwatch.Stop();
                            ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                            if (!ichk)
                            {
                                _CurSubStep = SUB_STEP.Error;
                                errmsg = "[COSMO STOP CHECK] 코스모 장치 STOP 명령이 수행되지 못했습니다.";
                                break;
                            }
                            int _CosmoDataLength = M_Cosmo.Cosmo1DataIndex;
                            if (_CosmoDataLength == 0)
                            {
                                _CurSubStep = SUB_STEP.Error;
                                errmsg = "[COSMO Data Read] 코스모 장치에서 데이터를 수집하지 못했습니다.";
                                break;
                            }
                            CosmoGraph2CallBack(true);
                            _CurSubStep = SUB_STEP.Sequence_End;
                            break;
                        }
                        if ((resultCosmoDevice & 0x00000004) > 0)
                        {
                            // ERROR
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[COSMO STOP CHECK] 코스모 장치 ERROR가 발생되었습니다.";
                        }
                    }
                    else
                    {
                        if (_Waitter_Stopwatch.ElapsedMilliseconds > (30 * 1000))
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[COSMO STOP CHECK] 지정된 시간내 코스모 장치에서 리크 테스트를 완료하지 못했습니다..";
                        }
                    }
                    break;



                case SUB_STEP.Cosmo_STOP_Set:
                    ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_STOP_Check;
                        _Waitter_Stopwatch.Reset();
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = " 코스모 장치에 시작 명령 전송 확인중 에러가 발생하였습니다.";
                    }
                    break;



                case SUB_STEP.Error:
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_15);  // 시험 시간 측정용
                    eMsg        = errmsg;
                    substep     = _CurSubStep;
                    _CurSubStep = SUB_STEP.Sequence_End;
                    return SUB_STEP_RESULTR.Error;                  
                case SUB_STEP.Sequence_End:
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_15);  // 시험 시간 측정용
                    _LastCurSubStep = _CurSubStep;
                    eMsg            = errmsg;
                    substep         = _CurSubStep;
                    return SUB_STEP_RESULTR.Finished;
               

            }
            eMsg = errmsg;
            substep = _CurSubStep;
            if (errmsg.Length > 0) return SUB_STEP_RESULTR.Error;
            else                   return SUB_STEP_RESULTR.Doing;

        }

        private Stopwatch _LT50_stopwatch = new Stopwatch();
        private SUB_STEP_RESULTR TestSub_InternalLeak5(out SUB_STEP substep, out string eMsg)
        {
            string errmsg = "";
            M_PLC.SOL Sol;
            M_PLC.CLAMP Clamp;

            bool ichk;

            _LastCurSubStep = _CurSubStep;
            switch (_CurSubStep)
            {

                case SUB_STEP.Init: // 초기 설정(시리얼 통신 포트 관련 정리)
                    _SubTestTimer.Start(TestTime.TESTNAME.LEAKTEST_AIR_50);  // 시험 시간 측정용
                    //M_Cosmo.Cosmo1_DataIndex_Clear();
                    _CurSubStep = SUB_STEP.Sol_Set;
                    break;
                case SUB_STEP.Sol_Set: // 솔 명령어 정리.   
                    Sol = M_PLC.SOL.ECU_POWER_OFF | M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Clamp_Set;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] 솔 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Clamp_Set:
                    Clamp = M_PLC.CLAMP.IGNITION_OFF | M_PLC.CLAMP.ECU_CONNECTOR_BWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_BWD | M_PLC.CLAMP.WHEEL_PORT_34_BWD | M_PLC.CLAMP.WORK_UNCLAMP;  // 5.0바 시험시 워크 클램프 해제!!!
                    ichk = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.CHARGE_5BAR_1;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] 클램프 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;

                case SUB_STEP.CHARGE_5BAR_1:
                    ichk = M_Plc.CMD_CosmoChSetting(2, 3.0);
                    if (ichk)
                    {
                        Delay(200);
                        _CurSubStep = SUB_STEP.CHARGE_5BAR_2;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO CH SET] 코스모 장치에 채널 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.CHARGE_5BAR_2:
                    ichk = M_Plc.CMD_CosmoChargeRun(3.0);
                    if (ichk)
                    {
                        _LT50_stopwatch.Restart();
                        _CurSubStep = SUB_STEP.CHARGE_TIME_CHECK;                     
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO RUN SET]  코스모 장치에 RUN 명령 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.CHARGE_TIME_CHECK:
                    if (_LT50_stopwatch.ElapsedMilliseconds>2500)
                    {
                        ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                        _LT50_stopwatch.Stop();
                        _CurSubStep = SUB_STEP.CHRAHE_TIME_END;
                        Delay(500);
                        break;
                    }
                    break;
                case SUB_STEP.CHRAHE_TIME_END:
                    Clamp = M_PLC.CLAMP.IGNITION_OFF | M_PLC.CLAMP.ECU_CONNECTOR_BWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_FWD | M_PLC.CLAMP.WORK_CLAMP;  // 5.0바 시험시 워크 클램프 재 설정
                    ichk = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                    if (ichk)
                    {
                        M_Cosmo.Cosmo1_DataIndex_Clear();
                        _CurSubStep = SUB_STEP.Cosmo_CH_Set;
                        Delay(100);
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] 클램프 명령어 전송중 에러가 발생하였습니다.";
                    }
                   
                    break;

                case SUB_STEP.Cosmo_CH_Set:
                    Delay(50);
                    ichk = M_Plc.CMD_CosmoChSetting(2, 3.0);
                    if (ichk)
                    {
                        Delay(200);
                        _CurSubStep = SUB_STEP.Cosmo_RUN_Set;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO CH SET] 코스모 장치에 채널 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_RUN_Set:
                    ichk = M_Plc.CMD_CosmoRunSetting(3.0);
                    if (ichk)
                    {
                        // COSMO DATA GRAPH VIEW
                        Delay(100);
                        _CurSubStep = SUB_STEP.Cosmo_STOP_Check;
                        _Waitter_Stopwatch.Reset();
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[COSMO RUN SET]  코스모 장치에 RUN 명령 전송중 에러가 발생하였습니다.";
                    }
                    break;
                case SUB_STEP.Cosmo_STOP_Check:
                    short resultCosmoDevice = 0;
                    ichk = M_Plc.CMD_CosmoStopChecking(1.0, ref resultCosmoDevice);
                    if (ichk)
                    {
                        if ((resultCosmoDevice & 0x00000100) > 0)
                        {
                            // END
                            _Waitter_Stopwatch.Stop();
                            ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                            if (!ichk)
                            {
                                _CurSubStep = SUB_STEP.Error;
                                errmsg = "[COSMO STOP CHECK] 코스모 장치 STOP 명령이 수행되지 못했습니다.";
                                break;
                            }
                            int _CosmoDataLength = M_Cosmo.Cosmo1DataIndex;
                            if (_CosmoDataLength == 0)
                            {
                                _CurSubStep = SUB_STEP.Error;
                                errmsg = "[COSMO Data Read] 코스모 장치에서 데이터를 수집하지 못했습니다.";
                                break;
                            }
                            CosmoGraph3CallBack();
                            _CurSubStep = SUB_STEP.Sequence_End;
                            break;
                        }
                        if ((resultCosmoDevice & 0x00000004) > 0)
                        {
                            // ERROR
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[COSMO STOP CHECK] 코스모 장치 ERROR가 발생되었습니다.";
                        }
                    }
                    else
                    {
                        if (_Waitter_Stopwatch.ElapsedMilliseconds > (30 * 1000))
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[COSMO STOP CHECK] 지정된 시간내 코스모 장치에서 리크 테스트를 완료하지 못했습니다..";
                        }
                    }
                    break;



                case SUB_STEP.Cosmo_STOP_Set:
                    ichk = M_Plc.CMD_CosmoStopSetting(3.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Cosmo_STOP_Check;
                        _Waitter_Stopwatch.Reset();
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = " 코스모 장치에 시작 명령 전송 확인중 에러가 발생하였습니다.";
                    }
                    break;



                case SUB_STEP.Error:
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_50);  // 시험 시간 측정용
                    eMsg        = errmsg;
                    substep     = _CurSubStep;
                    _CurSubStep = SUB_STEP.Sequence_End;
                    return SUB_STEP_RESULTR.Error;
                    break;
                case SUB_STEP.Sequence_End:
                    _LastCurSubStep = _CurSubStep;
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_50);  // 시험 시간 측정용
                    eMsg    = errmsg;
                    substep = _CurSubStep;
                    return SUB_STEP_RESULTR.Finished;
                    break;

            }
            eMsg = errmsg;
            substep = _CurSubStep;
            if (errmsg.Length > 0) return SUB_STEP_RESULTR.Error;
            else                   return SUB_STEP_RESULTR.Doing;

        }

        public class MBCREAD : IDisposable
        {
            #region Disposable
            private bool disposed = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        if (this._IsRun)
                        {
                            _RequestEnd = true;
                        }
                    }
                    disposed = true;
                }
            }
            ~MBCREAD()
            {
                Dispose(false);
            }
            #endregion
            #region 변수 및 외부 설정
            private bool _IsRun = false;
            public bool IsRun { set { this._IsRun = value; } get { return this._IsRun; } }

            private bool _Pause = true;
            public bool Pause { set { this._Pause = value; } }


            private bool _RequestEnd = false;
            public bool RequestEnd { set { this._RequestEnd = value; } }

            private Stopwatch _SW = new Stopwatch();
            public bool StopWatchRestart { set { this._SW.Restart(); } }
            private long _CheckTimeMs = 5; // 10msec
            public long CheckTimeMs { set { this._CheckTimeMs = value; } get { return this._CheckTimeMs; } }

            private bool _IsRTGVS = false;
            private M_DAQ.RealTimeGraphVIew _RealTimeGraphView = new M_DAQ.RealTimeGraphVIew();
            public bool RealTimeGraphViewSet(ref M_DAQ.RealTimeGraphVIew _RTGVS)
            {
                this._RealTimeGraphView = _RTGVS;
                this._IsRTGVS = true;
                return true;
            }

            private bool _IsECU = false;
            private M_ECU _Ecu = new M_ECU();
            public bool ECUSet(ref M_ECU _ecu)
            {
                this._Ecu = _ecu;
                this._IsECU = true;
                return true;
            }
            private double _Pos = 0.0;
            private double _Amp = 0.0;
            public double PosValue { get { return this._Pos; } }
            public double AmpValue { get { return this._Amp; } }
            private bool _ReadOK = false;
            public bool ReadCheck { get { return this._ReadOK; } }
            private long _StopWatchTime = 0;
            public long StopWatchTime { get { return this._StopWatchTime; } }
            #endregion
            public void DoWork()
            {
                bool ichk = false;
                double cTime = 0.0;
                double MBC_Pos = 0.0;
                double MBC_Amp = 0.0;
                _IsRun = true;
                _SW.Reset();
                _RealTimeGraphView.MBCDataClear();
                //Stopwatch _SW1 = new Stopwatch();
                Stopwatch _Loop = new Stopwatch();
                _StopWatchTime = _CheckTimeMs;
                _Loop.Restart();
                _SW.Restart();
                while (!_RequestEnd)
                {        
                    if ((_Loop.ElapsedMilliseconds >= _CheckTimeMs)&& (!this._Pause))
                    {
                        _Loop.Restart();
                        
                        cTime = (double)(_SW.ElapsedMilliseconds / 1000.0);                       
                        //_SW1.Restart();                                           
                        ichk = _Ecu.CMD_iMEB_MBCReadNew(ref MBC_Pos, ref MBC_Amp);
                        //_StopWatchTime=_SW1.ElapsedMilliseconds;
                        if ((ichk)&&(MBC_Pos != 0.0))
                        {
                            _RealTimeGraphView.MBCDataADD(cTime, MBC_Pos, MBC_Amp);                      
                            this._Pos = MBC_Pos;
                            this._Amp = MBC_Amp;
                            _ReadOK = true;                            
                        }
                        else _ReadOK = false;
                        
                    }
                    Thread.Sleep(1);                  
                }
                _IsRun = false;
            }

        }


        private int       _ECULeak_RetryCount = 0;                // 스텝 내부 재시도 횟수 사용
        private long      MBCReadInterval     = 0;                // (long)CurECULeakTest.Master_MBCINTERVAL;
        private string    msg = "";
        private Stopwatch _Sub_Waitter         = new Stopwatch();
        private bool      _MbcReadStart        = false;           // 진행중 MBC 데이터를 읽기 수행여부( 통신이 정상적으로 연결시 설정 인터벌마다 데이터 읽기를 수행)
        private Stopwatch _MBCStopwatch_Total  = new Stopwatch(); //
        private Stopwatch _MBCStopwatch        = new Stopwatch(); //
        private Stopwatch _ECU_SOLStopwatch    = new Stopwatch(); // ECU 차지 모드일때 솔을 계속 동작하기위하여

        private double    MotorFWDStartTime   = 0.0;              // 모터 전진 시작 시간 기록용
        private double    MotorFWDEndTime     = 0.0;              // 완료 혹은 타임아웃
        private double    MotorBWDStartTime   = 0.0;              // 모터 후진 시작 시간 기록용
        private double    MotorBWDEndTime     = 0.0;              // 완료 혹은 타임아웃

        private int       MBC_RefreshCount = 0;
        private Stopwatch ECU_TestPresent_Count = new Stopwatch();
        public  MBCREAD   _MBCReadFunction = new MBCREAD();
        private Thread    MBCReadThread = null;
        private int       MBCRReadVerifyCount = 0;                // 내부적으로 MBCREAD 읽은 횟수를 저장     
        private double    AfterMotorInitPosition = 1.0;           // 모터 초기화후 위치 저장용

        private int _retryMotorInitCmd = 0;
        private SUB_STEP_RESULTR TestSub_ECULeak(out SUB_STEP substep, out string eMsg,bool LogEnable)
        {
            string errmsg = "";
            string retMsg = "";
            string sndMsg = "";
            string canMsg = "";

            M_PLC.SOL Sol;
            M_PLC.CLAMP Clamp;

            bool   ichk              = false;
            double ChargeTime        = 0.0;
            double MBC_Pos           = 0.0;
            double MBC_Amp           = 0.0;
            bool   MBC_ReadCheck     = false;
            bool   MBC_ReadOK        = false;
            long   MBC_StopWatchTime = 0;
            byte[] EcuMBCReadData    = new byte[10];
            string retMsg1           = "";
            _LastCurSubStep = _CurSubStep;
            #region MBC 데이터 읽기
            if (_MbcReadStart)
            {
                if (_MBCReadFunction != null)
                {
                    MBC_Pos           = _MBCReadFunction.PosValue;
                    MBC_Amp           = _MBCReadFunction.AmpValue;
                    MBC_ReadCheck     = _MBCReadFunction.ReadCheck;
                    MBC_StopWatchTime = _MBCReadFunction.StopWatchTime;
                    MBC_ReadOK        = true;
                }
                else
                {
                    MBC_ReadOK = false;
                }
                    MBCCallBack(MBC_Pos, MBC_Amp, MBC_ReadCheck, MBC_StopWatchTime);
            }
            #endregion
            #region 테스트 프리젠트 신호 발생
            if (M_Ecu.isDiagOnMode)
            {
                if (ECU_TestPresent_Count.ElapsedMilliseconds > 2500.0)
                {
                    M_Ecu.CMD_iMEB_TestPresent(ref retMsg);
                    ECU_TestPresent_Count.Restart();
                }
            }
            #endregion
            switch (_CurSubStep)
            {
                case SUB_STEP.Init:
                    _SubTestTimer.Start(TestTime.TESTNAME.MOTORLEAKTEST);  // 시험 시간 측정용
                    #region PSU 전원 설정
                    double _SetPSU_Volt = CurECULeakTest.MasterPSUVoltsSet; 
                    bool Vo = M_Daq.PSU_SETVOLT(_SetPSU_Volt);
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] PSU Volt Output ? " + Vo.ToString());
                        Log.Info("[ECU Motor LeakTest] PSU Volt = " + string.Format("{0:F2}", _SetPSU_Volt) + "V");
                    }
                    _CurSubStep = SUB_STEP.Clamp_Set;
                    #endregion
                    break;
                case SUB_STEP.Clamp_Set:
                    #region 초기 클램프 구동(MC12/MC34 전진....)
                    Clamp = M_PLC.CLAMP.IGNITION_OFF | M_PLC.CLAMP.ECU_CONNECTOR_FWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_BWD | M_PLC.CLAMP.WORK_CLAMP;
                    ichk = M_Plc.CMD_ClampSetting((int)Clamp, 1.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Sol_Set;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] 클램프 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                #endregion
                case SUB_STEP.Sol_Set: // 솔 명령 정리.   최초 MC12 공급, MC34 차단 테스트 진행
                    #region MC12 진공공급 구성, 현단계에서는 안함.
                    Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 1.0);
                    if (ichk)
                    {
                        _Waitter_Stopwatch.Restart();
                        _CurSubStep = SUB_STEP.ECU_Motor_Power_Wait;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] 솔 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                #endregion
                case SUB_STEP.ECU_Motor_Power_Wait:
                    #region 모터 전원 온 시간 확인
                    double _SetMotorPowerOnTime = CurECULeakTest.MasterECUPowerOnTime * 1000.0;
                    if (_Waitter_Stopwatch.ElapsedMilliseconds > _SetMotorPowerOnTime)
                    {
                        // ECU Power ON
                        _Waitter_Stopwatch.Restart();
                        _CurSubStep = SUB_STEP.ECU_POWER_ON;
                    }
                    break;
                #endregion
                case SUB_STEP.ECU_POWER_ON:
                    #region ECU 전원 온
                    Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE | M_PLC.SOL.ECU_POWER_ON;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 1.0);
                    if (ichk)
                    {
                        _Waitter_Stopwatch.Restart();
                        _CurSubStep = SUB_STEP.ECU_IGN_On_Wait;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] ECU POWER ON 명령어 전송중 에러가 발생하였습니다.";
                    }
                    break;
                #endregion
                case SUB_STEP.ECU_IGN_On_Wait:
                    #region ECU 전원 온후 IGN 온 시간 확인(ECU ON -> IGN ON)
                    double _SetIGNOnTime = CurECULeakTest.MasterIGNOnTime * 1000.0;
                    if (_Waitter_Stopwatch.ElapsedMilliseconds > _SetIGNOnTime)
                    {
                        Clamp = M_PLC.CLAMP.IGNITION_ON | M_PLC.CLAMP.ECU_CONNECTOR_FWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                        Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_BWD | M_PLC.CLAMP.WORK_CLAMP;
                        ichk = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);  // 이그니션 온
                        if (ichk)
                        {
                            _ECULeak_RetryCount = 0;
                            _CurSubStep = SUB_STEP.ECU_CAN_OPEN;
                            if (LogEnable)
                            {
                                Log.Info("[ECU Motor LeakTest] ECU IGN ON 신호 발생 시점");
                                Log.Info("[ECU Motor LeakTest] _SetIGNOnTime duration(msec) = " + _Waitter_Stopwatch.ElapsedMilliseconds.ToString());
                            }
                            break;
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[CLAMP SET] IGN ON 명령어 전송중 에러가 발생하였습니다.";
                        }
                    }
                    break;
                #endregion
                case SUB_STEP.ECU_CAN_OPEN:
                    #region ECU CAN 통신 오픈
                    bool chk = M_Ecu.Init(Log, CurConfig);
                    if (chk)
                    {
                        _CurSubStep = SUB_STEP.ECU_ON_WAIT1;
                        Delay(500);
                        _Waitter_Stopwatch.Reset();
                        _Waitter_Stopwatch.Restart();
                    }
                    else
                    {
                        errmsg = "[ECU Motor LeakTest] CAN Init 에러가 발생하였습니다.";                     
                        ErrorNotifyCallBack(errmsg, 1);
                        _CurSubStep = SUB_STEP.Error;
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] PC측 CAN 통신 드라이버 시작");
                        Log.Info("[ECU Motor LeakTest] CAN Channel = " + CurConfig.CAN_PortNumber.ToString());
                        Log.Info("[ECU Motor LeakTest] CAN Tseg1 = " + CurConfig.CAN_Tseg1.ToString());
                        Log.Info("[ECU Motor LeakTest] CAN Tseg2 = " + CurConfig.CAN_Tseg2.ToString());
                        Log.Info("[ECU Motor LeakTest] CAN Connection(true/false) = " + chk.ToString());
                    }
                    break;
                #endregion
                case SUB_STEP.ECU_ON_WAIT1:
                    // 대기 시간
                    if (_Waitter_Stopwatch.ElapsedMilliseconds>2000)
                    {
                        _CurSubStep = SUB_STEP.ECU_CAN_MODE_Diag_On;
                        _Waitter_Stopwatch.Reset();
                    }
                    break;
                case SUB_STEP.ECU_CAN_MODE_Diag_On:
                    #region ECU DIAGNOSTIC MODE 오픈
                    chk = M_Ecu.CMD_iMEB_DiagnosticMode(ref retMsg);
                    M_Ecu.isDiagOnMode = false;
                    if (chk)
                    {
                        if (LogEnable)
                        {
                            Log.Info("[ECU DTC] 진단모드 진입(DIAG SERVICE)");
                        }
                        Delay(10);
                        // DTC Clear
                        bool chk2 = M_Ecu.CMD_iMEB_DTC_CLEAR(ref retMsg1, 0); // all group clear
                        if (!chk2)
                        {
                            errmsg = "[ECU Motor LeakTest] DIAG(DTC Claer명령 수행중) 에러 발생";
                            ErrorNotifyCallBack(errmsg, 1);
                            _CurSubStep = SUB_STEP.Error;
                        }
                        else
                        {
                            // Dignostic Mode 진입시 설정시간으로 MBC데이터는 계속 읽음.
                            _Waitter_Stopwatch.Reset();
                            _MBCStopwatch.Restart();
                            //_CurSubStep = SUB_STEP.LeakTest_Measurement_Start;
                            _CurSubStep = SUB_STEP.ECU_CAN_MotorInit;

                            M_Ecu.isDiagOnMode = true;
                            M_Ecu.CMD_iMEB_TestPresent(ref retMsg);
                            Log.Info("PC -> ECU Test Present Signal!! - 첫번째");
                            ECU_TestPresent_Count.Restart();
                        }
                        if (LogEnable)
                        {
                            Log.Info("[ECU DTC] 초기 DTC 클리어");
                            Log.Info("[ECU DTC] ECU->PC(ClearDTC)  = " + retMsg1);
                            Log.Info("[ECU DTC] 명령전송여부(True/False) = " + chk.ToString());
                        }
                        Delay(10);
                    }
                    else
                    {
                        int SetRetryCount = CurECULeakTest.MasterCANConnectionRetryCount;
                        if (SetRetryCount < _ECULeak_RetryCount)
                        {
                            _CurSubStep = SUB_STEP.ECU_CAN_OPEN;
                            _ECULeak_RetryCount++;
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[CAN] DIAG ON 응답이 이루워 지지 않았습니다..";
                            ErrorNotifyCallBack(errmsg, 1);
                        }
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] PC->ECU Diagnostic Mode Open = " + retMsg);
                        Log.Info("[ECU Motor LeakTest] 진단모드 진입 성공 여부(true/false) = " + chk.ToString());
                    }
                    break;
                #endregion
                case SUB_STEP.ECU_CAN_MotorInit: // 모터 초기화
                    #region 모터 초기화 명령 실행
                    lock (this)
                    {
                        //_MBCReadFunction.Pause = true;
                        chk = M_Ecu.CMD_iMEB_MotorInit(ref retMsg);
                        //_MBCReadFunction.Pause = false;
                    }
                    //Delay(35);
                    if (chk)
                    {
                        _Sub_Waitter.Restart();
                        //   _CurSubStep = SUB_STEP.ECU_CAN_MotorInit_Wait;
                        _CurSubStep = SUB_STEP.LeakTest_Measurement_Start;
                    }
                    else
                    {
                        errmsg = "[ECU Motor LeakTest] CAN  Motor Init 수행중 에러가 발생하였습니다.";
                        ErrorNotifyCallBack(errmsg, 1);
                        _retryMotorInitCmd++;
                        if (_retryMotorInitCmd > 3) _CurSubStep = SUB_STEP.Error;
                        else Log.Info("[ECU Motor LeakTest] 모터 초기화 명령 재전송 = " + _retryMotorInitCmd.ToString());
                        Delay(10);
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] 모터 초기화 명령");
                        Log.Info("[ECU Motor LeakTest] PC->ECU Motor Init = " + retMsg);
                        Log.Info("[ECU Motor LeakTest] 모터 초기화 명령 성공 여부(true/false) = " + chk.ToString());
                    }
                    #endregion
                    break;
                case SUB_STEP.LeakTest_Measurement_Start:
                    #region 데이터 읽기 모드 시작
                    MBCReadInterval = (long)CurECULeakTest.Master_MBCINTERVAL;
                    if (MBCReadInterval <= 5)   MBCReadInterval = 5;
                    _MBCReadFunction.CheckTimeMs      = MBCReadInterval;
                    _MBCReadFunction.StopWatchRestart = true;
                    _MBCReadFunction.Pause            = false;
                    
                    // DAQ data to Memory Save
                    //M_Daq.RTGraph.MBCDataClear();
                    M_Daq.RTGraph.StartDAQSave();

                    _Waitter1_Stopwatch.Restart();
                    _Sub_Waitter.Restart();

                    _MbcReadStart = true; // MBC 데이터 읽기 시작
                    _MBCStopwatch.Reset();
                    _MBCStopwatch.Start();
                    _MBCStopwatch_Total.Reset();
                    _MBCStopwatch_Total.Start();

                    _retryMotorInitCmd = 0;
                    _CurSubStep = SUB_STEP.ECU_CAN_MotorInit_Wait;
                    break;
                #endregion
                case SUB_STEP.TEST_ONLY:
                    // 임시대기 1~2초후 모터 초기화 전송
                    if (_Sub_Waitter.ElapsedMilliseconds > 3000)
                    {
                        _Sub_Waitter.Restart();
                        _CurSubStep = SUB_STEP.ECU_CAN_MotorInit;
                    }
                    break;
                case SUB_STEP.ECU_CAN_MotorInit_Wait:
                    #region 모터 초기화후 완료 대기시간 확인
                    double _SetMotorInitOnTime = 2.0 * 1000.0; // 임시 상수 처리, 모터 초기화명령 동작 시간 3초 예측
                    if (_Sub_Waitter.ElapsedMilliseconds > _SetMotorInitOnTime)
                    {
                        if (LogEnable)
                        {
                            Log.Info("[ECU Motor LeakTest] 모터 초기화 후 위치");
                            Log.Info("[ECU Motor LeakTest] MBC 데이터 읽기(True/False) = " + MBC_ReadOK.ToString());
                            Log.Info("[ECU Motor LeakTest] MBC 데이터 위치(0.5 ~ 1.9) = " + string.Format("{0:F2}", MBC_Pos));
                        }
                        _Sub_Waitter.Stop();
                        _CurSubStep = SUB_STEP.FWD_Clamp;
                        break;
                        // 모터 초기화 후 0.5 ~ 1.9 mm 위치에 존재하는지 확인
                        if ( (_MbcReadStart)&&(MBC_ReadOK) )
                        {
                            if ( (MBC_Pos>0.5)&&(MBC_Pos<1.9) )
                            {
                                _Sub_Waitter.Stop();
                                _CurSubStep = SUB_STEP.FWD_Clamp;
                            }
                        }                   
                        else
                        {
                            if (LogEnable)
                            {
                                Log.Info("[ECU Motor LeakTest] 모터 초기화 후 위치 에러");
                                Log.Info("[ECU Motor LeakTest] MBC 데이터 읽기(True/False) = " + MBC_ReadOK.ToString());
                                Log.Info("[ECU Motor LeakTest] MBC 데이터 위치(0.5 ~ 1.9) = " + string.Format("{0:F2}",MBC_Pos));
                            }
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[Motor Init] 모터 초기화 후 위치 정보 에러";
                            ErrorNotifyCallBack(errmsg, 1);
                        }
                    }
                    break;
                    #endregion
                case SUB_STEP.FWD_Clamp:
                    #region MC12 전진, MC34 후진 실행
                    Clamp = M_PLC.CLAMP.IGNITION_ON | M_PLC.CLAMP.ECU_CONNECTOR_FWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_BWD | M_PLC.CLAMP.WORK_CLAMP;
                    ichk  = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.ECU_FWD_SOL_SET;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] MC12 전진/MC34 후진 명령어 전송중 에러가 발생하였습니다.";
                        ErrorNotifyCallBack(errmsg, 1);
                    }          
                    break;
                    #endregion
                case SUB_STEP.ECU_FWD_SOL_SET:
                    #region ECU SOL 동작(0x44 Actuator)
                    int    S1_MotorFwdRpm         = CurECULeakTest.Master_FWDRPM;
                    double S1_MotorFwdDistance    = 0.0;
                    byte   S1_MotorFwdCode        = (byte)CurECULeakTest.Master_FWDSOLCODE;

                    if (MBC_ReadOK)
                    {
                        S1_MotorFwdDistance = 2.0;
                        //S1_MotorFwdDistance = 10.0; // 강제
                    }
                    else
                    {
                        /*
                        MBCCheck_RetryCount++;
                        if (MBCCheck_RetryCount>20)
                        {
                            errmsg = "[ECU Motor LeakTest] MBC Read 재시도 횟수가 20번을 넘었습니다.";
                            _CurSubStep = SUB_STEP.Error;
                            break;
                        }
                        break;
                        */
                        S1_MotorFwdDistance = 2.0;
                    }
                    // ECU Leak Mode Run
                    lock (this)
                    {
                        _MBCReadFunction.Pause = true;
                        ichk = M_Ecu.CMD_iMEB_LeakTest(S1_MotorFwdRpm, S1_MotorFwdDistance, S1_MotorFwdCode, out sndMsg, ref msg, ref canMsg);
                        _MBCReadFunction.Pause = false;
                    }
                    //ichk = M_Ecu.CMD_iMEB_LeakTest(S1_MotorFwdRpm, S1_MotorFwdDistance, S1_MotorFwdCode, out sndMsg, ref msg, ref canMsg);
                    
                    if (ichk)
                    {
                        _CurSubStep  = SUB_STEP.FWD_Charge;
                        _ECU_SOLStopwatch.Restart();
                    }
                    else
                    {
                        errmsg      = "[ECU Motor LeakTest] PC -> ECU FWD SOL 명령 수행중 에러가 발생하였습니다.";
                        ErrorNotifyCallBack(errmsg, 1);
                        _CurSubStep = SUB_STEP.Error;
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] Motor Leak Test 현위치에서 솔만 동작 = " + ichk.ToString());
                        Log.Info("[ECU Motor LeakTest] 전진 속도(RPM) = " + S1_MotorFwdRpm.ToString());
                        Log.Info("{ECU Motor LeakTest] 현위치 정보 읽기 = " + MBC_ReadOK.ToString());
                        Log.Info("[ECU Motor LeakTest] 전진 거리(mm) = " + S1_MotorFwdDistance.ToString()+"/"+string.Format("{0:F2}",MBC_Pos));
                        Log.Info("[ECU Motor LeakTest] 액츄에이터(HEX Code) = " + string.Format("{0:X2}", S1_MotorFwdCode));
                        Log.Info("[ECU Motor LeakTest] PC  전송 :" + sndMsg);
                        Log.Info("[ECU Motor LeakTest] ECU 응답 :" + msg);
                        Log.Info("[ECU Motor LeakTest] ECU 에러 코드 메세지 :" + canMsg);
                    }
                    break;
                    #endregion                    
                case SUB_STEP.FWD_Charge:
                    #region MC12측 진공 차징 시작(메인진공 밸브 개방, MC12측 밸브 개방)
                    Delay(500);
                    Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_OPEN | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_OPEN | M_PLC.SOL.ECU_POWER_ON;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 1.0);
                    if (ichk)
                    {
                        _Sub_Waitter.Restart();
                        _CurSubStep = SUB_STEP.FWD_Charge_Wait;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] MC12 진공공급 명령어 전송중 에러가 발생하였습니다.";
                        ErrorNotifyCallBack(errmsg, 1);
                    }
                    #endregion
                    break;             
                case SUB_STEP.FWD_Charge_Wait:
                    #region 차징 시간 확인, 추후 진공압 확인후 다음 스텝으로 이동기능 추가 후 메세지 삭제.
                    ChargeTime = CurECULeakTest.MasterVacuumChargeTime*1000.0;
                    if (_Sub_Waitter.ElapsedMilliseconds > ChargeTime)
                    {
                        Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_OPEN | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE | M_PLC.SOL.ECU_POWER_ON;
                        ichk = M_Plc.CMD_SolSetting((int)Sol, 1.0);

                        double NowVac1 = M_Daq.RTGraph.CurCH[2];

                        if ((ichk) && (NowVac1 > 475.0) && (NowVac1 < 525.0))                      
                        {
                            _Sub_Waitter.Stop();
                            _CurSubStep = SUB_STEP.FWD_Charge_STOP;
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[ECU Motor LeakTest] (전진 MC12)  진공압(475~500mmHg) 설정 범위 오버";
                            ErrorNotifyCallBack(errmsg, 1);
                        }
                        if (LogEnable)
                        {
                            Log.Info("[ECU Motor LeakTest] Vacuum Charge Check Vac1  = " + NowVac1.ToString());
                            Log.Info("[ECU Motor LeakTest] Chk(Sol Set)1 = " + ichk.ToString());
                        }
                    }
                    break;
                    #endregion
                case SUB_STEP.FWD_Charge_STOP:
                    #region 전진 솔 동작 후 진공 차징 후 ECU 진공 솔이 닫힐때까지 기달림
                    _Sub_Waitter.Stop();
                    _CurSubStep = SUB_STEP.FWD_Charge_STOP_Wait;
                    break;
                    #endregion
                case SUB_STEP.FWD_Charge_STOP_Wait:
                    #region ECU 동작솔 정지할때까지 기달림(3초)
                  
                    _CurSubStep = SUB_STEP.ECU_Motor_FWD_Run;

                    break;
                    #endregion
                case SUB_STEP.ECU_Motor_FWD_Run:
                    #region 모터 전진 명령 수행
                    int MotorFwdRpm            = CurECULeakTest.Master_FWDRPM;
                    double MotorFwdDistance    = CurECULeakTest.Master_FWDDISTANCE;
                    byte MotorFwdCode          = (byte)CurECULeakTest.Master_FWDSOLCODE;
                    double MotorFwdTestTime    = CurECULeakTest.Master_FWDTESTOPTIME * 1000.0;

                    // ECU Leak Mode Run
                    lock (this)
                    {
                        _MBCReadFunction.Pause = true;
                        ichk = M_Ecu.CMD_iMEB_LeakTest(MotorFwdRpm, MotorFwdDistance, MotorFwdCode, out sndMsg, ref msg, ref canMsg);
                        _MBCReadFunction.Pause = false;
                    }
                    //ichk = M_Ecu.CMD_iMEB_LeakTest(MotorFwdRpm, MotorFwdDistance, MotorFwdCode,out sndMsg, ref msg, ref canMsg);
                    MotorFWDStartTime = (_Waitter1_Stopwatch.ElapsedMilliseconds)/1000.0; // msec -> sec
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] Motor Leak Test(FWD) 명령전송 = " + ichk.ToString());
                        Log.Info("[ECU Motor LeakTest] 전진 속도(RPM) = " + MotorFwdRpm.ToString());
                        Log.Info("[ECU Motor LeakTest] 전진 거리(mm) = " + MotorFwdDistance.ToString());
                        Log.Info("[ECU Motor LeakTest] 액츄에이터(HEX Code) = " + string.Format("{0:X2}", MotorFwdCode));
                        Log.Info("[ECU Motor LeakTest] 시험시간(msec) = " + MotorFwdTestTime.ToString());
                        Log.Info("[ECU Motor LeakTest] PC  전송 :" + sndMsg);
                        Log.Info("[ECU Motor LeakTest] ECU 응답 :" + msg);
                        Log.Info("[ECU Motor LeakTest] ECU 에러 코드 메세지 :" + canMsg);
                    }
                    MBCRReadVerifyCount = 0;
                    _CurSubStep = SUB_STEP.ECU_Motor_FWD_Check;   
                    break;
                    #endregion
                case SUB_STEP.ECU_Motor_FWD_Check:
                    #region 모터 전진 완료 확인
                    ChargeTime         = CurECULeakTest.MasterVacuumChargeTime * 1000.0;
                    double FwdTestTime = CurECULeakTest.Master_FWDTESTOPTIME * 1000.0 + ChargeTime + 3000.0 + 3000.0; // 6000.0 is 6sec, 모터 초기화 시간 및 ECU 솔정지시간
                    double TagetPos    = CurECULeakTest.Master_FWDDISTANCE;
                    TagetPos           = TagetPos - 0.2; // 전진 오프셋
                    if (MBC_ReadOK)
                    {
                        
                        if ((MBC_Pos > TagetPos) || (MBC_Amp > 30.0))   // 전류값이 일정 이상일경우 전진 끝점까지 도달 했다고 판다
                        {
                            MotorFWDEndTime = _Waitter1_Stopwatch.ElapsedMilliseconds / 1000.0;
                            if (LogEnable)
                            {
                                Log.Info("[ECU Motor LeakTest] Motor 전진명령 종료 시간 : " + string.Format("{0:F2}sec", MotorFWDEndTime));
                                if (MBC_Amp>30.0)
                                {
                                    Log.Info("[ECU Motor LeakTest] Motor 전류값 한계치로 전진확인 : " + string.Format("{0:F2}sec", MBC_Amp) + "A, 위치 = "+  string.Format("{0:F2}",MBC_Pos));
                                    ErrorNotifyCallBack("모터 전진중 30A이상 부하 걸림", 1);
                                    if (MBC_Pos<32.0)
                                    {
                                        errmsg = "[Motor FWD] 모터 전진 명령후 32mm이하로 전진 = " + string.Format("{0:F2}",MBC_Pos);
                                        ErrorNotifyCallBack(errmsg, 1);
                                        _CurSubStep = SUB_STEP.Error;
                                        if (LogEnable)
                                        {
                                            Log.Info("[ECU Motor LeakTest] 모터 전진 명령후 32mm이하로 전진 : 에러 " );                                            
                                        }
                                        break;
                                    }
                                }
                            }
                            _CurSubStep = SUB_STEP.BWD_Clamp;
                            break;
                        }
                    }
                    if (_Waitter1_Stopwatch.ElapsedMilliseconds > FwdTestTime)
                    {
                        // 추후 전진 설정 시간보다 전진위치에 도달하지 못했을 경우 에러처리하기......
                        MotorFWDEndTime = _Waitter1_Stopwatch.ElapsedMilliseconds / 1000.0;
                        if (LogEnable)
                        {
                            Log.Info("[ECU Motor LeakTest] Motor 전진명령 종료 시간 : " + string.Format("{0:F2}sec", MotorFWDEndTime));
                        }
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[Motor FWD] 모터 전진 명령후 타임아웃 및 위치 정보 에러";
                        ErrorNotifyCallBack(errmsg, 1);
                    }
                    break;
                    #endregion                   
                case SUB_STEP.BWD_Clamp:
                    #region MC12 후진, MC34 전진 실행

                    Clamp = M_PLC.CLAMP.IGNITION_ON | M_PLC.CLAMP.ECU_CONNECTOR_FWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_BWD | M_PLC.CLAMP.WHEEL_PORT_34_FWD | M_PLC.CLAMP.WORK_CLAMP;
                    ichk  = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.ECU_BWD_SOL_SET;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] MC12 전진/MC34 후진 명령어 전송중 에러가 발생하였습니다.";
                        ErrorNotifyCallBack(errmsg, 1);
                    }          
                    break;
                    #endregion              
                case SUB_STEP.ECU_BWD_SOL_SET:
                    #region ECU SOL 동작(0x42 Actuator) -- 후진 관련 솔
                    int    S2_MotorFwdRpm         = CurECULeakTest.Master_BWDRPM; 
                    double S2_MotorFwdDistance    = 0.0;
                    byte   S2_MotorFwdCode        = (byte)CurECULeakTest.Master_BWDSOLCODE;

                    if (MBC_ReadOK)
                    {
                        S2_MotorFwdDistance = MBC_Pos;
                    }
                    else
                    {
                        S2_MotorFwdDistance = CurECULeakTest.Master_FWDDISTANCE;
                    }
                    // ECU Leak Mode Run
                    lock (this)
                    {
                        _MBCReadFunction.Pause = true;
                        ichk = M_Ecu.CMD_iMEB_LeakTest(S2_MotorFwdRpm, S2_MotorFwdDistance, S2_MotorFwdCode, out sndMsg, ref msg, ref canMsg);
                        _MBCReadFunction.Pause = false;
                    }
                    if (ichk)
                    {
                        //_FWDorBWD    = false; // false is FWD SOL Actuator
                        _CurSubStep = SUB_STEP.BWD_Charge;
                        _ECU_SOLStopwatch.Restart();
                    }
                    else
                    {
                        errmsg      = "[ECU Motor LeakTest] PC -> ECU FWD SOL 명령 수행중 에러가 발생하였습니다.";
                        ErrorNotifyCallBack(errmsg, 1);
                        _CurSubStep = SUB_STEP.Error;
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] Motor Leak Test(BWD) 현위치에서 솔만 동작 = " + ichk.ToString());
                        Log.Info("[ECU Motor LeakTest] 전진 속도(RPM) = " + S2_MotorFwdRpm.ToString());
                        Log.Info("{ECU Motor LeakTest] 현위치 정보 읽기 = " + MBC_ReadOK.ToString());
                        Log.Info("[ECU Motor LeakTest] 후진 거리(mm) = " + S2_MotorFwdDistance.ToString()+"/"+string.Format("{0:F2}",MBC_Pos));
                        Log.Info("[ECU Motor LeakTest] 액츄에이터(HEX Code) = " + string.Format("{0:X2}", S2_MotorFwdCode));
                        Log.Info("[ECU Motor LeakTest] PC  전송 :" + sndMsg);
                        Log.Info("[ECU Motor LeakTest] ECU 응답 :" + msg);
                        Log.Info("[ECU Motor LeakTest] ECU 에러 코드 메세지 :" + canMsg);
                    }
                    break;
                    #endregion
                case SUB_STEP.BWD_Charge:
                    #region MC34 진공 가압
                    Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_OPEN | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_OPEN | M_PLC.SOL.ECU_POWER_ON;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 1.0);
                    if (ichk)
                    {

                        _Sub_Waitter.Restart();
                        _CurSubStep = SUB_STEP.BWD_Charge_Wait;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[ECU Motor LeakTest] 진공 차징 명령 전송중 에러가 발생하였습니다.BWD";
                        ErrorNotifyCallBack(errmsg, 1);
                    }
                    break;
                    #endregion
                case SUB_STEP.BWD_Charge_Wait:
                    #region MC34측 진공 가압 확인
                    ChargeTime = CurECULeakTest.MasterVacuumChargeTime * 1000.0;
                    if (_Sub_Waitter.ElapsedMilliseconds > ChargeTime)
                    {
                        Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_OPEN | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE | M_PLC.SOL.ECU_POWER_ON;
                        ichk = M_Plc.CMD_SolSetting((int)Sol, 1.0);
                        double NowVac2 = M_Daq.RTGraph.CurCH[3];
                        if ((ichk) && (NowVac2 > 475.0) && (NowVac2 < 525.0))                        
                        {
                            _Sub_Waitter.Stop();
                            _CurSubStep = SUB_STEP.BWD_Charge_STOP;
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[ECU Motor LeakTest] (후진MC34) 진공압(475~525mmHg) 설절 범위 이탈";
                            ErrorNotifyCallBack(errmsg, 1);
                        }
                        if (LogEnable)
                        {
                            Log.Info("[ECU Motor LeakTest] Vacuum Charge Check Vac2  = " + NowVac2.ToString());
                            Log.Info("[ECU Motor LeakTest] Chk(Sol Set)1 = " + ichk.ToString());
                        }
                    }
                    break;
                    #endregion
                case SUB_STEP.BWD_Charge_STOP:
                    #region 후진 솔 동작 후 진공 차징 후 ECU 진공 솔이 닫힐때까지 기달림
                    _Sub_Waitter.Stop();
                    _CurSubStep = SUB_STEP.BWD_Charge_STOP_Wait;
                    break;
                    #endregion
                case SUB_STEP.BWD_Charge_STOP_Wait:
                    #region ECU 동작솔 정지할때까지 기달림(3초)                  
                    _CurSubStep = SUB_STEP.ECU_Motor_BWD_Run;
                    break;
                    #endregion
                case SUB_STEP.ECU_Motor_BWD_Run:
                    #region 모터 후진 명령 시작
                    int MotorBwdRpm            = CurECULeakTest.Master_BWDRPM;
                    //double MotorBwdDistance    = CurECULeakTest.Master_BWDDISTANCE;
                    double MotorBwdDistance    = 2.0;
                    byte MotorBwdCode          = (byte)CurECULeakTest.Master_BWDSOLCODE;
                    double MotorBwdTestTime    = CurECULeakTest.Master_BWDTESTOPTIME * 1000.0;
                    lock (this)
                    {
                        _MBCReadFunction.Pause = true;
                        ichk = M_Ecu.CMD_iMEB_LeakTest(MotorBwdRpm, MotorBwdDistance, MotorBwdCode, out sndMsg, ref msg, ref canMsg);
                        _MBCReadFunction.Pause = false;
                    }
                    MotorBWDStartTime = _Waitter1_Stopwatch.ElapsedMilliseconds / 1000.0;
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] Motor Leak Test(BWD) 명령전송 = " + ichk.ToString());
                        Log.Info("[ECU Motor LeakTest] 후진 속도(RPM) = " + MotorBwdRpm.ToString());
                        Log.Info("[ECU Motor LeakTest] 후진 위치(mm) = " + MotorBwdDistance.ToString());
                        Log.Info("[ECU Motor LeakTest] 액츄에이터(HEX Code) = " + string.Format("{0:X2}", MotorBwdCode));
                        Log.Info("[ECU Motor LeakTest] 시험시간(msec) = " + MotorBwdTestTime.ToString());
                        Log.Info("[ECU Motor LeakTest] PC  전송 :" + sndMsg);
                        Log.Info("[ECU Motor LeakTest] ECU 응답 :" + msg);
                        Log.Info("[ECU Motor LeakTest] ECU 에러 코드 메세지 :" + canMsg);
                    }
                    if(!ichk)
                    {
                        errmsg = "[ECU Motor LeakTest] 후진 명령 에러";
                        ErrorNotifyCallBack(errmsg, 1);
                        _Sub_Waitter.Stop();
                        _CurSubStep = SUB_STEP.BWD_Charge_STOP;
                    }
                    _CurSubStep = SUB_STEP.ECU_Motor_BWD_Check;
                    break;
                    #endregion
                case SUB_STEP.ECU_Motor_BWD_Check:                
                    #region 모터 후진 완료 확인
                    ChargeTime = CurECULeakTest.MasterVacuumChargeTime * 1000.0;
                    double BwdTestTime = CurECULeakTest.Master_BWDTESTOPTIME * 1000.0;
                    double fwdtesttime = CurECULeakTest.Master_FWDTESTOPTIME * 1000.0;

                    double TotalTestTime = fwdtesttime + BwdTestTime + ChargeTime*2.0 + 3000.0;
                           //TagetPos      = CurECULeakTest.Master_BWDDISTANCE;
                           TagetPos = 2.0;

                    TagetPos = TagetPos + 0.4; // 전진 오프셋
                    if (MBC_ReadOK)
                    {
                        if (MBC_Pos < TagetPos)
                        {
                            MotorBWDEndTime = _Waitter1_Stopwatch.ElapsedMilliseconds / 1000.0;
                            if (LogEnable)
                            {
                                Log.Info("[ECU Motor LeakTest] Motor 후진명령 최종 도달 위치 : " + string.Format("{0:F2}mm", MBC_Pos));
                                Log.Info("[ECU Motor LeakTest] Motor 후진명령 종료 시간(위치도달) : " + string.Format("{0:F2}sec", MotorBWDEndTime));
                            }
                            // 포지션완료로 종료 할 경우 3초정도 더 데이터를 측정한다.
                            _Sub_Waitter.Restart();
                            _CurSubStep = SUB_STEP.Over_Measuremrnt_Start;
                            break;
                        }
                    }
                    if (_Waitter1_Stopwatch.ElapsedMilliseconds > TotalTestTime)
                    {
                        // 추후 전진 설정 시간보다 전진위치에 도달하지 못했을 경우 에러처리하기......
                        MotorFWDEndTime = _Waitter1_Stopwatch.ElapsedMilliseconds / 1000.0;
                        if (LogEnable)
                        {
                            Log.Info("[ECU Motor LeakTest] Motor 후진명령 종료 시간(타임아웃) : " + string.Format("{0:F2}sec", MotorBWDEndTime));
                        }
                        _CurSubStep = SUB_STEP.ECU_CAN_MODE_Diag_Off;
                    }
                    break;
                    #endregion                
                case SUB_STEP.Over_Measuremrnt_Start:
                    double chkTime = 0.0;

                    chkTime = _Sub_Waitter.ElapsedMilliseconds / 1000.0;
                    if (chkTime>2.0)
                    {
                        _CurSubStep = SUB_STEP.ECU_CAN_MODE_Diag_Off;
                        _MBCReadFunction.Pause = true;  // MBC 쓰레드 포즈
                    }
                    break;



                case SUB_STEP.ECU_CAN_MODE_Diag_Off:
                    #region ECU 연결 종료
                    // DAQ STOP
                    M_Daq.RTGraph.StopDAQSave();
                    _MBCReadFunction.RequestEnd = true;
                    
                    Delay(50);
                    chk = M_Ecu.CMD_iMEB_DiagnosticMode_STOP(ref msg);
                    _MbcReadStart = false;                             // MBC 데이터 읽기 종료
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] 진단모드 종료 : " + msg);
                    }
                    M_Ecu.isDiagOnMode = false;
                    _SubTestTimer.Stop(TestTime.TESTNAME.MOTORLEAKTEST);  // 시험 시간 측정용
                    EcuMotorGraph1CallBack(MotorFWDStartTime,MotorFWDEndTime,MotorBWDStartTime,MotorBWDEndTime,false);  // Main화면 그래프 업데이트
                    Delay(50);
                    //EcuLeakTest2CallBack();
                    _CurSubStep = SUB_STEP.Sequence_End;
                    break;
                    #endregion
                case SUB_STEP.Error:
                    #region 실제 처리 되지 않는 코드, 에러시 스텝은 활성화되나 실행은 안됨....
                    _MbcReadStart = false;                             // MBC 데이터 읽기 종료
                    // PSU Power OFF
                    M_Daq.PSU_SETVOLT(0.0);
                    M_Ecu.isDiagOnMode = false;
                    //
                    _Waitter_Stopwatch.Stop();
                    _Waitter1_Stopwatch.Stop();
                    eMsg = errmsg;
                    substep = _CurSubStep;
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] 에러 = " + eMsg);
                        Log.Info("[ECU Motor LeakTest] 위치 = " + substep.ToString());
                    }
                    _SubTestTimer.Stop(TestTime.TESTNAME.MOTORLEAKTEST);  // 시험 시간 측정용
                    
                    EcuMotorGraph1CallBack(MotorFWDStartTime,MotorFWDEndTime,MotorBWDStartTime,MotorBWDEndTime,true);  // Main화면 그래프 업데이트, 에러시 화면에 표시 하기 위해
                    ErrorNotifyCallBack(eMsg, 1);
                    _CurSubStep = SUB_STEP.Sequence_End;
                    return SUB_STEP_RESULTR.Error;
                    #endregion
                    break;
                case SUB_STEP.Sequence_End:
                    ECU_TestPresent_Count.Stop();
                    // PSU Power OFF
                    M_Daq.PSU_SETVOLT(0.0);
                    // M_Ecu.isDiagOnMode = true;
                    M_Ecu.isDiagOnMode = false;
                    // Timer Clear & Stop
                    _Waitter_Stopwatch.Stop();
                    _Waitter1_Stopwatch.Stop();
                    EcuLeakTest2CallBack();                
                    _LastCurSubStep = _CurSubStep;
                    eMsg            = errmsg;
                    substep         = _CurSubStep;
                    return SUB_STEP_RESULTR.Finished;
          

            }
            if (_CurSubStep == SUB_STEP.Error)
            {
                _MbcReadStart               = false;                             // MBC 데이터 읽기 종료
                _MBCReadFunction.RequestEnd = true;
                //M_Daq.RTGraph.StopDAQSave();

                // PSU Power OFF
                M_Daq.PSU_SETVOLT(0.0);
                M_Ecu.isDiagOnMode = false;
                //
                _Waitter_Stopwatch.Stop();
                _Waitter1_Stopwatch.Stop();
                if (LogEnable)
                {
                    Log.Info("[ECU Motor LeakTest] 에러 = " + errmsg);
                    Log.Info("[ECU Motor LeakTest] 위치 = " + _CurSubStep.ToString());
                }
                _SubTestTimer.Stop(TestTime.TESTNAME.MOTORLEAKTEST);  // 시험 시간 측정용

                EcuMotorGraph1CallBack(MotorFWDStartTime, MotorFWDEndTime, MotorBWDStartTime, MotorBWDEndTime, true);  // Main화면 그래프 업데이트, 에러시 화면에 표시 하기 위해
                ErrorNotifyCallBack(errmsg, 1);
            }
            eMsg    = errmsg;
            substep = _CurSubStep;
            if (errmsg.Length > 0) return SUB_STEP_RESULTR.Error;
            else                   return SUB_STEP_RESULTR.Doing;

        }
        /// <summary>
        /// ECU DTC 확인 및 클리어
        /// 
        /// </summary>
        /// <param name="substep"></param>
        /// <param name="eMsg"></param>
        /// <param name="LogEnable"></param>
        /// <returns></returns>
        private SUB_STEP_RESULTR TestSub_ECUDTC(out SUB_STEP substep, out string eMsg, bool LogEnable)
        {
            string errmsg = "";
            M_PLC.SOL Sol;
            M_PLC.CLAMP Clamp;

            bool ichk;

            _LastCurSubStep = _CurSubStep;
            switch (_CurSubStep)
            {

                case SUB_STEP.Init: 
                    _SubTestTimer.Start(TestTime.TESTNAME.ECUDTCTEST);  // 시험 시간 측정용
                    #region PSU 전원 인가
                    double _SetPSU_Volt = CurEcuDtc.MasterPSUVoltsSet;
                    bool   Vo           = M_Daq.PSU_SETVOLT(_SetPSU_Volt);
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] PSU Volt Output ? " + Vo.ToString());
                        Log.Info("[ECU DTC] PSU Volt = " + string.Format("{0:F2}", _SetPSU_Volt) + "V");
                    }
                    _Waitter_Stopwatch.Restart();
                    _CurSubStep = SUB_STEP.Sol_Set;
                    #endregion
                    break;
                case SUB_STEP.Sol_Set: 
                    #region 솔 상태 설정(외부 진공 포트 닫음)
                    Sol = M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                    ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.Clamp_Set;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[SOL SET] 솔 명령어 전송중 에러가 발생하였습니다.";
                    }
                    #endregion
                    break;
                case SUB_STEP.Clamp_Set:
                    #region 클램프 상태 설정
                    Clamp = M_PLC.CLAMP.IGNITION_OFF | M_PLC.CLAMP.ECU_CONNECTOR_FWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                    Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_FWD | M_PLC.CLAMP.WORK_CLAMP;
                    ichk = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                    if (ichk)
                    {
                        _CurSubStep = SUB_STEP.ECU_Motor_Power_Wait;
                        break;
                    }
                    else
                    {
                        _CurSubStep = SUB_STEP.Error;
                        errmsg = "[CLAMP SET] 클램프 명령어 전송중 에러가 발생하였습니다.";
                    }
                    #endregion
                    break;
                case SUB_STEP.ECU_Motor_Power_Wait:
                    #region PSU 전원 인가후 대기(추후 삭제및 최단시간으로...모터 기동관련문제 확인 후)
                    double _SetMotorPowerOnTime = CurEcuDtc.MasterECUPowerOnTime * 1000.0 - 1000.0;
                    if (_Waitter_Stopwatch.ElapsedMilliseconds > _SetMotorPowerOnTime)
                    {
                        // ECU Power ON
                        Sol = M_PLC.SOL.ECU_POWER_ON | M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE | M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                        ichk = M_Plc.CMD_SolSetting((int)Sol, 2.0);
                        if (ichk)
                        {
                            _CurSubStep = SUB_STEP.ECU_IGN_On_Wait;
                            if (LogEnable)
                            {
                                Log.Info("[ECU DTC] _SetMotorPowerOnTime duration(msec) = " + _Waitter_Stopwatch.ElapsedMilliseconds.ToString());
                            }
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[SOL SET] ECU POWER ON 전송중 에러가 발생하였습니다.";
                        }
                        _Waitter_Stopwatch.Restart();
                    }
                    #endregion
                    break;
                case SUB_STEP.ECU_IGN_On_Wait:
                    #region IGN 신호 인가
                    double _SetIGNOnTime = CurEcuDtc.MasterIGNOnTime * 1000.0 - 1000;
                    if (_Waitter_Stopwatch.ElapsedMilliseconds > _SetIGNOnTime)
                    {
                        Clamp = M_PLC.CLAMP.IGNITION_ON | M_PLC.CLAMP.ECU_CONNECTOR_FWD | M_PLC.CLAMP.RESERVE_AIR_OPEN | M_PLC.CLAMP.RESERVE_DOWN;
                        Clamp = Clamp | M_PLC.CLAMP.SUCTION_FWD | M_PLC.CLAMP.WHEEL_PORT_12_FWD | M_PLC.CLAMP.WHEEL_PORT_34_FWD | M_PLC.CLAMP.WORK_CLAMP;
                        ichk = M_Plc.CMD_ClampSetting((int)Clamp, 2.0);
                        if (ichk)
                        {
                            _ECULeak_RetryCount = 0;
                            _CurSubStep = SUB_STEP.ECU_CAN_OPEN;
                            if (LogEnable)
                            {
                                Log.Info("[ECU DTC] _SetIGNOnTime duration(msec) = " + _Waitter_Stopwatch.ElapsedMilliseconds.ToString());
                            }
                            Delay(3000);
                            break;
                        }
                        else
                        {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[CLAMP SET] IGN ON 명령어 전송중 에러가 발생하였습니다.";
                        }
                    }
                    #endregion
                    break;
                case SUB_STEP.ECU_CAN_OPEN:
                    #region CAN OPEN
                    bool chk = M_Ecu.Init(Log, CurConfig);
                    if (chk)
                    {
                        _CurSubStep = SUB_STEP.ECU_CAN_MODE_Diag_On;
                        Delay(500);
                    }
                    else
                    {
                        errmsg = "[ECU DTC] CAN Init 에러가 발생하였습니다.";
                        _CurSubStep = SUB_STEP.Error;
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] CAN Channel = " + CurConfig.CAN_PortNumber.ToString());
                        Log.Info("[ECU DTC] CAN Tseg1 = " + CurConfig.CAN_Tseg1.ToString());
                        Log.Info("[ECU DTC] CAN Tseg2 = " + CurConfig.CAN_Tseg2.ToString());
                        Log.Info("[ECU DTC] CAN Connection(true/false) = " + chk.ToString());
                    }
                    #endregion
                    break;
                case SUB_STEP.ECU_CAN_MODE_Diag_On:
                    #region CAN DIAGNOSTIC MODE 진입
                    string retMsg = "";
                    chk = M_Ecu.CMD_iMEB_DiagnosticMode(ref retMsg);
                    M_Ecu.isDiagOnMode = false;

                    if (chk)
                    {
                        _CurSubStep        = SUB_STEP.ECU_CAN_DTC_CodeRead_0;
                        M_Ecu.isDiagOnMode = true;
                    }
                    else
                    {
                            _CurSubStep = SUB_STEP.Error;
                            errmsg = "[CAN] DIAG ON 응답이 이루워 지지 않았습니다..";
                    }
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] PC->ECU Diagnostic Mode Open = " + retMsg);
                        Log.Info("[ECU DTC] 진단모드 진입 성공 여부(true/false) = " + chk.ToString());
                    }
                    #endregion
                    break;
                case SUB_STEP.ECU_CAN_DTC_CodeRead_0:  // 기본적인 DTC 확인
                    #region DTC 코드 및 버전 확인
                    string retMsg1 = "";

                    // DTC Setting OFF
                    chk = M_Ecu.CMD_iMEB_DTC_Service(ref retMsg1, false);
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] ECU DTC Service 정지 ");
                        Log.Info("[ECU DTC] ECU->PC(DTC Setting OFF) = " + retMsg1);
                        Log.Info("[ECU DTC] 명령전송여부(True/False)  = " + chk.ToString());
                    }                    

                    chk = M_Ecu.CMD_iMEB_DTC_ReportNumber(ref retMsg1,0);
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] DTC 래포트 갯수 확인");
                        Log.Info("[ECU DTC] ECU->PC(DTC Report Number) = " + retMsg1);
                        Log.Info("[ECU DTC] 명령전송여부(True/False) = " + chk.ToString());
                    }

                    EcuDTC1CallBack(retMsg1, 0);

                    Delay(100);
                    M_ECU.DTC_RESULT dtcResult = new M_ECU.DTC_RESULT();
                    chk = M_Ecu.CMD_iMEB_DTC_Report(ref retMsg1,0, ref dtcResult);
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] DTC 래포트 내용 확인");
                        Log.Info("[ECU DTC] ECU->PC(Report) = " + retMsg1);
                        Log.Info("[ECU DTC] 명령전송여부(True/False) = " + chk.ToString());
                    }
                    _EcuDtcResultCheck = -1;
                    EcuDTC1CallBack(retMsg1, 1);
                    if (dtcResult.Counts>0)
                    {
                        _EcuDtcResultCheck = 0;
                        EcuDTC2CallBack(dtcResult, 0);
                        // OK/NG 판단후 

                    }
                    else
                    {
                        _EcuDtcResultCheck = 1;
                    }
                    Delay(50);

                    string VehicleName = "";
                    string SystemName  = "";
                    string HWSWVersion = "";
                    string ReleaseDate = "";
                    string PartNumber  = "";
                    chk = M_Ecu.CMD_iMEB_ReadECUIdentification(ref retMsg1,ref VehicleName, ref SystemName, ref HWSWVersion, ref ReleaseDate, ref PartNumber);
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] ECU ID 정보 확인");
                        Log.Info("[ECU DTC] ECU->PC(ReadECUIndentification) = " + retMsg1);
                        Log.Info("[ECU DTC] Vehicle Name = " + VehicleName);
                        Log.Info("[ECU DTC] System Name = " + SystemName);
                        Log.Info("[ECU DTC] HW/SW Version = " + HWSWVersion);
                        Log.Info("[ECU DTC] Release Date = " + ReleaseDate);
                        Log.Info("[ECU DTC] Part Number = " + PartNumber);
                        Log.Info("[ECU DTC] System Name = " + SystemName);
                        Log.Info("[ECU DTC] 명령전송여부(True/False) = " + chk.ToString());
                    }
                    DTC_ReadInfo.Model = VehicleName + " " + SystemName;
                    DTC_ReadInfo.PartNumber = PartNumber;
                    EcuDTC1CallBack(retMsg1, 2);
                    EcuDTC1CallBack(VehicleName+" "+SystemName, 3);                   
                    EcuDTC1CallBack(PartNumber, 4);
                   
                    string HWVersion = "";
                    string SWVersion = "";
                    if (HWSWVersion.Length==3)
                    {
                        HWVersion = HWSWVersion[0].ToString();
                        SWVersion = HWSWVersion[1].ToString() + HWSWVersion[2].ToString();
                    }
                    else
                    {
                        HWVersion = "?";
                        SWVersion = "?";
                    }
                    DTC_ReadInfo.HWVersion = HWVersion;
                    DTC_ReadInfo.SWVersion = SWVersion;
                    EcuDTC1CallBack(HWVersion, 5);
                    EcuDTC1CallBack(SWVersion, 6);
                   


                    Delay(50);
                    // Read ECU SW Version
                    string readSWVersion = "";
                    chk = M_Ecu.CMD_iMEB_DTC_ReadSWVersion(ref retMsg1, ref readSWVersion); 
                    if (LogEnable)
                    {
                        Log.Info("[ECU DTC] SW 정보 확인");
                        Log.Info("[ECU DTC] ECU->PC(ReadSWVersion)  = " + retMsg1);
                        Log.Info("[ECU DTC] SW  Version = " + readSWVersion);
                        Log.Info("[ECU DTC] 명령전송여부(True/False) = " + chk.ToString());
                    }
                    DTC_ReadInfo.MOBIS_HSWVersion = readSWVersion;
                    EcuDTC1CallBack(readSWVersion, 7);
                    
                    Delay(50);

                    if (_EcuDtcResultCheck == 1)
                    {
                        // DTC Clear
                        chk = M_Ecu.CMD_iMEB_DTC_CLEAR(ref retMsg1, 0); // all group clear
                        if (LogEnable)
                        {
                            Log.Info("[ECU DTC] DTC 클리어");
                            Log.Info("[ECU DTC] ECU->PC(ClearDTC)  = " + retMsg1);
                            Log.Info("[ECU DTC] 명령전송여부(True/False) = " + chk.ToString());
                        }                        
                        if (chk) EcuDTC1CallBack("DTC 클리어 명령 정상 실행", 9);
                        else EcuDTC1CallBack("DTC 클리어 명령중 에러 발생", 9);
                        
                    }

                    if (_EcuDtcResultCheck == -1)
                    {
                        EcuDTC1CallBack("DTC 확인 및 클리어 NG", 9);
                        if (LogEnable)
                        {
                            Log.Info("[ECU DTC] DTC 클리어 안함, 미확인 DTC 코드 검출됨.");
                        }
                    }
                    if (_EcuDtcResultCheck == 0)
                    {
                        EcuDTC1CallBack("DTC 확인중 프로그램 지연 발생 - 개발자 문의", 9);
                        if (LogEnable)
                        {
                            Log.Info("[ECU DTC] DTC 클리어 안함, 시간내 DTC 코드 확인 못함.");
                        }
                    }
                    Delay(50);
                    chk = M_Ecu.CMD_iMEB_ECUReset(ref retMsg1);
                    DTC_ReadInfo.ECUReset = chk;
                    if (chk) EcuDTC1CallBack("1", 8);
                    else EcuDTC1CallBack("?", 8);

                    _CurSubStep = SUB_STEP.ECU_CAN_DTC_CodeRead_1;   
                    #endregion
                    break;
                case SUB_STEP.ECU_CAN_DTC_CodeRead_1:
                    _CurSubStep = SUB_STEP.ECU_CAN_MODE_Diag_Off;
                    break;
                case SUB_STEP.ECU_CAN_MODE_Diag_Off:
                    chk = M_Ecu.CMD_iMEB_DiagnosticMode_STOP(ref msg);
                    if (LogEnable)
                    {
                        Log.Info("[ECU Motor LeakTest] 진단모드 종료 : " + msg);
                    }
                    M_Ecu.isDiagOnMode = false;

                    //EcuDTC2CallBack();  // Main화면 그래프 업데이트
                    _CurSubStep = SUB_STEP.Sequence_End;
                    break;



                case SUB_STEP.Error:
                    // PSU Power OFF
                    M_Daq.PSU_SETVOLT(0.0);
                    M_Ecu.isDiagOnMode = false;

                    _SubTestTimer.Stop(TestTime.TESTNAME.ECUDTCTEST);  // 시험 시간 측정용

                    //
                    eMsg        = errmsg;
                    substep     = _CurSubStep;
                    _CurSubStep = SUB_STEP.Sequence_End;
                    return SUB_STEP_RESULTR.Error;
                    break;
                case SUB_STEP.Sequence_End:
                    // PSU Power OFF
                    M_Daq.PSU_SETVOLT(0.0);
                    // M_Ecu.isDiagOnMode = true;
                    M_Ecu.isDiagOnMode = false;
                    _SubTestTimer.Stop(TestTime.TESTNAME.ECUDTCTEST);  // 시험 시간 측정용
                    // Timer Clear & Stop
                    _Waitter_Stopwatch.Stop();
                    _Waitter1_Stopwatch.Stop();

                    _LastCurSubStep = _CurSubStep;
                    eMsg = errmsg;
                    substep = _CurSubStep;
                    OKMsg_CallBack("시험이 완료되었습니다. 제품을 취출 하십시오");
                    return SUB_STEP_RESULTR.Finished;
                    break;

            }
            eMsg = errmsg;
            substep = _CurSubStep;
            if (errmsg.Length > 0) return SUB_STEP_RESULTR.Error;
            else                   return SUB_STEP_RESULTR.Doing;

        }
        #endregion
        /// <summary>
        /// 프로그램 시작시 초기화 수행
        /// </summary>
        /// <returns></returns>
        public bool System_Initialize()
        {
            List<string> errMsg = new List<string>();
            
            errMsg.Clear();

            this._Mode = MODE.BOOT;

            // Logger Set
            try
            {
                Log   = LogManager.GetLogger("Log");
                Debug = LogManager.GetLogger("Debug");
            }
            catch (Exception e1)
            {
                errMsg.Add("Logger Create :" + e1.Message);
                return false;
            }
            Log.Info("iMEB LEAK TEST Application Initialize...");

            // System Config File Read
            int Step = 0;
            try
            {
                CurConfig           = iMEB.RW_SysConfig.GetConfigData();
                Step++;
                CurExternalLeakTest = iMEB.RW_ExternalLeakTest.GetConfigData();
                Step++;
                CurInternalLeakTest = iMEB.RW_InternalLeakTest.GetConfigData();
                Step++;
                CurEcuDtc           = iMEB.RW_EcuDtc.GetConfigData();
                Step++;
                CurECULeakTest      = iMEB.RW_ECULeakTest.GetConfigData();
                Step++;
                // MES 사양 정보(로컬정보 읽기)
                CurMesSpec.GetConfigData();               
            }
            catch (Exception e1)
            {
                errMsg.Add("[System_Initialize]System Config File Read : Step="+Step.ToString()+"/" + e1.Message);
            }

            // PLC Communication Init
            this._PLC_isLive   = M_Plc.Init();
            if (_PLC_isLive)
            {
                bool iChk = M_Plc.CMD_ClearWorkArea(3.0);
                if (!iChk)
                {
                    Log.Error("[System_Initialize]PC->PLC 작업영역 클리어 수행시 통신에러가 발생하였습니다.");
                }
            }

            // DAQ NI-6259 Init
            this._DAQ_isLive   = M_Daq.Init(Log, 1000.0, CurConfig);

                // PSU Power OFF
                M_Daq.PSU_SETVOLT(0.0);
    
            // COSMO Init
            this._COSMO_isLive = M_Cosmo.Init(Log, CurConfig);

            // CAN CARD Init (ECU Connection)
            try
            {
                this._ECU_isLive = M_Ecu.Init(Log, CurConfig);
            }
            catch (Exception e1)
            {
                this._ECU_isLive = false;
                errMsg.Add("[System_Initialize]M_Ecu ->" + e1.Message);
                errMsg.Add("KVASER CAN CARD 초기화 하지 못했습니다.(드라이버 인식이 되지 않습니다)");
            }

            // MES,LDS,UDP 설정
            M_MesLds.LoggerSet(ref Log);
            bool ChkMES = M_MesLds.SetMES(CurConfig.MES_SERVERIP, CurConfig.MES_SERVERPORT);
            bool ChkLDS = M_MesLds.SetLDS(CurConfig.LDS_SERVERIP, CurConfig.LDS_SERVERPORT);
            bool ChkUDP = M_MesLds.SetUDP(CurConfig.UDP_SERVERIP, CurConfig.UDP_SERVERPORT);
            if (ChkUDP)
            {
                bool ChkUDPServer = M_MesLds.CreateUDPServer();
            }
            else
            {
                errMsg.Add("UDP Server 생성중 에러가 발생하였습니다.");
                errMsg.Add("UDP Server IP = " + CurConfig.UDP_SERVERIP);
                errMsg.Add("UDP Server PORT = " + CurConfig.UDP_SERVERPORT);
            }

            // MES 최초 모델 정보 확인 및 사양정보 불러오기
            string barcode      = "";
            string modelcode    = CurMesSpec.SPEC.LastProductCode;
            string plcmodelcode = "";
            string resultmsg    = "";
            int resultcode      = 0;

            if (CurMesSpec.SPEC.ImsiBarCode == null)
            {
                barcode = "01234567890";
            }
            else
            {
                barcode = CurMesSpec.SPEC.ImsiBarCode;
            }
            if (barcode.Length != 11) barcode = "01234567890";

            Log.Info("[MES] 모델 정보 요청");
            Log.Info("[MES] 모델코드 = " + modelcode);
            bool plcchk = M_Plc.CMD_Read_ModelCode(0.5, ref plcmodelcode);
            if (!plcchk)
            {
                Log.Info("[MES] PLC에서 모델정보 수신 에러");
                plcmodelcode = modelcode;
            }

            bool chk = M_MesLds.MES_RequestType(barcode, plcmodelcode, ref resultmsg, ref resultcode);
            if (!chk)
            {
                Log.Info("[MES] 모델 정보 요청시 응답이 없습니다. " + resultmsg);
                NGMsg_CallBack("[MES] 모델 정보 요청시 응답이 없습니다.\r\n");
            }
            else
            {
                Log.Info("[MES] 모델 세부 사양 정보 요청 " + plcmodelcode);
                Log.Info("[MES] 모델 정보 = " + resultmsg);
            }
            chk = M_MesLds.MES_RequestSpec(barcode, modelcode, ref resultmsg, ref resultcode);
            if (!chk) Log.Info("[MES] 모델 세부 사양 정보 업데이트 실패 = " + modelcode);
            else
            {
                // 업데이트 mes spec
                Log.Info("[MES] 모델 사양정보 업데이트 완료");
            }
            // Log Write
            errMsg.ForEach(delegate(string emsg)
            {
                Log.Info("System Init Error : " + emsg);
            });


            Log.Info("iMEB LEAK TEST Application Start...");
            this._Mode = MODE.MANUAL;
            return true;
        }
    }
}
