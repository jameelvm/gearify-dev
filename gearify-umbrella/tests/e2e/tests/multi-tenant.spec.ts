import { test, expect } from '@playwright/test';

test.describe('Multi-Tenant', () => {
  test('should show default tenant theme', async ({ page }) => {
    await page.goto('/');
    const primaryColor = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--primary-color')
    );
    expect(primaryColor.trim()).toBe('#1976d2');
  });

  test('should show demo tenant theme', async ({ page }) => {
    await page.goto('/', { headers: { 'Host': 'demo.localhost' } });
    const primaryColor = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--primary-color')
    );
    expect(primaryColor.trim()).toBe('#388e3c');
  });
});
