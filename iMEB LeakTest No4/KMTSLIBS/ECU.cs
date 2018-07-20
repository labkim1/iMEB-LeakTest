using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using canlibCLSNET;
using System.ComponentModel;
using NLog;
using System.Diagnostics;

namespace iMEBECUControl
{
    class ECU
    {
        // iMEB CAN Communication 통신관련 클래스
        // 참조 : c:\Program Files (x86)\Kvaser\Canlib\dotnet\win32\fw40\canlibCLSNET.dll  --> 32비트 버전사용,참조에 추가할 것
        #region 초기화 및 설정정보
        public enum FuctionResult : int
        {
            OK = 0,
            ERROR_UNKNOWN = -1,
            ERROR_UNDEF_0 = -2,
            ERROR_UNDEF_1 = -3,
            ERROR_UNDEF_2 = -4
        }

        private BackgroundWorker CanDumper;
        private int handle = -1;
        private int channel = -1;
        private int readHandle = -1;
        private bool onBus = false;
        private int flags = 0;       // flags의 기본 상태값을 확인하고 적용하기
        private Int32 _Channel = 1;       // 물리적 캔 포트는 1,2중 하나를 사용하므로 설정값은 1 혹은 2로 설정됨
        private Int32 _obhandle = -1;
        private string _LastErrorMessage = "";

        public Logger ECULog;
        #endregion

        public int CAN_Channel { set { this._Channel = value; } get { return this._Channel; } }
        public bool CAN_OnBus { set { this.onBus = value; } get { return this.onBus; } }
        public int CAN_Handle { set { this.handle = value; } get { return this.handle; } }

