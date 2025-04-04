﻿using System.Collections;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using KiteConnect;

namespace KiteConnectJugaad.Sdk
{
    /// <summary>
    ///     The API client class. In production, you may initialize a single instance of this class per `APIKey`.
    /// </summary>
    public class Kite
    {
        public readonly Dictionary<string, string> _routes = new()
        {
            ["parameters"] = "/parameters",
            ["api.token"] = "/session/token",
            ["api.refresh"] = "/session/refresh_token",
            ["instrument.margins"] = "/margins/{segment}",
            ["order.margins"] = "/margins/orders",
            ["basket.margins"] = "/margins/basket",
            ["order.contractnote"] = "/charges/orders",
            ["user.profile"] = "/user/profile",
            ["user.margins"] = "/user/margins",
            ["user.segment_margins"] = "/user/margins/{segment}",
            ["orders"] = "/orders",
            ["trades"] = "/trades",
            ["orders.history"] = "/orders/{order_id}",
            ["orders.place"] = "/orders/{variety}",
            ["orders.modify"] = "/orders/{variety}/{order_id}",
            ["orders.cancel"] = "/orders/{variety}/{order_id}",
            ["orders.trades"] = "/orders/{order_id}/trades",
            ["gtt"] = "/gtt/triggers",
            ["gtt.place"] = "/gtt/triggers",
            ["gtt.info"] = "/gtt/triggers/{id}",
            ["gtt.modify"] = "/gtt/triggers/{id}",
            ["gtt.delete"] = "/gtt/triggers/{id}",
            ["portfolio.positions"] = "/portfolio/positions",
            ["portfolio.holdings"] = "/portfolio/holdings",
            ["portfolio.positions.modify"] = "/portfolio/positions",
            ["portfolio.auction.instruments"] = "/portfolio/holdings/auctions",
            ["market.instruments.all"] = "/instruments",
            ["market.instruments"] = "/instruments/{exchange}",
            ["market.quote"] = "/quote",
            ["market.ohlc"] = "/quote/ohlc",
            ["market.ltp"] = "/quote/ltp",
            ["market.historical"] = "/instruments/historical/{instrument_token}/{interval}",
            ["market.trigger_range"] = "/instruments/trigger_range/{transaction_type}",
            ["mutualfunds.orders"] = "/mf/orders",
            ["mutualfunds.order"] = "/mf/orders/{order_id}",
            ["mutualfunds.orders.place"] = "/mf/orders",
            ["mutualfunds.cancel_order"] = "/mf/orders/{order_id}",
            ["mutualfunds.sips"] = "/mf/sips",
            ["mutualfunds.sips.place"] = "/mf/sips",
            ["mutualfunds.cancel_sips"] = "/mf/sips/{sip_id}",
            ["mutualfunds.sips.modify"] = "/mf/sips/{sip_id}",
            ["mutualfunds.sip"] = "/mf/sips/{sip_id}",
            ["mutualfunds.instruments"] = "/mf/instruments",
            ["mutualfunds.holdings"] = "/mf/holdings"
        };

        public string _accessToken;

        public string _apiKey;
        public bool _enableLogging;

        public string _login = "https://kite.zerodha.com/connect/login";

        // Default root API endpoint. It's possible to
        // override this by passing the `Root` parameter during initialisation.
        public string _root = "https://api.kite.trade";

        public Action _sessionHook;

        public int _timeout;

        public HttpClient httpClient;

        /// <summary>
        ///     Initialize a new Kite Connect client instance.
        /// </summary>
        /// <param name="APIKey">API Key issued to you</param>
        /// <param name="AccessToken">
        ///     The token obtained after the login flow in exchange for the `RequestToken` .
        ///     Pre-login, this will default to None,but once you have obtained it, you should persist it in a database or session
        ///     to pass
        ///     to the Kite Connect class initialisation for subsequent requests.
        /// </param>
        /// <param name="Root">
        ///     API end point root. Unless you explicitly want to send API requests to a non-default endpoint, this
        ///     can be ignored.
        /// </param>
        /// <param name="Debug">If set to True, will serialise and print requests and responses to stdout.</param>
        /// <param name="Timeout">
        ///     Time in milliseconds for which  the API client will wait for a request to complete before it
        ///     fails
        /// </param>
        /// <param name="Proxy">To set proxy for http request. Should be an object of WebProxy.</param>
        /// <param name="Pool">Number of connections to server. Client will reuse the connections if they are alive.</param>
        public Kite(string APIKey, string AccessToken = null, string Root = null, bool Debug = false, int Timeout = 7000, IWebProxy Proxy = null, int Pool = 2)
        {
            this._accessToken = AccessToken;
            this._apiKey = APIKey;
            if (!string.IsNullOrEmpty(Root))
            {
                this._root = Root;
            }

            this._enableLogging = Debug;

            this._timeout = Timeout;

            HttpClientHandler httpClientHandler = new() { Proxy = Proxy };
            this.httpClient = new HttpClient(httpClientHandler) { BaseAddress = new Uri(this._root), Timeout = TimeSpan.FromMilliseconds(Timeout) };

            ServicePointManager.DefaultConnectionLimit = Pool;
        }

        /// <summary>
        ///     Enabling logging prints HTTP request and response summaries to console
        /// </summary>
        /// <param name="enableLogging">Set to true to enable logging</param>
        public void EnableLogging(bool enableLogging)
        {
            this._enableLogging = enableLogging;
        }

        /// <summary>
        ///     Set a callback hook for session (`TokenException` -- timeout, expiry etc.) errors.
        ///     An `AccessToken` (login session) can become invalid for a number of
        ///     reasons, but it doesn't make sense for the client to
        ///     try and catch it during every API call.
        ///     A callback method that handles session errors
        ///     can be set here and when the client encounters
        ///     a token error at any point, it'll be called.
        ///     This callback, for instance, can log the user out of the UI,
        ///     clear session cookies, or initiate a fresh login.
        /// </summary>
        /// <param name="Method">Action to be invoked when session becomes invalid.</param>
        public void SetSessionExpiryHook(Action Method)
        {
            this._sessionHook = Method;
        }

        /// <summary>
        ///     Set the `AccessToken` received after a successful authentication.
        /// </summary>
        /// <param name="AccessToken">Access token for the session.</param>
        public void SetAccessToken(string AccessToken)
        {
            this._accessToken = AccessToken;
        }

