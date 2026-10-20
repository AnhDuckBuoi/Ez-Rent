# Thông tin Tenant và lịch sử bản nháp BM03

## Phạm vi

Dương duyệt form thông tin người thuê, lưu theo hợp đồng, điền mẫu, giữ lịch sử và đối chiếu điều khoản. Gói trên `feature/tenant-my-room`; xem [bàn giao ngày 20/10/2026](tenant-my-room-checkpoint.md). Không chạy database nhóm.

Người thuê nhập họ tên/điện thoại, ngày sinh, thường trú, loại/số giấy tờ, ngày/nơi cấp. Lần đầu lấy sẵn tên và điện thoại từ hồ sơ, không tạo bản ghi khi GET. Có thể lưu khi còn thiếu, nhưng họ tên bắt buộc. Chỉ dữ liệu đã lưu xuất hiện trên giấy; chỉnh form chưa lưu sẽ có lời nhắc và tạm khóa nút in.

## Đọc mã theo luồng

1. `ContractDraftModels.cs`: input chỉ có thông tin cá nhân và token kiểm tra trang cũ; không có TenantId, khoản tiền, thông tin chủ nhà hoặc trạng thái ký.
2. `ContractController.Draft`: GET kiểm tra quyền/query, đọc bản mới nhất hoặc revision cụ thể. `SaveDraft`: POST CSRF/validation, thành công redirect; lỗi 400 và xung đột 409 giữ dữ liệu đã nhập.
3. `TenantContractDraftService`: truy vấn theo cả chủ thể hiện tại của hợp đồng và Tenant tại lúc lưu. Lưu bằng transaction Serializable, so revision và dấu vết dữ liệu nguồn trước khi thêm phiên bản.
4. `ContractTenantDraft`: mỗi lần lưu là một bản chụp mới, giữ cá nhân, địa chỉ, chủ nhà, khoản tiền, thời hạn và điều khoản tại lúc lưu. Unique `(RentalContractId, TenantId, Revision)`, FK hợp đồng Restrict. Không cập nhật bản cũ hoặc hồ sơ tài khoản.
5. `Contract/_TenantDraftForm`, `Contract/Draft`: form và giấy tách riêng, bản lịch sử chỉ đọc; lỗi giữ thông tin trên form còn giấy vẫn là dữ liệu đã lưu. Razor mã hóa nội dung người dùng.
6. `tenant-contract-draft.css`, `tenant-rental.js`: form hai cột từ 640 px, mobile một cột, input 16 px và nút 44 px. JavaScript chỉ hỗ trợ focus lỗi/nhắc chưa lưu; form vẫn lưu qua server khi tắt JS.

Luồng: form POST → controller xác thực/CSRF/input → dịch vụ kiểm tra sở hữu và phiên bản → database lưu bản chụp → redirect GET → Razor dựng giấy theo bản đã lưu.

## Quy tắc

- Bản nháp riêng theo hợp đồng. Sửa hồ sơ hoặc thỏa thuận sau này không làm đổi phiên bản cũ; xem bản cũ không có form chỉnh sửa.
- Hai tab dùng cùng revision: lượt lưu cũ bị từ chối. Nguồn hợp đồng vừa đổi cũng bị chặn; GET mới hiển thị dữ liệu hiện tại để đối chiếu trước khi tạo bản tiếp theo.
- Ngày sinh/ngày cấp không ở tương lai; ngày cấp không trước ngày sinh. Loại giấy tờ đóng trong danh sách; số giấy tờ là thông tin khai báo, không có quy trình xác minh danh tính trong gói.
- Dữ liệu nhận dạng nằm trong body POST, không đưa lên query string hoặc TempData cookie. Các route kế thừa NoStore và giới hạn Tenant.
- Tiền/chủ nhà/địa chỉ/thời hạn/điều khoản luôn đọc từ server. Snapshot điện thoại chủ nhà giữ cùng khả năng lưu chuỗi với nguồn Identity, không tự cắt ở 20 ký tự.
- Lịch sử phân trang số, 10 bản/trang. Người mới được gán vào hợp đồng legacy không đọc được bản chụp cá nhân của người trước.
- Không đổi `RentalContract`, `ContractDocument`, tệp/SHA256, xác nhận đối chiếu hoặc trạng thái ký khi lưu bản nháp.

## Điều khoản và in

