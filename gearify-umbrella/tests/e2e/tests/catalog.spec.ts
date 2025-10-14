import { test, expect } from '@playwright/test';

test.describe('Product Catalog', () => {
  test('should display products', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('h1')).toContainText('Products');
    const products = page.locator('.product-card');
    await expect(products).toHaveCountGreaterThan(0);
  });

  test('should filter products by category', async ({ page }) => {
    await page.goto('/');
    await page.click('text=Bats');
    const products = page.locator('.product-card');
    await expect(products.first()).toContainText('Bat');
  });
});
