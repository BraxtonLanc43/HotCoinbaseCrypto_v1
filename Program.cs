using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json;
using CsvHelper;
using System.IO;

namespace CryptoComparer_v1
{
    class Program
    {
        #region Other Constants
        private static string coinbase_CSVPath = "PATH.csv";
        private static string textFile_ResultsPath = "OTHERPATH.csv";
        #endregion

        #region Coinbase creds
        private static string coinbase_APIKey = "YOUR_API_KEY";
        private static string coinbase_APISecret = "YOUR_API_SECRET";
        #endregion

        static void Main(string[] args)
        {
            
            //Get Coinbase data
            List<DTO.Coinbase.GetPrice.RootObject> coinbaseMarketData = new List<DTO.Coinbase.GetPrice.RootObject>();
            DTO.Coinbase.GetPrice.RootObject coinbaseBTC_MarketData = coinbase_GetSpotPrice("BTC");
            coinbaseBTC_MarketData.data.symbol = "BTC";
            DTO.Coinbase.GetPrice.RootObject coinbaseLTC_MarketData = coinbase_GetSpotPrice("LTC");
            coinbaseLTC_MarketData.data.symbol = "LTC";
            DTO.Coinbase.GetPrice.RootObject coinbaseETH_MarketData = coinbase_GetSpotPrice("ETH");
            coinbaseETH_MarketData.data.symbol = "ETH";
            coinbaseMarketData.Add(coinbaseBTC_MarketData);
            coinbaseMarketData.Add(coinbaseLTC_MarketData);
            coinbaseMarketData.Add(coinbaseETH_MarketData);
                       
            //Store Date and Value of ATH for the main coins in a file (csv)
            List<DTO.Coinbase.DataBrief> coinbaseCSVData = getCSVCoinbaseData(coinbase_CSVPath);

            //If current value is > ATH, then set ATH
            coinbaseCSVData = updateATH(coinbaseCSVData, coinbaseMarketData);

            //At least 2 weeks removed from ATH :: preserve "coinbaseCSVData" variable for rewrite to CSV
            List<DTO.Coinbase.DataBrief> postFiltered_CoinbaseCSVData = filterTwoWeeksFromATH_Only(coinbaseCSVData);
            string createText = "";
            if (postFiltered_CoinbaseCSVData.Count == 0)
            {
                //Write that there are no hot buys. Include historical?
                createText = createTextFileContents();

                return;
            }
            else
            {
                //(Is down from 2 weeks ago, AND down from a week ago) OR (4 weeks ago was down from 5th week, 3 weeks ago was down from 4th week, 2 weeks ago was down from 3rd week, last week was up from 2nd, this week up from last week)
                List<DTO.Coinbase.DataBrief> hot_buys = rules_HotBuys(postFiltered_CoinbaseCSVData);

                //Rewrite coinbaseCSVData to CSV
                string wroteCSV = writeCSV(coinbaseCSVData);

                //Create Output for "hot buys" (or none)
                if (hot_buys.Count > 0)
                    createText = createTextFileContents(hot_buys);
                else
                    createText = createTextFileContents();

                //Write to file
                File.WriteAllText(textFile_ResultsPath, createText);
            }
        }

        #region Title: CoinMarketCap endpoints --- Status: ALL WORKING

        /*
         * @desc gets data from coinmarketcap on all currencies
         * @param null
         * @return List<DTO.CoinMarketCap.Currneices.RootObject>
         * @status WORKING
         */
        private static List<DTO.CoinMarketCap.Currencies.RootObject> coinmarketcap_100()
        {
            //Build REST client
            var client = new RestClient("https://api.coinmarketcap.com/v1/ticker/");
            var request = new RestRequest(Method.GET);
            request.AddHeader("content-type", "application/json");
            IRestResponse response = client.Execute(request);

            //Serialize into response object
            var coinmarketcap_Response = JsonConvert.DeserializeObject<List<DTO.CoinMarketCap.Currencies.RootObject>>(response.Content);

            //returnid;
            return coinmarketcap_Response;
        }

        /*
         * @desc gets data from coinmarketcap on specified currency
         * @param string currency : the currency to get data on
         * @return DTO.CoinMarketCap.Currencies.RootObject
         * @status WORKING
         */
        private static DTO.CoinMarketCap.Currencies.RootObject coinmarketcap_Single(string currency)
        {
            //Build REST client
            var client = new RestClient("https://api.coinmarketcap.com/v1/ticker/" + currency);
            var request = new RestRequest(Method.GET);
            request.AddHeader("content-type", "application/json");
            IRestResponse response = client.Execute(request);

            //Serialize into response object
            var coinmarketcap_Response = JsonConvert.DeserializeObject<List<DTO.CoinMarketCap.Currencies.RootObject>>(response.Content);

            //returnid;
            return coinmarketcap_Response[0];
        }