        public bool CAN_LIB_Init()
        {
            bool _chk = false;
            try
            {
                Canlib.canInitializeLibrary();
                _chk = true;
            }
            catch (Exception e1)
            {
                string _errmsg = e1.Message;
                _chk = false;
                return _chk;
            }
            return _chk;
        }
        public bool CAN_OpenChannel(int mode)
        {
            int _hnd = -1;
            if (mode == 0)
            {
                _hnd = Canlib.canOpenChannel(this._Channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
            }
            else
            {
                _hnd = Canlib.canOpenChannel(this._Channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
            }

            if (_hnd >= 0)
            {
                this.handle = _hnd;
                Canlib.canAccept(handle, 0x7DF, Canlib.canFILTER_SET_CODE_STD);
                Canlib.canAccept(handle, 0x7D0, Canlib.canFILTER_SET_MASK_STD);
                return true;
            }
            else return false;
        }
        public bool CAN_SetBusParameter(int seg1, int seg2)
        {
            int sjw = 0;
            int nosampl = 0;
            int syncmode = 0;

            // 속도설정은 고정으로...
            Canlib.canStatus cs = Canlib.canSetBusParams(handle, Canlib.canBITRATE_500K, seg1, seg2, sjw, nosampl, syncmode);
            CheckStatus("CAN SetBusParameter", cs);
            if (cs == Canlib.canStatus.canOK) return true;
            else return false;
        }
        public bool CAN_BUS(bool tf)
        {
            Canlib.canStatus cs;

            if (tf)
            {
                cs = Canlib.canBusOn(handle);
                CheckStatus("CAN BUS(" + tf.ToString() + ")", cs);
                if (cs == Canlib.canStatus.canOK) this.onBus = true;
                else this.onBus = false;
            }
            else
            {
                cs = Canlib.canBusOff(handle);
                if (cs == Canlib.canStatus.canOK) this.onBus = false;
            }

            return this.onBus;
        }
        public void CAN_CLOSE()
        {
            Canlib.canStatus status = Canlib.canClose(handle);
            CheckStatus("Closing channel", status);
            handle = -1;
        }

        public static byte[] LT_ACTUATOR = new byte[17] { 0x00, 0x02, 0x30, 0x32, 0x34, 0x36, 0x38, 0x3A, 0x3C, 0x3E, 0x40, 0x42, 0x44, 0x46, 0x48, 0x4A, 0x4C };
        /*
        {
            None                      = 0x00,
            PumpMotorRelay            = 0x02,
            FrontLeftValve_Inlet      = 0x30,
            FrontLeftValve_Outlet     = 0x32,
            FrontRightValve_Inlet     = 0x34,
            FrontRightValve_Outlet    = 0x36,
            RearLeftValve_Inlet       = 0x38,
            RearLeftValve_Outlet      = 0x3A,
            RearRightValve_Inlet      = 0x3C,
            RearRightValve_Outlet     = 0x3E,
            PSV1                      = 0x40,
            LPWSV2                    = 0x22,
            WSV3                      = 0x44,
            RCV4                      = 0x46,
            TCV5                      = 0x48,
            LPTCV6                    = 0x4A,
            LSV7                      = 0x4C
        }
        */


        public FuctionResult Initialize()
        {
            try
            {
                ECULog = LogManager.GetLogger("Operation");
            }
            catch (Exception e1)
            {
                return FuctionResult.ERROR_UNKNOWN;
            }
            ECULog.Info("ECU-CAN Initialize Start.");
            /*
            try
            {
                if (CanDumper!=null)
                {
                    CanDumper.CancelAsync();
                    CanDumper.Dispose();
                }
                CanDumper                        = new BackgroundWorker();
                CanDumper.DoWork                += CanDumpMessageLoop;
                CanDumper.WorkerReportsProgress  = true;
                CanDumper.ProgressChanged       += new ProgressChangedEventHandler(ProcessMessage);

                // ECU - CAN
                // 라이브러리 초기화
                Canlib.canInitializeLibrary();
                // 채널 오픈
                int hnd = Canlib.canOpenChannel(_Channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
                CheckStatus("Open channel", (Canlib.canStatus)hnd);
                if (hnd>=0) handle = hnd;
                // 비트레이트 설정(ECU 통신설정은 500K로 고정타입으로 사용함)
                Canlib.canStatus cs = Canlib.canSetBusParams(handle, Canlib.canBITRATE_500K, 0, 0, 0, 0, 0);
                CheckStatus("Setting bitrate to 500K " ,cs);
                // 버스 온
                Canlib.canStatus cs1 = Canlib.canBusOn(handle);
                CheckStatus("Bus on", cs1);
                if (cs1==Canlib.canStatus.canOK)
                {
                    onBus = true;
                    if (!CanDumper.IsBusy)
                    {
                        CanDumper.RunWorkerAsync();
                    }
                }

            }
            catch (Exception e1)
            {
                _LastErrorMessage = e1.Message;
                return FuctionResult.ERROR_UNKNOWN;
            }
            */
            return FuctionResult.OK;
        }
        public FuctionResult Close()
        {
            if (handle != -1)
            {
                Canlib.canStatus cs = Canlib.canBusOff(handle);
                CheckStatus("Bus off", cs);
            }
            onBus = false;
            Canlib.canStatus cs1 = Canlib.canClose(handle);
            CheckStatus("Closing channel", cs1);
            handle = -1;

            return FuctionResult.OK;
        }
        public FuctionResult SendShortMsg(int id, byte[] data, int dlc)
        {
            if (data.Length != 8) return FuctionResult.ERROR_UNKNOWN;
            string msg = String.Format("{0}  {1}  {2:x2} {3:x2} {4:x2} {5:x2} {6:x2} {7:x2} {8:x2} {9:x2}   to handle {10}",
                                 id, dlc, data[0], data[1], data[2], data[3], data[4],
                                 data[5], data[6], data[7], handle);
            Canlib.canStatus status = Canlib.canWrite(handle, id, data, dlc, flags);
            CheckStatus("Writing message " + msg, status);

            return FuctionResult.OK;
        }
        private void CanDumpMessageLoop(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Canlib.canStatus status;
            int id;
            byte[] data = new byte[8];
            int dlc;
            int flags;
            long time;
            bool noError = true;
            string msg;

            //Open up a new handle for reading
            readHandle = Canlib.canOpenChannel(channel, Canlib.canOPEN_ACCEPT_VIRTUAL);

            status = Canlib.canBusOn(readHandle);

            while (noError && onBus && readHandle >= 0)
            {
                status = Canlib.canReadWait(readHandle, out id, data, out dlc, out flags, out time, 50);

                if (status == Canlib.canStatus.canOK)
                {
                    if ((flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)
                    {
                        msg = "***ERROR FRAME RECEIVED***";
                    }
                    else
                    {
                        msg = String.Format("ID={0:x4}  {1}  {2:x2} {3:x2} {4:x2} {5:x2} {6:x2} {7:x2} {8:x2} {9:x2}   {10}\r",
                                                 id, dlc, data[0], data[1], data[2], data[3], data[4],
                                                 data[5], data[6], data[7], time);
                    }

                    //Sends the message to the ProcessMessage method
                    worker.ReportProgress(0, msg);
                }

                else if (status != Canlib.canStatus.canERR_NOMSG)
                {
                    //Sends the error status to the ProcessMessage method and breaks the loop
                    worker.ReportProgress(100, status);
                    noError = false;
                }
            }
            Canlib.canBusOff(readHandle);
        }
        private void ProcessMessage(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
            {
                string output = (string)e.UserState;
                // 실제 전송받은 데이터는 output에 있으므로 필요에 따라 화면에 표시 할수 있도록.....
                //outputBox.AppendText(output); 
                //outputBox.ScrollToEnd();
                ECULog.Info(output);
            }
            else
            {
                //CheckStatus("Reading", (Canlib.canStatus)e.UserState);
                string stmsg = ((Canlib.canStatus)e.UserState).ToString();
                ECULog.Info("Reading :" + stmsg);
            }
        }
        private void CheckStatus(String action, Canlib.canStatus status)
        {
            if (status != Canlib.canStatus.canOK)
            {
                String errorText = "";
                Canlib.canGetErrorText(status, out errorText);
                ECULog.Info(action + " failed: " + errorText);
            }
            else
            {
                ECULog.Info(action + " succeeded");
            }
        }


        #region 장문 메세지 처리


        const byte SingleFrame = 0x00;
        const byte FirstFrame = 0x10;
        const byte ConsecutiveFrame = 0x20;
        const byte FlowControl = 0x30;

        public enum RET : int
        {
            OK = 0,
            NG = -1,
            CAN_NotInitialize = -100,
            UNDEF = -1000
        }


        public bool CMD_iMEB_DiagnosticMode(ref string errmsg)
        {

            byte[] SendData_SF = new byte[8] { 0x02, 0x10, 0x60, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            errmsg = Temp_ECUDataToString("Diag.Open", _EcuID, readdata, ReadDC);
            return true;
        }

        public bool CMD_iMEB_DiagnosticMode_STOP(ref string errmsg)
        {

            byte[] SendData_SF = new byte[8] { 0x01, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            errmsg = Temp_ECUDataToString("Diag.Stop", _EcuID, readdata, ReadDC);
            return true;
        }


        private string MsgConv(int id, int dlc, byte[] data, long time)
        {
            string msg = "";
            msg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10:D5}\r",
                                     id, dlc, data[0], data[1], data[2], data[3], data[4],
                                     data[5], data[6], data[7], time);
            return msg;
        }

        private bool CanSend_Single(int id, byte[] data, int ReceiveRetryCount, ref int EcuID, ref byte[] READDATA, ref int ReadDataCount, ref string ErrMsg)
        {
            // Real Data Count Only...
            int _handle = this.handle;
            int _flags = this.flags;
            Canlib.canStatus status;

            byte[] SendData = new byte[8];
            byte[] TempData = new byte[8] { 0x30, 0x00, 0x02, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };  // Flow Control Signal

            int RecvID = 0;
            byte[] RecvData = new byte[8];
            byte[] RecvDataBuffer = new byte[50];
            int RecvDataIndex = 0;
            int RecvDLC;
            int RecvFlags;
            long RecvTime;
            long RecvSetTimeOut = 50;
            int _ReceiveRetryCount = 0;


            Canlib.canBusOn(_handle);
            // Data Send
            SendData = data;
            status = Canlib.canWrite(_handle, id, SendData, 8, _flags);
            CheckStatus("PC->ECU " + MsgConv(id, 8, data, 0), status);
            bool MsgReceive = false;
            while (!MsgReceive)
            {
                status = Canlib.canReadWait(_handle, out RecvID, RecvData, out RecvDLC, out RecvFlags, out RecvTime, RecvSetTimeOut);
                if ((status == Canlib.canStatus.canOK) && (!((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)))
                {
                    if (RecvID == 0x07D9)
                    {
                        CheckStatus("ECU->PC " + MsgConv(RecvID, 8, RecvData, 0), status);
                        if ((RecvData[0] & 0xF0) == 0x10)
                        { // FF Respons
                            status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                            for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                        }
                        if ((RecvData[0] & 0xF0) == 0x20)
                        { // CF Respons
                            for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                        }
                        if ((RecvData[0] & 0xF0) == 0x00)
                        { // Single Frame
                            MsgReceive = true;
                            break;
                        }
                    }
                }
                if (_ReceiveRetryCount > ReceiveRetryCount) break;
                _ReceiveRetryCount++;
            }
            Canlib.canBusOff(_handle);
            if (RecvDataIndex > 0)
            {
                EcuID = RecvID;
                READDATA = RecvDataBuffer;
                ReadDataCount = RecvDataIndex;
            }
            else
            {
                EcuID = RecvID;
                READDATA = RecvData;
                ReadDataCount = 8;
            }
            return true;
        }
        private bool CanSend_Frame(int id, byte[] data, int datalength, int ReceiveRetryCount, ref int EcuID, ref byte[] READDATA, ref int ReadDataCount, ref string ErrMsg)
        {
            // Real Data Count Only...
            int _handle = this.handle;
            int _flags = this.flags;
            Canlib.canStatus status;

            byte[] SendData = new byte[8];
            byte[] TempData = new byte[8] { 0x30, 0x00, 0x02, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };  // Flow Control Signal

            int RecvID = 0;
            byte[] RecvData = new byte[8];
            byte[] RecvDataBuffer = new byte[50];
            int RecvDataIndex = 0;
            int RecvDLC;
            int RecvFlags;
            long RecvTime;
            long RecvSetTimeOut = 50;
            int _ReceiveRetryCount = 0;


            Canlib.canBusOn(_handle);
            bool SendAll = false;
            int DataIndex = 0;

            while (!SendAll)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (DataIndex < datalength) SendData[i] = data[DataIndex];
                    else SendData[i] = 0x00;
                    DataIndex++;

                }
                // Data Send
                status = Canlib.canWrite(_handle, id, SendData, 8, _flags);
                CheckStatus("PC->ECU " + MsgConv(id, 8, SendData, 0), status);
                bool MsgReceive = false;
                while (!MsgReceive)
                {
                    status = Canlib.canReadWait(_handle, out RecvID, RecvData, out RecvDLC, out RecvFlags, out RecvTime, RecvSetTimeOut);
                    if ((status == Canlib.canStatus.canOK) && (!((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)))
                    {
                        if (RecvID == 0x07D9)
                        {
                            CheckStatus("ECU->PC " + MsgConv(RecvID, 8, RecvData, 0), status);
                            if ((RecvData[0] & 0xF0) == 0x30)
                            {
                                for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                                MsgReceive = true;
                                break;
                            }
                            if ((RecvData[0] & 0xF0) == 0x10)
                            { // FF Respons
                                status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                                for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                            }
                            if ((RecvData[0] & 0xF0) == 0x20)
                            { // CF Respons
                                for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                            }
                            if ((RecvData[0] & 0xF0) == 0x00)
                            { // Single Frame
                                MsgReceive = true;
                                break;
                            }
                        }
                    }
                    if (_ReceiveRetryCount > ReceiveRetryCount) break;
                    _ReceiveRetryCount++;
                }
                if (DataIndex > datalength)
                {
                    SendAll = true;
                    break;
                }
            }
            Canlib.canBusOff(_handle);
            if (RecvDataIndex > 0)
            {
                EcuID = RecvID;
                READDATA = RecvDataBuffer;
                ReadDataCount = RecvDataIndex;
            }
            else
            {
                EcuID = RecvID;
                READDATA = RecvData;
                ReadDataCount = 8;
            }
            return true;
        }






        private bool CanShortMsgSend(int id, byte[] data, int datalength, int retrycount, double timeout, ref int EcuID, ref byte[] READDATA, ref string errmsg)
        {
            bool _LoopChk = true;
            bool _Result = false;
            int _dlc = datalength;
            int _flags = this.flags;
            int _channel = this.channel;
            long _time;
            int _id = id;
            int _handle = this.handle;
            bool _noError = true;
            Canlib.canStatus status;
            string msg = "";
            byte[] _ReadData = new byte[8];
            Stopwatch _stopwatch = new Stopwatch();
            bool _OnlyOneReceiveOK = false;

            //Canlib.canBusOn(_handle);
            //status = Canlib.canWrite(_handle, _id, data, _dlc, _flags);
            //Open up a new handle for reading
            /*
            readHandle = Canlib.canOpenChannel(_channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
            status     = Canlib.canAccept(readHandle, 0x7DF, Canlib.canFILTER_SET_CODE_STD);
            status     = Canlib.canAccept(readHandle, 0x7D0, Canlib.canFILTER_SET_MASK_STD);
            status     = Canlib.canBusOn(readHandle);
            if (status == Canlib.canStatus.canOK) onBus = true;
            else                                  onBus = false;
            if (readHandle < 0)
            {
                errmsg = "canOpenChannel Handle create error";
                return false;
            }
            if (!onBus)
            {
                errmsg = "onBus false";
                return false;
            }
            */
            string sndmsg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10}\r",
                                     _id, _dlc, data[0], data[1], data[2], data[3], data[4],
                                     data[5], data[6], data[7], 0);
            CheckStatus("PC->ECU " + sndmsg, Canlib.canStatus.canOK);
            Canlib.canBusOn(_handle);
            status = Canlib.canWrite(_handle, _id, data, _dlc, _flags);
            _stopwatch.Reset();
            _stopwatch.Start();
            while (_LoopChk)
            {
                // TimeOut Check
                long st = _stopwatch.ElapsedMilliseconds;
                double _durationtime = st / 1000.0;  // ms -> sec 
                if (_durationtime > timeout)
                {
                    // 설정시간안에 메세지 및 처리를 못했을 경우 종료
                    msg = "timeout error";
                    _LoopChk = false;
                    break;
                }
                // Message Recieve Check
                status = Canlib.canReadWait(_handle, out _id, _ReadData, out _dlc, out _flags, out _time, 100); // 100ms timeout
                if (status == Canlib.canStatus.canOK)
                {
                    if ((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)
                    {
                        msg = "***ERROR FRAME RECEIVED***";
                    }
                    else
                    {
                        msg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10}\r",
                                                 _id, _dlc, _ReadData[0], _ReadData[1], _ReadData[2], _ReadData[3], _ReadData[4],
                                                 _ReadData[5], _ReadData[6], _ReadData[7], _time);
                        if (_id == 0x07D9)
                        {
                            for (int i = 0; i < 8; i++) READDATA[i] = _ReadData[i];
                            _Result = true;
                            _OnlyOneReceiveOK = true;
                        }
                    }
                }
                else if (status != Canlib.canStatus.canERR_NOMSG)
                {
                    msg = "TimeOut:NO MSG";
                }
                if (_id == 0x7D9)
                {
                    CheckStatus("ECU->PC " + msg, status);
                    Canlib.canBusOff(_handle);
                    _LoopChk = false;
                }
                //onBus = false;
            }
            return _Result;
        }

