import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { formatCurrency } from '@shared/utils/currency.utils';

/**
 * Price display component with discount support
 */
@Component({
  selector: 'app-price-tag',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './price-tag.component.html',
  styleUrl: './price-tag.component.scss'
})
export class PriceTagComponent {
  @Input() price!: number;
  @Input() compareAtPrice?: number;
  @Input() currency = 'USD';
  @Input() size: 'small' | 'medium' | 'large' = 'medium';

  get formattedPrice(): string {
    return formatCurrency(this.price, this.currency);
  }

  get formattedComparePrice(): string | null {
    return this.compareAtPrice ? formatCurrency(this.compareAtPrice, this.currency) : null;
  }

  get discountPercentage(): number | null {
    if (!this.compareAtPrice || this.compareAtPrice <= this.price) {
      return null;
    }
    return Math.round(((this.compareAtPrice - this.price) / this.compareAtPrice) * 100);
  }
}
