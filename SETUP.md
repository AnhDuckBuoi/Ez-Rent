# 🚀 EZ-Rent — Hướng Dẫn Cài Đặt

> Clone repo về và chạy được trong **10 phút**

## 📋 Yêu cầu hệ thống

| # | Phần mềm | Version | Link |
|---|---|---|---|
| 1 | Git | Mới nhất | https://git-scm.com/downloads |
| 2 | .NET SDK | **8.0** | https://dotnet.microsoft.com/download/dotnet/8.0 |
| 3 | SQL Server | 2019+ / LocalDB | https://www.microsoft.com/sql-server |
| 4 | Node.js | 18+ | https://nodejs.org |
| 5 | VS Code | Mới nhất | https://code.visualstudio.com |

## 🔧 Bước 1 — Clone repo

```powershell
cd D:\
git clone https://github.com/<username>/EZ-Rent.git
cd EZ-Rent
```

## 🔧 Bước 2 — Cài Global Tools (1 lần duy nhất)

```powershell
dotnet tool install --global dotnet-ef
dotnet tool install --global dotnet-aspnet-codegenerator
```

## 🔧 Bước 3 — Restore packages

```powershell
dotnet restore
```

## 🔧 Bước 4 — Cấu hình User Secrets

### 4.1. Init

```powershell
dotnet user-secrets init
```

### 4.2. Set 7 secret (thay giá trị thật)

```powershell
# Database — dùng LocalDB
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=EZ_Rent;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"

# Google OAuth — liên hệ nhóm trưởng để lấy
dotnet user-secrets set "Authentication:Google:ClientId" "LIÊN HỆ NHÓM TRƯỞNG"
dotnet user-secrets set "Authentication:Google:ClientSecret" "LIÊN HỆ NHÓM TRƯỞNG"

# Email — liên hệ nhóm trưởng
dotnet user-secrets set "EmailSettings:Host" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:Email" "LIÊN HỆ NHÓM TRƯỞNG"
dotnet user-secrets set "EmailSettings:Password" "LIÊN HỆ NHÓM TRƯỞNG"
```

### 4.3. Kiểm tra

```powershell
dotnet user-secrets list
```

## 🔧 Bước 5 — Tạo Database

```powershell
# Khởi động LocalDB
sqllocaldb start mssqllocaldb

# Chạy migration
dotnet ef database update
```

**Kiểm tra:** Mở SSMS → DB `EZ_Rent` có ~20 bảng.

## 🔧 Bước 6 — Chạy project

```powershell
dotnet run
```

Mở browser: **https://localhost:7200**

## 🔐 Tài khoản mặc định

| Vai trò | Email | Password |
|---|---|---|
| Admin | admin@gmail.com | Admin@123 |
| Landlord | Tự đăng ký | — |
| Tenant | Tự đăng ký | — |

## 🐛 Lỗi thường gặp

| Lỗi | Fix |
|---|---|
| `dotnet-ef: command not found` | Đóng terminal, mở lại |
| `Cannot open database` | `sqllocaldb start mssqllocaldb` |
| `Correlation failed` (Google) | Clear cookies trình duyệt |
| `Tài khoản chưa xác thực email` | Tắt `RequireConfirmedEmail` trong Program.cs |
| Port 7200 bị chiếm | Đổi port trong `launchSettings.json` |

## 📞 Liên hệ nhóm trưởng để lấy:

- Google ClientId + ClientSecret
- Gmail App Password
- SQL Server ConnectionString (nếu không dùng LocalDB)

---

**© 2026 — Nhóm WNC.G07**