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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SIMWOODV1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    ///
    /// 

    static class Constants
    {
        public static string API_USERNAME_STORE = "";
        public static string API_PASSWORD_STORE = "";
        public static string API_ACCOUNT_STORE = "";
        public static string API_MOBILE_STORE = "";

        public const string vault_resource = "SIMWOOD";

        public static string current_contact = "";
    }

    public partial class MainPage : Page
    {


        public MainPage()
        {
            this.InitializeComponent();
            Source_Frame.Navigate(typeof(SIMWOODV1.Authenticate));
        }
    }
}
