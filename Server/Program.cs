using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace WeatherServer
{
    class Program
    {
        private const string AccuWeatherApiKey = "UEKh0o5TRg7meXD00Uovj8atYD8UeYDV";
        private const string AccuWeatherBaseUrl = "http://dataservice.accuweather.com";

        static async Task Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8888);
            server.Start();
            Console.WriteLine("Server started on port 8888...");

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string city = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Received request for city: {city}");

                    var weatherResponse = await GetRealWeatherData(city);
                    string jsonResponse = JsonConvert.SerializeObject(weatherResponse);
                    byte[] responseData = Encoding.UTF8.GetBytes(jsonResponse);
                    await stream.WriteAsync(responseData, 0, responseData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex}");
            }
        }

        static async Task<WeatherResponse> GetRealWeatherData(string city)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    // 1. Get Location Key
                    string locationUrl = $"{AccuWeatherBaseUrl}/locations/v1/cities/search?apikey={AccuWeatherApiKey}&q={Uri.EscapeDataString(city)}";
                    HttpResponseMessage locationResponse = await httpClient.GetAsync(locationUrl);

                    if (!locationResponse.IsSuccessStatusCode)
                    {
                        string errorContent = await locationResponse.Content.ReadAsStringAsync();
                        throw new Exception($"Location API failed: {errorContent}");
                    }

                    string locationJson = await locationResponse.Content.ReadAsStringAsync();
                    JArray locationArray = JArray.Parse(locationJson);

                    if (locationArray == null || locationArray.Count == 0)
                    {
                        throw new Exception("No location data found");
                    }

                    string locationKey = locationArray[0]["Key"].ToString();
                    string country = locationArray[0]["Country"]?["LocalizedName"]?.ToString() ?? "N/A";

                    // 2. Get Current Weather
                    string currentUrl = $"{AccuWeatherBaseUrl}/currentconditions/v1/{locationKey}?apikey={AccuWeatherApiKey}";
                    HttpResponseMessage currentResponse = await httpClient.GetAsync(currentUrl);

                    if (!currentResponse.IsSuccessStatusCode)
                    {
                        throw new Exception("Current weather API failed");
                    }

                    string currentJson = await currentResponse.Content.ReadAsStringAsync();
                    JArray currentArray = JArray.Parse(currentJson);
                    JObject currentData = (JObject)currentArray[0];

                    // 3. Get 5-day Forecast
                    string forecastUrl = $"{AccuWeatherBaseUrl}/forecasts/v1/daily/5day/{locationKey}?apikey={AccuWeatherApiKey}";
                    HttpResponseMessage forecastResponse = await httpClient.GetAsync(forecastUrl);

                    if (!forecastResponse.IsSuccessStatusCode)
                    {
                        throw new Exception("Forecast API failed");
                    }

                    string forecastJson = await forecastResponse.Content.ReadAsStringAsync();
                    JObject forecastData = JObject.Parse(forecastJson);

                    // Process data
                    List<DailyForecast> forecasts = ParseForecastData(forecastJson);

                    return new WeatherResponse
                    {
                        Success = true,
                        Temperature = currentData["Temperature"]?["Metric"]?["Value"]?.Value<double>() ?? 0,
                        Humidity = currentData["RelativeHumidity"]?.Value<int>() ?? 0,
                        WindSpeed = currentData["Wind"]?["Speed"]?["Metric"]?["Value"]?.Value<double>() ?? 0,
                        Description = currentData["WeatherText"]?.Value<string>() ?? "N/A",
                        City = city,
                        Country = country,
                        Icon = currentData["WeatherIcon"]?.Value<int>().ToString("00") ?? "01",
                        DailyForecast = forecasts
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return new WeatherResponse
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            }
        }

        private static List<DailyForecast> ParseForecastData(string json)
        {
            List<DailyForecast> forecasts = new List<DailyForecast>();

            try
            {
                JObject data = JObject.Parse(json);
                JArray dailyForecasts = data["DailyForecasts"] as JArray;

                if (dailyForecasts == null) return forecasts;

                foreach (var day in dailyForecasts)
                {
                    DateTime date = day["Date"].Value<DateTime>();
                    JObject temp = day["Temperature"] as JObject;

                    forecasts.Add(new DailyForecast
                    {
                        Date = date,
                        DayOfWeek = date.ToString("dddd", new CultureInfo("vi-VN")),
                        MinTemperature = temp?["Minimum"]?["Value"]?.Value<double>() ?? 0,
                        MaxTemperature = temp?["Maximum"]?["Value"]?.Value<double>() ?? 0,
                        Description = day["Day"]?["IconPhrase"]?.Value<string>() ?? "N/A",
                        Icon = day["Day"]?["Icon"]?.Value<int>().ToString("00") ?? "01"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Forecast parse error: {ex.Message}");
            }

            return forecasts;
        }
    }

    public class WeatherResponse
    {
        public bool Success { get; set; }
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public string Description { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Icon { get; set; }
        public string ErrorMessage { get; set; }
        public List<DailyForecast> DailyForecast { get; set; } = new List<DailyForecast>();
    }

    public class DailyForecast
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}