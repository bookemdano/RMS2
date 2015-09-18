using JiraShare;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Jiranator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Count() > 0)
            {
                var str = ControlHelper.ReadString("entSprint");
                var key = new SprintKey(str);
                JiraHttpAccess.GetSprintLive(key.Project, key.Sprint, false);
                Shutdown();
            }
        }
    }
}
