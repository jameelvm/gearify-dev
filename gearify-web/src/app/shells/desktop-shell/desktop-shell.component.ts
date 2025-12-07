import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-desktop-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  templateUrl: './desktop-shell.component.html',
  styleUrls: ['./desktop-shell.component.scss']
})
export class DesktopShellComponent {}
