using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Mail;

namespace WeatherServer
{
    class Program
    {
        private const string OpenWeatherMapApiKey = "26653ec7090961b6a70cc1709679d31d";
        private const string OpenWeatherMapBaseUrl = "http://api.openweathermap.org/data/2.5/weather";
        private const string OpenWeatherForecastUrl = "http://api.openweathermap.org/data/2.5/forecast";

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
        private static DateTime ConvertDateTime(long timestamp)
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            return dateTimeOffset.LocalDateTime;
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
                    // Current weather
                    string currentUrl = $"{OpenWeatherMapBaseUrl}?q={Uri.EscapeDataString(city)}&appid={OpenWeatherMapApiKey}&units=metric&lang=vi";
                    HttpResponseMessage currentResponse = await httpClient.GetAsync(currentUrl);

                    if (!currentResponse.IsSuccessStatusCode)
                    {
                        string errorContent = await currentResponse.Content.ReadAsStringAsync();
                        throw new Exception($"API request failed: {currentResponse.StatusCode}, {errorContent}");
                    }

                    string currentJson = await currentResponse.Content.ReadAsStringAsync();
                    dynamic currentData = JsonConvert.DeserializeObject(currentJson);
                    // Lấy thông tin thời gian mặt trời mọc/lặn
                    DateTime sunriseTime = ConvertDateTime((long)currentData.sys.sunrise);
                    DateTime sunsetTime = ConvertDateTime((long)currentData.sys.sunset);
                    // Forecast
                    string forecastUrl = $"{OpenWeatherForecastUrl}?q={Uri.EscapeDataString(city)}&appid={OpenWeatherMapApiKey}&units=metric&lang=vi&cnt=40";
                    HttpResponseMessage forecastResponse = await httpClient.GetAsync(forecastUrl);

                    if (!forecastResponse.IsSuccessStatusCode)
                    {
                        string errorContent = await forecastResponse.Content.ReadAsStringAsync();
                        throw new Exception($"Forecast API failed: {forecastResponse.StatusCode}, {errorContent}");
                    }

                    string forecastJson = await forecastResponse.Content.ReadAsStringAsync();
                    dynamic forecastData = JsonConvert.DeserializeObject(forecastJson);

                    // Process forecast data
                    var dailyForecast = ProcessDailyForecast(forecastData.list);
                    // Lấy nhiệt độ min/max từ dự báo ngày hôm nay
                    DailyForecast todayForecast = null;
                    foreach (var forecast in dailyForecast)
                    {
                        if (forecast.Date.Date == DateTime.Today)
                        {
                            todayForecast = forecast;
                            break;
                        }
                    }
                    // Nếu không tìm thấy dự báo cho hôm nay, lấy dự báo đầu tiên
                    if (todayForecast == null && dailyForecast.Count > 0)
                    {
                        todayForecast = dailyForecast[0];
                    }
                    // Sử dụng giá trị từ dự báo nếu có, nếu không thì dùng giá trị từ current data
                    string todayMinTemp = todayForecast != null ?
                        Math.Round(todayForecast.MinTemperature, 0).ToString() :
                        Math.Round((double)currentData.main.temp_min, 0).ToString();

                    string todayMaxTemp = todayForecast != null ?
                        Math.Round(todayForecast.MaxTemperature, 0).ToString() :
                        Math.Round((double)currentData.main.temp_max, 0).ToString();
                    return new WeatherResponse
                    {
                        Success = true,
                        Temperature = Math.Round((double)currentData.main.temp, 0),
                        Humidity = (int)currentData.main.humidity,
                        WindSpeed = Math.Round((double)currentData.wind.speed * 3.6, 1),
                        Pressure = (double)currentData.main.pressure,
                        Description = (string)currentData.weather[0].description,
                        City = (string)currentData.name,
                        Country = (string)currentData.sys.country,
                        Icon = (string)currentData.weather[0].icon,
                        Sunrise = sunriseTime.ToString("HH:mm"), 
                        Sunset = sunsetTime.ToString("HH:mm"),
                        Like_feel = currentData.main.feels_like,
                        Temp_min = todayMinTemp,
                        Temp_max = todayMaxTemp,

                        DailyForecast = dailyForecast
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting weather data: {ex}");
                    return new WeatherResponse
                    {
                        Success = false,
                        ErrorMessage = "Không thể lấy dữ liệu thời tiết. Vui lòng thử lại sau."
                    };
                }
            }
        }

        private static List<DailyForecast> ProcessDailyForecast(dynamic forecastList)
        {
            var dailyForecasts = new List<DailyForecast>();
            var groupedByDay = new Dictionary<string, List<dynamic>>();

            foreach (var item in forecastList)
            {
                DateTime dt = DateTime.Parse(item.dt_txt.ToString());
                string dateKey = dt.ToString("yyyy-MM-dd");

                if (!groupedByDay.ContainsKey(dateKey))
                {
                    groupedByDay[dateKey] = new List<dynamic>();
                }
                groupedByDay[dateKey].Add(item);
            }

            foreach (var day in groupedByDay)
            {
                double avgTemp = 0;
                double minTemp = double.MaxValue;
                double maxTemp = double.MinValue;
                string description = "";
                string icon = "";

                foreach (var item in day.Value)
                {
                    double temp = (double)item.main.temp;
                    avgTemp += temp;
                    minTemp = Math.Min(minTemp, (double)item.main.temp_min);
                    maxTemp = Math.Max(maxTemp, (double)item.main.temp_max);

                    if (string.IsNullOrEmpty(description))
                    {
                        description = item.weather[0].description;
                        icon = item.weather[0].icon;
                    }
                }

                avgTemp /= day.Value.Count;

                dailyForecasts.Add(new DailyForecast
                {
                    Date = DateTime.Parse(day.Key),
                    DayOfWeek = DateTime.Parse(day.Key).ToString("dddd"),
                    AvgTemperature = Math.Round(avgTemp, 1),
                    MinTemperature = Math.Round(minTemp, 1),
                    MaxTemperature = Math.Round(maxTemp, 1),
                    Description = description,
                    Icon = icon
                });
            }

            return dailyForecasts.OrderBy(d => d.Date).Take(7).ToList();
        }

    }

    public class WeatherResponse
    {
        public bool Success { get; set; }
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public double Pressure { get; set; }
        public double Like_feel { get; set; }
        public string Description { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Icon { get; set; }
        public string Sunset { get; set; }
        public string Sunrise { get; set; }
        public string ErrorMessage { get; set; }
        public string? Temp_min { get; set; }
        public string? Temp_max { get; set; }
        public List<DailyForecast> DailyForecast { get; set; } = new List<DailyForecast>();
    }

    public class DailyForecast
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public double AvgTemperature { get; set; }
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}