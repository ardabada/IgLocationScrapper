using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace IgHashtagSearch
{
    public class LocationGrabber
    {
        public LocationGrabber(string id, string locationName, DateTime destinationDate)
        {
            LocationId = id;
            Location = id + "/" + locationName + "/";
            Destination = destinationDate.Date;
            //getInitParams();
        }

        HttpWebRequest request;
        HttpWebResponse response;

        /// <summary>
        /// Value required to calculate X-Instagram-GIS header value
        /// </summary>
        string rhx_gis = string.Empty;
        /// <summary>
        /// Initial CSRF Token
        /// </summary>
        string csrf_token = string.Empty;

        string query_hash = string.Empty;

        readonly string baseUrl = "https://www.instagram.com";
        readonly string graphql_path = "/graphql/query/?query_hash={0}&variables={1}";

        readonly string locationUrl = "/explore/locations/";

        bool hasNextPage = true;
        string end_cursor = string.Empty;
        string last_saved_cursor = string.Empty; //"1808392907124963986";

        Dictionary<string, string> cookies = new Dictionary<string, string>();
        DateTime lastDate = DateTime.MaxValue;
        public DateTime Destination { get; private set; }
        
        public string LocationId { get; private set; }
        public string Location { get; private set; }

        public event EventHandler OnDownloadCountChanged;
        public event EventHandler OnNewIdentityRequired;

        private int _downloads = 0;
        public int CurrentDownloadCount
        {
            get { return _downloads; }
            set
            {
                _downloads = value;
                OnDownloadCountChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string EndCursor
        {
            get { return end_cursor; }
        }

        public void Init(string lastCursor = "")
        {
            last_saved_cursor = lastCursor;
            getInitParams();
        }

        private void initRequest(string url)
        {
            request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.98 Safari/537.36";
            request.Headers.Add("Accept-Language", "ro,en-US;q=0.9,en;q=0.8,ru;q=0.7");
            request.Accept = "*/*";
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.SetCookies(new Uri(baseUrl), string.Join(",", cookies.Select(x => x.Key + "=" + x.Value)));
        }
        private bool getResponse()
        {
            try
            {
                response = request.GetResponse() as HttpWebResponse;
                getCookies(response.Headers);
                return true;
            }
            catch (WebException ex)
            {
                getCookies(ex.Response.Headers);
                return false;
            }
            catch { return false; }
        }
        private string getResponseString()
        {
            if (getResponse())
                return new StreamReader(response.GetResponseStream()).ReadToEnd();
            return string.Empty;
        }
        void getCookies(WebHeaderCollection headers)
        {
            string[] accept = new string[] { "rur", "urlgen", "csrftoken" };
            for (int i = 0; i < headers.Count; i++)
            {
                string name = headers.GetKey(i);
                if (name != "Set-Cookie")
                    continue;
                string value = headers.Get(i);
                foreach (var singleCookie in value.Split(','))
                {
                    Match match = Regex.Match(singleCookie, "(.+?)=(.+?);");
                    if (match.Captures.Count == 0)
                        continue;
                    string cname = match.Groups[1].ToString();
                    string cvalue = match.Groups[2].ToString();
                    if (accept.Contains(cname) && !string.IsNullOrEmpty(cvalue))
                    {
                        if (cookies.ContainsKey(cname))
                            cookies[cname] = cvalue;
                        else cookies.Add(cname, cvalue);
                    }
                }
            }
        }

        private void getInitParams()
        {
            initRequest(baseUrl + locationUrl + Location);
            string html = getResponseString();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            //get first script element in body
            string js = doc.DocumentNode.SelectSingleNode("//body/script").InnerText;
            //remove "window._sharedData ="
            js = js.Remove(0, js.IndexOf('{')).Trim();
            //remove last ; in json string
            js = js.Remove(js.Length - 1);
            //convert js script part to json
            JToken json = JToken.Parse(js);
            csrf_token = json["config"]["csrf_token"].ToString();
            rhx_gis = json["rhx_gis"].ToString();

            //retreive query_hash
            //find .js file of page container
            var linkNode = doc.DocumentNode.SelectSingleNode("//link[@rel='preload']");
            string link = linkNode.GetAttributeValue("href", string.Empty);
            initRequest(baseUrl + link);
            //retreive js file
            js = getResponseString();
            //find where first queryId is in downloaded js file
            string q = "queryId:\"";
            int queryIdIndex = js.IndexOf(q);
            //remove string 
            js = js.Substring(queryIdIndex + q.Length);
            int hashEnd = js.IndexOf("\"");
            query_hash = js.Remove(hashEnd);

            if (string.IsNullOrEmpty(last_saved_cursor))
                parseNodes(json["entry_data"]["LocationsPage"].First["graphql"]["location"]["edge_location_to_media"]);

            if (!string.IsNullOrEmpty(last_saved_cursor))
                end_cursor = last_saved_cursor;
        }

        public void Start()
        {
            try
            {
                DateTime last = lastDate.Date;
                DateTime final = Destination.Date;
                while (last >= final)
                {
                    if (nextGraphRequest(0))
                    {
                        Console.WriteLine("Last: " + lastDate.ToShortDateString());
                        Thread.Sleep(TimeSpan.FromMilliseconds(300));
                    }
                    else
                    {
                        Console.WriteLine("Error while retreiving data. Last cursor: " + end_cursor);
                        return;
                    }
                }
            }
            catch
            {
                OnNewIdentityRequired?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool nextGraphRequest(int retry)
        {
            if (retry > 10)
            {
                throw new Exception("New identity required");
                Console.WriteLine("Too much retries for same get request.");
                return false;
            }

            string raw_variants = "{\"id\":\"" + LocationId + "\",\"first\":12,\"after\":\"" + end_cursor + "\"}";
            string gis = calculateGisHeader(raw_variants);
            string vars = Uri.EscapeDataString(raw_variants);

            string url = string.Format(baseUrl + graphql_path, query_hash, vars);
            initRequest(url);
            request.Headers.Add("X-Instagram-GIS", gis);
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            if (getResponse() == false)
            {
                retry++;
                int minutes = 10;//2 * retry;
                Console.WriteLine("Error. Retaking in " + minutes + " minute" + (minutes == 1 ? string.Empty : "s"));
                Thread.Sleep(TimeSpan.FromMinutes(minutes));
                return nextGraphRequest(retry);
            }

            string jsonResponse = new StreamReader(response.GetResponseStream()).ReadToEnd();
            JToken json = JToken.Parse(jsonResponse);
            parseNodes(json["data"]["location"]["edge_location_to_media"]);
            return true;
        }

        private string calculateGisHeader(string query)
        {
            return calculateMD5Hash(rhx_gis + ":" + query);
        }

        private string calculateMD5Hash(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }

        private void parseNodes(JToken source)
        {
            hasNextPage = source["page_info"]["has_next_page"].ToObject<bool>();
            end_cursor = source["page_info"]["end_cursor"].ToString();

            List<Record> records = new List<Record>();
            foreach (var node in source["edges"])
            {
                Record r = new Record();
                r.Date = node["node"]["taken_at_timestamp"].ToObject<double>();
                r.Name = node["node"]["shortcode"].ToString();
                r.Url = node["node"]["display_url"].ToString();
                r.Video = node["node"]["is_video"].ToObject<bool>();
                r.Cursor = end_cursor;
                downloadMedia(r);
                records.Add(r);
            }

            lastDate = records.Last().DateValue;
            File.AppendAllLines("data.csv", records.ConvertAll(x => x.ToString()));
        }


        void downloadMedia(Record r)
        {
            CurrentDownloadCount++;
            string path = "media";
            checkDir(path);
            path = Path.Combine(path, LocationId);
            checkDir(path);
            path = Path.Combine(path, r.DateValue.Year.ToString("0000") + r.DateValue.Month.ToString("00") + r.DateValue.Day.ToString("00"));
            checkDir(path);
            path = Path.Combine(path, r.Name + getExtensionFromUrl(r.Url));
            using (WebClient c = new WebClient())
            {
                c.DownloadFileCompleted += (s, e) => CurrentDownloadCount--;
                CurrentDownloadCount++;
                c.DownloadFileAsync(new Uri(r.Url), path);
            }
        }

        void checkDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        string getExtensionFromUrl(string url)
        {
            string[] parts1 = url.Split('/');
            string[] parts2 = parts1[parts1.Length - 1].Split('?');
            string filename = parts2[0];
            return Path.GetExtension(filename);
        }
    }
}
