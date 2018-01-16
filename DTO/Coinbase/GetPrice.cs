using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoComparer_v1.DTO.Coinbase
{
    class GetPrice
    {
        public class Data
        {
            public string amount { get; set; }
            public string currency { get; set; }
            public string symbol { get; set; }
        }

        public class RootObject
        {
            public Data data { get; set; }
        }
    }
}
