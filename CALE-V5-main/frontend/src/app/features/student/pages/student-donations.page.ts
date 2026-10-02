import { Component, computed, inject, signal } from '@angular/core';
import { SessionStore } from '../../../core/auth/session.store';

const SHARE_TEXT =
  'Estoy usando Luz Verde para practicar gratis el examen de conducción. Es gratis para estudiantes, instructores y escuelas:';

@Component({
  selector: 'app-student-donations-page',
  standalone: true,
  styles: [`
    :host { display: block; }
    .donate {
      width: min(1040px, 100%);
      margin: 0 auto;
      padding: clamp(1rem, 3vw, 2rem) clamp(0.75rem, 3vw, 1.5rem) 3rem;
      display: grid;
      gap: 1.25rem;
    }
    .hero {
      position: relative;
      overflow: hidden;
      border-radius: 1.4rem;
      padding: clamp(1.4rem, 4vw, 2.4rem);
      color: #fff;
      background:
        radial-gradient(120% 90% at 0% 0%, rgba(244, 63, 94, 0.38), transparent 60%),
        radial-gradient(90% 80% at 100% 100%, rgba(16, 185, 129, 0.28), transparent 60%),
        linear-gradient(150deg, #13263a, #0a1a2b);
    }
    .kicker {
      margin: 0 0 0.45rem;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #fda4af;
    }
    .hero h1 { margin: 0 0 0.6rem; font-size: clamp(1.7rem, 4.5vw, 2.5rem); line-height: 1.12; color: #fff; }
    .hero p { margin: 0; max-width: 44rem; line-height: 1.6; color: rgba(226, 239, 247, 0.92); }
    .chips { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 1.1rem; }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.4rem 0.8rem;
      border-radius: 999px;
      font-size: var(--text-sm);
      font-weight: 700;
      background: rgba(255, 255, 255, 0.1);
      border: 1px solid rgba(255, 255, 255, 0.18);
      color: #fff;
    }
    .grid {
      display: grid;
      grid-template-columns: minmax(0, 0.95fr) minmax(0, 1.05fr);
      gap: 1.25rem;
      align-items: start;
    }
    .card {
      border: 1px solid var(--color-border);
      border-radius: 1.25rem;
      background: var(--color-surface);
      padding: clamp(1.1rem, 3vw, 1.6rem);
    }
    .qr-card {
      display: grid;
      justify-items: center;
      gap: 0.85rem;
      text-align: center;
      border-color: color-mix(in srgb, #da0081 30%, var(--color-border));
      background:
        radial-gradient(120% 70% at 50% 0%, color-mix(in srgb, #da0081 10%, transparent), transparent 70%),
        var(--color-surface);
    }
    .qr-title { margin: 0; font-size: var(--text-xl); }
    .qr-sub { margin: -0.4rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .qr-frame {
      width: min(300px, 100%);
      padding: 0.6rem;
      border-radius: 1.2rem;
      background: #fff;
      box-shadow: 0 14px 34px rgba(0, 0, 0, 0.18);
    }
    .qr-frame img { display: block; width: 100%; height: auto; }
    .amounts { display: flex; flex-wrap: wrap; justify-content: center; gap: 0.4rem; }
    .amount {
      padding: 0.35rem 0.75rem;
      border-radius: 999px;
      font-size: var(--text-sm);
      font-weight: 700;
      background: color-mix(in srgb, #da0081 10%, var(--color-surface));
      color: var(--color-text);
      border: 1px solid color-mix(in srgb, #da0081 25%, var(--color-border));
    }
    .hint { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); line-height: 1.5; }
    .actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 0.5rem; width: 100%; }
    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      min-height: 2.75rem;
      padding: 0.6rem 1.15rem;
      border-radius: 999px;
      font: inherit;
      font-weight: 800;
      text-decoration: none;
      cursor: pointer;
      border: 1px solid transparent;
    }
    .btn-primary { color: #fff; background: linear-gradient(135deg, #da0081, #f43f5e); }
    .btn-primary:hover { filter: brightness(1.08); }
    .btn-ghost { color: var(--color-text); background: transparent; border-color: var(--color-border); }
    .btn-ghost:hover { background: color-mix(in srgb, var(--color-primary) 8%, transparent); }
    h2 { margin: 0 0 0.8rem; font-size: var(--text-lg); }
    ol, ul { margin: 0; padding: 0; list-style: none; display: grid; gap: 0.75rem; }
    .step { display: grid; grid-template-columns: 2rem 1fr; gap: 0.7rem; align-items: start; line-height: 1.5; }
    .num {
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      font-weight: 800;
      color: #fff;
      background: linear-gradient(135deg, #da0081, #f43f5e);
    }
    .uses li { display: flex; gap: 0.6rem; line-height: 1.5; }
    .uses span[aria-hidden] { font-size: 1.2rem; line-height: 1.3; }
    .section + .section { margin-top: 1.4rem; padding-top: 1.2rem; border-top: 1px solid var(--color-border); }
    .role-note {
      margin: 0;
      padding: 0.85rem 1rem;
      border-radius: 0.9rem;
      line-height: 1.5;
      background: color-mix(in srgb, var(--color-primary) 8%, var(--color-surface));
      border: 1px solid color-mix(in srgb, var(--color-primary) 22%, var(--color-border));
    }
    .share {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.9rem;
    }
    .share h2 { margin: 0 0 0.25rem; }
    .share .hint { max-width: 36rem; }
    .thanks {
      text-align: center;
      padding: 1.1rem;
      border-radius: 1.1rem;
      background: color-mix(in srgb, #f43f5e 10%, var(--color-surface));
      border: 1px solid color-mix(in srgb, #f43f5e 30%, var(--color-border));
      line-height: 1.55;
    }
    .thanks p { margin: 0; }
    .copied { font-size: var(--text-sm); color: var(--color-success); font-weight: 700; }
    @media (max-width: 820px) {
      .grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 520px) {
      .actions .btn, .share .btn { width: 100%; }
      .share { flex-direction: column; align-items: stretch; }
    }
  `],
  template: `
    <section class="donate">
      <header class="hero">
        <p class="kicker">Apoyar Luz Verde</p>
        <h1>Luz Verde es gratis para todos 💚</h1>
        <p>
          Estudiantes, instructores y escuelas usan todas las funciones sin pagar nada.
          Mantener la app en línea cuesta dinero cada mes (servidor, base de datos y dominio),
          así que Luz Verde vive de las donaciones de quienes la usan.
        </p>
        <div class="chips">
          <span class="chip">✅ Todo gratis</span>
          <span class="chip">💸 Donación voluntaria</span>
          <span class="chip">🔒 Pago directo por Nequi / Bre-B</span>
        </div>
      </header>

      <div class="grid">
        <div class="card qr-card">
          <h2 class="qr-title">Dona con Nequi</h2>
          <p class="qr-sub">Escanea el código y escribe el valor que quieras</p>
          <div class="qr-frame">
            <img src="donations/nequi-qr.png" alt="Código QR para donar a Luz Verde con Nequi o Bre-B" width="300" height="356" />
          </div>
          <div class="amounts" aria-label="Ideas de aporte">
            <span class="amount">$5.000</span>
            <span class="amount">$10.000</span>
            <span class="amount">$20.000</span>
            <span class="amount">Lo que quieras</span>
          </div>
          <p class="hint">Funciona con Nequi y con cualquier banco compatible con Bre-B.</p>
          <div class="actions">
            <a class="btn btn-primary" href="donations/nequi-qr.png" download="donar-luz-verde-nequi.png">⬇ Descargar QR</a>
          </div>
        </div>

        <div class="card">
          <div class="section">
            <h2>¿Cómo donar?</h2>
            <ol>
              <li class="step"><span class="num">1</span><span>Abre <strong>Nequi</strong> o la app de tu banco y elige <strong>Pagar con QR</strong> o <strong>Bre-B</strong>.</span></li>
              <li class="step"><span class="num">2</span><span><strong>En computador:</strong> escanea el código con tu celular. <strong>En el celular:</strong> toca «Descargar QR» y súbelo desde la galería.</span></li>
              <li class="step"><span class="num">3</span><span>Escribe el valor que quieras donar y confirma. ¡Listo, gracias!</span></li>
            </ol>
          </div>
          <div class="section">
            <h2>¿En qué se usa tu donación?</h2>
            <ul class="uses">
              <li><span aria-hidden="true">🖥️</span><span>Pagar el servidor y la base de datos para que Luz Verde no se caiga.</span></li>
              <li><span aria-hidden="true">📚</span><span>Agregar más preguntas, señales y material de estudio.</span></li>
              <li><span aria-hidden="true">🎮</span><span>Crear nuevos juegos y herramientas para clase.</span></li>
            </ul>
          </div>
          <div class="section">
            <p class="role-note">{{ roleNote() }}</p>
          </div>
        </div>
      </div>

      <div class="card share">
        <div>
          <h2>¿No puedes donar? Comparte Luz Verde</h2>
          <p class="hint">Recomendar la app a otros estudiantes, instructores o escuelas también ayuda muchísimo.</p>
        </div>
        <div class="actions" style="width: auto;">
          <button type="button" class="btn btn-ghost" (click)="share()">📣 Compartir Luz Verde</button>
          @if (copied()) {
            <span class="copied" role="status">Enlace copiado</span>
          }
        </div>
      </div>

      <div class="thanks">
        <p><strong>¡Gracias!</strong> Donar es totalmente voluntario: todas las funciones siguen siendo gratis, dones o no.</p>
      </div>
    </section>
  `
})
export class StudentDonationsPage {
  private readonly session = inject(SessionStore);
  readonly copied = signal(false);

