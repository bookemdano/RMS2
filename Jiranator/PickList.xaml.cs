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

namespace Jiranator
{
    /// <summary>
    /// Interaction logic for PickList.xaml
    /// </summary>
    public partial class PickList : Window
    {
        public PickList()
        {
            InitializeComponent();
            var rnd = new System.Random();
            cmb.Items.Add(rnd.NextDouble());
            cmb.Items.Add(rnd.NextDouble());
            cmb.Items.Add(rnd.NextDouble());
            cmb.Items.Add(rnd.NextDouble());
            cmb.Items.Add(rnd.NextDouble());
            cmb.Items.Add(rnd.NextDouble());
        }

        private void cmb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
