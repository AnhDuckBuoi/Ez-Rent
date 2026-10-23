# Chức năng Chủ trọ (Landlord) – EZ-Rent

Phần chủ trọ dựng trên nhánh `feature/tenant-integration`. Nhánh này đã gồm `main` mới (có phần Admin) và toàn bộ phần Tenant.

## 1. Nguyên tắc

- **Chỉ thêm file mới, không sửa file nào của nhóm.** Program.cs, `_Layout`, Models, DbContext, migration, code Tenant/Admin và các file Landlord cũ đều giữ nguyên.
- **Không đổi cơ sở dữ liệu.** Không thêm bảng, cột hay migration. Phần chủ trọ dùng đúng các bảng hiện có, kể cả 3 bảng Tenant vừa thêm (`ContractDocuments`, `ContractTenantDrafts`, `InvoiceMeterEvidence`).
- **Giao diện đồng nhất với main/Admin:**
  - Trang dùng `_Layout` chung của main (Bootstrap 5).
  - Dùng cùng thành phần như Admin: card `border-0 shadow-sm`, bảng `table-hover` với `thead table-light`, badge `bg-*`, nút `btn-sm btn-outline-*`, alert có nút đóng.
  - Phân trang số dùng `PaginatedList` + `PaginationInfo` + partial `_Pagination` của main.
  - JSON AJAX dùng `ApiResult` (`success`/`message`) như Admin.
- **Phân quyền ghi rõ trên từng controller:** `[Area("Landlord")]` + `[Authorize(Roles = "Landlord")]`. Riêng `JoinController` cố ý `[AllowAnonymous]` vì là trang đăng ký.

## 2. Cấu trúc thư mục (theo tầng, chia theo chức năng)

```
Infrastructure/Landlord/              Hạ tầng (cùng cấp Data/)
  LandlordHostingStartup.cs           Đăng ký khi khởi động, không sửa Program.cs
  LegacyLandlordRouteConvention.cs    Khóa và chuyển hướng các controller CRUD tự sinh cũ của Landlord

Services/                             Logic nghiệp vụ dùng chung (cạnh RentalDocumentService, RentalPrivateFileStore của nhóm)
  Common/        VietnamTime, DisplayFormat
  Uploads/       UploadStorage – ảnh/video phòng công khai, ảnh đại diện (dùng ImageHeaderValidation của nhóm)
  Rooms/         RoomMediaWriter – thêm ảnh/video, giữ một ảnh đại diện
  Meters/        MeterPhotoFiles – ảnh công tơ RIÊNG TƯ qua RentalPrivateFileStore (ngoài wwwroot)
  Billing/       BillingCalculator, InvoiceBreakdown, ServiceCharge
  Payments/      VietQr (chuỗi + ảnh QR NAPAS), BankAccountStore (tài khoản nhận tiền)
  Contracts/     LandlordLegalProfileStore (thông tin "Bên cho thuê" BM03), ContractTermsHeader (hình thức thanh toán, nơi lập)
  Notifications/ TenantNotifier – thông báo cho người thuê, đúng mẫu link phía Tenant
  Viewings/      ViewingHours + ViewingHoursStore (khung giờ), ViewingScheduleReader (nguồn khung giờ cho trang đặt lịch Tenant)
  Support/       LandlordSupportService – giống TenantSupportService, loại hội thoại LandlordAdmin

Areas/Landlord/                       Chỉ tầng giao diện
  Controllers/   LandlordAreaController (lớp nền) + 1 controller cho mỗi nhóm chức năng
  Helpers/       LandlordLabels (nhãn trạng thái), LandlordUi (badge, dòng phiếu tính tiền)
  ViewModels/    Mỗi chức năng một thư mục/namespace: Rooms, Viewings, Contracts, Meters, Invoices, Repairs, Support, Account, Payments, Overview, Shared
  Views/         Mỗi controller một thư mục; Shared/_LandlordMenu (thanh chức năng), _StatusTabs, LandlordProblem

wwwroot/css/landlord.css (bổ sung nhỏ trên Bootstrap), wwwroot/js/landlord.js
```