  readonly roleNote = computed(() => {
    const role = this.session.user()?.role;
    if (role === 'School') {
      return 'Si tu escuela usa Luz Verde con sus aprendices e instructores, un aporte mensual nos ayuda a mantenerla gratis y sin límites para todos.';
    }
    if (role === 'Teacher') {
      return 'Si usas Luz Verde en tus clases (Aula en Vivo, 100 Estudiantes Dijeron, presentaciones), tu apoyo mantiene esas herramientas gratis.';
    }
    return 'Si Luz Verde te está ayudando a prepararte para el examen, cualquier aporte, por pequeño que sea, hace la diferencia.';
  });

  async share(): Promise<void> {
    const url = window.location.origin;
    const nav = navigator as Navigator & { share?: (data: ShareData) => Promise<void> };
    if (nav.share) {
      try {
        await nav.share({ title: 'Luz Verde', text: SHARE_TEXT, url });
        return;
      } catch (err) {
        if ((err as DOMException)?.name === 'AbortError') {
          return;
        }
      }
    }
    try {
      await navigator.clipboard.writeText(`${SHARE_TEXT} ${url}`);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2500);
    } catch {
      window.open(`https://wa.me/?text=${encodeURIComponent(`${SHARE_TEXT} ${url}`)}`, '_blank', 'noopener');
    }
  }
}