        public bool CanLongMsgSend(int id, byte[] data, int datalength, int retrycount, double timeout, ref int EcuID, ref byte[] READDATA, ref string errmsg)
        {
            if ((datalength < 9) || (data.Length < 9))
            {
                errmsg = "Command Data Length <=8";
                return false;
            }

            bool _LoopChk = true;
            int _EcuID;
            byte[] _EcuData = new byte[8];
            int _EcuDlc;
            int _EcuFlags;
            long _EcuTime;

            int _Ecu_FS = -1;     // 정상적으로 수신시 확인되는 값, FS FlowStatus, BS BlockSize, STmin
            int _Ecu_BS = 0;
            int _Ecu_STmin = 0;
            double _Ecu_Stmin_sec; // 소수점단위 계산
            bool _Ecu_FlowControl_Receive_OK = false;

            byte[] _Data_FF = new byte[8];
            byte[] _Data_CF = new byte[8];
            int _LastSendDataIndex = 0;
            Canlib.canStatus status;
            Stopwatch _stopwatch_total = new Stopwatch();
            Stopwatch _stopwatch_send = new Stopwatch();
            // 명령 만들기
            _Data_FF[0] = 0x10;

            Int32 _DataLength_12bit = 0x00000000;
            _DataLength_12bit = (Int32)datalength;
            _DataLength_12bit = _DataLength_12bit & 0x00000FFF;

            byte byte1Lownibble;
            byte byte2;

            byte1Lownibble = (byte)((_DataLength_12bit & 0x00000F00) >> 16);
            byte2 = (byte)(_DataLength_12bit & 0x000000FF);

            _Data_FF[0] = (byte)(0x10 | byte1Lownibble);
            _Data_FF[1] = byte2;
            _Data_FF[2] = data[0];
            _Data_FF[3] = data[1];
            _Data_FF[4] = data[2];
            _Data_FF[5] = data[3];
            _Data_FF[6] = data[4];
            _Data_FF[7] = data[5];

            _LastSendDataIndex = 6;
            //
            //Canlib.canBusOn(handle);
            //status = Canlib.canWrite(handle, id, _Data_FF, 8, flags);
            //Open up a new handle for reading
            readHandle = Canlib.canOpenChannel(channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
            status = Canlib.canAccept(readHandle, 0x7DF, Canlib.canFILTER_SET_CODE_STD);
            status = Canlib.canAccept(readHandle, 0x7D0, Canlib.canFILTER_SET_MASK_STD);
            status = Canlib.canBusOn(readHandle);
            if (status == Canlib.canStatus.canOK) onBus = true;
            else onBus = false;
            if (readHandle < 0)
            {
                errmsg = "canOpenChannel Handle create error";
                return false;
            }
            if (!onBus)
            {
                errmsg = "onBus false";
                return false;
            }
            _stopwatch_total.Reset();
            _stopwatch_total.Start();
            _stopwatch_send.Reset();
            _stopwatch_send.Start();
            // Receive Channel Open -> SEND
            Canlib.canBusOn(handle);
            status = Canlib.canWrite(handle, id, _Data_FF, 8, flags);
            while (_LoopChk)
            {
                // TimeOut Check
                long st = _stopwatch_total.ElapsedMilliseconds;
                double _durationtime = st / 1000.0;  // ms -> sec 
                if (_durationtime > timeout)
                {
                    // 설정시간안에 메세지 및 처리를 못했을 경우 종료
                    errmsg = "timeout error";
                    _LoopChk = false;
                    break;
                }
                // Message Recieve Check
                bool _RecvOK = false;
                status = Canlib.canReadWait(readHandle, out _EcuID, _EcuData, out _EcuDlc, out _EcuFlags, out _EcuTime, 100); // 100ms timeout
                if (status == Canlib.canStatus.canOK)
                {
                    if ((_EcuFlags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)
                    {
                        errmsg = "***ERROR FRAME RECEIVED***";
                    }
                    else
                    {
                        errmsg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10}\r",
                                                 _EcuID, _EcuDlc, _EcuData[0], _EcuData[1], _EcuData[2], _EcuData[3], _EcuData[4],
                                                 _EcuData[5], _EcuData[6], _EcuData[7], _EcuTime);
                        if (_EcuID == 0x07D9) _RecvOK = true;
                    }
                }
                else if (status != Canlib.canStatus.canERR_NOMSG)
                {
                    errmsg = "TimeOut:NO MSG";
                }
                //CheckStatus("Read message " + errmsg, status);
                //Canlib.canBusOff(readHandle);                
                if ((_EcuID == 0x07D9) && (_RecvOK))
                {
                    byte EcuFC_Check = _EcuData[0];
                    EcuFC_Check = (byte)((EcuFC_Check & 0x00F0) >> 4);
                    if (EcuFC_Check == 3)
                    {
                        _LoopChk = false;
                        _Ecu_FS = (_EcuData[0] & 0x000F);
                        _Ecu_BS = (int)(_EcuData[1] & 0x00FF);
                        _Ecu_STmin = (int)(_EcuData[2] & 0x00FF);
                        if (_Ecu_STmin <= 0x7F) _Ecu_Stmin_sec = _Ecu_STmin * 0.001;
                        if (_Ecu_STmin >= 0xF1) _Ecu_Stmin_sec = (_Ecu_STmin - 0xF0) * 0.0001;
                        _Ecu_FlowControl_Receive_OK = true;
                    }
                    string sndmsg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}\r",
                         id, 8, _Data_FF[0], _Data_FF[1], _Data_FF[2], _Data_FF[3], _Data_FF[4],
                         _Data_FF[5], _Data_FF[6], _Data_FF[7]);
                    CheckStatus("PC->ECU " + sndmsg, Canlib.canStatus.canOK);
                    CheckStatus("ECU->PC " + errmsg, status);
                }
            }

