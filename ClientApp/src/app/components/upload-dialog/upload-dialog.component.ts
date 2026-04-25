import { CommonModule, DatePipe } from '@angular/common';
import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DOCUMENT_TYPES, DocumentType, DuplicateInfo } from '../../models/document';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';

type UploadStep = 'pick' | 'duplicate-warning' | 'uploading' | 'done';

@Component({
  selector: 'app-upload-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './upload-dialog.component.html',
  styleUrl: './upload-dialog.component.css'
})
export class UploadDialogComponent {
  private readonly api = inject(DocumentsService);
  private readonly notify = inject(NotificationService);

  @Output() closed = new EventEmitter<void>();
  @Output() uploaded = new EventEmitter<void>();

  readonly docTypes = DOCUMENT_TYPES;

  // Form
  title = '';
  documentType: DocumentType = 'Contract';
  uploadedBy = '';
  file: File | null = null;

  // State
  readonly step = signal<UploadStep>('pick');
  readonly hashing = signal(false);
  readonly fileHash = signal<string | null>(null);
  readonly duplicate = signal<DuplicateInfo | null>(null);
  readonly errorMsg = signal<string | null>(null);

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const f = input.files?.[0];
    if (!f) return;
    this.file = f;
    if (!this.title) this.title = this.suggestTitleFromFilename(f.name);

    // Pre-upload duplicate check — sunucuya tüm dosyayı gönderdikten sonra
    // 409 dönmek yerine, ön-kontrol için browser'da hash hesaplıyoruz.
    this.hashing.set(true);
    this.errorMsg.set(null);
    try {
      const hash = await this.api.hashFile(f);
      this.fileHash.set(hash);
      this.api.checkDuplicate(hash).subscribe({
        next: (dup) => {
          if (dup.isDuplicate) {
            this.duplicate.set(dup);
            this.step.set('duplicate-warning');
          }
        },
        error: () => { /* dup check hata verirse upload sırasında server tarafı yine kontrol edecek */ }
      });
    } catch (e) {
      this.errorMsg.set('Dosya okunurken hata oluştu.');
    } finally {
      this.hashing.set(false);
    }
  }

  canSubmit(): boolean {
    return !!this.file && !!this.title.trim() && !!this.uploadedBy.trim() && this.step() === 'pick';
  }

  submit(force = false): void {
    if (!this.file) return;
    this.step.set('uploading');
    this.errorMsg.set(null);
    this.api.upload(this.file, {
      title: this.title.trim(),
      documentType: this.documentType,
      uploadedBy: this.uploadedBy.trim(),
      force
    }).subscribe({
      next: (res) => {
        this.step.set('done');
        this.notify.success(res.message ?? 'Yüklendi.');
        this.uploaded.emit();
        setTimeout(() => this.close(), 800);
      },
      error: (err) => {
        // Server tarafı dup yakaladıysa (409) — pre-check kaçırmış olabilir
        if (err?.status === 409 && err?.error?.duplicate) {
          this.duplicate.set(err.error.duplicate);
          this.step.set('duplicate-warning');
          return;
        }
        this.errorMsg.set(err?.error?.message ?? 'Yükleme başarısız.');
        this.step.set('pick');
      }
    });
  }

  cancelDuplicate(): void {
    this.duplicate.set(null);
    this.fileHash.set(null);
    this.file = null;
    this.step.set('pick');
  }

  close(): void { this.closed.emit(); }

  private suggestTitleFromFilename(name: string): string {
    return name.replace(/\.[^/.]+$/, '').replace(/[_-]+/g, ' ').trim();
  }
}
