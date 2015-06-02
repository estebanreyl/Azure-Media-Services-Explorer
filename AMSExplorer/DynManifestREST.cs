﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;

using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using System.Globalization;
using Newtonsoft.Json;

using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;



using System.Web;





namespace AMSExplorer
{

    public class AcsToken
    {
        public string token_type
        {
            get;
            set;
        }

        public string access_token
        {
            get;
            set;
        }

        public int expires_in
        {
            get;
            set;
        }

        public string scope
        {
            get;
            set;
        }
    }


    public class MediaServiceContext
    {
        private const string acsEndpoint = "https://wamsprodglobal001acs.accesscontrol.windows.net/v2/OAuth2-13";

        private const string acsRequestBodyFormat = "grant_type=client_credentials&client_id={0}&client_secret={1}&scope=urn%3aWindowsAzureMediaServices";

        private string _accountName;

        private string _accountKey;

        private string _accessToken;

        private DateTime _accessTokenExpiry;

        private string _wamsEndpoint = "https://media.windows.net/";

        /// <summary>
        /// Creates a new instance of <see cref="MediaServiceContext"/>
        /// </summary>
        /// <param name="accountName">
        /// Media service account name.
        /// </param>
        /// <param name="accountKey">
        /// Media service account key.
        /// </param>
        public MediaServiceContext(string accountName, string accountKey)
        {
            this._accountName = accountName;
            this._accountKey = accountKey;
        }

