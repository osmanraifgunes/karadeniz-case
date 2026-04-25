using System.Data.Entity;

namespace DocumentManager.Models
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext() : base("name=DocumentDb") { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Offer> Offers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
    }
}
