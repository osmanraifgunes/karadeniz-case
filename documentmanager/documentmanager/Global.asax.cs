using System.Web.Http;

namespace documentmanager
{
    /// <summary>
    /// API-only host. MVC, Areas (HelpPage), Bundles, Filters ve RouteConfig
    /// (Razor view yönlendirmesi için olan) silindi — UI artık ayrı bir Angular SPA.
    /// Sadece Web API attribute routing aktif (WebApiConfig).
    /// </summary>
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
