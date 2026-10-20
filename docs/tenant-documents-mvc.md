# Tài liệu hợp đồng, BM03 và ảnh công tơ trong MVC

## Phạm vi và quyết định

Dương duyệt ghép xem/lưu tài liệu và ảnh vào Tenant trước. Hoãn ký trên web. Mẫu gốc là `docs/templates/bm03-hop-dong-thue-phong.pdf`, sao nguyên tệp BM03 Dương gửi; SHA256 `0B622B2C20E61FB6E880857A6BC874D424694EF52D31CBCBFA6AA1781DF47342`. Dương đã chốt **giữ nước theo m³**, nên bản nháp Razor đổi đúng đơn vị này và có thông báo điều chỉnh; bản gốc giữ nguyên.

Gói trên nhánh `feature/tenant-my-room`; xem [bàn giao ngày 20/10/2026](tenant-my-room-checkpoint.md) cho phạm vi đưa lên GitHub và phần cần review. Không chạy ứng dụng chính, không cập nhật database nhóm.

## Đọc mã theo luồng

1. `Services/RentalPrivateFileStore.cs`: lưu PDF/PNG/JPEG bằng tên ngẫu nhiên dưới `App_Data/RentalFiles`, ngoài `wwwroot`. Tên gốc chỉ là metadata; không dùng để tạo đường dẫn. Kiểm tra dung lượng thực đọc, định dạng sơ bộ, chiều ảnh; lưu SHA256. Kho tệp runtime được loại khỏi Git và publish. Dịch vụ nhận `RentalFileStorageOptions.RootPath` nếu host muốn cấu hình thư mục bền vững khác, bắt buộc ngoài web root.
2. `Services/ImageHeaderValidation.cs`: tách nguyên kiểm tra ảnh có sẵn của avatar để dùng chung, không đổi quy tắc ảnh đại diện. Đây là kiểm tra header, không phải giải mã đầy đủ hay quét mã độc. PDF kiểm tra sơ bộ đầu/cuối tệp, trả tải xuống thay vì nhúng PDF vào origin ứng dụng.
3. `Models/ContractDocument.cs`, `RentalContract.CurrentDocumentVersion`: mỗi lần cung cấp tài liệu tạo bản mới, không ghi đè tệp cũ. Giữ người cung cấp, người thuê tại lúc lưu, thời gian và dấu vết đối chiếu của đúng phiên bản. Không đồng nhất đối chiếu với chữ ký hoặc hiệu lực pháp lý.
4. `Models/InvoiceMeterEvidence.cs`: bộ chỉ số, đơn giá và hai ảnh riêng gắn duy nhất với một hóa đơn, có phòng/kỳ/người thuê tại lúc lưu. Không phụ thuộc URL ảnh hoặc chỉ số nguồn sau khi lưu. Chưa đóng băng toàn bộ trường tiền/trạng thái của `Invoice` trước các thao tác Landlord cũ; phần đó cần ghép đúng luồng phát hành chung.
5. `Services/RentalDocumentService.cs`: chủ nhà sở hữu hợp đồng mới thêm được phiên bản, so phiên bản hiện tại bằng cập nhật có điều kiện trong transaction. Xác nhận Tenant kiểm tra đúng chủ thể/phiên bản và idempotent. Lưu đối chiếu hóa đơn cần cả hai ảnh và chỉ số khớp phòng/kỳ/lượng tiêu thụ. Tệp chưa commit được dọn khi thao tác thất bại; nếu kết quả commit không xác định do mất kết nối thì giữ tệp để phục hồi, tránh xóa nhầm tệp đã được tham chiếu.
6. `Areas/Tenant/Controllers/ContractController.cs`: chi tiết và lịch sử 10 phiên bản/trang; đọc/tải theo quyền; POST CSRF xác nhận đối chiếu. Bản cũ trả 409 với đường mở lại. Các GET không tự tạo tài liệu hoặc xác nhận. `Draft` chỉ đọc dữ liệu sẵn có để dựng BM03, `Template` tải bản gốc. Không có POST Tenant sửa khoản tiền hoặc tải tài liệu phía chủ nhà.
7. `Areas/Tenant/Controllers/InvoiceController.cs`: ưu tiên bộ chỉ số/ảnh đã lưu, kiểm tra quyền và phòng/kỳ ở cả HTML lẫn route ảnh. Nếu chưa có bộ lưu thì hiển thị chỉ số legacy khớp phòng/kỳ với nhãn rõ ràng, không tự giả định ảnh đã được lưu khi phát hành.
8. Các view `Contract/_ContractDocuments`, `Draft`, `Invoice/_InvoiceMeterEvidence` dùng layout/header Tenant chung. Ảnh mở bằng dialog native, Escape/Đóng trả focus. Không có JavaScript vẫn dùng được liên kết ảnh/tải tệp; in BM03 dùng chức năng in trình duyệt.