Các file chủ trọ đã có sẵn trên integration (`LandlordControllerBase`, `MessagesController`, `Areas/Landlord/Services`, `ViewModels/LandlordCommon.cs`, `MessageModels.cs`, `Views/Messages`) được giữ nguyên và không trùng tên với file mới. Trang **Trò chuyện** dùng chính `MessagesController` của nhóm (có realtime SignalR). Khi nhóm thống nhất, có thể chuyển `MessagesController` sang dùng `Services/Common` rồi xóa thư mục `Areas/Landlord/Services` cũ.

## 3. Chức năng ↔ báo cáo (Hình 3.5, UC, BM, R)

| Nhóm | Controller (đường dẫn) | Báo cáo | Nội dung |
|---|---|---|---|
| Tổng quan | `Overview` | Hình 3.5 | Thẻ số liệu như Dashboard Admin; việc cần làm: lịch chờ, hóa đơn quá hạn/chờ xác nhận, sự cố, tin bị trả về, phản hồi hỗ trợ. |
| 1. Phòng trọ | `Rooms`, `RoomMedia` | UC03, UC04 · P02 · BM01 · R03–R08 | Đăng tin (nháp/gửi duyệt), sửa, ẩn/hiện, xóa khi chưa phát sinh dữ liệu. Có đủ dòng BM01 gồm phí Internet và dịch vụ tính riêng. Ảnh/video: tải lên, ảnh đại diện, sắp thứ tự. Hiện mã tin, ngày tạo, số người lưu tin. |
| 2. Lịch xem phòng | `Viewings`, `ViewingHours` | UC08 · P03 · BM02 · R13–R16 | Xác nhận, từ chối (bắt buộc lý do), "Đã hoàn thành" sau giờ hẹn. Chặn hai lịch cùng giờ. Khung giờ nhận khách theo tuần và theo ngày, cung cấp cho trang đặt lịch Tenant. |
| 3. Trò chuyện | `Messages` (của nhóm) | UC09 · R17, R20, R21 | Các trang chủ trọ có nút "Nhắn tin cho người thuê" gọi `Messages/Start`. |
| Hỗ trợ | `Support` | UC10 · R18 | Gửi yêu cầu/phản ánh tới quản trị viên, xem phản hồi, gửi bổ sung. |
| 4. Hợp đồng | `Contracts`, `ContractDocuments` | UC12, UC14, UC15 · P05 · BM03 · R22–R26 | Lập/sửa/gửi/thu hồi/hủy/kết thúc. Có hình thức thanh toán, nơi lập, thông tin pháp lý hai bên. **Tài liệu hợp đồng theo phiên bản.** Kích hoạt khi người thuê đã đối chiếu bản mới nhất. Có bản in BM03. |
| 5. Điện, nước | `MeterReadings`, `MeterPhotos` | UC16, UC17 · P06 · BM04 · R27–R32 | Ghi chỉ số kèm 2 ảnh công tơ **lưu riêng tư**. Chỉ số kỳ trước tự lấy. **Tính tiền AJAX khi nhập.** Có lịch sử theo kỳ (phân trang số). |
| 6. Hóa đơn | `Invoices`, `InvoicePayments` | UC18, UC19, UC21 · P07 · BM05 · R33–R39 | Bảng tính AJAX, VietQR NAPAS, **phát hành kèm bộ đối chiếu chỉ số + ảnh cho người thuê**. Xác nhận đã nhận tiền (bước 2), hủy, tạo lại QR, bản in. |
| 7. Yêu cầu sửa chữa | `Repairs` | UC24 · P08 · BM06 · R41–R43 | Tiếp nhận, cập nhật tiến độ, hoàn tất. **Lịch sử xử lý** lưu từng dòng có ngày giờ, người thuê đọc được. |
| Nhận tiền | `PaymentSettings` | UC20 | Ngân hàng (BIN NAPAS), số tài khoản, tên chủ tài khoản. |
| Tài khoản | `Account` | UC27 · R55 | Họ tên, điện thoại, ảnh đại diện, đổi mật khẩu, **thông tin pháp lý BM03**. Email, 2FA, Google dùng trang Identity sẵn có. |
| Đăng ký chủ trọ | `Join` (công khai) | UC25 · R44–R46 | Tạo tài khoản vai trò Landlord theo đúng quy tắc của trang Register (mật khẩu, email trùng, email xác thực). |

