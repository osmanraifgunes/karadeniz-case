using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace DocumentManager.App_Start
{
    /// <summary>
    /// Manuel CORS message handler.
    ///
    /// NEDEN: Microsoft.AspNet.WebApi.Cors paketini eklemiyoruz — case kısıtı
    /// "ek altyapı yatırımı yok" diyor; yeni runtime bağımlılığını da bilinçli
    /// olarak minimuma indiriyoruz. ~30 satırlık bu handler ihtiyacı karşılıyor.
    ///
    /// Allowed origins: development'ta Angular dev server (localhost:4200).
    /// Production'da reverse proxy / same-origin yaklaşımı önerilir.
    /// </summary>
    public class CorsHandler : DelegatingHandler
    {
        private static readonly string[] AllowedOrigins = new[]
        {
            "http://localhost:4200",
            "http://localhost:51439",
            "https://localhost:44308"
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var origin = request.Headers.Contains("Origin") ? request.Headers.GetValues("Origin").FirstOrDefault() : null;
            var isAllowed = !string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

            // Preflight (OPTIONS)
            if (request.Method == HttpMethod.Options)
            {
                var preflight = new HttpResponseMessage(HttpStatusCode.OK);
                AddCorsHeaders(preflight, origin, isAllowed);
                return preflight;
            }

            var response = await base.SendAsync(request, cancellationToken);
            AddCorsHeaders(response, origin, isAllowed);
            return response;
        }

        private static void AddCorsHeaders(HttpResponseMessage response, string origin, bool isAllowed)
        {
            if (!isAllowed) return;
            response.Headers.Add("Access-Control-Allow-Origin", origin);
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization, X-Requested-With");
            response.Headers.Add("Access-Control-Allow-Credentials", "true");
            response.Headers.Add("Access-Control-Max-Age", "600");
        }
    }
}
