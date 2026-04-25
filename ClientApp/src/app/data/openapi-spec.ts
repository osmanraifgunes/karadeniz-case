/**
 * OpenAPI 3.0 spec — el yazımı, single source of truth.
 *
 * NEDEN HAND-WRITTEN?
 *  - Backend'de Swashbuckle yok (yeni NuGet eklemedik). Olsaydı XML doc'tan
 *    auto-generate ederdik; şu an 3 endpoint var, manuel maliyeti düşük.
 *  - Spec Git'te → kontrat değişikliği PR diff'inde görünür.
 *  - Kullanıcı /docs route'unda Swagger UI ile interaktif test edebilir
 *    (Try it out → backend'e gerçek istek atar; CORS handler izin veriyor).
 *
 * Yeni endpoint eklediğinizde: bu dosyaya da bir entry ekleyin.
 * Drift olmasın diye küçük bir kontrol testi (eslint vs.) iyi olur — TODO.
 */
export const openApiSpec = {
  openapi: '3.0.3',
  info: {
    title: 'Doküman Yönetimi API',
    version: '1.0.0',
    description:
      'Doküman listeleme, arama, duplicate önleme ve yükleme endpoint\'leri. ' +
      'Mevcut `Documents` tablosu üzerine sidecar `DocumentSearchIndex` ile çalışır. ' +
      'Aşağıdaki istekleri "Try it out" ile direkt backend\'e atabilirsiniz.'
  },
  servers: [
    { url: '/', description: 'Aynı origin (Angular dev proxy / production reverse proxy)' },
    { url: 'https://localhost:44308', description: 'IIS Express (direct, dev only)' }
  ],
  tags: [
    { name: 'documents', description: 'Doküman arama ve yükleme' }
  ],
  paths: {
    '/api/documents': {
      get: {
        tags: ['documents'],
        summary: 'Doküman ara/listele',
        description:
          'Sidecar index üzerinde token-bazlı LIKE araması. `q` boş bırakılırsa ' +
          'tüm dokümanları döner (en yeni → eski). Filtreler AND mantığıyla birleşir. ' +
          'Bütün parametreler opsiyonel.',
        parameters: [
          { name: 'q', in: 'query', schema: { type: 'string' }, description: 'Arama terimi (Türkçe normalize edilir)', example: 'sözleşme' },
          { name: 'type', in: 'query', schema: { type: 'string', enum: ['Contract', 'Offer', 'Invoice'] }, description: 'Doküman türü' },
          { name: 'from', in: 'query', schema: { type: 'string', format: 'date-time' }, description: 'Yükleme tarihi başlangıç' },
          { name: 'to', in: 'query', schema: { type: 'string', format: 'date-time' }, description: 'Yükleme tarihi bitiş' },
          { name: 'uploader', in: 'query', schema: { type: 'string' }, description: 'Yükleyen (kısmi eşleşme)' },
          { name: 'page', in: 'query', schema: { type: 'integer', default: 1, minimum: 1 } },
          { name: 'pageSize', in: 'query', schema: { type: 'integer', default: 20, minimum: 1, maximum: 100 } }
        ],
        responses: {
          '200': {
            description: 'Sayfalanmış sonuç + meta',
            content: { 'application/json': { schema: { $ref: '#/components/schemas/SearchResponse' } } }
          },
          '500': {
            description: 'Sunucu hatası (sidecar bootstrap fail vb.)',
            content: { 'application/json': { schema: { $ref: '#/components/schemas/ErrorResponse' } } }
          }
        }
      }
    },
    '/api/documents/check-duplicate': {
      get: {
        tags: ['documents'],
        summary: 'Hash ile duplicate kontrolü',
        description:
          'Upload öncesi, dosyanın istemci tarafında hesaplanmış SHA-256 hash\'i ile ' +
          'sistemde aynı içerik var mı diye sorar. Frontend büyük dosyayı yüklemeden ' +
          'kullanıcıya uyarı çıkarmak için kullanır.',
        parameters: [
          {
            name: 'hash', in: 'query', required: true,
            schema: { type: 'string', minLength: 64, maxLength: 64 },
            description: 'SHA-256 (hex, lowercase, 64 karakter)',
            example: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'
          }
        ],
        responses: {
          '200': {
            description: '`isDuplicate=true` ise mevcut dokümanın bilgileri döner',
            content: { 'application/json': { schema: { $ref: '#/components/schemas/DuplicateInfo' } } }
          },
          '400': { description: 'hash parametresi eksik/geçersiz' }
        }
      }
    },
    '/api/documents/upload': {
      post: {
        tags: ['documents'],
        summary: 'Doküman yükle',
        description:
          'Multipart upload + sunucu tarafında SHA-256 + sidecar dedup. ' +
          'Aynı içerik daha önce yüklendiyse `409 Conflict` döner — `force=true` ' +
          'ile bypass edip "yeni versiyon" olarak kaydedilebilir.',
        requestBody: {
          required: true,
          content: {
            'multipart/form-data': {
              schema: {
                type: 'object',
                required: ['file', 'title', 'documentType', 'uploadedBy'],
                properties: {
                  file: { type: 'string', format: 'binary', description: 'Dosya içeriği' },
                  title: { type: 'string', example: 'ABC Şirketi Yıllık Hizmet Sözleşmesi' },
                  documentType: { type: 'string', enum: ['Contract', 'Offer', 'Invoice'] },
                  uploadedBy: { type: 'string', example: 'raif.gunes' },
                  force: { type: 'string', enum: ['true', 'false'], description: 'Duplicate uyarısını bypass et', default: 'false' }
                }
              }
            }
          }
        },
        responses: {
          '200': {
            description: 'Başarıyla yüklendi',
            content: { 'application/json': { schema: { $ref: '#/components/schemas/UploadResult' } } }
          },
          '400': { description: 'Eksik/hatalı form alanı' },
          '409': {
            description: 'Aynı içerik mevcut (soft-block; `force=true` ile aşılabilir)',
            content: { 'application/json': { schema: { $ref: '#/components/schemas/UploadResult' } } }
          }
        }
      }
    }
  },
  components: {
    schemas: {
      SearchResultItem: {
        type: 'object',
        properties: {
          id: { type: 'integer', example: 42 },
          title: { type: 'string', example: 'Hizmet Sözleşmesi - ABC' },
          documentType: { type: 'string', enum: ['Contract', 'Offer', 'Invoice'] },
          uploadedBy: { type: 'string', example: 'raif.gunes' },
          uploadDate: { type: 'string', format: 'date-time' },
          filePath: { type: 'string', example: '/App_Data/Uploads/e3b0c4...docx' },
          contentHash: { type: 'string', example: 'e3b0c4...', description: 'SHA-256, hex' },
          score: { type: 'number', format: 'double', description: 'Ranking skoru (0-1 arası taban + recency)' },
          matchedOn: { type: 'string', example: 'title|tags' }
        }
      },
      SearchResponse: {
        type: 'object',
        properties: {
          total: { type: 'integer' },
          page: { type: 'integer' },
          pageSize: { type: 'integer' },
          tookMs: { type: 'integer', description: 'Sorgu süresi (ms) — 400ms bütçesini kontrol için' },
          items: { type: 'array', items: { $ref: '#/components/schemas/SearchResultItem' } },
          suggestions: {
            type: 'array',
            items: { type: 'string' },
            description: '0 sonuçlu sorguda öneri token\'ları (sidecar\'daki son 5 başlık)'
          }
        }
      },
      DuplicateInfo: {
        type: 'object',
        properties: {
          isDuplicate: { type: 'boolean' },
          contentHash: { type: 'string' },
          existingDocumentId: { type: 'integer', nullable: true },
          existingTitle: { type: 'string', nullable: true },
          existingUploadedBy: { type: 'string', nullable: true },
          existingUploadDate: { type: 'string', format: 'date-time', nullable: true }
        }
      },
      UploadResult: {
        type: 'object',
        properties: {
          success: { type: 'boolean' },
          documentId: { type: 'integer', nullable: true },
          message: { type: 'string' },
          duplicate: { $ref: '#/components/schemas/DuplicateInfo', nullable: true }
        }
      },
      ErrorResponse: {
        type: 'object',
        properties: {
          message: { type: 'string' },
          exceptionType: { type: 'string' }
        }
      }
    }
  }
} as const;
