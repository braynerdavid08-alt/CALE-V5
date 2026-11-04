import { test, expect } from '@playwright/test';
import path from 'node:path';

/**
 * Captures the Phase 1 commercial directory empty/populated states.
 * Run with API + ng serve up: npx playwright test e2e/commercial-directory.spec.ts --project=chromium
 */
test.describe('Commercial directory /escuelas', () => {
  test('empty state screenshot', async ({ page }) => {
    await page.goto('/escuelas');
    await expect(page.getByRole('heading', { name: 'Escuelas CEA en Luz Verde' })).toBeVisible();
    await expect(page.getByText(/Aún no hay escuelas publicadas|Directorio comercial/i).first()).toBeVisible();

    const out = path.join('test-results', 'phase1-directorio-vacio.png');
    await page.screenshot({ path: out, fullPage: true });
  });

  test('populated layout screenshot (mocked eligible card)', async ({ page }) => {
    await page.route('**/api/public/commercial-schools**', async (route) => {
      const url = route.request().url();
      if (url.includes('/commercial-schools/') && !url.endsWith('/commercial-schools') && !url.includes('take=')) {
        await route.continue();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 101,
            name: 'CEA Demo Elegible',
            city: 'Bogotá',
            department: 'Cundinamarca',
            about:
              'Formación vial con instructores certificados, flota propia y acompañamiento en ruta para categorías A2 y B1.',
            licenseCategories: 'A2, B1',
            coverUrl: null,
            logoUrl: null,
            slug: 'cea-demo-elegible',
            detailPath: '/escuelas/cea-demo-elegible'
          }
        ])
      });
    });

    await page.goto('/escuelas');
    await expect(page.getByRole('heading', { name: 'CEA Demo Elegible' })).toBeVisible();

    const out = path.join('test-results', 'phase1-directorio-con-tarjeta.png');
    await page.screenshot({ path: out, fullPage: true });
  });

  test('school detail screenshot (mocked eligible page)', async ({ page }) => {
    const detail = {
      id: 101,
      name: 'CEA Demo Elegible',
      city: 'Bogotá',
      department: 'Cundinamarca',
      address: 'Cra 15 # 80-20',
      phone: '3001234567',
      publicEmail: 'contacto@cea-demo.test',
      whatsApp: '573001234567',
      whatsAppUrl: 'https://wa.me/573001234567',
      about:
        'Formación vial con instructores certificados, flota propia y acompañamiento en ruta para categorías A2 y B1.',
      licenseCategories: 'A2, B1',
      coverUrl: null,
      logoUrl: null,
      slug: 'cea-demo-elegible',
      detailPath: '/escuelas/cea-demo-elegible',
      mapsUrl: 'https://www.google.com/maps/search/?api=1&query=Bogota',
      posts: [{ id: 1, body: 'Abrimos inscripción A2 este mes.', mediaUrl: null, publishedAt: '2026-11-01T12:00:00Z' }],
      promotions: [
        {
          id: 1,
          title: 'Matrícula con descuento',
          body: '10% en matrícula hasta fin de mes.',
          mediaUrl: null,
          startsOn: '2026-11-01',
          endsOn: '2026-11-30'
        }
      ]
    };

    await page.route(
      (url) => /\/api\/public\/commercial-schools\/[^/?#]+/i.test(url.pathname),
      async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json; charset=utf-8',
          body: JSON.stringify(detail)
        });
      }
    );

    await page.goto('/escuelas/cea-demo-elegible');
    const title = page.locator('h1');
    await expect(title).toHaveText('CEA Demo Elegible', { timeout: 15_000 });
    // Capture as soon as the ficha renders; ng serve HMR can race a second navigation.
    const out = path.join('test-results', 'phase2-ficha-publica.png');
    await page.screenshot({ path: out, fullPage: true });
    await expect(title).toHaveText('CEA Demo Elegible');
  });

  test('school detail 404 screenshot when slug is not eligible', async ({ page }) => {
    await page.goto('/escuelas/slug-inexistente-xyz', { waitUntil: 'networkidle' });
    await expect(page.getByText(/no se encontró|no encontrada/i).first()).toBeVisible({
      timeout: 15_000
    });
    const out = path.join('test-results', 'phase2-ficha-404.png');
    await page.screenshot({ path: out, fullPage: true });
  });
});
