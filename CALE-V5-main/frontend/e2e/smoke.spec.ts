import { expect, test } from '@playwright/test';

test.describe('Public smoke', () => {
  test('landing loads with brand and login entry', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('link', { name: /iniciar sesión|entrar/i }).first()).toBeVisible({
      timeout: 15000
    });
  });

  test('login page shows email and password fields', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByLabel(/correo/i)).toBeVisible({ timeout: 15000 });
    await expect(page.getByLabel(/contraseña/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
  });

  test('student register page shows required fields', async ({ page }) => {
    await page.goto('/register');
    await expect(page.getByRole('heading', { name: /crear cuenta/i })).toBeVisible({
      timeout: 15000
    });
    await expect(page.getByLabel(/^nombre$/i)).toBeVisible();
    await expect(page.getByLabel(/correo/i)).toBeVisible();
    await expect(page.getByLabel(/contraseña/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /crear estudiante/i })).toBeVisible();
  });

  test('marketing routes render without crashing', async ({ page }) => {
    for (const path of ['/nosotros', '/cursos', '/contacto']) {
      await page.goto(path);
      await expect(page.locator('body')).toBeVisible({ timeout: 15000 });
      await expect(page.getByText(/error inesperado|something went wrong/i)).toHaveCount(0);
    }
  });

  test('unknown route redirects to landing', async ({ page }) => {
    await page.goto('/ruta-que-no-existe-e2e');
    await expect(page).toHaveURL(/\/$/);
  });
});
