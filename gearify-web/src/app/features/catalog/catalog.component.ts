import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './catalog.component.html',
  styleUrls: ['./catalog.component.scss']
})
export class CatalogComponent {
  products = [
    { name: 'CA Plus 15000 Bat', price: 299.99 },
    { name: 'SG RSD Xtreme Bat', price: 349.99 },
    { name: 'GM Diamond Bat', price: 279.99 }
  ];
}
