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
using System.Net.Http;
using System.Net;
using System.Diagnostics;
using Windows.UI.Popups;
using Windows.Storage;
using Windows.Storage.Search;
using System.Threading.Tasks;


namespace SIMWOODV1
{
    /// <summary>
    /// Main login/authentication page 
    /// </summary>
    public sealed partial class Authenticate : Page
    {
        // runs when the page is loaded in the source frame by the main page booting
        public Authenticate()
        {
            // initialise
            this.InitializeComponent();

            // check if remember me file exists
            // if it does:
            // -- Access the vault and fill the boxes with any credentials
            // -- tick the box
            // -- try to auto login

            // get the path
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            path += "\\rememberme.txt";

            // if the file exists
            if (System.IO.File.Exists(path))
            {
                // tick the remeber me box
                Remember_me.IsChecked = true;

                // pull the credentials from the vault, using the constant resource ID
                var vault = new Windows.Security.Credentials.PasswordVault();

                // look through the available ones , will cause an exception if empty
                try
                {
                    var credentialList = vault.FindAllByResource(Constants.vault_resource);
                    // if theres any credentials
                    if (credentialList.Count > 0)
                    {
                        // only the first one
                        var credential = credentialList[0];
                        credential.RetrievePassword();
                        Debug.WriteLine(credential.ToString());

                        // put them in
                        API_password.Password = credential.Password.ToString();
                        API_username.Text = credential.UserName.ToString();

                        // attempt auto-login
                        //Login_Click(this, new RoutedEventArgs());
                    }
                }
                catch (System.Runtime.InteropServices.COMException e)
                {
                    Debug.WriteLine("No credentials found: {0}", e.ToString());
                }

                // otherwise there werent any
                // in which case we wait for user input and manual activation

            }
        }



        // Manual Login Activation
        private async void Login_Click(object sender, RoutedEventArgs e)

        {
            // if theres nothing in the password box complain and cancel
            if (API_password.Password == "" || API_username.Text == "" || API_Mobile.Text == "" || API_Account.Text == "")
            {
                // spring up a complaint and then cancel 
                var MessageDialog = new MessageDialog("Please enter valid credentials to log in");
                await MessageDialog.ShowAsync();
                return;
            }

            // configure an API access
            SIMAPI auth_api_check = new SIMAPI(API_username.Text, API_password.Password, API_Account.Text, API_Mobile.Text);

            // check if we are authorised
            bool success = await auth_api_check.CheckAuthAsync();

            // if we failed to correctly access the API 
            if (!success)
            {
                // spring up a complaint and then cancel 
                var MessageDialog = new MessageDialog("Failed to log in, please try again");
                await MessageDialog.ShowAsync();
                return;
            }

            // otherwise we have succeeded and we can move forwards
            // store the credentials
            Constants.API_PASSWORD_STORE = API_password.Password.ToString();
            Constants.API_USERNAME_STORE = API_username.Text.ToString();
            Constants.API_MOBILE_STORE = API_Mobile.Text.ToString();
            Constants.API_ACCOUNT_STORE = API_Account.Text.ToString();

            // if remember me is checked
            // -- save the details to the secure vault overwriting existing ones
            // -- ensure an app data file exists for it to be checked next time 
            // else
            // -- delete the appdata file

            // get the path
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string path = localFolder.Path;
            path += "\\rememberme.txt";


            if (Remember_me.IsChecked == true)
            {
                var vault = new Windows.Security.Credentials.PasswordVault();

                // check if there are already credentials, if so delete all of them (no more than one pls)
                try
                {
                    var credentialList = vault.FindAllByResource(Constants.vault_resource);
                    if (credentialList.Count > 0)
                    {
                        foreach (Windows.Security.Credentials.PasswordCredential pw in credentialList)
                        {
                            vault.Remove(pw);
                        }
                    }
                }
                catch(System.Runtime.InteropServices.COMException q)
                {
                    // there werent any anyway so it doesnt matter
                    Debug.WriteLine("No credentials need to be deleted - none exist: {0}", q.ToString());
                }

                // save details to secure vault
                vault.Add(new Windows.Security.Credentials.PasswordCredential(Constants.vault_resource, Constants.API_USERNAME_STORE, Constants.API_PASSWORD_STORE));

                // create an appdata file to tick it next time
                if (!System.IO.File.Exists(path)) System.IO.File.Create(path);
            }
            else
            {
                // delete the appdata file to tick it next time
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            Frame parentFrame = Window.Current.Content as Frame;
            parentFrame.Navigate(typeof(Messages));

        }
    }
}
