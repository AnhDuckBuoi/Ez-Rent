# Chức năng Chủ trọ (Landlord) – EZ-Rent

Tài liệu này mô tả phần Landlord mới: phạm vi, cách đồng bộ với Tenant, an toàn thông tin và các ý dùng cho **Chương 4** của báo cáo.

## 1. Nguyên tắc khi thêm code

- **Chỉ thêm file mới, không sửa file nào có sẵn** (Program.cs, _Layout, Models, DbContext, migration, code Tenant/Admin giữ nguyên).
- **Không đổi cơ sở dữ liệu**: không thêm bảng, cột hay migration. Mọi chức năng dùng đúng các bảng trong `InitialFullSchema`.
- Tài khoản nhận tiền VietQR lưu trong bảng `AspNetUserClaims` có sẵn của Identity (không cần cột mới).
- Controller mới có tên khác controller cũ (`Rooms`, `Viewings`, `Contracts`…), nên không trùng route.

## 2. Chạy thử

1. Lấy code, build và chạy như bình thường (`dotnet ef database update`, `dotnet run`). Không cần migration mới.
2. Đăng nhập bằng tài khoản có vai trò **Landlord**, mở `/Landlord/Overview`.
3. Luồng thử đầy đủ:
   - Đăng tin phòng kèm ảnh, rồi gửi duyệt. Admin duyệt tin.
   - Tenant đặt lịch xem phòng. Chủ trọ xác nhận lịch, sau giờ hẹn thì đánh dấu hoàn tất.
   - Chủ trọ lập hợp đồng và gửi người thuê.
   - Ghi chỉ số điện nước, lập hóa đơn có VietQR, xác nhận đã nhận tiền.
   - Tenant gửi yêu cầu sửa chữa. Chủ trọ tiếp nhận rồi hoàn tất.
4. Cài **Nhận tiền → Tài khoản nhận tiền** trước khi lập hóa đơn để có mã VietQR.

## 3. Danh sách chức năng (theo Hình 3.5 và UC của báo cáo)

