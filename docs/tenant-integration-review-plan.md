# Kế hoạch chỉnh Tenant sau review của Đức

> Cập nhật sau khi Dương duyệt triển khai: bản sửa đã hoàn thành trên checkout riêng `feature/tenant-integration`, dựa trên main `19d0900`, giữ WIP gốc. Dùng layout thật của nhóm, gợi ý từ main và chat SignalR hai chiều. 274/274 kiểm thử và 38 kiểm tra trình duyệt đạt; Dương đã duyệt commit/push nhánh feature để Đức review, chưa merge main. Xem [bàn giao và cách review bản 8771](tenant-integration-checkpoint.md). Nội dung bên dưới là kế hoạch đã được duyệt trước triển khai.

Ngày 21/10/2026. Đây là kết quả đối chiếu và kế hoạch để Dương review, chưa triển khai thay giao diện hoặc gộp nhánh. Không commit/push/merge main trong lượt lập kế hoạch này.

## Nguồn đã xác minh

- [Nhận xét của Đức trên PR #2](https://github.com/AnhDuckBuoi/Ez-Rent/pull/2#issuecomment-6318613433): dùng chung `_Layout.cshtml` và menu, hoàn thiện chat, dùng phần gợi ý/tìm kiếm từ main.
- Đã fetch các nhánh từ origin. `origin/main`: `19d0900325a65dd76f81326297af70632ab886d1`, đã gộp phần Admin.
- PR #2 đang mở, chưa merge, head `feature/tenant-p3` tại `afb7083`. Chưa có các gói tài khoản (`9f7e528`), phòng/tài liệu (`d86b8ed`), gửi tin (`ccdde10`). Vì vậy kết quả kiểm thử của nhánh mới không được coi là bằng chứng PR #2 đã gửi/nhận tin được.
- Nhánh local hiện tại `feature/tenant-support`, HEAD `ccdde10`; gói Hỗ trợ mới chưa commit. Program.cs có thay đổi Google cũ và `.impeccable/` là dữ liệu local, phải giữ riêng.
- `origin/feature/landlord`: `f14dc93`, có MessagesController, khung giờ và nguồn thông báo; chưa thuộc main. Nhánh này dựa trên `d86b8ed`, nên mang phần Tenant cũ hơn gói gửi tin. Không chép đè thư mục Tenant từ nhánh đó.

## Nguyên nhân chồng chéo

| Vùng | Bằng chứng trong mã | Hướng xử lý |
|---|---|---|
| Khung giao diện | Tenant dùng Discovery/Home/Messages/Rental layout riêng. CSS Tin nhắn có reset trực tiếp `body`, `a`, `button`, `svg`, `:root`. | Dùng `Views/Shared/_Layout.cshtml` thật, cùng menu và thư viện nhóm. Tách nội dung khỏi shell; giới hạn CSS Tenant, không chỉ đổi tên layout. |
| Chat | PR #2 chỉ có bản đọc. Gửi Tenant ở nhánh messages; gửi Landlord ở nhánh landlord. | Tích hợp hai đầu trên cùng nền dữ liệu rồi kiểm thử hai phiên đăng nhập riêng. Không viết chat mới song song. |
| Tìm kiếm/gợi ý | Main có SearchHistory, RecommendationService, gợi ý tại Home; Tenant có truy vấn/filter riêng. | Dùng lại dịch vụ gợi ý, dữ liệu lịch sử và đăng ký có sẵn; ghép vào màn tìm phòng P3. Không viết lại thuật toán. |
| Dữ liệu chung | Main thêm SearchHistory/migration; Tenant thêm tài liệu hợp đồng/ảnh công tơ/bản nháp và migration. | Giữ cả hai chuỗi migration, soát DbContext/snapshot/model/đăng ký dịch vụ; không chạy ứng dụng chính hoặc áp migration vào database nhóm khi chỉ đang kiểm tra mã. |
| Mã đang review | Các gói Tenant tiếp nối chưa nằm trong PR #2. | Review một nhánh tích hợp có nền rõ ràng và hướng dẫn chạy chung; không để Đức review nhánh cũ nhưng dùng kết quả test nhánh mới. |

Phần “gợi ý” đã tìm thấy là gợi ý phòng cá nhân hóa dựa trên lịch sử/hành vi. Chưa thấy endpoint tự hoàn thành từ khóa trong main, nên không diễn giải thành tính năng autocomplete đã có.

## Kiểm tra gộp trước, không đổi working tree

Đã dùng `git merge-tree` tính thử gộp HEAD đã commit với origin/main; có **5 xung đột văn bản**:

1. `.gitignore`
2. `Areas/Tenant/Controllers/FavoriteController.cs`
3. `Areas/Tenant/Views/Favorite/Index.cshtml`
4. `Program.cs`
5. `Views/Shared/_LoginPartial.cshtml`

Gộp thử HEAD với nhánh landlord không báo xung đột văn bản. Điều này chưa chứng minh hoạt động đúng: layout Landlord vẫn riêng; đọc tin hiện ghi trạng thái trong GET, cách nhận tin định kỳ khác Tenant; nguồn giờ đã có nhưng quy tắc hết hạn và thanh toán cần đối chiếu các quyết định trước. Không tự đổi phạm vi Landlord hoặc chấp nhận các quy tắc khác chỉ vì Git gộp được.

Kết quả trên chưa bao gồm patch Hỗ trợ chưa commit và không thay cho build/test sau tích hợp. Đã sao lưu patch đầy đủ, các tệp mới và bảng SHA256 vào `.git/tenant-local-backups/20261021-222122/`, gồm 29 đường dẫn đang làm và tài liệu kế hoạch này. Bản sao nằm ngoài nội dung commit, trước mọi bước tích hợp thực tế.

## Thứ tự triển khai đề xuất

### 1. Dựng nền tích hợp

Tạo `feature/tenant-integration` trong checkout riêng từ main đã fetch. Đưa các commit Tenant tiếp nối vào bằng lịch sử Git, xử lý năm xung đột theo từng chức năng, không chọn nguyên bên “ours/theirs”. Giữ quyền sở hữu/CSRF của Tenant khi kết hợp Favorite với main. Program.cs giữ đăng ký dịch vụ nhóm và Tenant, tách thay đổi Google local. Soát `.gitignore`, dependency, enum kiểm duyệt mới và migration/snapshot.

Giữ gói Hỗ trợ hiện tại ở nhánh cũ; chỉ chuyển patch hỗ trợ có chọn lọc sang nền đã ổn định. Không mang cấu hình local hoặc dữ liệu `.impeccable/` vào. Nhánh main local/remote giữ nguyên.

### 2. Đồng bộ layout trước

- Toàn bộ màn Tenant kế thừa `_Layout.cshtml` chung, menu/đăng nhập/đăng xuất theo thành phần nhóm. Bổ sung các đích Tenant đã có vào đúng menu thay vì thêm header/navbar thứ hai.
- Giữ nội dung nghiệp vụ và các form đã review. CSS/JS đặc thù chỉ tải ở màn cần dùng, qua section trong layout chung; dùng Bootstrap/token nhóm, giới hạn selector vào vùng Tenant.
- Bỏ các layout độc lập khi không còn sử dụng; giữ partial nội dung và icon cần thiết. Không tạo một `_Layout` khác chỉ đổi tên để trông như đã dùng chung.
- Review trước một nhóm đại diện: Trang chủ, Tìm phòng, Tin nhắn; sau đó chuyển các màn Tài khoản, lịch hẹn, phòng/hợp đồng/hóa đơn/sửa chữa/hỗ trợ. Kiểm tra dropdown, footer, focus và các trang Admin/Landlord không bị ảnh hưởng CSS.

### 3. Dùng lại tìm kiếm và gợi ý của main

Ghép RecommendationService vào màn tìm phòng Tenant với số lượng gợi ý nhỏ, giữ Trang chủ thiên về thông tin như Dương đã chốt. Giữ bộ lọc và phân trang số. Kết quả/gợi ý chỉ trả tin Approved + Available, không lộ tin ẩn/chưa duyệt.

Đối chiếu từ khóa, city/district, giá/diện tích, room type, amenities và cách đặt tên sort. Không ghi lịch sử lặp chỉ do đổi số trang hoặc tải lại AJAX. Dùng chung điểm ghi lịch sử/invalidate cache; lỗi gợi ý không chặn tìm phòng. Có kiểm tra riêng cho tài khoản mới/chưa có lịch sử và dữ liệu thưa. Không tự lưu dữ liệu sang database nhóm.

### 4. Hoàn thiện gửi/nhận hai phía

Tái sử dụng TenantMessagesWriter và MessagesController của Landlord. Vì phần Landlord chưa ở main, kiểm thử trên nhánh phối hợp riêng có đúng commit Landlord, không tự gộp toàn bộ công việc của người khác vào main hoặc PR của Dương.

Kiểm tra hai tài khoản riêng: Tenant mở từ phòng → gửi → Landlord đọc/trả lời → Tenant nhận phản hồi; dùng đúng một Conversation, không nhân bản do chiều Initiator/Recipient đảo nhau. Thống nhất cách cập nhật tin mới và đánh dấu đã đọc qua thao tác có kiểm tra quyền; không đánh dấu hàng loạt lịch sử chưa hiển thị. Kiểm tra CSRF, ID giả, tài khoản bị khóa, người ngoài hội thoại, nội dung được encode, phân trang và trường hợp lỗi mạng.

Hỗ trợ TenantAdmin là phần riêng: giữ gói đã làm nhưng không tuyên bố hoàn tất trả lời Admin khi chưa có màn tiếp nhận. Các khác biệt quy tắc lịch/QR/hợp đồng được ghi riêng, không tự thay quyết định nghiệp vụ của Dương trong gói sửa review này.

### 5. Nghiệm thu và gửi lại review

Build và chạy lại bộ kiểm thử trên mã đã gộp; cập nhật các test layout theo shell chung, giữ test phân quyền. Chạy thử trên SQL Server/database thử riêng sau khi có cấu hình phù hợp; kiểm tra schema/migration trước khi nhóm triển khai. Soát giao diện ba kích thước với Tenant và các màn chung đại diện; kiểm thử chat hai phía và gợi ý theo lịch sử.

267/267 ca của gói Hỗ trợ là kết quả trước tích hợp, không dùng thay cho kết quả của bản mới. Sau khi Dương review bản sửa, commit tiếng Việt theo từng gói và đưa đúng nhánh tích hợp cho Đức review. Cập nhật hoặc thay thế PR #2 có giải thích phạm vi; không force-push, tự đóng PR, đăng nhận xét hoặc merge khi chưa được yêu cầu.

## Điểm nghiệm thu để Đức review

- Một layout/menu chung thật; không header lặp hoặc CSS Tenant làm vỡ trang nhóm.
- Main mới và chức năng gợi ý gốc được dùng trong luồng Tenant, còn nguyên kiểm tra tin công khai và phân trang số.
- Chat gửi/nhận hai phía trên cùng database thử, có bằng chứng từ hai phiên riêng.
- Không mất Tài khoản, Phòng đang ở, tài liệu, ảnh công tơ, sửa chữa hoặc gói Hỗ trợ đã làm.
- PR/nhánh được review khớp đúng bản đã build/test; main chờ Đức duyệt.
