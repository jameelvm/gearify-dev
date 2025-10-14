import { test, expect } from '@playwright/test';

test.describe('Checkout Flow', () => {
  test('should complete checkout with Stripe', async ({ page }) => {
    await page.goto('/');
    await page.click('.product-card:first-child .add-to-cart');
    await page.click('text=Cart');
    await page.click('text=Checkout');
    await page.fill('[name="email"]', 'test@gearify.com');
    await page.fill('[name="cardNumber"]', '4242424242424242');
    await page.fill('[name="expiry"]', '12/25');
    await page.fill('[name="cvc"]', '123');
    await page.click('text=Pay');
    await expect(page.locator('text=Order Confirmed')).toBeVisible();
  });
});