        /// <summary>
        /// Gets the access token. If access token is not yet fetched or the access token has expired,
        /// it gets a new access token.
        /// </summary>
        public string AccessToken
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_accessToken) || _accessTokenExpiry < DateTime.UtcNow)
                {
                    var tuple = FetchAccessToken();
                    _accessToken = tuple.Item1;
                    _accessTokenExpiry = tuple.Item2;
                }
                return _accessToken;
            }
        }

        /// <summary>
        /// Gets the endpoint for making REST API calls.
        /// </summary>
        public string WamsEndpoint
        {
            get
            {
                return _wamsEndpoint;
            }
        }

        /// <summary>
        /// This function makes the web request and gets the access token.
        /// </summary>
        /// <returns>
        /// <see cref="System.Tuple"/> containing 2 items - 
        /// 1. The access token. 
        /// 2. Token expiry date/time.
        /// </returns>
        private Tuple<string, DateTime> FetchAccessToken()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(acsEndpoint);
            request.Method = HttpVerbs.Post;
            string requestBody = string.Format(CultureInfo.InvariantCulture, acsRequestBodyFormat, _accountName, HttpUtility.UrlEncode(_accountKey));
            request.ContentLength = Encoding.UTF8.GetByteCount(requestBody);
            request.ContentType = "application/x-www-form-urlencoded";
            using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(requestBody);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream(), true))
                {
                    var returnBody = streamReader.ReadToEnd();
                    var acsToken = JsonConvert.DeserializeObject<AcsToken>(returnBody);
                    return new Tuple<string, DateTime>(acsToken.access_token, DateTime.UtcNow.AddSeconds(acsToken.expires_in));
                }
            }
        }

        /// <summary>
        /// This function checks if we need to redirect all WAMS requests.
        /// </summary>
        public void CheckForRedirection()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(WamsEndpoint);
            request.AllowAutoRedirect = false;
            request.Headers.Add(RequestHeaders.XMsVersion, RequestHeaderValues.XMsVersion);
            request.Headers.Add(RequestHeaders.Authorization, string.Format(CultureInfo.InvariantCulture, RequestHeaderValues.Authorization, AccessToken));
            request.Method = HttpVerbs.Get;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.Moved || response.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    string newLocation = response.Headers["Location"];
                    if (!newLocation.Equals(_wamsEndpoint))
                    {
                        _wamsEndpoint = newLocation;
                        _accessToken = string.Empty;//So that we can force to get a new access token.
                        _accessTokenExpiry = DateTime.MinValue;
                    }
                }
            }
        }

       
    }



    internal static class RequestHeaderValues
    {
        /// <summary>
        /// DataServiceVersion request header (3.0)
        /// </summary>
        internal const string DataServiceVersion = "3.0";

        /// <summary>
        /// MaxDataServiceVersion request header (3.0)
        /// </summary>
        internal const string MaxDataServiceVersion = "3.0";

        /// <summary>
        /// x-ms-version request header (2.0)
        /// </summary>
        internal const string XMsVersion = "2.11";

        /// <summary>
        /// Authorization request header format
        /// </summary>
        internal const string Authorization = "Bearer {0}";

        

        /// <summary>
        /// Authorization request header format
        /// </summary>
        internal const string ZeroID = "00000000-0000-0000-0000-000000000000";


    }

    /// <summary>
    /// Request header names.
    /// </summary>
    internal static class RequestHeaders
    {
        /// <summary>
        /// DataServiceVersion request header
        /// </summary>
        internal const string DataServiceVersion = "DataServiceVersion";

        /// <summary>
        /// MaxDataServiceVersion request header
        /// </summary>
        internal const string MaxDataServiceVersion = "MaxDataServiceVersion";

        /// <summary>
        /// x-ms-version request header
        /// </summary>
        internal const string XMsVersion = "x-ms-version";

        /// <summary>
        /// Authorization request header
        /// </summary>
        internal const string Authorization = "Authorization";

        /// <summary>
        /// x-ms-version request header
        /// </summary>
        internal const string XMsClientRequestId = "x-ms-client-request-id";
       
    }

    /// <summary>
    /// HTTP Verbs
    /// </summary>
    internal static class HttpVerbs
    {
        /// <summary>
        /// POST HTTP verb
        /// </summary>
        internal const string Post = "POST";

        /// <summary>
        /// GET HTTP verb
        /// </summary>
        internal const string Get = "GET";

        /// <summary>
        /// MERGE HTTP verb
        /// </summary>
        internal const string Merge = "MERGE";

        /// <summary>
        /// DELETE HTTP verb
        /// </summary>
        internal const string Delete = "DELETE";
    }

    internal static class RequestContentType
    {
        internal const string Json = "application/json";

        internal const string Atom = "application/atom+xml";
    }

     [DataContract]
    class IFilterPresentationTimeRange
    {
        [DataMember]
        public string StartTimestamp
        {
            get;
            set;
        }

        [DataMember]
        public string EndTimestamp
        {
            get;
            set;
        }

        [DataMember]
        public string PresentationWindowDuration
        {
            get;
            set;
        }

        [DataMember]
        public string LiveBackoffDuration
        {
            get;
            set;
        }

        [DataMember]
        public string Timescale
        {
            get;
            set;
        }
    }

     [DataContract]
     class IFilterTrackSelect

    {
        [DataMember]
        public List<FilterTrackPropertyCondition> PropertyConditions
        {
            get;
            set;
        }

    }

    public sealed class FilterProperty
    {
        public static readonly string Type = "Type";
        public static readonly string Name = "Name";
        public static readonly string Language = "Language";
        public static readonly string FourCC = "FourCC";
        public static readonly string Bitrate = "Bitrate";
    }

    public sealed class FilterPropertyTypeValue
    {
        public static readonly string video = "video";
        public static readonly string audio = "audio";
        public static readonly string text = "text";
    }

    public sealed class IOperator
    {
        public static readonly string Equal = "Equal";
        public static readonly string notEqual = "notEqual";
    }
    [DataContract]
    class FilterTrackPropertyCondition
    {
          [DataMember]
        public string Property
        {
            get;
            set;
        }

          [DataMember]
        public string Value
        {
            get;
            set;
        }

          [DataMember]
        public string Operator
        {
            get;
            set;
        }

    }


    class Filter
    {

        public string Name
        {
            get;
            set;
        }


        public IFilterPresentationTimeRange PresentationTimeRange
        {
            get;
            set;
        }


        public List<IFilterTrackSelect> Tracks
        {
            get;
            set;
        }
    }

    [DataContract]
    class DynamicFilter
    {
         private readonly MediaServiceContext _context;

        internal DynamicFilter(MediaServiceContext context)
        {
            _context = context;
        }


        // http://gauravmantri.com/2012/10/10/windows-azure-media-service-part-iii-managing-assets-via-rest-api/
        // https://azure.microsoft.com/en-us/documentation/articles/media-services-rest-dynamic-manifest/
        /// <summary>
        /// Friendly name for asset.
        /// </summary>
        /// 
        [DataMember]
        public string Name
        {
            get;
            set;
        }

        [DataMember]
        public IFilterPresentationTimeRange PresentationTimeRange
        {
            get;
            set;
        }

        [DataMember]
        public List<IFilterTrackSelect> Tracks
        {
            get;
            set;
        }


        public bool Create()  // return true if success
        {
           bool Success = false;
            
            var serializer = new JavaScriptSerializer();
            var serializedResult = serializer.Serialize(this);
          
            var requestBody = serializedResult;
          
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format(CultureInfo.InvariantCulture, "{0}Filters/", _context.WamsEndpoint));
            request.Method = HttpVerbs.Post;
            request.ContentType = RequestContentType.Json;
            request.Accept = RequestContentType.Json;
            request.Headers.Add(RequestHeaders.XMsVersion, RequestHeaderValues.XMsVersion);
            request.Headers.Add(RequestHeaders.Authorization, string.Format(CultureInfo.InvariantCulture, RequestHeaderValues.Authorization, _context.AccessToken));
            request.Headers.Add(RequestHeaders.DataServiceVersion, RequestHeaderValues.DataServiceVersion);
            request.Headers.Add(RequestHeaders.MaxDataServiceVersion, RequestHeaderValues.MaxDataServiceVersion);
            request.Headers.Add(RequestHeaders.XMsClientRequestId, RequestHeaderValues.ZeroID);
            request.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(requestBody);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.Created)
                {
                     Success = true;
                }
                
            }
            return Success;
        }

        public void List()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format(CultureInfo.InvariantCulture, "{0}Filters", _context.WamsEndpoint));
            request.Method = HttpVerbs.Get;
            request.ContentType = RequestContentType.Json;
            request.Accept = RequestContentType.Json;
            request.Headers.Add(RequestHeaders.XMsVersion, RequestHeaderValues.XMsVersion);
            request.Headers.Add(RequestHeaders.Authorization, string.Format(CultureInfo.InvariantCulture, RequestHeaderValues.Authorization, _context.AccessToken));
            request.Headers.Add(RequestHeaders.DataServiceVersion, RequestHeaderValues.DataServiceVersion);
            request.Headers.Add(RequestHeaders.MaxDataServiceVersion, RequestHeaderValues.MaxDataServiceVersion);

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream(), true))
                {
                    var returnBody = streamReader.ReadToEnd();

                    JObject responseJsonObject = JObject.Parse(returnBody);
                    var value = responseJsonObject["value"];

                    var serializer = new JavaScriptSerializer();
                    var serializedResult = serializer.Deserialize(value.ToString(), typeof(List<Filter>));
                }
            }
        }
    }
}