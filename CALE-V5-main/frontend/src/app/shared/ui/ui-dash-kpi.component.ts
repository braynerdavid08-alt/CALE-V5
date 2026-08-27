import { Component, Input } from '@angular/core';
import { UiIconComponent } from './ui-icon.component';

@Component({
  selector: 'ui-dash-kpi',
  standalone: true,
  imports: [UiIconComponent],
  template: `
    <article class="kpi" [attr.data-tone]="tone">
      <div class="kpi-body">
        <span class="label">{{ label }}</span>
        <strong>{{ value }}</strong>
        @if (delta !== null && delta !== undefined && delta !== '') {
          <span class="delta" [attr.data-dir]="deltaDir">
            <span aria-hidden="true">{{ deltaDir === 'down' ? '↓' : '↑' }}</span>
            {{ delta }}
          </span>
        } @else if (hint) {
          <span class="hint">{{ hint }}</span>
        }
      </div>
      @if (icon) {
        <span class="icon" aria-hidden="true">
          <ui-icon [name]="icon" />
        </span>
      }
    </article>
  `,
  styles: [`
    .kpi {
      min-height: 7.25rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 1.15rem 1.2rem;
      box-shadow: var(--shadow-sm);
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.85rem;
    }
    .kpi-body {
      display: grid;
      gap: 0.28rem;
      min-width: 0;
    }
    .label {
      color: var(--color-text-secondary);
      font-size: 0.9rem;
      font-weight: 600;
    }
    strong {
      font-size: 1.85rem;
      letter-spacing: -0.04em;
      line-height: 1.05;
      color: var(--color-text);
      font-weight: 800;
    }
    .hint {
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
    }
    .delta {
      display: inline-flex;
      align-items: center;
      gap: 0.2rem;
      font-size: var(--text-xs);
      font-weight: 700;
      color: var(--color-success);
    }
    .delta[data-dir='down'] { color: var(--color-danger); }
    .icon {
      width: 3.1rem;
      height: 3.1rem;
      border-radius: 999px;
      display: grid;
      place-items: center;
      flex-shrink: 0;
      background: var(--color-primary-soft);
      color: var(--color-primary);
    }
    .icon ::ng-deep svg {
      width: 1.35rem;
      height: 1.35rem;
    }
    .kpi[data-tone='primary'] .icon { background: var(--color-primary-soft); color: var(--color-primary); }
    .kpi[data-tone='success'] .icon { background: var(--color-success-soft); color: var(--color-success); }
    .kpi[data-tone='warning'] .icon { background: var(--color-warning-soft); color: var(--color-warning); }
    .kpi[data-tone='info'] .icon { background: var(--color-info-soft); color: var(--color-info); }
    .kpi[data-tone='orange'] .icon { background: var(--color-warning-soft); color: var(--color-warning); }
    @media (max-width: 600px) {
      .kpi {
        min-height: 5.5rem;
        padding: 0.95rem 1rem;
      }
      strong { font-size: 1.55rem; }
      .icon { width: 2.6rem; height: 2.6rem; }
    }
  `]
})
export class UiDashKpiComponent {
  @Input({ required: true }) label = '';
  @Input() value: string | number = '';
  @Input() hint = '';
  /** e.g. "+12.5% este mes" */
  @Input() delta: string | null = null;
  @Input() icon = '';
  @Input() tone: 'neutral' | 'primary' | 'success' | 'warning' | 'info' | 'orange' = 'primary';

  get deltaDir(): 'up' | 'down' {
    const raw = String(this.delta ?? '');
    return raw.trim().startsWith('-') ? 'down' : 'up';
  }
}