        /// <summary>
        ///     Get the remote login url to which a user should be redirected to initiate the login flow.
        /// </summary>
        /// <returns>Login url to authenticate the user.</returns>
        public string GetLoginURL()
        {
            return string.Format("{0}?api_key={1}&v=3", this._login, this._apiKey);
        }

        /// <summary>
        ///     Do the token exchange with the `RequestToken` obtained after the login flow,
        ///     and retrieve the `AccessToken` required for all subsequent requests.The
        ///     response contains not just the `AccessToken`, but metadata for
        ///     the user who has authenticated.
        /// </summary>
        /// <param name="RequestToken">Token obtained from the GET paramers after a successful login redirect.</param>
        /// <param name="AppSecret">API secret issued with the API key.</param>
        /// <returns>User structure with tokens and profile data</returns>
        public User GenerateSession(string RequestToken, string AppSecret)
        {
            string checksum = Utils.SHA256Hash(this._apiKey + RequestToken + AppSecret);

            Dictionary<string, dynamic> param = new() { { "api_key", this._apiKey }, { "request_token", RequestToken }, { "checksum", checksum } };

            dynamic userData = this.Post("api.token", param);

            return new User(userData);
        }

        /// <summary>
        ///     Kill the session by invalidating the access token
        /// </summary>
        /// <param name="AccessToken">Access token to invalidate. Default is the active access token.</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> InvalidateAccessToken(string AccessToken = null)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "api_key", this._apiKey);
            Utils.AddIfNotNull(param, "access_token", AccessToken);

            return this.Delete("api.token", param);
        }

        /// <summary>
        ///     Invalidates RefreshToken
        /// </summary>
        /// <param name="RefreshToken">RefreshToken to invalidate</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> InvalidateRefreshToken(string RefreshToken)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "api_key", this._apiKey);
            Utils.AddIfNotNull(param, "refresh_token", RefreshToken);