BM03 giữ phần trách nhiệm/mốc thanh toán theo mẫu, nước theo m³ đã chốt. `ContractTerms` được giữ riêng trong phần đối chiếu; nếu có nội dung thì bản in thêm một phụ lục **Điều khoản đang lưu**, không âm thầm gộp hoặc thay thế điều khoản mẫu.

Dữ liệu demo in ba trang BM03 và một trang phụ lục. Không có Terms thì không thêm phụ lục. Văn bản dài có thể tăng số trang, không bị cắt để ép số trang. Form/menu không xuất hiện trong bản in. Bản nháp luôn ghi chưa hoàn thiện/chưa ký.

Ngày/nơi lập, thông tin định danh/thường trú phía Landlord và hình thức thanh toán chưa có nguồn nhập trong gói Tenant, được báo rõ và để trống. Không cho Tenant sửa các mục của chủ nhà. Cần ghép phía Landlord để hai bên thống nhất trước khi sử dụng bản hoàn chỉnh.

## Migration và phối hợp

`20261017065816_BoSungBanNhapThongTinNguoiThue` chỉ thêm bảng `ContractTenantDrafts` và khóa/index. Migration được sinh qua factory design-time riêng, chưa áp dụng. Test/preview dùng SQLite EnsureCreated, không chạy entry point chính.

Program.cs thêm một đăng ký `TenantContractDraftService`; tổng cộng bốn dòng đăng ký cho hai gói tài liệu/bản nháp. Nội dung Google có điều kiện từ trước vẫn giữ nguyên. Khi commit sau review chỉ stage phần đăng ký của gói, tránh cuốn thay đổi Google cũ vào commit.

Đức review model/migration và ghép dữ liệu phía Landlord. Chưa kiểm thử migration, deadlock/retry hoặc tải thực tế trên SQL Server nhóm; kết quả SQLite không thay thế kiểm tra trên SQL Server.

## Review

Mở `/review/tenant?page=contract-draft&device=tablet`, đổi Desktop/Tablet/Mobile:

1. Điền dữ liệu minh họa, lưu, tải lại và xem giấy đã điền.
2. Sửa một trường: nút in nhắc lưu. Lưu tiếp rồi mở lịch sử bản trước, kiểm tra dữ liệu cũ chỉ đọc.
3. Nhập ngày sinh tương lai/ngày cấp trước ngày sinh: lỗi tiếng Việt, form giữ dữ liệu và giấy không đổi.
4. Mở hai tab, lưu tab đầu rồi lưu tab còn lại: báo xung đột, không ghi đè.
5. In/lưu PDF để xem các mục đã điền và phụ lục. Tài liệu chủ nhà đã cung cấp trên trang hợp đồng vẫn là tài liệu riêng.

Database/tệp preview tách biệt và reset khi dừng host. Dùng dữ liệu minh họa để review.

## Kiểm chứng

- Nhóm mới **20/20 qua** (`tenant-draft-form.trx`): CSRF/quyền, overposting, lưu thiếu, ngày/enum/phone, tab cũ/nguồn đổi, hồ sơ đổi, XSS, giữ tài liệu đã đối chiếu, lịch sử/phân trang/gán lại hợp đồng.
- Suite đầy đủ **214/214 qua**, cả lượt `tenant-draft-full.trx` và xác nhận cuối `tenant-draft-final.trx`. Build không lỗi, 16 cảnh báo nullable có sẵn ngoài gói; một lần build đầu bị preview cũ khóa DLL, đã dừng đúng host rồi chạy lại thành công.
- Browser **18 kiểm tra qua**, không lỗi JavaScript: lưu/tải lại/giấy đã lưu, tab cũ, lịch sử, lỗi giữ dữ liệu; ba trạng thái × 1440/834/390/320 px và viewer ba thiết bị. Không tràn ngang/trùng ID; trường có nhãn, font ít nhất 16 px.
- PDF bốn trang được kết xuất Poppler và xem đủ các trang. Chromium Windows/viewport mô phỏng; chưa kiểm tra Safari/máy thật/bàn phím ảo.
- Bằng chứng tại artifact chat: `tenant-draft-form-browser.json`, ảnh tương ứng, `bm03-personal-draft-review.pdf`, các PNG in, SQL migration review. Thông tin cá nhân kiểm thử đều là minh họa.
