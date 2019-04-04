import { Component, Inject, OnInit } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-fetch-data',
  templateUrl: './fetch-data.component.html'
})
export class FetchDataComponent implements OnInit {
  public forecasts: WeatherForecast[];

  constructor(private http: HttpClient, private authService: AuthService) {
  }

  ngOnInit(): void {
    let headers = new HttpHeaders({ 'Authorization': this.authService.getAuthorizationHeaderValue() });
    this.http.get<WeatherForecast[]>(`${environment.apiUrl}/api/SampleData/WeatherForecasts`, { headers: headers }).subscribe(result => {
      this.forecasts = result;
    }, error => console.error(error));
  }

}

interface WeatherForecast {
  dateFormatted: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}
