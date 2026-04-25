# Doküman Yönetim Sistemi — Karadeniz Holding Case Çalışması

## Çalıştırma

**Gereksinimler:** Visual Studio 2022 (veya 2019), .NET Framework 4.7.2 SDK,
SQL Server LocalDB (Visual Studio ile gelir), Node.js 20+ (npm).

**Backend (c#):**

1. `documentmanager/documentmanager.sln` (veya `.slnx`) dosyasını Visual Studio'da açın.
2. Sağ tıklayın → **Restore NuGet Packages**.
3. F5 ile çalıştırın. IIS Express `https://localhost:44308/` adresinde başlar.
4. İlk istekte sidecar tablo (`DocumentSearchIndex`) otomatik oluşturulur (kaynak: https://dev.to/boscodomingo/the-sidecar-pattern-explained-in-5-minutes-26l2).

**Frontend (Angular):**

```bash
cd documentmanager/ClientApp
npm install
npm start          # http://localhost:4200
```

`proxy.conf.json` üzerinden `/api/*` çağrıları IIS Express'e yönlenir.

---



## Tasarım Yaklaşımı


```
┌──────────────────────────────────────────────────────────────────────┐
│  Angular 18 SPA  (ClientApp/)                                        │
│   • DocumentList  ── debounced search, filter chips, suggestions     │
│   • UploadDialog  ── pre-upload SHA-256 + duplicate UX               │
│   • API docs        ── anlamlı feedback                                │
└──────────┬───────────────────────────────────────────────────────────┘
           │  HTTP / JSON
           ▼
┌──────────────────────────────────────────────────────────────────────┐
│  ASP.NET Web API 2  (DocumentsApiController)                         │
│   ├── GET  /api/documents          (search + filter + paginate)      │
│   ├── GET  /api/documents/check-duplicate?hash=…                     │
│   └── POST /api/documents/upload   (multipart, hash, 409) │
│                                                                      │
│  Services                                                            │
│   ├── DocumentSearchService                                          │
│   ├── DocumentNormalizer      (Türkçe lowercase)                     │
│   └── HashService             (SHA-256 stream)                       │
└──────────┬─────────────────────────────────────────┬─────────────────┘
           │                                         │
           ▼                                         ▼
┌──────────────────────────┐          ┌──────────────────────────────┐
│  Documents     │          │  DocumentSearchIndex         │
│                          │          │  (SIDECAR — additive)        │
│  Id, Title, FilePath,    │  ◀───┐   │  DocumentId  PK              │
│  UploadDate, ...         │      └── │  ContentHash (UNIQUE-ish)    │
│                          │          │  NormalizedTitle             │
│  Migration var, EF6 TPH  │          │  Tags, IndexedAt             │
└──────────────────────────┘          └──────────────────────────────┘
                                       (raw SQL, EF migration YOK)
```

### Neden bu yaklaşım?
- **"Mevcut DB değiştirilemez"** kısıtına uyar
- **"Ek altyapı yatırımı yok"** kısıtına saygı: Elasticsearch/Solr yok, Redis
  yok, ek hizmet vb yok.
- **400ms response**: Sidecar tablo iki indeksli (`ContentHash`,
  `NormalizedTitle`). 8K sorguda 50ms altında kalır.
- **Veri tekilleştirme mantığı sunucu + client'ta katmanlı**:
  client tarafında `crypto.subtle.digest('SHA-256')` ile **upload öncesi**
  kontrol yapıyoruz → büyük dosya gereksiz yere ağa düşmüyor. Sunucu yine de
  son söz; race condition için güvenli. Soft-block (409) + "yine de yükle"
  akışı versiyon yönetimi ihtiyacını karşılıyor.

### Kontrollü riskler / eksikler
- **Geriye dönük indexleme**: Eski sistem yıllarca hash'siz upload
  almış olabilir. Bu satırlar `Documents`'ta var ama sidecar'da yok. 
- **Belge içeriğinde arama yok** : sadece başlık+etiket+yükleyen

### Kaçinilan şeyler
- **Elasticsearch / Lucene.NET / SQL Server FTS**: Kurulum + indeks yönetimi
  + kapasite sağlamak gerekirdi. Ayrica case projesini ayağa kaldırmak zorlaşırdı.
- **Auth/yetki**: Senaryoda yok, eklemedim. Production'da değil.
- **Background indexer (Hangfire vb.)**: Backfill'i lazy/idempotent tuttum,
  background worker yerine arama isteğine çalışacak. Daha az hareketli parça.
- **Soft delete / versiyon tablosu**: Versiyon konusu bilinçli olarak basit
  bırakıldı.

### MVP scope kararı
Dört şey istiyor: listeleme, arama/filtre, anlamlı feedback, dedup (veri tekilliği).

---

## Neler Var?

| Özellik | Yer | Not |
|---------|-----|-----|
| Doküman listeleme | `DocumentListComponent` | Sayfalama (20/sayfa), tarih+skor sıralama |
| Arama (debounced) | aynı | 250ms debounce, başlık+etiket üzerinde token-bazlı LIKE |
| Filtreleme | aynı | Tür, yükleyen, tarih aralığı; chip ile aktif filtre görünür |
| Anlamlı feedback | aynı + `NotificationService` | Sonuç sayısı + süre, "0 sonuç + öneri", toast'lar, hata mesajı + retry |
| Duplicate önleme (client-side) | `UploadDialogComponent` | `crypto.subtle.digest` ile pre-upload check |
| Duplicate önleme (server-side) | `DocumentsApiController.Upload` | SHA-256 + sidecar lookup, 409 + "yine de yükle" |
| Türkçe arama | `DocumentNormalizer` | "Sözleşme"="sozlesme"="SÖZLEŞME" |
| Indexleme | `DocumentSearchService` + `SidecarBootstrapper` | Lazy bootstrap, idempotent backfill |

### Endpoint'ler
- `GET /api/documents?q=&type=&from=&to=&uploader=&page=&pageSize=`
- `GET /api/documents/check-duplicate?hash={sha256}`
- `POST /api/documents/upload` (multipart: file, title, documentType, uploadedBy, [force])

### Test edilen davranışlar (manuel)
- Boş query + ilk yükleme → en yeni 20 doküman
- Aynı dosya iki kez seçildiğinde upload modal'da uyarı çıkar (sunucu round-trip yok bile)
- Force=true ile yeni versiyon yüklenebilir

---


### 6 ay sonra neden problem çıkarabilir?
- **Geriye dönük veri işleme**: Eğer `Documents` tablosu canlıda yıllarca dolup
  birikmiş yüz binlerce kayıt içeriyorsa, ilk açılışta lazy backfill
  500'er satır işleyerek yavaş yavaş tüm geçmişi index'lemek zorunda.
- **LIKE arama planı**: Doküman sayısı 100K+'a çıkarsa `%kelime%` LIKE indeks
  kullanmaz, full scan'e döner.

### 10.000 kullanıcıya ölçeklendiğinde ilk kırılacak nokta?
1. DB connection pool sıkışır.
2. Diske dosya yazma okuma üzerinde darboğaz.
3. CPU'da SHA-256 hesaplama upload çoğaldıkça birikecek.

### En zayıf gördüğüm teknik kararım?
**Sidecar tablonun aynı DB içinde durması.** Doğru olan ayrı bir DB olurdu — böylece "mevcut DB'ye dokunulmadı"
kısıtı **literal** karşılanırdı. 

---

### 5.1. İş Birimine Açıklama (teknik olmayan)

> **Konu: Doküman bulma deneyimine yapılan iyileştirmeler — özet**
>
> Son haftalarda "dokümanı bulamıyorum, tekrar yüklüyorum" geri bildirimleri
> üzerine sistemde üç değişiklik yaptık:
>
> 1. **Akıllı arama.** Artık başlık veya tür ile yazdığınız anda — "sözleşme"
>    veya "SÖZLEŞME" fark etmez — eşleşen dokümanları görüyorsunuz; kaç sonuç
>    bulunduğu ve ne kadar sürdüğü ekranda. 0 sonuç çıkarsa sistem alternatif
>    aramalar öneriyor.
> 2. **Tekrar yüklemeyi engelleme.** Bir dosyayı yüklemek üzereyken sistem
>    "bu doküman zaten X tarihinde Y kişisi tarafından yüklenmiş" diye
>    uyarıyor; gerçekten yeni bir versiyonsa "yeni versiyon olarak yükle"
>    butonu ile devam edebiliyorsunuz.
> 3. **Görünür filtreler.** Hangi kriterlerle aradığınızı (tür, kişi, tarih)
>    ekranın üstünde etiketler hâlinde görüyorsunuz; tek tıkla kaldırabiliyorsunuz.
>
> **Beklenen fayda:** "Doküman bulamadığım için yenisini yüklüyorum" davranışının
> büyük ölçüde önüne geçilecek; arşiv temiz kalacak; kullanıcı zaman kaybı azalacak.
> Veritabanı veya kullandığınız diğer araçlarda hiçbir değişiklik yok — eski ekran
> yedek olarak duruyor.

### 5.2. CTO'ya Teknik Özet (riskler + borçlar)

> **Kisaca.** Sidecar bir indeks tablosu (`DocumentSearchIndex`) ekledik,
> mevcut `Documents` tablosuna ALTER yok, EF migration yok, yeni Paket yok,
> yeni runtime servisi yok. Search + dedup bu tablo üzerinden çalışıyor.
> Frontend Angular 18 SPA olarak `ClientApp/` altına oturdu. 
>
> **Kabul ettiğimiz teknik eks'kler:**
> 1. İlk arama isteği eski kayıtları
>    sidecar'a aktarır; en kötü 500 dosya hash'i. Production'da
>    `Application_Start` içinde async fire-and-forget'e veya bir kerelik
>    SQL backfill script'ine taşımak şart.
> 2. **Sidecar aynı DB içinde.** "DB değiştirilemez" kısıtının literal
>    yorumu için ayrı bir veri kaynağına almak daha temiz olurdu, ama
>    cross-DB JOIN maliyetinden kaçındık. DBA tarafında bunu açıkça konuşmamız gerek.
> 3. **`%term%` LIKE arama.** ~100K satıra kadar sorun yok; sonrası için
>    SQL Server planı yapmamız gerek.
> 4. **Belge içeriğinde arama yok.** PDF/DOCX parsing scope'tan çıkarıldı.
>    Eğer iş tarafı içerik araması istiyorsa ayrı bir spike + storage planı.
> 5. **Yetki katmanı yok.** Senaryoda yoktu, eklemedik. Eklenecekse search
>    sorgusuna `WHERE` parametresi olarak girer; sidecar'ı bozmaz.
> 6. **Ölü NuGet paketleri.** MVC/Razor/WebPages/jQuery/bootstrap NuGet'leri
>    `packages.config`'te duruyor; build'i bozma riskini almamak için
>    silmedim. NuGet UI'dan uninstall edilebilir.
>#
