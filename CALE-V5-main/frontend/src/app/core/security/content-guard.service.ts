import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject } from '@angular/core';
import { SessionStore } from '../auth/session.store';

/** Instructors and schools are guarded too: only the platform owner can copy freely. */
const STAFF_ROLES = new Set(['Admin']);
const GUARD_CLASS = 'content-guard';

/**
 * Discourages copying course material: no right-click, copy, drag, printing or DevTools shortcuts.
 * A determined user can still bypass it; what really protects the content is the server
 * (answers never leave the API and videos require a session).
 */
@Injectable({ providedIn: 'root' })
export class ContentGuardService {
  private readonly document = inject(DOCUMENT);
  private readonly session = inject(SessionStore);
  private active = false;
  private warned = false;

  start(): void {
    const doc = this.document;
    doc.addEventListener('contextmenu', (e) => this.block(e), true);
    doc.addEventListener('copy', (e) => this.block(e), true);
    doc.addEventListener('cut', (e) => this.block(e), true);
    doc.addEventListener('dragstart', (e) => {
      const tag = (e.target as HTMLElement | null)?.tagName;
      if (this.active && (tag === 'IMG' || tag === 'VIDEO' || tag === 'A')) {
        e.preventDefault();
      }
    }, true);
    doc.addEventListener('keydown', (e) => this.onKey(e), true);

    effect(() => {
      const role = this.session.user()?.role ?? '';
      this.active = !STAFF_ROLES.has(role);
      doc.body.classList.toggle(GUARD_CLASS, this.active);
      if (this.active) {
        this.warnConsole();
      }
    });
  }

  private block(e: Event): void {
    if (this.active && !this.isEditable(e.target)) {
      e.preventDefault();
    }
  }

  private onKey(e: KeyboardEvent): void {
    if (!this.active) {
      return;
    }
    const key = e.key.toLowerCase();
    const ctrl = e.ctrlKey || e.metaKey;
    const devTools = key === 'f12'
      || (ctrl && e.shiftKey && ['i', 'j', 'c', 'k'].includes(key))
      || (e.metaKey && e.altKey && ['i', 'j', 'c', 'u'].includes(key));
    const savePrintSource = ctrl && !e.shiftKey && ['s', 'p', 'u'].includes(key);
    if (devTools || savePrintSource) {
      e.preventDefault();
      e.stopPropagation();
    }
  }

  private isEditable(target: EventTarget | null): boolean {
    const el = target as HTMLElement | null;
    return !!el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable);
  }

  private warnConsole(): void {
    if (this.warned) {
      return;
    }
    this.warned = true;
    console.log(
      '%c¡Alto!',
      'color:#c62828;font-size:42px;font-weight:800'
    );
    console.log(
      '%cEl contenido de Luz Verde está protegido por derechos de autor. Copiarlo o distribuirlo sin autorización está prohibido. '
        + 'Si alguien te pidió pegar algo aquí, es un intento de robar tu cuenta.',
      'font-size:16px'
    );
  }
}
