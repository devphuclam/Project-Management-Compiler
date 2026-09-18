# IDEA Engineering — Technical Pilot Kanban và CARIO

## Control envelope

| Thông tin kiểm soát | Nội dung |
| --- | --- |
| Phiên bản / trạng thái | 0.3 / Draft |
| Baseline | IE-PLAN-DEC2026-002 |
| Tổng card thực hiện | 53 |
| Card quyết định và mốc | 7 |

## 1. Cách dùng và chính sách

### 1.1 Trạng thái

Mọi card mới bắt đầu ở trạng thái chưa xác định cho đến khi có bằng chứng thực thi.

### 1.2 Giới hạn WIP

| Chính sách | Giá trị |
| --- | --- |
| WIP implementation | 1 |
| Resource baseline | One coder, weekday-only |

### 1.3 CARIO

Các bảng CARIO dưới đây là ma trận trách nhiệm; một ô có nhiều mã tạo ra nhiều
assignment cho cùng một card.

## 2. Danh sách 53 card thực hiện

### 2.1 PH0 — Sẵn sàng triển khai

| Card | Tên task nhập Kanban | Giờ | Thời gian | Cần trước | Nội dung ghi trên card |
| --- | --- | ---: | --- | --- | --- |
| P01 | Chốt mục tiêu phân tích | 8 | 18/09 | — | Evidence: mục tiêu |
| P02 | Chốt vai trò và thuật ngữ | 8 | 21/09 | — | Evidence: vai trò |
| P03 | Chốt phạm vi kỹ thuật | 8 | 22/09 | P01, P02 | Evidence: phạm vi |
| P04 | Chuẩn bị môi trường minh họa | 12 | 23/09 AM–24/09 AM | P03 | Evidence: môi trường |
| P05 | Xác định dữ liệu đầu vào | 8 | 24/09 | P03 | Evidence: dữ liệu |
| P06 | Xác định tiêu chí kiểm chứng | 12 | 25/09 | P04 | Evidence: tiêu chí |
| P07 | Gói bằng chứng PH0 | 8 | 29/09 | P05, P06 | Evidence: PH0 |

#### CARIO matrix PH0

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| P01 | PM | — | — | — | STAKE | OWNER |
| P02 | PM | — | — | PDA, PROC | STAKE | OWNER |
| P03 | PM | DEV | — | PDA | STAKE | OWNER |
| P04 | PM | DEV | QA | OPS | STAKE | OWNER |
| P05 | PM | — | — | DATA | STAKE | OWNER |
| P06 | PM | DEV | QA | DATA | STAKE | OWNER |
| P07 | PM | — | QA | PDA | STAKE | OWNER |

### 2.2 PH1 — Nền tảng dữ liệu

| Card | Tên task nhập Kanban | Giờ | Thời gian | Cần trước | Nội dung ghi trên card |
| --- | --- | ---: | --- | --- | --- |
| F01-A | Mô hình nguồn — phần A | 8 | 05/10 | P07 | Evidence: model A |
| F01-B | Mô hình nguồn — phần B | 8 | 06/10 | F01-A | Evidence: model B |
| F02 | Chuẩn hóa định danh | 12 | 07/10 | F01-B | Evidence: identifiers |
| F03-A | Bộ đọc tài liệu — phần A | 8 | 08/10 | F02 | Evidence: parser A |
| F03-B | Bộ đọc tài liệu — phần B | 8 | 09/10 | F03-A | Evidence: parser B |
| F04 | Kiểm tra dữ liệu | 12 | 12/10 | F03-B | Evidence: diagnostics |
| F05-A | Gói bằng chứng PH1 — phần A | 8 | 13/10 | F04 | Evidence: proof A |
| F05-B | Gói bằng chứng PH1 — phần B | 8 | 14/10 | F05-A | Evidence: proof B |

#### CARIO matrix PH1

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| F01-A | PM | DEV | — | PDA | STAKE | OWNER |
| F01-B | PM | DEV | — | PDA | STAKE | OWNER |
| F02 | PM | DEV | QA | DATA | STAKE | OWNER |
| F03-A | PM | DEV | — | PROC | STAKE | OWNER |
| F03-B | PM | DEV | QA | PROC | STAKE | OWNER |
| F04 | PM | DEV | QA | PDA | STAKE | OWNER |
| F05-A | PM | — | QA | PDA | STAKE | OWNER |
| F05-B | PM | — | QA | PDA | STAKE | OWNER |

### 2.3 PH2 — Chuẩn hóa canonical

