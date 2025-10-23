import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

/**
 * Server-side bootstrap for SSR
 */
const bootstrap = () => bootstrapApplication(AppComponent, appConfig);

export default bootstrap;