            Canlib.canClose(readHandle);

            if (!_Ecu_FlowControl_Receive_OK)
            {
                errmsg = "Ecu FlowControl not arrived";
                return false;
            }
            if (_Ecu_FS > 0)
            {
                errmsg = "Ecu FlowControl signal Busy";
                return false;
            }





            // ECU에서 FC를 정상적으로 수신후 데이터 전송....            
            _stopwatch_total.Reset();
            _stopwatch_total.Start();
            _stopwatch_send.Reset();
            _stopwatch_send.Start();
            _LoopChk = true;
            int _SequenceNumber = 1;
            byte _SN = (byte)_SequenceNumber;
            int _BlockSendLoopCount = _Ecu_BS;

            Canlib.canBusOn(handle);
            bool _EcuLastReceiveOK = false;

            // CF 전송후 수신확인.
            //Open up a new handle for reading
            readHandle = Canlib.canOpenChannel(channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
            status     = Canlib.canAccept(readHandle, 0x7DF, Canlib.canFILTER_SET_CODE_STD);
            status     = Canlib.canAccept(readHandle, 0x7D0, Canlib.canFILTER_SET_MASK_STD);
            status     = Canlib.canBusOn(readHandle);
            if (status == Canlib.canStatus.canOK) onBus = true;
            else onBus = false;
            if (readHandle < 0)
            {
                errmsg = "canOpenChannel Handle create error";
                return false;
            }
            if (!onBus)
            {
                errmsg = "onBus false";
                return false;
            }

            while (_LoopChk)
            {
                // TimeOut Check
                long st = _stopwatch_total.ElapsedMilliseconds;
                double _durationtime = st / 1000.0;  // ms -> sec 
                if (_durationtime > timeout)
                {
                    // 설정시간안에 메세지 및 처리를 못했을 경우 종료
                    errmsg = "timeout error";
                    _LoopChk = false;
                    break;
                }
                if (datalength < _LastSendDataIndex)
                {
                    _LoopChk = false;
                    break;
                }
                _stopwatch_send.Reset();
                _stopwatch_send.Start();
                // CF 전송
                for (int hs = 0; hs < _BlockSendLoopCount; hs++)
                {

                    // FF전송후 CF 데이터 전송 준비
                    _Data_CF[0] = 0x20;
                    _Data_CF[0] = (byte)(_Data_CF[0] + _SN);
                    for (int i = 0; i < 7; i++)
                    {
                        if (_LastSendDataIndex < datalength) _Data_CF[1 + i] = data[_LastSendDataIndex];
                        else _Data_CF[1 + i] = 0x00;// 0xAA;
                        _LastSendDataIndex++;
                    }
                    _SN++;
                    if (_SN > 16) _SN = 0;
                    status = Canlib.canWrite(handle, id, _Data_CF, 8, flags);
                    long _dt = _stopwatch_send.ElapsedMilliseconds;
                    string sndmsg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10:D5}ms\r",
                                             id, 8, _Data_CF[0], _Data_CF[1], _Data_CF[2], _Data_CF[3], _Data_CF[4],
                                             _Data_CF[5], _Data_CF[6], _Data_CF[7], _dt);
                    CheckStatus("PC->ECU " + sndmsg, status);
                    break; // Only One Run-----------------------------------------------실제 전송될 데이터 길이만큼만 전송함----------------------------------------------------------
                }


