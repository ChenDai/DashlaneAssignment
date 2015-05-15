using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Web;
using System.Web.Services;
using System.Xml;

namespace IconCrawlerWebService
{
    /// <summary>
    /// Description résumée de Service1
    /// </summary>
    [WebService(Namespace = "http://chendai.assignement.dashlane.com/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // Pour autoriser l'appel de ce service Web depuis un script à l'aide d'ASP.NET AJAX, supprimez les marques de commentaire de la ligne suivante. 
    // [System.Web.Script.Services.ScriptService]
    public class IconCrawler : System.Web.Services.WebService
    {
        /// <summary>
        /// a structure to store the image location and it's lastModifiedDate in cache hashtable
        /// </summary>
        private struct ImageInfo
        {
            public string ImageLocation;
            public DateTime LastRequestDate;

            public ImageInfo(string imageLocation, DateTime lastRequestDate)
            {
                ImageLocation = imageLocation;
                LastRequestDate = lastRequestDate;
            }
        }

        #region attributs
        /// <summary>
        /// The image list to try to fetch
        /// </summary>
        readonly private List<string> ImagesToTry = new List<string> { "/favicon.ico", "/favicon.png", "/myicon.ico", "/image.ico", "/apple-touch-icon.png" };

        /// <summary>
        /// the cache hash table key : domain, value : image location with last modified date
        /// </summary>
        static private Hashtable ImageLocationCache = new Hashtable();
        #endregion

        #region private util methods
        /// <summary>
        /// private method to get the httpwebresponse
        /// </summary>
        /// <param name="domain">the url to try</param>
        /// <returns>the httpwebresponse null if exception</returns>
        private HttpWebResponse getWebResponse(string domain, DateTime? ifModifiedSince = null)
        {
            HttpWebResponse response = null;

            try
            {
                WebRequest request = WebRequest.Create(domain);

                request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.CacheIfAvailable);

                if (ifModifiedSince != null)
                    (request as HttpWebRequest).IfModifiedSince = ifModifiedSince.Value;

                request.Credentials = CredentialCache.DefaultCredentials;

                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    response = (HttpWebResponse)e.Response;
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    { }
                    else
                        response = null;
                }
            }
            catch (Exception)
            {
                response = null;
            }

            return response;
        }

        /// <summary>
        /// update the domain and image in cache hash table
        /// </summary>
        /// <param name="domain">the domain, key of hash table</param>
        /// <param name="image">the image information, value of hash table</param>
        private void updateCache(string domain, ImageInfo? image)
        {
            if (ImageLocationCache.ContainsKey(domain))
            {
                if (image != null)
                    ImageLocationCache[domain] = image;
                else
                    ImageLocationCache.Remove(domain);
            }
            else
            {
                if (image != null)
                {
                    ImageLocationCache.Add(domain, image);
                }
            }
        }
        #endregion

        #region web methods
        /// <summary>
        /// GetIcon Original Url, from a domain in string get the icon of this domain in string
        /// </summary>
        /// <param name="domain">the domain string</param>
        /// <returns>an Url in string format empty if not found</returns>
        [WebMethod]
        public string GetIcon(string domain)
        {
            // null or empty domain
            if (string.IsNullOrEmpty(domain))
                return string.Empty;

            #region step 0 : normalize the url
            domain = domain.ToLower();
            if (!domain.Contains("http://") && !domain.Contains("https://"))
                domain = string.Concat("http://", domain);
            #endregion

            #region step 1 : getwebresponse with original domain name.
            // step 1 : getwebresponse with original domain name
            HttpWebResponse response = getWebResponse(domain);

            #region step 1.1 : try to add or delete www. in domain name
            // if response is void or domain not found return empty
            if (response == null || response.StatusCode == HttpStatusCode.NotFound)
            {
                // if contain www. delete it, otherwise add www.
                if (domain.Contains("www."))
                    domain = domain.Replace("www.", "");
                else
                    domain = domain.Replace("://", "://www.");

                response = getWebResponse(domain);

                // still no response, return void
                if (response == null || response.StatusCode == HttpStatusCode.NotFound)
                {
                    updateCache(domain, null);
                    return string.Empty;
                }
            }
            #endregion

            #region step 1.2 : try to load the domain in cache hashtable 
            if (ImageLocationCache.ContainsKey(domain))
            {
                ImageInfo imageInCache = (ImageInfo)ImageLocationCache[domain];
                HttpWebResponse imageResponse = getWebResponse(imageInCache.ImageLocation, imageInCache.LastRequestDate);
                if (imageResponse != null)
                {
                    // in cache and has not been modified
                    if (imageResponse.StatusCode == HttpStatusCode.NotModified)
                    {
                        return imageInCache.ImageLocation;
                    }
                    // in cache but has been modified
                    else if (imageResponse.StatusCode == HttpStatusCode.OK)
                    {
                        updateCache(domain, new ImageInfo(imageInCache.ImageLocation, DateTime.Now));
                        return imageInCache.ImageLocation;
                    }
                    // image does not exist anymore
                    else
                        updateCache(domain, null);

                }
                // image does not exist anymore
                else
                    updateCache(domain, null);
            }
            #endregion

            // try potentially images list
            foreach (string image in ImagesToTry)
            {
                HttpWebResponse imageResponse = getWebResponse(string.Concat(domain, image));
                if (imageResponse != null && imageResponse.StatusCode == HttpStatusCode.OK)
                {
                    updateCache(domain, new ImageInfo(string.Concat(domain, image), DateTime.Now));
                    return string.Concat(domain, image);
                }
            }
            #endregion

            #region step 2 : scan the home page and try the link tag in page
            // try to find link tag in html page
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                try
                {
                    string pageContent = reader.ReadToEnd();
                    HtmlAgilityPack.HtmlDocument htmlPageContent = new HtmlAgilityPack.HtmlDocument();
                    htmlPageContent.LoadHtml(pageContent);
                    HtmlAgilityPack.HtmlNode pageNode = htmlPageContent.DocumentNode;

                    foreach (HtmlAgilityPack.HtmlNode link in pageNode.SelectNodes("/html/head/link[contains(@rel,'icon') and @href]"))
                    {
                        // if the rel attribut of any link contains icon, try to fetch the href
                        if (link.Attributes["rel"] != null && link.Attributes["rel"].Value.Contains("icon"))
                        {
                            string hrefInLink = link.Attributes["href"] == null ? string.Empty : link.Attributes["href"].Value;
                            if (!string.IsNullOrEmpty(hrefInLink))
                            {
                                HttpWebResponse imageResponse = getWebResponse(hrefInLink);
                                if (imageResponse != null && imageResponse.StatusCode == HttpStatusCode.OK)
                                {
                                    updateCache(domain, new ImageInfo(hrefInLink, DateTime.Now));
                                    return hrefInLink;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    updateCache(domain, null);
                    return string.Empty;
                }

                updateCache(domain, null);
                return string.Empty; 
            }
            #endregion
        }
        #endregion
    }
}