| Nhóm | Đường dẫn | UC / Quy trình | Nội dung |
|---|---|---|---|
| Tổng quan | `/Landlord/Overview` | – | Số liệu chính và danh sách việc cần làm: lịch chờ, hóa đơn quá hạn hoặc chờ xác nhận, sự cố, tin bị trả về. |
| 1. Quản lý phòng | `/Landlord/Rooms` | UC03, UC04 · P02 · BM01 | Đăng tin (lưu nháp hoặc gửi duyệt), sửa tin, ẩn/hiện tin. Quản lý ảnh/video: tải lên, ảnh đại diện, sắp thứ tự, xóa. Quản lý dịch vụ tính riêng và tiện ích. Xóa tin khi chưa phát sinh dữ liệu. |
| 2. Lịch xem phòng | `/Landlord/Viewings` | UC08 · P03 · BM02 | Xác nhận, từ chối (bắt buộc lý do), đánh dấu hoàn tất sau giờ hẹn. Hiện lịch trùng giờ và cảnh báo yêu cầu đã chờ quá 2 giờ. |
| 3. Trò chuyện | `/Landlord/Messages` | UC09 · P04 | Danh sách cuộc trò chuyện, tìm kiếm, lọc chưa đọc. Gửi tin bằng AJAX, tự nhận tin mới mỗi 15 giây. Mở cuộc trò chuyện từ lịch hẹn, hợp đồng hoặc sự cố. |
| 4. Hợp đồng | `/Landlord/Contracts` | UC12, UC14, UC15 · P05 · BM03 | Lập hợp đồng (điền sẵn giá theo phòng bằng AJAX, có mẫu điều khoản). Gửi người thuê, thu hồi để sửa, xác nhận hiệu lực, hủy, kết thúc. Có bản in. |
| 5. Điện nước | `/Landlord/MeterReadings` | UC16, UC17 · P06 · BM04 | Danh sách phòng cần ghi theo kỳ. Ghi chỉ số kèm ảnh công tơ, **tính tiền bằng AJAX**. Xem lịch sử, sửa hoặc xóa khi chưa lập hóa đơn. |
| 6. Hóa đơn | `/Landlord/Invoices` | UC18, UC19, UC21 · P07 · BM05 | Lập hóa đơn từ hợp đồng và phiếu chỉ số (bảng tính AJAX), tạo **VietQR** theo chuẩn NAPAS, xác nhận đã nhận tiền, sửa, hủy. Có bản in. |
| 7. Sửa chữa | `/Landlord/Repairs` | UC24 · P08 · BM06 | Tiếp nhận yêu cầu, cập nhật tiến độ, hoàn tất (bắt buộc ghi nội dung xử lý). |
| Nhận tiền | `/Landlord/PaymentSettings` | UC20 (dữ liệu QR) | Ngân hàng, số tài khoản, tên chủ tài khoản để sinh mã VietQR. |
| Khung giờ nhận khách | `/Landlord/ViewingHours` | P03 (quyết định 15/10) | Lịch lặp theo tuần (giờ đến mỗi thứ), ngày nghỉ hoặc giờ riêng theo ngày, xem trước 7 ngày tới với trạng thái từng giờ (còn trống, đang giữ chỗ, đã có lịch, quá sát giờ). Trang đặt lịch của Tenant đọc đúng dữ liệu này. |
| Hỗ trợ | `/Landlord/Support` | R18, UC10 · P04 | Gửi yêu cầu hỗ trợ, phản ánh hoặc báo cáo vi phạm tới quản trị viên (gắn phòng nếu cần), xem phản hồi, gửi bổ sung. |
| Tài khoản | `/Landlord/Account` | UC27 · R55 | Sửa họ tên, số điện thoại, ảnh đại diện; đổi mật khẩu ngay trong giao diện chủ trọ. Email, 2FA, liên kết Google dùng các trang Identity sẵn có. |
| Đăng ký chủ trọ | `/Landlord/Join` | UC25 · R44–R46 | Tạo tài khoản với vai trò **Landlord** (trang đăng ký chung hiện luôn gán Tenant). Cùng quy tắc với Identity: kiểm tra mật khẩu, email trùng, gửi email xác thực. Trang công khai, không cần đăng nhập. |

Tên menu theo đúng các nhóm của Hình 3.5 (Phòng trọ, Lịch xem phòng, Trò chuyện, Hợp đồng, Điện nước, Hóa đơn, Yêu cầu sửa chữa). Trang **Dashboard cũ** (`/Landlord`, `/Landlord/Dashboard`, link "Dashboard" ở menu `_Layout`) tự chuyển sang trang Tổng quan mới, nên chủ trọ luôn thấy cùng một giao diện (file Dashboard cũ không bị sửa).

Khớp biểu mẫu và yêu cầu của báo cáo:
- Nhãn trạng thái theo R15, R42, BM05 (lịch hẹn dùng "Đã hoàn thành").
- Hiện mã tin, mã lịch hẹn, mã phiếu chỉ số, mã hợp đồng, mã hóa đơn, mã yêu cầu (BM01–BM06).
- BM01 có dòng "Phí Internet" riêng, lưu thành dịch vụ "Internet" để hóa đơn tính được.
- Yêu cầu sửa chữa lưu **lịch sử xử lý** (UC24, R43, Hình 3.5): mỗi lần cập nhật thêm một dòng có ngày giờ, không ghi đè.

Mọi danh sách dùng **phân trang đánh số** (Trước · 1 2 3 … · Sau), 10 dòng mỗi trang, giữ nguyên bộ lọc trên URL. Không dùng cuộn vô hạn.

## 4. Đồng bộ với phía Tenant

