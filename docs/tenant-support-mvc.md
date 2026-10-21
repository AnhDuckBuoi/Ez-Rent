# Hỗ trợ và phản ánh phía Tenant

> Bản tích hợp sau review Đức giữ gói hỗ trợ này, chuyển view sang layout thật của nhóm và phát cập nhật hội thoại qua SignalR sau khi lưu. Admin chưa có màn trả lời TenantAdmin. Bản review mới ở cổng 8771, xem [bàn giao tích hợp](tenant-integration-checkpoint.md); các mô tả không realtime/không đổi Program.cs bên dưới thuộc checkpoint hỗ trợ trước tích hợp.

21/10/2026. Nhánh `feature/tenant-support`, tiếp từ `ccdde10`. Dương duyệt làm phần Tenant, dùng mã gốc và trình bản chạy thử trước khi commit. Chưa commit/push gói này.

## Dữ liệu và phạm vi

Đã đối chiếu `origin/main` tại `19d0900`: `/Admin/Support` vẫn dùng RepairRequest. Không đưa phản ánh chung vào bảng sửa chữa hoặc thay controller của Đức.

Mỗi lần gửi yêu cầu mới tạo một Conversation loại TenantAdmin và tin đầu trong cùng giao dịch, dùng hai bảng gốc. Tiêu đề (100 ký tự), nhóm vấn đề và mô tả (1.000 ký tự) là văn bản của tin đầu, tổng dưới 2.000 ký tự. Không phân tích chuỗi đó để giả lập trạng thái xử lý. Chưa có bảng phiếu xử lý/phân công/deadline. Không đổi model nghiệp vụ, DbContext, migration hoặc Program.cs.

Người gửi từ Identity. Người nhận do server chọn từ tài khoản hoạt động có vai trò Admin trong bảng Identity, khác người gửi; bản đầu chọn ID tăng dần để có quy tắc xác định. Không chia tải hoặc tự chuyển người tiếp nhận cũ. Nhóm cần chốt tài khoản hỗ trợ khi triển khai; không tự tạo Admin trong database nhóm. Không có người nhận hợp lệ thì GET giải thích/khóa gửi, POST kiểm tra lại và không tạo dữ liệu một phần.

Phòng liên quan không bắt buộc: chỉ Approved + Available công khai hoặc phòng có hợp đồng Active của chính Tenant. Form đưa tối đa 30 phòng hợp lệ từ phòng được truyền vào, yêu thích và đang thuê; ưu tiên phòng đã truyền vào. POST kiểm tra lại để chặn phòng vừa ngừng công khai. Không lộ phòng riêng của Tenant khác.

## Hành vi

- Tin nhắn, thông tin phòng trong chat và Hướng dẫn có lối mở form thật, hoạt động cả khi tắt JavaScript. POST có Tenant authorization, ID đăng nhập, CSRF, input riêng và giới hạn request.
- Thành công chuyển đến hội thoại mới và dùng thành phần thông báo chung. **Yêu cầu đã gửi** mở bộ lọc Hỗ trợ trong Tin nhắn: 10 hội thoại/trang, 40 tin/trang, phân trang số. Mã yêu cầu là ID hội thoại. GET chỉ đọc.
- Gửi bổ sung dùng composer cũ. Cả reader và writer kiểm tra thành viên, tài khoản tiếp nhận vẫn hoạt động và có quyền Admin. Mất quyền/khóa thì giữ lịch sử chỉ đọc. Không tự chuyển sang Admin khác.
- Lỗi form thường trả lại dữ liệu và lỗi cạnh trường. JavaScript khóa nút/ô nhập khi chờ, giữ nội dung khi lỗi mạng và hỏi trước khi rời trang có nội dung. Không đưa nội dung vào URL/localStorage/log chủ động. Bản nháp ở form/tab, không bảo đảm sau reload/đóng tab.
- Mất phản hồi có thể xảy ra sau khi đã lưu: hướng dẫn kiểm tra **Yêu cầu đã gửi**, không tự gửi lại. Khóa nút tránh hai lần nhấn cùng trang, không bảo đảm duy nhất nếu chủ động gửi lại hoặc mở nhiều tab.

## Đọc mã và bàn giao

`TenantSupportModels.cs` nhận input riêng; `TenantSupportService.cs` chọn người nhận/phòng và lưu giao dịch; `SupportController.cs` xử lý GET/POST, lỗi JSON/form; `Views/Support/Create.cshtml` và `tenant-support.css/js` dùng shell Tenant chung. Reader/writer Tin nhắn bổ sung lọc và gửi tiếp. Hộp thoại chỉ xem trước cũ được bỏ, không giữ hai cách gửi.

Đức cần ghép màn đọc/trả lời ConversationType.TenantAdmin theo đúng người nhận hoặc quy tắc đội hỗ trợ thống nhất sau. Không nhận SenderId từ form; kiểm tra quyền ở GET/POST, chỉ đánh dấu tin nhận đã hiển thị, giữ lịch sử. `/Admin/Support` hiện tại chưa đọc các yêu cầu này. Chưa có UI trả lời Admin, trạng thái giải quyết, realtime, đính kèm hoặc tự phát thông báo. Phản hồi preview ghi rõ minh họa, không phải Admin thật trả lời.

## Kiểm chứng và review

TenantSupportTests thêm **23 ca** kiểm tra HTTP tạo/lưu thật, danh tính server, không ghi sửa chữa, giới hạn input/XSS, quyền phòng/trang cũ, Admin thiếu/khóa/mất quyền, gửi tiếp/quyền sở hữu, phân trang và CSRF/role. Build đạt, 0 lỗi; còn 16 cảnh báo nullable có sẵn ngoài gói. Bộ kiểm thử cuối **267/267 đạt**, không bỏ qua. Một ca của lượt đầu vẫn kiểm tra hộp thoại mẫu cũ, đã cập nhật theo form thật. Host HTTP/SQLite riêng, không chạy Program.cs chính hoặc SQL Server nhóm.

Chrome đạt **19 kiểm tra**, không lỗi JavaScript: 1440/834/390/320 px không tràn ngang hoặc trùng ID; form/gửi mới/tải lại, gửi bổ sung, lọc hỗ trợ, phản hồi minh họa và cập nhật đọc, thông báo chung, khóa gửi hai lần, validation cạnh trường, lỗi máy chủ/mất phản hồi giữ nội dung và không tự gửi lại, form khi tắt JavaScript. Đã xem ảnh desktop/tablet/mobile và xác nhận lại cách xếp nút trên điện thoại nhỏ. TRX, script, JSON và ảnh nằm ở artifact `tenant-support` ngoài Git.

```powershell
dotnet run --project tests/TenantSecurity.Tests --no-build -- --preview
```

Mở `http://127.0.0.1:8770/review/tenant?page=support&device=desktop`, đổi Tablet/Mobile. Thử gửi, mở Yêu cầu đã gửi rồi gửi bổ sung. Có một phản hồi minh họa cho review. Dữ liệu chỉ ở bộ nhớ host riêng, reset khi khởi động lại. Chưa kiểm chứng SQL Server nhiều máy/deadlock, Safari hoặc máy vật lý.
