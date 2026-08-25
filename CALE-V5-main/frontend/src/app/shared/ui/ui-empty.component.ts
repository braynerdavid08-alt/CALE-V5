import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-empty',
  standalone: true,
  templateUrl: './ui-empty.component.html',
  styleUrl: './ui-empty.component.css'
})
export class UiEmptyComponent {
  @Input({ required: true }) title = '';
  @Input() message = '';
}
