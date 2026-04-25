using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using DocumentManager.Data;
using DocumentManager.Models;

namespace DocumentManager.Services
{
    /// <summary>
    /// Search + dedup orchestration. Controller'ı kasten ince tutuyoruz;
    /// kararlar (ranking, normalize, hash) burada — test edilebilir kalsın diye.
    /// </summary>
    public class DocumentSearchService
    {
        private readonly DocumentSearchRepository _repo = new DocumentSearchRepository();

        public SearchResponse Search(SearchRequest req)
        {
            SidecarBootstrapper.EnsureCreated();
            var sw = Stopwatch.StartNew();

            var normalized = DocumentNormalizer.Normalize(req.Query ?? string.Empty);
            var tokens = DocumentNormalizer.Tokenize(normalized);

            var resp = _repo.Search(req, normalized, tokens);
            resp.TookMs = sw.ElapsedMilliseconds;

            if (resp.Total == 0 && tokens.Length > 0)
            {
                resp.Suggestions = _repo.SuggestTerms(5);
            }
            return resp;
        }

        public DuplicateInfo CheckDuplicate(string contentHash)
        {
            SidecarBootstrapper.EnsureCreated();
            return _repo.FindByHash(contentHash);
        }

        /// <summary>
        /// Yüklenen dosyayı diske kaydeder, hash + sidecar index üretir.
        /// Caller (controller) Documents tablosuna insert yaptıktan sonra
        /// burayı çağırarak sidecar'ı güncellemiş olur.
        /// </summary>
        public string SaveAndHash(HttpPostedFileBase file, string uploadDirAbsolute)
        {
            if (!Directory.Exists(uploadDirAbsolute))
                Directory.CreateDirectory(uploadDirAbsolute);

            // Önce hash hesapla — duplicate ise dosyayı diske yazmaya gerek yok.
            var hash = HashService.ComputeSha256(file.InputStream);

            // Hash-tabanlı dosya adı: aynı içerik birden çok kez upload edilse de
            // disk'te tek kopya tutulur. Orijinal isim title alanında kalır.
            var ext = Path.GetExtension(file.FileName ?? "") ?? "";
            var storedName = hash + ext;
            var fullPath = Path.Combine(uploadDirAbsolute, storedName);
            if (!File.Exists(fullPath))
            {
                file.InputStream.Position = 0;
                using (var fs = File.Create(fullPath))
                {
                    file.InputStream.CopyTo(fs);
                }
            }
            return hash;
        }

        public void IndexDocument(int documentId, string title, string contentHash, string tags = null)
        {
            SidecarBootstrapper.EnsureCreated();
            var normalized = DocumentNormalizer.Normalize(title);
            _repo.Upsert(documentId, contentHash, normalized, tags ?? string.Empty);
        }

        /// <summary>
        /// Mevcut Documents satırlarını sidecar'a aktar. Lazy çağrılır
        /// (ilk arama isteğinde, önceden indexlenmemiş kayıtlar için).
        /// </summary>
        public int BackfillUnindexed(string uploadDirAbsolute)
        {
            SidecarBootstrapper.EnsureCreated();
            var unindexed = _repo.ListUnindexed(500);
            int count = 0;
            foreach (var (id, title, filePath) in unindexed)
            {
                string hash = string.Empty;
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    // FilePath örnek: "/App_Data/Uploads/abc.docx"
                    var rel = filePath.Replace("/App_Data/Uploads/", "").Replace("\\App_Data\\Uploads\\", "");
                    var abs = Path.Combine(uploadDirAbsolute, rel);
                    if (File.Exists(abs))
                    {
                        try { hash = HashService.ComputeSha256OfFile(abs); }
                        catch { /* okunamayan dosyayı sessizce geç — sonraki backfill yine dener */ }
                    }
                }
                _repo.Upsert(id, hash, DocumentNormalizer.Normalize(title), string.Empty);
                count++;
            }
            return count;
        }
    }
}