| Card | Tên task nhập Kanban | Giờ | Thời gian | Cần trước | Nội dung ghi trên card |
| --- | --- | ---: | --- | --- | --- |
| C01-A | Chuẩn hóa baseline — phần A | 8 | 19/10 | F05-B | Evidence: baseline A |
| C01-B | Chuẩn hóa baseline — phần B | 8 | 20/10 | C01-A | Evidence: baseline B |
| C02-A | Chuẩn hóa phase — phần A | 10 | 21/10 | C01-B | Evidence: phase A |
| C02-B | Chuẩn hóa phase — phần B | 10 | 22/10 | C02-A | Evidence: phase B |
| C03-A | Chuẩn hóa work package — phần A | 10 | 23/10 | C02-B | Evidence: work package A |
| C03-B | Chuẩn hóa work package — phần B | 10 | 26/10 | C03-A | Evidence: work package B |
| C04-A | Chuẩn hóa delivery card — phần A | 10 | 27/10 | C03-B | Evidence: card A |
| C04-B | Chuẩn hóa delivery card — phần B | 10 | 28/10 | C04-A | Evidence: card B |
| C05-A | Gói bằng chứng PH2 — phần A | 10 | 29/10 | C04-B | Evidence: proof A |
| C05-B | Gói bằng chứng PH2 — phần B | 10 | 30/10 | C05-A | Evidence: proof B |

#### CARIO matrix PH2

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| C01-A | PM | DEV | — | PDA | STAKE | OWNER |
| C01-B | PM | DEV | — | PDA | STAKE | OWNER |
| C02-A | PM | DEV | QA | PDA | STAKE | OWNER |
| C02-B | PM | DEV | QA | PDA | STAKE | OWNER |
| C03-A | PM | DEV | — | PROC | STAKE | OWNER |
| C03-B | PM | DEV | QA | PROC | STAKE | OWNER |
| C04-A | PM | DEV | — | PDA | STAKE | OWNER |
| C04-B | PM | DEV | QA | PDA | STAKE | OWNER |
| C05-A | PM | — | QA | PDA | STAKE | OWNER |
| C05-B | PM | — | QA | PDA | STAKE | OWNER |

### 2.4 PH3 — Thực hiện pilot

| Card | Tên task nhập Kanban | Giờ | Thời gian | Cần trước | Nội dung ghi trên card |
| --- | --- | ---: | --- | --- | --- |
| W01-A | Thiết lập luồng phân tích — phần A | 10 | 09/11 | C05-B | Evidence: flow A |
| W01-B | Thiết lập luồng phân tích — phần B | 10 | 10/11 | W01-A | Evidence: flow B |
| W02-A | Ghi nhận tiến độ — phần A | 10 | 11/11 | W01-B | Evidence: progress A |
| W02-B | Ghi nhận tiến độ — phần B | 10 | 12/11 | W02-A | Evidence: progress B |
| W03-A | Theo dõi phụ thuộc — phần A | 12 | 13/11 | W02-B | Evidence: dependency A |
| W03-B | Theo dõi phụ thuộc — phần B | 12 | 16/11 | W03-A | Evidence: dependency B |
| W04-A | Tính lịch kế hoạch — phần A | 12 | 17/11 | W03-B | Evidence: schedule A |
| W04-B | Tính lịch kế hoạch — phần B | 12 | 18/11 | W04-A | Evidence: schedule B |
| W05-A | Kiểm tra cảnh báo — phần A | 12 | 19/11 | W04-B | Evidence: alert A |
| W05-B | Kiểm tra cảnh báo — phần B | 12 | 20/11 | W05-A | Evidence: alert B |
| W06-A | Kiểm tra đầu ra — phần A | 10 | 23/11 | W05-B | Evidence: output A |
| W06-B | Kiểm tra đầu ra — phần B | 10 | 24/11 | W06-A | Evidence: output B |
| W07 | Gói bằng chứng PH3 | 12 | 25/11 | W06-B | Evidence: PH3 |

#### CARIO matrix PH3

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| W01-A | PM | DEV | — | PDA | STAKE | OWNER |
| W01-B | PM | DEV | — | PDA | STAKE | OWNER |
| W02-A | PM | DEV | QA | PROC | STAKE | OWNER |
| W02-B | PM | DEV | QA | PROC | STAKE | OWNER |
| W03-A | PM | DEV | — | PDA | STAKE | OWNER |
| W03-B | PM | DEV | QA | PDA | STAKE | OWNER |
| W04-A | PM | DEV | — | DATA | STAKE | OWNER |
| W04-B | PM | DEV | QA | DATA | STAKE | OWNER |
| W05-A | PM | DEV | — | PDA | STAKE | OWNER |
| W05-B | PM | DEV | QA | PDA | STAKE | OWNER |
| W06-A | PM | — | QA | PDA | STAKE | OWNER |
| W06-B | PM | — | QA | PDA | STAKE | OWNER |
| W07 | PM | — | QA | PDA | STAKE | OWNER |

### 2.5 PH4 — Học và hiệu chỉnh

