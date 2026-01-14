# Realtime Auction Platform - Backend

Backend API cho ứng dụng đấu giá realtime được xây dựng bằng .NET 8 Web API, MongoDB và SignalR.

## 🛠️ Công nghệ sử dụng

- **.NET 8** - Web API Framework
- **MongoDB** - Database
- **SignalR** - Real-time communication
- **CORS** - Cross-Origin Resource Sharing

## 📋 Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) hoặc cao hơn
- MongoDB (có thể dùng MongoDB Atlas hoặc local)

## 🚀 Cách chạy dự án

### 1. Clone repository

```bash
git clone https://github.com/KeyT9999/realtime-auction-platform-backend.git
cd realtime-auction-platform-backend
```

### 2. Di chuyển vào thư mục project

```bash
cd RealtimeAuction.Api
```

### 3. Restore dependencies

```bash
dotnet restore
```

### 4. Chạy ứng dụng

```bash
dotnet run
```

Hoặc chạy với profile cụ thể:

```bash
# Chạy với HTTP (port 5145)
dotnet run --launch-profile http

# Chạy với HTTPS (port 7270)
dotnet run --launch-profile https
```

### 5. Kiểm tra API

- **Swagger UI**: `https://localhost:7270/swagger` (hoặc `http://localhost:5145/swagger`)
- **API Base URL**: `http://localhost:5145/api` hoặc `https://localhost:7270/api`
- **Test Endpoint**: `GET /api/test`

## 📁 Cấu trúc thư mục

```
RealtimeAuction.Api/
├── Controllers/     # API Controllers
├── Models/         # Domain Models
├── Dtos/           # Data Transfer Objects
├── Services/       # Business Logic Services
├── Repositories/   # Data Access Layer
├── Hubs/           # SignalR Hubs
├── Settings/        # Configuration Settings
└── Program.cs       # Application Entry Point
```

## 🔧 Cấu hình

### Environment Variables

Tạo file `.env` (không được commit lên Git) để cấu hình:

```
MONGODB_CONNECTION_STRING=mongodb://localhost:27017
MONGODB_DATABASE_NAME=auction_db
```

### appsettings.json

Cấu hình cơ bản trong `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## 📡 API Endpoints

### Test Endpoints

- `GET /api/test` - Test kết nối API
- `GET /api/test/ping` - Ping API

## 🔐 CORS Configuration

Backend đã được cấu hình CORS để cho phép frontend (`http://localhost:5173`) kết nối.

## 📦 Packages đã cài đặt

- `MongoDB.Driver` - MongoDB database driver
- `Microsoft.AspNetCore.SignalR` - Real-time communication

## 🐛 Troubleshooting

### Lỗi port đã được sử dụng

Thay đổi port trong `Properties/launchSettings.json`:

```json
{
  "applicationUrl": "http://localhost:5000"
}
```

### Lỗi SSL Certificate

Nếu gặp lỗi SSL, có thể chạy với HTTP profile hoặc trust certificate:

```bash
dotnet dev-certs https --trust
```

## 📝 Development Notes

- Backend chạy trên port `5145` (HTTP) hoặc `7270` (HTTPS) mặc định
- CORS được cấu hình để cho phép frontend kết nối
- Swagger UI chỉ hiển thị trong môi trường Development

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License.
