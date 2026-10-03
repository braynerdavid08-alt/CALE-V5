import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TheoryStudentDashboardDto } from '../api/student-theory.api';

/** Stays visible while the student is cleared for the theory exam but has not booked it yet. */
@Component({
  selector: 'app-next-step-banner',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (show()) {
      <section class="next-step" role="status" aria-live="polite">
        <span class="icon" aria-hidden="true">🎉</span>
        <div class="text">
          <strong>¡Ya puedes agendar tu examen teórico!</strong>
          <p>
            {{ hoursDone() ? 'Completaste tus horas de teoría y taller y quedaste habilitado.' : 'Tu escuela te habilitó.' }}
            Escoge el día y la hora de tu examen.
          </p>
        </div>
        <a class="cta" routerLink="/student/exam">Agendar mi examen</a>
      </section>
    }
  `,
  styles: [`
    .next-step {
      display: flex;
      align-items: center;
      gap: var(--spacing-md);
      flex-wrap: wrap;
      padding: var(--spacing-lg);
      border-radius: var(--radius-lg);
      border: 2px solid var(--color-primary);
      background: linear-gradient(135deg, var(--color-welcome-start), var(--color-welcome-end));
      box-shadow: var(--shadow-md);
      animation: pop .35s ease-out;
    }
    .icon { font-size: 2.25rem; line-height: 1; }
    .text { flex: 1 1 16rem; min-width: 0; }
    .text strong { display: block; font-size: var(--text-lg); color: var(--color-text); }
    .text p { margin: .25rem 0 0; color: var(--color-text-secondary); }
    .cta {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 3.25rem;
      padding: 0 var(--spacing-lg);
      border-radius: var(--radius-md);
      background: var(--color-primary);
      color: var(--color-on-primary);
      font-weight: 700;
      font-size: var(--text-md);
      text-decoration: none;
    }
    .cta:hover { background: var(--color-primary-hover); }
    @media (max-width: 600px) {
      .cta { flex: 1 1 100%; }
    }
    @keyframes pop {
      from { transform: translateY(6px); opacity: 0; }
      to { transform: none; opacity: 1; }
    }
  `]
})
export class NextStepBannerComponent {
  readonly theory = input<TheoryStudentDashboardDto | null>(null);

  readonly hoursDone = computed(() => {
    const t = this.theory();
    return !!t && t.hoursCompleted >= t.hoursRequired && t.workshopHoursCompleted >= t.workshopHoursRequired;
  });

  readonly show = computed(() => {
    const t = this.theory();
    const pe = t?.practicalEligibility;
    return !!pe?.theoryExamAuthorized && !pe.theoryExamPassed && !t?.nextExamAppointment;
  });
}
