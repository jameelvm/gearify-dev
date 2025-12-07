import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-mobile-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  templateUrl: './mobile-shell.component.html',
  styleUrls: ['./mobile-shell.component.scss']
})
export class MobileShellComponent {}
