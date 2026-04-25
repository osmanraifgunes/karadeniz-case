using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using DocumentManager.Models;

namespace DocumentManager.Data
{
    /// <summary>
    /// Sidecar üzerinde ADO.NET CRUD. EF kullanmıyoruz — saf, açıklanabilir
    /// SQL ile arama planını kontrol etmek istiyoruz (400ms response budget).
    /// </summary>
    public class DocumentSearchRepository
    {
        private static readonly Dictionary<int, string> DocTypeMap = new Dictionary<int, string>
        {
            { 0, "Contract" }, { 1, "Offer" }, { 2, "Invoice" }
        };

        private static readonly Dictionary<string, int> DocTypeMapReverse = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Contract", 0 }, { "Offer", 1 }, { "Invoice", 2 }
        };

        public DuplicateInfo FindByHash(string contentHash)
        {
            if (string.IsNullOrEmpty(contentHash)) return new DuplicateInfo { IsDuplicate = false };
            const string sql = @"
SELECT TOP 1 d.Id, d.Title, d.UploadedBy, d.UploadDate
FROM DocumentSearchIndex idx
JOIN Documents d ON d.Id = idx.DocumentId
WHERE idx.ContentHash = @hash
ORDER BY d.UploadDate DESC;";
            using (var conn = SidecarBootstrapper.OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 64).Value = contentHash;
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new DuplicateInfo
                        {
                            IsDuplicate = true,
                            ContentHash = contentHash,
                            ExistingDocumentId = r.GetInt32(0),
                            ExistingTitle = r.IsDBNull(1) ? null : r.GetString(1),
                            ExistingUploadedBy = r.IsDBNull(2) ? null : r.GetString(2),
                            ExistingUploadDate = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3),
                        };
                    }
                }
            }
            return new DuplicateInfo { IsDuplicate = false, ContentHash = contentHash };
        }

        public void Upsert(int documentId, string contentHash, string normalizedTitle, string tags)
        {
            const string sql = @"
MERGE DocumentSearchIndex AS target
USING (SELECT @id AS DocumentId) AS src
ON (target.DocumentId = src.DocumentId)
WHEN MATCHED THEN UPDATE SET
    ContentHash = @hash,
    NormalizedTitle = @title,
    Tags = @tags,
    IndexedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (DocumentId, ContentHash, NormalizedTitle, Tags, IndexedAt)
    VALUES (@id, @hash, @title, @tags, SYSUTCDATETIME());";
            using (var conn = SidecarBootstrapper.OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = documentId;
                cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 64).Value = contentHash ?? string.Empty;
                cmd.Parameters.Add("@title", SqlDbType.NVarChar, 512).Value = normalizedTitle ?? string.Empty;
                cmd.Parameters.Add("@tags", SqlDbType.NVarChar, 512).Value = tags ?? string.Empty;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Sidecar'da olmayan (eski) Documents satırlarının Id+Title+FilePath listesi.</summary>
        public List<(int Id, string Title, string FilePath)> ListUnindexed(int limit = 500)
        {
            var list = new List<(int, string, string)>();
            const string sql = @"
SELECT TOP (@n) d.Id, ISNULL(d.Title, ''), ISNULL(d.FilePath, '')
FROM Documents d
LEFT JOIN DocumentSearchIndex idx ON idx.DocumentId = d.Id
WHERE idx.DocumentId IS NULL;";
            using (var conn = SidecarBootstrapper.OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@n", SqlDbType.Int).Value = limit;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
                }
            }
            return list;
        }

        public SearchResponse Search(SearchRequest req, string normalizedQuery, string[] tokens)
        {
            // Dynamic SQL — tüm parametreler @-binding ile geçer (SQL injection güvenli).
            var where = new List<string>();
            var p = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(req.DocType) && DocTypeMapReverse.TryGetValue(req.DocType, out var dt))
            {
                where.Add("d.DocumentType = @dt");
                p.Add(new SqlParameter("@dt", SqlDbType.Int) { Value = dt });
            }
            if (!string.IsNullOrWhiteSpace(req.Uploader))
            {
                where.Add("d.UploadedBy LIKE @uploader");
                p.Add(new SqlParameter("@uploader", SqlDbType.NVarChar, 256) { Value = "%" + req.Uploader.Trim() + "%" });
            }
            if (req.From.HasValue)
            {
                where.Add("d.UploadDate >= @from");
                p.Add(new SqlParameter("@from", SqlDbType.DateTime) { Value = req.From.Value });
            }
            if (req.To.HasValue)
            {
                where.Add("d.UploadDate <= @to");
                p.Add(new SqlParameter("@to", SqlDbType.DateTime) { Value = req.To.Value });
            }

            // Token bazlı arama — her token için ayrı LIKE; AND ile birleştir.
            for (int i = 0; i < tokens.Length; i++)
            {
                where.Add($"(idx.NormalizedTitle LIKE @t{i} OR idx.Tags LIKE @t{i})");
                p.Add(new SqlParameter("@t" + i, SqlDbType.NVarChar, 256) { Value = "%" + tokens[i] + "%" });
            }

            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;

            // Skor: tam başlık eşleşmesi > prefix > kısmi. Recency bonusu küçük tutuldu.
            var scoreSql = @"