| Dữ liệu | Quy ước chung | Cách Landlord tuân theo |
|---|---|---|
| Thông báo | Tenant chỉ mở link dạng `/Tenant/{ViewingAppointment\|Contract\|Invoice\|RepairRequest}/Details/{id}` (`TenantNotificationTarget`). | `LandlordNotifier` tạo link đúng mẫu này cho mọi thông báo: xác nhận/từ chối lịch, gửi hợp đồng, hóa đơn mới, đã nhận tiền, tiến độ sửa chữa. |
| Lịch xem phòng | `ViewingDate` là giờ Việt Nam do Tenant chọn. Tenant hủy/sửa lịch bằng UPDATE có điều kiện trạng thái. | So sánh với giờ Việt Nam hiện tại, không cộng/trừ múi giờ. Xác nhận/từ chối chỉ chạy khi lịch còn `Pending`; nếu người thuê vừa hủy thì trả lỗi 409, không ghi đè. |
| Người dẫn xem | Khóa theo người dẫn + giờ đến (data contract). | Người dẫn là chính chủ trọ: không xác nhận hai lịch cùng giờ đến trên mọi phòng của mình. |
| Sửa chữa | Tenant chỉ sửa/hủy khi `Pending`. | Chủ trọ đổi trạng thái bằng UPDATE kèm điều kiện trạng thái, tránh ghi đè thao tác hủy. |
| Tiện ích phòng | Tenant đọc `AmenitiesJson` dạng mảng chuỗi hoặc object. | Landlord ghi dạng mảng chuỗi `["Wifi","Điều hòa"]`. |
| Ảnh phòng | Tenant dùng ảnh `IsPrimary`, rồi theo `DisplayOrder`. Chỉ nhận URL tương đối hoặc http(s). | Luôn giữ đúng một ảnh đại diện. Ảnh lưu tại `/uploads/landlord/...`. |
| Tin nhắn | Tenant đọc cuộc trò chuyện `TenantLandlord` mà mình là Initiator/Recipient. | Landlord dùng đúng bảng và kiểu này, nên tin gửi đi hiện ngay bên Tenant. |
| Khung giờ xem phòng | Trang `ViewingBooking` của Tenant nhận dữ liệu qua `ITenantViewingScheduleReader`; không có reader thì báo “chưa kết nối”. | `LandlordViewingScheduleReader` cài đặt interface này và được đăng ký trong `LandlordHostingStartup`. Trang chỉ hiện giờ chủ trọ đã mở; khóa theo người dẫn (chính chủ trọ) + giờ đến trên mọi phòng. Lịch đã xác nhận cho trạng thái Booked, lịch chờ còn trong 2 giờ cho Held/HeldByMe. Có lỗi thì trả Error, không làm hỏng trang Tenant. |
| Yêu thích | Tenant lưu tin vào bảng `Favorites`. | Chủ trọ xem số người đã lưu từng tin ở danh sách và chi tiết phòng. |
| Hỗ trợ Admin | DB có sẵn `ConversationType.LandlordAdmin`. | Chủ trọ tạo cuộc trò chuyện `LandlordAdmin` gửi tới tài khoản Admin đang hoạt động được tạo sớm nhất. Tin đầu tiên có dạng `[Loại] Tiêu đề` rồi nội dung. |
| Hiển thị tin | Tenant chỉ thấy tin `Approved` + `Available`. | Sửa nội dung hoặc thêm ảnh cho tin đã duyệt sẽ đưa tin về `Pending`. Ẩn tin đặt `Hidden`. Hợp đồng hiệu lực đặt `Rented`. |

**Phần cần phía Tenant làm tiếp** (không nằm trong phạm vi Landlord, cũng chưa có trong code Tenant hiện tại):

