using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Serialization;

using System.Runtime.CompilerServices;
using System.ComponentModel;

using System.Threading;
using System.IO.Ports;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

using NationalInstruments.DAQmx;
using NLog;


using System.Net;
using System.Net.Sockets;


using canlibCLSNET;
using System.Windows.Data;


namespace iMEB_LeakTest_No4.KMTSLIBS
{
    public delegate void UI_Refresh(int datacounts);
    /// <summary>
    /// iMEB 클래스 설명
    ///  - 전체 iMEB 관련하여 공통으로 사용되어질 요소에 대한 클래스 목록 구축
    ///  - 환경 변수
    ///  - 실행 정보
    ///  - 제어 참조
    /// </summary>
    public class iMEB
    {
        /// <summary>
        /// 서브 설정화일 - 내부 리크 테스트용
        /// </summary>
        public class ExternalLeakTest
        {
            // 프로그램 전반적으로 사용되는 변수 및 상수등을 화일에 기록하기 위한 클래스
            #region 전역 변수 및 상수
            public int TestCount { get; set; }
            #endregion
            #region 프로그램 내부에서 사용하는 변수 및 상수
            #endregion
            #region 사용자 수정 가능한 변수(프로퍼티 그리드에서 수정할수 있음)
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-1. 외부 리크 검사")]
            [Description("체크시 자동운전모드에서 시험 진행합니다.")]
            public bool Testable_ExternalLeakTest { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-2. NG시 재시도 횟수")]
            [Description("설정된 횟수만큼 반복 테스트를 진행합니다.(0=1회 정상시험, 2이상 값을 입력하십시오.")]
            public int Testable_RetryCount { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-3. NG시 자동운전 종료")]
            [Description("체크시 자동운전모드에서 시험 진행중 NG가 발생하면 종료합니다.")]
            public bool Testable_NGQuit { get; set; }

            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-1. 진공 설정압(mmHg)")]
            [Description("에어 리크 테스트기에 설정된 수동 레귤레이터 값을 입력하십시오.")]
            public double MasterVacuumSet  { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-2. 진공 설정압 상한값")]
            [Description("자동운전시 설정된 상한값 이상이 알람 동작합니다.")]
            public double MasterVacuumLimitMax { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-3. 진공 설정압 하한값")]
            [Description("자동운전시 설정된 하한값 이하시 알람 동작합니다.")]
            public double MasterVacuumLimitLow { get; set; }

            [Category("3. 시험 동작 및 합부 정보")]
            [DisplayName("3-1. 진공 가압 및 안정 시간(초,예:0.00)")]
            [Description("마스터 진공 가압 시간 및 안정 시간을 입력하십시오.")]
            public double MasterVacuumOperationTime { get; set; }
            [Category("3. 시험 동작 및 합부 정보")]
            [DisplayName("3-2. 측정 시간(초,예:0.00)")]
            [Description("마스터 진공 가압후 시험측정 시간을 입력하십시오. 실시간 측정 및 그래프 표시 구간")]
            public double MasterVacuumTestTime { get; set; }
            [Category("3. 시험 동작 및 합부 정보")]
            [DisplayName("3-3. 합격 압력 강하량 최소값(mmHg)")]
            [Description("측정 시작점과 마지막점의 압력차이의 최소값을 입력하십시오.")]
            public double Check_Low { get; set; }
            [Category("3. 시험 동작 및 합부 정보")]
            [DisplayName("3-4. 합격 압력 강하량 최대값(mmHg)")]
            [Description("측정 시작점과 마지막점의 압력차이의 최대값을 입력하십시오.")]
            public double Check_High { get; set; }
            #endregion
        }
        /// <summary>
        /// 
        /// </summary>
        public class RW_ExternalLeakTest
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "iMEB_LT4_ExternalLeakTest.xml"; //iMEB + LeakTest 4 + Config.xml
            public static ExternalLeakTest GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ExternalLeakTest));
                        ExternalLeakTest sxml = new ExternalLeakTest();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ExternalLeakTest));
                        ExternalLeakTest sc = (ExternalLeakTest)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(ExternalLeakTest config)
            {
                if (File.Exists(CONFIG_FNAME))
                {
                    // 화일이 존재 할 경우  삭제 
                    File.Delete(CONFIG_FNAME);
                    //return false; // don't do anything if file doesn't exist
                }

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(ExternalLeakTest));
                    xs.Serialize(fs, config);
                    fs.Flush();
                    fs.Close();
                    return true;
                }
            }
        }
        /// </summary>
        public class InternalLeakTest
        {
            // 프로그램 전반적으로 사용되는 변수 및 상수등을 화일에 기록하기 위한 클래스
            #region 전역 변수 및 상수
            public int TestCount { get; set; }
            #endregion
            #region 프로그램 내부에서 사용하는 변수 및 상수
            #endregion
            #region 사용자 수정 가능한 변수(프로퍼티 그리드에서 수정할수 있음)
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-1. 내부 리크 검사")]
            [Description("체크시 자동운전모드에서 시험 진행합니다.")]
            public bool Testable_InternalLeakTest { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-2. NG시 재시도 횟수")]
            [Description("설정된 횟수만큼 반복 테스트를 진행합니다.(0=1회 정상시험, 2이상 값을 입력하십시오.")]
            public int Testable_RetryCount { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-3. NG시 자동운전 종료")]
            [Description("체크시 자동운전모드에서 시험 진행중 NG가 발생하면 종료합니다.")]
            public bool Testable_NGQuit { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-4. 1.5바 시험 안함")]
            [Description("체크시 1.5바 공압 리크 시험을 OK 통과 됩니다.")]
            public bool Testable_15Pass { get; set; }


            #endregion
        }
        public class RW_InternalLeakTest
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "iMEB_LT4_InternalLeakTest.xml"; //iMEB + LeakTest 4 + Config.xml
            public static InternalLeakTest GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(InternalLeakTest));
                        InternalLeakTest sxml = new InternalLeakTest();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(InternalLeakTest));
                        InternalLeakTest sc = (InternalLeakTest)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(InternalLeakTest config)
            {
                if (File.Exists(CONFIG_FNAME))
                {
                    // 화일이 존재 할 경우  삭제 
                    File.Delete(CONFIG_FNAME);
                    //return false; // don't do anything if file doesn't exist
                }

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(InternalLeakTest));
                    xs.Serialize(fs, config);
                    fs.Flush();
                    fs.Close();
                    return true;
                }
            }
        }

        public class ECULeakTest
        {
            // 프로그램 전반적으로 사용되는 변수 및 상수등을 화일에 기록하기 위한 클래스
            #region 전역 변수 및 상수
            public int TestCount { get; set; }
            #endregion
            #region 프로그램 내부에서 사용하는 변수 및 상수
            #endregion
            #region 사용자 수정 가능한 변수(프로퍼티 그리드에서 수정할수 있음)
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-1. ECU 모터 리크 검사")]
            [Description("체크시 자동운전모드에서 시험 진행합니다.")]
            public bool Testable_ECULeakTest { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-2. 디버그 활성화")]
            [Description("체크시 로그창에 디버그 정보를 기록합니다.")]
            public bool Log_ECULeakTest { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-3. NG시 재시도 횟수")]
            [Description("설정된 횟수만큼 반복 테스트를 진행합니다.(0=1회 정상시험, 2이상 값을 입력하십시오.")]
            public int Testable_RetryCount { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-4. NG시 자동운전 종료")]
            [Description("체크시 자동운전모드에서 시험 진행중 NG가 발생하면 종료합니다.")]
            public bool Testable_NGQuit { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-5. 진공 차징 시간(초)")]
            [Description("진공 차징 시간을 입력하십시오.")]
            public double MasterVacuumChargeTime { get; set; }


            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-1. ECU PSU 공급전압")]
            [Description("ECU PSU(Motor+ECU) 전원을 입력하세요.")]
            public double MasterPSUVoltsSet { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-2. ECU 전원 투입 시간")]
            [Description("ECU 모터 전원인가 후 설정된 시간지연후 ECU전원을 인가합니다.")]
            public double MasterECUPowerOnTime { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-3. ECU IGN 투입 시간")]
            [Description("ECU 전원인가 후 설정된 시간지연후 IGN 신호를 인가합니다.")]
            public double MasterIGNOnTime { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-4. CAN 통신 연결 재시도 횟수")]
            [Description("ECU 통신 연결시 재시도 횟수를 입력하십시오. Diagnostic Into")]
            public int MasterCANConnectionRetryCount { get; set; }

            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-1. 전진 회전수(RPM)")]
            [Description("전진 시험시 설정 회전수를 입력하십시오.")]
            public int Master_FWDRPM { get; set; }
            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-2. 전진 거리(mm)")]
            [Description("전진 시험시 움질일 거리를 입력하십시오.(0~50mm)")]
            public int Master_FWDDISTANCE { get; set; }

            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-3. 전진 솔 동작 코드")]
            [Description("기 확인된 솔 동작 코드를 입력하십시오(십진수).")]
            public int Master_FWDSOLCODE { get; set; }
            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-4. 전진 시험 시간")]
            [Description("입력된 시간이 넘으면 자동으로 종료됩니다(초).")]
            public double Master_FWDTESTOPTIME { get; set; }

            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-5. 후진 회전수(RPM)")]
            [Description("전진 시험시 설정 회전수를 입력하십시오.")]
            public int Master_BWDRPM { get; set; }
            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-6. 후진 거리(mm)")]
            [Description("전진 시험시 움질일 거리를 입력하십시오.(0~50mm)")]
            public double Master_BWDDISTANCE { get; set; }

            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-7. 후진 솔 동작 코드")]
            [Description("기 확인된 솔 동작 코드를 입력하십시오(십진수).")]
            public int Master_BWDSOLCODE { get; set; }
            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-8. 후진 시험 시간")]
            [Description("입력된 시간이 넘으면 자동으로 종료됩니다(초).")]
            public int Master_BWDTESTOPTIME { get; set; }
            [Category("3. 시험 모드 설정 정보")]
            [DisplayName("3-9. 모터 정보 읽기 인터벌 속도(msec) ")]
            [Description("설정된 속도로 모터정보를 ECU에서 읽어 저장합니다.")]
            public int Master_MBCINTERVAL { get; set; }


            #endregion
        }
        public class RW_ECULeakTest
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "iMEB_LT4_ECULeakTest.xml"; //iMEB + LeakTest 4 + Config.xml
            public static ECULeakTest GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ECULeakTest));
                        ECULeakTest sxml = new ECULeakTest();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ECULeakTest));
                        ECULeakTest sc = (ECULeakTest)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(ECULeakTest config)
            {
                if (File.Exists(CONFIG_FNAME))
                {
                    // 화일이 존재 할 경우  삭제 
                    File.Delete(CONFIG_FNAME);
                    //return false; // don't do anything if file doesn't exist
                }

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(ECULeakTest));
                    xs.Serialize(fs, config);
                    fs.Flush();
                    fs.Close();
                    return true;
                }
            }
        }

        public class EcuDtc
        {
            // 프로그램 전반적으로 사용되는 변수 및 상수등을 화일에 기록하기 위한 클래스
            #region 전역 변수 및 상수

            public int TestCount { get; set; }
            #endregion
            #region 프로그램 내부에서 사용하는 변수 및 상수
            #endregion
            #region 사용자 수정 가능한 변수(프로퍼티 그리드에서 수정할수 있음)
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-1. ECU DTC 검사")]
            [Description("체크시 자동운전모드에서 시험 진행합니다.")]
            public bool Testable_ExternalLeakTest { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-2. 디버그 활성화")]
            [Description("체크시 로그창에 디버그 정보를 기록합니다.")]
            public bool Log_ECUDTCTest { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-3. NG시 재시도 횟수")]
            [Description("설정된 횟수만큼 반복 테스트를 진행합니다.(0=1회 정상시험, 2이상 값을 입력하십시오.")]
            public int Testable_RetryCount { get; set; }
            [Category("1. 시험 진행 정보")]
            [DisplayName("1-4. NG시 자동운전 종료")]
            [Description("체크시 자동운전모드에서 시험 진행중 NG가 발생하면 종료합니다.")]
            public bool Testable_NGQuit { get; set; }

            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-1. ECU PSU 공급전압")]
            [Description("ECU PSU(Motor+ECU) 전원을 입력하세요.")]
            public double MasterPSUVoltsSet { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-2. ECU 전원 투입 시간")]
            [Description("ECU 모터 전원인가 후 설정된 시간지연후 ECU전원을 인가합니다.")]
            public double MasterECUPowerOnTime { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-3. ECU IGN 투입 시간")]
            [Description("ECU 전원인가 후 설정된 시간지연후 IGN 신호를 인가합니다.")]
            public double MasterIGNOnTime { get; set; }
            [Category("2. 시험 장치 설정 정보")]
            [DisplayName("2-4. CAN 통신 연결 재시도 횟수")]
            [Description("ECU 통신 연결시 재시도 횟수를 입력하십시오. Diagnostic Into")]
            public int MasterCANConnectionRetryCount { get; set; }



            [Category("3. Control DTC Setting service DTC On/OFF")]
            [DisplayName("3-1. 시험진행전 DTC Setvice On 활성화 여부")]
            [Description("ECU 연결후 DTC Service DTC On.")]
            public bool FristDTCServiceON { get; set; }
            [Category("4. Read DTC Information")]
            [DisplayName("4-1. DTC StatusMask = ALL DTC")]
            [Description("ALL DTC = 0x08")]
            public bool DTC_ReadMode_ALLDTC { get; set; }
            [Category("4. Read DTC Information")]
            [DisplayName("4-2. DTC StatusMask = CURRENT DTC")]
            [Description("CURRENT DTC = 0x01")]
            public bool DTC_ReadMode_CUURRENTDTC { get; set; }

            [Category("5. Ignore DTC Codes")]
            [DisplayName("5-1.Ignore DTC's ")]
            [Description("Ignore DTC 코드를 ','구분으로 입력하십시오.")]
            public string DTC_Ignore_Codes { get; set; }

            #endregion
        }
        public class RW_EcuDtc
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "iMEB_LT4_EcuDtc.xml"; //iMEB + LeakTest 4 + Config.xml
            public static EcuDtc GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(EcuDtc));
                        EcuDtc sxml = new EcuDtc();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(EcuDtc));
                        EcuDtc sc = (EcuDtc)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(EcuDtc config)
            {
                if (File.Exists(CONFIG_FNAME))
                {
                    // 화일이 존재 할 경우  삭제 
                    File.Delete(CONFIG_FNAME);
                    //return false; // don't do anything if file doesn't exist
                }

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(EcuDtc));
                    xs.Serialize(fs, config);
                    fs.Flush();
                    fs.Close();
                    return true;
                }
            }
        }

        /// <summary>
        /// iMEB Leak Test No 4 Configuration details
        /// 프로그램 운영 환경 관련 설정
        /// </summary>        
        public class SysConfig
        {
            // 프로그램 전반적으로 사용되는 변수 및 상수등을 화일에 기록하기 위한 클래스
            #region 전역 변수 및 상수
            private string _Test_Information = "iMEB Leak Test Program by KMTS 2017";
            private string _SW_Version       = "1.431";
            // 2017.09.29 = ECU DTC 코드 관련 추가 
            // 2017.12.05 MES SPEC 변경 = 1.431
            #endregion
            #region 프로그램 내부에서 사용하는 변수 및 상수
            #endregion
            #region 사용자 수정 가능한 변수(프로퍼티 그리드에서 수정할수 있음)           
            [Category("1. 시험 정보")]
            [DisplayName("1-1. 시험기명 및 제작코드")]
            [ReadOnly(true)]
            [Description("본 항목은 제작사에서 발급된 설정 정보입니다.")]
            public string TestInforamtion { get { return this._Test_Information; } }

            [Category("1. 시험 정보")]
            [DisplayName("1-2. 프로그램 버전")]
            [ReadOnly(true)]
            [Description("본 항목은 제작사에서 발급된 설정 정보입니다.")]
            public string SW_Version { get { return this._SW_Version; }  }

            [Category("2. PCI-6259 관련")]
            [DisplayName("2-1. Analog Input CH0-Factor")]
            [Description("AI CH0 - FACTOR")]
            public double AI_CH0_Factor { get; set; }
            [Category("2. PCI-6259 관련")]
            [DisplayName("2-2. Analog Input CH0-Offset")]
            [Description("AI CH0 - OFFSET")]
            public double AI_CH0_Offset { get; set; }

            [Category("2. PCI-6259 관련")]
            [DisplayName("2-3. Analog Input CH1-Factor")]
            [Description("AI CH1 - FACTOR")]
            public double AI_CH1_Factor { get; set; }
            [Category("2. PCI-6259 관련")]
            [DisplayName("2-4. Analog Input CH1-Offset")]
            [Description("AI CH1 - OFFSET")]
            public double AI_CH1_Offset { get; set; }

            [Category("2. PCI-6259 관련")]
            [DisplayName("2-5. Analog Input CH2-Factor")]
            [Description("AI CH2 - FACTOR")]
            public double AI_CH2_Factor { get; set; }
            [Category("2. PCI-6259 관련")]
            [DisplayName("2-6. Analog Input CH2-Offset")]
            [Description("AI CH2 - OFFSET")]
            public double AI_CH2_Offset { get; set; }

            [Category("2. PCI-6259 관련")]
            [DisplayName("2-7. Analog Input CH3-Factor")]
            [Description("AI CH3 - FACTOR")]
            public double AI_CH3_Factor { get; set; }
            [Category("2. PCI-6259 관련")]
            [DisplayName("2-8. Analog Input CH3-Offset")]
            [Description("AI CH3 - OFFSET")]
            public double AI_CH3_Offset { get; set; }


            [Category("3. CAN CARD 관련")]
            [DisplayName("3-1. CAN Port Number")]
            [Description("Kvaser Card Port Number(0~1)")]
            public int CAN_PortNumber { get; set; }
            [Category("3. CAN CARD 관련")]
            [DisplayName("3-2. Tseg1")]
            [Description("Tsegment 1 (Default=5)")]
            public int CAN_Tseg1 { get; set; }
            [Category("3. CAN CARD 관련")]
            [DisplayName("3-3. Tseg2")]
            [Description("Tsegment 2 (Default=2)")]
            public int CAN_Tseg2 { get; set; }
            [Browsable(false)]
            [Category("3. CAN CARD 관련")]
            [DisplayName("3-4. BitRate 500K")]
            [Description("Bit Rate 500K, sjw=0, nosample mode, syncmode=0 로 설정합니다.")]
            public bool CAN_Default { get; set; }
            [Category("3. CAN CARD 관련")]
            [DisplayName("3-5. CAN Logger Enable")]
            [Description("CAN Message를 로그창에 기록여부를 설정합니다.")]
            public bool CAN_LogEnable { get; set; }




            [Category("4. COSMO(AIR LEAK TESTER) 관련")]
            [DisplayName("4-1. Com Port Number")]
            [Description("PC Serial Port Number(1~n),COSMO장치와 연결된 시리얼 포트번호를 입력하십시오.")]
            public int COSMO_PortNumber { get; set; }
            [Browsable(false)]
            [Category("4. COSMO(AIR LEAK TESTER) 관련")]
            [DisplayName("4-2. 115200,8,StopBits=1, Parity=None,")]
            [Description("COSMO장치에는 반드시 상기정보로 설정되어야 합니다.")]
            public bool COSMO_DefaultSet { get; set; }


            [Category("5. 내부 동작 타이머 설정")]
            [DisplayName("5-1. UI Refresh Timer Set(ms)")]
            [Description("UI 및 기타 이벤트 확인용 타이머 설정(50~250ms)")]
            public double UI_RefreshTimerValue { get; set; }
            [Category("5. 내부 동작 타이머 설정")]
            [DisplayName("5-2. MainControl Loop Timer Set(ms)")]
            [Description("메인 세부 스템 제어용 타이머 설정(10~100ms)")]
            public double MC_LoopTimerValue { get; set; }

            [Category("6. 네트워크 설정")]
            [DisplayName("6-1. MES 서버 주소")]
            [Description("서버 IP를 입력하십시오(xxx.x.x.xxx")]
            public string MES_SERVERIP { get; set; }
            [Category("6. 네트워크 설정")]
            [DisplayName("6-2. MES 서버 포트 번호")]
            [Description("서버 Port를 입력하십시오(xxxx")]
            public string MES_SERVERPORT { get; set; }
            [Category("6. 네트워크 설정")]
            [DisplayName("6-3. LDS 서버 주소")]
            [Description("서버 IP를 입력하십시오(xxx.x.x.xxx")]
            public string LDS_SERVERIP { get; set; }
            [Category("6. 네트워크 설정")]
            [DisplayName("6-4. LDS 서버 포트 번호")]
            [Description("서버 Port를 입력하십시오(xxxx")]
            public string LDS_SERVERPORT { get; set; }
            [Category("6. 네트워크 설정")]
            [DisplayName("6-5. UDP 서버 주소")]
            [Description("서버 IP를 입력하십시오(xxx.x.x.xxx")]
            public string UDP_SERVERIP { get; set; }
            [Category("6. 네트워크 설정")]
            [DisplayName("6-6. UDP 포트 번호")]
            [Description("UDP Port를 입력하십시오(xxxx")]
            public string UDP_SERVERPORT { get; set; }

            #endregion
        }
        public class RW_SysConfig
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "iMEB_LT4_Sysconfig.xml"; //iMEB + LeakTest 4 + Config.xml
            public static SysConfig GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(SysConfig));
                        SysConfig sxml = new SysConfig();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(SysConfig));
                        SysConfig sc = (SysConfig)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(SysConfig config)
            {
                if (File.Exists(CONFIG_FNAME))
                {
                    // 화일이 존재 할 경우  삭제 
                    File.Delete(CONFIG_FNAME);
                    //return false; // don't do anything if file doesn't exist
                }

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {                  
                        XmlSerializer xs = new XmlSerializer(typeof(SysConfig));
                        xs.Serialize(fs, config);
                        fs.Flush();
                        fs.Close();
                    return true;
                }
            }
        }



        // 그리드 결과 표시용 테이블 클래스
        public class TestResultTable // 시험결과 그리드테이블 표시용
        {
            public string Name { get; set; }
            public string Low { get; set; }
            public string Measurement { get; set; }
            public string High { get; set; }
            public string Unit { get; set; }
            public string Result { get; set; }
            public string Message { get; set; }

            public TestResultTable(string name,double low,double measurement, double high, string unit, string result, string msg)
            {
                this.Name        = name;
                this.Low         = string.Format("{0:F3}", low);
                this.Measurement = string.Format("{0:F3}", measurement);
                this.High        = string.Format("{0:F3}", high);
                this.Unit        = unit;
                this.Result      = result;
                this.Message     = msg;
            }
            public TestResultTable(string name, string low, string measurement, string high, string unit, string result, string msg)
            {
                this.Name        = name;
                this.Low         = low;
                this.Measurement = measurement;
                this.High        = high;
                this.Unit        = unit;
                this.Result      = result;
                this.Message     = msg;
            }
        }
        public enum DTC_Description : int
        {
            ECU_Voltage_supply_high_voltage                               = 0x110101,
            ECU_Voltage_supply_low_voltage                                = 0x110201,
            Wheel_Speed_sensor_front_left_open_or_shoth                   = 0x120001,
            Wheel_Speed_sensor_front_left_range_performance_intermittent  = 0x120102,
            Wheel_Speed_sensor_front_left_invalid_no_signal               = 0x120202,
            Wheel_Speed_sensor_front_right_open_or_short                  = 0x120301,
            Wheel_Speed_sensor_front_right_range_performance_intermittent = 0x120402,
            Wheel_Speed_sensor_front_right_ivalid_no_signal               = 0x120502,
            Wheel_Speed_sensor_rear_left_open_or_short                    = 0x120601,
            Wheel_Speed_sensor_rear_left_range_performance_intermittent   = 0x120702,
            Wheel_Speed_sensor_rear_left_invalid_no_signal                = 0x120802,
            Wheel_Speed_sensor_rear_right_open_or_short                   = 0x120901,
            Wheel_Speed_sensor_rear_right_range_performance_intermittent  = 0x121002,
            Wheel_Speed_sensor_rear_right_invalid_no_signal               = 0x121102,
            Wheel_Speed_sensor_frequency_error                            = 0x121302,
            Pressur_Sensor_fault_electrical                               = 0x123501,
            Pressur_Sensor_fault_signal_error                             = 0x123702,
            Steering_Angle_sensor_signal_error                            = 0x126002,
            Steering_Angle_sensor_not_calibrated                          = 0x126104,
            Lateral_G_sensor_Yaw_rate_sensor_signal_error                 = 0x128302,
            AX_Calibartion_error                                          = 0x128504,
            IMU_Signal_error                                              = 0x128602,
            AVH_Switch_open_or_short                                      = 0x135801,
            TCS_ESC_switch_error                                          = 0x150301,
            Brake_Light_switch_error                                      = 0x154201,
            Clutch_Signal_error                                           = 0x152001,
            Reverse_Gear_signal_error                                     = 0x152701,
            ECU_hardware_error                                            = 0x160404,
            P_CAN_bus_off_error                                           = 0x160E08,
            CAN_Time_out_EMS                                              = 0x161108,
            CAN_Time_out_TCU                                              = 0x161208,
            Wrong_EMS_CAN_message                                         = 0x161308,
            C_CAN_bus_off_error                                           = 0x161608,
            CAN_Time_out_SAS                                              = 0x162308,
            ESC_Implausible_control                                       = 0x162604,
            _4WD_Message_timeout                                          = 0x162708,
            CAN_timeout_SCC                                               = 0x163808,
            CAN_timeout_YRS                                               = 0x164308,
            EMS15_Signal_error                                            = 0x164908,
            Wrong_SCC_CAN_message                                         = 0x165008,
            CAN_timeout_EPB                                               = 0x165108,
            EPB_Fail                                                      = 0x165208,
            VSM2_Message_timeout                                          = 0x168708,
            VSM2_Signal_error                                             = 0x168808,
            AEB_Message_timeout                                           = 0x16B687,
            AEB_Signal_error                                              = 0x16B781,
            Variant_coding_error                                          = 0x170204,
            CAN_Timeout_CGW                                               = 0x181208,
            Valve_Relay_error_electrical                                  = 0x211201,
            BLA_Open_or_Short_error                                       = 0x213001,
            ESS_Relay_Open_or_Short                                       = 0x213101,
            SCC_Signal_error                                              = 0x222808,
            Solenoid_Valve_fault                                          = 0x238001,
            Return_Pump_fault_motor_electrical                            = 0x240201,

            EPB_Operation_incomplete_due_to_insufficient_supply_power     = 0x110001,
            EPB_Switch_falut                                              = 0x150101,
            EPB_Unsuccessful_latch                                        = 0x220201,
            EPB_RL_motor_fault                                            = 0x222001,
            EPB_RR_motor_fault                                            = 0x222401,
            EPB_RL_motor_open_or_short                                    = 0x241601,
            EPB_RR_motor_open_or_short                                    = 0x241701
        }
        public class DTCResultTable // 시험결과 그리드테이블 표시용(DTC)
        {
            public string DTC { get; set; }
            public string Status { get; set; }
            public string Description { get; set; }
            public string LowData { get; set; }

            public DTCResultTable(string dtc, string status, string description, string lowdata)
            {
                this.DTC         = dtc;
                this.Status      = status;
                this.Description = description;
                this.LowData     = lowdata;
            }
            public DTCResultTable(Int32 dtc, byte status,string checkmsg)
            {
                this.DTC         = string.Format("C{0:X6}",dtc);
                this.Status      = string.Format("0x{0:X2}", status);
                this.Description = "No information => " + checkmsg;
                foreach (DTC_Description dtcCode in Enum.GetValues(typeof(DTC_Description)))
                {
                    int nDtcCode = (int)dtcCode;
                    if (dtc==nDtcCode)
                    {
                        this.Description = Enum.GetName(typeof(DTC_Description), dtc)+"=> "+checkmsg;
                        this.Description = Description.Replace("_", " ");
                    }
                }
                this.LowData     = string.Format("0x{0:X6}{1:X2}",dtc, status); 
            }
        }

    }
   public class M_COSMO : IDisposable
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
                        if (Cosmo1!=null)
                        {
                            Cosmo1.CloseComm();
                        }
                    }
                    disposed = true;
                }
            }
            ~M_COSMO()
            {
                Dispose(false);
            }
        #endregion
       // 델리게이트
            public event SerialComState_UI_Refresh_Delegate MainUIUpdate_ComState;
            public event SerialComState_GRAPH_Refresh_Delegate MainGraphUpdate_Cosmo;
       // 변수 관련
       public SerialComm Cosmo1;                                                            // #4번기에서 한대만 운영

       private string Cosmo1Buffer = System.String.Empty;
       public struct COSMO_D_Format                                                         // COSMO D Format Struct
       {
           public string TT;             // Test Timer
           public double LV;             // Leak Value
           public double NRDP;           // Notthing Revision Differential Pessure
           public double CQ;             // Compensating Quantity
           public double DP;             // Delta Pressure
           public double TP;             // Test Pressure
           public string TN;             // Test Name
           public int RC;                // Run CH (CH#)
           public COSMO_D_Format(string testtimer, double leakvalue, double dp, double cq, double dp2, double tp, string tn, int rc)
           {
               this.TT   = testtimer;
               this.LV   = leakvalue;
               this.NRDP = dp;
               this.CQ   = cq;
               this.DP   = dp2;
               this.TP   = tp;
               this.TN   = tn;
               this.RC   = rc;
           }
       }
       const int MAX_SERIAL_DATA_COUNTS   = 5000;                                           // 최대 각 포트당 데이터 수신 갯수 (코스모 장치 10Hz이므로 최대 500초의 데이터를 저장)
       public COSMO_D_Format[] Cosmo1Data = new COSMO_D_Format[MAX_SERIAL_DATA_COUNTS];     // 통신 데이터 수신후 저장소
       private int Cosmo1_DataIndex       = 0;
       public int Cosmo1DataIndex { get { return this.Cosmo1_DataIndex; } }
       public void Cosmo1_DataIndex_Clear()
       {
           this.Cosmo1_DataIndex = 0;
       }
       //
       public void CloseAll()
       {
           if (Cosmo1!=null)
           {
               Cosmo1.CloseComm();
           }
       }

       public bool Init(Logger refLog, iMEB.SysConfig _SysConfig)
       {
           string COMPPORT = "COM";
           int ComPortNum  = _SysConfig.COSMO_PortNumber;

           if (_SysConfig.COSMO_PortNumber < 1)
           {
               refLog.Info("기본설정 항목에서 통신포트가 0이하의 값으로 설정되어 COM1로 자동설정됩니다.");
               ComPortNum = 1;
           }

           COMPPORT = COMPPORT + string.Format("{0}", ComPortNum);
           // 데이터 수신 핸들 및 종결 핸들 설정
           try
           {
               Cosmo1 = new SerialComm();
               Cosmo1.DataReceivedHandler = DataReceive; // 통신중 데이터 수신용 핸들
               Cosmo1.DisconnectedHandler = Close;       // 통신종료 핸들
                   
           }
           catch (Exception e1)
           {
               refLog.Error("통신 SerialComm 생성중 에러가 발생하였습니다..");
               return false;
           }

           bool chk_default = _SysConfig.COSMO_DefaultSet;
           if (!chk_default)
           {
               refLog.Error("기본설정 항목에서 디폴트사용이 설정되지 않았습니다.");
               return false;
           }
           bool Connection = Cosmo1.OpenComm(COMPPORT, 115200, 8, StopBits.One, Parity.None, Handshake.None);
           if (Connection) return Connection;
           else
           {
               refLog.Error("PC 통신 포트가 사용중이거나 없습니다. 설정포트 ="+COMPPORT);
               return false;
           }
       }
       private void Close()
       {
           Cosmo1_DataIndex = 0;
       }
       private void DataReceive(byte[] receiveData)
       {
           string Msg = Encoding.Default.GetString(receiveData); // 문자열 변환
           COSMO_D_Format _Data = new COSMO_D_Format();
           lock (this) { Cosmo1Buffer += Msg; }                 // 수신데이터를 메모리 변수에 저장시 Lock 사용
                                                                 // Msg 내부 변수로 일차변환하므로 Lock 사용이 필요한지 재검토 필요함
                                                                 // 확인 후 본 메세지 정리 요망
           if (Cosmo1Buffer.IndexOf("\r") > 0)
           {
               //MainUIUpdate_ComState(false, true);
               while (true)
               {
                   String t = Cosmo1Buffer.Substring(0, Cosmo1Buffer.IndexOf("\r") + 1);
                   var parts = t.Split(',');
                   if (parts.Length == 8)
                   {
                       if (Cosmo1_DataIndex < MAX_SERIAL_DATA_COUNTS)
                       {  // D Format 일 경우
                           _Data = new COSMO_D_Format(parts[0], double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]),
                                                      double.Parse(parts[4]), double.Parse(parts[5]), parts[6], int.Parse(parts[7]));
                           Cosmo1Data[Cosmo1_DataIndex] =_Data;
                           Cosmo1_DataIndex++;
                           MainGraphUpdate_Cosmo(_Data);
                       }
                       else Cosmo1_DataIndex = 0;// Auto circulation clear buffer
                       lock (this)
                       {
                           Cosmo1Buffer = Cosmo1Buffer.Replace(t, "");  // 8개의 데이터 항목을 읽은 후 버퍼에서 해당자료를 삭제한다.
                       }
                   }
                   if (Cosmo1Buffer.IndexOf("\r") <= 0) break; // 버퍼중 더 변환할 데이터 유무를 조사
               }           
           }           
           //MainUIUpdate_ComState(false, false);
       }
   }
    // iMEB 와 직접적으로 연관은 없으며 실행하는 참고할 수 있는 클래스 목록
    /// <summary>
    /// 시리얼 통신 클래스 (RS232 Simple style)
    /// </summary>
    public class SerialComm
    {
        public delegate void DataReceivedHandlerFunc(byte[] receiveData);
        public DataReceivedHandlerFunc DataReceivedHandler;

        public delegate void DisconnectedHandlerFunc();
        public DisconnectedHandlerFunc DisconnectedHandler;

        private SerialPort serialPort;

        public bool IsOpen
        {
            get
            {
                if (serialPort != null) return serialPort.IsOpen;
                return false;
            }
        }

        // serial port check
        private Thread threadCheckSerialOpen;
        private bool isThreadCheckSerialOpen = false;

        public SerialComm()
        {
        }

        public bool OpenComm(string portName, int baudrate, int databits, StopBits stopbits, Parity parity, Handshake handshake)
        {
            try
            {
                serialPort = new SerialPort();

                serialPort.PortName = portName;
                serialPort.BaudRate = baudrate;
                serialPort.DataBits = databits;
                serialPort.StopBits = stopbits;
                serialPort.Parity = parity;
                serialPort.Handshake = handshake;

                serialPort.Encoding = new System.Text.ASCIIEncoding();
                //serialPort.NewLine = "\r";
                serialPort.NewLine = "\r\n";
                serialPort.ErrorReceived += serialPort_ErrorReceived;
                serialPort.DataReceived += serialPort_DataReceived;

                serialPort.Open();

                StartCheckSerialOpenThread();
                return true;
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
                return false;
            }
        }

        public void CloseComm()
        {
            try
            {
                if (serialPort != null)
                {
                    StopCheckSerialOpenThread();
                    serialPort.Close();
                    serialPort = null;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
            }
        }

        public bool Send(string sendData)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Write(sendData);
                    return true;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
            }
            return false;
        }

        public bool Send(byte[] sendData)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Write(sendData, 0, sendData.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
            }
            return false;
        }

        public bool Send(byte[] sendData, int offset, int count)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Write(sendData, offset, count);
                    return true;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
            }
            return false;
        }

        private byte[] ReadSerialByteData()
        {
            serialPort.ReadTimeout = 100;
            byte[] bytesBuffer = new byte[serialPort.BytesToRead];
            int bufferOffset = 0;
            int bytesToRead = serialPort.BytesToRead;

            while (bytesToRead > 0)
            {
                try
                {
                    int readBytes = serialPort.Read(bytesBuffer, bufferOffset, bytesToRead - bufferOffset);
                    bytesToRead -= readBytes;
                    bufferOffset += readBytes;
                }
                catch (TimeoutException ex)
                {
                    // Debug.WriteLine(ex.ToString());
                }
            }

            return bytesBuffer;
        }

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                byte[] bytesBuffer = ReadSerialByteData();
                string strBuffer = Encoding.ASCII.GetString(bytesBuffer);

                if (DataReceivedHandler != null)
                    DataReceivedHandler(bytesBuffer);

                //Debug.WriteLine("received(" + strBuffer.Length + ") : " + strBuffer);
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(ex.ToString());
            }
        }

        private void serialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            //Debug.WriteLine(e.ToString());
        }

        private void StartCheckSerialOpenThread()
        {
            StopCheckSerialOpenThread();

            isThreadCheckSerialOpen = true;
            threadCheckSerialOpen = new Thread(new ThreadStart(ThreadCheckSerialOpen));
            threadCheckSerialOpen.Start();
        }

        private void StopCheckSerialOpenThread()
        {
            if (threadCheckSerialOpen != null)
            {
                isThreadCheckSerialOpen = false;
                if (Thread.CurrentThread != threadCheckSerialOpen)
                    threadCheckSerialOpen.Join();
                threadCheckSerialOpen = null;
            }
        }

        private void ThreadCheckSerialOpen()
        {
            while (isThreadCheckSerialOpen)
            {
                Thread.Sleep(100);

                try
                {
                    if (serialPort == null || !serialPort.IsOpen)
                    {
                        //Debug.WriteLine("seriaport disconnected");
                        if (DisconnectedHandler != null)
                            DisconnectedHandler();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    //Debug.WriteLine(ex.ToString());
                }
            }
        }
    }
    /// <summary>
    /// PLC 연결 및 제어 관련 클래스
    /// </summary>
    public class M_PLC : IDisposable
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
                        if (PLC!=null)
                        {
                            PLC.Close();
                        }
                    }
                    disposed = true;
                }
            }
            ~M_PLC()
            {
                Dispose(false);
            }
        #endregion
        private ActUtlTypeLib.ActUtlTypeClass PLC;
        public class PLCSTATE
        {
            public bool Auto;
            public bool Manual;
            public bool Reset;
            public bool Loading;
            public bool LeakMode;
            public bool Error;
            public PLCSTATE()
            {
                this.Auto     = false;
                this.Manual   = false;
                this.Reset    = false;
                this.Loading  = false;
                this.LeakMode = false;
                this.Error    = false;
            }
        }
        public enum SOL : int
        {
            // D10011(PC->PLC) SOL VALVE Control
            CLEAR                     = 0x00000000,

            WHEEL_PORT_MAIN_VAC_CLOSE = 0x00000001,
            WHEEL_PORT_MAIN_VAC_OPEN  = 0x00000002,

            WHEEL_PORT_12_VAC_CLOSE   = 0x00000004,
            WHEEL_PORT_12_VAC_OPEN    = 0x00000008,

            WHEEL_PORT_34_VAC_CLOSE   = 0x00000010,
            WHEEL_PORT_34_VAC_OPEN    = 0x00000020,
            ECU_POWER_OFF             = 0x00000080,
            ECU_POWER_ON              = 0x00000080

        }
        // PLC CLAMP 명령 cmd 만들기
        public enum CLAMP : int
        {
            // D10012(PC->PLC) CLAMP Control
            CLEAR             = 0x00000000,
            ECU_CONNECTOR_FWD = 0x00000002,
            ECU_CONNECTOR_BWD = 0x00000001,
            WORK_UNCLAMP      = 0x00000004,
            WORK_CLAMP        = 0x00000008,
            SUCTION_BWD       = 0x00000010,
            SUCTION_FWD       = 0x00000020,
            IGNITION_OFF      = 0x00000040,
            IGNITION_ON       = 0x00000080,
            WHEEL_PORT_12_BWD = 0x00000100,
            WHEEL_PORT_12_FWD = 0x00000200,
            WHEEL_PORT_34_BWD = 0x00000400,
            WHEEL_PORT_34_FWD = 0x00000800,
            RESERVE_UP        = 0x00001000,
            RESERVE_DOWN      = 0x00002000,
            RESERVE_AIR_CLOSE = 0x00004000,
            RESERVE_AIR_OPEN  = 0x00008000
        }

        public PLCSTATE PLCState = new PLCSTATE();
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
        public bool Init()
        {
            string errMsg = "";
            try
            {
                if (PLC!=null)
                {
                    PLC.Close();
                    PLC = null;
                }
                PLC = new ActUtlTypeLib.ActUtlTypeClass();
                PLC.ActLogicalStationNumber = 0;
                PLC.ActPassword             = "";
                if (PLC.Open() != 0) return false;
            }
            catch (Exception e1)
            {
                errMsg = e1.Message;
                return false;
            }
            return true;
        }
        /// <summary>
        /// PLC측 메모리 클리어(자동운전전 수행)
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_ClearWorkArea(double timeout)
        {
            Stopwatch st  = new Stopwatch();
            bool ChkError = false;
            string DCode  = "D100";

            int ret = 0;
            ret = PLC.WriteDeviceBlock("D10011", 1, 0x00000000);  // SOL VALVVE PART
            Delay(20);
            ret = PLC.WriteDeviceBlock("D10012", 1, 0x00000000);  // CLAMP PART
            Delay(20);
            //ret = PLC.WriteDeviceBlock("D10013", 1, 0x00000000);  // SERVO STATUS
            //Delay(20);
            ret = PLC.WriteDeviceBlock("D10040", 1, 0x00000000);  // COSMO #1
            Delay(20);
            //ret = PLC.WriteDeviceBlock("D10041", 1, 0x00000000);
            //Delay(20);
            ret = PLC.WriteDeviceBlock("D10000", 1, 0x00000001);
            /*
            if (timeout <= 0.0) timeout = 0.0;
            st.Start();
            while (true)
            {                                            
                int Ret = PLC.WriteDeviceBlock(DCode, 40, 0);
                if (Ret >= 0)
                {
                    ChkError = true;
                    break;
                }
                if (st.ElapsedMilliseconds > (timeout * 1000)) break;
            }            
            if (ChkError) return true;
            else          return false;
            */
            return true;
        }
        /// <summary>
        /// PC->PLC 모드 읽기(자동/수동,리셋,제품로딩등등)
        /// </summary>
        /// <returns></returns>
        public bool CMD_CurrentState()
        {
            int PlcReadStatus;
            short[] PlcData = new short[2];

            PlcReadStatus = PLC.ReadDeviceBlock2("D10100", 2, out PlcData[0]); // D10100, D10101 읽기
            if (PlcReadStatus.Equals(0))
            {
                if ((PlcData[0] & 0x0001) == 1)
                {
                    PLCState.Auto   = true;
                    PLCState.Manual = false;
                }
                else
                {
                    PLCState.Auto   = false;
                    PLCState.Manual = true;
                }
                if ((PlcData[0] & 0x0002) == 1) PLCState.Error = true;
                else                           PLCState.Error = false;
                if ((PlcData[0] & 0x0004) == 1) PLCState.Reset = true;
                else                           PLCState.Reset = false;
                if ((PlcData[0] & 0x0100) == 1) PLCState.LeakMode = true;
                else                           PLCState.LeakMode = false;
                if ((PlcData[1] & 0x0001) == 1) PLCState.Loading = true;
                else                           PLCState.Loading = false;

                return true;
            }
            else return false;
        }
        /// <summary>
        /// PLC->PC Live Signal Read
        /// </summary>
        /// <param name="readData"></param>
        /// <returns></returns>
        public bool CMD_PLCLIVEState(ref short readData)
        {
            short PLCcmd = 0x0000;

            int Chk = PLC.ReadDeviceBlock2("D10106", 1, out PLCcmd);
            if (Chk.Equals(0))
            {
                readData = PLCcmd;
                return true;
            }
            else return false;
        }
        /// <summary>
        /// PC->PLC 자동운전 시작 신호 보냄
        /// </summary>
        /// <returns></returns>
        public bool CMD_PCRunSignlaSet(bool val)
        {
            int setValue = 0x00000000;
            if (val) setValue = 0x00000001;
            else     setValue = 0x00000000;
            int ret = PLC.WriteDeviceBlock("D10006", 1, setValue);
            if (ret.Equals(0)) return true;
            else return false;
        }
        /// <summary>
        /// PC-PLC 자동운전 종료시 상태값 클리어용
        /// </summary>
        /// <param name="testresult"></param>
        /// <returns></returns>
        public bool CMD_TestEndSet(int testresult,double timeout)
        {
            int chk;
            bool Loop_Chk   = false;
            bool[] Step_Chk = new bool[4];
            bool result     = false;
            Stopwatch s1    = new Stopwatch();
            s1.Start();
            while (!Loop_Chk)
            {
                // PC->PLC Clear Set
                chk = PLC.WriteDeviceBlock("D10011", 3, 0x00000000);
                Step_Chk[0] = chk.Equals(0);
                chk = PLC.WriteDeviceBlock("D10040", 2, 0x00000000);
                Step_Chk[1] = chk.Equals(0);
                // PC->PLC (OK/NG) Set
                chk = PLC.WriteDeviceBlock("D10002", 1, testresult);
                Step_Chk[2] = chk.Equals(0);
                // PC->PLC AutoMode Reset Signal Set
                chk = PLC.WriteDeviceBlock("D10000", 1, 0x00008001);
                Step_Chk[3] = chk.Equals(0);
                if ((Step_Chk[0])&&(Step_Chk[1])&&(Step_Chk[2])&&(Step_Chk[3]))
                {
                    Loop_Chk = true;
                    result   = true;
                    break;
                }
                if (s1.ElapsedMilliseconds>=(timeout*1000))
                {
                    Loop_Chk = true;
                    break;
                }
                Delay(10);
            }
            return result;
        }
        /// <summary>
        /// PC->PLC BUSY Signal Set
        /// </summary>
        /// <returns></returns>
        public bool CMD_PLC_BusySetting()
        {
            int ret = PLC.WriteDeviceBlock("D10000", 1, 0x00000003);
            if (ret.Equals(0)) return true;
            else              return false;
        }
        /// <summary>
        /// PLC->PC BUSY Signal Read         
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_BusySettingRead(double timeout)
        {
            short Data;
            bool chk = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                int ret = PLC.ReadDeviceBlock2("D10000", 1, out Data);
                if (ret.Equals(0))
                {
                    if ((Data & 0x00000002) > 0) chk = true;
                    else                         chk = false;
                    break;
                }
                if (s1.ElapsedMilliseconds > (timeout * 1000)) break;
                Delay(10);
            }
            return chk;
        }
        /// <summary>
        /// PC-PLC CLAMP Control
        /// </summary>
        /// <param name="Cmd"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_ClampSetting(int Cmd, double timeout)
        {
            int PC_Chk = -1;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10012", 1, Cmd);
                if (PC_Chk.Equals(0))
                {
                    Send_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }
        public bool CMD_PLCCLAMPState(ref short readData)
        {
            short PLCcmd = 0x0000;

            int Chk = PLC.ReadDeviceBlock2("D10112", 1, out PLCcmd);
            if (Chk.Equals(0))
            {
                readData = PLCcmd;
                return true;
            }
            else return false;
        }
        /// <summary>
        /// PC->PLC SOL Control
        /// </summary>
        /// <param name="Cmd"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_SolSetting(int Cmd, double timeout)
        {
            int PC_Chk   = -1;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10011", 1, Cmd);
                if (PC_Chk.Equals(0))
                {
                    Send_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else         return false;
        }


        public bool CMD_PLCSOLState(ref short readData)
        {            
            short PLCcmd = 0x0000;
            
            int Chk = PLC.ReadDeviceBlock2("D10111", 1, out PLCcmd);
            if (Chk.Equals(0))
            {
                readData = PLCcmd;
                return true;
            }
            else return false;
        }
        /// <summary>
        /// PC->PLC COSMO CH Set
        /// </summary>
        /// <param name="PriCh"></param>
        /// <param name="FloCh"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_CosmoChSetting(int PriCh, double timeout)
        {
            int PLC_Chk = -1;
            int PC_Chk = -1;
            int PCcmd = ((int)PriCh) & 0x000000FF; 
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10042", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {   
                    PLC_Chk = PLC.ReadDeviceBlock2("D10142", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }
        /// <summary>
        /// PC->PLC COSMO Run Set
        /// 코스모 장치에 START 신호 인가후 BUSY 및 STAGE0 신호를 timeout이내에 확인하여 결과를 반환
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_CosmoRunSetting(double timeout)
        {
            // 코스모 장치에 START 신호를 주고 timeout이내에 BUSY & STAGE0 신호를 확인하면 정상반환
            int PLC_Chk  = -1;
            int PC_Chk   = -1;
            int PCcmd    = 0x00000001;  // COSMO PRI START=10040.0
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            short check = 0;
            short comp = 0x0081;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10040", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10140", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        check =(short)(PLCcmd & comp); // BUSY & STAGE0
                        if (check == 0x81)
                        {
                            PC_Chk = PLC.WriteDeviceBlock("D10040", 1, 0x0000);
                            Delay(10);
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    short temp = check;
                    break;
                }
                Delay(10);
            }
            return Send_OK;
        }
        /// <summary>
        /// PC->PLC COSMO STOP Set
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_CosmoStopSetting(double timeout)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk  = -1;
            int PC_Chk   = -1;
            int PCcmd    = 0x00000002;  // COSMO PRI START=10040.0
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10040", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10040", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }

        /// <summary>
        ///  코스모 에어 리크 테스트 동작후 에러 내용을 확인함.
        ///  무조건 설정 시간동안 계속 통신하여 마지막 정상 수신 데이터롤 결과를 반영함(2017-09-19)
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="PriSide"></param>
        /// <param name="ErrName"></param>
        /// <returns></returns>
        public bool PLC_CosmoErrorCheck(double timeout, ref bool PriSide, ref string ErrName)
        {
            bool   Readchk          = false;
            int    ret1             = -1;
            short  Data1            = 0x0000;
            bool   Cosmo_Error      = false;
            string Cosmo_Error_Name = "";

            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                Readchk = false;  
                ret1    = PLC.ReadDeviceBlock2("D10140", 1, out Data1);  // PRI Side
                Delay(10);
                if ( (ret1.Equals(0)) )
                {
                    Cosmo_Error_Name = "";
                    // 10140.2 ERROR
                    if ((Data1 & 0x00000004) > 0)
                    {
                        ErrName     = "COSMO ERROR";
                        Cosmo_Error = true;
                    }
                    // 10140.3 OK, 정상동작후에는 바로 종료
                    if ((Data1 & 0x00000008) > 0)
                    {
                        ErrName     = "";
                        Readchk     = true;   
                        Cosmo_Error = false;
                        break;
                    }
                    // 10140.4 UL NG
                    if ((Data1 & 0x00000010) > 0)
                    {
                        ErrName = ErrName + "/UL NG";
                        Cosmo_Error = true;
                    }
                    // 10140.A LL2 NG
                    if ((Data1 & 0x00000400) > 0)
                    {
                        ErrName = ErrName + "/LL2 NG";
                        Cosmo_Error = true;
                    }
                    // 10140.B LL NG
                    if ((Data1 & 0x00000800) > 0)
                    {
                        ErrName = ErrName + "/LL NG";
                        Cosmo_Error = true;
                    }
                    // 10140.C UL NG
                    if ((Data1 & 0x00001000) > 0)
                    {
                        ErrName = ErrName + "/UL2 NG";
                        Cosmo_Error = true;
                    }
                    Readchk = true;                   
                }
                if (s1.ElapsedMilliseconds > timeout * 1000.0) { break; }
            }

            if (Readchk)
            {
                PriSide = Cosmo_Error;
                return true;
            }
            else return false;
        }


        /// <summary>
        /// PLC->PC COSMO STATUS READ
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_CosmoStopChecking(double timeout, ref short ReadData)
        {
            bool Read_OK = false;
            int ret1;
            short Data1;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                ret1 = PLC.ReadDeviceBlock2("D10140", 1, out Data1);
                if ((ret1.Equals(0)))
                {
                    Read_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >(timeout*1000)) break;
                Delay(10);
            }
            if (Read_OK)
            {
                ReadData = Data1;
                return true; ;
            }
            else return false;
        }
        /// <summary>
        /// PC->PLC COSMO CHARGE MODE RUN Set
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool CMD_CosmoChargeRun(double timeout)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk  = -1;
            int PC_Chk   = -1;
            int PCcmd    = 0x00000004;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();
            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10040", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10040", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }




        // 2017-1024 MES/LDS관련 추가
        /// <summary>
        /// PLC -> PC LDS와 연동 신호 확인(10107)
        /// 상기 신호가 True일 경우 MES/LDS와 연동하여 해당 작업 진행 유무 결정 프로세스를 진행하기 위한 정보를 읽음
        /// 메인 타이머에서 구동되므로 실시간 처리로 타임아웃 없음
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool CMD_Read_LDSSignal(ref bool result)
        {
            int       PLC_Chk = -1;
            short     PLCcmd  = 0x0000;
            bool      Read_OK = false;
            Stopwatch s1      = new Stopwatch();
        
                PLC_Chk = PLC.ReadDeviceBlock2("D10107", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    if (PLCcmd == 0x0001) result = true;
                    if (PLCcmd == 0x0000) result = false;
                    Read_OK = true;
                }

            if (Read_OK) return true;
            else return false;
        }


        public bool CMD_Read_SystemStatus(double timeout, ref string status)
        {
            int PLC_Chk  = -1;
            short PLCcmd = 0;
            bool Read_OK = false;
            Stopwatch s1 = new Stopwatch();
            byte bLow    = 0x00;

            s1.Start();

            while (true)
            {
                PLC_Chk = PLC.ReadDeviceBlock2("D10165", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    bLow = (byte)(PLCcmd & 0x00FF);
                    status = (PLCcmd < 10) ? PLCcmd.ToString() : "0";
                    Read_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Read_OK)
            {
                return true;
            }
            else return false;
        }
        /// <summary>
        /// PLC -> PC 바코드 정보 읽기
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="barcode"></param>
        /// <returns></returns>
        public bool CMD_Read_BarCode(double timeout, ref string barcode)
        {
            int       PLC_Chk     = -1;
            short[]   PLCcmd      = new short[10];
            bool      Read_OK     = false;
            Stopwatch s1          = new Stopwatch();
            string    readBarCode = "";
            byte      bHigh       = 0x00;
            byte      bLow        = 0x00;

            s1.Start();

            while (true)
            {
                PLC_Chk = PLC.ReadDeviceBlock2("D10150", 10, out PLCcmd[0]);
                Delay(10);
                PLC_Chk = PLC.ReadDeviceBlock2("D10150", 10, out PLCcmd[0]);
                if (PLC_Chk.Equals(0))
                {
                    readBarCode = "";
                    for (int i=0; i<6; i++)
                    {
                        bHigh = (byte)((PLCcmd[i] & 0xFF00) >> 8);
                        bLow  = (byte)(PLCcmd[i] & 0x00FF);
                        if (i != 5) readBarCode = readBarCode + System.Text.Encoding.ASCII.GetString(new[] { bLow }) + System.Text.Encoding.ASCII.GetString(new[] { bHigh });
                        if (i == 5) readBarCode = readBarCode + System.Text.Encoding.ASCII.GetString(new[] { bLow });
                    }

                    Read_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Read_OK)
            {
                barcode = readBarCode;
                return true;
            }
            else return false;        
        }

        public bool CMD_Read_ModelLDSCheck(double timeout, ref string modelCode)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk = -1;
            int PCcmd = 0x00000004;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Read_OK = false;
            Stopwatch s1 = new Stopwatch();
            string readModel = "";
            s1.Start();
            while (true)
            {
                PLC_Chk = PLC.ReadDeviceBlock2("D10160", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    byte DataHigh = (byte)((PLCcmd & 0xFF00) >> 8);
                    byte DataLow = (byte)(PLCcmd & 0x00FF);
                    readModel = System.Text.Encoding.ASCII.GetString(new[] { DataHigh }) + System.Text.Encoding.ASCII.GetString(new[] { DataLow });
                    Read_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Read_OK) return true;
            else return false;

        }

        public bool CMD_Read_ModelCode(double timeout, ref string modelCode)
        {


            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk  = -1;
            int PCcmd    = 0x00000004;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Read_OK = false;
            Stopwatch s1 = new Stopwatch();
            string readModel = "";
            modelCode = readModel;
            s1.Start();
            while (true)
            {
                PLC_Chk = PLC.ReadDeviceBlock2("D10160", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    byte DataHigh = (byte)((PLCcmd & 0xFF00) >> 8);
                    byte DataLow  = (byte)(PLCcmd & 0x00FF);
                    readModel     = System.Text.Encoding.ASCII.GetString(new[] { DataLow }) + System.Text.Encoding.ASCII.GetString(new[] { DataHigh });
                    modelCode     = readModel;
                    Read_OK       = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Read_OK) return true;
            else         return false;

        }

        public bool CMD_Read_ModelChange(double timeout, ref int result)
        {            
            int PLC_Chk = -1;
            int PCcmd = 0x00000004;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Read_OK = false;
            Stopwatch s1 = new Stopwatch();
            string readModel = "";

            result = -1;

            s1.Start();
            while (true)
            {
                PLC_Chk = PLC.ReadDeviceBlock2("D10161", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    if (PLCcmd == 0x0001) result = 1;
                    if (PLCcmd == 0x0000) result = 0;
                    Read_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Read_OK) return true;
            else return false;
        }
        public bool CMD_Read_TyepRequest(double timeout, ref int result)
        {
            int PLC_Chk = -1;
            int PCcmd = 0x00000000;  
            short PLCcmd = 0x0000;
            bool Read_OK = false;
            Stopwatch s1 = new Stopwatch();
            string readModel = "";

            result = -1;

            s1.Start();
            while (true)
            {
                PLC_Chk = PLC.ReadDeviceBlock2("D10163", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    if (PLCcmd == 0x0001) result = 1;
                    if (PLCcmd == 0x0000) result = 0;
                    Read_OK = true;
                    break;
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Read_OK) return true;
            else return false;
        }


        public bool CMD_Write_ModelLDSBusy(double timeout, int setValue)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk = -1;
            int PC_Chk = -1;
            int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();

            if (setValue == 0) PCcmd = 0x00000000;
            if (setValue == 1) PCcmd = 0x00000001;

            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10007", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10007", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }
        public bool CMD_Write_ModelLDSResult(double timeout, int setValue)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk = -1;
            int PC_Chk = -1;
            int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();

            if (setValue == 0) PCcmd = 0x00000000;  // 초기화 ++
            if (setValue == 1) PCcmd = 0x00000001;  // OK
            if (setValue == 2) PCcmd = 0x00000002;  // NG

            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10008", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10008", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }
        public bool CMD_Write_ModelBusy(double timeout,int setValue)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk = -1;
            int PC_Chk = -1;
            int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();

            if (setValue == 0) PCcmd = 0x00000000;
            if (setValue == 1) PCcmd = 0x00000001;

            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10060", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10060", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }
        public bool CMD_Write_ModelResult(double timeout, int setValue)
        {
            // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
            int PLC_Chk = -1;
            int PC_Chk = -1;
            int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
            short PLCcmd = 0x0000;
            bool Send_OK = false;
            Stopwatch s1 = new Stopwatch();
            s1.Start();

            if (setValue == 0) PCcmd = 0x00000000;
            if (setValue == 1) PCcmd = 0x00000001;

            while (true)
            {
                PC_Chk = PLC.WriteDeviceBlock("D10061", 1, (int)PCcmd);
                if (PC_Chk.Equals(0))
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10060", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PCcmd == PLCcmd)
                        {
                            Send_OK = true;
                            break;
                        }
                    }
                }
                if (s1.ElapsedMilliseconds >= (timeout * 1000))
                {
                    break;
                }
                Delay(10);
            }
            if (Send_OK) return true;
            else return false;
        }

        public bool PLC_TestEndNGCodeSet(int NGCode)
        {

            Delay(20);
            PLC.WriteDeviceBlock("D10009", 1, NGCode);
            Delay(20);
            return true;
        }

    }
    /// <summary>
    /// NI PCI-6259 Class, 진공압력센서(MC12,MC34), PSU 전압/전류 측정용
    /// </summary>
    public class M_DAQ : IDisposable
    {
        #region Disposable 관련
        // DISPOSABLE 관련
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

                    // Do work     
                    if (HSrunningTask!=null)  HSrunningTask.Dispose();
                    if (inputTask!=null)      inputTask.Dispose();
                    if (outputTask!=null)     outputTask.Dispose();
                }
                disposed = true;
            }
        }
        ~M_DAQ()
        {
            Dispose(false);
        }
        #endregion
        
        #region 변수
        private NationalInstruments.DAQmx.Task inputTask;
        private AIChannel                      AI0,AI1,VAC1, VAC2; // AI0,AI1은 임시 확인안됨.
        private AnalogMultiChannelReader       analogReader;
        private AsyncCallback                  inputCallback;
        public NationalInstruments.DAQmx.Task HSrunningTask;

        private NationalInstruments.DAQmx.Task outputTask;
        private AOChannel                      PSU_V;
        private AnalogSingleChannelWriter      PSU_writer;

        private Logger                         reflog = null;

        public RealTimeGraphVIew RTGraph = new RealTimeGraphVIew();

        private double _PSU_Volt = 0.0;
        public double PSU_Volt { get { return this._PSU_Volt; }  }
        private bool _IsDAQHighSpeedRun = false;
        public bool IsDAQHighSpeedRun { get { return this._IsDAQHighSpeedRun; }  }
        #endregion

        #region 메서드 및 펑션
        public bool Init(Logger refLog,double aiSampleRate,iMEB.SysConfig _SysConfig)
        {
            List<string> errMsg = new List<string>();
            bool Chk = true;
            // Gain & Offset Set
            RTGraph.FactorOffset_Apply(_SysConfig);
            // Task
            try
            {
                if (inputTask != null)
                {
                    inputTask.Stop();
                    inputTask.Dispose();
                    inputTask = null;                    
                }
                inputTask = new NationalInstruments.DAQmx.Task("iMEB Leak Test");
                bool iChk = DAQ_ChannelSet(inputTask);
                DAQ_SampleRate(inputTask,aiSampleRate);
                inputTask.Control(TaskAction.Verify);
                HSStartTask();
                inputTask.Start();

                inputCallback = new AsyncCallback(HSRead);
                analogReader  = new AnalogMultiChannelReader(inputTask.Stream);
                analogReader.SynchronizeCallbacks = true;
                analogReader.BeginReadMultiSample(3000, inputCallback, inputTask);
            }
            catch (Exception e1)
            {
                errMsg.Add("(AnalogInput Task Create)" + e1.Message);
                _IsDAQHighSpeedRun = false;
                Chk = false;
            }
            // Analog Output Task Setting
            if (outputTask != null)
            {
                outputTask.Stop();
                outputTask.Dispose();
                outputTask = null;
            }
            outputTask = new NationalInstruments.DAQmx.Task("iMEB NI Output Task");
            if (DAQ_AnalogOutputSet(outputTask) == false)
            {
                errMsg.Add("Analog Output Task Create Fails");
                Chk = false;
            }
            if (!Chk)
            {
                // Log Write
                errMsg.ForEach(delegate (string emsg)
                {
                    refLog.Info("NI PCI-6221 : " + emsg);
                });
            }
            _IsDAQHighSpeedRun = true;
            return Chk;
        }
        private bool DAQ_AnalogOutputSet(NationalInstruments.DAQmx.Task task)
        {
            try
            {
                if (PSU_V == null)
                {
                    PSU_V = task.AOChannels.CreateVoltageChannel(
                        "dev1/ao0",
                        "PSU_V",
                        0.0,
                        10.0,
                        AOVoltageUnits.Volts
                        );
                    PSU_writer = new AnalogSingleChannelWriter(task.Stream);
                }
            }
            catch (Exception e1)
            {
                return false;
            }
            return true;
        }
        public bool PSU_SETVOLT(double volt)
        {
            double setVolt = (volt > 0.0) ? volt/7.0 : 0.0;
            try
            {
                if (PSU_writer != null) PSU_writer.WriteSingleSample(true, setVolt);
                else return false;
            }
            catch (Exception e1) { }
            /*
            try
            {
                if (PSU_writer != null) PSU_writer.WriteSingleSample(true, setVolt);
                else return false;
            }
            catch (Exception e1)
            {
                bool chk = DAQ_AnalogOutputSet(outputTask);
                if (chk) PSU_writer.WriteSingleSample(true, setVolt);
                else return false;
            }
            */
            return true;
        }
        private bool DAQ_ChannelSet(NationalInstruments.DAQmx.Task task)
        {
            try
            {
                if (this.AI0 == null)
                {
                    AI0 = task.AIChannels.CreateVoltageChannel(
                        "dev1/ai0",
                        "AI0",
                        AITerminalConfiguration.Differential,
                        0.0,
                        10.0,
                        AIVoltageUnits.Volts
                        );
                }
                if (this.AI1 == null)
                {
                    AI1 = task.AIChannels.CreateVoltageChannel(
                        "dev1/ai1",
                        "AI1",
                        AITerminalConfiguration.Differential,
                        0.0,
                        10.0,
                        AIVoltageUnits.Volts
                        );
                }
                if (this.VAC1==null)
                {
                    VAC1 = task.AIChannels.CreateVoltageChannel(
                        "dev1/ai2",
                        "VAC1",
                        AITerminalConfiguration.Differential,
                        0.0,
                        10.0,
                        AIVoltageUnits.Volts
                        );
                }
                if (this.VAC2 == null)
                {
                    VAC2 = task.AIChannels.CreateVoltageChannel(
                        "dev1/ai3",
                        "VAC2",
                        AITerminalConfiguration.Differential,
                        0.0,
                        10.0,
                        AIVoltageUnits.Volts
                        );
                }
            }
            catch (Exception e1)
            {
                string errmsg = e1.Message;
                return false;
            }
            return true;
        }
        private void DAQ_SampleRate(NationalInstruments.DAQmx.Task task,double samplerate)
        {
            // 10Khz, 10msec ticks data read....
            task.Timing.ConfigureSampleClock("",
                                             samplerate,
                                             SampleClockActiveEdge.Rising,
                                             SampleQuantityMode.ContinuousSamples,
                                             20000);
        }
        private void HSRead(IAsyncResult ar)       // 고속모드 데이터 리더(로우데이터 포멧)
        {
            try
            {
                if (HSrunningTask != null && HSrunningTask == ar.AsyncState)
                {                    
                    double[,] data = analogReader.EndReadMultiSample(ar);
                    //lock(this)
                    //{                    
                   //bool chk =  
                    RTGraph.DAQStorage(data);
                    //}
                    analogReader.BeginReadMultiSample(50, inputCallback, inputTask); // 
                    _IsDAQHighSpeedRun = true;
                }
            }
            catch (Exception ex)
            {
                _IsDAQHighSpeedRun = false;
                HSStopTask(ex.Message);
            }
        }
        private void HSStartTask()                 // 고속모드용 태스크 시작
        {
            if (HSrunningTask == null)
            {
                HSrunningTask = inputTask;
            }
        }
        private void HSStopTask(string errmsg)     // 고속모드용 태스크 정지
        {
            HSrunningTask = null;
            if (inputTask != null)
            {
                try
                {
                    inputTask.Stop();
                    inputTask.Dispose();
                    reflog.Error("HSStopTask Error = " + errmsg);
                }
                catch (Exception e1)
                {
                    if (reflog != null)
                    {
                        reflog.Error("HSStopTask Error = " + e1.Message);
                    }
                }
            }

        }
        #endregion

        #region 고속데이터 화면 및 저장 관련
        public class RealTimeGraphVIew
        {
            public event UI_Refresh _RealTimeGrpah;

            const int               DAQ_BUFFERS    = 160000;  // 최대 임시 저장 버퍼 크기(1Khz=160초)
            const int               MBC_BUFFERS    = 32000;   // 최대 임시 저장 버퍼 크기(5msec -> 200Hz, 160초 분량의 데이터)
            const int               MAX_USED_AI_CH = 4;      // 현재 시험에 사용중인 아날로그 입력 채널 수

            public double[,] RealData   = new double[MAX_USED_AI_CH,DAQ_BUFFERS];

            private int _DataIndex_MBC = 0;
            public double[,] RealMBCData = new double[3, MBC_BUFFERS];   // ECU 통신으로 읽은 MBC 정보 저장용 0 = 시간(double,SEC), 1=ECU Position, 2=ECU Current
            public int CurDataIndexMBC { get { return this._DataIndex_MBC; } }
            public void MBCDataClear()
            {
                this._DataIndex_MBC = 0;                
            }
            public void MBCDataADD(double time,double Position, double Current)
            {
                int DataIndex = this._DataIndex_MBC;

                this.RealMBCData[0, DataIndex] = time;
                this.RealMBCData[1, DataIndex] = Current;
                this.RealMBCData[2, DataIndex] = Position;
                DataIndex++;
                if (DataIndex >= DAQ_BUFFERS) DataIndex = 0;
                this._DataIndex_MBC = DataIndex;
            }
            private double[] _Factor = new double[MAX_USED_AI_CH];
            private double[] _Offset = new double[MAX_USED_AI_CH];
            private double[] _CurCh  = new double[MAX_USED_AI_CH];
            public double[] CurCH { get { return this._CurCh; } }

            public void FactorOffset_Apply(iMEB.SysConfig _SysConfig)
            {
                // 설정화일의 게인/옵셉을 적용
                this._Factor[0] = _SysConfig.AI_CH0_Factor;
                this._Factor[1] = _SysConfig.AI_CH1_Factor;
                this._Factor[2] = _SysConfig.AI_CH2_Factor;
                this._Factor[3] = _SysConfig.AI_CH3_Factor;

                this._Offset[0] = _SysConfig.AI_CH0_Offset;
                this._Offset[1] = _SysConfig.AI_CH1_Offset;
                this._Offset[2] = _SysConfig.AI_CH2_Offset;
                this._Offset[3] = _SysConfig.AI_CH3_Offset;

            }

            private int _EveryDCTime = 10; // 매 데이터 10개마다 한번씩 SW Trigger 실행
            public int EveryDCTime { get { return this._EveryDCTime; } set { this._EveryDCTime = value; } }

 
            private int _DataIndex = 0; // 내부적으로 측정된 데이터가 저장되면 저장된 데이터 갯수를 가짐
            private bool _IsDataMemorySave = false;
            public int CurDataIndex { get { return this._DataIndex; } }
            public void StartDAQSave()
            {
                this._DataIndex        = 0;
                this._IsDataMemorySave = true;
            }
            public void StopDAQSave()
            {
                this._IsDataMemorySave = false;
            }
            public bool DAQStorage(double[,] _Datas)
            {
                int DataLength    = _Datas.GetLength(1);
                int DataItemCount = _Datas.GetLength(0);
                int everydctime   = this._EveryDCTime;
                int edct_count    = 0;

                double f0 = this._Factor[0];
                double f1 = this._Factor[1];
                double f2 = this._Factor[2];
                double f3 = this._Factor[3];

                double o0 = this._Offset[0];
                double o1 = this._Offset[1];
                double o2 = this._Offset[2];
                double o3 = this._Offset[3];

                int startIndex = _DataIndex;
                    for (int i = 0; i < DataLength; i++)
                    {
                        // 실시간 데이터 변환(화면및 내부 확인용)
                        edct_count++;
                        if (edct_count > everydctime)
                        {

                            for (int dic = 0; dic < DataItemCount; dic++)
                            {
                                this._CurCh[dic] = _Datas[dic, i] * this._Factor[dic] + this._Offset[dic];
                            }
                            edct_count = 0;
                        }

                        if (startIndex + i >= DAQ_BUFFERS)
                        {
                            _DataIndex = 0;
                            startIndex = 0;
                        }
                        this.RealData[0, startIndex + i] = _Datas[0, i] * f0 + o0;
                        this.RealData[1, startIndex + i] = _Datas[1, i] * f1 + o1;
                        this.RealData[2, startIndex + i] = _Datas[2, i] * f2 + o2;
                        this.RealData[3, startIndex + i] = _Datas[3, i] * f3 + o3;
                    }
                    _DataIndex = _DataIndex + DataLength;

                return true;
            }
            public void EventCallGraph()
            {
                // 메인 화면에 그래프 업데이트
                _RealTimeGrpah(_DataIndex);
                //RTDataStorage.DataWrite(RealDataCh0, RealDataCh1, RealDataCh2, RealDataCh3, RealDataCh4, RealDataCh5, RealDataCh6, RealDataCh7, DataCounts);
            }
        }
        #endregion

        
    }
    /// <summary>
    /// 모비스 iMEB ECU 연결용 쿨래스, UDS,KVASER CAN
    /// </summary>
    public class M_ECU : IDisposable
    {
        #region Disposable 관련
        // DISPOSABLE 관련
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
                    // Do work     
                    if (handle!=-1)        Canlib.canClose(handle);
                }
                disposed = true;
            }
        }
        ~M_ECU()
        {
            Dispose(false);
        }
        #endregion
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
        
        private int   handle     = -1;
        private int   channel    = -1;
        private int   readHandle = -1;
        private bool  onBus      = false;
        private int   flags      = 0;       // flags의 기본 상태값을 확인하고 적용하기
        private Int32 _Channel   = 1;       // 물리적 캔 포트는 1,2중 하나를 사용하므로 설정값은 1 혹은 2로 설정됨

        public Logger ECULog;
        #endregion

        public int  CAN_Channel { set { this._Channel = value; } get { return this._Channel; } }
        public bool CAN_OnBus   { set { this.onBus = value; }    get { return this.onBus; } }
        public int  CAN_Handle  { set { this.handle = value; }   get { return this.handle; } }

        private bool _isConnected  = false;
        private bool _isDiagOnMode = false;
        public bool isConnected  { get { return this._isConnected; } }
        public bool isDiagOnMode { get { return this._isDiagOnMode; } set { this._isDiagOnMode = value; } }
        public bool Init(Logger refLog, iMEB.SysConfig _SysConfig)
        {
            List<string> errMsg = new List<string>();

            int hnd           = -1;
            int Channel       = _SysConfig.CAN_PortNumber;
            int sjw           = 0;
            int nosampl       = 0;
            int syncmode      = 0;
            int seg1          = _SysConfig.CAN_Tseg1;
            int seg2          = _SysConfig.CAN_Tseg2;            
            this.ECULog       = refLog;
            this._isConnected = false;
            bool isOK         = false;
            try
            {
                Canlib.canInitializeLibrary();
                hnd = Canlib.canOpenChannel(Channel, Canlib.canOPEN_ACCEPT_LARGE_DLC);
                if (hnd >= 0)
                {
                    this.handle = hnd;
                    
                    Canlib.canAccept(handle, 0x7DF, Canlib.canFILTER_SET_CODE_STD);
                    Canlib.canAccept(handle, 0x7D0, Canlib.canFILTER_SET_MASK_STD);

                    Canlib.canStatus cs = Canlib.canSetBusParams(handle, Canlib.canBITRATE_500K, seg1, seg2, sjw, nosampl, syncmode);
                    if (cs != Canlib.canStatus.canOK)
                    {
                        errMsg.Add("[canSetBusParams] 초기화 실패, 채널 = " + string.Format("{0}", Channel) +"\n");
                        errMsg.Add("[canSetBusParams] 500K, sjw=0, nosampl=0, syncmode=0 \n");
                        errMsg.Add("[canSetBusParams] " + string.Format("Tseg1={0}, Tseg2={1}",seg1,seg2) + "\n");
                    }

                    Canlib.canStatus cs2 = Canlib.canBusOn(this.handle);
                    isOK = true;
                    this._isConnected = true;
                }
            }
            catch (Exception e1)
            {
                refLog.Info("초기화 설정중 에러가 발생하였습니다.");
                return false;
            }            
            // Log Write
            errMsg.ForEach(delegate(string emsg)
            {
                refLog.Info("KVASER CAN CARD : " + emsg);
            });
            if (isOK) return true;
            else return false;
        }

        public bool CAN_BUS(bool tf)
        {
            Canlib.canStatus cs;

            if (tf)
            {
                cs = Canlib.canBusOn(handle);
                //CheckStatus("CAN BUS(" + tf.ToString() + ")", cs);
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
            //CheckStatus("Closing channel", status);
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


        public FuctionResult SendShortMsg(int id, byte[] data, int dlc)
        {
            if (data.Length != 8) return FuctionResult.ERROR_UNKNOWN;
            string msg = String.Format("{0}  {1}  {2:x2} {3:x2} {4:x2} {5:x2} {6:x2} {7:x2} {8:x2} {9:x2}   to handle {10}",
                                       id, dlc, data[0], data[1], data[2], data[3], data[4],
                                       data[5], data[6], data[7], handle);
            Canlib.canStatus status = Canlib.canWrite(handle, id, data, dlc, flags);
            //CheckStatus("Writing message " + msg, status);

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
            return;
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
        
        
        #region Error Response Code List ( ECU 응답 코드 관련 )

        public enum ECU_ERROR_CODE : byte
        {
            General_Reject                                  = 0x10,
            Service_Not_Support                             = 0x11,
            Sub_Function_Not_Supported                      = 0x12,
            Incorrect_Message_Length_Or_Invalid_Format      = 0x13,
            Responde_Too_Long                               = 0x14,
            Busy_Repeat_Request                             = 0x21,
            Conditions_Not_Correct                          = 0x22,
            Request_Sequence_Error                          = 0x24,
            Request_Out_Of_Range                            = 0x31,
            Security_Access_Denied                          = 0x33,
            Invalid_Key                                     = 0x35,
            Exceed_Number_Of_Attempts                       = 0x36,
            Required_Time_Delay_Not_Expired                 = 0x37,
            Upload_Down_Not_Accepted                        = 0x70,
            Transfer_Suspended                              = 0x71,
            General_Programming_Failure                     = 0x72,
            Wrong_Block_Sequence_Counter                    = 0x73,
            Request_Correctly_Received_Response_Pending     = 0x78,
            Sub_Function_Not_Supported_In_Active_Session    = 0x7E,
            Service_Not_Supported_In_Active_Session         = 0x7F,
            Service_Not_Supported_In_Active_Diagnostic_Mode = 0x80,
            RPM_Too_High                                    = 0x81,
            RPM_Too_Low                                     = 0x82,
            Engine_Is_Running                               = 0x83,
            Engine_Is_Not_Running                           = 0x84,
            Engine_Run_Time_Too_Low                         = 0x85,
            Temperature_Too_High                            = 0x86,
            Temperature_Too_Low                             = 0x87,
            Vehicle_Speed_Too_High                          = 0x88,
            Vehicle_Speed_Too_Low                           = 0x89,
            Throttle_Pedal_Too_High                         = 0x8A,
            Throttle_Pedal_Too_Low                          = 0x8B,
            Transmission_Range_Not_In_Neutral               = 0x8C,
            Transmission_Range_Not_In_Gear                  = 0x8D,
            Brake_Switch_Not_Closed                         = 0x8F,
            Shift_Level_Not_In_Park                         = 0x90,
            Torque_Converter_Clutch_Locked                  = 0x91,
            Voltage_Too_High                                = 0x92,
            Voltage_Too_Low                                 = 0x93
        }
        public string ErrorCodeToString(byte errCode)
        {
            string retMsg = "";
            retMsg = ((ECU_ERROR_CODE)errCode).ToString();
            if (retMsg.Length != 0)
            {
                string tmp = "";
                tmp = retMsg.Replace("_", " ");
                retMsg = "ECU Error : "+tmp;
            }
            else retMsg = "";
            return retMsg;
        }
        public string ErrorCodeToString(ECU_ERROR_CODE errCode)
        {
            string retMsg = "";
            retMsg = errCode.ToString();
            if (retMsg.Length != 0)
            {
                string tmp = "";
                tmp = retMsg.Replace("_", " ");
                retMsg = "ECU Error : " + tmp;
            }
            else retMsg = "";
            return retMsg;
        }
        #endregion


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
        /// <summary>
        /// CAN -> ECU
        /// Report Number of DTC by Status Mask
        /// mode = 0 = ALL DTC,  mode = 1 = Current DTC
        /// ECU로부터 DTC 발생 개수 읽기(상태에 따른 고장코드 개수 보고) 
        /// </summary>
        /// <param name="errmsg"></param>
        /// <returns></returns>
        public bool CMD_iMEB_DTC_ReportNumber(ref string errmsg,int mode)
        {
            //  0x19 = Read DTC Information Request Service ID
            //  0x01 = Report Number Of DTC By Status Mask
            //  0x08 = DTC Status Mask(0x08=ALL DTC, 0x01=Current DTC)
            byte[] Master_0_SF = new byte[8] { 0x03, 0x19, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00 };
            byte[] Master_1_SF = new byte[8] { 0x03, 0x19, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00 };
            byte[] SendData_SF = new byte[8] { 0x03, 0x19, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00 };
            if (mode == 0) SendData_SF = Master_0_SF;
            else SendData_SF = Master_1_SF;


            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.ReportNumber", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                // 확인 후 다시 코드 정리
                if ((readdata[0] == 0x0002) && (readdata[1] == 0x0050) && (readdata[2] == 0x0060)) return true;
            }
            else return false;

            return true;
        }

        public bool CMD_iMEB_DTC_CLEAR(ref string errmsg, int mode)
        {
            //  0x14 = ClearDiagnosticInformation Request Service ID

            byte[] Master_0_SF = new byte[8] { 0x04, 0xBB, 0x40, 0x00, 0xFF, 0x00, 0x00, 0x00 };  // Chassis group
            byte[] Master_1_SF = new byte[8] { 0x04, 0xBB, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00 };  // All groups

            byte[] SendData_SF = new byte[8] { 0x04, 0xBB, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            if (mode == 0)  SendData_SF = Master_0_SF;
            else            SendData_SF = Master_1_SF;


            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.CLEAR", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                // 확인 후 다시 코드 정리
                if ((readdata[0] == 0x0001) && (readdata[1] == 0x00FB))  return true;
            }
            else return false;

            return false;
        }

        public bool CMD_iMEB_DTC_Service(ref string errmsg, bool OnOff)
        {
            //  0x14 = ClearDiagnosticInformation Request Service ID

            byte[] Master_0_SF = new byte[8] { 0x02, 0x85, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };  // DTC Setting ON
            byte[] Master_1_SF = new byte[8] { 0x02, 0x85, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 };  // DTC Setting OFF

            byte[] SendData_SF = new byte[8] { 0x02, 0x85, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            if (OnOff) SendData_SF = Master_0_SF;
            else SendData_SF = Master_1_SF;


            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.Setting", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                // 확인 후 다시 코드 정리
                if ((readdata[0] == 0x0002) && (readdata[1] == 0x0050) && (readdata[2] == 0x0060)) return true;
            }
            else return false;

            return true;
        }

        public bool CMD_iMEB_DTC_ReadSWVersion(ref string errmsg, ref string resutlMsg)
        {
            //  0x22 = Read S/W Version

            byte[] SendData_SF = new byte[8] { 0x03, 0x22, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00 };




            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.Read SW Version", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                byte[] temp16 = new byte[16];
                temp16[0] = readdata[14];
                temp16[1] = readdata[15];

                temp16[2] = readdata[17];
                temp16[3] = readdata[18];
                temp16[4] = readdata[19];
                temp16[5] = readdata[20];
                temp16[6] = readdata[21];
                temp16[7] = readdata[22];
                temp16[8] = readdata[23];

                temp16[9] = readdata[25];
                temp16[10] = readdata[26];
                temp16[11] = readdata[27];
                temp16[12] = readdata[28];
                temp16[13] = readdata[29];
                temp16[14] = readdata[30];
                temp16[15] = readdata[31];
                string SWVersion = Encoding.ASCII.GetString(temp16);
                string ReleaseDate = string.Format("{0:X2}{1:X2}{2:X2}", readdata[33], readdata[34], readdata[35]);
                resutlMsg = SWVersion + ReleaseDate;
                //resutlMsg = ReleaseDate; // 앞부분의 소프트웨어 버전을 체크 안하므로 반환값에서 제외.2017.12.04
                // 확인 후 다시 코드 정리
                if ((readdata[0] == 0x0002) && (readdata[1] == 0x0050) && (readdata[2] == 0x0060)) return true;
            }
            else return false;

            return true;
        }





        public bool CMD_iMEB_ReadECUIdentification(ref string errmsg,ref string VName,ref string SName,ref string HSVersion,ref string RDate,ref string PNumber)
        {
            //  0x22   = ReadDataByLocalIdentifier Request Service ID
            //  0xF100 = ReadECUIdentification 

            byte[] SendData_SF = new byte[8] { 0x03, 0x22, 0xF1, 0x00, 0x00, 0x00, 0x00, 0x00 };



            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;

            string VehicleName = "";
            string SystemName  = "";
            string HWSWVersion = "";
            string ReleaseDate = "";
            string PartNumber  = "";

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.ReadECUIndentification", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                // 확인 후 다시 코드 정리
                if ((readdata[2] == 0x062) && (readdata[3] == 0x00F1) && (readdata[4] == 0x0000))
                {
                    VehicleName = Encoding.ASCII.GetString(readdata, 5, 2);                    

                    byte[] temp = new byte[3];

                    temp[0] = readdata[9];
                    temp[1] = readdata[10];
                    temp[2] = readdata[11];
                    SystemName = Encoding.ASCII.GetString(temp);

                    temp[0] = readdata[15];
                    temp[1] = readdata[17];
                    temp[2] = readdata[18];
                    HWSWVersion = Encoding.ASCII.GetString(temp);

                    temp[0] = readdata[19];
                    temp[1] = readdata[20];
                    temp[2] = readdata[21];
                    ReleaseDate =  string.Format("{0:X2}-{1:X2}-{2:X2}",temp[0],temp[1],temp[2]);

                    byte[] temp10 = new byte[11];
                    temp10[0] = readdata[23];
                    temp10[1] = readdata[25];
                    temp10[2] = readdata[26];
                    temp10[3] = readdata[27];
                    temp10[4] = readdata[28];
                    temp10[5] = readdata[29];
                    temp10[6] = readdata[30];
                    temp10[7] = readdata[31];
                    temp10[8] = readdata[33];
                    temp10[9] = readdata[34];
                    temp10[10] = readdata[35];
                    PartNumber = Encoding.ASCII.GetString(temp10);

                    VName     = VehicleName;
                    SName     = SystemName;
                    HSVersion = HWSWVersion;
                    RDate     = ReleaseDate;
                    PNumber   = PartNumber;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else return false;            
        }



        public class DTC_RESULT
        {
            const int MAX_DTC_CODES = 200;
            public Int32[] Codes = new Int32[MAX_DTC_CODES];
            public byte[] Status = new byte[MAX_DTC_CODES];
            public int Counts = 0;
            public DTC_RESULT()
            {
                for(int i=0; i<MAX_DTC_CODES; i++)
                {
                    this.Codes[i]  = 0x00000000;
                    this.Status[i] = 0x00;
                    this.Counts    = 0;
                }
            }
        }
        /// <summary>
        /// CAN -> ECU
        /// Report DTC by Status Mask
        /// 상태에 따른 고장코드 보고
        /// mode = 0 = ALL DTC,  mode = 1 = Current DTC
        /// </summary>
        /// <param name="errmsg"></param>
        /// <returns></returns>
        public bool CMD_iMEB_DTC_Report(ref string errmsg,int mode, ref DTC_RESULT result)
        {
            DTC_RESULT _result = new DTC_RESULT();
            bool _ReceiveOK    = false;

            //  0x19 = Read DTC Information Request Service ID
            //  0x02 = Report DTC By Status Mask
            //  0x08 = DTC Status Mask(0x08=ALL DTC, 0x01=Current DTC)
            byte[] Master_0_SF = new byte[8] { 0x03, 0x19, 0x02, 0x08, 0x00, 0x00, 0x00, 0x00 };
            byte[] Master_1_SF = new byte[8] { 0x03, 0x19, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00 };
            byte[] SendData_SF = new byte[8] { 0x03, 0x19, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00 };
            
            if (mode == 0) SendData_SF = Master_0_SF;
            else           SendData_SF = Master_1_SF;

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[1000];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 10, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.Report", _EcuID, readdata, ReadDC);

            byte[] conversionOnlyDTCCodes  = new byte[1000];  // 0xHH 0xMM 0xLL 0xSS
            int    conversionIndex         = 0;
            bool   isShort                 = false;           // DTC 응답 코드가 단문일 경우 처리
            bool   isResponsePositive      = false;
            if (_EcuID == 0x07D9)
            {
                int receivedataLength = ReadDC;
                if ((readdata[0]&0xF0)==0)
                {
                    // 단문 메세지로 DTC코드가 없을 경우
                    isShort = true;
                }
                if ((readdata[0] & 0xF0) == 0x10)
                {
                    if ( (readdata[2]==0x59)&&(readdata[3]==0x02)&&(readdata[4]==0x89) )
                    {
                        isResponsePositive        = true;          // 장문 데이터일 경우 서두부문은 미리 변환.
                        conversionOnlyDTCCodes[0] = readdata[5];
                        conversionOnlyDTCCodes[1] = readdata[6];
                        conversionOnlyDTCCodes[2] = readdata[7];
                        conversionIndex           = 3;
                    }                   
                }
                byte Check_SN   = 0x01;
                int  dataIndex  = 8;
                bool Check_Loop = true;
                if ((!isShort)&&(isResponsePositive))
                {
                    // 장문이고 긍정응답일 경우 나머지 메세지 변환.
                    do
                    {
                        if (Check_SN == (readdata[dataIndex]&0x0F))
                        {                            
                            conversionOnlyDTCCodes[conversionIndex] = readdata[dataIndex + 1];
                            conversionOnlyDTCCodes[conversionIndex+1] = readdata[dataIndex + 2];
                            conversionOnlyDTCCodes[conversionIndex+2] = readdata[dataIndex + 3];
                            conversionOnlyDTCCodes[conversionIndex+3] = readdata[dataIndex + 4];
                            conversionOnlyDTCCodes[conversionIndex+4] = readdata[dataIndex + 5];
                            conversionOnlyDTCCodes[conversionIndex+5] = readdata[dataIndex + 6];
                            conversionOnlyDTCCodes[conversionIndex+6] = readdata[dataIndex + 7];
                            conversionIndex = conversionIndex + 7;
                        }
                        if (receivedataLength > (dataIndex + 7))
                        {
                            Check_SN++;
                            if (Check_SN > 0x0F) Check_SN = 0x00;
                            dataIndex = dataIndex + 8;
                        }
                        else Check_Loop = false;
                    } while (Check_Loop);

                    int ci = 0;
                    _result.Counts = 0;
                    for (int i=0; i< FF_DL; i=i+4)
                    {
                        Int32 HighData   = 0x00;
                        Int32 MidData    = 0x00;
                        Int32 LowData    = 0x00;
                        Int32 MixData    = 0x00000000;
                        byte  StatusData = 0x00;

                        HighData   = conversionOnlyDTCCodes[i];
                        MidData    = conversionOnlyDTCCodes[i + 1];
                        LowData    = conversionOnlyDTCCodes[i + 2];
                        StatusData = conversionOnlyDTCCodes[i + 3];

                        HighData = (HighData & 0x000000FF) << 16;
                        MidData  = (MidData  & 0x000000FF) << 8;
                        LowData  = (LowData  & 0x000000FF);
                        
                        MixData = HighData | MidData | LowData;

                        _result.Codes[ci]  = MixData - 0x400000;
                        _result.Status[ci] = StatusData;
                        ci++;
                        _result.Counts++;
                    }
                    _ReceiveOK = true;
                }


            }
            else return false;

            if (_ReceiveOK) result = _result;

            return true;
        }
        /// <summary>
        /// CAN -> ECU
        /// Report DTC Extended Data Record by DTC Number
        /// DTC 부가정보 보고
        /// </summary>
        /// <param name="errmsg"></param>
        /// <returns></returns>
        public bool CMD_iMEB_DTC_Extended(ref string errmsg,int DTC_id)
        {
            //  0x19 = Read DTC Information Request Service ID
            //  0x06 = Report DTC Extended Data Record by DTC Number
            //  0x?? = DTC High Byte
            //  0x?? = DTC Middle Byte
            //  0x?? = DTC Low Byte
            //  0x01 = DTC Extended Data Record Number
            byte highbyte   = (byte)( ((DTC_id & 0x00FF0000) >> 16) & (0x000000FF) );
            byte middlebyte = (byte)( ((DTC_id & 0x0000FF00) >> 8) & (0x000000FF));
            byte lowbyte    = (byte)( DTC_id & 0x000000FF );
            byte[] SendData_SF = new byte[8] { 0x03, 0x19, 0x06, highbyte, middlebyte, lowbyte, 0x01, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);

            errmsg = Temp_ECUDataToString("DTC.Extended", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                // 확인 후 다시 코드 정리
                if ((readdata[0] == 0x0002) && (readdata[1] == 0x0050) && (readdata[2] == 0x0060)) return true;
            }
            else return false;

            return true;
        }








        public bool CMD_iMEB_DiagnosticMode(ref string errmsg)
        {

            byte[] SendData_SF = new byte[8] { 0x02, 0x10, 0x60, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);

            errmsg = Temp_ECUDataToString("Diag.Open", _EcuID, readdata, ReadDC);
            if (_EcuID == 0x07D9)
            {
                if ((readdata[0]==0x0002)&&(readdata[1]==0x0050)&&(readdata[2]==0x0060)) return true;
            }
            else return false;

            return true;
        }

        public bool CMD_iMEB_DiagnosticMode_STOP(ref string errmsg)
        {

            byte[] SendData_SF = new byte[8] { 0x01, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
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

        private bool CanSend_SingleMBCOnly(int id, byte[] data, ref byte[] READDATA)
        {
            // Real Data Count Only...
            int _handle = this.handle;
            int _flags = this.flags;
            Canlib.canStatus status;

            int RecvID = 0;
            byte[] RecvData = new byte[8];

            int RecvDLC;
            int RecvFlags;
            long RecvTime;

            bool ReceiveOK = false;
            // Data Send
            //Canlib.canFlushReceiveQueue(_handle);
            //Canlib.canFlushTransmitQueue(_handle);
            status = Canlib.canWriteWait(this.handle, id, data, 8, _flags,1);
            status = Canlib.canReadWait(this.handle, out RecvID, RecvData, out RecvDLC, out RecvFlags, out RecvTime, 3);
                if ((status == Canlib.canStatus.canOK) && (!((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)))
                {
                    if (RecvID == 0x07D9)
                    {
                        if ((RecvData[0] & 0xF0) == 0x00)
                        {                           
                            READDATA = RecvData;
                            ReceiveOK = true;                          
                        }
                    }                   
                }                        
            return ReceiveOK;
        }



        private bool CanSend_Single(int id, byte[] data, int ReceiveRetryCount, ref int EcuID, ref byte[] READDATA, ref int ReadDataCount, ref string ErrMsg,ref int FfDl)
        {
            // Real Data Count Only...
            int _handle = this.handle;
            int _flags = this.flags;
            Canlib.canStatus status;

            byte[] SendData = new byte[8];
            byte[] TempData = new byte[8] { 0x30, 0x00, 0x02, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };  // Flow Control Signal

            int    RecvID         = 0;
            byte[] RecvData       = new byte[8];
            byte[] RecvDataBuffer = new byte[1000];
            int    RecvDataIndex  = 0;
            int    RecvDLC;
            int    RecvFlags;
            long   RecvTime;
            long   RecvSetTimeOut     = 50;
            int    _ReceiveRetryCount = 0;
            Stopwatch iStopWatch = new Stopwatch();
            bool PendingFlag = false;


            //Canlib.canFlushReceiveQueue(_handle);
            //Canlib.canFlushTransmitQueue(_handle);
            //Canlib.canBusOn(_handle);
            // Data Send
            SendData = data;           
            status   = Canlib.canWrite(_handle, id, SendData, 8, _flags);
            //if ((SendData[0]==0x02)&&(SendData[1]==0x3E)&&(SendData[2]==0x80))
            //{
            //    return true; // 온리 전송만......
            //}
            //CheckStatus("PC->ECU " + MsgConv(id, 8, data, 0), status);
            bool MsgReceive = false;
            byte CurrentSN = 0x00;
            iStopWatch.Restart();
            while (!MsgReceive)
            {
                status = Canlib.canReadWait(_handle, out RecvID, RecvData, out RecvDLC, out RecvFlags, out RecvTime, RecvSetTimeOut);                
                if ((status == Canlib.canStatus.canOK) && (!((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)))
                {
                    if (RecvID == 0x07D9)
                    {
                        //CheckStatus("ECU->PC " + MsgConv(RecvID, 8, RecvData, 0), status);
                        if ((RecvData[0] & 0xF0) == 0x10)
                        { // FF Respons
                            // FC (PC->ECU)
                            status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                            for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                            // 현재 SN 확인
                            CurrentSN = RecvData[0];
                            CurrentSN = (byte)(CurrentSN & 0x000F);
                            if (CurrentSN==0x0F)
                            {
                                status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                            }
                            // FF Analay
                            int FF_DL = (RecvData[0] & 0x0F) * 0xFF + (RecvData[1]);
                            if (FF_DL > 0)
                            {
                                ReceiveRetryCount = (FF_DL / 7) + 1;
                                FfDl = FF_DL;
                            }

                        }
                        if ((RecvData[0] & 0xF0) == 0x20)
                        { // CF Respons
                            for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                        }
                        if ((RecvData[0] & 0xF0) == 0x00)
                        { // Single Frame
                            // Pending Check !!!
                            int lastResult = RecvData[0];
                            if ((RecvData[1] == 0x7F) && (RecvData[2]==0xBB) && (RecvData[lastResult] == 0x78))
                            {
                                // 재시도                                
                                PendingFlag = true;
                            }
                            else
                            {
                                MsgReceive = true;
                                break;
                            }
                        }
                    }
                }
                if ( (_ReceiveRetryCount > ReceiveRetryCount) &&(!PendingFlag) )
                {
                            break;
                }

                if ( (iStopWatch.ElapsedMilliseconds > 7000.0)&&(PendingFlag)) break;

                _ReceiveRetryCount++;
            }

            iStopWatch.Stop();
            //Canlib.canBusOff(_handle);
            if (RecvDataIndex > 0)
            {
                EcuID         = RecvID;
                READDATA      = RecvDataBuffer;
                ReadDataCount = RecvDataIndex;
            }
            else
            {
                EcuID         = RecvID;
                READDATA      = RecvData;
                ReadDataCount = 8;
            }
            return true;
        }
        private bool CanSend_Single1(int id, byte[] data, int ReceiveRetryCount, ref int EcuID, ref byte[] READDATA, ref int ReadDataCount, ref string ErrMsg, ref int FfDl)
        {
            // Real Data Count Only...
            int _handle = this.handle;
            int _flags = this.flags;
            Canlib.canStatus status;

            byte[] SendData = new byte[8];
            byte[] TempData = new byte[8] { 0x30, 0x00, 0x02, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };  // Flow Control Signal

            int RecvID = 0;
            byte[] RecvData = new byte[8];
            byte[] RecvDataBuffer = new byte[100];
            int RecvDataIndex = 0;
            int RecvDLC;
            int RecvFlags;
            long RecvTime;
            long RecvSetTimeOut = 50;
            int _ReceiveRetryCount = 0;
            Stopwatch iStopWatch = new Stopwatch();
            bool PendingFlag = false;


            //Canlib.canFlushReceiveQueue(_handle);
            //Canlib.canFlushTransmitQueue(_handle);
            //Canlib.canBusOn(_handle);
            // Data Send
            SendData = data;
            status = Canlib.canWrite(_handle, id, SendData, 8, _flags);
            //if ((SendData[0]==0x02)&&(SendData[1]==0x3E)&&(SendData[2]==0x80))
            //{
            //    return true; // 온리 전송만......
            //}
            //CheckStatus("PC->ECU " + MsgConv(id, 8, data, 0), status);
            bool MsgReceive = false;
            byte CurrentSN = 0x00;
            iStopWatch.Restart();
            while (!MsgReceive)
            {
                status = Canlib.canReadWait(_handle, out RecvID, RecvData, out RecvDLC, out RecvFlags, out RecvTime, RecvSetTimeOut);
                if ((status == Canlib.canStatus.canOK) && (!((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)))
                {
                    if (RecvID == 0x07D9)
                    {
                        //CheckStatus("ECU->PC " + MsgConv(RecvID, 8, RecvData, 0), status);
                        if ((RecvData[0] & 0xF0) == 0x10)
                        { // FF Respons
                            // FC (PC->ECU)
                            status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                            for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                            // 현재 SN 확인
                            CurrentSN = RecvData[0];
                            CurrentSN = (byte)(CurrentSN & 0x000F);
                            if (CurrentSN == 0x0F)
                            {
                                status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                            }
                            // FF Analay
                            int FF_DL = (RecvData[0] & 0x0F) * 0xFF + (RecvData[1]);
                            if (FF_DL > 0)
                            {
                                ReceiveRetryCount = (FF_DL / 7) + 1;
                                FfDl = FF_DL;
                            }

                        }
                        if ((RecvData[0] & 0xF0) == 0x20)
                        { // CF Respons
                            for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                        }
                        if ((RecvData[0] & 0xF0) == 0x00)
                        { // Single Frame
                            // Pending Check !!!
                            int lastResult = RecvData[0];
                            if ((RecvData[1] == 0x7F) && (RecvData[2] == 0xBB) && (RecvData[lastResult] == 0x78))
                            {
                                // 재시도                                
                                PendingFlag = true;
                            }
                            else
                            {
                                MsgReceive = true;
                                break;
                            }
                        }
                    }
                }
                if ((_ReceiveRetryCount > ReceiveRetryCount) && (!PendingFlag))
                {
                    break;
                }

                if ((iStopWatch.ElapsedMilliseconds > 7000.0) && (PendingFlag)) break;

                _ReceiveRetryCount++;
            }

            iStopWatch.Stop();
            //Canlib.canBusOff(_handle);
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
        private bool CanSend_Frame(int id, byte[] data, int datalength, int ReceiveRetryCount, ref int EcuID, ref byte[] READDATA, ref int ReadDataCount, ref string ErrMsg, ref int FfDl)
        {
            // Real Data Count Only...
            int _handle = this.handle;
            int _flags = this.flags;
            Canlib.canStatus status;

            byte[] SendData = new byte[8];
            byte[] TempData = new byte[8] { 0x30, 0x00, 0x02, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };  // Flow Control Signal

            int RecvID            = 0;
            byte[] RecvData       = new byte[8];
            byte[] RecvDataBuffer = new byte[300];
            int RecvDataIndex     = 0;
            int RecvDLC;
            int RecvFlags;
            long RecvTime;
            long RecvSetTimeOut    = 100;
            int _ReceiveRetryCount = 0;


            Canlib.canFlushReceiveQueue(_handle);
            //Canlib.canFlushTransmitQueue(_handle);
            //Canlib.canBusOff(_handle);
            //Canlib.canBusOn(_handle);
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
                status = Canlib.canWriteWait(_handle, id, SendData, 8, _flags,20);
                //CheckStatus("PC->ECU " + MsgConv(id, 8, SendData, 0), status);
                bool MsgReceive = false;
                byte CurrentSN  = 0x00;
                while (!MsgReceive)
                {
                    status = Canlib.canReadWait(_handle, out RecvID, RecvData, out RecvDLC, out RecvFlags, out RecvTime, RecvSetTimeOut);
                    if ((status == Canlib.canStatus.canOK) && (!((_flags & Canlib.canMSG_ERROR_FRAME) == Canlib.canMSG_ERROR_FRAME)))
                    {
                        if (RecvID == 0x07D9)
                        {
                            //CheckStatus("ECU->PC " + MsgConv(RecvID, 8, RecvData, 0), status);
                            // Flow Control(FC) 메세지 응답 처리
                            if ((RecvData[0] & 0xF0) == 0x30)
                            {
                                //for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                                // Flow Control 데이터는 무시하고 받지 않음 - 2017-08-16
                                // 추후 FS,BS,STmin 등의 데이터 변경 및 문제시 현코드 수정 요망
                                MsgReceive = true;
                                break;
                            }
                            if ((RecvData[0] & 0xF0) == 0x10)
                            { // FF Respons                                
                                // FC (PC->ECU)
                                status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                                for (int i = 0; i < 8; i++) { RecvDataBuffer[RecvDataIndex] = RecvData[i]; RecvDataIndex++; }
                                // 현재 SN 확인
                                CurrentSN = RecvData[0];
                                CurrentSN = (byte)(CurrentSN & 0x000F);
                                if (CurrentSN == 0x0F)
                                {
                                    status = Canlib.canWrite(_handle, id, TempData, 8, _flags);
                                }
                                // FF Analay
                                int FF_DL = (RecvData[0] & 0x0F) * 0xFF + (RecvData[1]);
                                if (FF_DL > 0)
                                {
                                    ReceiveRetryCount = (FF_DL / 7) + 1;
                                    FfDl = FF_DL;
                                }
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
            //Canlib.canBusOff(_handle);
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



        /// <summary>
        /// MotorSpeed=RPM=0~4095까지, MotorPosition=0.01mm~40.95mm까지
        /// </summary>
        /// <param name="MotorSpeed"></param>
        /// <param name="MotorPosition"></param>
        /// <returns></returns>
        public bool CMD_iMEB_LeakTest(int MotorSpeed, double MotorPosition, byte Actuator, out string sndMsg,ref string emsg,ref string resultMsg)
        {
            // 모터속도,위치, 액츄에이터를 변환하고 배열에 담고 호출만...
            //if (!onBus) return RET.CAN_NotInitialize;

            byte[] COMMAND = new byte[12];
            byte highbyte = 0x00;
            byte lowbyte = 0x00;

            Delay(30);
            #region 모터속도,위치,동작기기 변환
            // Motor Speed Conversion
            Int16 motorspeed = 0;
            //if (MotorSpeed > 0x0FFF) MotorSpeed = 0x0FFF;
            //if (MotorSpeed < 0x0000) MotorSpeed = 0x0000;
            motorspeed = (short)MotorSpeed;
            // Position Conversion
            Int16 position = 0;
            if (MotorPosition < 0.0)   MotorPosition = 0.0;
            if (MotorPosition > 40.95) MotorPosition = 40.95;
            double Pos = MotorPosition * 100.0;
            position = (short)Pos;
            // Actuator Conversion
            byte actuator = Actuator;
            #endregion
            #region 기본 명령어 설정 및 변수 대입
            // 명령 설정
            COMMAND[0] = 0x10;  // Single Frame
            COMMAND[1] = 0x09;  // SF_DL
            COMMAND[2] = 0x2F;
            COMMAND[3] = 0xF0;
            COMMAND[4] = 0x4A;
            COMMAND[5] = 0x03;
            highbyte   = (byte)((motorspeed & 0xFF00) >> 8);
            lowbyte    = (byte)(motorspeed & 0x00FF);
            COMMAND[6] = highbyte;
            COMMAND[7] = lowbyte;
            highbyte    = (byte)((position & 0xFF00) >> 8);
            lowbyte     = (byte)(position & 0x00FF);
            COMMAND[8]  = 0x21;   // SN
            COMMAND[9]  = highbyte;
            COMMAND[10] = lowbyte;
            COMMAND[11] = actuator;
            #endregion
            #region 명령 수행


            byte[] READDATA   = new byte[50];
            int EcuID         = 0;
            int ReadDataCount = 0;
            int FfDl          = 0;
            string ErrMsg     = "";
            int RetryCount    = 4;
            bool loopChk = true;
            while (loopChk)
            {
                bool _Chk = CanSend_Frame(0x07D1, COMMAND, 12, 1, ref EcuID, ref  READDATA, ref  ReadDataCount, ref  ErrMsg, ref FfDl);

                if ((READDATA[2] == 0x6F) && (READDATA[3] == 0xF0) && (READDATA[4] == 0x4A))
                {
                        loopChk = false;
                        break;
                }
                else RetryCount++;
                if (RetryCount>3)
                {
                    loopChk = false;
                    break;
                }
            }
            emsg = Temp_ECUDataToString("LeakTestStart", EcuID, READDATA, ReadDataCount);
            #endregion
            sndMsg ="";
            sndMsg = Temp_ECUDataToString("iMEB LeakTest",0x7D1,COMMAND,12);

            if ( (READDATA[2] == 0x6F)&&(READDATA[3] == 0xF0)&&(READDATA[4] == 0x4A) )
            {
                resultMsg = "";
                return true;
            }
            else
            {
                if ( (READDATA[1] == 0x7F)&&(READDATA[2] == 0x2F) )
                {
                    byte eCode = 0x00;
                    eCode = READDATA[3];
                    switch (eCode)
                    {
                        case 0x13 :
                            resultMsg = "0x13 = Incorrect Message Length or Invalid Format";
                            break;
                        case 0x21:
                            resultMsg = "0x21 = Busy Repeat Request";
                            break;
                        case 0x22:
                            resultMsg = "0x22 = ";
                            break;
                        case 0x31:
                            resultMsg = "0x31 = Conditions Not Correct";
                            break;
                        case 0x33:
                            resultMsg = "0x33 = Security Aceess Denied";
                            break;
                        default:
                            resultMsg = string.Format("0x{0:X2}",eCode)+" Undefined";
                            break;
                    }
                }
                return false;
            }
            
        }
        public RET CMD_iMEB_LeakTest_STOP(ref string emsg)
        {

            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x4A, 0x00, 0x00, 0x00, 0x00 };
            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
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
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
            msg = Temp_ECUDataToString("LeakTestSupport", _EcuID, readdata, ReadDC);
            emsg = msg;
            if (ret) return RET.OK;
            else return RET.NG;
        }
        public bool CMD_iMEB_MBCReadNew(ref double pos, ref double current)
        {
            byte[] SendData_SF = new byte[8] { 0x03, 0x22, 0x10, 0x03, 0x00, 0x00, 0x00, 0x00 };
            byte[] readdata    = new byte[10];

            double Pos1 = 0.0;
            double Amp1 = 0.0;
            Int16 pc = 0;
            Int16 ac = 0;
            bool ret = CanSend_SingleMBCOnly(0x07D1, SendData_SF, ref readdata);
            if (ret)
            {
                if ((readdata[1] == 0x62) && (readdata[2] == 0x10) && (readdata[3] == 0x03))
                {
                    pc = (Int16)((((readdata[4] & 0x00FF) << 8) | (readdata[5] & 0x00FF)));
                    ac = (Int16)((((readdata[6] & 0x00FF) << 8) | (readdata[7] & 0x00FF)));
                    //Pos1 = (double)(((readdata[4] & 0x00FF) << 8) | (readdata[5] & 0x00FF));
                    //Amp1 = (double)(((readdata[6] & 0x00FF) << 8) | (readdata[7] & 0x00FF));
                    Pos1 = (double)pc;
                    Amp1 = (double)ac;
                    Pos1 = Pos1 * 0.01;
                    Amp1 = Amp1 * 0.01;




                }
            }
            else
            {
                pos     = 0.0;
                current = 0.0;
                return false;
            }
            pos = Pos1;
            current = Amp1;
            return true;
        }
        public bool CMD_iMEB_MBCRead(ref double pos, ref double current, out byte[] EcuReadData)
        {
            byte[] SendData_SF = new byte[8] { 0x03, 0x22, 0x10, 0x03, 0x00, 0x00, 0x00, 0x00 };
            byte[] readdata = new byte[10];
 
            Int16 pos_temp = 0x0000;
            Int16 A_temp   = 0x0000;
            double d_pos;
            double d_A;

            bool ret = CanSend_SingleMBCOnly(0x07D1, SendData_SF, ref readdata);

            if (ret)
            {
                if ((readdata[1] == 0x62)&& (readdata[2] == 0x10)&& (readdata[3] == 0x03))
                {
                    pos_temp     = (Int16)(((readdata[4] & 0x00FF) << 8) | (readdata[5] & 0x00FF));
                    A_temp       = (Int16)(((readdata[6] & 0x00FF) << 8) | (readdata[7] & 0x00FF));
                    d_pos        = (double)pos_temp;
                    d_A          = (double)A_temp;
                    pos          = d_pos * 0.01;
                    current      = d_A * 0.01;
                }
            }
            else
            {
                EcuReadData = readdata;
                return false;
            }
            EcuReadData = readdata;
            return true;
        }

        public bool CMD_iMEB_NVHTest(byte mode, ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x05, 0x2F, 0xF0, 0x4B, 0x03, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            SendData_SF[5] = mode;

            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
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
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
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
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
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
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
            //errmsg = Temp_ECUDataToString("TestPresent", _EcuID, readdata, ReadDC);
            //if (readdata[0]==0x7E)
            return true;
        }
        public bool CMD_iMEB_MotorInit(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x49, 0x03, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single1(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);
            // 리턴 메세지 확인 처리 
            errmsg = Temp_ECUDataToString("MotorInit", _EcuID, readdata, ReadDC);
            if (readdata[1] == 0x7F) return false;
            if ((readdata[1] == 0x6F) && (readdata[2] == 0xF0) && (readdata[3] == 0x49)) return true;
            else return false;

            return ret;
        }



        public enum ACTUATOR1 : byte
        {
            FrontLeftValve_Inlet    = 0x01,   // bit 0
            FrontLeftValve_Outlet   = 0x02,
            FrontRightValve_Inlet   = 0x04,
            FrontRightValve_Outlet  = 0x08,
            RearLeftValve_Inlet     = 0x10,
            RearLeftValve_Outlet    = 0x20,
            RearRightValve_Inlet    = 0x40,
            RearRightValve_Outlet   = 0x80   // bit 7
        }
        public enum ACTUATOR2 : byte
        {
            PSV      = 0x01,
            LP_WSV   = 0x02,
            WSV      = 0x04,
            RCV      = 0x08,
            TCV      = 0x10,
            LP_TCV   = 0x20,
            LSV      = 0x40,
            Reserved = 0x80
        }
        /// <summary>
        /// 멀티플 액츄애이터 콘트롤(장문메세지)
        /// </summary>
        /// <param name="Actuator1"></param>
        /// <param name="Actuator2"></param>
        /// <param name="Parameter"></param>
        /// <param name="sendmsg"></param>
        /// <param name="emsg"></param>
        /// <returns></returns>
        public bool CMD_iMEB_SOLs(byte Actuator1, byte Actuator2, byte Parameter,double ActivationTime, out string sendmsg, ref string emsg)
        {
            // Multiple actuator control
            // 

            byte[] COMMAND     = new byte[11];
            byte highbyte      = 0x00;
            byte lowbyte       = 0x00;
            Int16 ActTime      = 0;
            double CalcActTime = 0.0;

            #region 기본 명령어 설정 및 변수 대입
            // 명령 설정
            COMMAND[0] = 0x10; // FF 
            COMMAND[1] = 0x08;

            COMMAND[2] = 0x2F;
            COMMAND[3] = 0xF0;
            COMMAND[4] = 0x1E;
            COMMAND[5] = Parameter;  

            COMMAND[6] = Actuator1;
            COMMAND[7] = Actuator2;


            CalcActTime = (ActivationTime>0.0) ? (ActivationTime/5.0) : (0.0);
            
            ActTime = (Int16)CalcActTime;

            COMMAND[8]  = 0x21; 
            highbyte    = (byte)((ActTime & 0xFF00) >> 8);
            lowbyte     = (byte)(ActTime & 0x00FF);
            COMMAND[9]  = highbyte;
            COMMAND[10]  = lowbyte;
            #endregion
            #region 명령 수행

            sendmsg = Temp_ECUDataToString("Multiple actuator control", 0x7D1, COMMAND, 11);


            byte[] READDATA = new byte[150];
            int EcuID = 0;
            int ReadDataCount = 0;
            int FfDl = 0;
            string ErrMsg = "";
            bool _Chk = CanSend_Frame(0x07D1, COMMAND, 11, 1, ref EcuID, ref  READDATA, ref  ReadDataCount, ref  ErrMsg,ref FfDl);
            emsg = Temp_ECUDataToString("Multiple actuator control", EcuID, READDATA, ReadDataCount);
            #endregion

            if ((READDATA[2] == 0x6F) && (READDATA[3] == 0xF0) &&(READDATA[2] == 0x01) ) _Chk = true;
            if (_Chk) return true;
            else return false;
         
        }




















        /// <summary>
        /// 2017/09/15 추가
        /// 전진시 가압을 하기위한 솔 오픈 명령(0x44)
        /// </summary>
        /// <param name="errmsg"></param>
        /// <returns></returns>
        public bool CMD_iMEB_MotorFWDSol(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x43, 0x03, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);
            // 리턴 메세지 확인 처리 
            errmsg = Temp_ECUDataToString("Motor SOL FWD", _EcuID, readdata, ReadDC);

            return true;
        }
        /// <summary>
        /// 2017/09/15 추가
        /// 전진시 가압을 하기위한 솔 오픈 명령(0x44)
        /// </summary>
        /// <param name="errmsg"></param>
        /// <returns></returns>
        public bool CMD_iMEB_MotorBWDSol(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x42, 0x03, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);
            // 리턴 메세지 확인 처리 
            errmsg = Temp_ECUDataToString("Motor SOL BWD", _EcuID, readdata, ReadDC);

            return true;
        }



        public bool CMD_iMEB_MotorFWDSolStop(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x43, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);
            // 리턴 메세지 확인 처리 
            errmsg = Temp_ECUDataToString("Motor SOL FWD", _EcuID, readdata, ReadDC);

            return true;
        }

        public bool CMD_iMEB_MotorBWDSolStop(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x04, 0x2F, 0xF0, 0x42, 0x00, 0x00, 0x00, 0x00 };

            string msg = "";
            int _EcuID = 0;
            byte[] readdata = new byte[50];
            int ReadDC = 0;
            int FF_DL = 0;
            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg, ref FF_DL);
            // 리턴 메세지 확인 처리 
            errmsg = Temp_ECUDataToString("Motor SOL BWD", _EcuID, readdata, ReadDC);

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

            int FF_DL = 0;


            bool ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);

            errmsg = Temp_ECUDataToString("MotorInit", _EcuID, readdata, ReadDC);

            return true;
        }
        public bool CMD_iMEB_ECUReset(ref string errmsg)
        {
            byte[] SendData_SF = new byte[8] { 0x02, 0x11, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

            string msg      = "";
            int _EcuID      = 0;
            byte[] readdata = new byte[50];
            int ReadDC      = 0;
            int FF_DL       = 0;

            bool        ret = CanSend_Single(0x07D1, SendData_SF, 1, ref _EcuID, ref readdata, ref ReadDC, ref  msg,ref FF_DL);

            errmsg = Temp_ECUDataToString("ECU Reset", _EcuID, readdata, ReadDC);
            if ((readdata[1] == 0x51) && (readdata[2] == 0x01)) return true;
            else                                                return false;
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
    /// <summary>
    /// 2017-10-16
    /// MES/LDS 연결용 TCP/IP , UDP 서버/클라이언트
    /// </summary>
    public class MESLDS : IDisposable
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
                        if (LDSTcpClient != null) LDSTcpClient.Close();
                    }
                    disposed = true;
                }
            }
            ~MESLDS()
            {
                Dispose(false);
            }
        #endregion

            /*
             * 
    1. SPEC
        시작(1) / 분류(2) / 공정(3) / 길이(4) / 바코드(11) / 전공정(1) / 시작시간(14) / 종료시간(14) / 데이터(..) / 종료(1)
                        헤더(50)           
        예) 2 00 030 0026 A07AA090001 1 20070101123033 20070101123033 #ABCDEFGHIJ..# 3
             
    2. 내용 
        1) 시    작: STX(0x02)
        2) 분    류: 타입요청:00 / 정보요청:01 / 작업결과:02 / 사양 및 SPEC:03 / 설비 알람:04
                     응답(NG):91 / 응답(OK):92 / 정상수신:98 / 재전송:99 
        3) 공    정: 각 공정번호
        4) 길    이: 헤더 내용을 뺀 송수신 데이터 총길이(바코드부터..종료까지)
        5) 바 코 드: 차종 및 모델(1),연도(2),월(1),일(2),라인 및 Shift(1),생산시리얼번호(4)
        6) 전 공 정: 전공정 처리여부(NG:0, OK:1, 시작:2)
        7) 시작시간: 작업 시작시간
        8) 종료시간: 작업 종료시간
        9) 데 이 터: 타입 / 정보 / 작업결과 데이터 / 사양 및 SPEC / 설비알람
        10) 종    료: ETX(0x03)

             */





            public enum LDS_SEND : int
        {
            Request_Infomation = 1,
            Result             = 2,
            System_Status      = 3
        }
        /// <summary>
        /// LDS/MES 공용으로 사용됨. 동일 프로토콜 사용함.
        /// </summary>
        public class LDS_MSG_SPEC 
        {
            public const byte   LDS_STX            = 0x02;
            public const byte   LDS_ETX            = 0x03;
            public const string LDS_BODY           = "#";
            public const string LDS_CDOE_REQ_INFO  = "01";
            public const string LDS_CDOE_RESULT    = "02";
            public const string LDS_CDOE_STATUS    = "17";
            public const string LDS_CDOE_REQ_TYPE  = "00";
            public const string LDS_CDOE_REQ_SPEC  = "03";

            public const string LDS_PROCESS_NUM    = "020"; // 020 = LEAK TEST 공정번호, 본 클래스 사용시 공정번호는 맞게 수정후 사용.

            public const string LDS_PART_REQ       = "1";
            public const string LDS_PART_RESULT    = "E";

            private string STX                     = System.Text.Encoding.ASCII.GetString(new[] { LDS_STX });
            private string CODE                    = "";
            private string PROCESS_NUM             = LDS_PROCESS_NUM;
            private string BODY_LENGTH             = "0002";
            private string BARCODE                 = "12345678901";
            private string RCODE                   = LDS_PART_REQ;
            private string JOBSTARTDATE            = "YYYYMMDDHHNNSS";
            private string JOBENDDATE              = "YYYYMMDDHHNNSS";
            private string BODY_START_CHAR         = "#";
            private string BODY_MSG                = "01";
            private string BODY_END_CHAR           = "#";
            private string EXT                     = System.Text.Encoding.ASCII.GetString(new[] { LDS_ETX });

            private string SendMsg                 = "";

            public void GenMessage_TestOnly()
            {
                CODE         = LDS_CDOE_REQ_INFO;
                PROCESS_NUM  = LDS_PROCESS_NUM;
                BODY_LENGTH  = "0002";
                BARCODE      = "12345678901";
                RCODE        = "1";
                JOBSTARTDATE = string.Format("{0:yyyyMMddHHmmss}",DateTime.Now);
                JOBENDDATE   = JOBSTARTDATE;
                BODY_MSG     = "01";
                this.SendMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;
                
            }
            /// <summary>
            /// GM = Generate Message Request Infomation
            /// 메세지 생성 - 정보요청
            /// </summary>
            /// <param name="sendType"></param>
            /// <param name="barCode"></param>
            /// <param name="Msg"></param>
            /// <returns></returns>
            public string GM_ReqInfo(string barCode)
            {
                string ResultMsg = "";

                this.BODY_LENGTH  = "0002";
                this.CODE         = LDS_CDOE_REQ_INFO;
                if (barCode.Length != 11) barCode = "99999999999";
                this.BARCODE      = barCode;
                this.BODY_MSG     = "01";
                this.JOBSTARTDATE = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                this.JOBENDDATE   = JOBSTARTDATE;

                ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                return ResultMsg;
            }
            /// <summary>
            /// 타입식별 요청
            /// </summary>
            /// <param name="barCode"></param>
            /// <param name="typeStr"></param>
            /// <returns></returns>
            public string GM_ReqType(string barCode,string typeStr)
            {
                string ResultMsg = "";

                this.CODE = LDS_CDOE_REQ_TYPE;
                if (barCode.Length != 11) barCode = "99999999999";
                this.BARCODE         = barCode;
                this.BODY_MSG        = typeStr;
                string bodyLengthStr = string.Format("{0:D4}", BODY_MSG.Length);
                this.BODY_LENGTH     = bodyLengthStr;
                this.JOBSTARTDATE    = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                this.JOBENDDATE      = JOBSTARTDATE;

                ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                return ResultMsg;
            }
            public string GM_ReqSpec(string barCode, string modelStr)
            {
                string ResultMsg = "";

                this.CODE            = LDS_CDOE_REQ_SPEC;
                if (barCode.Length != 11) barCode = "99999999999";
                this.BARCODE         = barCode;
                this.BODY_MSG        = modelStr;
                string bodyLengthStr = string.Format("{0:D4}", BODY_MSG.Length);
                this.BODY_LENGTH     = bodyLengthStr;
                this.JOBSTARTDATE    = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                this.JOBENDDATE      = JOBSTARTDATE;

                ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                return ResultMsg;
            }


            public string GM_Result(string barCode,string startTime,string endTime,string bodyMsg)
            {
                string ResultMsg = "";

                this.CODE            = LDS_CDOE_RESULT;
                if (barCode.Length != 11) barCode = "99999999999";
                this.BARCODE         = barCode;
                this.BODY_MSG        = bodyMsg;
                int bodyLength       = BODY_MSG.Length;
                string bodyLengthStr = string.Format("{0:D4}",bodyLength);
                this.BODY_LENGTH     = bodyLengthStr;
                this.JOBSTARTDATE    = startTime;
                this.RCODE           = "E"; // 결과 전송시 E
                this.JOBENDDATE      = endTime;

                ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                return ResultMsg;
            }
            public string GM_Status(string barCode, string startTime, string endTime, string bodyMsg)
            {
                string ResultMsg = "";

                this.CODE            = LDS_CDOE_STATUS;
                if (barCode.Length != 11) barCode = "99999999999";
                this.BARCODE         = barCode;
                this.BODY_MSG        = bodyMsg;
                int bodyLength       = BODY_MSG.Length;
                string bodyLengthStr = string.Format("{0:D4}", bodyLength);
                this.BODY_LENGTH     = bodyLengthStr;
                this.JOBSTARTDATE    = startTime;// string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                this.RCODE           = "E"; // 결과 전송시 E
                this.JOBENDDATE      = endTime;//JOBSTARTDATE;

                ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                return ResultMsg;
            }
            public string GetMsg()
            {
                return SendMsg;
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
        private TcpClient LDSTcpClient = null;

        private LDS_MSG_SPEC LDSMsg = new LDS_MSG_SPEC();
        private LDS_MSG_SPEC MESMsg = new LDS_MSG_SPEC();

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
            _Log.Info("[MES/LDS] 로그 핸들이 활성화 되었습니다.");
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
            if (this._LogEnable)
            {
                this._Log.Info("[UDP Server] UDP Server가 실행되었습니다.(true/false) = " + this._IsUDPServerRun.ToString());
            }
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
                this._Log.Info("[UDP Server] Create UDP Server Port = " + string.Format("{0:D}", this._UDP_ServerPort));
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
        /// <summary>
        /// PC -> MES 타입요청
        /// </summary>
        /// <param name="barCode"></param>
        /// <param name="resultMsg"></param>
        /// <param name="resultCode"></param>
        /// <returns></returns>
        public bool MES_RequestType(string barCode,string typeStr, ref string resultMsg, ref int resultCode)
        {
            string SendMsg = LDSMsg.GM_ReqType(barCode, typeStr);
            byte[] RecieveMsg = new byte[1024];

            bool chk = MES_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg  = MES_Parse(RecieveMsg, ref resCode);
                resultCode = resCode;
            }
            else
            {
                resultMsg  = "TCP 수신 오류";
                resultCode = -1;
            }

            if (_LogEnable)
            {
                _Log.Info("[MES] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else _Log.Info("[MES] Receive Msg = null");
                _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }
            if (resultCode == -1) return false;
            else                  return true;
        }
        /// <summary>
        /// PC -> MES 모델 사양 및 스펙 요청
        /// </summary>
        /// <param name="barCode"></param>
        /// <param name="modelStr"></param>
        /// <param name="resultMsg"></param>
        /// <param name="resultCode"></param>
        /// <returns></returns>
        public bool MES_RequestSpec(string barCode, string modelStr, ref string resultMsg, ref int resultCode)
        {
            string SendMsg = LDSMsg.GM_ReqSpec(barCode, modelStr);
            byte[] RecieveMsg = new byte[1024];

            bool chk = MES_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg  = MES_Parse(RecieveMsg, ref resCode);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }

            if (_LogEnable)
            {
                _Log.Info("[MES] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else _Log.Info("[MES] Receive Msg = null");
                _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }
            if (resultCode == -1) return false;
            else return true;
        }
        /// <summary>
        /// MES에서 읽어 들인 데이터 문자열을 스펙데이터 배열로 변환
        /// </summary>
        /// <param name="specStr"></param>
        /// <param name="delimiters"></param>
        /// <param name="masterCountNum"></param>
        /// <param name="dataValue"></param>
        /// <returns></returns>
        public bool MES_SpecConverter(string specStr,char[] delimiters, int masterCountNum,ref double[] dataValue)
        {
            double[] conValue = new double[100]; // 최대 100까지 변환
            if (masterCountNum > 100) return false;

            try
            {
                string[] wordSplit  = specStr.Split(delimiters); // 구분자로 문자열 분리
                int      splitCount = wordSplit.Length;          // 구분된 문자열 갯수
                if (splitCount != masterCountNum) return false;  // 구분된 문자열 갯수와 설정된 갯수가 다를 경우 변환 안함
                for (int i=0; i<masterCountNum; i++)
                {
                    bool chk = double.TryParse(wordSplit[i], out conValue[i]);                    
                }
                dataValue = conValue;
            }
            catch (Exception e1)
            {
                return false;
            }
            return true;
        }

         

        public bool MES_RequestInfomation(string barCode, ref string resultMsg, ref int resultCode)
        {
            // 
            string SendMsg = LDSMsg.GM_ReqInfo(barCode);
            byte[] RecieveMsg = new byte[1024];

            bool chk = MES_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg = MES_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }

            if (_LogEnable)
            {
                _Log.Info("[MES] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else                    _Log.Info("[MES] Receive Msg = null");
                _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }

            return true;
        }
        private bool MES_TcpSend(string SendMsg, ref byte[] RecieveMsg)
        {
            bool StxFind = false;
            int StxIndex = -10;
            bool EtxFind = false;
            int EtxIndex = -10;
            Stopwatch _st = new Stopwatch();
            byte[] outbuf = new byte[1024];
            byte[] outbytes = new byte[1024];
            try
            {
                MESTcpClient = new TcpClient(this.MES_ServerIp, this._MES_ServerPort);
                if (!MESTcpClient.Connected)
                {
                    if (this._LogEnable)
                    {
                        _Log.Info("[MES] MES_TcpSend(TcpClient) 연결이 되지 않습니다.");
                    }
                    return false;
                }

                byte[] buff = Encoding.ASCII.GetBytes(SendMsg);

                NetworkStream stream = MESTcpClient.GetStream();

                stream.ReadTimeout = 1000;
                stream.WriteTimeout = 1000;

                stream.Write(buff, 0, buff.Length);

                int nbytes;
                MemoryStream mem = new MemoryStream();


                _st.Restart();
                while ((nbytes = stream.Read(outbuf, 0, outbuf.Length)) > 0)
                {
                    mem.Write(outbuf, 0, nbytes);
                    for (int i = 0; i < outbuf.Length; i++)
                    {
                        if ((outbuf[i] == 0x02) && (!StxFind))
                        {
                            StxFind = true;
                            StxIndex = i;
                        }
                        if ((outbuf[i] == 0x03) && (!EtxFind))
                        {
                            EtxFind = true;
                            EtxIndex = i;
                        }
                    }
                    if ((StxFind) && (EtxFind)) break;
                    if (_st.ElapsedMilliseconds > 5000) break;
                }
                outbytes = mem.ToArray();
                mem.Close();

                stream.Close();
                MESTcpClient.Close();
            }
            catch (Exception e1)
            {
                RecieveMsg = null;
                return false;
            }

            if ((StxFind) && (EtxFind))
            {
                RecieveMsg = outbytes;
                return true;
            }
            else
            {
                RecieveMsg = null;
                return false;
            }
        }
        private string MES_Parse(byte[] msg, ref int resultCode)
        {
            string   strMsg        = Encoding.ASCII.GetString(msg);
            Char[]   delimiters    = { '#' };
            string[] wordsSplit    = strMsg.Split(delimiters, 3);

            string   resultCodeStr = System.Text.Encoding.ASCII.GetString(msg, 1, 2);
            if (resultCodeStr.Length > 0)
            {
                if (!int.TryParse(resultCodeStr, out resultCode)) resultCode = 0;
                // resultCode = 91=NG, 92=OK, 98
            }
            string FindMsg = "";
            if (wordsSplit[1].Length > 0) FindMsg = wordsSplit[1];
            else FindMsg = "No Msg";
            FindMsg = FindMsg.Trim();
            return FindMsg;
        }
        public bool MES_Result(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
        {
            string SendMsg = LDSMsg.GM_Result(barCode, startTime, endTime, TestResult);
            byte[] RecieveMsg = new byte[1024];

            bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }

            if (_LogEnable)
            {
                _Log.Info("[MES] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else                    _Log.Info("[MES] Receive Msg = null");
                _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }

            return true;
        }
        public bool MES_Status(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
        {

            string SendMsg = LDSMsg.GM_Status(barCode, startTime, endTime, TestResult);
            byte[] RecieveMsg = new byte[1024];

            bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }
            // 문자열 파싱

            if (_LogEnable)
            {

                _Log.Info("[MES] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[LDSMES Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else                    _Log.Info("[MES] Receive Msg = null");
                _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }

            return true;
        }

        #endregion
        #region LDS 서버 관련
        public bool LDS_RequestInfomation(string barCode,ref string resultMsg,ref int resultCode)
        {
            string SendMsg    = LDSMsg.GM_ReqInfo(barCode);
            byte[] RecieveMsg = new byte[1024];

            bool chk          = LDS_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }
                
            if (_LogEnable)
            {
                _Log.Info("[LDS] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[LDS] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else                    _Log.Info("[LDS] Receive Msg = null"); 
                _Log.Info("[LDS] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[LDS] Receive Parse(응답코드) = " + string.Format("{0:D2}",resultCode));
            }

            return true;
        }
        private bool LDS_TcpSend(string SendMsg,ref byte[] RecieveMsg)
        {
            bool StxFind  = false;
            int StxIndex  = -10;
            bool EtxFind  = false;
            int EtxIndex  = -10;
            Stopwatch _st = new Stopwatch();
            byte[] outbuf = new byte[1024];
            byte[] outbytes = new byte[1024];
            try
            {
                LDSTcpClient = new TcpClient(this.LDS_ServerIp, this._LDS_ServerPort);
                if (!LDSTcpClient.Connected)
                {
                    if (this._LogEnable)
                    {
                        _Log.Info("[LDS] LDS_TcpSend(TcpClient) 연결이 되지 않습니다.");
                    }
                    return false;
                }

                byte[] buff = Encoding.ASCII.GetBytes(SendMsg);

                NetworkStream stream = LDSTcpClient.GetStream();

                stream.ReadTimeout  = 1000;
                stream.WriteTimeout = 1000;

                stream.Write(buff, 0, buff.Length);

                int nbytes;
                MemoryStream mem = new MemoryStream();


                _st.Restart();
                while ((nbytes = stream.Read(outbuf, 0, outbuf.Length)) > 0)
                {
                    mem.Write(outbuf, 0, nbytes);
                    for (int i = 0; i < outbuf.Length; i++)
                    {
                        if ((outbuf[i] == 0x02) && (!StxFind))
                        {
                            StxFind = true;
                            StxIndex = i;
                        }
                        if ((outbuf[i] == 0x03) && (!EtxFind))
                        {
                            EtxFind = true;
                            EtxIndex = i;
                        }
                    }
                    if ((StxFind) && (EtxFind)) break;
                    if (_st.ElapsedMilliseconds > 5000) break;
                }
                outbytes = mem.ToArray();
                mem.Close();

                stream.Close();
                LDSTcpClient.Close();
            }
            catch (Exception e1) 
            {
                RecieveMsg = null;
                return false;
            }

            if ((StxFind) && (EtxFind))
            {
                RecieveMsg = outbytes;
                return true;
            }
            else
            {
                RecieveMsg = null;
                return false;
            }
        }
        private string LDS_Parse(byte[] msg,ref int resultCode)
        {
            string strMsg = Encoding.ASCII.GetString(msg); 
            Char[] delimiters = { '#' };
            string[] wordsSplit = strMsg.Split(delimiters, 3);
            

            string resultCodeStr = System.Text.Encoding.ASCII.GetString(msg,1,2);
            if (resultCodeStr.Length>0)
            {
                if (!int.TryParse(resultCodeStr, out resultCode)) resultCode = 0;
            }
            string FindMsg = "";
            if (wordsSplit[1].Length > 0) FindMsg = wordsSplit[1];
            else FindMsg = "No Msg";
            FindMsg = FindMsg.Trim();
            return FindMsg;
        }
        public bool LDS_Result(string barCode,string startTime,string endTime, string TestResult, ref string resultMsg, ref int resultCode)
        {
            string SendMsg    = LDSMsg.GM_Result(barCode,startTime,endTime,TestResult);
            byte[] RecieveMsg = new byte[1024];

            bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }

            if (_LogEnable)
            {
                _Log.Info("[LDS] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[LDS] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else                    _Log.Info("[LDS] Receive Msg = null"); 
                _Log.Info("[LDS] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[LDS] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }

            return true;
        }
        public bool LDS_Status(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
        {

            string SendMsg    = LDSMsg.GM_Status(barCode, startTime, endTime, TestResult);
            byte[] RecieveMsg = new byte[1024];

            bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

            int resCode = -1;
            if (chk)
            {
                resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                resultCode = resCode;
            }
            else
            {
                resultMsg = "TCP 수신 오류";
                resultCode = -1;
            }
            // 문자열 파싱

            if (_LogEnable)
            {
                
                _Log.Info("[LDS] Send Msg = " + SendMsg);
                if (RecieveMsg != null) _Log.Info("[LDS] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                else                    _Log.Info("[LDS] Receive Msg = null"); 
                _Log.Info("[LDS] Receive Parse(Result Msg) = " + resultMsg);
                _Log.Info("[LDS] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
            }

            return true;
        }
        #endregion

    }


    /// <summary>
    /// MES 연동용 사양정의 클래스
    /// </summary>
    public class MESSPEC
    {
        public class Specification
        {

            [Browsable(true)]
            [Category("1. 생산 정보")]
            [DisplayName("1-1. 모델 이름")]
            [Description("PLC에 적용된 최근 모델 정보")]
            public string LastProductCode { get; set; }
            [Browsable(false)]
            [Category("1. 생산 정보")]
            [DisplayName("1-2. 바코드")]
            [Description("임시 바코드 정보 - 비정규 시험테스트시만 사용되는 코드")]
            public string ImsiBarCode { get; set; }


            [Category("2. iMEB ASSY 내부 리크")]
            [DisplayName("2-1. 고진공 리크량(mmHg) min")]
            [Description("최소")]
            public double InternalLeak_LeakMin { get; set; }
            [Category("2. iMEB ASSY 내부 리크")]
            [DisplayName("2-2. 고진공 리크량(mmHg) max")]
            [Description("최소")]
            public double InternalLeak_LeakMax { get; set; }


            [Category("3. iMEB ASSY 외부 리크")]
            [DisplayName("3-1. 1.5바 리크량(mbar) min")]
            [Description("최소")]
            public double ExternalLeak_15LeakMin { get; set; }
            [Category("3. iMEB ASSY 외부 리크")]
            [DisplayName("3-2. 1.5바 리크량(mbar) max")]
            [Description("최대")]
            public double ExternalLeak_15LeakMax { get; set; }
            [Category("3. iMEB ASSY 외부 리크")]
            [DisplayName("3-3. 5.0바 리크량(mbar) min")]
            [Description("최소")]
            public double ExternalLeak_50LeakMin { get; set; }
            [Category("3. iMEB ASSY 외부 리크")]
            [DisplayName("3-4. 5.0바 리크량(mbar) max")]
            [Description("최대")]
            public double ExternalLeak_50LeakMax { get; set; }


            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-1. MC12측 진공 누설량(mmHg) min")]
            [Description("MC12측 진공누설량")]
            public double LeakII_MC12LeakMin { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-2. MC12측 진공 누설량(mmHg) max")]
            [Description("MC12측 진공누설량")]
            public double LeakII_MC12LeakMax { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-3. 전진 기동 전류(A) min")]
            [Description("전진 기동시 전류")]
            public double LeakII_FWDPeakMin { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-4. 전진 기동 전류(A) max")]
            [Description("전진 기동시 전류")]
            public double LeakII_FWDPeakMax { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-5. 전진 평균 전류(A) min")]
            [Description("전진 행정중 평균전류")]
            public double LeakII_FWDAvgMin { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-6. 전진 평균 전류(A) max")]
            [Description("전진 행정중 편균전류")]
            public double LeakII_FWDAvgmax { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-7. 전진 전류 편차(A) min")]
            [Description("전진 행정중 표준 편차")]
            public double LeakII_FWDDivaMin { get; set; }
            [Category("4. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 전진행정")]
            [DisplayName("4-8. 전진 전류 편차(A) max")]
            [Description("전진 행정중 표준 편차")]
            public double LeakII_FWDDivaMax { get; set; }



            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-1. MC34측 진공 누설량(mmHg) min")]
            [Description("MC34측 진공누설량")]
            public double LeakII_MC34LeakMin { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-2. MC34측 진공 누설량(mmHg) max")]
            [Description("MC34측 진공누설량")]
            public double LeakII_MC34LeakMax { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-3. 후진 기동 전류(A) min")]
            [Description("전진 기동시 전류")]
            public double LeakII_BWDPeakMin { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-4. 후진 기동 전류(A) max")]
            [Description("전진 기동시 전류")]
            public double LeakII_BWDPeakMax { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-5. 후진 평균 전류(A) min")]
            [Description("전진 행정중 평균 전류")]
            public double LeakII_BWDAvgMin { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-6. 후진 평균 전류(A) max")]
            [Description("전진 행정중 평균 전류")]
            public double LeakII_BWDAvgmax { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-7. 후진 전류 편차(A) min")]
            [Description("진진 행정중 표준 편차")]
            public double LeakII_BWDDivaMin { get; set; }
            [Category("5. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 후진행정")]
            [DisplayName("5-8. 후진 전류 편차(A) max")]
            [Description("전진 행정중 표준 편차")]
            public double LeakII_BWDDivaMax { get; set; }


            // 2017-12-01 추가 분
            [Category("6. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 공통")]
            [DisplayName("6-1. 모터 파형 검사(구간 평균편차(A)) Min")]
            [Description("모터 전진/후진 행정중 각 구간별 이동평균을 측정후 해당 구간의 최대/최소의 편차값 ")]
            public double LeakII_Motor_C_Min { get; set; }
            [Category("6. iMEB ASSY 피스톤무빙, 피스톤스트로크 및 내부 리크 테스트[II] - 공통")]
            [DisplayName("6-2. 모터 파형 검사(구간 평균편차(A)) Max")]
            [Description("모터 전진/후진 행정중 각 구간별 이동평균을 측정후 해당 구간의 최대/최소의 편차값 ")]
            public double LeakII_Motor_C_Max { get; set; }

            // ECU DTC 검사는 Norminal 값과 같은지 비교하여 결과를 산출
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-1. ECU MODEL")]
            [Description("Code & Type check ")]
            public string DTC_ECU_MODEL { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-2. ECU Part Number")]
            [Description("ECU Part Number ")]
            public string DTC_ECU_PartNumber { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-3. ECU HW Version")]
            [Description("ECU Customer HW Version")]
            public string DTC_ECU_HW_Version { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-4. ECU SW Version")]
            [Description("ECU Customer HW Version")]
            public string DTC_ECU_SW_Version { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-5. ECU HSW Version")]
            [Description("eeprom MOBIS H&SW Version")]
            public string DTC_ECU_HSW_Version { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-6. ECU Reset result")]
            [Description("ECU Self test routine result")]
            public string DTC_ECU_Reset { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-7. ECU Motor current Min")]
            [Description("ECU Motor current min")]
            public double DTC_ECU_Motor_A_Min { get; set; }
            [Category("7. iMEB ASSY ECU DTC 테스트 ")]
            [DisplayName("7-8. ECU Motor current Max")]
            [Description("ECU Motor current max")]
            public double DTC_ECU_Motor_A_Max { get; set; }


        }
        public class Local_Var
        {
            public string StartDate  = "";
            public string EndDate    = "";
            public string ModelCode  = "";
            public string BarCode    = "";
            public string TestResult = "OK";
        }
        // name of the .xml file
        public Specification SPEC = new Specification();
        public Local_Var LocalVar = new Local_Var();
        private string CONFIG_FNAME = "iMEB_LT4_MESSpecification.xml";
        /// <summary>
        /// 내부 정의된 SPEC 데이터로 xml화일에서 정보 읽기
        /// </summary>
        /// <returns></returns>
        public bool GetConfigData()
        {
            if (!File.Exists(CONFIG_FNAME)) // create config file with default values
            {
                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(Specification));
                    Specification sxml = new Specification();
                    xs.Serialize(fs, sxml);
                    this.SPEC = sxml;
                }
            }
            else // read configuration from file
            {
                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(Specification));
                    Specification sc = (Specification)xs.Deserialize(fs);
                    this.SPEC = sc;
                }
            }
            return true;
        }
        /// <summary>
        /// 내부 정의된 SPEC 데이터를 화일에 저장
        /// </summary>
        /// <returns></returns>
        public bool SaveConfigData()
        {
            if (File.Exists(CONFIG_FNAME))
            {
                File.Delete(CONFIG_FNAME);
            }
            using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
            {
                XmlSerializer xs = new XmlSerializer(typeof(Specification));
                xs.Serialize(fs, this.SPEC);
                fs.Flush();
                fs.Close();
                return true;
            }
        }                             
    }



    public class Trendline
    {
        /// <summary>
        /// Get's the line's best fit slope of the line
        /// </summary>
        public double Slope { get; private set; }

        /// <summary>
        /// Get's the Mia
        /// </summary>
        public double Offset { get; private set; }

        public double[] ValuesX { get; private set; }

        public double[] ValuesY { get; private set; }

        private double sumY;

        private double sumX;

        private double sumXY;

        private double sumX2;

        private double n;

        public Trendline(double[] x, double[] y)
        {
            this.ValuesX = x;
            this.ValuesY = y;

            this.sumXY = this.calculateSumXsYsProduct(this.ValuesX, this.ValuesY);
            this.sumX  = this.calculateSumXs(this.ValuesX);
            this.sumY  = this.calculateSumYs(this.ValuesY);
            this.sumX2 = this.calculateSumXsSquare(this.ValuesX);
            this.n     = this.ValuesX.Length;

            this.calculateSlope();
            this.calculateOffset();
        }

        public Trendline(double[] y)
        {
            //Assinging Y Values
            this.ValuesY = y;
            int length = y.Length;
            //Assinging X Values
            this.ValuesX = new double[length];
            for (int i = 0; i < length; i++)
            {
                this.ValuesX[i] = i;
            }


            this.sumXY = this.calculateSumXsYsProduct(this.ValuesX, this.ValuesY);
            this.sumX  = this.calculateSumXs(this.ValuesX);
            this.sumY  = this.calculateSumYs(this.ValuesY);
            this.sumX2 = this.calculateSumXsSquare(this.ValuesX);
            this.n     = this.ValuesX.Length;
        }

        public Trendline(double[][] xy)
        {
            double[] xs = new double[xy.Length];
            double[] ys = new double[xy.Length];
            for (int i = 0; i < xy.Length; i++)
            {
                xs[i] = xy[i][0];
                ys[i] = xy[i][1];
            }
            this.ValuesX = xs;
            this.ValuesY = ys;
        }

        private double calculateSumXsYsProduct(double[] xs, double[] ys)
        {
            double sum = 0;
            for (int i = 0; i < xs.Length; i++)
            {
                sum += xs[i] * ys[i];
            }
            return sum;
        }

        private double calculateSumXs(double[] xs)
        {
            double sum = 0;
            foreach (double x in xs)
            {
                sum += x;
            }
            return sum;
        }

        private double calculateSumYs(double[] ys)
        {
            double sum = 0;
            foreach (double y in ys)
            {
                sum += y;
            }
            return sum;
        }

        private double calculateSumXsSquare(double[] xs)
        {
            double sum = 0;
            foreach (double x in xs)
            {
                sum += System.Math.Pow(x, 2);
            }
            return sum;
        }

        private void calculateSlope()
        {
            try
            {
                Slope = (n * sumXY - sumX * sumY) / (n * sumX2 - System.Math.Pow(sumX, 2));
            }
            catch (DivideByZeroException)
            {
                Slope = 0;
            }
        }

        private void calculateOffset()
        {
            try
            {
                Slope = (sumY - Slope * sumX) / n;
            }
            catch (DivideByZeroException) { }
        }
    }






}
