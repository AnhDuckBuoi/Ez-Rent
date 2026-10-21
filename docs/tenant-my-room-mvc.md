# Phòng đang ở, hợp đồng, hóa đơn và sửa chữa

## Phạm vi đã duyệt

Gói này nằm trên nhánh `feature/tenant-my-room`, tiếp từ gói tài khoản `9f7e528`. Phần dưới ghi lại gói Phòng đang ở ban đầu bằng model sẵn có, chưa thêm bảng/migration, không chạy database nhóm và không sửa Admin/Landlord. Phạm vi tài liệu/BM03 bổ sung sau đó và kiểm tra trước khi commit được tổng hợp trong [bàn giao ngày 20/10/2026](tenant-my-room-checkpoint.md).

- `/Tenant/MyRoom`: chọn phòng trong các hợp đồng đang hiệu lực, thông tin chủ nhà, tiền thuê theo hợp đồng, tổng chưa thanh toán, ba hóa đơn và ba yêu cầu sửa chữa gần đây.
- `/Tenant/Contract`: danh sách có lọc trạng thái, phân trang số và chi tiết hợp đồng.
- `/Tenant/Invoice`: danh sách có lọc hợp đồng/trạng thái, phân trang số, chi tiết khoản thu và chỉ số điện nước gắn với hóa đơn.
- `/Tenant/RepairRequest`: danh sách, chi tiết, gửi/sửa/hủy yêu cầu thật bằng luồng hiện có. Hủy chỉ đổi trạng thái, giữ lịch sử; chỉ sửa/hủy khi còn `Pending`.
- Menu trên chung có Phòng đang ở từ 768px. Một partial mobile chung có Trang chủ / Tìm phòng / Phòng đang ở / Tin nhắn / Cá nhân. Chat vẫn ẩn menu dưới theo cơ chế cũ khi đang mở cuộc trò chuyện.

Ký hợp đồng và thanh toán VietQR nằm ngoài gói này. Không thêm nút thanh toán, nút xác nhận đã trả hoặc tự ghi nhận thanh toán. Các giới hạn phân quyền ở route tài chính Landlord/global đã ghi trong `docs/tenant-security-review.md` vẫn cần xử lý riêng.

## Bản đồ đọc mã

1. `Areas/Tenant/Controllers/MyRoomController.cs`: lấy danh tính hiện tại, các hợp đồng `Active` của người đó, chọn hợp đồng hợp lệ, rồi truy vấn hóa đơn/yêu cầu theo phạm vi sở hữu. Không lấy trạng thái hoặc số tiền từ form.
2. `Areas/Tenant/ViewModels/MyRoomModels.cs`: dữ liệu trang tổng quan, bộ lọc và kết quả phân trang cho ba danh sách. Các record chỉ dùng để đọc; form ghi tiếp tục dùng `RepairRequestInputModel` riêng.
3. `Areas/Tenant/Controllers/ContractController.cs`, `InvoiceController.cs`: truy vấn sở hữu sẵn có, thêm lọc/phân trang. Hợp đồng và hóa đơn chỉ có GET. Chi tiết hóa đơn chỉ lấy chỉ số điện nước nếu khớp cả phòng và kỳ.
4. `Areas/Tenant/Controllers/RepairRequestController.cs`: tái sử dụng kiểm tra hợp đồng đang hiệu lực và cập nhật có điều kiện theo trạng thái. Thêm thông báo chung, chọn sẵn phòng hợp lệ, validation URL ảnh và trang phục hồi khi trạng thái vừa đổi.
5. `Areas/Tenant/Services/TenantRentalPresentation.cs`: nhãn tiếng Việt, định dạng tiền/ngày/kỳ, số trang, màu trạng thái. Giá trị tài chính được đọc từ hợp đồng/hóa đơn đã lưu, không tính lại từ giá tin phòng.
6. `Areas/Tenant/Views/Shared/_TenantRentalLayout.cshtml`: ghép layout Home và tab Tổng quan / Hợp đồng / Hóa đơn / Sửa chữa. Hai partial dòng hóa đơn/yêu cầu được dùng lại trong tổng quan và danh sách; `_RepairForm` dùng chung cho gửi và sửa.
7. `wwwroot/css/tenant-rental.css`, `wwwroot/js/tenant-rental.js`: bố cục responsive, focus, bộ đếm mô tả và trạng thái ảnh lỗi. Form vẫn hoạt động khi tắt JavaScript.

Luồng đọc: request → controller kiểm tra danh tính/bộ lọc → truy vấn EF có điều kiện sở hữu → model trang → Razor và partial chung. Luồng ghi sửa chữa: form có anti-forgery → input riêng → kiểm tra phòng/trạng thái → cập nhật có điều kiện → thông báo và chuyển về danh sách.

## Các quyết định giữ tương thích