1. **Người thuê xác nhận hợp đồng.** Hiện Tenant chỉ xem hợp đồng. Khi làm, Tenant chỉ cần ghi `TenantConfirmedAt` (giữ `PendingTenant` hoặc chuyển `PendingLandlord`). Chủ trọ bấm “Xác nhận hiệu lực” để hợp đồng thành `Active` và phòng chuyển `Rented` (màn Landlord chấp nhận cả hai trạng thái).
2. **Người thuê báo đã chuyển tiền.** Tenant đặt `TenantConfirmedAt` và đổi `PaymentStatus` sang `AwaitingConfirmation`. Landlord đã hiển thị và xử lý trạng thái này. Chủ trọ cũng xác nhận được khi nhận tiền mặt.
3. **Hiển thị mã QR.** `Invoice.VietQRContent` chứa chuỗi VietQR chuẩn NAPAS. Phía Tenant có thể dùng chung `EZ_Rent.Areas.Landlord.Services.VietQr.Png(content)` để vẽ ảnh QR.
4. **Gửi lịch theo khung giờ.** Trang đặt lịch của Tenant đã hiện giờ thật của chủ trọ, nhưng nút gửi theo khung giờ vẫn thuộc phía Tenant. Khi gửi cần lưu `ViewingDate` đúng giờ đến của khung (giờ Việt Nam) và kiểm tra lại ở server: giờ đó còn trong lịch mở của chủ trọ, cách hiện tại ít nhất 6 giờ, chưa bị lịch khác giữ chỗ.

**Phần cần người phụ trách xác thực làm:** thêm một link "Bạn là chủ trọ? Đăng ký tại đây" trỏ tới `/Landlord/Join` ở trang Đăng ký/Đăng nhập. Không có link này thì chủ trọ phải gõ địa chỉ trực tiếp. Nếu muốn trang Register chung có ô chọn vai trò (R46), có thể bỏ trang `/Landlord/Join`.

**Nhãn trạng thái lịch hẹn:** báo cáo (R15) gọi là "Đã hoàn thành", phía Tenant hiện ghi "Đã hoàn tất". Nên sửa phía Tenant cho trùng.

**Phần cần phía Admin làm (R19):** hộp thư Admin đọc các `Conversation` có `ConversationType = LandlordAdmin` và trả lời bằng `Message` trong cùng cuộc trò chuyện. Trang Hỗ trợ của chủ trọ tự hiện phản hồi và đếm số tin chưa đọc. (Trang `Admin/Support` hiện là CRUD tự sinh trên bảng sửa chữa nên chưa đọc được.)

**Kiểm thử khi phía Tenant chưa có nút xác nhận:** chạy SQL sau để giả lập thao tác của người thuê. Lệnh chỉ đổi đúng các cột mà Tenant sẽ đổi.

```sql
-- Giả lập người thuê đã xác nhận hợp đồng số 5 (đang "Chờ người thuê xác nhận", Status = 2)
UPDATE RentalContracts SET TenantConfirmedAt = SYSUTCDATETIME() WHERE RentalContractId = 5 AND Status = 2;
-- Sau đó chủ trọ mở hợp đồng số 5 và bấm "Xác nhận hiệu lực".

-- Giả lập người thuê báo đã chuyển tiền hóa đơn số 7 (Unpaid = 1 → AwaitingConfirmation = 2)
UPDATE Invoices SET PaymentStatus = 2, TenantConfirmedAt = SYSUTCDATETIME() WHERE InvoiceId = 7 AND PaymentStatus = 1;
```

**Khung giờ không đổi schema dùng chung:** cấu hình giờ nhận khách lưu thành một claim JSON (`ezrent:landlord:viewing-hours`) trong `AspNetUserClaims`, nên không cần nhóm duyệt bảng mới. Các điểm nhóm chưa chốt (thời lượng mỗi lượt xem, khoảng nghỉ giữa hai lượt, cách biểu diễn "hết hạn phản hồi") giữ nguyên như hiện tại: khóa đúng giờ đến, không tự đổi trạng thái lịch quá hạn.