**Phân trang số** (← Trước · 1 2 3 … · Sau →, 10 dòng/trang, giữ bộ lọc) có ở: phòng trọ, lịch xem, hợp đồng, điện nước theo kỳ, lịch sử chỉ số, hóa đơn, sửa chữa, hỗ trợ.

## 4. Đồng bộ với Tenant và Admin

| Điểm nối | Phía nhóm | Phía chủ trọ làm |
|---|---|---|
| Tài liệu hợp đồng | `RentalDocumentService.AppendContractAsync`; Tenant xem và bấm "đã đối chiếu" từng phiên bản | `ContractDocumentsController.Upload` gọi đúng hàm, lấy chủ trọ từ phiên đăng nhập, gửi kèm `expectedVersion`. Xung đột → 409, không ghi đè. Tải về luôn dạng đính kèm. Gửi thông báo cho người thuê. |
| Kích hoạt hợp đồng | Tenant hoãn ký trên web; đối chiếu ≠ chữ ký | Chỉ cho "Xác nhận hiệu lực" khi người thuê đã đối chiếu **đúng phiên bản hiện tại**, hoặc có `TenantConfirmedAt` nếu sau này Tenant làm nút ký. |
| Bản nháp BM03 | `ContractTenantDrafts` do người thuê tự khai | Chi tiết hợp đồng hiện bản nháp mới nhất của **đúng người thuê hiện tại** (chỉ đọc, ghi rõ chưa xác minh). |
| Thông tin Bên A (BM03) | Tenant ghi "chờ phía Landlord" | Lưu ở `AspNetUserClaims`. Khóa: `ezrent:landlord:bm03:date-of-birth`, `document-kind`, `document-number`, `document-issued-on`, `document-issued-at`, `permanent-address`. Tenant đọc các khóa này nếu muốn điền vào bản nháp. |
| Hình thức thanh toán, nơi lập | Chưa có cột | Hai dòng đầu của `RentalContract.Terms`, nên cũng hiện trong phụ lục "Điều khoản đang lưu" phía Tenant. |
| Bộ đối chiếu hóa đơn | `RentalDocumentService.CaptureInvoiceAsync` (bắt buộc 2 ảnh) | Hóa đơn kèm điện nước chỉ phát hành khi phiếu chỉ số đủ 2 ảnh. Sau khi lưu hóa đơn, gọi `CaptureInvoiceAsync`; nếu lỗi thì **xóa hóa đơn vừa tạo**, người thuê không bao giờ thấy hóa đơn thiếu bằng chứng. Không sửa/xóa bộ đã lưu; sửa hóa đơn không đổi chỉ số, kỳ, người thuê. |
| Ảnh công tơ | Tenant không đọc `MeterReading.*ImageUrl` | Ảnh lưu bằng `RentalPrivateFileStore` (ngoài wwwroot). Trường URL chỉ giữ tham chiếu `rental-file:{key}`. Chủ trọ xem qua `MeterPhotos` (kiểm tra sở hữu). |
| Khung giờ xem phòng | `ITenantViewingScheduleReader` (trang `ViewingBooking`) | `ViewingScheduleReader`, đăng ký bằng `TryAdd` trong `LandlordHostingStartup`. Khóa theo người dẫn (chủ trọ) + giờ đến; giữ chỗ 2 giờ; lỗi trả `Error` thay vì 500. |
| Thông báo | Tenant chỉ mở link `/Tenant/{ViewingAppointment\|Contract\|Invoice\|RepairRequest}/Details/{id}` | `TenantNotifier` tạo đúng mẫu link này. |
| Hỗ trợ | `TenantSupportService` (TenantAdmin) | `LandlordSupportService` làm y hệt với `LandlordAdmin`: Admin nhận lấy từ DB, transaction Serializable, cùng định dạng tin. |
| Duyệt tin | Admin `Approve` luôn đặt `RoomStatus = Available` | Sửa tin của phòng đang cho thuê **không** gửi duyệt ngay, chỉ đánh dấu `ApprovedAt = null`. Khi kết thúc hợp đồng, tin tự chuyển "Chờ duyệt". Nhờ vậy nút Duyệt của Admin không làm phòng đang thuê hiện lại là "Còn trống". |
| Menu cũ trong `_Layout` | Trỏ tới `Post`, `Contract`, `Invoice`… và `Dashboard` cũ | `LegacyLandlordRouteConvention` bắt buộc vai trò Landlord và chuyển từng link sang trang mới (giữ id). |

