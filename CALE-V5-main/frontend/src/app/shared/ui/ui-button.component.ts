import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { RouterLink, Params } from '@angular/router';

@Component({
  selector: 'ui-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  templateUrl: './ui-button.component.html',
  styleUrl: './ui-button.component.css'
})
export class UiButtonComponent {
  @Input() type: 'button' | 'submit' = 'button';
  @Input() variant: 'primary' | 'secondary' | 'ghost' | 'danger' = 'primary';
  @Input() disabled = false;
  @Input() loading = false;
  /** When set, renders a semantic link styled as a button (avoids nested button-in-anchor). */
  @Input() routerLink: string | any[] | null = null;
  @Input() queryParams: Params | null = null;
}