## Dữ liệu và migration

Migration `20261017063240_LuuTaiLieuHopDongVaAnhCongTo` chỉ thêm `CurrentDocumentVersion` và hai bảng mới, cùng các khóa/index liên quan. Không chuyển đổi hoặc tự gán tài liệu/ảnh cho bản ghi cũ.

EF design-time không có cấu hình Identity UI đã từng sinh thay đổi độ dài khóa đăng nhập ngoài phạm vi. `AppDbContext` nay ghi rõ bốn khóa login/token dài 128 ký tự để giữ đúng schema trong migration gốc; migration cuối không đổi bảng Identity.

Migration được tạo/kiểm tra bằng startup project design-time riêng trong artifact của chat. Project đó có factory SQL Server dùng chuỗi kết nối giả, entry point chủ động chặn chạy, không đọc cấu hình ứng dụng và không mở kết nối. Kiểm tra pending model báo không thay đổi; SQL review được sinh ra tệp, chưa thực thi. Không dùng `dotnet ef database update` vào database nhóm khi review.

## Phần Đức cần ghép phía Landlord

- Đăng ký ba dịch vụ trong `Program.cs` đã có; nếu dùng kho khác thì cấu hình options bằng thư mục riêng, backup tệp cùng database. Nội dung Google có điều kiện từ trước được giữ nguyên; khi commit sau này chỉ stage ba dòng đăng ký dịch vụ của gói, không cuốn thay đổi Google cũ vào commit.
- Controller phía chủ nhà phải kiểm tra role, CSRF, giới hạn multipart và lấy actor ID từ phiên đăng nhập; không nhận ID người cung cấp từ form. Gọi `AppendContractAsync(contractId, authenticatedLandlordId, expectedVersion, file)`. Bắt xung đột để yêu cầu tải lại, không ghi đè.
- Khi phát hành hóa đơn, kiểm tra chỉ số và gọi `CaptureInvoiceAsync(invoiceId, authenticatedLandlordId, electricFile, waterFile)`. Hai ảnh bắt buộc ở dịch vụ này. Các controller Landlord cũ chưa gọi dịch vụ, nên hiện chưa thể khẳng định mọi hóa đơn tạo từ luồng cũ đều có ảnh. Cần ghép trạng thái/giao dịch phát hành chung để không công bố hóa đơn khi lưu ảnh thất bại.
- Không cung cấp thao tác sửa/xóa trực tiếp bộ đối chiếu đã lưu. Nếu sửa hóa đơn đã phát hành, cần quyết định luồng điều chỉnh và lịch sử riêng. Không đổi Tenant/room/period trên bản đã phát hành; lớp đọc riêng tư vẫn ràng buộc người thuê lúc lưu để chống lộ tệp khi dữ liệu legacy bị gán lại.
- Mẫu BM03 đã điền tên/điện thoại/phòng/khoản tiền/thời hạn đang có. Ngày/nơi lập, ngày sinh, CMND/ngày cấp/nơi cấp, thường trú và hình thức thanh toán chưa có trường dữ liệu, để trống. Bản nháp luôn ghi chưa hoàn thiện/chưa ký, không tự đưa vào kho như bản hai bên đã ký. Form bổ sung thông tin và phiên bản mẫu đầy đủ là bước sau; không tự thêm thông tin giả.
- Bản nháp dựa trên điều khoản BM03, chưa tổng hợp tự động `RentalContract.Terms` vào mẫu. Hai nội dung cần được đối chiếu khi chốt hợp đồng trước khi dùng, tránh một bản nháp khác điều khoản đã thống nhất.

