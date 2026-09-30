import { Component } from '@angular/core';

@Component({
  selector: 'app-student-donations-page',
  standalone: true,
  styles: [`
    :host { display: block; }
    .donate {
      width: min(980px, 100%);
      margin: 0 auto;
      padding: clamp(1rem, 3vw, 2rem) clamp(0.75rem, 3vw, 1.5rem) 3rem;
      display: grid;
      gap: 1.25rem;
    }
    .hero {
      position: relative;
      overflow: hidden;
      border-radius: 1.4rem;
      padding: clamp(1.4rem, 4vw, 2.2rem);
      color: #fff;
      background:
        radial-gradient(120% 90% at 0% 0%, rgba(244, 63, 94, 0.35), transparent 60%),
        radial-gradient(90% 80% at 100% 100%, rgba(78, 182, 212, 0.3), transparent 60%),
        linear-gradient(150deg, #13263a, #0a1a2b);
    }
    .kicker {
      margin: 0 0 0.4rem;
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #fda4af;
    }
    .hero h1 { margin: 0 0 0.5rem; font-size: clamp(1.6rem, 4vw, 2.2rem); line-height: 1.15; color: #fff; }
    .hero p { margin: 0; max-width: 40rem; line-height: 1.55; color: rgba(226, 239, 247, 0.9); }
    .grid {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr);
      gap: 1.25rem;
      align-items: start;
    }
    .card {
      border: 1px solid var(--color-border);
      border-radius: 1.2rem;
      background: var(--color-surface);
      padding: clamp(1.1rem, 3vw, 1.6rem);
    }
    .qr-card { display: grid; justify-items: center; gap: 0.9rem; text-align: center; }
    .qr-frame {
      width: min(320px, 100%);
      padding: 0.6rem;
      border-radius: 1.1rem;
      background: #fff;
      box-shadow: 0 12px 30px rgba(0, 0, 0, 0.18);
    }
    .qr-frame img { display: block; width: 100%; height: auto; }
    .qr-card strong { font-size: var(--text-lg); }
    .qr-card small { color: var(--color-text-secondary); line-height: 1.45; }
    .download {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.6rem 1.1rem;
      border-radius: 999px;
      font-weight: 700;
      text-decoration: none;
      color: var(--color-on-primary);
      background: var(--color-primary);
    }
    .download:hover { filter: brightness(1.08); }
    h2 { margin: 0 0 0.8rem; font-size: var(--text-lg); }
    ol, ul { margin: 0; padding: 0; list-style: none; display: grid; gap: 0.7rem; }
    .step { display: grid; grid-template-columns: 2rem 1fr; gap: 0.7rem; align-items: start; line-height: 1.45; }
    .num {
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      font-weight: 800;
      color: #fff;
      background: linear-gradient(135deg, #f43f5e, #fb7185);
    }
    .uses li { display: flex; gap: 0.6rem; line-height: 1.45; }
    .uses span[aria-hidden] { font-size: 1.2rem; line-height: 1.3; }
    .section + .section { margin-top: 1.4rem; padding-top: 1.2rem; border-top: 1px solid var(--color-border); }
    .thanks {
      text-align: center;
      padding: 1.1rem;
      border-radius: 1.1rem;
      background: color-mix(in srgb, #f43f5e 10%, var(--color-surface));
      border: 1px solid color-mix(in srgb, #f43f5e 30%, var(--color-border));
      line-height: 1.5;
    }
    .thanks p { margin: 0; }
    @media (max-width: 760px) {
      .grid { grid-template-columns: 1fr; }
    }
  `],
  template: `
    <section class="donate">
      <header class="hero">
        <p class="kicker">Donaciones</p>
        <h1>Apoya Mi CALE 💙</h1>
        <p>
          Mi CALE es gratis para los estudiantes. Mantenerla en línea cuesta dinero cada mes:
          servidor, base de datos y dominio. Si la app te está ayudando a prepararte,
          puedes donar lo que quieras para que siga activa.
        </p>
      </header>

      <div class="grid">
        <div class="card qr-card">
          <div class="qr-frame">
            <img src="donations/nequi-qr.png" alt="Código QR para donar con Nequi o Bre-B" width="320" height="380" />
          </div>
          <strong>Escanea para donar</strong>
          <small>Funciona con Nequi y con cualquier app de banco compatible con Bre-B. Tú eliges el valor.</small>
          <a class="download" href="donations/nequi-qr.png" download="donar-mi-cale-qr.png">⬇ Descargar QR</a>
        </div>

        <div class="card">
          <div class="section">
            <h2>¿Cómo donar?</h2>
            <ol>
              <li class="step"><span class="num">1</span><span>Abre Nequi o la app de tu banco y busca la opción <strong>Pagar con QR</strong> o <strong>Bre-B</strong>.</span></li>
              <li class="step"><span class="num">2</span><span>Escanea el código. Si estás en el celular, descarga la imagen y súbela desde la galería.</span></li>
              <li class="step"><span class="num">3</span><span>Escribe el valor que quieras donar y confirma. ¡Listo!</span></li>
            </ol>
          </div>
          <div class="section">
            <h2>¿En qué se usa tu donación?</h2>
            <ul class="uses">
              <li><span aria-hidden="true">🖥️</span><span>Pagar el servidor y la base de datos para que la página no se caiga.</span></li>
              <li><span aria-hidden="true">📚</span><span>Agregar más preguntas, señales y material de estudio.</span></li>
              <li><span aria-hidden="true">🎮</span><span>Crear nuevos juegos y mejoras para practicar.</span></li>
            </ul>
          </div>
        </div>
      </div>

      <div class="thanks">
        <p><strong>¡Gracias!</strong> Donar es totalmente voluntario: todas las funciones siguen siendo gratis para ti, dones o no.</p>
      </div>
    </section>
  `
})
export class StudentDonationsPage {}
