# Gửi tin và trạng thái đọc phía Tenant

> Bản tích hợp sau review Đức đã chuyển sang layout chung và bổ sung chat Tenant–Landlord qua SignalR/WebSocket, tái sử dụng controller/dữ liệu phía Landlord. 274/274 kiểm thử và 38 kiểm tra trình duyệt đạt. Xem [bàn giao bản tích hợp](tenant-integration-checkpoint.md); những giới hạn realtime/Landlord bên dưới thuộc checkpoint trước đó.

> Cập nhật gói kế tiếp 21/10/2026: hỗ trợ TenantAdmin đã có form gửi mới và gửi bổ sung phía Tenant; xem [hỗ trợ/phản ánh](tenant-support-mvc.md). Các giới hạn TenantAdmin dưới đây mô tả checkpoint Tin nhắn trước đó.

21/10/2026. Nhánh `feature/tenant-messages`, tiếp từ `d86b8ed`. Dương duyệt gửi văn bản cho chủ nhà, giữ giao diện ba kích thước và dùng lại mã gốc. Sau khi xem thử, Dương duyệt commit lên nhánh này để Đức review; chưa được gộp vào nhánh chính.

## Hành vi

- Chi tiết phòng có **Nhắn chủ nhà**. GET mở hội thoại đúng Tenant/phòng/chủ nhà nếu đã có; nếu chưa có chỉ dựng màn soạn. Liên hệ mới chỉ mở cho phòng được duyệt/còn trống, chủ nhà hoạt động và khác người thuê. Không tạo hội thoại rỗng khi GET.
- Tin đầu tiên tạo hội thoại và tin trong cùng giao dịch. Người gửi lấy từ phiên đăng nhập, người nhận lấy từ phòng trên server. `ContactStamp` chỉ kiểm tra trang cũ: nếu chủ phòng thay đổi sau khi mở trang, POST bị chặn để người thuê xem lại người nhận. Không coi dấu này là chữ ký hoặc thông tin xác thực.
- Hội thoại cũ tiếp tục trao đổi giữa hai thành viên khi phòng ngừng công khai; thông tin phòng vẫn bị che theo quy tắc cũ. Tài khoản liên quan bị khóa thì chỉ đọc. Hội thoại TenantAdmin vẫn chỉ đọc trong gói này.
- Nội dung từ 1 đến 2.000 ký tự, bỏ khoảng trắng hai đầu. Không nhận danh tính, thời gian hoặc cờ đã đọc từ form. Form gửi vẫn hoạt động khi tắt JavaScript.
- Khóa lần gửi đang chờ của từng hội thoại; giữ bản nháp khi chuyển màn, chỉ xóa phần đã xác nhận lưu. Lỗi ở cạnh ô nhập. Nếu mất phản hồi sau khi server đã lưu, giữ nội dung và hướng dẫn **Tải tin mới** để đối chiếu, không tự gửi lại.
- **Tải tin mới** là GET và giữ bản nháp. Chưa có SignalR/realtime, trạng thái online hoặc đang nhập.
- Đọc dùng POST riêng cho tối đa 40 ID. JavaScript chỉ đánh dấu tin nhận nhìn thấy đủ phần trong vùng chat sau khi dừng cuộn khoảng 650 ms; tab ẩn/chat bị ẩn trong hộp thư mobile không tự đánh dấu. Tin dài dùng ngưỡng theo phần viewport nhìn thấy. Không đánh dấu cả hội thoại hoặc tin mới vừa đến.
- Khi cập nhật đọc lỗi có nút thử lại. Tắt JavaScript thì người dùng chủ động đánh dấu các tin trong trang. Phân trang số giữ 10 hội thoại/trang và 40 tin/trang.

## Đọc mã theo luồng

