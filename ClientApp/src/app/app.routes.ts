import { Routes } from '@angular/router';
import { DocumentListComponent } from './components/document-list/document-list.component';
import { DocsComponent } from './components/docs/docs.component';

/**
 * Hash-based routing kullanıyoruz (app.config.ts'te `withHashLocation`):
 * IIS Express'in deep route'ları index.html'e fall-back etmemesi sorununu
 * tek satırla halleder. Production'da reverse proxy ile pretty URL'lere geçilebilir.
 */
export const routes: Routes = [
  { path: '', component: DocumentListComponent, title: 'Doküman Listesi' },
  { path: 'docs', component: DocsComponent, title: 'API Dokümantasyonu' },
  { path: '**', redirectTo: '' }
];
