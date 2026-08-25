import { Component, EventEmitter, Input, Output } from '@angular/core';
import { resolveMediaUrl } from '../../core/media/resolve-media-url';
import { UiButtonComponent } from './ui-button.component';

@Component({
  selector: 'ui-image-picker',
  standalone: true,
  imports: [UiButtonComponent],
  template: `
    <div class="picker">
      @if (src) {
        <img [src]="media(src)" [alt]="alt" />
      }
      <div class="row">
        <label class="upload">
          {{ src ? 'Cambiar imagen' : 'Agregar imagen' }}
          <input
            type="file"
            accept="image/*"
            [disabled]="busy"
            (change)="onFile($event)" />
        </label>
        @if (src) {
          <ui-button type="button" variant="ghost" (click)="cleared.emit()">
            Quitar
          </ui-button>
        }
      </div>
      @if (busy) {
        <small>Subiendo imagen…</small>
      }
    </div>
  `,
  styles: [`
    .picker { display: grid; gap: 0.5rem; }
    img {
      max-width: min(280px, 100%);
      max-height: 180px;
      object-fit: contain;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-chip);
      padding: 0.35rem;
    }
    .row { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; }
    .upload {
      display: inline-flex;
      align-items: center;
      min-height: 2.4rem;
      padding: 0.4rem 0.9rem;
      border-radius: var(--radius-sm);
      border: 1px dashed var(--color-primary);
      color: var(--color-primary);
      font-weight: 650;
      font-size: var(--text-sm);
      cursor: pointer;
      background: var(--color-primary-soft);
    }
    .upload input { display: none; }
    small { color: var(--color-text-secondary); }
  `]
})
export class UiImagePickerComponent {
  @Input() src: string | null = null;
  @Input() busy = false;
  @Input() alt = '';
  @Output() selected = new EventEmitter<File>();
  @Output() cleared = new EventEmitter<void>();
  readonly media = resolveMediaUrl;

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) {
      this.selected.emit(file);
    }
  }
}
