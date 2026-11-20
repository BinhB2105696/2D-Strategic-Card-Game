# Bảng đánh giá kỹ thuật LearnedBot

Cập nhật 2026-11-20. Dữ liệu là 192 ván đã xác minh replay, 64 ván mỗi đối thủ Easy, Normal và Hard, với 16 seed, hai bộ bài và đổi bên. Learner trong các ván này lấy mẫu nước đi từ Reward v1. Bảng dưới tập trung vào Hard. CSV có 161 nhóm điều kiện tổng hợp và theo từng đối thủ.

**Điểm kỹ thuật trên 100** bằng số lần đạt kết quả cục bộ chia số lần quan sát được kết quả đó, nhân 100. Ví dụ, điểm của nước cấp năng lượng đo việc quái có thực sự tấn công trong lượt. Điểm triệu hồi theo quân bài đo việc quân đó còn trên bàn ở đầu Main kế tiếp. CSV ghi riêng số ván thắng từ các ván thuộc mỗi nhóm. Một ván có thể góp vào nhiều nhóm. Các tỷ lệ này mô tả replay, vì thế nên kiểm tra đối đầu trực tiếp để kết luận về sức chơi.

## Năng lượng và quyết định vào Battle

| Điều kiện trước Hard | Đạt / quan sát | Điểm /100 |
|---|---:|---:|
| Cấp đủ phần năng lượng còn thiếu và quái tấn công trong lượt | 168/189 | 88,89 |
| Chỉ cấp một phần năng lượng còn thiếu và quái tấn công trong lượt | 4/27 | 14,81 |
| Vào Battle với quái ở thế Công, đủ năng lượng và còn lượt đánh, sau đó thực sự tấn công | 257/262 | 98,09 |
| Vào Battle không có quái đạt ba điều kiện trên, sau đó thực sự tấn công | 0/94 | 0,00 |
| Vào Battle chưa sẵn sàng dù trước đó có nước cấp đủ hợp lệ, sau đó thực sự tấn công | 0/9 | 0,00 |

Cho model thấy phần năng lượng thiếu của từng quái, nước cấp có lấp được chỗ thiếu và số quái đạt điều kiện về thế, năng lượng, lượt đánh sau lựa chọn. Danh sách nước hợp lệ của game xử lý những điều kiện còn lại.

## Xây bàn, chọn quái và tư thế

| Điều kiện trước Hard | Còn quái hoặc đúng quân được theo dõi ở Main sau | Điểm /100 |
|---|---:|---:|
| 0 / 1 / 2 / 3 lần triệu hồi trong lượt | 59/127 · 78/111 · 69/83 · 44/50 | 46,46 · 70,27 · 83,13 · 88,00 |
| Rời Main với 1 / 2 / 3 quái trên bàn | 37/90 · 72/87 · 151/158 | 41,11 · 82,76 · 95,57 |
| Rời Main với đúng một quái ở Công / Thủ | 21/64 · 16/26 | 32,81 · 61,54 |
| Triệu hồi ở Công / Thủ | 127/286 · 106/185 | 44,41 · 57,30 |
| Đổi sang Thủ, sau đó bị đối phương chọn tấn công | 1/7 | 14,29 |

Khi bàn đã có lợi thế, bot thường vừa triệu hồi được nhiều hơn vừa giữ bàn tốt hơn. Dùng thông tin số ô và vật hiến tế để đánh giá nước cụ thể. Với một quái duy nhất, ưu tiên kiểm tra sát thương có thể chịu ở hai tư thế trước khi chọn. Bảy ví dụ phòng thủ đạt tiêu chí huấn luyện đều thuộc tập train, nên hiệu quả học kỹ năng phòng thủ vẫn cần dữ liệu kiểm tra riêng.

