namespace Execute
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net;
    using System.Collections;
    using System.Text.RegularExpressions;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;
    using AngleSharp;
    using AngleSharp.Dom;
    public class RetObject
    {
        public string ResponseText
        {
            get;
            set;
        }
        public OrderedDictionary HttpResponseHeaders
        {
            get;
            set;
        }
        public CookieCollection CookieCollection
        {
            get;
            set;
        }
        public IDocument HtmlDocument
        {
            get;
            set;
        }
        public HttpResponseMessage HttpResponseMessage
        {
            get;
            set;
        }
    }
    public class Utils
    {
        public Utils()
        {
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(this.GetType().Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }
        private static IDocument DomParser(string responseText)
        {
            IDocument dom = DOMParser.GetDomDocument(responseText);
            return dom;
        }
        private static CookieCollection SetCookieParser(String domain, List<string> setCookieHeader, CookieCollection new_collection, CookieCollection previousCookies)
        {
            Cookie[] cks0 = new Cookie[new_collection.Count];
            new_collection.CopyTo(cks0, 0);
            for (int a = 0; a < setCookieHeader.Count; a++)
            {
                string[] cookie_strings = setCookieHeader[a].Split(';');
                Cookie cookie = new Cookie();
                for (int i = 0; i < cookie_strings.Count(); i++)
                {
                    string kvp = cookie_strings[i].Trim();
                    if (i == 0)
                    {
                        cookie.Name = kvp.Split('=')[0].Trim();
                        cookie.Value = String.Join('='.ToString(), kvp.Split('=').Skip(1)).Trim();
                    }
                    else
                    {
                        string property = kvp.Split('=')[0].Trim().ToLower();
                        string value = String.Join('='.ToString(), kvp.Split('=').Skip(1)).Trim();
                        switch (property)
                        {
                            case "comment":
                                cookie.Comment = value;
                                break;
                            case "commenturi":
                                try
                                {
                                    cookie.CommentUri = new Uri(value);
                                }
                                catch { }
                                break;
                            case "discard":
                                cookie.Discard = true;
                                break;
                            case "domain":
                                cookie.Domain = value;
                                break;
                            case "expires":
                                cookie.Expires = DateTime.Parse(value);
                                break;
                            case "httponly":
                                cookie.HttpOnly = true;
                                break;
                            case "path":
                                cookie.Path = value;
                                break;
                            case "port":
                                cookie.Port = value;
                                break;
                            case "secure":
                                cookie.Secure = true;
                                break;
                            case "version":
                                try
                                {
                                    cookie.Version = Int32.Parse(value);
                                }
                                catch { }
                                break;
                            default:
                                break;
                        }
                    }
                }
                if (String.IsNullOrEmpty(cookie.Domain))
                {
                    cookie.Domain = domain;
                }
                if (String.IsNullOrEmpty(cookie.Path))
                {
                    cookie.Path = "/";
                }
                if (cks0.Where(ck =>
                {
                    return (ck.Name.Equals(cookie.Name));
                }).Count() == 0)
                {
                    new_collection.Add(cookie);
                }
            }
            if (previousCookies != null)
            {
                Cookie[] ckse = new Cookie[new_collection.Count];
                new_collection.CopyTo(ckse, 0);
                foreach (Cookie pcookie in previousCookies)
                {
                    if (ckse.Where(ck =>
                    {
                        return (ck.Name.Equals(pcookie.Name));
                    }).Count() == 0)
                    {
                        new_collection.Add(pcookie);
                    }
                }
            }
            return new_collection;
        }
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];
            int cnt;
            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }
        public static string Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }
                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }
        public async Task<RetObject> SendHttp(string uri, HttpMethod method = null, OrderedDictionary headers = null, CookieCollection cookies = null, string contentType = null, string body = null, string filepath = null)
        {
            return await Task.Run(async () =>
            {
                byte[] reStream;
                RetObject retObj = new RetObject();
                HttpResponseMessage res = new HttpResponseMessage();
                OrderedDictionary httpResponseHeaders = new OrderedDictionary();
                CookieCollection responseCookies;
                CookieCollection rCookies = new CookieCollection();
                List<string> setCookieValue = new List<string>();
                CookieContainer coo = new CookieContainer();
                IDocument dom;
                string htmlString = String.Empty;
                if (method == null)
                {
                    method = HttpMethod.Get;
                }
                HttpClientHandler handle = new HttpClientHandler()
                {
                    AutomaticDecompression = (DecompressionMethods)1 & (DecompressionMethods)2,
                    UseProxy = false,
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = Int32.MaxValue,
                    MaxConnectionsPerServer = Int32.MaxValue,
                    MaxResponseHeadersLength = Int32.MaxValue,
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls12
                };
                HttpClient client = new HttpClient(handle);
                if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.89 Safari/537.36");
                }
                client.DefaultRequestHeaders.Add("Path", (new Uri(uri).PathAndQuery));
                List<string> headersToSkip = new List<string>();
                headersToSkip.Add("Accept");
                headersToSkip.Add("pragma");
                headersToSkip.Add("Cache-Control");
                headersToSkip.Add("Date");
                headersToSkip.Add("Content-Length");
                headersToSkip.Add("Content-Type");
                headersToSkip.Add("Expires");
                headersToSkip.Add("Last-Modified");
                if (headers != null)
                {
                    headersToSkip.ForEach((i) => {
                        headers.Remove(i);
                    });
                    IEnumerator enume = headers.Keys.GetEnumerator();
                    while (enume.MoveNext())
                    {
                        string key = enume.Current.ToString();
                        string value = String.Join("\n", headers[key]);
                        if (client.DefaultRequestHeaders.Contains(key))
                        {
                            client.DefaultRequestHeaders.Remove(key);
                        }
                        try
                        {
                            client.DefaultRequestHeaders.Add(key, value);
                        }
                        catch
                        {
                            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
                        }
                    }
                }
                if (cookies != null)
                {
                    IEnumerator cnume = cookies.GetEnumerator();
                    while (cnume.MoveNext())
                    {
                        Cookie cook = (Cookie)cnume.Current;
                        coo.Add(cook);
                    }
                    handle.CookieContainer = coo;
                }
                bool except = false;
                string domain = new Uri(uri).Host;
                switch (method.ToString())
                {
                    case "DELETE":
                        res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                        if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                        {
                            reStream = res.Content.ReadAsByteArrayAsync().Result;
                            htmlString = Unzip(reStream);
                        }
                        else
                        {
                            htmlString = res.Content.ReadAsStringAsync().Result;
                        }
                        try
                        {
                            setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                        }
                        catch
                        { }
                        res.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        res.Content.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        if (!String.IsNullOrEmpty(htmlString))
                        {
                            dom = DomParser(htmlString);
                            retObj.HtmlDocument = dom;
                        }
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                    case "GET":
                        res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                        if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                        {
                            reStream = res.Content.ReadAsByteArrayAsync().Result;
                            htmlString = Unzip(reStream);
                        }
                        else
                        {
                            try
                            {
                                htmlString = res.Content.ReadAsStringAsync().Result;
                            }
                            catch
                            {
                                except = true;
                            }
                            if (except)
                            {
                                var responseStream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                                using (var sr = new StreamReader(responseStream, Encoding.UTF8))
                                {
                                    htmlString = await sr.ReadToEndAsync().ConfigureAwait(false);
                                }
                            }

                        }
                        try
                        {
                            setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                        }
                        catch
                        { }
                        res.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        res.Content.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        if (!String.IsNullOrEmpty(htmlString))
                        {
                            dom = DomParser(htmlString);
                            retObj.HtmlDocument = dom;
                        }
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                    case "HEAD":
                        res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                        try
                        {
                            setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                        }
                        catch
                        { }
                        res.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        res.Content.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                    case "OPTIONS":
                        res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                        if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                        {
                            reStream = res.Content.ReadAsByteArrayAsync().Result;
                            htmlString = Unzip(reStream);
                        }
                        else
                        {
                            htmlString = res.Content.ReadAsStringAsync().Result;
                        }
                        try
                        {
                            setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                        }
                        catch
                        { }
                        res.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        res.Content.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        if (!String.IsNullOrEmpty(htmlString))
                        {
                            dom = DomParser(htmlString);
                            retObj.HtmlDocument = dom;
                        }
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                    case "POST":
                        if (String.IsNullOrEmpty(contentType))
                        {
                            contentType = "application/x-www-form-urlencoded";
                        }
                        if (!String.IsNullOrEmpty(body))
                        {
                            switch (contentType)
                            {
                                case @"application/x-www-form-urlencoded":
                                    res = await client.SendAsync(
                                        (new HttpRequestMessage(method, uri)
                                        {
                                            Content = (new StringContent(body, Encoding.UTF8, contentType))
                                        })
                                    );
                                    break;
                                case @"multipart/form-data":
                                    MultipartFormDataContent mpc = new MultipartFormDataContent("Boundary----" + DateTime.Now.Ticks.ToString("x"));
                                    if (!String.IsNullOrEmpty(filepath))
                                    {
                                        if (File.Exists(filepath))
                                        {
                                            string mime = MimeKit.MimeTypes.GetMimeType(filepath);
                                            ByteArrayContent bac = new ByteArrayContent(File.ReadAllBytes(filepath));
                                            bac.Headers.Add("Content-Type", mime);
                                            bac.Headers.ContentDisposition = ContentDispositionHeaderValue.Parse("attachment");
                                            bac.Headers.ContentDisposition.Name = "file";
                                            bac.Headers.ContentDisposition.FileName = new FileInfo(filepath).Name;
                                            mpc.Add(bac, new FileInfo(filepath).Name);
                                        }
                                    }
                                    if (!String.IsNullOrEmpty(body))
                                    {
                                        StringContent sc = new StringContent(body, Encoding.UTF8, @"application/x-www-form-urlencoded");
                                        mpc.Add(sc);
                                    }
                                    res = await client.SendAsync(
                                        (new HttpRequestMessage(method, uri)
                                        {
                                            Content = mpc
                                        })
                                    );
                                    break;
                                default:
                                    res = await client.SendAsync(
                                        (new HttpRequestMessage(method, uri)
                                        {
                                            Content = (new StringContent(body, Encoding.UTF8, contentType))
                                        })
                                    );
                                    break;
                            }
                            if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                            {
                                reStream = res.Content.ReadAsByteArrayAsync().Result;
                                htmlString = Unzip(reStream);
                            }
                            else
                            {
                                htmlString = res.Content.ReadAsStringAsync().Result;
                            }
                            try
                            {
                                setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                            }
                            catch
                            { }
                            res.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                            res.Content.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                        }
                        else
                        {
                            switch (contentType)
                            {
                                case @"application/x-www-form-urlencoded":
                                    res = await client.SendAsync(
                                        (new HttpRequestMessage(method, uri)
                                        {
                                            Content = (new StringContent(String.Empty, Encoding.UTF8, contentType))
                                        })
                                    );
                                    break;
                                case @"multipart/form-data":
                                    MultipartFormDataContent mpc = new MultipartFormDataContent("Boundary----" + DateTime.Now.Ticks.ToString("x"));
                                    if (!String.IsNullOrEmpty(filepath))
                                    {
                                        if (File.Exists(filepath))
                                        {
                                            string mime = MimeKit.MimeTypes.GetMimeType(filepath);
                                            ByteArrayContent bac = new ByteArrayContent(File.ReadAllBytes(filepath));
                                            bac.Headers.Add("Content-Type", mime);
                                            bac.Headers.ContentDisposition = ContentDispositionHeaderValue.Parse("attachment");
                                            bac.Headers.ContentDisposition.Name = "file";
                                            bac.Headers.ContentDisposition.FileName = new FileInfo(filepath).Name;
                                            mpc.Add(bac, new FileInfo(filepath).Name);
                                        }
                                    }
                                    res = await client.SendAsync(
                                        (new HttpRequestMessage(method, uri)
                                        {
                                            Content = mpc
                                        })
                                    );
                                    break;
                                default:
                                    res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                                    break;
                            }
                            if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                            {
                                reStream = res.Content.ReadAsByteArrayAsync().Result;
                                htmlString = Unzip(reStream);
                            }
                            else
                            {
                                htmlString = res.Content.ReadAsStringAsync().Result;
                            }
                            try
                            {
                                setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                            }
                            catch
                            { }
                            res.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                            res.Content.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                        }
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        if (!String.IsNullOrEmpty(htmlString))
                        {
                            dom = DomParser(htmlString);
                            retObj.HtmlDocument = dom;
                        }
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                    case "PUT":
                        if (String.IsNullOrEmpty(contentType))
                        {
                            contentType = "application/x-www-form-urlencoded";
                        }
                        if (!String.IsNullOrEmpty(body))
                        {
                            res = await client.SendAsync(
                                (new HttpRequestMessage(method, uri)
                                {
                                    Content = (new StringContent(body, Encoding.UTF8, contentType))
                                })
                            );
                            if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                            {
                                reStream = res.Content.ReadAsByteArrayAsync().Result;
                                htmlString = Unzip(reStream);
                            }
                            else
                            {
                                htmlString = res.Content.ReadAsStringAsync().Result;
                            }
                            try
                            {
                                setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                            }
                            catch
                            { }
                            res.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                            res.Content.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                        }
                        else
                        {
                            res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                            if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                            {
                                reStream = res.Content.ReadAsByteArrayAsync().Result;
                                htmlString = Unzip(reStream);
                            }
                            else
                            {
                                htmlString = res.Content.ReadAsStringAsync().Result;
                            }
                            try
                            {
                                setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                            }
                            catch
                            { }
                            res.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                            res.Content.Headers.ToList().ForEach((i) =>
                            {
                                httpResponseHeaders.Add(i.Key, i.Value);
                            });
                        }
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        if (!String.IsNullOrEmpty(htmlString))
                        {
                            dom = DomParser(htmlString);
                            retObj.HtmlDocument = dom;
                        }
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                    case "TRACE":
                        res = await client.SendAsync((new HttpRequestMessage(method, uri)));
                        if (res.Content.Headers.ContentEncoding.ToString().ToLower().Equals("gzip"))
                        {
                            reStream = res.Content.ReadAsByteArrayAsync().Result;
                            htmlString = Unzip(reStream);
                        }
                        else
                        {
                            htmlString = res.Content.ReadAsStringAsync().Result;
                        }
                        try
                        {
                            setCookieValue = res.Headers.GetValues("Set-Cookie").ToList();
                        }
                        catch
                        { }
                        res.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        res.Content.Headers.ToList().ForEach((i) =>
                        {
                            httpResponseHeaders.Add(i.Key, i.Value);
                        });
                        responseCookies = handle.CookieContainer.GetCookies(new Uri(uri));
                        rCookies = SetCookieParser(domain,setCookieValue, responseCookies, cookies);
                        if (!String.IsNullOrEmpty(htmlString))
                        {
                            dom = DomParser(htmlString);
                            retObj.HtmlDocument = dom;
                        }
                        retObj.HttpResponseHeaders = httpResponseHeaders;
                        retObj.HttpResponseMessage = res;
                        break;
                }
                if (!String.IsNullOrEmpty(htmlString))
                {
                    retObj.ResponseText = htmlString;
                }
                retObj.CookieCollection = rCookies;
                return retObj;
            });
        }
    }
    public class HttpRequest
    {
        public HttpRequest()
        {
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(this.GetType().Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }
        static HttpRequest()
        {
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(typeof(HttpRequest).Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }
        public static RetObject Send(string uri, HttpMethod method = null, OrderedDictionary headers = null, CookieCollection cookies = null, string contentType = null, string body = null, string filepath = null)
        {
            Task<RetObject> t = Task.Factory.StartNew(async () =>
            {
                Utils utils = new Utils();
                RetObject r = await utils.SendHttp(uri, method, headers, cookies, contentType, body, filepath);
                return r;
            }).Result;
            return t.Result;
        }
    }
    public class DOMParser
    {
        static DOMParser()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string resourceName = new AssemblyName(args.Name).Name + ".dll";
                string resource = Array.Find(typeof(DOMParser).Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }
        public static IDocument GetDomDocument(string HtmlString)
        {
            Task<IDocument> doc = Task.Factory.StartNew(async () =>
            {
                IBrowsingContext context = BrowsingContext.New(Configuration.Default);
                IDocument d = await context.OpenAsync(req => req.Content(HtmlString));
                return d;
            }).Result;
            return doc.Result;
        }
    }
}