- Phòng đang ở dùng `ContractStatus.Active`, đúng với `MyRentedPosts` hiện tại. Không tự đổi trạng thái theo ngày bắt đầu/kết thúc trong lượt tải trang. Hợp đồng hết hiệu lực vẫn tra cứu được ở danh sách/chi tiết.
- Nếu có nhiều hợp đồng, mặc định chọn hợp đồng bắt đầu gần nhất, sau đó theo mã giảm dần; có bộ chọn phòng bằng GET. Mã hợp đồng không thuộc người dùng hoặc không Active trả 404.
- Tổng chưa thanh toán gồm `Unpaid`, `Overdue`, `AwaitingConfirmation`; nhãn hiển thị rõ bao gồm chờ đối soát. Loại `Paid` và `Cancelled`. Số tiền lấy nguyên từ `TotalAmount`; cộng các giá trị decimal sau truy vấn để dùng được cả SQL Server và SQLite.
- Không tự suy ra trạng thái quá hạn từ ngày đến hạn; hiển thị trạng thái đã lưu. Kỳ là `yyyy-MM`, hiển thị `MM/yyyy`; ngày hợp đồng/hóa đơn giữ nguyên ngày lưu. Thời điểm gửi/cập nhật sửa chữa lưu UTC được hiển thị giờ Việt Nam.
- Mỗi danh sách 10 dòng/trang, có thứ tự và mã làm điều kiện sắp xếp phụ; số trang vượt cuối được đưa về trang cuối, số trang/bộ lọc không hợp lệ trả 400. Liên kết phân trang giữ bộ lọc đang dùng.
- Bộ lọc hợp đồng/phòng kiểm tra sở hữu trước khi truy vấn. Điều này áp dụng cả khi người dùng sửa URL hoặc thêm trường danh tính giả.
- Chỉ số điện nước không khớp phòng hoặc kỳ sẽ không hiển thị. Đây là bảo vệ ở lúc đọc, không sửa dữ liệu cũ trong database.
- Yêu cầu sửa chữa chỉ có `RentalPostId`, chưa có FK hợp đồng. Tổng quan hiển thị các yêu cầu của chính người dùng tại phòng được chọn, có thể bao gồm lịch sử lần thuê trước của cùng người đó. Không suy diễn thành yêu cầu thuộc riêng một hợp đồng.
- Ảnh sửa chữa tiếp tục dùng URL tùy chọn có sẵn; chưa thêm upload file cho sửa chữa. Chấp nhận HTTP/HTTPS, mã hóa nội dung qua Razor; ảnh lỗi có thông báo. Ảnh phòng chỉ chọn `MediaType.Image`, theo ảnh chính/thứ tự hiển thị, bỏ qua URL không an toàn.

## Review

```powershell
dotnet run --project tests/TenantSecurity.Tests --no-build -- --preview
```

Mở `http://127.0.0.1:8770/review/tenant?page=room`. Chọn Desktop / Tablet / Mobile và các trang Phòng đang ở, Hợp đồng, Hóa đơn, Sửa chữa. Toàn bộ là MVC thật trên SQLite tạm; dữ liệu demo được tạo riêng trong `SeedRentalPreviewAsync`, mất khi khởi động lại. Không kết nối database nhóm.

Gợi ý review:

1. Đổi qua phòng thứ hai rồi quay lại phòng Cầu Giấy; tiền thuê và hóa đơn phải đổi theo phòng.
2. Mở hợp đồng, kiểm tra điều khoản và đi tới hóa đơn của đúng hợp đồng.
3. Ở Hóa đơn, thử Chưa thanh toán / Đã thanh toán / Chờ đối soát, mở trang 2 và xem chi tiết hóa đơn đầu tiên có chỉ số điện nước.
4. Gửi yêu cầu sửa chữa từ phòng, sửa nội dung khi còn chờ, rồi hủy qua trang xác nhận. Mở lại chi tiết để thấy lịch sử Đã hủy và không còn nút sửa/hủy.
5. Mở yêu cầu Đang xử lý/Đã hoàn tất để đối chiếu phản hồi và trạng thái thao tác. Xem trên ba kích thước; bản review là mô phỏng viewport, chưa thay kiểm tra bàn phím ảo/thiết bị vật lý.

Nếu cần đăng nhập lại sau khi host restart: tài khoản demo `minhanh@example.test`, mật khẩu `TenantDemo@123`, chỉ tồn tại trong host test. Cơ chế cookie được giải thích tại `docs/tenant-account-mvc.md`.

## Kiểm chứng

- Build thành công; chỉ có 16 cảnh báo nullable có sẵn ngoài các file mới.
- Suite đầy đủ **176/176 qua** tại `tenant-my-room-final.trx`; sau đó thêm ca lọc loại media và xác nhận nhóm Phòng đang ở **23/23 qua** tại `tenant-my-room-media.trx` (suite hiện có 177 ca).
- Lượt trình duyệt đầu **81 kiểm tra qua**, không lỗi JavaScript: 10 trang × 5 độ rộng (1440, 834, 390, 768, 320), menu của 5 trang × 5 độ rộng, chọn phòng, phân trang giữ bộ lọc, gửi/sửa/hủy giữ lịch sử và viewer ba thiết bị.
- Lượt xác nhận **12/12 qua** sau khi thu gọn ảnh: tổng quan, chi tiết hóa đơn, form sửa chữa và danh sách sửa chữa ở ba kích thước. Ảnh phòng cao khoảng 372px desktop, 474px tablet, 210px mobile; không tràn ngang.
- Bằng chứng ở thư mục artifact của chat: `tenant-rental-browser.json`, `tenant-rental-confirmation.json`, `tenant-rental-{desktop,tablet,mobile}-{room,invoice,create,repairs}.png` và ảnh viewport tương ứng.
- `Program.cs` có thay đổi cục bộ từ trước, được giữ nguyên; `.impeccable/` không thuộc gói. SHA256 Program: `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`.


## Phần tài liệu tiếp theo

Phần trên ghi lại checkpoint Phòng đang ở ban đầu. Sau đó Dương đã duyệt ghép kho tài liệu và ảnh công tơ, cung cấp BM03 và chốt nước theo m³. Phạm vi mới có model/migration nguồn nhưng chưa áp dụng database nhóm; xem `docs/tenant-documents-mvc.md` để biết luồng Tenant hiện tại và phần phối hợp Landlord còn lại.
