# Bản thử thiết kế Tin nhắn Tenant — 16/10/2026

Dương yêu cầu thử thiết kế một trang riêng để đánh giá bộ skill bổ sung. Trang được chọn là Tin nhắn Tenant, nối với phần UC09/UC10 đã ghi nhận. Đây là bản thử HTML độc lập, chưa ghép vào MVC và chưa phê duyệt nhận diện mới cho các trang khác.

Mở: http://127.0.0.1:8766/tenant-messages-design-trial.html . Dữ liệu trong tab, ảnh minh họa, không gửi tin hoặc yêu cầu thật. Bấm “Về giao diện hiện tại” để đối chiếu với trang Messages của MVC trên 8770.

## Hướng thiết kế và cách dùng skill

Mục tiêu là trao đổi về nơi ở một cách rõ ràng, gần gũi. Đây là màn vận hành, không phải trang quảng bá. Cấu trúc desktop: danh sách trái 292px | khung chat co giãn | thông tin phòng 268px. Nội dung nằm trong một khung chung, dùng đường chia và màu nền nhẹ thay vì nhiều thẻ có bóng giống nhau.

Palette của bản thử: xanh chính #356797, chữ #203246, chữ phụ #607085, nền #f4f6f9, mặt trắng #ffffff, đường chia #e0e6ed. Be Vietnam Pro tự lưu trong repo, hai trọng lượng 400/600, hỗ trợ tiếng Việt. Tiêu đề 30px, tiêu đề khu vực 19px, tin nhắn 13px; nhãn thời gian nhỏ hơn nội dung. Phân biệt gửi/nhận bằng vị trí và màu bong bóng. Ảnh nội thất tạo điểm nhấn ở thông tin phòng, có nhãn ảnh minh họa. Không giả trạng thái online hay tự sinh phản hồi của chủ nhà.

- **Frontend Design:** định hướng bố cục và phân cấp, giữ một thế giới thị giác cho trang thử. Ảnh và nội dung về phòng là nét riêng của EZ Rent; không thêm hero, số liệu thành tích hay quảng cáo.
- **UI/UX Pro Max:** đã thử `messaging collaboration workspace --design-system`, nhưng cấu trúc landing/CTA và màu indigo trong kết quả không phù hợp màn chat. Không áp dụng/persist đầu ra này. Truy vấn hẹp `messaging inbox --domain product` trả **Chat & Messaging App**, lấy gợi ý màu thương hiệu và tương phản bong bóng. Truy vấn `chat message keyboard --domain ux` trả keyboard navigation và skip links. Không đổi dự án sang framework khác.
- **Impeccable:** dùng phần Operate, Craft floor và Polish để kiểm tra mật độ, khoảng cách, trạng thái, độ rõ của hành động. Context launcher đã lỗi ở giai đoạn trước; dùng PRODUCT.md/DESIGN.md và bối cảnh hiện có trực tiếp, không tuyên bố đã chạy detector hoặc quality gate tự động. Không khởi tạo lại workflow chọn concept hoặc thay DESIGN.md toàn dự án cho bản thử này.
- **Web Design Guidelines:** đọc bản hiện tại từ https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md và đối chiếu thủ công ba file HTML/CSS/JS của bản thử. Đã xử lý ngữ nghĩa control, nhãn, focus, lỗi cạnh trường, ghi chú dài, trạng thái URL, giảm chuyển động và bố cục hẹp. Đây là kiểm tra thủ công có phạm vi, không tuyên bố chứng nhận đầy đủ WCAG.

## Các tương tác để Dương thử

1. Tìm tên hoặc phòng ở cột trái; hỗ trợ tìm không dấu, không có kết quả và xóa tìm kiếm. Chọn Chưa đọc, mở từng cuộc trò chuyện để thấy số chưa đọc cập nhật.
2. Soạn tin, chuyển sang chủ nhà khác rồi quay lại: mỗi cuộc trò chuyện giữ bản nháp riêng. Gợi ý nội dung điền vào ô soạn để người dùng xem trước, không tự gửi.
3. Enter gửi mẫu, Shift+Enter xuống dòng; xử lý IME để không gửi giữa lúc gõ tiếng Việt. Nội dung có HTML được hiển thị dạng văn bản. Tin tự thêm có nhãn “Đã gửi trong bản thử”; không có phản hồi tự động.
4. Tình huống “Lỗi gửi một lần” tạo tin có nút Thử lại; thử lại cập nhật chính tin đó, không tạo bản sao. Có tình huống chưa có trò chuyện, đang tải và lỗi tải/thử lại.
5. Mở thông tin phòng, lịch mẫu qua progressive disclosure. Ở chiều rộng 1024px, thông tin phòng chuyển thành panel mở bằng nút thông tin; có đóng/Esc. Lịch hiển thị chỉ minh họa, tin nhắn không thay đổi lịch.
6. “Hỗ trợ & phản ánh” mở form gửi đến **bộ phận hỗ trợ EZ Rent**, phân biệt với chat chủ nhà. Có validation cạnh trường, focus vào trường sai đầu tiên, bỏ lỗi khi đã sửa hợp lệ. Tạo yêu cầu chỉ tạo biên nhận mẫu trong tab, xem lại được ở cột thông tin. Có tạo yêu cầu khác và đóng bằng Esc.

