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
});
