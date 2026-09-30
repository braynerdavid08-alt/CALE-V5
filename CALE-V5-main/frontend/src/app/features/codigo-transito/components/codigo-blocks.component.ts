import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CodigoBlock, CodigoInline } from '../codigo-transito.api';

interface Span {
  text: string;
  bold: boolean;
  strike: boolean;
  underline: boolean;
}

/** Renders Transiteca content blocks literally: notes, boxes, nested children and inline marks. */
@Component({
  selector: 'codigo-blocks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (b of blocks; track $index) {
      <div class="block" [attr.data-type]="b.type || 'paragraph'" [attr.data-variant]="b.variant || ''">
        <p class="text">
          @for (s of spans(b); track $index) {
            <span
              [class.bold]="s.bold"
              [class.strike]="s.strike"
              [class.underline]="s.underline"
              [attr.title]="s.strike ? 'Texto tachado en la norma original' : null">{{ s.text }}</span>
          }
        </p>
        @if (b.children?.length) {
          <div class="children">
            <codigo-blocks [blocks]="b.children!" />
          </div>
        }
      </div>
    }
  `,
  styles: [`
    :host { display: grid; gap: 0.85rem; }
    .block { display: grid; gap: 0.6rem; }
    .text { margin: 0; line-height: 1.6; white-space: pre-wrap; }
    .bold { font-weight: 800; }
    .underline { text-decoration: underline; }
    .strike {
      text-decoration: line-through;
      text-decoration-thickness: 1px;
      color: var(--color-text-secondary);
      opacity: 0.85;
    }
    .strike.underline { text-decoration: line-through underline; }

    .block[data-type='note'] {
      padding: 0.55rem 0.8rem;
      border-radius: 8px;
      border: 1px solid color-mix(in srgb, #d97706 45%, transparent);
      background: color-mix(in srgb, #d97706 16%, transparent);
    }
    .block[data-type='note'] .text {
      font-size: 0.9rem;
      font-weight: 600;
      line-height: 1.45;
    }
    .block[data-type='note'][data-variant='modificado'] .text { color: color-mix(in srgb, #f59e0b 75%, var(--color-text)); }
    .block[data-type='note'][data-variant='inexequible'],
    .block[data-type='note'][data-variant='derogado'] {
      border-color: color-mix(in srgb, #dc2626 45%, transparent);
      background: color-mix(in srgb, #dc2626 16%, transparent);
    }
    .block[data-type='note'][data-variant='inexequible'] .text,
    .block[data-type='note'][data-variant='derogado'] .text { color: color-mix(in srgb, #f87171 70%, var(--color-text)); }
    .block[data-type='note'][data-variant='adicionado'] {
      border-color: color-mix(in srgb, #059669 45%, transparent);
      background: color-mix(in srgb, #059669 16%, transparent);
    }
    .block[data-type='note'][data-variant='adicionado'] .text { color: color-mix(in srgb, #34d399 65%, var(--color-text)); }
    .block[data-type='note'][data-variant='condicionalmente'] {
      border-color: color-mix(in srgb, #2563eb 45%, transparent);
      background: color-mix(in srgb, #2563eb 16%, transparent);
    }
    .block[data-type='note'][data-variant='condicionalmente'] .text { color: color-mix(in srgb, #60a5fa 70%, var(--color-text)); }
    .block[data-type='note'][data-variant='editor'] {
      border-color: var(--color-border);
      background: color-mix(in srgb, var(--color-text) 6%, transparent);
    }

    .block[data-type='box'][data-variant='paragrafo'] {
      padding: 0.85rem 1rem;
      border-radius: 8px;
      border-left: 4px solid var(--color-primary);
      background: color-mix(in srgb, var(--color-primary) 10%, var(--color-surface));
    }

    .children {
      padding-left: 1.1rem;
      border-left: 2px solid var(--color-border);
    }
    @media (max-width: 600px) {
      .children { padding-left: 0.7rem; }
    }
  `]
})
export class CodigoBlocksComponent {
  @Input({ required: true }) blocks: CodigoBlock[] = [];

  spans(b: CodigoBlock): Span[] {
    const c = b.content;
    if (!c) return [];
    if (typeof c === 'string') return [{ text: c, bold: false, strike: false, underline: false }];
    if (!Array.isArray(c)) return [];
    return c
      .map((x): Span | null => {
        if (typeof x === 'string') return { text: x, bold: false, strike: false, underline: false };
        const item = x as CodigoInline | null;
        if (!item || typeof item.text !== 'string') return null;
        const marks = item.marks ?? [];
        return {
          text: item.text,
          bold: marks.includes('bold'),
          strike: marks.includes('strike'),
          underline: marks.includes('underline')
        };
      })
      .filter((s): s is Span => !!s && s.text.length > 0);
  }
}