        /*
         * @desc gets global data on cryptocurrency market from coinmarketcap
         * @param null
         * @return DTO.CoinMarketCap.GlobalData.RootObject global response
         * @status WORKING
         */
        private static DTO.CoinMarketCap.GlobalData.RootObject coinmarketcap_Global()
        {
            //Build REST client
            var client = new RestClient("https://api.coinmarketcap.com/v1/global/");
            var request = new RestRequest(Method.GET);
            request.AddHeader("content-type", "application/json");
            IRestResponse response = client.Execute(request);

            //Serialize into response object
            var globalData_Response = JsonConvert.DeserializeObject<DTO.CoinMarketCap.GlobalData.RootObject>(response.Content);

            //returnid;
            return globalData_Response;
        }

        #endregion


        #region Title: Coinbase endpoints --- Status: ALL WORKING

        /*
         * @desc Retrieves data on the current buy price for the specified currency. Includes the 1% Coinbase Fee!
         * @param string currency : currency abbreviation to retrieve (example: "BTC")
         * @return DTO.Coinbase.GetBuyPrice.RootObject : data on the current buy price for the specified currency
         * @status WORKING
         */
        private static DTO.Coinbase.GetPrice.RootObject coinbase_GetBuyPrice(string currency)
        {
            // Build REST client
            var client = new RestClient("https://api.coinbase.com/v2/prices/" + currency + "-USD/buy");
            var request = new RestRequest(Method.GET);
            request.AddHeader("content-type", "application/json");
            request.AddHeader("Authorization", coinbase_APISecret);
            IRestResponse response = client.Execute(request);

            //Serialize into response object
            var coinbase_GetBuyResponse = JsonConvert.DeserializeObject<DTO.Coinbase.GetPrice.RootObject>(response.Content);

            //returnid;
            return coinbase_GetBuyResponse;
        }

        /*
         * @desc Retrieves data on the current sell price for the specified currency. Includes the 1% Coinbase Fee!
         * @param string currency : currency abbreviation to retrieve (example: "BTC")
         * @return DTO.Coinbase.GetPrice.RootObject : data on the current sell price for the specified currency
         * @status WORKING
         */
        private static DTO.Coinbase.GetPrice.RootObject coinbase_GetSellPrice(string currency)
        {
            // Build REST client
            var client = new RestClient("https://api.coinbase.com/v2/prices/" + currency + "-USD/sell");
            var request = new RestRequest(Method.GET);
            request.AddHeader("content-type", "application/json");
            request.AddHeader("Authorization", coinbase_APISecret);
            IRestResponse response = client.Execute(request);

            //Serialize into response object
            var coinbase_GetSellResponse = JsonConvert.DeserializeObject<DTO.Coinbase.GetPrice.RootObject>(response.Content);

            //returnid;
            return coinbase_GetSellResponse;
        }

        /*
         * @desc Retrieves data on the current spot price for the specified currency. 
         * @param string currency : currency abbreviation to retrieve (example: "BTC"), (optional) string date : historic date to get price of from that time (example: "1992-09-03")
         * @return DTO.Coinbase.GetPrice.RootObject : data on the current spot/market price for the specified currency
         * @status WORKING
         */
        private static DTO.Coinbase.GetPrice.RootObject coinbase_GetSpotPrice(string currency, string date = "none")
        {
            // Build REST client
            var client = new RestClient("https://api.coinbase.com/v2/prices/" + currency + "-USD/spot");
            if(date != "none")
                client = new RestClient("https://api.coinbase.com/v2/prices/" + currency + "-USD/spot?date=" + date);
            var request = new RestRequest(Method.GET);
            request.AddHeader("content-type", "application/json");
            request.AddHeader("Authorization", coinbase_APISecret);
            IRestResponse response = client.Execute(request);

            //Serialize into response object
            var coinbase_GetSellResponse = JsonConvert.DeserializeObject<DTO.Coinbase.GetPrice.RootObject>(response.Content);

            //returnid;
            return coinbase_GetSellResponse;
        }


        #endregion


        #region Title: Utils --- Status: 4 Working. 2 Ready for Test.

        /*
         * @desc Reads data from local CSV of Coinbase data
         * @param string path : path to the local CSV
         * @return List<DTO.Coinbase.DataBrief>
         * @status WORKING
         */
        private static List<DTO.Coinbase.DataBrief> getCSVCoinbaseData(string path)
        {
            List<DTO.Coinbase.DataBrief> dataBriefs = new List<DTO.Coinbase.DataBrief>();

            using (var sr = new StreamReader(path))
            {
                var reader = new CsvReader(sr);

                //CSVReader will now read the whole file into an enumerable
                IEnumerable<DTO.Coinbase.DataBrief> records = reader.GetRecords<DTO.Coinbase.DataBrief>();

                //serialize our objects
                dataBriefs = records.ToList<DTO.Coinbase.DataBrief>();
                
            }

            return dataBriefs;
        }