1. `TenantMessageWriteModels.cs`: input riêng cho gửi/đọc, chỉ chọn một mục tiêu hội thoại hoặc phòng.
2. `TenantMessagesWriter.cs`: điều kiện thành viên dùng chung, tìm hội thoại, kiểm tra tài khoản, gửi trong giao dịch Serializable và cập nhật đọc có điều kiện. Dùng bảng gốc, không model/schema mới.
3. `TenantMessagesReader.cs`: dùng lại phân trang, ảnh an toàn và lịch gốc; thêm màn chưa có hội thoại và quyền gửi. GET vẫn chỉ đọc.
4. `DashboardController.SendMessage` và `ReadMessages`: Tenant/CSRF/validation/giới hạn request; JSON cho AJAX, HTML/chuyển trang cho form thường. Nội dung không đi qua URL, TempData hoặc log chủ động.
5. Các view Tin nhắn và chi tiết phòng: form/token thật, lỗi cạnh trường, lối tải tin mới. JavaScript giữ bản nháp trong bộ nhớ tab theo hội thoại/phòng, không lưu nội dung vào localStorage; không thêm bong bóng giả trước khi lưu.

Luồng gửi: form POST → xác thực/CSRF/input → xác định thành viên/người nhận → giao dịch lưu → GET hội thoại → Razor hiển thị tin đã lưu. Không đổi Program.cs, Admin/Landlord, DbContext hoặc migration trong gói.

## Kiểm chứng và giới hạn

- Build đạt, 0 lỗi; còn 16 cảnh báo nullable có sẵn ngoài gói. Suite cuối **244/244 đạt**, trong đó **30 ca mới** tại `TenantMessageWriteTests`. TRX: `tenant-messages-final.trx`. Nhóm đọc/ghi trung gian đã đạt trước khi bổ sung kiểm tra trang cũ khi đổi chủ.
- Trình duyệt **22 kiểm tra đạt**, không lỗi JavaScript: gửi/lưu/tải lại, mở lại hội thoại theo phòng, nhấn hai lần, chuyển đi/quay lại khi đang gửi, lỗi mạng/mất phản hồi, giữ bản nháp, đọc và thử lại, Enter xuống dòng, form gửi/lỗi validation không cần JavaScript. Độ rộng 1440/834/390/375/320 px và điện thoại ngang 844×390 với cảm ứng/motion giảm. Bố cục đã xem qua ảnh, không tràn ngang trong các ca. Chưa kiểm chứng máy thật hoặc Safari; mô phỏng prefers-color-scheme không tạo theme tối mới cho sản phẩm.
- TRX, JSON và ảnh tại artifact `tenant-messages-write`, không đưa vào Git. Một lượt kiểm thử đầu bị lỗi do bộ thử chưa giữ đúng cookie CSRF, đã sửa helper để lấy cặp token/cookie khớp; không thay cơ chế CSRF ứng dụng.

Chưa có UI gửi/trả lời phía Landlord/Admin; không coi thử gửi/lưu từ Tenant là hoàn tất trò chuyện hai phía. Gửi hỗ trợ, tệp đính kèm, sửa/xóa tin và thông báo đẩy nằm ngoài gói.

Serializable bảo vệ quyết định tìm/tạo và nguồn chủ phòng. Chưa kiểm chứng tranh chấp/deadlock hoặc nhiều máy trên SQL Server. Không tự retry khi kết quả commit không rõ. Khóa nút tránh nhấn hai lần trong cùng tab, không bảo đảm chỉ lưu một lần nếu client chủ động gửi lại sau mất phản hồi. Kiểm thử dùng HTTP/SQLite riêng, không database nhóm hoặc máy thật/Safari.

## Review

```powershell
dotnet run --project tests/TenantSecurity.Tests -- --preview
```

Mở `http://127.0.0.1:8770/Tenant/RentalPost`, chọn phòng rồi **Nhắn chủ nhà**. Thử gửi, tải lại, chuyển hội thoại khi đang soạn và **Tải tin mới**. Hoặc `/review/tenant?page=messages` để đổi ba kích thước. Tin chỉ lưu trong database minh họa; khởi động lại host sẽ reset.

Landlord khi ghép cần dùng cùng bảng, kiểm tra thành viên và chỉ đánh dấu tin nhận đã hiển thị. Không nhận người gửi từ form hoặc tạo hội thoại chỉ theo tên. Hỗ trợ/phản ánh cần thống nhất riêng với Admin, không ghép nhầm vào sửa chữa.
