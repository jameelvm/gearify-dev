import { test, expect } from '@playwright/test';

test.describe('Order Tracking', () => {
  test('should track order status', async ({ page }) => {
    await page.goto('/orders/order-001');
    await expect(page.locator('.order-status')).toContainText('Confirmed');
    await expect(page.locator('.tracking-number')).toBeVisible();
  });
});