## 5. Chương 4 – Đánh giá theo yêu cầu

### 5.1 Responsive

- Bố cục bằng CSS Grid/Flexbox với ba mốc:
  - **> 1180px:** thanh điều hướng trái và nội dung 2 cột.
  - **≤ 960px:** thanh điều hướng thành thanh trên, menu cuộn ngang.
  - **≤ 760px:** biểu mẫu 1 cột, **bảng chuyển thành thẻ** (mỗi ô có nhãn lấy từ `data-label`).
- Ô bấm cao tối thiểu 40–44px, ô nhập cỡ chữ 16px để iOS không tự phóng to. Bàn phím số dùng `inputmode="numeric"/"decimal"`.
- Trên điện thoại, ghi điện nước có thể **chụp ảnh công tơ trực tiếp** (`capture="environment"`).
- Tin nhắn trên di động: chọn một cuộc trò chuyện thì chỉ hiện nội dung, có nút quay lại danh sách.
- Có `@media print` để in hợp đồng (BM03) và hóa đơn (BM05) sạch, không kèm menu.
- Tôn trọng `prefers-reduced-motion`.

### 5.2 SEO

- Khu Landlord là trang riêng tư sau đăng nhập. Mỗi trang có thẻ `<meta name="robots" content="noindex, nofollow">` để công cụ tìm kiếm không lập chỉ mục trang quản lý. Đây là cách làm SEO đúng: chỉ trang công khai (tìm phòng, chi tiết phòng) mới nên được index.
- Mỗi trang có `<title>` riêng dạng "Tên trang · Chủ trọ · EZ-Rent" và `meta description`.
- HTML5 ngữ nghĩa: `header`, `nav` (có `aria-label`), `main`, `section`, `article` (hợp đồng, hóa đơn), `aside`, `dl` cho cặp nhãn–giá trị, `table` có `caption` và `th scope`. Tiêu đề phân cấp h1 → h2 → h3. Breadcrumb dạng `nav > ol`. Thuộc tính `lang="vi"`.
- Trang **đăng ký chủ trọ** (`/Landlord/Join`) là trang công khai nên được index: có title, meta description riêng, nội dung giới thiệu quy trình cho chủ trọ.
- Chất lượng dữ liệu cho trang công khai: form đăng tin bắt buộc tiêu đề 10–200 ký tự, mô tả ≥ 30 ký tự, địa chỉ đủ quận/thành phố, ảnh thật có ảnh đại diện. Ảnh có `alt` mô tả và `loading="lazy"`. Đây là nội dung mà trang chi tiết phòng phía Tenant dùng để SEO.

### 5.3 AJAX (`wwwroot/js/landlord.js`, dùng `fetch`, không cần thư viện)

1. **Tính tiền điện nước khi nhập chỉ số** (ví dụ tiêu biểu):
   - Ngừng gõ 350ms thì gọi `GET /Landlord/MeterReadings/Preview`.
   - Server tính lượng tiêu thụ × giá trong hợp đồng, trả JSON. Phiếu tính tiền cập nhật ngay, kèm cảnh báo nếu dùng nhiều bất thường (> 2,5 lần kỳ trước).
2. **Bảng tính hóa đơn** (`/Landlord/Invoices/Calculate`): đổi số lượng dịch vụ hay khoản điều chỉnh thì tổng tiền cập nhật ngay.
3. **Lọc, đổi tab, chuyển trang số**: chỉ tải lại vùng danh sách (header `X-EZ-Region`, server trả partial view). URL vẫn cập nhật bằng `history.pushState`, nên nút Back của trình duyệt vẫn đúng.
4. **Thao tác nhanh không tải lại trang**: xác nhận lịch, ẩn/hiện tin, đổi ảnh đại diện, xác nhận đã nhận tiền, tiếp nhận sửa chữa… Server trả JSON `{ ok, message }` rồi trang làm mới đúng vùng liên quan.
5. **Tin nhắn**: gửi bằng AJAX, tự kiểm tra tin mới mỗi 15 giây (chỉ khi tab đang mở).
6. **Điền sẵn giá khi chọn phòng** trong form hợp đồng (`/Landlord/Contracts/RoomDefaults`).

