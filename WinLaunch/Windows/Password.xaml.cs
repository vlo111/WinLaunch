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

namespace WinLaunch
{
    public partial class Password: Window
    {
        public string password = "";

        public Password()
        {
            InitializeComponent();

            tbxPassword.Focus();
        }

        private void tbxPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            password = tbxPassword.Text;

            if (!String.IsNullOrEmpty(password))
            {
                //correct url
                btnConfirm.IsEnabled = true;
            }
            else
            {
                //incorrect url
                btnConfirm.IsEnabled = false;
            }
        }

        private void ConfirmClicked(object sender, RoutedEventArgs e)
        {
            if (tbxPassword.Text == "Eli$@An@$t2022")
            {
                password = tbxPassword.Text;

                DialogResult = true;
                Close();
            }
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