### Phần nhóm nên làm thêm (ngoài phạm vi chủ trọ)

- **Tenant:** nút báo đã chuyển tiền (đặt `TenantConfirmedAt`, `PaymentStatus = AwaitingConfirmation`; chủ trọ đã xử lý trạng thái này); nút gửi lịch theo khung giờ; đổi nhãn "Đã hoàn tất" thành "Đã hoàn thành" (R15).
- **Admin:**
  - hộp thư đọc/trả lời hội thoại `TenantAdmin` và `LandlordAdmin` (R19);
  - `Post/Approve` không đặt `RoomStatus = Available` khi phòng đang có hợp đồng hiệu lực.
- **Người phụ trách đăng nhập:** thêm link "Bạn là chủ trọ? Đăng ký" tới `/Landlord/Join` ở trang Đăng ký/Đăng nhập.

## 5. Chương 4 – Đánh giá

**Responsive**
- Lưới Bootstrap (`row`/`col-md-*`/`col-lg-*`), bảng trong `.table-responsive`.
- Thanh chức năng `nav-pills` cuộn ngang trên điện thoại; form một cột trên di động.
- Ô chụp ảnh công tơ mở camera sau (`capture="environment"`).
- Có `@media print` cho hợp đồng BM03 và hóa đơn BM05: ẩn header, footer, menu.

**SEO**
- Trang quản lý riêng tư gửi header `X-Robots-Tag: noindex, nofollow`. Layout chung không có chỗ chèn meta, và header là cách được công cụ tìm kiếm chấp nhận.
- Trang công khai `/Landlord/Join` được index, có title và mô tả riêng.
- Mỗi trang có `<title>` riêng, heading phân cấp, breadcrumb `nav > ol.breadcrumb`, bảng có `thead`, ảnh có `alt`, `loading="lazy"`.
- Dữ liệu chủ trọ nhập (tiêu đề ≥ 10 ký tự, mô tả ≥ 30 ký tự, địa chỉ đầy đủ, ảnh đại diện) là nội dung cho trang chi tiết phòng công khai.

**AJAX** (`wwwroot/js/landlord.js`, fetch)
1. **Tính tiền điện nước khi nhập chỉ số:** `GET /Landlord/MeterReadings/Preview` trả JSON, phiếu cập nhật ngay, cảnh báo nếu dùng > 2,5 lần kỳ trước.
2. **Bảng tính hóa đơn:** `GET /Landlord/Invoices/Calculate`.
3. **Lọc, đổi tab, chuyển trang số** chỉ tải lại vùng danh sách (header `X-EZ-Region`); URL cập nhật bằng `history.pushState`.
4. **Thao tác nhanh** (xác nhận lịch, ẩn/hiện tin, ảnh đại diện, đã nhận tiền, tiếp nhận sửa chữa…): server trả `ApiResult`, hiện alert Bootstrap như Admin.
5. **Điền sẵn giá theo phòng** trong form hợp đồng.

Tắt JavaScript thì trang vẫn chạy bằng form thường (Post/Redirect/Get).

**An toàn thông tin**

