using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Jirate
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        JiraSprintViewModel _vm = new JiraSprintViewModel();
        public MainPage()
        {
            this.InitializeComponent();
            grid.DataContext = _vm;

            _vm.Load();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _vm.Load();
        }

        private void SearchBox_QuerySubmitted(SearchBox sender, SearchBoxQuerySubmittedEventArgs args)
        {
            _vm.FindIssue(args.QueryText);
        }

        private void lst_ItemClick(object sender, ItemClickEventArgs e)
        {
            _vm.GotoIssue("MOB-1971");
        }
    }
}
