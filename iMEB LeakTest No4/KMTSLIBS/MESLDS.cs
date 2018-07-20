using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;

using NLog;

namespace iMEB_LeakTest_No4.KMTSLIBS
{
    /// <summary>
    /// 2017-10-16
    /// MES/LDS 연결용 TCP/IP , UDP 서버/클라이언트
    /// </summary>
    class MESLDS : IDisposable
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
                        if (UDPServerThread != null)
                        {
                            try
                            {
                                _RequestStop = true;
                                Delay(100);
                                UDPServerThread.Abort();
                                UDPServerThread = null;
                            }
                            catch (Exception e1) { }
                        }
                        if (UdpServer != null) UdpServer.Close();
                    }
                    disposed = true;
                }
            }
            ~MESLDS()
            {
                Dispose(false);
            }
        #endregion
        
        public class LDS_MSG_SPEC 
        {
            public const byte LDS_STX  = 0x02;
            public const byte LDS_ETX  = 0x03;
            public const byte LDS_BODY = (byte)'#';
            public const string LDS_CDOE_REQ_INFO  = "01";
            public const string LDS_CDOE_RESULT    = "02";
            public const string LDS_CDOE_STATUS    = "17";

            public const string LDS_PROCESS_NUM = "020"; // 020 = LEAK TEST 공정번호

            public const string LDS_PART_REQ = "1";
            public const string LDS_PART_RESULT = "E";

            private byte   STX             = LDS_STX;
            private string CODE            = "";
            private string PROCESS_NUM     = LDS_PROCESS_NUM;
            private string BODY_LENGTH     = "0002";
            private string BARCODE         = "12345678901";
            private string RCODE           = LDS_PART_REQ;
            private string JOBSTARTDATE    = "YYYYMMDDHHNNSS";
            private string JOBENDDATE      = "YYYYMMDDHHNNSS";
            private string BODY_START_CHAR = LDS_BODY.ToString();
            private string BODY_MSG        = "01";
            private string BODY_END_CHAR   = LDS_BODY.ToString();
            private byte   EXT             = LDS_ETX;

            private string SendMsg = "";

            public void GenMessage()
            {
                this.SendMsg = STX.ToString() + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT.ToString();
            }

        }

        #region 내부 사용 변수들
        private string MES_ServerIp;
        private string MES_ServetPort;
        private int    _MES_ServerPort;

        private string LDS_ServerIp;
        private string LDS_ServetPort;
        private int    _LDS_ServerPort;

        private string UDP_ServerIp;
        private string UDP_ServetPort;
        private int    _UDP_ServerPort;

        private Logger _Log = null;
        private bool _LogEnable = false;

        private IPEndPoint remoteEP    = null;
        private UdpClient UdpServer    = null;
        private Thread UDPServerThread = null;
        private bool _IsUDPServerRun   = false;
        private bool _RequestStop      = false;

        private bool _IsMESServerRun   = false;
        private TcpClient MESTcpClient = null;


        private bool _IsLDSServerRun = false;
        #endregion
        #region 기타 딜레이 및 참조용
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
        public void LoggerSet(ref Logger log)
        {
            this._Log       = log;
            this._LogEnable = true;
        }
        // 바이트 배열을 String으로 변환
        private string ByteToString(byte[] strByte) 
        {
            string str = Encoding.Default.GetString(strByte); 
            return str; 
        }
        // String을 바이트 배열로 변환
        private byte[] StringToByte(string str)
        {
            byte[] StrByte = Encoding.UTF8.GetBytes(str);
            return StrByte; 
        }


        #endregion
        #region 외부 설정값 관련
        public bool SetMES(string IP,string Port)
        {
            this.MES_ServerIp   = IP;
            this.MES_ServetPort = Port;

            if (int.TryParse(Port, out this._MES_ServerPort)) return true;
            else return false;            
        }
        public bool SetLDS(string IP, string Port)
        {
            this.LDS_ServerIp   = IP;
            this.LDS_ServetPort = Port;

            if (int.TryParse(Port, out this._LDS_ServerPort)) return true;
            else return false;
        }
        public bool SetUDP(string IP, string Port)
        {
            this.UDP_ServerIp = IP;
            this.UDP_ServetPort = Port;

            if (int.TryParse(Port, out this._UDP_ServerPort)) return true;
            else return false;
        }
        #endregion

        #region UDP 서버 관련
        public void RequestStop_UDPServer()
        {
            this._RequestStop = true;
        }
        public bool CreateUDPServer()
        {

            if (UDPServerThread != null)
            {
                UDPServerThread.Abort();
                UDPServerThread = null;
                Delay(100);
            }
            UDPServerThread = new Thread(new ThreadStart(DoUDPWork));
            //AutomaticThread.Priority = ThreadPriority.Highest;
            UDPServerThread.Start();
            Delay(50);
            return this._IsUDPServerRun;
        }
        private void DoUDPWork()
        {
            if (UdpServer != null)
            {
                try
                {
                    UdpServer.Close();
                    UdpServer = null;
                }
                catch (Exception e1) 
                {
                    this._IsUDPServerRun = false;
                    return;
                }
            }

            UdpServer = new UdpClient(this._UDP_ServerPort);
            // 클라이언트 IP를 담을 변수
            IPAddress UdpAddress = null;
            if (!(IPAddress.TryParse(this.UDP_ServerIp,out UdpAddress)))
            {
                this._IsUDPServerRun = false;
                return;
            }
            remoteEP = new IPEndPoint(UdpAddress, 0);     
       
            if (this._LogEnable)
            {
                this._Log.Info("[UDP Server] Create UDP Server IP = " + this.UDP_ServerIp);
                this._Log.Info("[UDP Server] Create UDP Server Port = " + string.Format("{0:N4}", this._UDP_ServerPort));
            }

            this._IsUDPServerRun = true;

            while (true)
            {
                if (this._RequestStop)
                {
                    this._IsUDPServerRun = false;
                    break;
                }
                // (2) 데이타 수신
                byte[] dgram = UdpServer.Receive(ref remoteEP);
                if (this._LogEnable)
                {
                    this._Log.Info("[UDP Server] Msg Recieve = " + ByteToString(dgram));
                    this._Log.Info("[UDP Server] Msg IP = " + remoteEP.ToString());
                }
                UDPMessageParse(remoteEP, dgram);
            }
        }
        private bool UDPMessageParse(IPEndPoint ep, byte[] data)
        {
            // 데이터 분석

            // 화면 업데이트

            // PLC 맵 업데이트

            return true;
        }
        public bool UDPSend()
        {
            if (!_IsUDPServerRun) return false;
            
                //UdpServer.Send(dgram, dgram.Length, remoteEP);
                //Console.WriteLine("[Send] {0} 로 {1} 바이트 송신", remoteEP.ToString(), dgram.Length);
            
            return true;
        }
        #endregion
        #region MES 서버 관련
        public bool CreateMESServer()
        {
            MESTcpClient = new TcpClient(this.MES_ServerIp,this._MES_ServerPort);
            if (!MESTcpClient.Connected)
            {
                _IsMESServerRun = false;
                return _IsMESServerRun;
            }
            return _IsMESServerRun;
        }
        #endregion
        #region LDS 서버 관련
        public bool SendLDS()
        {
            MESTcpClient = new TcpClient(this.MES_ServerIp, this._MES_ServerPort);
            if (!MESTcpClient.Connected)
            {
                _IsMESServerRun = false;
                return _IsMESServerRun;
            }
            return _IsMESServerRun;
        }

        #endregion

    }
}
