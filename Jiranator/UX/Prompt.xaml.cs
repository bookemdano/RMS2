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
    /// Interaction logic for Prompt.xaml
    /// </summary>
    public partial class Prompt : Window
    {
        public Prompt()
        {
            InitializeComponent();
        }

        public Prompt(string label, string def) :
            this()
        {
            sta.Content = label;
            ent.Text = def;
        }
        
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Response = ent.Text;
            Close();
        }

        public string Response { get; set; }
    }
}
