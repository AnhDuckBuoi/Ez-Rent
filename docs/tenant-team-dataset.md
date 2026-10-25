# Bản tích hợp và demo bằng dataset của nhóm

Ngày 25/10/2026. Dương duyệt đưa mã về thư mục gốc, ghép Landlord PR #6, dùng DbSeeder của nhóm và thống nhất Trang chủ/Tìm phòng Tenant.

## Mở đúng dự án

Mở folder `D:\Workspace\University\WebNC\BTL\Ez Rent` trong VS Code. Nhánh hiện tại là `feature/tenant-integration`; bản tích hợp đã chuyển về đây. Checkout cũ ở ổ C chỉ giữ bản trước đó để đối chiếu.

Đã ghép `origin/main` tại `0b94c6b` (PR #6 `origin/feature/landlord-v4-pr` tại `8749e7d`) vào nền Tenant `8aada7f`, giữ các sửa chat/đăng nhập local. Merge đang chờ commit; chưa push hoặc merge main. Giữ thay đổi Google tùy chọn từ bản gốc để máy chưa có khóa Google vẫn dùng được tài khoản thường.

Hai bộ thay đổi trước khi chuyển đã lưu bằng patch, bản sao file, manifest SHA256 và Git stash. Vị trí bản sao lần này: `C:\Users\ASUS\.codex\visualizations\2026\10\13\01a135d7-a110-7673-955a-c339104538cc\tenant-sync-backup-20261025-231854`. Không xóa các stash sao lưu trước khi Dương chốt bản này.

## Chạy từ VS Code

Cần .NET SDK 8 và SQL Server LocalDB. Mở terminal PowerShell tại folder dự án:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Demo.ps1 -Database EZRent_Demo_Team_20261025 -Port 8771
```

Nhấn Ctrl+C để dừng. Sau khi đã build, có thể thêm `-NoBuild`. Đổi `-Port 8772` nếu cổng cũ đang dùng. Dừng bản đang chạy trước khi build lại.

Script dùng instance LocalDB riêng `EZRentDemo`, database `EZRent_Demo_Team_20261025`, môi trường `Demo`, nghe loopback. Chế độ Demo từ chối tên database không có tiền tố `EZRent_Demo_`. Không dùng connection string hoặc dữ liệu của database nhóm. Kết nối chỉ được đặt cho tiến trình chạy demo; không ghi thông tin kết nối riêng vào Git.

Ứng dụng chính tự áp migration và gọi **DbSeeder gốc, không sửa thuật toán sinh dữ liệu**. Database đã có tin phòng sẽ không bị seed lại. Dữ liệu SQL demo được giữ khi tắt/chạy lại, khác host SQLite thử trước đây.

## Tài khoản và kịch bản lần khởi tạo này

| Vai trò | Email | Mật khẩu demo |
|---|---|---|
| Tenant — Khải Hà Lê | `tenant1@ezrent.vn` | `Tenant@123` |
| Landlord — Anh Duy Đặng | `landlord2@ezrent.vn` | `Landlord@123` |
| Admin | `admin@gmail.com` | `Admin@123` |

Tài khoản được chọn theo quan hệ thực tế trong dataset; không mặc định rằng Tenant 1 luôn đi với Landlord 1. Dùng cửa sổ thường cho Tenant và ẩn danh cho Landlord. Dữ liệu mới ở máy khác có thể chọn cặp khác; xem `App_Data/Demo/<tên-database>/demo-links.json` hoặc dòng `DATASET DEMO` trong terminal.

- Review ba kích thước: `/review/tenant`.
- Trang chủ Tenant: `/Tenant/Dashboard`.
- Tìm phòng Tenant: `/Tenant/RentalPost`.
- Chat lần này: Tenant `/Tenant/Dashboard/Messages?ConversationId=1&View=chat`, Landlord `/Landlord/Messages?c=1`.
- Phòng công khai để xem chi tiết/khung giờ: `/Tenant/RentalPost/Details/13`.
- Hợp đồng: `/Tenant/Contract/Details/7`; bản nháp `/Tenant/Contract/Draft/7`.
- Hóa đơn có ảnh đối chiếu: `/Tenant/Invoice/Details/19`.

Các ID trên thuộc database đã khởi tạo lần này. Code và trang review lấy ID từ `/review/dataset` (chỉ có ở môi trường Demo), không gắn cứng với 101 hoặc 1.

DbSeeder gốc tạo 21 tài khoản, 60 tin phòng, 68 hội thoại và 586 tin nhắn ở lần khởi tạo; số tin nhắn sẽ tăng khi thử chat. Bổ sung tối thiểu bằng `DatasetDemoSeed`: giờ xem phòng cho các chủ nhà chưa cấu hình, một bản PDF BM03 mẫu **chưa ký**, ảnh công tơ minh họa và snapshot hóa đơn liên quan. Không thêm bảng/migration; không ghi đè giờ chủ nhà đã sửa hoặc tạo thêm phiên bản tài liệu mỗi lần khởi động. File riêng và manifest nằm trong `App_Data/Demo`, ngoài wwwroot và ngoài Git/publish.

## Phân chia Trang chủ và Tìm phòng

- Khách chưa đăng nhập giữ Trang chủ công khai của nhóm. Tenant vào `/` hoặc `/Home/Index` được dẫn về Trang chủ Tenant của Dương.
- Các URL Tìm phòng/chi tiết cũ của Tenant dẫn về giao diện Tenant, giữ giá, diện tích, tiện ích, sắp xếp và số trang.
- `RentalSearchQuery` là nơi dùng chung điều kiện lọc/sắp xếp, tin được duyệt và còn trống. Giữ giao diện và kích thước phân trang riêng: Tenant 12, khách 9.
- `RecommendationService` của Đức giữ nguyên. Gợi ý trong Tenant tiếp tục qua bộ lọc đang chọn; không làm thay đổi tổng kết quả tìm kiếm hoặc số trang.
- `_Layout.cshtml` chung tiếp tục được dùng. Không dựng lại layout Tenant riêng.

## Sửa tương thích và phần tiếp theo

Khi thử Admin trên SQL phát hiện `PostController` của main có hai action Index trùng route gây 500. Giữ bản Index có tham số search và bỏ bản cũ trùng; không thay nghiệp vụ duyệt tin.

Bản này mới nối nguồn khung giờ thật, chưa triển khai POST đặt lịch từ màn chọn giờ. Gói tiếp theo cần xử lý gửi/hủy/xác nhận theo mốc 6 giờ, giữ độc quyền 2 giờ và chống trùng ở server. Thanh toán Landlord mới vẫn xác nhận bằng tay, cần đối chiếu lại quyết định VietQR tự gạch nợ trước khi làm phần đó. Admin chưa có hộp thư trả lời TenantAdmin/LandlordAdmin. Đối chiếu tài liệu không được hiển thị thành thao tác ký hợp đồng.

Kết quả kiểm tra được ghi ở mục cập nhật hiện tại trong `tests/TenantSecurity.Tests/README.md`; các checkpoint trước đó là lịch sử.

Kết quả bàn giao lần này: **293/293 kiểm thử**, **46 kiểm tra trình duyệt**; build/publish thành công với đủ tài liệu, ảnh demo và SignalR. Không có file database, kho riêng, cấu hình Development/Local hoặc fixture kiểm thử trong gói publish đã kiểm tra. Còn 13 cảnh báo nullable từ nền mã. Ảnh phòng từ nguồn ngoài chuyển sang placeholder cả khi lỗi xảy ra trước lúc script được tải; không phụ thuộc việc mạng tải ảnh thành công mới dùng được chat.