Chọn cuộc trò chuyện, bộ lọc chưa đọc và tình huống demo có trạng thái URL để dùng Back/Forward/deep link. Bản nháp và nội dung hỗ trợ không đưa vào URL hoặc lưu xuống database/localStorage. Có cảnh báo rời trang khi còn nội dung chưa gửi. “Làm lại” là công cụ reset dữ liệu demo, không phải thao tác xóa dữ liệu thật của sản phẩm.

## Xác minh và điểm đã sửa

- Lượt đầu: 19 nhóm kiểm tra trình duyệt đạt; không lỗi JS, không request ghi, không asset tải lỗi; phông và ảnh tải từ file local.
- Ảnh review chỉ ra khung hẹp bị cắt nội dung dù document không tràn. Đã sửa min-width của pane và track grid; kiểm tra xác nhận cả chiều rộng pane, không chỉ scrollWidth của document.
- Sửa nhãn Tháng 10 trong ô lịch bị xuống dòng do selector quá rộng; metadata từ #6b7a8c → #607085, placeholder nền xám đổi màu để đạt tương phản tốt hơn; kiểm tra các cặp màu chính ≥4.5:1.
- Lượt xác nhận: 20 nhóm đạt tại 1440/1024/1920/834/390/320, gồm bản nháp, IME, hai dòng, chuỗi 2.000 ký tự, HTML dạng văn bản, retry, URL/Back, chưa đọc, mốc ngày khi trả lời cuộc trò chuyện cũ, trạng thái trống/tải/lỗi, drawer/Esc và reduced motion. Hỗ trợ mở form mới và phục hồi focus đúng.
- Khi xem lại form sau validation, phát hiện thông báo cũ còn sau khi trường được sửa. Đã sửa và kiểm tra đúng trường hợp này; lỗi tự hết trước khi submit lần nữa. Ảnh form/kết quả được chụp lại từ nội dung bình thường, không dùng chuỗi stress-test để trình Dương.
- Kết quả/ảnh tại thư mục artifact `tenant-messages-trial` của chat: `first-review.json`, `confirmation.json`, `messages-1440.png`, `messages-1024.png`, `messages-390.png`, `support-form-1440.png`, `support-result-1440.png`.

Ở lượt đầu, ưu tiên desktop; bố cục hẹp khi đó chỉ là fallback. Phần cập nhật bên dưới thay thế giới hạn này theo yêu cầu bổ sung của Dương. Không chạy lại bộ C# vì chỉ thêm bản thử riêng. Nguồn ảnh Caleb Oquendo / Pexels và Be Vietnam Pro / SIL OFL đã được ghi ở cuối trang, tái sử dụng tài nguyên đã có.

## Ranh giới triển khai

Không sửa MVC, controller, model, schema, Figma, dữ liệu nhóm, Landlord hoặc Admin. Không gắn trạng thái chat/biên nhận mẫu vào bảng thật. Header có mục Tin nhắn để thử cách tìm đường trên bản riêng, chưa thêm mục này vào menu MVC. Không commit/push; giữ nhánh `feature/tenant-p3`. Chờ review của Dương trước khi quyết định ghép hoặc áp dụng phong cách này.


## Bổ sung tablet và mobile — 16/10/2026

Dương thích hướng thiết kế và yêu cầu bổ sung cả tablet/mobile, cho phép thiết kế lại những phần chưa ổn. Tiếp tục trên prototype riêng, chưa ghép MVC hoặc mở rộng backend.

Trang review: [tenant-messages-responsive-review.html](tenant-messages-responsive-review.html), chạy trên `http://127.0.0.1:8766/tenant-messages-responsive-review.html`. Ba nút Desktop / Tablet / Mobile đổi kích thước thật của iframe, không tải lại nội dung; bản nháp được giữ. Khung có thể thu nhỏ cho vừa màn hình review, không dùng tỷ lệ thu nhỏ để đánh giá kích thước chữ thực tế.

