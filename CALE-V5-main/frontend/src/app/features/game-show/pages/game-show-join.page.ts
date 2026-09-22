import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

/**
 * Legacy entry — GameShow codes now enter through Aula en vivo (/live/join).
 */
@Component({
  selector: 'app-game-show-join-page',
  standalone: true,
  template: `<p class="redir">Redirigiendo a Aula en vivo…</p>`,
  styles: [`
    .redir {
      margin: 3rem auto;
      text-align: center;
      color: var(--color-text-secondary);
    }
  `]
})
export class GameShowJoinPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    if (code) {
      void this.router.navigate(['/live/join', code.toUpperCase()]);
      return;
    }
    void this.router.navigate(['/live/join']);
  }
}
