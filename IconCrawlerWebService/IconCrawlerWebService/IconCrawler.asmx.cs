using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Services;

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

        readonly private List<string> ImagesToTry = new List<string>{ "/favicon.ico", "/apple-touch-icon.png" };

        private HttpWebResponse getWebResponse(string domain)
        {
            HttpWebResponse response = null;

            try
            {
                WebRequest request = WebRequest.Create(domain);

                request.Credentials = CredentialCache.DefaultCredentials;

                response = (HttpWebResponse)request.GetResponse();
            }
            catch
            {
                response = null;
            }

            return response;
        }

        [WebMethod]
        public string GetIcon(string domain)
        {
            // null or empty domain
            if (string.IsNullOrEmpty(domain))
                return string.Empty;

            // step 1 : getwebresponse with original domain name
            domain = domain.ToLower();
            if (!domain.Contains("http://") && !domain.Contains("https://"))
                domain = "http://" + domain;

            HttpWebResponse response = getWebResponse(domain);

            // if response is void or domain not found return empty
            if (response == null || response.StatusCode == HttpStatusCode.NotFound)
            {
                // if contain www. delete it, otherwise add www.
                if(domain.Contains("www."))
                    domain = domain.Replace("www.", "");
                else
                    domain = domain.Replace("://", "://www.");

                response = getWebResponse(domain);

                // still no response, return void
                if (response == null || response.StatusCode == HttpStatusCode.NotFound)
                    return string.Empty;
            }

            // try potentially images list
            foreach (string image in ImagesToTry)
            {
                response = getWebResponse(domain + image);
                if(response != null && response.StatusCode == HttpStatusCode.OK)
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        return reader.ReadToEnd();
                    }
            }

            // try to find link tag in html page
            return string.Empty;
        }
    }
}