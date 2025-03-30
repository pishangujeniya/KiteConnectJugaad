﻿using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CsvHelper;
using KiteConnect;
using Newtonsoft.Json.Linq;
using DataException = KiteConnect.DataException;
using Kite = KiteConnectJugaad.Sdk.Kite;

namespace KiteConnectJugaad
{
    /// <summary>
    ///     This class helps to convert the API calls from APIKey to Cookie based
    /// </summary>
    public class KiteJugaad : Kite
    {
        /// <summary>
        ///     Keeps the last received enctoken cookie value.
        /// </summary>
        private string _encToken = string.Empty;

        /// 1. Original Kite class of the official library uses _root variable for API domain.
        /// 2. We needed to change _root to browser based url, which is done in constructor.
        /// 3. We also need to concatenate a patch to the _root url, which is done in constructor.
        /// 4. We need to replace the headers with browser based headers which is overriden in AddExtraHeaders method.
        /// 5. We need to first login and then two-factor authentication.
        /// 6. On success of both we need to read the cookie value of enctoken name and then after every request should have that token in the Authorization header.
        /// 7. The _request method is overriden with one logic where the every response of it should update the value of _encToken with the last received response value.
        /// <summary>
        ///     Stores the User ID.
        /// </summary>
        private string _userId = string.Empty;

        /// <summary>
        ///     Default constructor, which sets the browser based _root url with a concatenation patch
        /// </summary>
        public KiteJugaad() : base(string.Empty)
        {
            this._root = JugaadConstants.KiteBrowserApiRoot;
        }

        /// <summary>
        ///     A simpler way to detect the session is active or not.
        /// </summary>
        public bool IsAuthorized => !string.IsNullOrWhiteSpace(this._userId) && !string.IsNullOrWhiteSpace(this._encToken);

        /// <summary>
        ///     Fully functional password based login flow including two factor authentication.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="password"></param>
        /// <param name="appCode"></param>
        /// <exception cref="GeneralException"></exception>
        /// <exception cref="TokenException"></exception>
        public async Task<bool> Login(string userId, string password, string appCode)
        {
            this._userId = userId;

            string loginUrl = "https://kite.zerodha.com/api/login";

            // Create form data
            Dictionary<string, string> postParameters = new() { { "user_id", userId }, { "password", password } };

            // Create HTTP request
            HttpRequestMessage loginRequest = new(HttpMethod.Post, loginUrl) { Content = new FormUrlEncodedContent(postParameters) };

            // Add headers
            this.AddExtraHeaders(ref loginRequest);

            // Send request
            using HttpClient httpClient = new();
            using HttpResponseMessage loginResponse = await httpClient.SendAsync(loginRequest);

            // Read response
            string loginResponseContent = await loginResponse.Content.ReadAsStringAsync();
            JObject loginResponseToken = JObject.Parse(loginResponseContent);

            string? loginResponseStatus = (string?)loginResponseToken.SelectToken("status");
            if (!string.IsNullOrWhiteSpace(loginResponseStatus) && loginResponseStatus.ToLower().Equals("success"))
            {
                string? loginResponseRequestId = (string?)loginResponseToken.SelectToken("data")?.SelectToken("request_id");
                if (!string.IsNullOrWhiteSpace(loginResponseRequestId))
                {
                    return await TwoFactorAuthentication(loginResponseRequestId, appCode);
                }

                throw new GeneralException("request id not found in the login attempt", loginResponse.StatusCode);
            }

            throw new TokenException("login attempt failed", loginResponse.StatusCode);

            async Task<bool> TwoFactorAuthentication(string requestId, string appCode)
            {
                string twoFactorAuthenticationUrl = "https://kite.zerodha.com/api/twofa";

                // Create form data
                Dictionary<string, string> postParameters = new() { { "user_id", userId }, { "request_id", requestId }, { "twofa_type", "app_code" }, { "twofa_value", appCode } };

                // Create HTTP request
                HttpRequestMessage twoFaRequest = new(HttpMethod.Post, twoFactorAuthenticationUrl) { Content = new FormUrlEncodedContent(postParameters) };

                // Add headers
                this.AddExtraHeaders(ref twoFaRequest);

                // Send request
                using HttpResponseMessage twoFaResponse = await httpClient.SendAsync(twoFaRequest);

                // Read response
                string responseContent = await twoFaResponse.Content.ReadAsStringAsync();
                this.SetEncTokenIfReceived(twoFaResponse);

                JObject twoFaToken = JObject.Parse(responseContent);

                string? twoFaResponseStatus = (string?)twoFaToken.SelectToken("status");
                if (!string.IsNullOrWhiteSpace(twoFaResponseStatus) && twoFaResponseStatus.ToLower().Equals("success"))
                {
                    return true;
                }

                throw new TokenException("app code verification failed", twoFaResponse.StatusCode);
            }
        }

