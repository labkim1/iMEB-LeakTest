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
using System.Windows.Navigation;
using System.Windows.Shapes;

using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using System.Collections;
using iMEB_LeakTest_No4.KMTSLIBS;
using iMEB_LeakTest_No4.Config;
using System.Windows.Threading;
using System.Threading;

namespace iMEB_LeakTest_No4.SubScreen
{
    /// <summary>
    /// LogIn.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LogIn : MetroWindow
    {
        LeakTest _LeakTest = null;
        public LogIn(LeakTest LeakTest) 
        {
            InitializeComponent();
            _LeakTest = LeakTest;
        }
    }
}
