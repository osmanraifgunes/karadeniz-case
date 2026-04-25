using System;

namespace DocumentManager.Models
{
    public enum DocumentType
    {
        Contract,
        Offer,
        Invoice
    }

    public abstract class Document
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadDate { get; set; }
        public string UploadedBy { get; set; }
        public DocumentType DocumentType { get; set; }
    }

    public class Contract : Document
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PartyA { get; set; }
        public string PartyB { get; set; }
    }

    public class Offer : Document
    {
        public decimal Amount { get; set; }
        public DateTime? ValidUntil { get; set; }
    }

    public class Invoice : Document
    {
        public string InvoiceNumber { get; set; }
        public decimal Total { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
