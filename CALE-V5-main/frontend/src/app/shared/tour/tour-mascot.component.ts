import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MASCOT_NAME, TourPose } from '../../core/tour/tour.types';

@Component({
  selector: 'app-tour-mascot',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <img
      class="mascot"
      [class.sm]="size() === 'sm'"
      [class.md]="size() === 'md'"
      [src]="src()"
      [alt]="name"
      width="128"
      height="128"
      decoding="async"
      draggable="false" />
  `,
  styles: `
    :host { display: inline-grid; place-items: center; }
    .mascot {
      display: block;
      width: 5.5rem;
      height: 5.5rem;
      object-fit: contain;
      user-select: none;
      pointer-events: none;
    }
    .mascot.sm { width: 4rem; height: 4rem; }
    .mascot.md { width: 6.5rem; height: 6.5rem; }
  `
})
export class TourMascotComponent {
  readonly name = MASCOT_NAME;
  readonly pose = input<TourPose>('explain');
  readonly size = input<'sm' | 'md'>('md');

  private readonly reducedMotion =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  readonly src = computed(() => {
    const pose = this.pose();
    if (this.reducedMotion) return '/mascot/kuzu-explain-static.gif';
    return pose === 'celebrate' ? '/mascot/kuzu-dance-128.gif' : '/mascot/kuzu-explain-128.gif';
  });
}
