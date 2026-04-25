import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, catchError, finalize } from 'rxjs';
import { DOCUMENT_TYPES, DocumentType, SearchParams, SearchResponse } from '../../models/document';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-document-list',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './document-list.component.html',
  styleUrl: './document-list.component.css'
})
export class DocumentListComponent implements OnInit {
  private readonly api = inject(DocumentsService);
  private readonly notify = inject(NotificationService);
  private readonly route = inject(ActivatedRoute);

  readonly docTypes = DOCUMENT_TYPES;

  // Form state
  q = '';
  type: DocumentType | '' = '';
  uploader = '';
  from = '';
  to = '';
  page = 1;
  pageSize = 20;

  // UI state — signals
  readonly loading = signal(false);
  readonly response = signal<SearchResponse | null>(null);
  readonly error = signal<string | null>(null);

  // Filtre chipleri için türetilmiş state
  readonly activeFilters = computed(() => {
    const r: { key: keyof SearchParams; label: string }[] = [];
    if (this.type) r.push({ key: 'type', label: this.docTypeLabel(this.type) });
    if (this.uploader) r.push({ key: 'uploader', label: 'Yükleyen: ' + this.uploader });
    if (this.from) r.push({ key: 'from', label: 'Başlangıç: ' + this.from });
    if (this.to) r.push({ key: 'to', label: 'Bitiş: ' + this.to });
    return r;
  });

  readonly totalPages = computed(() => {
    const r = this.response();
    if (!r || r.total === 0) return 1;
    return Math.ceil(r.total / r.pageSize);
  });

  // Debounce için query subject
  private readonly query$ = new Subject<SearchParams>();

  ngOnInit(): void {
    // 250ms debounce — kullanıcı yazarken her tuşta sunucuya gitme.
    this.query$
      .pipe(
        debounceTime(250),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        switchMap((params) => {
          this.loading.set(true);
          this.error.set(null);
          return this.api.search(params).pipe(
            catchError((err) => {
              this.error.set(this.errorMessage(err));
              return of<SearchResponse | null>(null);
            }),
            finalize(() => this.loading.set(false))
          );
        })
      )
      .subscribe((r) => {
        if (r) this.response.set(r);
      });

    this.runSearch(); // ilk yükleme

    // Upload sonrası AppComponent ?refresh=<ts> parametresi ile yönlendiriyor;
    // değişimde listeyi yeniden çek. (Distinct: aynı `null`'da boşa atmasın diye.)
    this.route.queryParamMap
      .pipe(distinctUntilChanged((a, b) => a.get('refresh') === b.get('refresh')))
      .subscribe((params) => {
        if (params.get('refresh')) this.runSearch();
      });
  }

  /** Arama çağrısını tetikler. Kasıtlı: paramları her zaman buradan geçiriyoruz
   *  → tek "kaynaktan gerçek" — ikili state senkronizasyonu yok. */
  runSearch(): void {
    this.query$.next({
      q: this.q || undefined,
      type: this.type || undefined,
      from: this.from || undefined,
      to: this.to || undefined,
      uploader: this.uploader || undefined,
      page: this.page,
      pageSize: this.pageSize
    });
  }

  onQueryChange(): void {
    this.page = 1;
    this.runSearch();
  }

  onFilterChange(): void {
    this.page = 1;
    this.runSearch();
  }

  removeFilter(key: keyof SearchParams): void {
    if (key === 'type') this.type = '';
    else if (key === 'uploader') this.uploader = '';
    else if (key === 'from') this.from = '';
    else if (key === 'to') this.to = '';
    this.onFilterChange();
  }

  clearAll(): void {
    this.q = '';
    this.type = '';
    this.uploader = '';
    this.from = '';
    this.to = '';
    this.page = 1;
    this.runSearch();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page = p;
    this.runSearch();
  }

  applySuggestion(s: string): void {
    this.q = s;
    this.onQueryChange();
  }

  docTypeLabel(t: DocumentType | string): string {
    return this.docTypes.find((d) => d.value === t)?.label ?? String(t);
  }

  /** Liste boş mu, search'ün hatası mı, yoksa filtre yoğun mu? — UX için ayrım. */
  get emptyReason(): 'no-data' | 'no-results' | null {
    const r = this.response();
    if (!r || r.total > 0) return null;
    const hasFilter = this.q || this.type || this.uploader || this.from || this.to;
    return hasFilter ? 'no-results' : 'no-data';
  }

  /** External — upload component'tan refresh tetiklemek için. */
  refresh(): void { this.runSearch(); }

  private errorMessage(err: any): string {
    if (err?.status === 0) return 'Sunucuya ulaşılamıyor. Backend (IIS Express) ayakta mı?';
    return err?.error?.message ?? 'Beklenmeyen bir hata oluştu.';
  }
}
