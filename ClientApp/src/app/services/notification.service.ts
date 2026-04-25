import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info' | 'warning';
export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  /** Signal — UI auto-render. */
  readonly toasts = signal<Toast[]>([]);
  private nextId = 1;

  show(kind: ToastKind, message: string, durationMs = 4000): void {
    const id = this.nextId++;
    this.toasts.update((t) => [...t, { id, kind, message }]);
    setTimeout(() => this.dismiss(id), durationMs);
  }

  success(msg: string) { this.show('success', msg); }
  error(msg: string) { this.show('error', msg, 6000); }
  info(msg: string) { this.show('info', msg); }
  warning(msg: string) { this.show('warning', msg, 5000); }

  dismiss(id: number): void {
    this.toasts.update((t) => t.filter((x) => x.id !== id));
  }
}
