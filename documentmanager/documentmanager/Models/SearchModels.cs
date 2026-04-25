using System;
using System.Collections.Generic;

namespace DocumentManager.Models
{
    /// <summary>
    /// API DTO'ları. Mevcut domain modelinden ayrı tutuyoruz ki entity'leri
    /// dışarıya sızdırmayalım ve API kontratını bağımsız evrimleştirebilelim.
    /// </summary>
    public class SearchRequest
    {
        public string Query { get; set; }
        public string DocType { get; set; }     // "Contract" | "Offer" | "Invoice" | null
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Uploader { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SearchResultItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string DocumentType { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadDate { get; set; }
        public string FilePath { get; set; }
        public string ContentHash { get; set; }
        /// <summary>Ranking için skor (0..1). Eşleşme türüne göre değişir.</summary>
        public double Score { get; set; }
        /// <summary>Hangi alanın eşleştiğini UI'da göstermek için.</summary>
        public string MatchedOn { get; set; }
    }

    public class SearchResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TookMs { get; set; }
        public List<SearchResultItem> Items { get; set; } = new List<SearchResultItem>();
        /// <summary>Boş sonuçta önerilecek query ipuçları.</summary>
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    public class DuplicateInfo
    {
        public bool IsDuplicate { get; set; }
        public string ContentHash { get; set; }
        public int? ExistingDocumentId { get; set; }
        public string ExistingTitle { get; set; }
        public string ExistingUploadedBy { get; set; }
        public DateTime? ExistingUploadDate { get; set; }
    }

    public class UploadResult
    {
        public bool Success { get; set; }
        public int? DocumentId { get; set; }
        public string Message { get; set; }
        public DuplicateInfo Duplicate { get; set; }
    }
}
