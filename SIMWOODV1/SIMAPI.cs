using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;
using System.Diagnostics;
using Windows.UI.Xaml;

namespace SIMWOODV1
{
    // class to support the use of the API
    class SIMAPI
    {
        // readonly classes for storing the creds
        private readonly string Api_Username;
        private readonly string Api_Password;
        private readonly string Api_Account;
        private readonly string Api_Mobile;

        // constants
        private const string API_Root = "https://api.simwood.com";

        // holder for the http client
        // create a client to connect
        private readonly HttpClient HttpClient;

        // initialisation parameter
        public SIMAPI(string user, string pass, string account, string mobile)
        {
            // set the readonly parameters
            Api_Username = user;
            Api_Password = pass;
            Api_Account = account;
            Api_Mobile = mobile;

            // initialise a client in the global handler
            HttpClient = new HttpClient();

            //specify to use TLS 1 as default connection
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            // configure the Auth
            var byteArray = Encoding.ASCII.GetBytes(Api_Username + ":" + Api_Password);
            HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        // check the API key was appropriate
        public async Task<bool> CheckAuthAsync()
        {
            // we will check for unopened files on an account (harmless)
            string API_Call = API_Root + "/v3/files/" + Api_Account;

            // get the response
            HttpResponseMessage response = await HttpClient.GetAsync(API_Call);
           
            // check if we were not authed
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return false;
            }
            else if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // not sure what happened
            return false;
            
        }

        // access the get time parameter
        public async Task<string> GetTimeAsync()
        {
            return await GetRequestAsync("/v3/tools/time");
        }

        // access the get time parameter
        public async Task<string> GetIPAsync()
        {
            return await GetRequestAsync("/v3/tools/myip");
        }

        // SEND AN SMS YA BOIII
        public async Task<string> SendSMSAsync(string from, string to, string message)
        {
            // lets format the target
            string path = API_Root + "/v3/messaging/" + Api_Account + "/sms";

            // create the text message
            SMSoutbound outbound = new SMSoutbound
            {
                from = from,
                to = to,
                message = message
            };

            // serialise it into JSON
            string output = JsonConvert.SerializeObject(outbound);
            Debug.WriteLine(output);

            // lets send it
            string response = await PostRequestAsync(path, output);
            Debug.Write(response);

            if (response == "Failed") return "Failed";

            return "Success";
        }

        // perform a basic get request
        private async Task<string> GetRequestAsync(string path)
        {
            // define the call
            string API_Call = API_Root + path;

            // get the response
            HttpResponseMessage response = await HttpClient.GetAsync(API_Call);

            // if it was successful
            if (response.IsSuccessStatusCode)
            {
                // return the reponse as a string
                return await response.Content.ReadAsStringAsync();
            }

            return "Failed";
        }

        // perform a basic post request
        private async Task<string> PostRequestAsync(string path, string body)
        {
            // define the call
            string API_Call = API_Root + path;

            // set the post body
            StringContent content = new StringContent(body);

            // get the response
            HttpResponseMessage response = await HttpClient.PostAsync(API_Call, content);

            // if it was successful
            if (response.IsSuccessStatusCode)
            {
                // return the reponse as a string
                return await response.Content.ReadAsStringAsync();
            }

            return "Failed";
        }

    }
}
