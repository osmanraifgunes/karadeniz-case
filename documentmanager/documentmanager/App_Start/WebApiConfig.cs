using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using DocumentManager.App_Start;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace documentmanager
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // CORS handler (manual, no extra NuGet package — case kısıtı: ek altyapı yok).
            config.MessageHandlers.Add(new CorsHandler());

            // JSON: camelCase + ISO date — Angular tarafıyla idiomatic kontrat.
            var jsonFormatter = config.Formatters.JsonFormatter;
            jsonFormatter.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            jsonFormatter.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            jsonFormatter.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            jsonFormatter.SerializerSettings.Converters.Add(new StringEnumConverter());

            // Attribute routing
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
