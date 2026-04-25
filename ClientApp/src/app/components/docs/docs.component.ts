import { Component, ElementRef, OnDestroy, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { openApiSpec } from '../../data/openapi-spec';

declare global {
  interface Window {
    SwaggerUIBundle?: any;
    SwaggerUIStandalonePreset?: any;
  }
}

const SWAGGER_VERSION = '5.17.14';
const SWAGGER_CSS = `https://unpkg.com/swagger-ui-dist@${SWAGGER_VERSION}/swagger-ui.css`;
const SWAGGER_BUNDLE = `https://unpkg.com/swagger-ui-dist@${SWAGGER_VERSION}/swagger-ui-bundle.js`;
const SWAGGER_PRESET = `https://unpkg.com/swagger-ui-dist@${SWAGGER_VERSION}/swagger-ui-standalone-preset.js`;

/**
 * /docs route — interaktif API dokümantasyonu.
 *
 * Swagger UI'yi unpkg CDN'den lazy-load eder. Spec'i bundle'a katmıyoruz
 * (60KB+ runtime; sadece /docs'a giren kullanıcı ödesin diye).
 *
 * Production'da CDN yerine paketi node_modules'tan import etmek daha sağlam;
 * MVP için CDN yeterli — ayrıca offline geliştirme için fallback
 * mesajı (status === 'error') var.
 */
@Component({
  selector: 'app-docs',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="docs-page">
      <div class="docs-intro card">
        <h2>API Dokümantasyonu</h2>
        <p class="muted">
          Aşağıdaki Swagger UI üzerinden endpoint'leri inceleyebilir, <strong>"Try it out"</strong>
          ile çalışan backend'e gerçek istek atabilirsiniz. CORS handler bu origin'e izin veriyor.
        </p>
        <p class="text-xs muted">
          Spec kaynağı: <code>ClientApp/src/app/data/openapi-spec.ts</code> (el yazımı, Git'te versiyonlanır)
        </p>
      </div>

      <div class="status status-{{ status() }}" *ngIf="status() !== 'ready'">
        <ng-container *ngIf="status() === 'loading'">Swagger UI yükleniyor…</ng-container>
        <ng-container *ngIf="status() === 'error'">
          Swagger UI yüklenemedi (CDN engelli olabilir). Spec'e doğrudan
          <a href="javascript:void(0)" (click)="downloadSpec()">JSON olarak ulaşın</a>.
          {{ errorDetail() }}
        </ng-container>
      </div>

      <div #host id="swagger-ui-host" [class.hidden]="status() !== 'ready'"></div>
    </div>
  `,
  styles: [`
    .docs-page { max-width: 1100px; margin: 1.25rem auto; padding: 0 1.25rem; }
    .docs-intro { padding: 1rem 1.25rem; margin-bottom: 1rem; }
    .docs-intro h2 { margin: 0 0 0.5rem 0; }
    .docs-intro p { margin: 0.25rem 0; }

    .status { padding: 1rem; border-radius: 6px; text-align: center; }
    .status-loading { background: #eef2ff; color: #3730a3; }
    .status-error { background: #fef2f2; color: #991b1b; }

    /* Swagger UI default arkaplanı beyaz; container'a kart gibi davranalım. */
    #swagger-ui-host { background: white; border-radius: 6px; border: 1px solid var(--border); padding: 0.5rem; }
    #swagger-ui-host.hidden { display: none; }

    /* Swagger UI'nin "topbar"ını gizle — kendi başlığımız var. */
    :global(.swagger-ui .topbar) { display: none; }
  `]
})
export class DocsComponent implements OnInit, OnDestroy {
  @ViewChild('host', { static: false }) host?: ElementRef<HTMLDivElement>;

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly errorDetail = signal<string>('');

  async ngOnInit(): Promise<void> {
    try {
      await this.ensureCss();
      await this.ensureScript(SWAGGER_BUNDLE);
      await this.ensureScript(SWAGGER_PRESET);
      // Render bir microtask sonraya — host #host ViewChild bağlansın diye
      queueMicrotask(() => this.render());
    } catch (e: any) {
      this.status.set('error');
      this.errorDetail.set(e?.message ? `(${e.message})` : '');
    }
  }

  ngOnDestroy(): void {
    // Swagger UI DOM'a doğrudan yapışıyor — Angular destroy'unda zaten temizleniyor.
  }

  private render(): void {
    if (!window.SwaggerUIBundle) {
      this.status.set('error');
      this.errorDetail.set('(SwaggerUIBundle global yüklenemedi)');
      return;
    }
    window.SwaggerUIBundle({
      spec: openApiSpec,
      domNode: this.host?.nativeElement,
      deepLinking: true,
      presets: [
        window.SwaggerUIBundle.presets.apis,
        window.SwaggerUIStandalonePreset
      ],
      layout: 'BaseLayout',
      tryItOutEnabled: true,
      filter: true,
      defaultModelsExpandDepth: 0
    });
    this.status.set('ready');
  }

  private ensureCss(): Promise<void> {
    return new Promise((resolve, reject) => {
      const existing = document.querySelector(`link[href="${SWAGGER_CSS}"]`);
      if (existing) { resolve(); return; }
      const link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = SWAGGER_CSS;
      link.onload = () => resolve();
      link.onerror = () => reject(new Error('CSS load failed'));
      document.head.appendChild(link);
    });
  }

  private ensureScript(src: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const existing = document.querySelector(`script[src="${src}"]`);
      if (existing) { resolve(); return; }
      const s = document.createElement('script');
      s.src = src;
      s.async = false; // sırayı koru: bundle → preset
      s.onload = () => resolve();
      s.onerror = () => reject(new Error(`Script load failed: ${src}`));
      document.head.appendChild(s);
    });
  }

  /** CDN engelliyse spec'i ham JSON olarak indir. */
  downloadSpec(): void {
    const blob = new Blob([JSON.stringify(openApiSpec, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'documentmanager-openapi.json';
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