        /// <summary>
        ///     A method which will extract the enctoken from the cookies and set it to the _encToken variable
        /// </summary>
        /// <param name="response"></param>
        private void SetEncTokenIfReceived(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookieValues))
            {
                string? encTokenCookie = cookieValues.FirstOrDefault(x => x.StartsWith("enctoken="));
                if (!string.IsNullOrWhiteSpace(encTokenCookie))
                {
                    this._encToken = encTokenCookie.Split(';')[0].Substring("enctoken=".Length);
                }
            }
        }

        /// <summary>
        ///     Adds the browser based extra custom headers needed for the requests
        /// </summary>
        /// <param name="Req"></param>
        public override void AddExtraHeaders(ref HttpRequestMessage Req)
        {
            base.AddExtraHeaders(ref Req);

            // Set User-Agent (replaces if exists)
            Req.Headers.UserAgent.Clear();
            Req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36");

            // Set Accept header (replaces if exists)
            Req.Headers.Accept.Clear();
            Req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");

            // Set Accept-Language header (replaces if exists)
            Req.Headers.AcceptLanguage.Clear();
            Req.Headers.AcceptLanguage.ParseAdd("en-GB,en-US;q=0.9,en;q=0.8");

            // Add custom headers (using TryAddWithoutValidation to avoid exceptions if header exists)
            Req.Headers.TryAddWithoutValidation("X-Kite-Version", "3.0.6");
            Req.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
            Req.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
            Req.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");

            // Set Referrer (replaces if exists)
            Req.Headers.Referrer = new Uri("https://kite.zerodha.com/dashboard");

            // Add user ID if available
            if (!string.IsNullOrWhiteSpace(this._userId))
            {
                Req.Headers.TryAddWithoutValidation("x-kite-userid", this._userId);
            }

            // Add authorization token if available (replaces if exists)
            if (!string.IsNullOrWhiteSpace(this._encToken))
            {
                Req.Headers.Authorization = new AuthenticationHeaderValue("enctoken", this._encToken);
            }
        }

        /// <summary>
        ///     Overriden method of request where only the value of last received enctoken cookie is get and stored in the
        ///     variable.
        /// </summary>
        /// <param name="Route"></param>
        /// <param name="Method"></param>
        /// <param name="Params"></param>
        /// <param name="QueryParams"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        /// <exception cref="GeneralException"></exception>
        /// <exception cref="TokenException"></exception>
        /// <exception cref="PermissionException"></exception>
        /// <exception cref="OrderException"></exception>
        /// <exception cref="InputException"></exception>
        /// <exception cref="KiteConnect.DataException"></exception>
        /// <exception cref="NetworkException"></exception>
        public override async Task<object> Request(string Route, string Method, dynamic Params = null, Dictionary<string, dynamic> QueryParams = null, bool json = false)
        {
            string route = this._root + this._routes[Route];

            if (Params is null)
            {
                Params = new Dictionary<string, dynamic>();
            }

            if (QueryParams is null)
            {
                QueryParams = new Dictionary<string, dynamic>();
            }

            if (route.Contains("{") && !json)
            {
                Dictionary<string, dynamic> routeParams = (Params as Dictionary<string, dynamic>).ToDictionary(entry => entry.Key, entry => entry.Value);

                foreach (KeyValuePair<string, dynamic> item in routeParams)
                {
                    if (route.Contains("{" + item.Key + "}"))
                    {
                        route = route.Replace("{" + item.Key + "}", (string)item.Value);
                        Params.Remove(item.Key);
                    }
                }
            }

            using HttpClient httpClient = new();
            HttpRequestMessage request;
            HttpResponseMessage response;

            if (Method == "POST" || Method == "PUT")
            {
                string url = route;
                if (QueryParams.Count > 0)
                {
                    url += "?" + string.Join("&", QueryParams.Select(x => Utils.BuildParam(x.Key, x.Value)));
                }

                string requestBody = json
                    ? Utils.JsonSerialize(Params)
                    : string.Join("&", (Params as Dictionary<string, dynamic>).Select(x => Utils.BuildParam(x.Key, x.Value)));

                IEnumerable<KeyValuePair<string, string>> formData = (Params as Dictionary<string, dynamic> ?? new Dictionary<string, dynamic>())
                    .Select(x => new KeyValuePair<string, string>(x.Key, x.Value?.ToString() ?? string.Empty));

                request = new HttpRequestMessage(new HttpMethod(Method), url)
                {
                    Content = json
                        ? new StringContent(requestBody, Encoding.UTF8, "application/json")
                        : new FormUrlEncodedContent(formData)
                };

                if (this._enableLogging)
                {
                    Console.WriteLine("DEBUG: " + Method + " " + url + "\n" + requestBody);
                }
            }
            else
            {
                string url = route;
                Dictionary<string, dynamic> allParams = new();

                // merge both params
                foreach (KeyValuePair<string, dynamic> item in QueryParams)
                {
                    allParams[item.Key] = item.Value;
                }

                foreach (KeyValuePair<string, dynamic> item in Params)
                {
                    allParams[item.Key] = item.Value;
                }

                // build final url
                if (allParams.Count > 0)
                {
                    url += "?" + string.Join("&", allParams.Select(x => Utils.BuildParam(x.Key, x.Value)));
                }

                request = new HttpRequestMessage(new HttpMethod(Method), url);
                if (this._enableLogging)
                {
                    Console.WriteLine("DEBUG: " + Method + " " + url);
                }
            }

            // Add headers
            this.AddExtraHeaders(ref request);

            try
            {
                response = await httpClient.SendAsync(request);
                this.SetEncTokenIfReceived(response);
            }
            catch (HttpRequestException e)
            {
                throw new NetworkException(e.Message, HttpStatusCode.InternalServerError);
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            if (this._enableLogging)
            {
                Console.WriteLine("DEBUG: " + (int)response.StatusCode + " " + responseContent + "\n");
            }

            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                Dictionary<string, dynamic>? responseDictionary = Utils.JsonDeserialize(responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorType = "GeneralException";
                    string message = "";

                    if (responseDictionary.ContainsKey("error_type"))
                    {
                        errorType = responseDictionary["error_type"];
                    }

                    if (responseDictionary.ContainsKey("message"))
                    {
                        message = responseDictionary["message"];
                    }

                    switch (errorType)
                    {
                        case "GeneralException": throw new GeneralException(message, response.StatusCode);
                        case "TokenException":
                            {
                                this._sessionHook?.Invoke();
                                throw new TokenException(message, response.StatusCode);
                            }
                        case "PermissionException": throw new PermissionException(message, response.StatusCode);
                        case "OrderException": throw new OrderException(message, response.StatusCode);
                        case "InputException": throw new InputException(message, response.StatusCode);
                        case "DataException": throw new DataException(message, response.StatusCode);
                        case "NetworkException": throw new NetworkException(message, response.StatusCode);
                        default: throw new GeneralException(message, response.StatusCode);
                    }
                }

                return responseDictionary;
            }

            if (response.Content.Headers.ContentType?.MediaType == "text/csv")
            {
                return Utils.ParseCSV(responseContent);
            }

            throw new DataException("Unexpected content type " + response.Content.Headers.ContentType?.MediaType + " " + responseContent);
        }

        /// <summary>
        ///     Provides the Instruments CSV file into DataTable
        /// </summary>
        /// <returns></returns>
        public static async Task<DataTable> GetInstrumentsCsv()
        {
            using HttpClient httpClient = new();
            Stream stream = await httpClient.GetStreamAsync(JugaadConstants.KiteInstrumentsCsvUrl);
            using StreamReader reader = new(stream);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
            using CsvDataReader dr = new(csv);
            DataTable dt = new();
            dt.Load(dr);
            return dt;
        }
    }
}