        /*
         * @desc Compares the date of the All Time High of the currency and sees if it was more than 2 weeks ago (arbitrary amount of time picked to ensure it didn't JUST experience an all time high)
         * @param List<DataBrief> briefs : list of DataBrief objects earlier obtained from CSV 
         * @return List<DTO.Coinbase.DataBrief> postFilter : dataset after 2 week ATH filtering takes place
         * @status WORKING
         */
        private static List<DTO.Coinbase.DataBrief> filterTwoWeeksFromATH_Only(List<DTO.Coinbase.DataBrief> briefs)
        {
            //initialize for return
            List<DTO.Coinbase.DataBrief> postFilter = new List<DTO.Coinbase.DataBrief>();

            //Get today's date
            var today = DateTime.Today;

            for (int i = 0; i < briefs.Count; i++)
            {
                //Get the date 
                DateTime thisDate = Convert.ToDateTime(briefs[i].ATH_Date);

                //Get two weeks ago
                DateTime twoWeeksAgo = today.AddDays(-14);

                //If > 2 weeks ago...
                if (twoWeeksAgo > thisDate)
                    postFilter.Add(briefs[i]);
            }

            return postFilter;
        }

        /*
         * @desc Updates the ATH values if need be
         * @param List<DTO.Coinbase.DataBrief> briefs, List<DTO.Coinbase.GetPrice.RootObject> cbPrices
         * @return List<DTO.Coinbase.DataBrief> postFilter : dataset after ATH values updated if applicable
         * @status WORKING
         */
        private static List<DTO.Coinbase.DataBrief> updateATH(List<DTO.Coinbase.DataBrief> briefs, List<DTO.Coinbase.GetPrice.RootObject> cbPrices)
        {
            for (int i = 0; i < briefs.Count; i++)
            {
                //Get the ATH and the symbol of the current element
                string ath = briefs[i].ATH_Amt;
                string symbol = briefs[i].Abbrev;
                string coin = briefs[i].Coin;
                string d = briefs[i].ATH_Date;
                string currentPrice = "";
                string today = DateTime.Today.ToString("d");

                //Use the symbol to find the current price from our CB data
                for (int j = 0; j < cbPrices.Count; j++)
                {
                    if(cbPrices[j].data.symbol == symbol || cbPrices[j].data.currency == coin)
                    {
                        currentPrice = cbPrices[j].data.amount;                        
                        break;
                    }
                }

                //If the currentPrice is > ATH, set the ATH
                double ath_Double = Convert.ToDouble(ath);
                double currentPrice_Double = Convert.ToDouble(currentPrice);
                if (currentPrice_Double > ath_Double)
                {
                    briefs[i].ATH_Amt = Convert.ToString(currentPrice_Double);
                    briefs[i].ATH_Date = today;
                }                    
            }


            return briefs;
        }

