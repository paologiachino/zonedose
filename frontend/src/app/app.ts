import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Root component: only the router outlet, where the current page is drawn. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
})
export class App {}
