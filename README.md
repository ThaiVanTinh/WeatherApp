WeatherApp là một ứng dụng dự báo thời tiết được phát triển bằng ngôn ngữ C# trên nền tảng .NET. Ứng dụng cho phép người dùng tra cứu thông tin thời tiết theo địa điểm cụ thể bằng cách sử dụng API từ OpenWeatherMap.

## 🧩 Tính năng

- Tìm kiếm thông tin thời tiết theo tên thành phố
- Hiển thị các thông tin cơ bản như nhiệt độ, độ ẩm, áp suất, tốc độ gió và mô tả thời tiết
- Giao diện đơn giản, dễ sử dụng

## 🛠️ Công nghệ sử dụng

 - Ngôn ngữ lập trình: C#
 - Nền tảng: .NET Framework
 - API thời tiết: https://openweathermap.org/api
 - Thư viện hỗ trợ: Newtonsoft.Json để xử lý dữ liệu JSON

## 📁 Cấu trúc thư mục

WeatherApp/
├── Client/                  # Mã nguồn giao diện người dùng
├── Server/                  # Mã nguồn xử lý logic và gọi API
├── packages/                # Thư viện bên ngoài (Newtonsoft.Json)
├── WeatherApp.sln           # Tệp giải pháp của Visual Studio
├── .gitignore               # Tệp cấu hình Git
└── WeatherApp.slnLaunch.user # Tệp cấu hình khởi chạy (tự động tạo)


## 🚀 Hướng dẫn cài đặt và chạy ứng dụng
1. Clone repository về: git clone https://github.com/ThaiVanTinh/WeatherApp.git
2. Mở tệp WeatherApp.sln bằng Visual Stuio.
3. Khôi phục các gói NuGet nếu cần thiết (đặc biệt là `Newtonsoft.Json`).
4. Chạy ứng dụng bằng cách nhấn `F5` hoặc chọn "Start" trong Visual Stuio.

## 🔑 Lưu ý về API ey

Để ứng dụng hoạt động chính xác, bạn cần có API Key từ OpenWeatheMap:
1. Truy cập [https://openweathermap.org/api](https://openweathermap.org/api) và đăng ký tài koản.
2. Tạo một API Key mới trong phần quản lý tài koản.
3. Thêm API Key vào mã nguồn tại vị trí gọi API (thường trong tệp cấu hình hoặc mã xử lý loic).

## 📷 Giao diện ứng dụng
![image](https://github.com/user-attachments/assets/ec719729-4753-469b-bc08-e8aeda4351ca)


![image](https://github.com/user-attachments/assets/bf332825-409d-47cd-b136-104d34ef289e)


## 📄 Giấyphép

Dự án được phát hành dưới giấy phép [MIT License](LIENSE).
---
