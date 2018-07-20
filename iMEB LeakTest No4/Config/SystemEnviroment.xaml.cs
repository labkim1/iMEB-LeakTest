using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using System.Collections;
using iMEB_LeakTest_No4.KMTSLIBS;
using iMEB_LeakTest_No4.Config;
using System.Windows.Threading;
using System.Threading;


namespace iMEB_LeakTest_No4.Config
{
    /// <summary>
    /// SystemEnviroment.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SystemEnviroment : MetroWindow
    {
        iMEB_LeakTest_No4.KMTSLIBS.LeakTest SubLeakTest;
     

        private DispatcherTimer UITimer = new DispatcherTimer();

        private iMEB.SysConfig        _SysConfig        = null;
        private iMEB.ExternalLeakTest _ExternalLeakTest = null;
        private iMEB.InternalLeakTest _InternalLeakTest = null;
        private iMEB.EcuDtc           _EcuDtc           = null;
        private iMEB.ECULeakTest      _EcuLeak          = null;

        private MESSPEC.Specification            _MESSpec  = null;

        public SystemEnviroment(iMEB_LeakTest_No4.KMTSLIBS.LeakTest LeakTest)
        {
            InitializeComponent();
            SubLeakTest = LeakTest;
            // 메인 화면 업데이트용 타이머 설정
            UITimer.Interval = TimeSpan.FromMilliseconds(100);
            UITimer.Tick    += new EventHandler(UITimer_Tick);
            UITimer.Start();

            // 터치 키보드 활성화

            // 프로퍼티 그리드 초기 설정
            ArrayList selected = new ArrayList();
            _ExternalLeakTest = SubLeakTest.CurExternalLeakTest;
            object item = this.GetType().GetField("_ExternalLeakTest",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item);
            this.PG_ExtLeak.SelectedObjects = selected.ToArray();
            this.PG_ExtLeak.HelpVisible = true;
            this.PG_ExtLeak.RefreshPropertyList();

            selected.Clear();
            _SysConfig = SubLeakTest.CurConfig;
            object item1 = this.GetType().GetField("_SysConfig",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item1);
            this.PG_SysConfig.SelectedObjects = selected.ToArray();
            this.PG_SysConfig.HelpVisible     = true;
            this.PG_SysConfig.RefreshPropertyList();

            selected.Clear();
            _InternalLeakTest = SubLeakTest.CurInternalLeakTest;
            object item4 = this.GetType().GetField("_InternalLeakTest",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item4);
            this.PG_IntLeak.SelectedObjects = selected.ToArray();
            this.PG_IntLeak.HelpVisible = true;
            this.PG_IntLeak.RefreshPropertyList();


            selected.Clear();
            _EcuDtc = SubLeakTest.CurEcuDtc;
            object item2 = this.GetType().GetField("_EcuDtc",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item2);
            this.PG_EcuDtc.SelectedObjects = selected.ToArray();
            this.PG_EcuDtc.HelpVisible = true;
            this.PG_EcuDtc.RefreshPropertyList();

            selected.Clear();
            _EcuLeak = SubLeakTest.CurECULeakTest;
            object item3 = this.GetType().GetField("_EcuLeak",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item3);
            this.PG_EcuLeak.SelectedObjects = selected.ToArray();
            this.PG_EcuLeak.HelpVisible = true;
            this.PG_EcuLeak.RefreshPropertyList();


            selected.Clear();
            _MESSpec = SubLeakTest.CurMesSpec.SPEC;
            object item5 = this.GetType().GetField("_MESSpec",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item5);
            this.PG_MES.SelectedObjects = selected.ToArray();
            this.PG_MES.HelpVisible = true;
            this.PG_MES.RefreshPropertyList();
        }
        short PLCSOL_State   = 0x00;
        short PLCCLAMP_State = 0x00;
        short PLCLIVE_State  = 0x00;
        private void UITimer_Tick(object sender, EventArgs e)
        {
            if (SubLeakTest.PLC_isLive)
            {
                #region PLC 관련 정보 표시 
                //
                bool readOK = SubLeakTest.M_Plc.CMD_CurrentState();
                if (readOK)
                {
                    if (SubLeakTest.M_Plc.PLCState.Auto)    { this.LED_AUTO.Value = true;  this.LED_MANUAL.Value = false; }
                    if (SubLeakTest.M_Plc.PLCState.Manual)  { this.LED_AUTO.Value = false; this.LED_MANUAL.Value = true; }
                    if (SubLeakTest.M_Plc.PLCState.Loading)   this.LED_LOADING.Value  = true;
                    else                                      this.LED_LOADING.Value  = false;
                    if (SubLeakTest.M_Plc.PLCState.Reset)     this.LED_RESET.Value    = true;
                    else                                      this.LED_RESET.Value    = false;
                    if (SubLeakTest.M_Plc.PLCState.LeakMode)  this.LED_LEAKMODE.Value = true;
                    else                                      this.LED_LEAKMODE.Value = false;
                    if (SubLeakTest.M_Plc.PLCState.Error)     this.LED_ERROR.Value = true;
                    else                                      this.LED_ERROR.Value = false;
                }
                // SOL State
                if (SubLeakTest.M_Plc.CMD_PLCSOLState(ref PLCSOL_State))
                {
                    this.Btn_WHEELPORT_MAIN_VAC.Value = (PLCSOL_State & (short)M_PLC.SOL.WHEEL_PORT_MAIN_VAC_OPEN) > 0 ? true : false;
                    this.Btn_WHEELPORT_12.Value       = (PLCSOL_State & (short)M_PLC.SOL.WHEEL_PORT_12_VAC_OPEN) > 0   ? true : false;
                    this.Btn_WHEELPORT_34.Value       = (PLCSOL_State & (short)M_PLC.SOL.WHEEL_PORT_34_VAC_OPEN) > 0   ? true : false;
                    this.Btn_ECU_POWER.Value          = (PLCSOL_State & (short)M_PLC.SOL.ECU_POWER_ON) > 0             ? true : false;
                }
                // PLC Live State
                if (SubLeakTest.M_Plc.CMD_PLCLIVEState(ref PLCLIVE_State))
                {
                    this.LED_LIVECODE.Value = PLCLIVE_State > 0 ? true : false;
                }
                // CLAMP State
                if (SubLeakTest.M_Plc.CMD_PLCCLAMPState(ref PLCCLAMP_State))
                {
                    this.Btn_POSITION.Value        = (PLCCLAMP_State & (short)M_PLC.CLAMP.ECU_CONNECTOR_FWD) > 0 ? true : false;
                    this.Btn_WORKCLAMP.Value       = (PLCCLAMP_State & (short)M_PLC.CLAMP.WORK_CLAMP) > 0        ? true : false;
                    this.Btn_SUCTION.Value         = (PLCCLAMP_State & (short)M_PLC.CLAMP.SUCTION_FWD) > 0       ? true : false;
                    this.Btn_IGN.Value             = (PLCCLAMP_State & (short)M_PLC.CLAMP.IGNITION_ON) > 0       ? true : false;
                    this.Btn_WHEELPORT_12_FB.Value = (PLCCLAMP_State & (short)M_PLC.CLAMP.WHEEL_PORT_12_FWD) > 0 ? true : false;
                    this.Btn_WHEELPORT_34_FB.Value = (PLCCLAMP_State & (short)M_PLC.CLAMP.WHEEL_PORT_34_FWD) > 0 ? true : false;
                    this.Btn_RESERVE.Value         = (PLCCLAMP_State & (short)M_PLC.CLAMP.RESERVE_UP) > 0        ? true : false;
                    this.Btn_RESERVE_AIR.Value     = (PLCCLAMP_State & (UInt16)M_PLC.CLAMP.RESERVE_AIR_OPEN) > 0 ? true : false;
                }
                #endregion
            }
        }


        private void Btn_WHEELPORT_12_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_WHEELPORT_12.Value;
            bool Chk = false;
            short _LastState = PLCSOL_State;
            int _SetCmd      = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.WHEEL_PORT_12_VAC_OPEN);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.WHEEL_PORT_12_VAC_OPEN;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.WHEEL_PORT_12_VAC_CLOSE);
            }
            Chk = SubLeakTest.M_Plc.CMD_SolSetting(_SetCmd, 2.0);            
        }

        private void Btn_WHEELPORT_34_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_WHEELPORT_34.Value;
            bool Chk = false;
            short _LastState = PLCSOL_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.WHEEL_PORT_34_VAC_OPEN);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.WHEEL_PORT_34_VAC_OPEN;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.WHEEL_PORT_34_VAC_CLOSE);
            }
            Chk = SubLeakTest.M_Plc.CMD_SolSetting(_SetCmd, 2.0);
        }

        private void Btn_ECU_POWER_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_ECU_POWER.Value;
            bool Chk = false;
            short _LastState = PLCSOL_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.ECU_POWER_OFF;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.ECU_POWER_ON);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.ECU_POWER_ON;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.ECU_POWER_OFF);
            }
            Chk = SubLeakTest.M_Plc.CMD_SolSetting(_SetCmd, 2.0);
        }

        private void Btn_WHEELPORT_MAIN_VAC_Click_1(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_WHEELPORT_MAIN_VAC.Value;
            bool Chk = false;
            short _LastState = PLCSOL_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.WHEEL_PORT_MAIN_VAC_OPEN);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.SOL.WHEEL_PORT_MAIN_VAC_OPEN;
                _SetCmd = _SetCmd & (int)(~M_PLC.SOL.WHEEL_PORT_MAIN_VAC_CLOSE);
            }
            Chk = SubLeakTest.M_Plc.CMD_SolSetting(_SetCmd, 2.0);
        }

        private void Btn_POSITION_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_POSITION.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.ECU_CONNECTOR_FWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.ECU_CONNECTOR_BWD);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.ECU_CONNECTOR_FWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.ECU_CONNECTOR_BWD);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_WORKCLAMP_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_WORKCLAMP.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.WORK_UNCLAMP;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.WORK_CLAMP);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.WORK_CLAMP;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.WORK_UNCLAMP);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_SUCTION_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_SUCTION.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.SUCTION_BWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.SUCTION_FWD);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.SUCTION_FWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.SUCTION_BWD);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_IGN_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_IGN.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.IGNITION_OFF;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.IGNITION_ON);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.IGNITION_ON;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.IGNITION_OFF);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_WHEELPORT_12_FB_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_WHEELPORT_12_FB.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.WHEEL_PORT_12_BWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.WHEEL_PORT_12_FWD);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.WHEEL_PORT_12_FWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.WHEEL_PORT_12_BWD);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_WHEELPORT_34_FB_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_WHEELPORT_34_FB.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.WHEEL_PORT_34_BWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.WHEEL_PORT_34_FWD);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.WHEEL_PORT_34_FWD;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.WHEEL_PORT_34_BWD);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_RESERVE_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_RESERVE.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.RESERVE_DOWN;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.RESERVE_UP);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.RESERVE_UP;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.RESERVE_DOWN);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void Btn_RESERVE_AIR_Click(object sender, RoutedEventArgs e)
        {
            bool lastValue = this.Btn_RESERVE_AIR.Value;
            bool Chk = false;
            short _LastState = PLCCLAMP_State;
            int _SetCmd = (int)_LastState;

            if (!lastValue)
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.RESERVE_AIR_CLOSE;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.RESERVE_AIR_OPEN);
            }
            else
            {
                _SetCmd = _SetCmd | (int)M_PLC.CLAMP.RESERVE_AIR_OPEN;
                _SetCmd = _SetCmd & (int)(~M_PLC.CLAMP.RESERVE_AIR_CLOSE);
            }
            Chk = SubLeakTest.M_Plc.CMD_ClampSetting(_SetCmd, 2.0);
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 설정화면 종료시....
            SubLeakTest.CurConfig           = _SysConfig;
            SubLeakTest.CurExternalLeakTest = _ExternalLeakTest;
            SubLeakTest.CurEcuDtc           = _EcuDtc;
            SubLeakTest.CurECULeakTest      = _EcuLeak;
            SubLeakTest.CurMesSpec.SPEC     = _MESSpec;

            iMEB.RW_SysConfig.SaveConfigData(SubLeakTest.CurConfig);
            iMEB.RW_ExternalLeakTest.SaveConfigData(SubLeakTest.CurExternalLeakTest);
            iMEB.RW_InternalLeakTest.SaveConfigData(SubLeakTest.CurInternalLeakTest);
            iMEB.RW_EcuDtc.SaveConfigData(SubLeakTest.CurEcuDtc);
            iMEB.RW_ECULeakTest.SaveConfigData(SubLeakTest.CurECULeakTest);

            SubLeakTest.CurMesSpec.SaveConfigData();
        }
    }
}
