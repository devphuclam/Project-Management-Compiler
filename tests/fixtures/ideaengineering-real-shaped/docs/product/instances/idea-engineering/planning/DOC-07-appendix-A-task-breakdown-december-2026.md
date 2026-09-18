# IDEA Engineering — Phụ lục A: Kế hoạch Technical Pilot đến 31/12/2026

## 1. Thông tin kiểm soát

| Thông tin kiểm soát | Nội dung |
| --- | --- |
| Baseline | IE-PLAN-DEC2026-002 |
| Phiên bản | 0.1 |
| Phạm vi | 35 work package; 512 giờ; 88 giờ dự phòng |
| Tài liệu điều khiển | DOC-07 |

## 4. Work package theo thứ tự thực hiện

### 4.1 PH0 — Sẵn sàng triển khai, 64 giờ

| Mã | Công việc | Giờ | Cần trước | Đầu ra và cách biết đã xong |
| --- | --- | ---: | --- | --- |
| P01 | Chốt mục tiêu phân tích | 8 | — | Mục tiêu được ghi nhận |
| P02 | Chốt vai trò và thuật ngữ | 8 | — | Vai trò được xác nhận |
| P03 | Chốt phạm vi kỹ thuật | 8 | P01, P02 | Phạm vi được duyệt |
| P04 | Chuẩn bị môi trường minh họa | 12 | P03 | Môi trường chạy được |
| P05 | Xác định dữ liệu đầu vào | 8 | P03 | Danh mục dữ liệu hoàn tất |
| P06 | Xác định tiêu chí kiểm chứng | 12 | P04 | Tiêu chí có thể kiểm tra |
| P07 | Gói bằng chứng PH0 | 8 | P05, P06 | Bằng chứng PH0 được lưu |

### 4.2 PH1 — Nền tảng dữ liệu, 72 giờ

| Mã | Công việc | Giờ | Cần trước | Đầu ra và cách biết đã xong |
| --- | --- | ---: | --- | --- |
| F01 | Mô hình nguồn | 16 | P07 | Mô hình nguồn được mô tả |
| F02 | Chuẩn hóa định danh | 12 | P07 | Định danh ổn định |
| F03 | Bộ đọc tài liệu | 16 | F01 | Bộ đọc có kiểm tra lỗi |
| F04 | Kiểm tra dữ liệu | 12 | F02, F03 | Cảnh báo được phân loại |
| F05 | Gói bằng chứng PH1 | 16 | F04 | Bằng chứng PH1 được lưu |

### 4.3 PH2 — Chuẩn hóa canonical, 96 giờ

| Mã | Công việc | Giờ | Cần trước | Đầu ra và cách biết đã xong |
| --- | --- | ---: | --- | --- |
| C01 | Chuẩn hóa baseline | 16 | F05 | Baseline canonical hợp lệ |
| C02 | Chuẩn hóa phase | 20 | F05 | Phase canonical hợp lệ |
| C03 | Chuẩn hóa work package | 20 | C01, C02 | 35 work package được giữ |
| C04 | Chuẩn hóa delivery card | 20 | C03 | Card liên kết được |
| C05 | Gói bằng chứng PH2 | 20 | C04 | Bằng chứng PH2 được lưu |

### 4.4 PH3 — Thực hiện pilot, 144 giờ

| Mã | Công việc | Giờ | Cần trước | Đầu ra và cách biết đã xong |
| --- | --- | ---: | --- | --- |
| W01 | Thiết lập luồng phân tích | 20 | C05 | Luồng phân tích chạy được |
| W02 | Ghi nhận tiến độ | 20 | W01 | Tiến độ có nguồn |
| W03 | Theo dõi phụ thuộc | 24 | W02 | Phụ thuộc được kiểm tra |
| W04 | Tính lịch kế hoạch | 24 | W03 | Lịch được tính định lượng |
| W05 | Kiểm tra cảnh báo | 24 | W04 | Cảnh báo có điều kiện |
| W06 | Kiểm tra đầu ra | 20 | W05 | Đầu ra tái lập được |
| W07 | Gói bằng chứng PH3 | 12 | W06 | Bằng chứng PH3 được lưu |

### 4.5 PH4 — Học và hiệu chỉnh, 72 giờ

| Mã | Công việc | Giờ | Cần trước | Đầu ra và cách biết đã xong |
| --- | --- | ---: | --- | --- |
| L01 | Tổng hợp quan sát | 16 | W07 | Quan sát được tổng hợp |
| L02 | Đối chiếu sai lệch | 16 | L01 | Sai lệch được giải thích |
| L03 | Hiệu chỉnh quy tắc | 20 | L02 | Quy tắc có kiểm thử |
| L04 | Kiểm tra hồi quy | 12 | L03 | Hồi quy không tăng |
| L05 | Gói bằng chứng PH4 | 8 | L04 | Bằng chứng PH4 được lưu |

### 4.6 PH5 — Đóng gói và bàn giao, 64 giờ

| Mã | Công việc | Giờ | Cần trước | Đầu ra và cách biết đã xong |
| --- | --- | ---: | --- | --- |
| Q01 | Chuẩn bị báo cáo | 12 | L05 | Báo cáo có provenance |
| Q02 | Chuẩn bị hướng dẫn | 8 | L05 | Hướng dẫn có thể làm theo |
| Q03 | Kiểm tra an toàn nguồn | 8 | Q01 | Không lộ nội dung nguồn |
| Q04 | Kiểm tra tái lập | 12 | Q02, Q03 | Kết quả tái lập được |
| Q05 | Chuẩn bị bàn giao | 8 | Q04 | Gói bàn giao hoàn tất |
| Q06 | Gói bằng chứng kết thúc | 16 | Q05 | Bằng chứng kết thúc được duyệt |
