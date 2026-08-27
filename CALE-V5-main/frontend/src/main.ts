import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig, registerPwaServiceWorker } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig)
  .then(() => registerPwaServiceWorker())
  .catch((err) => {
    console.error(err);
    const root = document.querySelector('app-root');
    if (root) {
      root.innerHTML =
        '<div style="font-family:system-ui;padding:2rem;max-width:40rem;margin:auto">' +
        '<h1>Mi CALE no pudo iniciar</h1>' +
        '<p>Recarga con Ctrl+F5. Si continúa, borra datos del sitio para localhost:4200.</p>' +
        '<pre style="white-space:pre-wrap;color:#b00020">' +
        String((err && (err as Error).message) || err) +
        '</pre></div>';
    }
  });
