# 🎮 Game Caro LAN - Dự án Lập Trình Mạng

<div align="center">

![.NET Version](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![Language](https://img.shields.io/badge/Language-C%23-green)

**Game Caro đa người chơi qua mạng LAN sử dụng Socket Programming**

</div>

---

## 🎯 Giới thiệu

**Game Caro LAN** là ứng dụng game đa người chơi được phát triển bằng C# WinForms, cho phép nhiều người chơi cùng tham gia chơi Caro qua mạng LAN. Dự án sử dụng kiến trúc Client-Server với Socket Programming.

### Đặc điểm nổi bật

- ✅ Đa người chơi qua mạng LAN
- ✅ Giao diện WinForms hiện đại
- ✅ Hệ thống âm thanh đầy đủ
- ✅ Quản lý phòng chơi và mời bạn
- ✅ Tự động phát hiện server trong mạng LAN

---

## ✨ Tính năng

- **Bàn cờ 15x15**: Bàn cờ Caro chuẩn, thắng khi có 5 quân liên tiếp
- **Đếm ngược thời gian**: 20 giây mỗi lượt
- **Đăng nhập/Đăng ký**: Hệ thống xác thực người dùng với MySQL
- **Quản lý phòng**: Tạo phòng, tham gia phòng, mời bạn chơi
- **Danh sách online**: Xem người chơi đang online
- **Âm thanh**: Hiệu ứng âm thanh khi chơi game

---

## 🛠️ Công nghệ sử dụng

- **.NET 8.0** - Framework phát triển
- **C#** - Ngôn ngữ lập trình
- **Windows Forms** - Giao diện người dùng
- **Socket Programming** - Giao tiếp TCP/IP
- **MySQL** - Cơ sở dữ liệu
- **MySqlConnector** - Thư viện kết nối MySQL

---

## 💻 Yêu cầu hệ thống

- Windows 10 trở lên
- .NET 8.0 Runtime
- MySQL Server 8.0+ (cho server)
- RAM: 2GB trở lên
- Mạng LAN ổn định

---

## 📦 Cài đặt

### 1. Clone repository
```bash
git clone https://github.com/yourusername/LapTrinhMang_ProjectCuoiKy.git
cd LapTrinhMang_ProjectCuoiKy
```

### 2. Cài đặt .NET 8.0 SDK
Tải từ [Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)

### 3. Cài đặt MySQL Server
- Tải và cài đặt MySQL Server
- Tạo database mới
- Import `database_schema.sql` (nếu có)

### 4. Cấu hình
- Chỉnh sửa chuỗi kết nối MySQL trong `UserManager.cs`
- Kiểm tra PORT trong `ServerSocketManager.cs` (mặc định: 9999)

### 5. Build
```bash
dotnet build LapTrinhMang_ProjectCuoiKy.sln
```

---

## 🚀 Sử dụng

### Khởi động Server
1. Chạy `WinFormServer.exe` hoặc project `WinFormServer`
2. Server lắng nghe trên port 9999

### Khởi động Client
1. Chạy `CaroLAN.exe` hoặc project `CaroLAN`
2. Đăng nhập/Đăng ký tài khoản
3. Vào sảnh chờ → Tạo phòng hoặc tham gia phòng
4. Mời bạn chơi và bắt đầu game!

### Cách chơi
- Click vào ô trên bàn cờ để đánh
- Mỗi lượt có 20 giây
- Thắng khi có 5 quân cờ liên tiếp (ngang/dọc/chéo)

---

## 📁 Cấu trúc dự án

```
LapTrinhMang_ProjectCuoiKy/
│
├── CaroLAN/                                    # Thư mục chứa Client và Server
│   ├── CaroLAN/                                # Client Application
│   │   ├── Forms/                              # Các form giao diện
│   │   │   ├── LoginForm.cs                    # Form đăng nhập/đăng ký
│   │   │   ├── LoginForm.Designer.cs
│   │   │   ├── LoginForm.resx
│   │   │   ├── sanhCho.cs                      # Form sảnh chờ
│   │   │   ├── sanhCho.Designer.cs
│   │   │   ├── sanhCho.resx
│   │   │   ├── Form1.cs                        # Form game chính
│   │   │   ├── Form1.Designer.cs
│   │   │   └── Form1.resx
│   │   ├── Managers/                           # Các lớp quản lý
│   │   │   ├── SocketManager.cs                # Quản lý socket client
│   │   │   ├── ChessBoardManager.cs            # Quản lý bàn cờ và logic game
│   │   │   ├── SoundManager.cs                 # Quản lý âm thanh
│   │   │   └── ServerDiscoveryClient.cs        # Phát hiện server trong LAN
│   │   ├── Sounds/                             # File âm thanh
│   │   │   ├── background.wav                  # Nhạc nền
│   │   │   ├── button_click.wav                # Âm thanh click nút
│   │   │   ├── piece_click.wav                 # Âm thanh đánh quân cờ
│   │   │   ├── game_win.wav                    # Âm thanh thắng
│   │   │   └── game_lose.wav                   # Âm thanh thua
│   │   ├── Program.cs                          # Entry point của Client
│   │   ├── CaroLAN.csproj                      # Project file
│   │   └── CaroLAN.csproj.user                 # User settings
│   ├── CaroLAN.slnx                            # Solution file cho Client
│   │
│   └── WinFormServer/                           # Server Application
│       ├── Forms/                               # Form giao diện server
│       │   ├── ServerForm.cs                    # Form quản lý server
│       │   ├── ServerForm.Designer.cs
│       │   └── ServerForm.resx
│       ├── Managers/                            # Các lớp quản lý server
│       │   ├── ServerSocketManager.cs           # Quản lý socket server và xử lý message
│       │   ├── UserManager.cs                   # Quản lý người dùng và database
│       │   ├── RoomManager.cs                   # Quản lý phòng chơi
│       │   └── BroadcastDiscovery.cs            # Broadcast để client phát hiện server
│       ├── Game/                                # Logic xử lý game
│       │   └── GameEngine.cs                    # Engine xử lý nước đi và kiểm tra thắng thua
│       ├── Models/                              # Các model dữ liệu
│       │   ├── User.cs                          # Model người dùng
│       │   ├── GameRoom.cs                      # Model phòng chơi
│       │   ├── GameBoardState.cs                # Model trạng thái bàn cờ
│       │   ├── GameInvitation.cs                # Model lời mời chơi
│       │   ├── GameModels.cs                    # Các enum và model game khác
│       │   └── MatchHistory.cs                  # Model lịch sử trận đấu
│       ├── Database/                            # Database schema
│       │   └── database_schema.sql              # File SQL tạo database
│       ├── Program.cs                           # Entry point của Server
│       ├── WinFormServer.csproj                 # Project file
│       └── WinFormServer.csproj.user            # User settings
│
├── LapTrinhMang_ProjectCuoiKy.sln               # Solution file chính
├── ServerDiscoveryClient.cs                     # File hỗ trợ (nếu có)
├── SocketManager.cs                             # File hỗ trợ (nếu có)
└── README.md                                    
```

---

## 📝 Ghi chú

- Server phải khởi động trước khi client kết nối
- Tất cả client phải cùng mạng LAN với server
- Cấu hình firewall cho phép port 9999

---

## 👨‍💻 Tác giả

**Nhóm 1**

---

<div align="center">

**Made with ❤️ for Lập Trình Mạng Project**

⭐ Star repo nếu bạn thấy hữu ích!

</div>
