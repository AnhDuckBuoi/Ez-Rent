# Bản thử hợp đồng và ảnh công tơ

## Phạm vi được duyệt

Dương duyệt dựng giao diện trước khi ghép luồng lưu trữ thật. Bản đầu hoãn ký trên web, hỗ trợ chuẩn bị mẫu và lưu bản được ký bên ngoài. Bản thử nằm trên nhánh `feature/tenant-my-room`, chưa commit/push.

Mở `/review/tenant?page=documents` trên máy chủ xem thử 8770; chọn Desktop, Tablet hoặc Mobile. Trang riêng: `/review/tenant-documents`. Các route và ba tệp giao diện chỉ được phục vụ trong nhánh `preview` của `TenantTestApp`, không đăng ký ở ứng dụng chính.

## Cách review

1. Trong Hợp đồng, sửa họ tên, điện thoại, địa chỉ, ngày thuê và khoản tiền. Nhấn **Tạo bản xem trước**. Trước khi nhấn, nội dung bản nháp trước giữ nguyên.
2. Nhấn **In / Lưu PDF**, chọn lưu PDF trong hộp thoại in của trình duyệt. Bản in chỉ có mẫu hợp đồng, không có menu/form. Đây là mẫu bố cục chưa hoàn chỉnh; nội dung phải được nhóm chốt trước khi sử dụng.
3. Chọn tài liệu PDF/JPG/PNG để xem trong khu vực bản tải lên. Thử xác nhận đối chiếu rồi chọn tệp thứ hai: xác nhận được đặt lại, phiên bản trước vẫn mở lại được. Bản thử giữ tối đa 10 tệp trong bộ nhớ của tab.
4. Chuyển Hóa đơn & công tơ. Xem riêng chỉ số điện/nước và ảnh, chọn ảnh trên máy để thử phóng to. Ảnh ban đầu để trống, không dùng ảnh minh họa như thể là ảnh công tơ thật.
5. Thử Escape/Đóng để đóng tài liệu/ảnh; tiêu điểm trở lại nút mở. Đổi ba thiết bị trong viewer giữ nội dung đang thử, tải lại trang sẽ xóa nội dung.

## Ranh giới của bản thử

- Số liệu và tên là minh họa cố định, không đọc dữ liệu hợp đồng thật. Các trường phía chủ nhà/khoản tiền cho sửa chỉ để thử điền mẫu; chưa phải quy tắc ghi dữ liệu cho Tenant.
- Không gửi mạng khi tạo mẫu, chọn tệp hoặc xác nhận. Không có upload, lưu database, localStorage, ký điện tử hay thay đổi trạng thái hợp đồng/hóa đơn.
- Tệp được mở qua object URL, không có đường dẫn công khai. Tối đa 10 MB cho tài liệu, 5 MB cho ảnh công tơ; kiểm tra sơ bộ định dạng và giải mã ảnh để hỗ trợ preview. Những kiểm tra này không thay thế xử lý upload phía máy chủ.
- Giao diện header kế thừa CSS hiện có; HTML bản thử là bản sao phục vụ review. Khi ghép MVC phải dùng lại component Razor chung.
- Ở luồng thật, chủ nhà cung cấp chỉ số và ảnh, Tenant xem/đối chiếu. Không biến các ô chọn ảnh thử thành quyền sửa hóa đơn của Tenant.

## Việc tiếp theo sau khi duyệt

- Chốt mẫu và các trường còn thiếu, thông tin tại thời điểm lập hợp đồng, ngày hiệu lực do hai bên thỏa thuận; tách phiên bản tài liệu và xác nhận đối chiếu. Không suy ra hệ thống đã xác minh pháp lý từ trạng thái thao tác.
- Thiết kế kho tệp riêng, kiểm tra quyền tải/xem, loại tệp, kích thước và lịch sử; không để tài liệu hợp đồng nhạy cảm dưới URL tĩnh công khai.
- Giữ chỉ số, đơn giá và ảnh của đúng phòng/kỳ tại lúc phát hành hóa đơn; không cho sửa nguồn ghi đè căn cứ hóa đơn cũ. Tái sử dụng `MeterReading` và liên kết `Invoice.MeterReadingId`, bổ sung cách lưu phiên bản hoặc dữ liệu chốt sau khi thống nhất với Đức.
- Chỉ bổ sung model/migration và phần ghi dùng chung sau khi chốt dữ liệu/phân quyền. Không áp dụng migration vào database nhóm trong bản thử.

## Kiểm chứng

- Build test host thành công (0 lỗi, 0 cảnh báo trong lượt build tăng dần).
- **18 kiểm tra trình duyệt qua**: hợp đồng/hóa đơn ở 1440, 834, 390, 320, 938 px; snapshot độc lập và mã hóa nội dung nhập; ngày kết thúc; in đúng phạm vi; tệp không hợp lệ, lịch sử phiên bản và xác nhận lại; hai ảnh riêng/phóng to/tiêu điểm bàn phím; tải lại xóa tệp; viewer ba thiết bị. Không lỗi JavaScript và không request ghi dữ liệu.
- Bản PDF một trang được kết xuất bằng Poppler, kiểm tra trực quan dấu tiếng Việt và bố cục. Dùng Times New Roman cho mẫu giấy; giao diện ứng dụng giữ Segoe UI.
- Bằng chứng tại thư mục artifact của chat: `tenant-documents-browser.json`, script tương ứng, ảnh ba thiết bị và `tenant-contract-layout-sample.pdf` cùng ảnh kết xuất. Ảnh dùng trong bài kiểm tra upload là fixture kỹ thuật, không phải ảnh công tơ thật.
- Chỉ kiểm tra Chromium trên Windows và viewport mô phỏng; chưa kiểm tra Safari, máy thật hoặc bàn phím ảo. Chưa kiểm tra lưu trữ/upload thật vì bản thử không có chức năng đó.
- `Program.cs` giữ nguyên SHA256 `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`; không sửa model, Landlord/Admin hay database nhóm.
