# Bàn giao bản tích hợp Tenant sau review của Đức

Ngày 21/10/2026. Dương đã duyệt triển khai sau [nhận xét trên PR #2](https://github.com/AnhDuckBuoi/Ez-Rent/pull/2#issuecomment-6318613433), bổ sung yêu cầu chat realtime bằng socket. Sau khi xem bản sửa, Dương đã duyệt commit và push lên nhánh `feature/tenant-integration` để Đức review. **Chưa merge vào main.**

## Nền mã và nơi làm việc

- Nhánh làm việc: `feature/tenant-integration`, checkout riêng tại `C:\Users\ASUS\.codex\visualizations\2026\10\13\01a135d7-a110-7673-955a-c339104538cc\ez-rent-integration`.
- Nền `origin/main`: `19d0900325a65dd76f81326297af70632ab886d1`; ghép Tenant đã commit tại `ccdde10b222269ce24b62a5ca959ae090a88ef9e`, đồng thời giữ gói Hỗ trợ chưa commit.
- Tái sử dụng riêng phần chat từ `origin/feature/landlord` tại `f14dc93f8b55980c335cd7227fbd00def9969370`. Không gộp toàn bộ nghiệp vụ Landlord từ nhánh đó.
- Đã xử lý năm xung đột văn bản: `.gitignore`, `FavoriteController.cs`, `Favorite/Index.cshtml`, `Program.cs`, `_LoginPartial.cshtml`. Không còn đường dẫn unmerged hoặc dấu xung đột; kết quả tích hợp được lưu trên nhánh feature.
- Checkout gốc `D:\Workspace\University\WebNC\BTL\Ez Rent` vẫn ở `feature/tenant-support`; không ghi đè 29 đường dẫn đang sửa của Dương. Bản sao lưu có tại `.git/tenant-local-backups/20261021-222122` trong checkout gốc.
- Nhánh chính không thay đổi. PR #2 vẫn trỏ đến gói P3 cũ; chưa thể dùng PR đó để review toàn bộ bản tích hợp này.

## Ba điểm đã sửa

| Nhận xét | Kết quả | Đọc mã chính |
|---|---|---|
| Dùng chung layout/menu | Các view Tenant dùng chính `Views/Shared/_Layout.cshtml`, menu Bootstrap chung; bỏ bốn layout độc lập Discovery/Home/Messages/Rental. CSS Tenant giới hạn trong `.tenant-surface`, không reset thanh menu toàn hệ thống. | `_Layout.cshtml`, `_TenantSharedMenu.cshtml`, `_TenantRentalNavigation.cshtml`, `tenant-integration.css` |
| Chat chưa gửi/nhận được | Tenant và Landlord đọc cùng Conversation/Message, gửi thật và nhận cập nhật realtime bằng SignalR. Giữ form POST/token, bản nháp và đường tải tin thủ công khi cần. | `ChatRealtime.cs`, hai controller Tin nhắn, `chat-realtime.js`, `tenant-messages.js`, `landlord-chat.js` |
| Dùng lại tìm kiếm/gợi ý của main | Tenant gọi `RecommendationService` gốc. `SearchHistoryRecorder` dùng chung cho tìm kiếm gốc và Tenant; không ghi lại cùng bộ lọc trong hai phút khi tải lại trang/partial. Chỉ đưa tin công khai hợp lệ vào gợi ý; giữ phân trang số. | `SearchHistoryRecorder.cs`, hai `RentalPostController`, `_Results.cshtml` |

Phần tìm được trên main là gợi ý phòng theo lịch sử/hành vi; không coi đây là endpoint tự hoàn thành từ khóa.

## Luồng chat để review

1. HTTP POST xác thực người dùng, quyền hội thoại, trạng thái tài khoản, nội dung và CSRF; lưu tin bằng bảng gốc.
2. Sau khi lưu thành công, server báo `ConversationChanged` tới đúng ID hai thành viên còn hoạt động và có vai trò phù hợp. Socket chỉ chuyển ID hội thoại; không chuyển nội dung tin.
3. Mỗi phía tải lại nội dung qua MVC có kiểm tra thành viên. Không tin ID người gửi/người nhận do trình duyệt tự khai. Hub không mở phương thức cho client tùy ý gửi tin hoặc tham gia nhóm.
4. JavaScript giữ bản nháp và vị trí đọc khi cập nhật; tự kết nối lại sau mất mạng. Lỗi phát socket không biến tin đã lưu thành lỗi gửi. Đánh dấu đọc vẫn là POST riêng; GET không ghi trạng thái đọc.

Server dùng SignalR của ASP.NET Core; client Microsoft 8.0.29 được lưu cùng mã nguồn, kèm nguồn và giấy phép trong `wwwroot/lib/signalr`. Tài liệu nền: [xác thực SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0), [JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-8.0).

Phần Landlord được lấy từ nhánh đã nêu gồm controller/base, model/tiện ích và view chat cần thiết. Bổ sung kiểm tra người nhận, CSRF, GET chỉ đọc, POST đánh dấu tin nhận, trạng thái lỗi và realtime. Các màn Landlord khác chưa thuộc phạm vi tích hợp này.

## Chạy và review đúng phiên bản

Chạy từ **checkout tích hợp** nêu trên:

```powershell
$env:EZRENT_PREVIEW_PORT = '8771'
dotnet run --project tests/TenantSecurity.Tests -- --preview
```

Mở [bản review tích hợp](http://127.0.0.1:8771/review/tenant?page=messages&device=desktop). Chọn Desktop, Tablet hoặc Mobile; lần lượt xem Trang chủ, Tìm phòng, Tin nhắn, Tài khoản, Phòng đang thuê và Hỗ trợ. Cổng **8770 là bản chạy cũ ở checkout gốc**, không dùng để kết luận về bản sửa này.

| Vai trò demo | Email | Mật khẩu | Màn xem |
|---|---|---|---|
| Tenant | `minhanh@example.test` | `TenantDemo@123` | `/Tenant/Dashboard/Messages` |
| Landlord | `chutro@example.test` | `LandlordDemo@123` | `/Landlord/Messages` |

Để thử hai phía cùng lúc, dùng hai phiên trình duyệt riêng (ví dụ thường và ẩn danh). Đăng nhập tại `/Identity/Account/Login`. Cookie localhost có thể còn từ bản 8770; nếu không vào được, đăng nhập lại bằng tài khoản demo của bản 8771. Các tài khoản trên chỉ tồn tại trong host thử độc lập. Khởi động lại host sẽ đặt lại dữ liệu demo.

Host thử chỉ nghe loopback, dùng SQLite trong bộ nhớ và nơi lưu file riêng; không chạy `Program.cs` ứng dụng chính hoặc seed/migrate SQL Server của nhóm. Chưa thay dữ liệu nhóm.

## Kết quả kiểm tra

- Build thành công, 0 lỗi; còn 13 cảnh báo nullable đã có trong nền mã.
- Bộ kiểm thử cuối: **274/274 đạt**, không bỏ qua. Bao gồm phân quyền/CSRF, dữ liệu Tenant, lịch sử tìm kiếm, tương thích yêu thích, gửi/đọc Landlord và quyền kết nối hub.
- Trình duyệt: **38 kiểm tra đạt**, không có lỗi JavaScript chưa xử lý. Đã xác nhận kết nối WebSocket thật ở hai vai trò, gửi/nhận hai chiều không tải lại document, không thêm bong bóng trùng, giữ bản nháp, người thứ ba không nhận sự kiện và nhận tin sau kết nối lại.
- Kiểm tra bảy màn ở các chiều rộng 1440, 834, 390 và 320 px: không tràn ngang/ID trùng/menu Tenant cũ; ô nhập chat hiển thị trong vùng làm việc trên màn nhỏ.
- Sinh script migration SQL Server để đối chiếu metadata ngay trong test, không mở kết nối: giữ SearchHistory cùng các bảng tài liệu/bản nháp/ảnh công tơ; snapshot không có thay đổi model đang chờ. **Chưa áp migration vào database nhóm.**
- Bằng chứng ở thư mục `integration-qa` cạnh checkout này: `integration-verified.trx`, `browser-results.json`, ảnh Home/Search/Messages theo kích thước. Đây là tài liệu kiểm tra local, không tự đưa toàn bộ ảnh/log vào Git.

## Giới hạn và bước bàn giao

Hỗ trợ TenantAdmin đã có gửi mới/gửi bổ sung, nhưng chưa có màn Admin trả lời hoặc trạng thái xử lý phiếu. Chưa kiểm thử thiết bị vật lý/Safari, triển khai nhiều máy chủ hoặc database SQL Server đang dùng của nhóm. Luồng đặt lịch thật, ký hợp đồng và thanh toán giữ nguyên phạm vi đã thống nhất trước; không coi việc chat chạy được là hoàn tất các luồng đó.

Dương đã duyệt commit nội dung tiếng Việt và push nhánh feature. Đức review [nhánh tích hợp](https://github.com/AnhDuckBuoi/Ez-Rent/tree/feature/tenant-integration) và [so sánh với main](https://github.com/AnhDuckBuoi/Ez-Rent/compare/main...feature/tenant-integration) trước khi quyết định merge. PR #2 vẫn là nhánh P3 cũ; chưa tự đổi nhánh hoặc đăng nhận xét trên PR đó. Không commit/merge vào main.