| Card | Tên task nhập Kanban | Giờ | Thời gian | Cần trước | Nội dung ghi trên card |
| --- | --- | ---: | --- | --- | --- |
| L01-A | Tổng hợp quan sát — phần A | 8 | 07/12 | W07 | Evidence: observations A |
| L01-B | Tổng hợp quan sát — phần B | 8 | 08/12 | L01-A | Evidence: observations B |
| L02-A | Đối chiếu sai lệch — phần A | 8 | 09/12 | L01-B | Evidence: variance A |
| L02-B | Đối chiếu sai lệch — phần B | 8 | 10/12 | L02-A | Evidence: variance B |
| L03-A | Hiệu chỉnh quy tắc — phần A | 10 | 11/12 | L02-B | Evidence: rules A |
| L03-B | Hiệu chỉnh quy tắc — phần B | 10 | 14/12 | L03-A | Evidence: rules B |
| L04 | Kiểm tra hồi quy | 12 | 15/12 | L03-B | Evidence: regression |
| L05 | Gói bằng chứng PH4 | 8 | 16/12 | L04 | Evidence: PH4 |

#### CARIO matrix PH4

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| L01-A | PM | DEV | — | PDA | STAKE | OWNER |
| L01-B | PM | DEV | — | PDA | STAKE | OWNER |
| L02-A | PM | DEV | QA | PDA | STAKE | OWNER |
| L02-B | PM | DEV | QA | PDA | STAKE | OWNER |
| L03-A | PM | DEV | — | PROC | STAKE | OWNER |
| L03-B | PM | DEV | QA | PROC | STAKE | OWNER |
| L04 | PM | — | QA | PDA | STAKE | OWNER |
| L05 | PM | — | QA | PDA | STAKE | OWNER |

### 2.6 PH5 — Đóng gói và bàn giao

| Card | Tên task nhập Kanban | Giờ | Thời gian | Cần trước | Nội dung ghi trên card |
| --- | --- | ---: | --- | --- | --- |
| Q01 | Chuẩn bị báo cáo | 12 | 21/12 | L05 | Evidence: report |
| Q02 | Chuẩn bị hướng dẫn | 8 | 22/12 | L05 | Evidence: guide |
| Q03 | Kiểm tra an toàn nguồn | 8 | 23/12 | Q01 | Evidence: safety |
| Q04 | Kiểm tra tái lập | 12 | 24/12 | Q02, Q03 | Evidence: repeatability |
| Q05 | Chuẩn bị bàn giao | 8 | 28/12 | Q04 | Evidence: handoff |
| Q06-A | Gói bằng chứng kết thúc — phần A | 8 | 29/12 | Q05 | Evidence: closeout A |
| Q06-B | Gói bằng chứng kết thúc — phần B | 8 | 30/12 | Q06-A | Evidence: closeout B |

#### CARIO matrix PH5

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| Q01 | PM | DEV | — | PDA | STAKE | OWNER |
| Q02 | PM | DEV | — | PROC | STAKE | OWNER |
| Q03 | PM | — | QA | PDA | STAKE | OWNER |
| Q04 | PM | DEV | QA | PDA | STAKE | OWNER |
| Q05 | PM | — | QA | PDA | STAKE | OWNER |
| Q06-A | PM | — | QA | PDA | STAKE | OWNER |
| Q06-B | PM | — | QA | PDA | STAKE | OWNER |

## 3. Bảy card quyết định và mốc

| Card | Tên task nhập Kanban | Hạn | Cần trước | Nội dung ghi trên card |
| --- | --- | --- | --- | --- |
| G-D0 | Quyết định hướng roadmap | 2026-09-25 | P03 | Evidence: decision |
| G-MS0 | Mốc PH0 | 2026-10-02 | P07, G-D0 | Evidence: gate PH0 |
| G-MS1 | Mốc PH1 | 2026-10-16 | F05-B | Evidence: gate PH1 |
| G-MS2 | Mốc PH2 | 2026-11-06 | C05-B | Evidence: gate PH2 |
| G-MS3 | Mốc PH3 | 2026-12-04 | W07 | Evidence: gate PH3 |
| G-MS4 | Mốc PH4 | 2026-12-18 | L05 | Evidence: gate PH4 |
| G-MS5 | Mốc kết thúc | 2026-12-31 | Q06-B | Evidence: gate PH5 |

#### CARIO matrix gates

| Card | A | R+ | R | C | I | O |
| --- | --- | --- | --- | --- | --- | --- |
| G-D0 | PM | — | — | PDA | STAKE | OWNER |
| G-MS0 | PM | — | QA | PDA | STAKE | OWNER |
| G-MS1 | PM | — | QA | PDA | STAKE | OWNER |
| G-MS2 | PM | — | QA | PDA | STAKE | OWNER |
| G-MS3 | PM | — | QA | PDA | STAKE | OWNER |
| G-MS4 | PM | — | QA | PDA | STAKE | OWNER |
| G-MS5 | PM | — | QA | PDA | STAKE | OWNER |
