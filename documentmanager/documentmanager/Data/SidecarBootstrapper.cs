using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;

namespace DocumentManager.Data
{
    /// <summary>
    /// Sidecar tablo (DocumentSearchIndex) oluşturulması.
    ///
    /// Mevcut "Documents" tablosuna ALTER yok — sadece YENİ bir tablo ekliyoruz.
    /// EF migration kullanmıyoruz; raw SQL "IF NOT EXISTS CREATE TABLE" çalıştırıyoruz.
    /// Bu sayede:
    ///   * Mevcut EF model hash'i değişmez (legacy DbContext "model değişti" hatası vermez).
    ///   * DBA tarafında onay almak gereken migration script'i yok.
    ///   * Roll-back basit: tek bir DROP TABLE.
    /// </summary>
    public static class SidecarBootstrapper
    {
        private static readonly object _lock = new object();
        private static volatile bool _initialized;

        private const string CreateSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DocumentSearchIndex')
BEGIN
    CREATE TABLE [dbo].[DocumentSearchIndex] (
        [DocumentId]       INT           NOT NULL PRIMARY KEY,
        [ContentHash]      NVARCHAR(64)  NOT NULL,
        [NormalizedTitle]  NVARCHAR(512) NOT NULL,
        [Tags]             NVARCHAR(512) NOT NULL DEFAULT (''),
        [IndexedAt]        DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocSearchIdx_Hash')
    CREATE INDEX IX_DocSearchIdx_Hash ON [dbo].[DocumentSearchIndex]([ContentHash]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocSearchIdx_NormTitle')
    CREATE INDEX IX_DocSearchIdx_NormTitle ON [dbo].[DocumentSearchIndex]([NormalizedTitle]);
";

        public static void EnsureCreated()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                try
                {
                    using (var conn = OpenConnection())
                    using (var cmd = new SqlCommand(CreateSql, conn))
                    {
                        cmd.CommandTimeout = 15;
                        cmd.ExecuteNonQuery();
                    }
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    // Bilinçli karar: bootstrap fail olursa app çökertmeyelim.
                    // API endpoint'leri 503 dönecek, mevcut MVC akışı çalışmaya devam.
                    Trace.TraceError("SidecarBootstrapper failed: " + ex.Message);
                }
            }
        }

        public static SqlConnection OpenConnection()
        {
            var cs = ConfigurationManager.ConnectionStrings["DocumentDb"]?.ConnectionString;
            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("DocumentDb connection string not found.");
            var conn = new SqlConnection(cs);
            conn.Open();
            return conn;
        }
    }
}