                // Message Recieve Check
                bool _RecvOK = false;

                bool _FinalReceiveCheck = true;
                Stopwatch _Final = new Stopwatch();
                _Final.Reset();
                _Final.Start();
                while (_FinalReceiveCheck)
                {
                    if ((_Final.ElapsedMilliseconds / 1000.0) > 3.0)
                    {
                        errmsg = "Finnal ECU Receive Msg TIMEOUT";
                        _FinalReceiveCheck = false;
                        break;
                    }
                    status = Canlib.canReadWait(readHandle, out _EcuID, _EcuData, out _EcuDlc, out _EcuFlags, out _EcuTime, 100); // 100ms timeout
                    if (status == Canlib.canStatus.canOK)
                    {
                        if ((_EcuFlags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)
                        {
                            errmsg = "***ERROR FRAME RECEIVED***";
                        }
                        else
                        {
                            errmsg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10}\r",
                                                     _EcuID, _EcuDlc, _EcuData[0], _EcuData[1], _EcuData[2], _EcuData[3], _EcuData[4],
                                                     _EcuData[5], _EcuData[6], _EcuData[7], _EcuTime);
                            if (_EcuID == 0x07D9)
                            {
                                _RecvOK = true;
                                CheckStatus("ECU->PC " + errmsg, status);
                            }
                        }
                    }
                    else if (status != Canlib.canStatus.canERR_NOMSG)
                    {
                        errmsg = "TimeOut:NO MSG";
                    }
                    //CheckStatus("Read message " + errmsg, status);
                    //Canlib.canBusOff(readHandle);                
                    if ((_EcuID == 0x07D9) && (_RecvOK))
                    {
                        Canlib.canBusOn(handle);
                        byte[] temp = new byte[8] { 0x30, 0x00, 0x02, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
                        status = Canlib.canWrite(handle, 0x07D1, _Data_FF, 8, flags);
                        CheckStatus("PC->ECU " + "PC AutoResponse(0x30,0x00,0x02,0xAA,0xAA,0xAA,0xAA,0xaa", status);
                        Canlib.canBusOff(handle);
                        status = Canlib.canReadWait(readHandle, out _EcuID, _EcuData, out _EcuDlc, out _EcuFlags, out _EcuTime, 100);
                        if (status == Canlib.canStatus.canOK)
                        {
                            errmsg = String.Format("ID={0:X4}  {1}  {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2}   {10}\r",
                                                    _EcuID, _EcuDlc, _EcuData[0], _EcuData[1], _EcuData[2], _EcuData[3], _EcuData[4],
                                                    _EcuData[5], _EcuData[6], _EcuData[7], _EcuTime);
                            if (_EcuID == 0x07D9)
                            {
                                _RecvOK = true;
                                CheckStatus("ECU->PC " + errmsg, status);
                            }
                        }
                        _FinalReceiveCheck = false;
                        _EcuLastReceiveOK = true;
                        _LoopChk = false;
                        break;
                    }
                }
            }
            if (readHandle >= 0) Canlib.canClose(readHandle);
            if (_EcuLastReceiveOK)
            {
                if (_EcuData[1] == 0x62) return true;
                return false;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// MotorSpeed=RPM=0~4095까지, MotorPosition=0.01mm~40.95mm까지
        /// </summary>
        /// <param name="MotorSpeed"></param>
        /// <param name="MotorPosition"></param>
        /// <returns></returns>
        public RET CMD_iMEB_LeakTest(int MotorSpeed, double MotorPosition, byte Actuator, ref string emsg)
        {
            // 모터속도,위치, 액츄에이터를 변환하고 배열에 담고 호출만...
            //if (!onBus) return RET.CAN_NotInitialize;

            byte[] COMMAND = new byte[12];
            byte highbyte = 0x00;
            byte lowbyte = 0x00;


            #region 모터속도,위치,동작기기 변환
            // Motor Speed Conversion
            Int16 motorspeed = 0;
            //if (MotorSpeed > 0x0FFF) MotorSpeed = 0x0FFF;
            //if (MotorSpeed < 0x0000) MotorSpeed = 0x0000;
            motorspeed = (short)MotorSpeed;
            // Position Conversion
            Int16 position = 0;
            if (MotorPosition < 0.0) MotorPosition = 0.0;
            if (MotorPosition > 40.95) MotorPosition = 40.95;
            double Pos = MotorPosition * 100.0;
            position = (short)Pos;
            // Actuator Conversion
            byte actuator = Actuator;
            #endregion
            #region 기본 명령어 설정 및 변수 대입
            // 명령 설정
            COMMAND[0] = 0x10;
            COMMAND[1] = 0x09;

            COMMAND[2] = 0x2F;
            COMMAND[3] = 0xF0;
            COMMAND[4] = 0x4A;
            COMMAND[5] = 0x03;

            highbyte = (byte)((motorspeed & 0xFF00) >> 8);
            lowbyte = (byte)(motorspeed & 0x00FF);
            COMMAND[6] = highbyte;
            COMMAND[7] = lowbyte;

            highbyte = (byte)((position & 0xFF00) >> 8);
            lowbyte = (byte)(position & 0x00FF);
            COMMAND[8] = 0x21;
            COMMAND[9] = highbyte;
            COMMAND[10] = lowbyte;
            COMMAND[11] = actuator;
            #endregion
            #region 명령 수행


            byte[] READDATA = new byte[50];
            int EcuID = 0;
            int ReadDataCount = 0;
            string ErrMsg = "";
            bool _Chk = CanSend_Frame(0x07D1, COMMAND, 12, 1, ref EcuID, ref  READDATA, ref  ReadDataCount, ref  ErrMsg);
            emsg = Temp_ECUDataToString("LeakTestStart", EcuID, READDATA, ReadDataCount);
            #endregion
            if (READDATA[2] == 0x6F) _Chk = true;
            if (_Chk) return RET.OK;
            else return RET.NG;
        }
        public RET CMD_iMEB_LeakTest_STOP(ref string emsg)
        {

            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x4A, 0x00, 0x00, 0x00, 0x00 };
            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            msg = Temp_ECUDataToString("LeakTestStop", _EcuID, readdata, ReadDC);
            emsg = msg;
            if (ret) return RET.OK;
            else return RET.NG;
        }
        public RET CMD_iMEB_LeakTest_SERVICE(ref string emsg)
        {

            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x4A, 0x02, 0x00, 0x00, 0x00 };
            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            msg = Temp_ECUDataToString("LeakTestSupport", _EcuID, readdata, ReadDC);
            emsg = msg;
            if (ret) return RET.OK;
            else return RET.NG;
        }
        public bool CMD_iMEB_MBCRead(ref double pos, ref double current)
        {
            byte[] SendData_SF = new byte[8] { 0x03, 0x22, 0x10, 0x03, 0x00, 0x00, 0x00, 0x00 };
            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            double _pos = 0.0;
            double _A = 0.0;

            Int16 pos_temp = 0x0000;
            Int16 A_temp = 0x0000;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);

            if (ret)
            {
                if (readdata[1] == 0x62)
                {
                    pos_temp = (Int16)(((readdata[4] & 0x00FF) << 8) | (readdata[5] & 0x00FF));
                    A_temp = (Int16)(((readdata[6] & 0x00FF) << 8) | (readdata[7] & 0x00FF));
                    double d_pos = (double)pos_temp;
                    double d_A = (double)A_temp;
                    pos = d_pos * 0.01;
                    current = d_A * 0.01;
                }
            }
            else
            {
                pos = 0.0;
                current = 0.0;
                return false;
            }
            return true;
        }

        public bool CMD_iMEB_NVHTest(byte mode, ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x05, 0x2F, 0xF0, 0x4B, 0x03, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            SendData_SF[5] = mode;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            errmsg = Temp_ECUDataToString("NVHTest", _EcuID, readdata, ReadDC);
            return true;
        }

        public bool CMD_iMEB_NVHTest_STOP(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x4B, 0x00, 0x00, 0x00, 0x00 };


            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            errmsg = Temp_ECUDataToString("NVHStop", _EcuID, readdata, ReadDC);
            return true;
        }

        public bool CMD_iMEB_NVHTest_SERVICE(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0xF0, 0x4B, 0x02, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            errmsg = Temp_ECUDataToString("NVHSupport", _EcuID, readdata, ReadDC);
            return ret;
        }

        public bool CMD_iMEB_TestPresent(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x02, 0x3E, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);
            //errmsg = Temp_ECUDataToString("TestPresent", _EcuID, readdata, ReadDC);
            return true;
        }
        public bool CMD_iMEB_MotorInit(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x49, 0x03, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);

            errmsg = Temp_ECUDataToString("MotorInit", _EcuID, readdata, ReadDC);

            return true;
        }
        public bool CMD_iMEB_ReadLocalID(Int16 localid, ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x03, 0x22, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            byte highbyte;
            byte lowbyte;

            highbyte = (byte)((localid & 0xFF00) >> 8);
            lowbyte = (byte)(localid & 0x00FF);
            SendData_SF[2] = highbyte;
            SendData_SF[3] = lowbyte;



            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);

            errmsg = Temp_ECUDataToString("MotorInit", _EcuID, readdata, ReadDC);

            return true;
        }
        public bool CMD_iMEB_ECUReset(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x02, 0x11, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg);

            errmsg = Temp_ECUDataToString("MotorInit", _EcuID, readdata, ReadDC);

            return true;
        }

        private string Temp_ECUDataToString(string head, int id, byte[] readdata, int length)
        {
            string msg = "ECU{" + head + ")";

            msg = msg + string.Format(" ID={0:X4} ", id);

            string tmp = "";
            for (int i = 0; i < length; i++)
            {
                tmp = tmp + string.Format("{0:X2} ", readdata[i]);
            }
            msg = msg + tmp + "\r";

            return msg;
        }

        public void Do_MBC_Read()
        {
            // 쓰레드 구동
            // 100ms마다 화면 업데이트 할수 있도록 구성
        }
        #endregion
    }
}