CASE
    WHEN idx.NormalizedTitle = @qExact THEN 1.0
    WHEN idx.NormalizedTitle LIKE @qPrefix THEN 0.8
    WHEN @hasQuery = 1 THEN 0.5
    ELSE 0.3
END
+ (DATEDIFF(DAY, '2000-01-01', d.UploadDate) / 100000.0)";

            var page = Math.Max(1, req.Page);
            var size = Math.Min(100, Math.Max(1, req.PageSize));
            var offset = (page - 1) * size;

            var sql = $@"
SELECT d.Id, d.Title, d.DocumentType, d.UploadedBy, d.UploadDate, d.FilePath, idx.ContentHash,
       {scoreSql} AS Score
FROM Documents d
LEFT JOIN DocumentSearchIndex idx ON idx.DocumentId = d.Id
{whereSql}
ORDER BY Score DESC, d.UploadDate DESC
OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY;

SELECT COUNT(*) FROM Documents d
LEFT JOIN DocumentSearchIndex idx ON idx.DocumentId = d.Id
{whereSql};";

            var resp = new SearchResponse { Page = page, PageSize = size };

            using (var conn = SidecarBootstrapper.OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                foreach (var pp in p) cmd.Parameters.Add(pp);
                cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
                cmd.Parameters.Add("@size", SqlDbType.Int).Value = size;
                cmd.Parameters.Add("@qExact", SqlDbType.NVarChar, 512).Value = normalizedQuery ?? string.Empty;
                cmd.Parameters.Add("@qPrefix", SqlDbType.NVarChar, 512).Value = (normalizedQuery ?? string.Empty) + "%";
                cmd.Parameters.Add("@hasQuery", SqlDbType.Bit).Value = string.IsNullOrWhiteSpace(normalizedQuery) ? 0 : 1;

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var docTypeInt = r.GetInt32(2);
                        DocTypeMap.TryGetValue(docTypeInt, out var docType);
                        resp.Items.Add(new SearchResultItem
                        {
                            Id = r.GetInt32(0),
                            Title = r.IsDBNull(1) ? null : r.GetString(1),
                            DocumentType = docType ?? docTypeInt.ToString(),
                            UploadedBy = r.IsDBNull(3) ? null : r.GetString(3),
                            UploadDate = r.GetDateTime(4),
                            FilePath = r.IsDBNull(5) ? null : r.GetString(5),
                            ContentHash = r.IsDBNull(6) ? null : r.GetString(6),
                            Score = Convert.ToDouble(r.GetValue(7)),
                            MatchedOn = string.IsNullOrWhiteSpace(normalizedQuery) ? "all" : "title|tags"
                        });
                    }
                    if (r.NextResult() && r.Read())
                    {
                        resp.Total = r.GetInt32(0);
                    }
                }
            }
            return resp;
        }

        /// <summary>Boş sonuçlu sorgular için: en sık geçen başlık token'ları.</summary>
        public List<string> SuggestTerms(int limit = 5)
        {
            var list = new List<string>();
            const string sql = @"
SELECT TOP (@n) NormalizedTitle FROM DocumentSearchIndex
WHERE LEN(NormalizedTitle) > 0
ORDER BY IndexedAt DESC;";
            using (var conn = SidecarBootstrapper.OpenConnection())
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@n", SqlDbType.Int).Value = limit;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var t = r.GetString(0);
                        var firstToken = t.Split(' ').FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(firstToken) && !list.Contains(firstToken))
                            list.Add(firstToken);
                    }
                }
            }
            return list;
        }
    }
}