        /*
         * @desc Filters the options based on a made-up set of rules that would classify a coin as "hot"
         * @param List<DTO.Coinbase.DataBrief> briefs
         * @return List<DTO.Coinbase.DataBrief> hot : dataset after "hot" filtering
         * @status WORKING
         */
        private static List<DTO.Coinbase.DataBrief> rules_HotBuys(List<DTO.Coinbase.DataBrief> briefs)
        {
            //(Is down from 2 weeks ago, AND down from a week ago) OR (4 weeks ago was down from 5th week, 3 weeks ago was down from 4th week, 2 weeks ago was down from 3rd week, last week was up from 2nd, this week up from last week)

            //Initialize for return
            List<DTO.Coinbase.DataBrief> hot = new List<DTO.Coinbase.DataBrief>();

            //Get dates we'll need to use
            string date_FiveWeeksAgo = (DateTime.Today.AddDays(-35)).ToString("yyyy-MM-dd");
            string date_FourWeeksAgo = (DateTime.Today.AddDays(-28)).ToString("yyyy-MM-dd");
            string date_ThreeWeeksAgo = (DateTime.Today.AddDays(-21)).ToString("yyyy-MM-dd");
            string date_TwoWeeksAgo = (DateTime.Today.AddDays(-14)).ToString("yyyy-MM-dd");
            string date_OneWeeksAgo = (DateTime.Today.AddDays(-7)).ToString("yyyy-MM-dd");

            //For each currency...
            for (int i = 0; i < briefs.Count; i++)
            {
                //get the value at each of these dates
                DTO.Coinbase.GetPrice.RootObject getPrice_5WeeksAgo = coinbase_GetSpotPrice(briefs[i].Abbrev, date_FiveWeeksAgo);
                DTO.Coinbase.GetPrice.RootObject getPrice_4WeeksAgo = coinbase_GetSpotPrice(briefs[i].Abbrev, date_FourWeeksAgo);
                DTO.Coinbase.GetPrice.RootObject getPrice_3WeeksAgo = coinbase_GetSpotPrice(briefs[i].Abbrev, date_ThreeWeeksAgo);
                DTO.Coinbase.GetPrice.RootObject getPrice_2WeeksAgo = coinbase_GetSpotPrice(briefs[i].Abbrev, date_TwoWeeksAgo);
                DTO.Coinbase.GetPrice.RootObject getPrice_1WeeksAgo = coinbase_GetSpotPrice(briefs[i].Abbrev, date_OneWeeksAgo);
                DTO.Coinbase.GetPrice.RootObject getPrice_current = coinbase_GetSpotPrice(briefs[i].Abbrev);

                //Rule #1 :: Is down from 2 weeks ago, AND down from a week ago, AND last week's is down from 2 weeks ago
                //tldr; has been down two consecutive weeks
                bool isRule1 = false;
                if ((Convert.ToDouble(getPrice_current.data.amount) < Convert.ToDouble(getPrice_2WeeksAgo.data.amount)) && (Convert.ToDouble(getPrice_current.data.amount) < Convert.ToDouble(getPrice_1WeeksAgo.data.amount)) && (Convert.ToDouble(getPrice_1WeeksAgo.data.amount) < Convert.ToDouble(getPrice_2WeeksAgo.data.amount)))
                    isRule1 = true;

                //Rule #2 :: (4 weeks ago was down from 5th week, 3 weeks ago was down from 4th week, 2 weeks ago was down from 3rd week, last week was up from 2nd, this week up from last week)
                //tldr; was down for 3 consecutive weeks, and has now been up 2 weeks in a row so may be hot
                bool isRule2 = false;
                if ((Convert.ToDouble(getPrice_4WeeksAgo.data.amount) < Convert.ToDouble(getPrice_5WeeksAgo.data.amount)) && (Convert.ToDouble(getPrice_3WeeksAgo.data.amount) < Convert.ToDouble(getPrice_4WeeksAgo.data.amount)) && (Convert.ToDouble(getPrice_2WeeksAgo.data.amount) < Convert.ToDouble(getPrice_3WeeksAgo.data.amount)) && (Convert.ToDouble(getPrice_1WeeksAgo.data.amount) > Convert.ToDouble(getPrice_2WeeksAgo.data.amount)) && (Convert.ToDouble(getPrice_1WeeksAgo.data.amount) < Convert.ToDouble(getPrice_current.data.amount)))
                    isRule2 = true;

                //Pass all rules?
                if (isRule1 && isRule2)
                    hot.Add(briefs[i]);
            }

            return hot;
        }

        /*
         * @desc Writes to a CSV to update the data
         * @param List<DTO.Coinbase.DataBrief> briefs
         * @return string : Success or error message
         * @status WORKING
         */
        private static string writeCSV(List<DTO.Coinbase.DataBrief> toWrite)
        {
            try
            {
                using (TextWriter writer = new StreamWriter(coinbase_CSVPath))
                {
                    var csv = new CsvWriter(writer);
                    csv.WriteRecords(toWrite); // where values implements IEnumerable
                }
            } 
            catch(Exception e)
            {
                return e.Message.ToString();
            }

            return "Success";
        }

        /*
         * @desc Writes "Hot Buys" to a text file
         * @param List<DTO.Coinbase.DataBrief> data
         * @return string : contents to write to the text file
         * @status Ready to Test (but should mimic the working one below)
         */
        private static string createTextFileContents(List<DTO.Coinbase.DataBrief> data)
        {
            string contents = "";

            //Read current file values
            string readText = File.ReadAllText(textFile_ResultsPath);
            contents += readText;
            contents += Environment.NewLine;
            contents += "------------------------------";
            contents += Environment.NewLine;
            contents += DateTime.Now.ToString();
            contents += Environment.NewLine;

            //Create contents based on whether any values to add or not
            for (int i = 0; i < data.Count; i++)
            {
                if(i == 0)
                    contents += "Hot Buy: " + data[i].Coin.ToString();
                else
                {
                    contents += Environment.NewLine;
                    contents += "Hot Buy: " + data[i].Coin.ToString();
                }
            }

            return contents;
        }

        /*
         * @desc Writes that there are no "Hot Buys" to a text file
         * @param null
         * @return string : contents to write to the text file
         * @status WORKING
         */
        private static string createTextFileContents()
        {
            string contents = "";

            //Read current file values
            string readText = File.ReadAllText(textFile_ResultsPath);
            contents += readText;
            contents += Environment.NewLine;
            contents += "------------------------------";
            contents += Environment.NewLine;
            contents += DateTime.Now.ToString();
            contents += Environment.NewLine;
            contents += "No hot buys today";

            return contents;

        }
        #endregion

    }
}
