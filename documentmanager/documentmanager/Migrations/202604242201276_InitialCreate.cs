namespace documentmanager.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Documents",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        FilePath = c.String(),
                        UploadDate = c.DateTime(nullable: false),
                        UploadedBy = c.String(),
                        DocumentType = c.Int(nullable: false),
                        StartDate = c.DateTime(),
                        EndDate = c.DateTime(),
                        PartyA = c.String(),
                        PartyB = c.String(),
                        InvoiceNumber = c.String(),
                        Total = c.Decimal(precision: 18, scale: 2),
                        InvoiceDate = c.DateTime(),
                        DueDate = c.DateTime(),
                        Amount = c.Decimal(precision: 18, scale: 2),
                        ValidUntil = c.DateTime(),
                        Discriminator = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.Documents");
        }
    }
}
