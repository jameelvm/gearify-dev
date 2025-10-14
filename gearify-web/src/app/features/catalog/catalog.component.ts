import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>Cricket Gear Catalog</h2>
    <div class="products">
      <div *ngFor="let product of products" class="product-card">
        <h3>{{ product.name }}</h3>
        <p>Price: {{ product.price | currency }}</p>
      </div>
    </div>
  `,
  styles: [`
    .products { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    .product-card { border: 1px solid #ccc; padding: 1rem; }
  `]
})
export class CatalogComponent {
  products = [
    { name: 'CA Plus 15000 Bat', price: 299.99 },
    { name: 'SG RSD Xtreme Bat', price: 349.99 },
    { name: 'GM Diamond Bat', price: 279.99 }
  ];
}
