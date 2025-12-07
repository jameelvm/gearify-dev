import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './product-detail.component.html',
  styleUrls: ['./product-detail.component.scss']
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