Trang vẫn hoạt động khi tắt JavaScript (link và form thường, Post/Redirect/Get), JS chỉ giúp nhanh hơn. Kết quả AJAX được thông báo cho trình đọc màn hình qua vùng `aria-live`.

### 5.4 An toàn thông tin

| Nguy cơ | Biện pháp trong code |
|---|---|
| Truy cập trái phép | `[Authorize(Roles = "Landlord")]` trên lớp nền. Mỗi request kiểm tra lại `UserStatus`: tài khoản bị khóa/cấm bị chặn dù cookie còn hạn. |
| Xem/sửa dữ liệu người khác (IDOR) | Mọi truy vấn đi qua các tập đã lọc theo chủ trọ đang đăng nhập (`MyPosts`, `MyViewings`, `MyContracts`…). Đổi id trên URL sang dữ liệu người khác chỉ nhận trang 404. |
| Route CRUD tự sinh cũ (không kiểm tra quyền) | `LegacyLandlordRouteConvention` (nạp qua `LandlordHostingStartup`, **không sửa file cũ**) bắt buộc vai trò Landlord và chuyển các route cũ sang trang mới. Menu cũ trong `_Layout` vẫn dùng được nhưng không còn đường ghi dữ liệu không an toàn. |
| CSRF | `[AutoValidateAntiforgeryToken]` cho mọi POST. Request AJAX gửi kèm token có sẵn trong form. |
| Over-posting / mass assignment | Dùng input model riêng (`RoomInput`, `ContractInput`…). Chủ sở hữu, trạng thái duyệt, người duyệt, thời gian và số tiền do server gán, chủ trọ không thể tự duyệt tin. |
| Sai lệch tiền | Server tự tính lại lượng tiêu thụ, tiền điện nước và tổng hóa đơn, không tin số liệu từ trình duyệt. Chỉ số kỳ trước lấy từ DB. Phiếu chỉ số đã lập hóa đơn không sửa/xóa được. |
| Thao tác đồng thời (race condition) | Đổi trạng thái bằng `ExecuteUpdate` có điều kiện trạng thái (ví dụ chỉ xác nhận khi còn `Pending`). Ngoài ra có transaction khi kích hoạt/kết thúc hợp đồng, và index duy nhất của DB chặn 2 hợp đồng hiệu lực cho một phòng hoặc 2 hóa đơn cho một kỳ. |
| Tải file độc hại | Kiểm tra **chữ ký đầu file (magic bytes)**: chỉ nhận JPEG/PNG/WebP/MP4/WebM, ảnh ≤ 5MB, video ≤ 25MB. Tên file do server sinh (GUID). Không nhận SVG/HTML nên file tải lên không chạy được script. Xóa file chỉ trong thư mục `uploads/landlord`. |
| XSS | Razor tự mã hóa HTML. JS chỉ chèn nội dung bằng `textContent`. URL ảnh trong DB được lọc qua `SafeMediaUrl`. Link bản đồ chỉ nhận https hoặc tọa độ. |
| Lộ thông tin cá nhân | Số điện thoại/email người thuê chỉ hiện khi lịch hẹn còn hiệu lực. Danh sách gợi ý người thuê che bớt email. Chỉ nhắn tin được cho người đã có liên hệ với phòng của mình. |
| Dữ liệu sai định dạng | DataAnnotations kiểm tra ở cả client (jquery-validate) và server. Số thập phân được đọc theo InvariantCulture, để máy chủ cài tiếng Việt không đọc nhầm "25.5" thành 255. |
| Lịch sử, truy vết | Không xóa lịch hẹn, hợp đồng, hóa đơn: chỉ đổi trạng thái để hai bên tra cứu. Tin phòng đã phát sinh dữ liệu không xóa được (chỉ ẩn). |
| Ghi log (R68) | Logger `EZ_Rent.Landlord` ghi mọi thao tác thành công (Information) và bị từ chối (Warning) kèm UserId, action, id. Cũng ghi các lần truy cập id không thuộc quyền (dấu hiệu dò IDOR), tài khoản bị khóa vẫn cố truy cập, và lỗi đọc khung giờ. Mức log cấu hình trong `appsettings` như phần còn lại của ứng dụng. |

