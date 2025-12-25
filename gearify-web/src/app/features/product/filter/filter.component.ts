import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-filter',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './filter.component.html',
  styleUrls: ['./filter.component.scss']
})
export class FilterComponent {
  // Filter dropdowns state
  showBrandDropdown = false;
  showPriceDropdown = false;

  // Brand filter
  brandSearchQuery = '';
  brands = [
    { name: 'A. Veer', count: 5 },
    { name: 'adidas', count: 11 },
    { name: 'Akademiks', count: 10 },
    { name: 'ALDO', count: 39 },
    { name: 'Alfani', count: 5 },
    { name: 'Alpine Swiss', count: 10 },
    { name: 'Anthony Veer', count: 10 },
    { name: 'Asics', count: 6 },
    { name: 'Aston Marc', count: 4 },
    { name: 'Balenciaga', count: 8 },
    { name: 'Birkenstock', count: 22 },
    { name: 'Brooks', count: 14 },
    { name: 'Bruno Magli', count: 7 },
    { name: 'Burberry', count: 6 },
    { name: 'Calvin Klein', count: 28 },
    { name: 'Camper', count: 13 },
    { name: 'Clarks', count: 45 },
    { name: 'Coach', count: 19 },
    { name: 'Cole Haan', count: 32 },
    { name: 'Converse', count: 41 },
    { name: 'Crocs', count: 27 },
    { name: 'DC Shoes', count: 16 },
    { name: 'Diesel', count: 11 },
    { name: 'DKNY', count: 9 },
    { name: 'Dr. Martens', count: 24 },
    { name: 'Ecco', count: 31 },
    { name: 'Fila', count: 17 },
    { name: 'Florsheim', count: 12 },
    { name: 'Frye', count: 8 },
    { name: 'Geox', count: 15 },
    { name: 'Gucci', count: 5 },
    { name: 'Guess', count: 13 },
    { name: 'Hush Puppies', count: 20 },
    { name: 'Johnston & Murphy', count: 14 },
    { name: 'Kenneth Cole', count: 23 },
    { name: 'Lacoste', count: 16 },
    { name: 'Michael Kors', count: 26 },
    { name: 'New Balance', count: 12 },
    { name: 'Nike', count: 25 },
    { name: 'Nine West', count: 18 },
    { name: 'Prada', count: 4 },
    { name: 'Puma', count: 18 },
    { name: 'Reebok', count: 15 },
    { name: 'Rockport', count: 21 },
    { name: 'Saucony', count: 10 },
    { name: 'Skechers', count: 38 },
    { name: 'Sperry', count: 19 },
    { name: 'Steve Madden', count: 29 },
    { name: 'Timberland', count: 33 },
    { name: 'Tommy Hilfiger', count: 22 },
    { name: 'UGG', count: 25 },
    { name: 'Under Armour', count: 9 },
    { name: 'Vans', count: 35 },
    { name: 'Versace', count: 3 },
    { name: 'Wolverine', count: 11 }
  ];

  // Price filter
  priceRanges = [
    { label: 'Under $50', count: 172, value: '0-50' },
    { label: '$50-$100', count: 581, value: '50-100' },
    { label: '$100-$250', count: 520, value: '100-250' },
    { label: '$250-$500', count: 37, value: '250-500' },
    { label: '$500 & Above', count: 2, value: '500+' }
  ];
  customPriceMin = '';
  customPriceMax = '';

  // Sort options
  sortOptions = [
    'Featured Items',
    'Price: Low to High',
    'Price: High to Low',
    'Newest First',
    'Top Rated'
  ];
  selectedSort = 'Featured Items';

  // View mode
  viewMode: 'small' | 'medium' = 'medium';

  toggleBrandDropdown() {
    this.showBrandDropdown = !this.showBrandDropdown;
    if (this.showBrandDropdown) {
      this.showPriceDropdown = false;
    }
  }

  togglePriceDropdown() {
    this.showPriceDropdown = !this.showPriceDropdown;
    if (this.showPriceDropdown) {
      this.showBrandDropdown = false;
    }
  }

  closeAllDropdowns() {
    this.showBrandDropdown = false;
    this.showPriceDropdown = false;
  }

  setViewMode(mode: 'small' | 'medium') {
    this.viewMode = mode;
  }

  get filteredBrands() {
    if (!this.brandSearchQuery) return this.brands;
    return this.brands.filter(b =>
      b.name.toLowerCase().includes(this.brandSearchQuery.toLowerCase())
    );
  }
}
