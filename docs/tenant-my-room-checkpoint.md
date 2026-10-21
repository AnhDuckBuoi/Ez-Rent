# Bàn giao phòng đang ở và tài liệu thuê phòng

Ngày kiểm tra: 20/10/2026. Nhánh: `feature/tenant-my-room`, tiếp từ `feature/tenant-account` tại `9f7e528`. Dương duyệt đưa gói lên nhánh chức năng để Đức review; không tự gộp vào `main`.

## Kết quả trong gói

- Tổng quan phòng đang ở, hợp đồng/hóa đơn có lọc và phân trang số; menu chung trên desktop, tablet và mobile.
- Gửi, sửa và hủy yêu cầu sửa chữa theo quyền sở hữu và trạng thái đang chờ; giữ lịch sử.
- Đọc/tải tài liệu hợp đồng riêng, lịch sử phiên bản và xác nhận đã đối chiếu đúng bản. Xác nhận không phải ký trên web.
- Dịch vụ lưu tài liệu và bộ đối chiếu hóa đơn với hai ảnh công tơ đúng phòng/kỳ; tệp ngoài `wwwroot`, không đưa dữ liệu runtime vào Git hoặc publish.
- Mẫu BM03 gốc được giữ nguyên; bản nháp dùng nước theo m³ đã chốt. Form thông tin Tenant lưu bản chụp mới mỗi lần, bản cũ chỉ đọc; chặn trang cũ/nguồn hợp đồng vừa đổi, giữ riêng phụ lục điều khoản khi in.
- Tái sử dụng Identity, model nghiệp vụ và kiểm tra ảnh đại diện gốc; phần kiểm tra ảnh được tách thành dịch vụ dùng chung.

## Kiểm chứng trước khi commit

- `dotnet build tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj --no-restore`: thành công, 0 lỗi; 16 cảnh báo nullable có sẵn ngoài gói.
- `dotnet test tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj --no-build --no-restore`: **214/214 đạt**, 0 thất bại, 0 bỏ qua ngày 20/10/2026. Tệp TRX nằm trong artifact của chat, không đưa vào Git.
- Bộ kiểm thử chạy HTTP/SQLite riêng, không chạy entry point ứng dụng chính hoặc ghi SQL Server nhóm. Các lượt kiểm tra trình duyệt/bản in trước đó và giới hạn được ghi trong tài liệu từng gói; không nhận là đã chạy lại trên máy thật hôm nay.

## Phần Đức cần review và phối hợp

Hai migration nguồn `20261017063240_LuuTaiLieuHopDongVaAnhCongTo` và `20261017065816_BoSungBanNhapThongTinNguoiThue` **chưa áp dụng database nhóm**. Review model, ràng buộc và phương án lưu/backup tệp trước khi triển khai. Bốn độ dài khóa Identity trong DbContext chỉ được ghi rõ để giữ schema cũ; migration không thay bảng Identity.

Controller Landlord cũ chưa gọi dịch vụ cung cấp tài liệu hoặc lưu bộ ảnh hóa đơn. Cần ghép quyền, thời điểm phát hành và giao dịch ghi để không công bố hóa đơn khi lưu ảnh thất bại. Chưa đóng băng toàn bộ trường tiền hóa đơn trước các thao tác cũ. Thông tin Landlord còn thiếu trong BM03 vẫn để trống.

Chưa triển khai ký trên web, VietQR tự gạch nợ, gửi tin/hỗ trợ, nguồn giờ xem phòng và cơ chế giữ giờ hai phía. Các khoảng hở phân quyền qua route Landlord/global vẫn cần xử lý riêng. Kiểm thử hiện tại không thay cho SQL Server, OAuth và thiết bị vật lý.

Kiểm tra tệp là kiểm tra sơ bộ header/dung lượng/chiều ảnh, không phải giải mã đầy đủ hoặc quét mã độc. Hai ảnh công tơ trong fixture chỉ là sơ đồ minh họa, không phải ảnh chụp thật.

## Chạy review và đọc mã

```powershell
dotnet run --project tests/TenantSecurity.Tests -- --preview
```

Mở `http://127.0.0.1:8770/review/tenant?page=room`, sau đó chọn Hợp đồng, Hóa đơn, Sửa chữa, Tài liệu hoặc Bản nháp BM03 và đổi ba kích thước. Dữ liệu/tệp thử tách riêng và reset khi khởi động lại.

- [Phòng đang ở và sửa chữa](tenant-my-room-mvc.md)
- [Kho tài liệu, ảnh công tơ và điểm nối Landlord](tenant-documents-mvc.md)
- [Form và lịch sử bản nháp BM03](tenant-contract-draft.md)

## Phạm vi thay đổi cấu hình

`Program.cs` chỉ đưa bốn dòng đăng ký dịch vụ mới của gói vào commit. Thay đổi Google có điều kiện đã có trên máy từ trước được giữ riêng, không đưa vào nhánh. `.impeccable/`, kết quả kiểm thử, dữ liệu cá nhân runtime và ba bài tập Word không thuộc commit này.