## 6. Danh sách file mới

```
Areas/Landlord/Infrastructure/  LandlordHostingStartup.cs, LegacyLandlordRouteConvention.cs
Areas/Landlord/Controllers/     LandlordControllerBase.cs, Overview, Rooms, Viewings, ViewingHours, Messages, Contracts,
                                MeterReadings, Invoices, Repairs, PaymentSettings, Support, Account, Join (…Controller.cs)
Areas/Landlord/Services/        LandlordTime, LandlordFormat, LandlordLabels, LandlordHtml, LandlordNotifier,
                                LandlordUploads, LandlordBilling, LandlordBankAccounts, VietQr,
                                LandlordViewingHours, LandlordViewingScheduleReader
Areas/Landlord/ViewModels/      LandlordCommon, RoomModels, ViewingModels, MessageModels, ContractModels,
                                MeterModels, InvoiceModels, RepairModels, OverviewModels, ViewingHoursModels, SupportModels,
                                AccountModels
Areas/Landlord/Views/           Shared/ (layout, layout trang công khai, phân trang, tab), Overview/, Rooms/, Viewings/,
                                ViewingHours/, Messages/, Contracts/, MeterReadings/, Invoices/, Repairs/, PaymentSettings/,
                                Support/, Account/, Join/
wwwroot/css/landlord.css, wwwroot/js/landlord.js
docs/landlord-implementation.md
```

Muốn tắt phần chuyển hướng route cũ (gồm cả Dashboard cũ) và nguồn khung giờ cho Tenant, xóa `Areas/Landlord/Infrastructure/LandlordHostingStartup.cs`. Các trang mới vẫn chạy bình thường.

**Phụ thuộc nhánh Tenant:** `LandlordViewingScheduleReader.cs` và `LandlordHostingStartup.cs` dùng interface `ITenantViewingScheduleReader` trong `Areas/Tenant/Services` (có từ nhánh `feature-tenant-p3`). Vì vậy hãy merge nhánh Tenant trước, hoặc đẩy gói này lên nhánh đã có Tenant. Nếu buộc phải đưa vào nhánh chưa có Tenant: xóa file reader và dòng `TryAddScoped<ITenantViewingScheduleReader…>` trong hosting startup.

**Đã kiểm tra:** build toàn bộ C# và Razor 0 lỗi, 0 cảnh báo. Chạy 25 bài kiểm tra logic không cần database (checksum CRC của VietQR theo giá trị chuẩn 0x29B1, cấu trúc chuỗi VietQR, phân tích khung giờ và ngày ngoại lệ, làm tròn tiền điện nước, đổi giờ Việt Nam, lọc URL ảnh độc hại, lịch sử xử lý sửa chữa): tất cả đạt. Truy vấn EF Core cần chạy thử trên SQL Server thật.

## 7. Ghi chú quy tắc AI (AUR)

Phần code này được viết với hỗ trợ của Claude (Anthropic). Theo AUR.04 và AUR.06, người đưa code vào cần:
- ghi vào **Phụ lục Z**: ngày giờ, công cụ, prompt, tóm tắt kết quả, phần đã áp dụng;
- thêm footnote ở các mục báo cáo có dùng;
- đọc hiểu và giải thích được từng phần trước khi nộp.