            return this.Delete("api.token", param);
        }

        /// <summary>
        ///     Renew AccessToken using RefreshToken
        /// </summary>
        /// <param name="RefreshToken">RefreshToken to renew the AccessToken.</param>
        /// <param name="AppSecret">API secret issued with the API key.</param>
        /// <returns>TokenRenewResponse that contains new AccessToken and RefreshToken.</returns>
        public TokenSet RenewAccessToken(string RefreshToken, string AppSecret)
        {
            Dictionary<string, dynamic> param = new();

            string checksum = Utils.SHA256Hash(this._apiKey + RefreshToken + AppSecret);

            Utils.AddIfNotNull(param, "api_key", this._apiKey);
            Utils.AddIfNotNull(param, "refresh_token", RefreshToken);
            Utils.AddIfNotNull(param, "checksum", checksum);

            return new TokenSet(this.Post("api.refresh", param));
        }

        /// <summary>
        ///     Gets currently logged in user details
        /// </summary>
        /// <returns>User profile</returns>
        public Profile GetProfile()
        {
            dynamic? profileData = this.Get("user.profile");

            return new Profile(profileData);
        }

        /// <summary>
        ///     A virtual contract provides detailed charges order-wise for brokerage, STT, stamp duty, exchange transaction
        ///     charges, SEBI turnover charge, and GST.
        /// </summary>
        /// <param name="ContractNoteParams">List of all order params to get contract notes for</param>
        /// <returns>List of contract notes for the params</returns>
        public List<ContractNote> GetVirtualContractNote(List<ContractNoteParams> ContractNoteParams)
        {
            List<Dictionary<string, dynamic>> paramList = new();

            foreach (ContractNoteParams item in ContractNoteParams)
            {
                Dictionary<string, dynamic> param = new();
                param["order_id"] = item.OrderID;
                param["exchange"] = item.Exchange;
                param["tradingsymbol"] = item.TradingSymbol;
                param["transaction_type"] = item.TransactionType;
                param["quantity"] = item.Quantity;
                param["average_price"] = item.AveragePrice;
                param["product"] = item.Product;
                param["order_type"] = item.OrderType;
                param["variety"] = item.Variety;

                paramList.Add(param);
            }

            dynamic contractNoteData = this.Post("order.contractnote", paramList, json: true);

            List<ContractNote> contractNotes = new();
            foreach (Dictionary<string, dynamic> item in contractNoteData["data"])
            {
                contractNotes.Add(new ContractNote(item));
            }

            return contractNotes;
        }

        /// <summary>
        ///     Margin data for a specific order
        /// </summary>
        /// <param name="OrderMarginParams">List of all order params to get margins for</param>
        /// <param name="Mode">Mode of the returned response content. Eg: Constants.MARGIN_MODE_COMPACT</param>
        /// <returns>List of margins of order</returns>
        public List<OrderMargin> GetOrderMargins(List<OrderMarginParams> OrderMarginParams, string Mode = null)
        {
            List<Dictionary<string, dynamic>> paramList = new();

            foreach (OrderMarginParams item in OrderMarginParams)
            {
                Dictionary<string, dynamic> param = new();
                param["exchange"] = item.Exchange;
                param["tradingsymbol"] = item.TradingSymbol;
                param["transaction_type"] = item.TransactionType;
                param["quantity"] = item.Quantity;
                param["price"] = item.Price;
                param["product"] = item.Product;
                param["order_type"] = item.OrderType;
                param["trigger_price"] = item.TriggerPrice;
                param["variety"] = item.Variety;

                paramList.Add(param);
            }

            Dictionary<string, dynamic> queryParams = new();
            if (Mode != null)
            {
                queryParams["mode"] = Mode;
            }

            dynamic orderMarginsData = this.Post("order.margins", paramList, queryParams, true);

            List<OrderMargin> orderMargins = new();
            foreach (Dictionary<string, dynamic> item in orderMarginsData["data"])
            {
                orderMargins.Add(new OrderMargin(item));
            }

            return orderMargins;
        }

        /// <summary>
        ///     Margin data for a basket orders
        /// </summary>
        /// <param name="OrderMarginParams">List of all order params to get margins for</param>
        /// <param name="ConsiderPositions">Consider users positions while calculating margins</param>
        /// <param name="Mode">Mode of the returned response content. Eg: Constants.MARGIN_MODE_COMPACT</param>
        /// <returns>List of margins of order</returns>
        public BasketMargin GetBasketMargins(List<OrderMarginParams> OrderMarginParams, bool ConsiderPositions = true, string Mode = null)
        {
            List<Dictionary<string, dynamic>> paramList = new();

            foreach (OrderMarginParams item in OrderMarginParams)
            {
                Dictionary<string, dynamic> param = new();
                param["exchange"] = item.Exchange;
                param["tradingsymbol"] = item.TradingSymbol;
                param["transaction_type"] = item.TransactionType;
                param["quantity"] = item.Quantity;
                param["price"] = item.Price;
                param["product"] = item.Product;
                param["order_type"] = item.OrderType;
                param["trigger_price"] = item.TriggerPrice;
                param["variety"] = item.Variety;

                paramList.Add(param);
            }

            Dictionary<string, dynamic> queryParams = new();
            queryParams["consider_positions"] = ConsiderPositions ? "true" : "false";
            if (Mode != null)
            {
                queryParams["mode"] = Mode;
            }

            dynamic basketMarginsData = this.Post("basket.margins", paramList, queryParams, true);

            return new BasketMargin(basketMarginsData["data"]);
        }

        /// <summary>
        ///     Get account balance and cash margin details for all segments.
        /// </summary>
        /// <returns>User margin response with both equity and commodity margins.</returns>
        public UserMarginsResponse GetMargins()
        {
            dynamic? marginsData = this.Get("user.margins");
            return new UserMarginsResponse(marginsData["data"]);
        }

        /// <summary>
        ///     Get account balance and cash margin details for a particular segment.
        /// </summary>
        /// <param name="Segment">Trading segment (eg: equity or commodity)</param>
        /// <returns>Margins for specified segment.</returns>
        public UserMargin GetMargins(string Segment)
        {
            dynamic userMarginData = this.Get("user.segment_margins", new Dictionary<string, dynamic> { { "segment", Segment } });
            return new UserMargin(userMarginData["data"]);
        }

        /// <summary>
        ///     Place an order
        /// </summary>
        /// <param name="Exchange">Name of the exchange</param>
        /// <param name="TradingSymbol">Tradingsymbol of the instrument</param>
        /// <param name="TransactionType">BUY or SELL</param>
        /// <param name="Quantity">Quantity to transact</param>
        /// <param name="Price">For LIMIT orders</param>
        /// <param name="Product">Margin product applied to the order (margin is blocked based on this)</param>
        /// <param name="OrderType">Order type (MARKET, LIMIT etc.)</param>
        /// <param name="Validity">Order validity (DAY, IOC and TTL)</param>
        /// <param name="DisclosedQuantity">Quantity to disclose publicly (for equity trades)</param>
        /// <param name="TriggerPrice">For SL, SL-M etc.</param>
        /// <param name="SquareOffValue">
        ///     Price difference at which the order should be squared off and profit booked (eg: Order
        ///     price is 100. Profit target is 102. So squareoff = 2)
        /// </param>
        /// <param name="StoplossValue">
        ///     Stoploss difference at which the order should be squared off (eg: Order price is 100.
        ///     Stoploss target is 98. So stoploss = 2)
        /// </param>
        /// <param name="TrailingStoploss">
        ///     Incremental value by which stoploss price changes when market moves in your favor by the
        ///     same incremental value from the entry price (optional)
        /// </param>
        /// <param name="Variety">
        ///     You can place orders of varieties; regular orders, after market orders, cover orders, iceberg
        ///     orders etc.
        /// </param>
        /// <param name="Tag">An optional tag to apply to an order to identify it (alphanumeric, max 20 chars)</param>
        /// <param name="ValidityTTL">Order life span in minutes for TTL validity orders</param>
        /// <param name="IcebergLegs">
        ///     Total number of legs for iceberg order type (number of legs per Iceberg should be between 2
        ///     and 10)
        /// </param>
        /// <param name="IcebergQuantity">Split quantity for each iceberg leg order (Quantity/IcebergLegs)</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> PlaceOrder(
            string Exchange,
            string TradingSymbol,
            string TransactionType,
            int Quantity,
            decimal? Price = null,
            string Product = null,
            string OrderType = null,
            string Validity = null,
            int? DisclosedQuantity = null,
            decimal? TriggerPrice = null,
            decimal? SquareOffValue = null,
            decimal? StoplossValue = null,
            decimal? TrailingStoploss = null,
            string Variety = Constants.VARIETY_REGULAR,
            string Tag = "",
            int? ValidityTTL = null,
            int? IcebergLegs = null,
            int? IcebergQuantity = null,
            string AuctionNumber = null
        )
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "exchange", Exchange);
            Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
            Utils.AddIfNotNull(param, "transaction_type", TransactionType);
            Utils.AddIfNotNull(param, "quantity", Quantity.ToString());
            Utils.AddIfNotNull(param, "price", Price.ToString());
            Utils.AddIfNotNull(param, "product", Product);
            Utils.AddIfNotNull(param, "order_type", OrderType);
            Utils.AddIfNotNull(param, "validity", Validity);
            Utils.AddIfNotNull(param, "disclosed_quantity", DisclosedQuantity.ToString());
            Utils.AddIfNotNull(param, "trigger_price", TriggerPrice.ToString());
            Utils.AddIfNotNull(param, "squareoff", SquareOffValue.ToString());
            Utils.AddIfNotNull(param, "stoploss", StoplossValue.ToString());
            Utils.AddIfNotNull(param, "trailing_stoploss", TrailingStoploss.ToString());
            Utils.AddIfNotNull(param, "variety", Variety);
            Utils.AddIfNotNull(param, "tag", Tag);
            Utils.AddIfNotNull(param, "validity_ttl", ValidityTTL.ToString());
            Utils.AddIfNotNull(param, "iceberg_legs", IcebergLegs.ToString());
            Utils.AddIfNotNull(param, "iceberg_quantity", IcebergQuantity.ToString());
            Utils.AddIfNotNull(param, "auction_number", AuctionNumber);

            return this.Post("orders.place", param);
        }

        /// <summary>
        ///     Modify an open order.
        /// </summary>
        /// <param name="OrderId">Id of the order to be modified</param>
        /// <param name="ParentOrderId">Id of the parent order (obtained from the /orders call) as BO is a multi-legged order</param>
        /// <param name="Exchange">Name of the exchange</param>
        /// <param name="TradingSymbol">Tradingsymbol of the instrument</param>
        /// <param name="TransactionType">BUY or SELL</param>
        /// <param name="Quantity">Quantity to transact</param>
        /// <param name="Price">For LIMIT orders</param>
        /// <param name="Product">Margin product applied to the order (margin is blocked based on this)</param>
        /// <param name="OrderType">Order type (MARKET, LIMIT etc.)</param>
        /// <param name="Validity">Order validity</param>
        /// <param name="DisclosedQuantity">Quantity to disclose publicly (for equity trades)</param>
        /// <param name="TriggerPrice">For SL, SL-M etc.</param>
        /// <param name="Variety">You can place orders of varieties; regular orders, after market orders, cover orders etc. </param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> ModifyOrder(
            string OrderId,
            string ParentOrderId = null,
            string Exchange = null,
            string TradingSymbol = null,
            string TransactionType = null,
            string Quantity = null,
            decimal? Price = null,
            string Product = null,
            string OrderType = null,
            string Validity = Constants.VALIDITY_DAY,
            int? DisclosedQuantity = null,
            decimal? TriggerPrice = null,
            string Variety = Constants.VARIETY_REGULAR)
        {
            Dictionary<string, dynamic> param = new();

            string VarietyString = Variety;
            string ProductString = Product;

            if ((ProductString == "bo" || ProductString == "co") && VarietyString != ProductString)
            {
                throw new Exception(string.Format("Invalid variety. It should be: {0}", ProductString));
            }

            Utils.AddIfNotNull(param, "order_id", OrderId);
            Utils.AddIfNotNull(param, "parent_order_id", ParentOrderId);
            Utils.AddIfNotNull(param, "trigger_price", TriggerPrice.ToString());
            Utils.AddIfNotNull(param, "variety", Variety);

            if (VarietyString == "bo" && ProductString == "bo")
            {
                Utils.AddIfNotNull(param, "quantity", Quantity);
                Utils.AddIfNotNull(param, "price", Price.ToString());
                Utils.AddIfNotNull(param, "disclosed_quantity", DisclosedQuantity.ToString());
            }
            else if (VarietyString != "co" && ProductString != "co")
            {
                Utils.AddIfNotNull(param, "exchange", Exchange);
                Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
                Utils.AddIfNotNull(param, "transaction_type", TransactionType);
                Utils.AddIfNotNull(param, "quantity", Quantity);
                Utils.AddIfNotNull(param, "price", Price.ToString());
                Utils.AddIfNotNull(param, "product", Product);
                Utils.AddIfNotNull(param, "order_type", OrderType);
                Utils.AddIfNotNull(param, "validity", Validity);
                Utils.AddIfNotNull(param, "disclosed_quantity", DisclosedQuantity.ToString());
            }

            return this.Put("orders.modify", param);
        }

        /// <summary>
        ///     Cancel an order
        /// </summary>
        /// <param name="OrderId">Id of the order to be cancelled</param>
        /// <param name="Variety">You can place orders of varieties; regular orders, after market orders, cover orders etc. </param>
        /// <param name="ParentOrderId">Id of the parent order (obtained from the /orders call) as BO is a multi-legged order</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> CancelOrder(string OrderId, string Variety = Constants.VARIETY_REGULAR, string ParentOrderId = null)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "order_id", OrderId);
            Utils.AddIfNotNull(param, "parent_order_id", ParentOrderId);
            Utils.AddIfNotNull(param, "variety", Variety);

            return this.Delete("orders.cancel", param);
        }

        /// <summary>
        ///     Gets the collection of orders from the orderbook.
        /// </summary>
        /// <returns>List of orders.</returns>
        public List<Order> GetOrders()
        {
            dynamic ordersData = this.Get("orders");

            List<Order> orders = new();

            foreach (Dictionary<string, dynamic> item in ordersData["data"])
            {
                orders.Add(new Order(item));
            }

            return orders;
        }

        /// <summary>
        ///     Gets information about given OrderId.
        /// </summary>
        /// <param name="OrderId">Unique order id</param>
        /// <returns>List of order objects.</returns>
        public List<Order> GetOrderHistory(string OrderId)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("order_id", OrderId);

            dynamic orderData = this.Get("orders.history", param);

            List<Order> orderhistory = new();

            foreach (Dictionary<string, dynamic> item in orderData["data"])
            {
                orderhistory.Add(new Order(item));
            }

            return orderhistory;
        }

        /// <summary>
        ///     Retreive the list of trades executed (all or ones under a particular order).
        ///     An order can be executed in tranches based on market conditions.
        ///     These trades are individually recorded under an order.
        /// </summary>
        /// <param name="OrderId">
        ///     is the ID of the order (optional) whose trades are to be retrieved. If no `OrderId` is specified,
        ///     all trades for the day are returned.
        /// </param>
        /// <returns>List of trades of given order.</returns>
        public List<Trade> GetOrderTrades(string OrderId = null)
        {
            Dictionary<string, dynamic> tradesdata;
            if (!string.IsNullOrEmpty(OrderId))
            {
                Dictionary<string, dynamic> param = new();
                param.Add("order_id", OrderId);
                tradesdata = this.Get("orders.trades", param);
            }
            else
            {
                tradesdata = this.Get("trades");
            }

            List<Trade> trades = new();

            foreach (Dictionary<string, dynamic> item in tradesdata["data"])
            {
                trades.Add(new Trade(item));
            }

            return trades;
        }

        /// <summary>
        ///     Retrieve the list of positions.
        /// </summary>
        /// <returns>Day and net positions.</returns>
        public PositionResponse GetPositions()
        {
            dynamic? positionsdata = this.Get("portfolio.positions");
            return new PositionResponse(positionsdata["data"]);
        }

        /// <summary>
        ///     Retrieve the list of equity holdings.
        /// </summary>
        /// <returns>List of holdings.</returns>
        public List<Holding> GetHoldings()
        {
            dynamic holdingsData = this.Get("portfolio.holdings");

            List<Holding> holdings = new();

            foreach (Dictionary<string, dynamic> item in holdingsData["data"])
            {
                holdings.Add(new Holding(item));
            }

            return holdings;
        }

        /// <summary>
        ///     Retrieve the list of auction instruments.
        /// </summary>
        /// <returns>List of auction instruments.</returns>
        public List<AuctionInstrument> GetAuctionInstruments()
        {
            dynamic instrumentsData = this.Get("portfolio.auction.instruments");

            List<AuctionInstrument> instruments = new();

            foreach (Dictionary<string, dynamic> item in instrumentsData["data"])
            {
                instruments.Add(new AuctionInstrument(item));
            }

            return instruments;
        }

        /// <summary>
        ///     Modify an open position's product type.
        /// </summary>
        /// <param name="Exchange">Name of the exchange</param>
        /// <param name="TradingSymbol">Tradingsymbol of the instrument</param>
        /// <param name="TransactionType">BUY or SELL</param>
        /// <param name="PositionType">overnight or day</param>
        /// <param name="Quantity">Quantity to convert</param>
        /// <param name="OldProduct">Existing margin product of the position</param>
        /// <param name="NewProduct">Margin product to convert to</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> ConvertPosition(
            string Exchange,
            string TradingSymbol,
            string TransactionType,
            string PositionType,
            int? Quantity,
            string OldProduct,
            string NewProduct)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "exchange", Exchange);
            Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
            Utils.AddIfNotNull(param, "transaction_type", TransactionType);
            Utils.AddIfNotNull(param, "position_type", PositionType);
            Utils.AddIfNotNull(param, "quantity", Quantity.ToString());
            Utils.AddIfNotNull(param, "old_product", OldProduct);
            Utils.AddIfNotNull(param, "new_product", NewProduct);

            return this.Put("portfolio.positions.modify", param);
        }

        /// <summary>
        ///     Retrieve the list of market instruments available to trade.
        ///     Note that the results could be large, several hundred KBs in size,
        ///     with tens of thousands of entries in the list.
        /// </summary>
        /// <param name="Exchange">Name of the exchange</param>
        /// <returns>List of instruments.</returns>
        public List<Instrument> GetInstruments(string Exchange = null)
        {
            Dictionary<string, dynamic> param = new();

            List<Dictionary<string, dynamic>> instrumentsData;

            if (string.IsNullOrEmpty(Exchange))
            {
                instrumentsData = this.Get("market.instruments.all", param);
            }
            else
            {
                param.Add("exchange", Exchange);
                instrumentsData = this.Get("market.instruments", param);
            }

            List<Instrument> instruments = new();

            foreach (Dictionary<string, dynamic> item in instrumentsData)
            {
                instruments.Add(new Instrument(item));
            }

            return instruments;
        }

        /// <summary>
        ///     Retrieve quote and market depth of upto 200 instruments
        /// </summary>
        /// <param name="InstrumentId">
        ///     Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or
        ///     InstrumentToken (eg: 408065)
        /// </param>
        /// <returns>Dictionary of all Quote objects with keys as in InstrumentId</returns>
        public Dictionary<string, Quote> GetQuote(string[] InstrumentId)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("i", InstrumentId);
            Dictionary<string, dynamic> quoteData = this.Get("market.quote", param)["data"];

            Dictionary<string, Quote> quotes = new();
            foreach (string item in quoteData.Keys)
            {
                quotes.Add(item, new Quote(quoteData[item]));
            }

            return quotes;
        }

        /// <summary>
        ///     Retrieve LTP and OHLC of upto 200 instruments
        /// </summary>
        /// <param name="InstrumentId">
        ///     Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or
        ///     InstrumentToken (eg: 408065)
        /// </param>
        /// <returns>Dictionary of all OHLC objects with keys as in InstrumentId</returns>
        public Dictionary<string, OHLC> GetOHLC(string[] InstrumentId)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("i", InstrumentId);
            Dictionary<string, dynamic> ohlcData = this.Get("market.ohlc", param)["data"];

            Dictionary<string, OHLC> ohlcs = new();
            foreach (string item in ohlcData.Keys)
            {
                ohlcs.Add(item, new OHLC(ohlcData[item]));
            }

            return ohlcs;
        }

        /// <summary>
        ///     Retrieve LTP of upto 200 instruments
        /// </summary>
        /// <param name="InstrumentId">
        ///     Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or
        ///     InstrumentToken (eg: 408065)
        /// </param>
        /// <returns>Dictionary with InstrumentId as key and LTP as value.</returns>
        public Dictionary<string, LTP> GetLTP(string[] InstrumentId)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("i", InstrumentId);
            Dictionary<string, dynamic> ltpData = this.Get("market.ltp", param)["data"];

            Dictionary<string, LTP> ltps = new();
            foreach (string item in ltpData.Keys)
            {
                ltps.Add(item, new LTP(ltpData[item]));
            }

            return ltps;
        }

        /// <summary>
        ///     Retrieve historical data (candles) for an instrument.
        /// </summary>
        /// <param name="InstrumentToken">
        ///     Identifier for the instrument whose historical records you want to fetch. This is
        ///     obtained with the instrument list API.
        /// </param>
        /// <param name="FromDate">
        ///     Date in format yyyy-MM-dd for fetching candles between two days. Date in format yyyy-MM-dd
        ///     hh:mm:ss for fetching candles between two timestamps.
        /// </param>
        /// <param name="ToDate">
        ///     Date in format yyyy-MM-dd for fetching candles between two days. Date in format yyyy-MM-dd
        ///     hh:mm:ss for fetching candles between two timestamps.
        /// </param>
        /// <param name="Interval">
        ///     The candle record interval. Possible values are: minute, day, 3minute, 5minute, 10minute,
        ///     15minute, 30minute, 60minute
        /// </param>
        /// <param name="Continuous">Pass true to get continous data of expired instruments.</param>
        /// <param name="OI">Pass true to get open interest data.</param>
        /// <returns>List of Historical objects.</returns>
        public List<Historical> GetHistoricalData(
            string InstrumentToken,
            DateTime FromDate,
            DateTime ToDate,
            string Interval,
            bool Continuous = false,
            bool OI = false)
        {
            Dictionary<string, dynamic> param = new();

            param.Add("instrument_token", InstrumentToken);
            param.Add("from", FromDate.ToString("yyyy-MM-dd HH:mm:ss"));
            param.Add("to", ToDate.ToString("yyyy-MM-dd HH:mm:ss"));
            param.Add("interval", Interval);
            param.Add("continuous", Continuous ? "1" : "0");
            param.Add("oi", OI ? "1" : "0");

            dynamic historicalData = this.Get("market.historical", param);

            List<Historical> historicals = new();

            foreach (ArrayList item in historicalData["data"]["candles"])
            {
                historicals.Add(new Historical(item));
            }

            return historicals;
        }

        /// <summary>
        ///     Retrieve the buy/sell trigger range for Cover Orders.
        /// </summary>
        /// <param name="InstrumentId">
        ///     Indentification of instrument in the form of EXCHANGE:TRADINGSYMBOL (eg: NSE:INFY) or
        ///     InstrumentToken (eg: 408065)
        /// </param>
        /// <param name="TrasactionType">BUY or SELL</param>
        /// <returns>List of trigger ranges for given instrument ids for given transaction type.</returns>
        public Dictionary<string, TrigerRange> GetTriggerRange(string[] InstrumentId, string TrasactionType)
        {
            Dictionary<string, dynamic> param = new();

            param.Add("i", InstrumentId);
            param.Add("transaction_type", TrasactionType.ToLower());

            dynamic? triggerdata = this.Get("market.trigger_range", param)["data"];

            Dictionary<string, TrigerRange> triggerRanges = new();
            foreach (string item in triggerdata.Keys)
            {
                triggerRanges.Add(item, new TrigerRange(triggerdata[item]));
            }

            return triggerRanges;
        }

        #region GTT

        /// <summary>
        ///     Retrieve the list of GTTs.
        /// </summary>
        /// <returns>List of GTTs.</returns>
        public List<GTT> GetGTTs()
        {
            dynamic gttsdata = this.Get("gtt");

            List<GTT> gtts = new();

            foreach (Dictionary<string, dynamic> item in gttsdata["data"])
            {
                gtts.Add(new GTT(item));
            }

            return gtts;
        }


        /// <summary>
        ///     Retrieve a single GTT
        /// </summary>
        /// <param name="GTTId">Id of the GTT</param>
        /// <returns>GTT info</returns>
        public GTT GetGTT(int GTTId)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("id", GTTId.ToString());

            dynamic gttdata = this.Get("gtt.info", param);

            return new GTT(gttdata["data"]);
        }

        /// <summary>
        ///     Place a GTT order
        /// </summary>
        /// <param name="gttParams">Contains the parameters for the GTT order</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> PlaceGTT(GTTParams gttParams)
        {
            Dictionary<string, dynamic> condition = new();
            condition.Add("exchange", gttParams.Exchange);
            condition.Add("tradingsymbol", gttParams.TradingSymbol);
            condition.Add("trigger_values", gttParams.TriggerPrices);
            condition.Add("last_price", gttParams.LastPrice);
            condition.Add("instrument_token", gttParams.InstrumentToken);

            List<Dictionary<string, dynamic>> ordersParam = new();
            foreach (GTTOrderParams o in gttParams.Orders)
            {
                Dictionary<string, dynamic> order = new();
                order["exchange"] = gttParams.Exchange;
                order["tradingsymbol"] = gttParams.TradingSymbol;
                order["transaction_type"] = o.TransactionType;
                order["quantity"] = o.Quantity;
                order["price"] = o.Price;
                order["order_type"] = o.OrderType;
                order["product"] = o.Product;
                ordersParam.Add(order);
            }

            Dictionary<string, dynamic> parms = new();
            parms.Add("condition", Utils.JsonSerialize(condition));
            parms.Add("orders", Utils.JsonSerialize(ordersParam));
            parms.Add("type", gttParams.TriggerType);

            return this.Post("gtt.place", parms);
        }

        /// <summary>
        ///     Modify a GTT order
        /// </summary>
        /// <param name="GTTId">Id of the GTT to be modified</param>
        /// <param name="gttParams">Contains the parameters for the GTT order</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> ModifyGTT(int GTTId, GTTParams gttParams)
        {
            Dictionary<string, dynamic> condition = new();
            condition.Add("exchange", gttParams.Exchange);
            condition.Add("tradingsymbol", gttParams.TradingSymbol);
            condition.Add("trigger_values", gttParams.TriggerPrices);
            condition.Add("last_price", gttParams.LastPrice);
            condition.Add("instrument_token", gttParams.InstrumentToken);

            List<Dictionary<string, dynamic>> ordersParam = new();
            foreach (GTTOrderParams o in gttParams.Orders)
            {
                Dictionary<string, dynamic> order = new();
                order["exchange"] = gttParams.Exchange;
                order["tradingsymbol"] = gttParams.TradingSymbol;
                order["transaction_type"] = o.TransactionType;
                order["quantity"] = o.Quantity;
                order["price"] = o.Price;
                order["order_type"] = o.OrderType;
                order["product"] = o.Product;
                ordersParam.Add(order);
            }

            Dictionary<string, dynamic> parms = new();
            parms.Add("condition", Utils.JsonSerialize(condition));
            parms.Add("orders", Utils.JsonSerialize(ordersParam));
            parms.Add("type", gttParams.TriggerType);
            parms.Add("id", GTTId.ToString());

            return this.Put("gtt.modify", parms);
        }

        /// <summary>
        ///     Cancel a GTT order
        /// </summary>
        /// <param name="GTTId">Id of the GTT to be modified</param>
        /// <returns>Json response in the form of nested string dictionary.</returns>
        public Dictionary<string, dynamic> CancelGTT(int GTTId)
        {
            Dictionary<string, dynamic> parms = new();
            parms.Add("id", GTTId.ToString());

            return this.Delete("gtt.delete", parms);
        }

        #endregion GTT


        #region MF Calls

        /// <summary>
        ///     Gets the Mutual funds Instruments.
        /// </summary>
        /// <returns>The Mutual funds Instruments.</returns>
        public List<MFInstrument> GetMFInstruments()
        {
            Dictionary<string, dynamic> param = new();

            List<Dictionary<string, dynamic>> instrumentsData;

            instrumentsData = this.Get("mutualfunds.instruments", param);

            List<MFInstrument> instruments = new();

            foreach (Dictionary<string, dynamic> item in instrumentsData)
            {
                instruments.Add(new MFInstrument(item));
            }

            return instruments;
        }

        /// <summary>
        ///     Gets all Mutual funds orders.
        /// </summary>
        /// <returns>The Mutual funds orders.</returns>
        public List<MFOrder> GetMFOrders()
        {
            Dictionary<string, dynamic> param = new();

            Dictionary<string, dynamic> ordersData;
            ordersData = this.Get("mutualfunds.orders", param);

            List<MFOrder> orderlist = new();

            foreach (Dictionary<string, dynamic> item in ordersData["data"])
            {
                orderlist.Add(new MFOrder(item));
            }

            return orderlist;
        }

        /// <summary>
        ///     Gets the Mutual funds order by OrderId.
        /// </summary>
        /// <returns>The Mutual funds order.</returns>
        /// <param name="OrderId">Order id.</param>
        public MFOrder GetMFOrders(string OrderId)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("order_id", OrderId);

            Dictionary<string, dynamic> orderData;
            orderData = this.Get("mutualfunds.order", param);

            return new MFOrder(orderData["data"]);
        }

        /// <summary>
        ///     Places a Mutual funds order.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="TradingSymbol">Tradingsymbol (ISIN) of the fund.</param>
        /// <param name="TransactionType">BUY or SELL.</param>
        /// <param name="Amount">Amount worth of units to purchase. Not applicable on SELLs.</param>
        /// <param name="Quantity">
        ///     Quantity to SELL. Not applicable on BUYs. If the holding is less than
        ///     minimum_redemption_quantity, all the units have to be sold.
        /// </param>
        /// <param name="Tag">An optional tag to apply to an order to identify it (alphanumeric, max 8 chars).</param>
        public Dictionary<string, dynamic> PlaceMFOrder(
            string TradingSymbol,
            string TransactionType,
            decimal? Amount,
            decimal? Quantity = null,
            string Tag = "")
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
            Utils.AddIfNotNull(param, "transaction_type", TransactionType);
            Utils.AddIfNotNull(param, "amount", Amount.ToString());
            Utils.AddIfNotNull(param, "quantity", Quantity.ToString());
            Utils.AddIfNotNull(param, "tag", Tag);

            return this.Post("mutualfunds.orders.place", param);
        }

        /// <summary>
        ///     Cancels the Mutual funds order.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="OrderId">Unique order id.</param>
        public Dictionary<string, dynamic> CancelMFOrder(string OrderId)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "order_id", OrderId);

            return this.Delete("mutualfunds.cancel_order", param);
        }

        /// <summary>
        ///     Gets all Mutual funds SIPs.
        /// </summary>
        /// <returns>The list of all Mutual funds SIPs.</returns>
        public List<MFSIP> GetMFSIPs()
        {
            Dictionary<string, dynamic> param = new();

            Dictionary<string, dynamic> sipData;
            sipData = this.Get("mutualfunds.sips", param);

            List<MFSIP> siplist = new();

            foreach (Dictionary<string, dynamic> item in sipData["data"])
            {
                siplist.Add(new MFSIP(item));
            }

            return siplist;
        }

        /// <summary>
        ///     Gets a single Mutual funds SIP by SIP id.
        /// </summary>
        /// <returns>The Mutual funds SIP.</returns>
        /// <param name="SIPID">SIP id.</param>
        public MFSIP GetMFSIPs(string SIPID)
        {
            Dictionary<string, dynamic> param = new();
            param.Add("sip_id", SIPID);

            Dictionary<string, dynamic> sipData;
            sipData = this.Get("mutualfunds.sip", param);

            return new MFSIP(sipData["data"]);
        }

        /// <summary>
        ///     Places a Mutual funds SIP order.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="TradingSymbol">ISIN of the fund.</param>
        /// <param name="Amount">
        ///     Amount worth of units to purchase. It should be equal to or greated than
        ///     minimum_additional_purchase_amount and in multiple of purchase_amount_multiplier in the instrument master.
        /// </param>
        /// <param name="InitialAmount">
        ///     Amount worth of units to purchase before the SIP starts. Should be equal to or greater than
        ///     minimum_purchase_amount and in multiple of purchase_amount_multiplier. This is only considered if there have been
        ///     no prior investments in the target fund.
        /// </param>
        /// <param name="Frequency">weekly, monthly, or quarterly.</param>
        /// <param name="InstalmentDay">
        ///     If Frequency is monthly, the day of the month (1, 5, 10, 15, 20, 25) to trigger the order
        ///     on.
        /// </param>
        /// <param name="Instalments">
        ///     Number of instalments to trigger. If set to -1, instalments are triggered at fixed intervals
        ///     until the SIP is cancelled.
        /// </param>
        /// <param name="Tag">An optional tag to apply to an order to identify it (alphanumeric, max 8 chars).</param>
        public Dictionary<string, dynamic> PlaceMFSIP(
            string TradingSymbol,
            decimal? Amount,
            decimal? InitialAmount,
            string Frequency,
            int? InstalmentDay,
            int? Instalments,
            string Tag = "")
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "tradingsymbol", TradingSymbol);
            Utils.AddIfNotNull(param, "initial_amount", InitialAmount.ToString());
            Utils.AddIfNotNull(param, "amount", Amount.ToString());
            Utils.AddIfNotNull(param, "frequency", Frequency);
            Utils.AddIfNotNull(param, "instalment_day", InstalmentDay.ToString());
            Utils.AddIfNotNull(param, "instalments", Instalments.ToString());

            return this.Post("mutualfunds.sips.place", param);
        }

        /// <summary>
        ///     Modifies the Mutual funds SIP.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="SIPId">SIP id.</param>
        /// <param name="Amount">
        ///     Amount worth of units to purchase. It should be equal to or greated than
        ///     minimum_additional_purchase_amount and in multiple of purchase_amount_multiplier in the instrument master.
        /// </param>
        /// <param name="Frequency">weekly, monthly, or quarterly.</param>
        /// <param name="InstalmentDay">
        ///     If Frequency is monthly, the day of the month (1, 5, 10, 15, 20, 25) to trigger the order
        ///     on.
        /// </param>
        /// <param name="Instalments">
        ///     Number of instalments to trigger. If set to -1, instalments are triggered idefinitely until
        ///     the SIP is cancelled.
        /// </param>
        /// <param name="Status">Pause or unpause an SIP (active or paused).</param>
        public Dictionary<string, dynamic> ModifyMFSIP(
            string SIPId,
            decimal? Amount,
            string Frequency,
            int? InstalmentDay,
            int? Instalments,
            string Status)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "status", Status);
            Utils.AddIfNotNull(param, "sip_id", SIPId);
            Utils.AddIfNotNull(param, "amount", Amount.ToString());
            Utils.AddIfNotNull(param, "frequency", Frequency);
            Utils.AddIfNotNull(param, "instalment_day", InstalmentDay.ToString());
            Utils.AddIfNotNull(param, "instalments", Instalments.ToString());

            return this.Put("mutualfunds.sips.modify", param);
        }

        /// <summary>
        ///     Cancels the Mutual funds SIP.
        /// </summary>
        /// <returns>JSON response as nested string dictionary.</returns>
        /// <param name="SIPId">SIP id.</param>
        public Dictionary<string, dynamic> CancelMFSIP(string SIPId)
        {
            Dictionary<string, dynamic> param = new();

            Utils.AddIfNotNull(param, "sip_id", SIPId);

            return this.Delete("mutualfunds.cancel_sips", param);
        }

        /// <summary>
        ///     Gets the Mutual funds holdings.
        /// </summary>
        /// <returns>The list of all Mutual funds holdings.</returns>
        public List<MFHolding> GetMFHoldings()
        {
            Dictionary<string, dynamic> param = new();

            Dictionary<string, dynamic> holdingsData;
            holdingsData = this.Get("mutualfunds.holdings", param);

            List<MFHolding> holdingslist = new();

            foreach (Dictionary<string, dynamic> item in holdingsData["data"])
            {
                holdingslist.Add(new MFHolding(item));
            }

            return holdingslist;
        }

        #endregion

        #region HTTP Functions

        /// <summary>
        ///     Alias for sending a GET request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        public dynamic Get(string Route, Dictionary<string, dynamic> Params = null, Dictionary<string, dynamic> QueryParams = null)
        {
            return this.Request(Route, "GET", Params, QueryParams);
        }

        /// <summary>
        ///     Alias for sending a POST request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        public dynamic Post(string Route, dynamic Params = null, Dictionary<string, dynamic> QueryParams = null, bool json = false)
        {
            return Request(Route, "POST", Params, QueryParams: QueryParams, json: json);
        }

        /// <summary>
        ///     Alias for sending a PUT request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        public dynamic Put(string Route, dynamic Params = null)
        {
            return Request(Route, "PUT", Params);
        }

        /// <summary>
        ///     Alias for sending a DELETE request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Params">Additional paramerters</param>
        /// <returns>Varies according to API endpoint</returns>
        public dynamic Delete(string Route, dynamic Params = null)
        {
            return Request(Route, "DELETE", Params);
        }

        /// <summary>
        ///     Adds extra headers to request
        /// </summary>
        /// <param name="Req">Request object to add headers</param>
        public virtual void AddExtraHeaders(ref HttpRequestMessage Req)
        {
            Assembly? KiteAssembly = Assembly.GetAssembly(typeof(Kite));
            if (KiteAssembly != null)
            {
                Req.Headers.UserAgent.TryParseAdd("KiteConnect.Net/" + KiteAssembly.GetName().Version);
            }

            Req.Headers.Add("X-Kite-Version", "3");
            Req.Headers.Add("Authorization", "token " + this._apiKey + ":" + this._accessToken);

            if (this._enableLogging)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in Req.Headers)
                {
                    Console.WriteLine("DEBUG: " + header.Key + ": " + string.Join(",", header.Value.ToArray()));
                }
            }
        }

        /// <summary>
        ///     Make an HTTP request.
        /// </summary>
        /// <param name="Route">URL route of API</param>
        /// <param name="Method">Method of HTTP request</param>
        /// <param name="Params">Additional paramerters. Can be dictionary, list etc.</param>
        /// <returns>Varies according to API endpoint</returns>
        public virtual dynamic Request(string Route, string Method, dynamic Params = null, Dictionary<string, dynamic> QueryParams = null, bool json = false)
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

            HttpRequestMessage request = new();

            if (Method == "POST" || Method == "PUT")
            {
                string url = route;
                if (QueryParams.Count > 0)
                {
                    url += "?" + string.Join("&", QueryParams.Select(x => Utils.BuildParam(x.Key, x.Value)));
                }

                string requestBody = "";
                if (json)
                {
                    requestBody = Utils.JsonSerialize(Params);
                }
                else
                {
                    requestBody = string.Join("&", (Params as Dictionary<string, dynamic>).Select(x => Utils.BuildParam(x.Key, x.Value)));
                }

                request.RequestUri = new Uri(url);
                request.Method = new HttpMethod(Method);
                this.AddExtraHeaders(ref request);

                if (this._enableLogging)
                {
                    Console.WriteLine("DEBUG: " + Method + " " + url + "\n" + requestBody);
                }

                request.Content = new StringContent(requestBody, Encoding.UTF8, json ? "application/json" : "application/x-www-form-urlencoded");
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

                request.RequestUri = new Uri(url);
                request.Method = new HttpMethod(Method);
                if (this._enableLogging)
                {
                    Console.WriteLine("DEBUG: " + Method + " " + url);
                }

                this.AddExtraHeaders(ref request);
            }

            HttpResponseMessage response = this.httpClient.Send(request);
            HttpStatusCode status = response.StatusCode;

            string responseBody = response.Content.ReadAsStringAsync().Result;
            if (this._enableLogging)
            {
                Console.WriteLine("DEBUG: " + (int)status + " " + responseBody + "\n");
            }

            if (response.Content.Headers.ContentType.MediaType == MediaTypeNames.Application.Json)
            {
                Dictionary<string, dynamic> responseDictionary = Utils.JsonDeserialize(responseBody);

                if (status != HttpStatusCode.OK)
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
                        case "GeneralException": throw new GeneralException(message, status);
                        case "TokenException":
                            {
                                this._sessionHook?.Invoke();
                                throw new TokenException(message, status);
                            }
                        case "PermissionException": throw new PermissionException(message, status);
                        case "OrderException": throw new OrderException(message, status);
                        case "InputException": throw new InputException(message, status);
                        case "DataException": throw new DataException(message, status);
                        case "NetworkException": throw new NetworkException(message, status);
                        default: throw new GeneralException(message, status);
                    }
                }

                return responseDictionary;
            }

            if (response.Content.Headers.ContentType.MediaType == "text/csv")
            {
                return Utils.ParseCSV(responseBody);
            }

            throw new DataException("Unexpected content type " + response.Content.Headers.ContentType.MediaType + " " + response);
        }

        #endregion
    }
}