## Review

Chạy `dotnet run --project tests/TenantSecurity.Tests --no-build -- --preview`, mở `/review/tenant?page=contract-files` hoặc `?page=contract-draft`. Invoice có thể mở từ hợp đồng, vào hóa đơn đầu tiên kỳ hiện tại.

- Hợp đồng #1 có hai tài liệu BM03 mẫu **chưa ký** để review lịch sử; #3 không có tài liệu để review trạng thái trống.
- Xác nhận đã đối chiếu trên #1 rồi tải lại trang: trạng thái vẫn còn trong database thử; không đổi trạng thái ký, tiền hoặc ngày hợp đồng.
- Mở bản nháp BM03 để xem thông tin tự điền, dùng In/Lưu PDF. Kiểm tra dòng nước theo m³ và các mục để trống.
- Hóa đơn #1 có hai **hình sơ đồ minh họa, không phải ảnh chụp**. Thử mở/tải riêng điện/nước, Escape để đóng. Hóa đơn cũ có nhãn chưa lưu bộ đối chiếu.
- Chọn Desktop/Tablet/Mobile; không có ô Tenant tải ảnh hoặc sửa tiền. Tệp trong preview nằm dưới thư mục tạm riêng; database SQLite/tệp reset khi dừng host. Production lưu vào kho tệp riêng và database thật sau khi nhóm review triển khai.

## Kiểm chứng

- Build thành công. 16 cảnh báo nullable có sẵn ở các phần ngoài gói.
- Toàn bộ suite **193/193 qua** tại `tenant-documents-final.trx`. Sau đó thêm bản nháp BM03 và một ca kiểm tra: nhóm tài liệu cuối **17/17 qua** tại `tenant-documents-bm03-final.trx` (hiện tổng cộng 194 ca được định nghĩa).
- Một lượt suite trước đó có timeout 10 giây ở bài upload avatar cũ khi chạy cả suite; lượt full sau qua 193/193. Một bài BM03 mới ban đầu bắt nhầm đơn vị trong lời giải thích ngoài mẫu; đã giới hạn kiểm tra vào nội dung giấy, 17/17 qua.
- Browser **25 kiểm tra qua**, không lỗi JavaScript: năm trang/trạng thái × bốn độ rộng (1440/834/390/320), xác nhận thật giữ sau reload, tải PDF riêng, hai ảnh phóng to và focus, bản in và viewer ba thiết bị.
- PDF nháp được kết xuất thành đúng ba trang và xem đủ các trang bằng Poppler. Chromium/viewport mô phỏng, chưa phải máy thật/Safari/bàn phím ảo.
- Không chạy live Landlord upload, SQL Server migration hoặc OAuth. Không quét mã độc/full PDF parser. Không có thay đổi Admin/Landlord trong gói.
- Bằng chứng ở artifact của chat: `tenant-documents-mvc-browser.json`, ảnh tương ứng, `bm03-filled-draft-review.pdf`, ba ảnh kết xuất và `tenant-document-migration-review.sql`.
- Ngoài ba dòng đăng ký dịch vụ mới, nội dung `Program.cs` chuẩn hóa CRLF giữ SHA256 cũ `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`.


## Gói thông tin Tenant tiếp theo

Dương đã duyệt form và lịch sử bản nháp. Xem `docs/tenant-contract-draft.md` cho luồng hiện tại. Bản nháp chụp thông tin riêng mỗi lần lưu và có phụ lục điều khoản đối chiếu; thông tin Landlord còn thiếu vẫn chờ phía Landlord. Tổng đăng ký dịch vụ trong Program.cs hiện là bốn dòng của hai gói, không thay nội dung Google cũ.
