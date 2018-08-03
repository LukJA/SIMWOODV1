using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIMWOODV1
{
    class SMSoutbound
    {
        // required
        public string to;
        public string from;
        public string message;
        // optional
        public int flash = 0;
        public int replace = 0;
        public int concat = 2;
    }

    class SMSoutboutResp
    {
        // reponse
        public string id;
    }

    class SMSdata
    {
        // data returned from an inbound
        public string time;
        public string origintor;
        public string destination;
        public string message;
        public int length;
    }

    class SMSinboundBETA
    {
        public string app;
        public string id;
        public SMSdata data;
    }
}