| Nguy cơ | Biện pháp |
|---|---|
| Truy cập trái phép | `[Authorize(Roles = "Landlord")]` trên từng controller. Tài khoản bị khóa (`UserStatus`) bị chặn ngay cả khi cookie còn hạn. |
| Xem/sửa dữ liệu người khác (IDOR) | Mọi truy vấn qua `MyPosts`/`MyContracts`/`MyInvoices`… đã lọc theo chủ trọ đang đăng nhập; id lạ trả 404 và được ghi log. |
| Route CRUD tự sinh cũ | `LegacyLandlordRouteConvention` bắt buộc vai trò Landlord và chuyển sang trang mới. |
| CSRF | `[AutoValidateAntiforgeryToken]` cho mọi POST, kể cả fetch. |
| Over-posting | Input model riêng; chủ sở hữu, trạng thái duyệt, người duyệt, số tiền do server gán. |
| Sai lệch tiền | Server tự tính; chỉ số kỳ trước lấy từ DB; phiếu đã lập hóa đơn không sửa được; bộ đối chiếu không sửa được. |
| Thao tác đồng thời | `ExecuteUpdate` có điều kiện trạng thái → 409. Tài liệu hợp đồng so phiên bản trong transaction. Index duy nhất của DB chặn trùng hợp đồng hiệu lực/hóa đơn cùng kỳ. |
| Tải file độc hại | Ảnh kiểm tra header + kích thước bằng `ImageHeaderValidation` của nhóm; video kiểm tra chữ ký MP4/WebM; tên file GUID; không nhận SVG/HTML. Ảnh công tơ, tài liệu hợp đồng nằm ngoài wwwroot, chỉ trả qua controller kiểm tra quyền, có `nosniff`, `no-store`. |
| XSS | Razor mã hóa HTML; JS chèn bằng `textContent`; URL ảnh lọc qua `SafeMediaUrl`. |
| Lộ thông tin cá nhân | Số điện thoại/email người thuê chỉ hiện khi lịch còn hiệu lực; bản nháp CCCD chỉ đúng người thuê hiện tại; email gợi ý che bớt. |
| Ghi log (R68) | Logger `EZ_Rent.Landlord`: thao tác thành công/bị từ chối, id không thuộc quyền, tài khoản bị khóa, lỗi lưu bộ đối chiếu. |

## 6. Chạy và kiểm thử

1. Lấy code nhánh có Tenant integration, chép các file chủ trọ vào, rồi `dotnet build`.
2. Chạy `dotnet ef database update` (chỉ các migration sẵn có của nhóm) và `dotnet run`.
3. Mở `/Landlord/Join` để tạo tài khoản chủ trọ. Admin (`admin@gmail.com`) duyệt tin ở `/Admin/Post`.
4. Người thuê đặt lịch, chủ trọ xác nhận. Chủ trọ tải tài liệu hợp đồng, người thuê bấm "đã đối chiếu" ở trang hợp đồng của họ, chủ trọ xác nhận hiệu lực. Ghi điện nước (đủ 2 ảnh), phát hành hóa đơn, quét VietQR, xác nhận đã nhận tiền.
5. Giả lập người thuê báo đã chuyển tiền (phía Tenant chưa có nút):
   ```sql
   UPDATE Invoices SET PaymentStatus = 2, TenantConfirmedAt = SYSUTCDATETIME() WHERE InvoiceId = 1 AND PaymentStatus = 1;
   ```

**Đã kiểm tra:**
- Build toàn bộ C# và 63 Razor view: 0 lỗi, 0 cảnh báo, biên dịch chung với các file Landlord sẵn có của nhóm (không trùng tên).
- Chạy kiểm thử không cần database, tất cả đạt:
  - checksum VietQR đúng giá trị chuẩn 0x29B1, cấu trúc chuỗi VietQR;
  - khung giờ, tính tiền, giờ Việt Nam, ghép/tách hình thức thanh toán;
  - lịch sử sửa chữa;
  - kiểm tra file bằng `ImageHeaderValidation`;
  - lưu/đọc/xóa ảnh công tơ bằng chính `RentalPrivateFileStore` của nhóm và chuyển ảnh cho `CaptureInvoiceAsync`;
  - lưới bảo vệ và chuyển hướng route cũ.
- Truy vấn EF Core và luồng thật cần chạy thử trên SQL Server.

## 7. Ghi chú quy tắc AI (AUR)

Code có hỗ trợ của Claude (Anthropic). Theo AUR.04/AUR.06, ghi vào Phụ lục Z (ngày giờ, công cụ, prompt, tóm tắt, phần áp dụng), thêm footnote ở mục báo cáo liên quan, và đọc hiểu, giải thích được từng phần trước khi nộp.
