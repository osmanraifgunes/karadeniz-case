import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DuplicateInfo, SearchParams, SearchResponse, UploadResult } from '../models/document';

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly http = inject(HttpClient);
  /** Dev'de proxy.conf.json /api'yi IIS Express'e yönlendiriyor.
   *  Production'da reverse proxy ile aynı origin servis ediliyor. */
  private readonly base = '/api/documents';

  search(params: SearchParams): Observable<SearchResponse> {
    let p = new HttpParams();
    if (params.q) p = p.set('q', params.q);
    if (params.type) p = p.set('type', params.type);
    if (params.from) p = p.set('from', params.from);
    if (params.to) p = p.set('to', params.to);
    if (params.uploader) p = p.set('uploader', params.uploader);
    if (params.page) p = p.set('page', params.page.toString());
    if (params.pageSize) p = p.set('pageSize', params.pageSize.toString());
    return this.http.get<SearchResponse>(this.base, { params: p });
  }

  /** İstemci tarafında SHA-256 hesaplayıp upload öncesi check eder.
   *  Avantaj: Büyük dosyayı sunucuya göndermeden duplicate olduğunu yakalarız. */
  async hashFile(file: File): Promise<string> {
    const buffer = await file.arrayBuffer();
    const digest = await crypto.subtle.digest('SHA-256', buffer);
    return Array.from(new Uint8Array(digest))
      .map((b) => b.toString(16).padStart(2, '0'))
      .join('');
  }

  checkDuplicate(hash: string): Observable<DuplicateInfo> {
    return this.http.get<DuplicateInfo>(`${this.base}/check-duplicate`, {
      params: new HttpParams().set('hash', hash)
    });
  }

  upload(file: File, meta: { title: string; documentType: string; uploadedBy: string; force?: boolean }): Observable<UploadResult> {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('title', meta.title);
    fd.append('documentType', meta.documentType);
    fd.append('uploadedBy', meta.uploadedBy);
    if (meta.force) fd.append('force', 'true');
    return this.http.post<UploadResult>(`${this.base}/upload`, fd);
  }
}