- Desktop trên 1240px: giữ ba cột đã được thích, thêm lối vào hỗ trợ trong thông tin phòng.
- Tablet từ 768 đến 1240px: danh sách và chat hai cột, vừa chiều cao viewport; thông tin phòng trong dialog bên phải. Hàng tóm tắt phòng có ảnh nhỏ giúp đối chiếu ngữ cảnh, chữ tin nhắn 14px, ô nhập 16px.
- Mobile dưới 768px: danh sách và chat là hai màn riêng. Điều hướng dưới chỉ xuất hiện ở hộp thư; chat có nút quay lại, thông tin phòng và ô soạn ở dưới. Giữ tìm kiếm, bộ lọc, vị trí danh sách và bản nháp khi quay lại. URL `?chat=ha` mở chat; `?chat=ha&view=inbox` mở hộp thư với lựa chọn gần nhất. Deep link đang mở được đánh dấu đã đọc trong bộ dữ liệu mẫu.
- Điện thoại ngang: khi chiều rộng dưới 1024px, cao tối đa 600px và đầu vào chính là cảm ứng, dùng bố cục một màn như mobile. Không ép hai cột trên điện thoại ngang.
- Composer: điện thoại dùng Enter xuống dòng và nút mũi tên gửi; desktop/tablet giữ Enter gửi, Shift+Enter xuống dòng và bảo vệ IME. Theo dõi visualViewport ở mobile, giảm phần phụ khi viewport thấp; không tự bật bàn phím lúc mở chat. Nội dung tin 15px, ô nhập 16px, nút gửi 44px. Có safe-area và không khóa zoom.
- Thông tin phòng: dùng cùng một DOM pane, di chuyển vào dialog native trên tablet/mobile. Đóng bằng nút hoặc Escape, nền không nhận focus khi modal mở; trả focus cho nút mở khi phù hợp. Mobile thông tin phòng và form hỗ trợ chiếm màn hình, vẫn cuộn được và không mất nội dung.
- Form hỗ trợ: một cột trên mobile, chữ nhập 16px, thao tác tối thiểu 48px, lỗi cạnh trường; giữ form khi đóng/mở lại, xem biên nhận trong tab. Modal hỗ trợ có thể mở từ thông tin phòng, đóng quay lại thông tin phòng.

Đã phối hợp Frontend Design giữ hướng thị giác, Impeccable Adapt cho thay đổi theo ngữ cảnh, UI/UX Pro Max truy vấn hẹp `mobile messaging keyboard --domain ux`, và đối chiếu lại bản Web Interface Guidelines mới. Không áp dụng đầu ra landing-page hoặc thay stack.

### Kết quả kiểm tra responsive

17 nhóm kiểm tra tương tác cuối đạt trên Chromium có mô phỏng cảm ứng; thêm kiểm tra trang review với ba kích thước và giữ bản nháp. Các chiều rộng 320, 390, 768, 834, 1024, 1240, 1440, 1920; kiểm tra riêng điện thoại ngang 844×390 và viewport thu nhỏ 390×430. Không tràn ngang hoặc pane vượt khung; kiểm tra composer, local font, bản nháp riêng, URL/Back, Enter/IME, HTML dạng văn bản, retry không nhân bản, các trạng thái trống/tải/lỗi, hỗ trợ/validation/biên nhận, modal/đóng/focus, reduced motion. Không lỗi JavaScript, lỗi asset hoặc request ghi trong luồng kiểm tra.

Lượt đầu phát hiện tablet cần cuộn trang mới thấy hết composer và điện thoại ngang bị nhận như tablet; đã sửa và xác nhận. Phép kiểm tra focus ban đầu quá chặt vì dialog native cho phép Tab đến giao diện trình duyệt; đã đổi kiểm tra đúng việc nền không thể nhận focus, các phần tử trong trang vẫn thuộc dialog, Escape đóng và phục hồi focus. Không thêm focus trap tự viết chồng lên dialog native.

Bằng chứng tại artifact `tenant-messages-responsive`: `functional.json`, `review-page.json`, ảnh desktop/tablet/phone, phòng, hỗ trợ, landscape và viewport thu nhỏ. Đã kiểm tra giả lập trên Chromium; chưa xác minh bàn phím ảo và Safari trên thiết bị vật lý. Không chạy lại C# vì gói này chỉ thay HTML/CSS/JS và tài liệu prototype. Không commit/push hoặc ghi dữ liệu nhóm.


Kiểm tra bổ sung khi bàn phím mở ở ô tìm kiếm: ẩn phần điều hướng phụ để kết quả còn chỗ hiển thị. Phát hiện reflow lúc blur có thể làm hụt lần chạm vào kết quả; đã giữ bố cục tìm kiếm ổn định cho đến khi viewport phục hồi, mở chat hoặc chọn nút Thoát tìm kiếm. Đã xác nhận kết quả vẫn nhìn thấy/chọn được ở 390×430, giữ truy vấn khi quay lại và nút thoát phục hồi điều hướng. Bằng chứng `search-viewport.json`, `phone-search-compact.png`. Safe-area cũng áp dụng cho hai cạnh và modal trên thiết bị có vùng khuyết.
