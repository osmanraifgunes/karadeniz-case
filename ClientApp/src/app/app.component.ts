import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterOutlet, Router, ActivatedRoute, NavigationEnd } from '@angular/router';
import { Subject, filter } from 'rxjs';
import { HeaderComponent } from './components/header/header.component';
import { UploadDialogComponent } from './components/upload-dialog/upload-dialog.component';
import { NotificationService } from './services/notification.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, HeaderComponent, RouterOutlet, UploadDialogComponent],
  template: `
    <app-header (upload)="openUpload()"></app-header>

    <main class="route-host">
      <router-outlet></router-outlet>
    </main>

    <app-upload-dialog
      *ngIf="showUpload()"
      (closed)="showUpload.set(false)"
      (uploaded)="onUploaded()"
    ></app-upload-dialog>

    <!-- Toasts -->
    <div class="toast-stack" aria-live="polite">
      <div *ngFor="let t of notify.toasts()" class="toast toast-{{ t.kind }}">
        <span>{{ t.message }}</span>
        <button class="btn btn-ghost text-sm" (click)="notify.dismiss(t.id)">×</button>
      </div>
    </div>

    <footer class="container muted text-xs">
      Doküman Yönetimi · case prototype · arama + duplicate dedup sidecar
    </footer>
  `,
  styles: [`
    main { display: block; }
    .container { max-width: 1100px; margin: 1.25rem auto; padding: 0 1.25rem; }
    footer.container { padding-top: 2rem; padding-bottom: 1rem; text-align: center; }

    .toast-stack { position: fixed; bottom: 1rem; right: 1rem; display: flex; flex-direction: column; gap: 0.5rem; z-index: 100; }
    .toast { display: flex; gap: 0.75rem; align-items: center; padding: 0.65rem 0.85rem; border-radius: 6px; box-shadow: 0 6px 20px rgba(15,23,42,0.18); background: white; border: 1px solid var(--border); min-width: 280px; }
    .toast-success { border-left: 4px solid var(--success); }
    .toast-error { border-left: 4px solid var(--danger); }
    .toast-warning { border-left: 4px solid var(--warning); }
    .toast-info { border-left: 4px solid var(--primary); }
  `]
})
export class AppComponent {
  readonly notify = inject(NotificationService);
  private readonly router = inject(Router);
  readonly showUpload = signal(false);

  /**
   * Refresh sinyali — DocumentList bunu dinler ve listeyi yeniler.
   * (ViewChild'a güvenmek route-aware değil; AppComponent her zaman aktif.)
   * window'a koymak yerine bir RxJS Subject ile geçirebilirdik ama burada
   * query param "refresh=now" trick'i en az iz bırakanı.
   */
  onUploaded(): void {
    // Eğer şu an /docs'taysak, kullanıcıyı listeye geri götür.
    if (this.router.url.startsWith('/docs') || this.router.url === '/docs') {
      this.router.navigate(['/'], { queryParams: { refresh: Date.now() } });
    } else {
      // Aynı sayfada query param güncelle → DocumentList paramMap subscription'ı tetikler.
      this.router.navigate([], { queryParams: { refresh: Date.now() }, queryParamsHandling: 'merge' });
    }
  }

  openUpload(): void { this.showUpload.set(true); }
}
