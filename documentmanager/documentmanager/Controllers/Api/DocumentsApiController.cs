using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using DocumentManager.Models;
using DocumentManager.Services;

namespace DocumentManager.Controllers.Api
{
    /// <summary>
    /// /api/documents — Angular SPA buradan konuşuyor.
    /// </summary>
    [RoutePrefix("api/documents")]
    public class DocumentsApiController : ApiController
    {
        private readonly DocumentSearchService _service = new DocumentSearchService();
        private readonly DocumentDbContext _db = new DocumentDbContext();

        [HttpGet]
        [Route("")]
        public IHttpActionResult Get(string q = null, string type = null,
            DateTime? from = null, DateTime? to = null, string uploader = null,
            int page = 1, int pageSize = 20)
        {
            var req = new SearchRequest
            {
                Query = q,
                DocType = type,
                From = from,
                To = to,
                Uploader = uploader,
                Page = page,
                PageSize = pageSize
            };
            // İlk istek geldiğinde mevcut Documents satırlarını sidecar'a backfill et.
            // Yalnızca eksikleri işlediği için maliyeti düşük (idempotent).
            _service.BackfillUnindexed(HttpContext.Current.Server.MapPath("~/App_Data/Uploads"));
            var resp = _service.Search(req);
            return Ok(resp);
        }

        [HttpGet]
        [Route("check-duplicate")]
        public IHttpActionResult CheckDuplicate(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return BadRequest("hash parameter required");
            return Ok(_service.CheckDuplicate(hash));
        }

        /// <summary>
        /// Multipart upload + SHA-256 dedup.
        /// 409 → duplicate (force=true ile bypass edilebilir, "yeni versiyon" senaryosu).
        /// </summary>
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult Upload()
        {
            var ctx = HttpContext.Current;
            if (ctx == null || ctx.Request.Files.Count == 0)
                return BadRequest("file is required");

            var file = ctx.Request.Files[0];
            if (file == null || file.ContentLength == 0)
                return BadRequest("file is empty");

            var title = ctx.Request.Form["title"];
            var docTypeStr = ctx.Request.Form["documentType"]; // "Contract"|"Offer"|"Invoice"
            var uploadedBy = ctx.Request.Form["uploadedBy"];
            var force = string.Equals(ctx.Request.Form["force"], "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(title)) return BadRequest("title required");
            if (string.IsNullOrWhiteSpace(uploadedBy)) return BadRequest("uploadedBy required");
            if (!Enum.TryParse<DocumentType>(docTypeStr, true, out var docType))
                return BadRequest("documentType must be Contract|Offer|Invoice");

            var fileWrap = new HttpPostedFileWrapper(file);
            var uploadDir = ctx.Server.MapPath("~/App_Data/Uploads");
            var hash = _service.SaveAndHash(fileWrap, uploadDir);

            if (!force)
            {
                var dup = _service.CheckDuplicate(hash);
                if (dup.IsDuplicate)
                {
                    // Soft-block: dosyayı zaten yazdık (hash'li ad → çakışmaz).
                    // Documents tablosuna INSERT atmıyoruz; mevcut kaydı işaret ediyoruz.
                    return Content(HttpStatusCode.Conflict, new UploadResult
                    {
                        Success = false,
                        DocumentId = dup.ExistingDocumentId,
                        Message = "Bu doküman zaten sistemde mevcut. Üzerine yeni versiyon yüklemek için 'Yine de yükle' seçeneğini kullanın.",
                        Duplicate = dup
                    });
                }
            }

            // Documents tablosuna kayıt at — TPH inheritance: doc type'a göre alt sınıf.
            Document doc;
            switch (docType)
            {
                case DocumentType.Contract: doc = new Contract(); break;
                case DocumentType.Offer: doc = new Offer(); break;
                case DocumentType.Invoice: doc = new Invoice(); break;
                default: doc = new Contract(); break;
            }
            doc.Title = title;
            doc.DocumentType = docType;
            doc.UploadedBy = uploadedBy;
            doc.UploadDate = DateTime.UtcNow;
            doc.FilePath = "/App_Data/Uploads/" + hash + System.IO.Path.GetExtension(file.FileName ?? "");

            _db.Documents.Add(doc);
            _db.SaveChanges();

            _service.IndexDocument(doc.Id, doc.Title, hash);

            return Ok(new UploadResult
            {
                Success = true,
                DocumentId = doc.Id,
                Message = force ? "Yeni versiyon olarak yüklendi." : "Doküman yüklendi."
            });
        }
    }
}