| Quái cấp cao được triệu hồi trước Hard | Quân còn ở Main sau | Điểm /100 | Khi có lựa chọn quái cấp cao khác |
|---|---:|---:|---:|
| `aegis_sentinel` | 12/12 | 100,00 | — |
| `sample_sky_sage` | 24/28 | 85,71 | 4/4 |
| `thorn_bastion` | 11/14 | 78,57 | 3/3 |
| `siegebreaker` | 27/42 | 64,29 | 4/10 |
| `scar_duelist` | 5/8 | 62,50 | 2/2 |
| `fire_dragon` | 14/24 | 58,33 | 1/2 |

Các lựa chọn cùng có mặt còn ít. Khi dạy chọn quái cấp cao, giữ thông tin về quân bị hiến tế và số quái còn lại sau nước triệu hồi. Dùng ví dụ Hard thực hiện được và kiểm tra theo seed.

## Tấn công và kết liễu

| Điều kiện trước Hard | Đạt / quan sát | Điểm /100 |
|---|---:|---:|
| Đánh trực tiếp khi hai hàng bài Phép/Bẫy trống, gây LP damage | 30/30 | 100,00 |
| Đánh quái khi hai hàng bài Phép/Bẫy trống, hạ quái địch và giữ toàn bộ quái mình trong hành động đó | 66/161 | 40,99 |
| Chọn một đòn kết liễu có thể xác minh khi có cơ hội | 7/10 | 70,00 |

Đòn kết liễu được dạy bằng tập các nước thắng đã kiểm chứng. Các hành động còn lại dùng ví dụ Hard có kết quả cục bộ xác minh được trong replay.

## Kết quả huấn luyện thử

455 bài học train, 109 bài học kiểm tra theo seed. Reward v1 chọn trúng 64/109. Cả bản học bài tập với encoder cũ và bản học bài tập có thêm thông tin từng ô đều chọn trúng 66/109. Bản thêm thông tin từng ô có loss kiểm tra thấp hơn, điểm kiểm tra gốc 61,23% so với 62,21% của v1 và qua Python/C# parity 31/31 lựa chọn.

Lượt sàng lọc trước Hard nguyên sức mạnh đã xong 96 ván. Reward v1 thắng 18/48, ứng viên thắng 19/48. Chênh lệch ghép cặp +2,08 điểm phần trăm với khoảng tin cậy 95% từ −6,25 đến +10,42. Giữ Reward v1. Bước đề xuất là kiểm tra riêng kỹ năng chuẩn bị đòn đánh trên replay trước khi tinh chỉnh thêm.

## Tinh chỉnh riêng nước cấp năng lượng cùng giá

| Tập kiểm tra | Reward v1 ưu tiên nước cấp đủ | Ứng viên ưu tiên nước cấp đủ |
|---|---:|---:|
| Seed tách khỏi tập huấn luyện | 3/13 (23,08%) | 9/13 (69,23%) |
| Replay độc lập trước Normal | 16/31 (51,61%) | 27/31 (87,10%) |

Chỉ tinh chỉnh ba cột liên quan đến năng lượng từ Reward v1 trên 22 cặp nước cấp cùng giá. Tỷ lệ trên đo thứ hạng giữa hai nhóm nước hợp lệ khi chưa có quái sẵn sàng, nên cần đấu trước Hard để đo ảnh hưởng lên tỷ lệ thắng. Kết quả kiểm tra chi tiết ở `SimulationResults/LearnedFundamentals/attack-readiness-v1/report.json`.

Sàng lọc trước Hard đã hoàn tất. Ứng viên thắng 20/48 so với 18/48 của Reward v1 trên cùng seed, bộ bài và bên đi. Chênh lệch +4,17 điểm phần trăm, khoảng tin cậy 95% từ 0,00 đến +10,42. Mức tăng ròng nằm ở một bộ bài. Hai cặp có log chi tiết cho thấy điểm cấp năng lượng tăng đủ để thay cả một nước đổi tư thế và một nước triệu hồi. Cần kiểm tra tiếp những cặp đổi kết quả ván trước khi chọn bản thay thế.

Dữ liệu chi tiết ở `SimulationResults/LearnedFundamentals/v1/action-scorecard.csv`. Kết quả model và seed split ở `SimulationResults/LearnedFundamentals/v1/report.json`.
