import { Component, EventEmitter, Output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <header class="app-header">
      <div class="brand">
        <span class="logo">DM</span>
        <h1>Doküman Yönetimi</h1>
      </div>
      <nav class="nav">
        <a routerLink="/" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="active">Dokümanlar</a>
        <a routerLink="/docs" routerLinkActive="active">API Docs</a>
      </nav>
      <div class="actions">
        <button class="btn btn-primary" (click)="upload.emit()">+ Yeni Doküman Yükle</button>
      </div>
    </header>
  `,
  styles: [`
    .app-header { display: flex; align-items: center; justify-content: space-between; padding: 0.875rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); gap: 1.5rem; }
    .brand { display: flex; align-items: center; gap: 0.75rem; }
    .brand h1 { margin: 0; font-size: 1.05rem; font-weight: 600; }
    .logo { width: 32px; height: 32px; background: var(--primary); color: white; border-radius: 6px; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 13px; }

    .nav { display: flex; gap: 0.5rem; flex: 1; margin-left: 1rem; }
    .nav a { color: var(--muted); text-decoration: none; padding: 0.4rem 0.75rem; border-radius: 6px; font-size: 14px; font-weight: 500; transition: background-color 0.12s, color 0.12s; }
    .nav a:hover { background: #f3f4f6; color: var(--text); }
    .nav a.active { background: #eef2ff; color: var(--primary); }
  `]
})
export class HeaderComponent {
  @Output() upload = new EventEmitter<void>();
}
