using System;
using System.Data;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;

namespace Client
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            InitializeChart();
            InitializeDateTimeTimer();
        }

        private void InitializeChart()
        {
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            chart1.Titles.Clear();

            // Create chart area
            ChartArea chartArea = new ChartArea("ForecastArea");
            chartArea.AxisX.Title = "Ngày";
            chartArea.AxisY.Title = "Nhiệt độ (°C)";
            chartArea.AxisX.Interval = 1;
            chart1.ChartAreas.Add(chartArea);

            // Add legend
            Legend legend = new Legend();
            chart1.Legends.Add(legend);
        }
        private System.Windows.Forms.Timer timerDateTime;
        private void InitializeDateTimeTimer()
        {
            timerDateTime = new System.Windows.Forms.Timer();
            timerDateTime.Interval = 1000;
            timerDateTime.Tick += TimerDateTime_Tick;
            timerDateTime.Start();
            UpdateDateTime();
        }

        private void TimerDateTime_Tick(object sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            // Sử dụng Invoke nếu cần thiết để tránh cross-thread operation
            if (labDateTime.InvokeRequired || labDateTime2.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate {
                    labDateTime.Text = "Giờ: " + DateTime.Now.ToString("HH:mm:ss");
                    labDateTime2.Text = "Ngày: " + DateTime.Now.ToString("dd/MM/yyyy");
                });
            }
            else
            {
                labDateTime.Text = "Giờ: " + DateTime.Now.ToString("HH:mm:ss");
                labDateTime2.Text = "Ngày: " + DateTime.Now.ToString("dd/MM/yyyy");
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            string city = TBCity.Text.Trim();
            if (string.IsNullOrEmpty(city))
            {
                MessageBox.Show("Vui lòng nhập tên thành phố!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                btnSearch.Enabled = false;

                string response = await GetWeatherDataFromServerAsync(city);
                DisplayWeatherData(response);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lấy dữ liệu thời tiết: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnSearch.Enabled = true;
            }
        }

        private void DisplayWeatherData(string jsonData)
        {
            try
            {
                var weatherResponse = JsonConvert.DeserializeObject<WeatherResponse>(jsonData);

                if (!weatherResponse.Success)
                {
                    MessageBox.Show(weatherResponse.ErrorMessage, "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Display current weather
                labTemperature.Text = $"{weatherResponse.Temperature}°C";
                labHumidity.Text = $"{weatherResponse.Humidity}%";
                labWindSpeed.Text = $"{weatherResponse.WindSpeed} km/h";
                labPressure.Text = $"{weatherResponse.Pressure} hPa";
                labDetail2.Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(weatherResponse.Description.ToLower());
                labSunset.Text = weatherResponse.Sunset;
                labSunrise.Text = weatherResponse.Sunrise;
                labDistrict.Text = $"{weatherResponse.City}, {weatherResponse.Country}";
                labTemp_min.Text = $"{weatherResponse.Temp_min}°C";
                labTemp_max.Text = $"{weatherResponse.Temp_max}°C";

                if (!string.IsNullOrEmpty(weatherResponse.Icon))
                {
                    picIcon.ImageLocation = $"http://openweathermap.org/img/wn/{weatherResponse.Icon}@2x.png";
                }

                labFeels_like.Text = $"~{weatherResponse.Like_feel}°C";
                labAdvice.Text = GetWeatherAdvice(weatherResponse.Temperature, weatherResponse.Description);

                // Display forecast
                if (weatherResponse.DailyForecast != null && weatherResponse.DailyForecast.Count > 0)
                {
                    DisplayForecastChart(weatherResponse.DailyForecast);
                    DisplayForecastDetails(weatherResponse.DailyForecast);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi hiển thị dữ liệu: {ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void DisplayForecastChart(List<DailyForecast> forecasts)
        {
            chart1.Series.Clear();
            // Add average temperature series
            Series avgSeries = new Series("Nhiệt độ TB")
            {
                ChartType = SeriesChartType.Column,
                Color = Color.DeepSkyBlue,
                BorderWidth = 2,
                IsValueShownAsLabel = true
            };

            // Add min/max series
            Series minSeries = new Series("Thấp nhất")
            {
                ChartType = SeriesChartType.Line,
                Color = Color.Blue,
                BorderWidth = 2,
                IsValueShownAsLabel = true
            };

            Series maxSeries = new Series("Cao nhất")
            {
                ChartType = SeriesChartType.Point,
                Color = Color.Red,
                BorderWidth = 2,
                IsValueShownAsLabel = true
            };

            foreach (var forecast in forecasts)
            {
                string dayLabel = forecast.Date.ToString("dd/MM");
                avgSeries.Points.AddXY(dayLabel, forecast.AvgTemperature);
                minSeries.Points.AddXY(dayLabel, forecast.MinTemperature);
                maxSeries.Points.AddXY(dayLabel, forecast.MaxTemperature);
            }

            chart1.Series.Add(avgSeries);
            chart1.Series.Add(minSeries);
            chart1.Series.Add(maxSeries);

            chart1.Titles.Clear();
            chart1.Titles.Add("DỰ BÁO THỜI TIẾT 7 NGÀY");
        }

        private void DisplayForecastDetails(List<DailyForecast> forecasts)
        {
            // Tạo DataTable với các cột cần thiết (đã bỏ cột đầu tiên trống)
            DataTable dt = new DataTable();
            dt.Columns.Add("Ngày");
            dt.Columns.Add("Thứ");
            dt.Columns.Add("Thấp nhất");
            dt.Columns.Add("Cao nhất");
            dt.Columns.Add("Mô tả");
            dt.Columns.Add("Thời tiết", typeof(Image));

            // Thêm dữ liệu vào các hàng (bỏ hàng cuối cùng trống)
            foreach (var forecast in forecasts)
            {
                // Tải hình ảnh từ OpenWeatherMap
                Image weatherIcon = null;
                try
                {
                    using (var webClient = new WebClient())
                    {
                        byte[] imageData = webClient.DownloadData($"http://openweathermap.org/img/wn/{forecast.Icon}.png");
                        using (var stream = new MemoryStream(imageData))
                        {
                            weatherIcon = Image.FromStream(stream);
                        }
                    }
                }
                catch
                {
                    weatherIcon = null;
                }

                // Viết hoa chữ cái đầu mô tả
                string description = string.IsNullOrEmpty(forecast.Description)
                    ? ""
                    : char.ToUpper(forecast.Description[0]) + forecast.Description.Substring(1);

                dt.Rows.Add(
                    forecast.Date.ToString("dd/MM"),
                    forecast.DayOfWeek,
                    $"{forecast.MinTemperature}°C",
                    $"{forecast.MaxTemperature}°C",
                    description,
                    weatherIcon
                );
            }

            // Gán DataTable vào DataGridView
            dataGridView1.DataSource = dt;

            // Cấu hình hiển thị
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.RowHeadersVisible = false; // Ẩn cột đầu tiên (cột trống)

            // Cấu hình cột biểu tượng
            DataGridViewImageColumn imageColumn = (DataGridViewImageColumn)dataGridView1.Columns["Thời tiết"];
            imageColumn.ImageLayout = DataGridViewImageCellLayout.Zoom;
            imageColumn.DefaultCellStyle.NullValue = null;
            imageColumn.Width = 40;

            // Căn chỉnh nội dung các cột
            dataGridView1.Columns["Ngày"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns["Thứ"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns["Thấp nhất"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns["Cao nhất"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns["Mô tả"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            // Đặt font cho cột mô tả
            dataGridView1.Columns["Mô tả"].DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Regular);

            // Ẩn dòng trống cuối cùng (nếu có)
            dataGridView1.AllowUserToAddRows = false;
        }

        private string GetWeatherAdvice(double temperature, string description)
        {
            if (temperature > 30)
                return "Nắng nóng, nên mặc đồ thoáng mát và uống nhiều nước";
            else if (temperature < 20)
                return "Trời mát/lạnh, nên mặc áo ấm";
            else
                return "Thời tiết dễ chịu, thích hợp cho các hoạt động ngoài trời";
        }

        private async Task<string> GetWeatherDataFromServerAsync(string city)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", 8888);

                using (NetworkStream stream = client.GetStream())
                {
                    byte[] data = Encoding.UTF8.GetBytes(city);
                    await stream.WriteAsync(data, 0, data.Length);

                    byte[] buffer = new byte[4096];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }
            }
        }


        private void btnLocation_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Chức năng lấy vị trí hiện tại chưa được triển khai!",
                "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        public string Temp_min { get; set; }
        public string Temp_max { get; set; }

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