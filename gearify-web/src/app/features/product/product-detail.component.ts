import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="product-detail">
      <div class="product-images">
        <img src="/assets/bat.jpg" alt="Cricket Bat" class="main-image" />
      </div>

      <div class="product-info">
        <h1>{{ product.name }}</h1>
        <div class="rating">â­â­â­â­â­</div>
        <div class="price">Price: {{ product.price | currency }}</div>

        <div class="specifications">
          <h3>Specifications</h3>
          <table>
            <tr><td>Brand:</td><td>{{ product.brand }}</td></tr>
            <tr><td>Weight:</td><td>{{ product.weight }}</td></tr>
            <tr><td>Grade:</td><td>{{ product.grade }}</td></tr>
          </table>
        </div>

        <button (click)="addToCart()">Add to Cart</button>
      </div>
    </div>

    <div class="reviews-section">
      <h2>Customer Reviews</h2>
      <div class="review" *ngFor="let review of reviews">
        <p><strong>{{ review.author }}</strong></p>
        <p>{{ review.text }}</p>
      </div>
    </div>
  `,
  styles: [`
    .product-detail { display: grid; grid-template-columns: 1fr 1fr; gap: 2rem; padding: 2rem; }
    .main-image { width: 100%; }
    .price { font-size: 2rem; color: #1e3a8a; margin: 1rem 0; }
    .specifications table { width: 100%; margin: 1rem 0; }
    .specifications td { padding: 0.5rem; }
    button { padding: 1rem 2rem; background: #1e3a8a; color: white; border: none; cursor: pointer; }
    .reviews-section { padding: 2rem; }
    .review { border-bottom: 1px solid #eee; padding: 1rem 0; }
  `]
})
export class ProductDetailComponent {
  product = {
    name: 'CA Plus 15000 Bat',
    price: 299.99,
    brand: 'CA',
    weight: '35oz',
    grade: 'Grade 1'
  };

  reviews = [
    { author: 'John D.', text: 'Excellent bat! Great balance.' },
    { author: 'Raj P.', text: 'Best bat I have owned.' }
  ];

  addToCart() {
    alert('Added to cart!');
  }
}
