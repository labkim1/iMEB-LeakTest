using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Diagnostics;
namespace iMEB_LeakTest_No4
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        Mutex mutex = null;
        protected override void OnStartup(StartupEventArgs e)
        {
            mutex = new System.Threading.Mutex(false, "supercooluniquemutex");
            try
            {
                bool tryAgain = true;
                while (tryAgain)
                {
                    bool result = false;
                    try
                    {
                        result = mutex.WaitOne(0, false);
                    }
                    catch (AbandonedMutexException ex)
                    {
                        // No action required
                        result = true;
                    }
                    if (result)
                    {
                        // Run the application
                        tryAgain = false;
                        base.OnStartup(e);
                    }
                    else
                    {
                        foreach (Process proc in Process.GetProcesses())
                        {
                            if (proc.ProcessName.Equals(Process.GetCurrentProcess().ProcessName) && proc.Id != Process.GetCurrentProcess().Id)
                            {
                                proc.Kill();
                                break;
                            }
                        }
                        // Wait for process to close
                        Thread.Sleep(2000);
                    }
                }
            }
            finally
            {
                if (mutex != null)
                {
                    mutex.Close();
                    mutex = null;
                }
            }


        }
    }
}
