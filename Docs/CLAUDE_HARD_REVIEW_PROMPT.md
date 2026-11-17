# Prompt gửi Claude — review Hard H3 và chuẩn bị ML

Bạn hỗ trợ phản biện kỹ thuật cho luận văn “Building a multi-level bot system in a2D tactical card game”. Hãy trả lời bằng tiếng Việt, kèm thuật ngữ tiếng Anh khi hữu ích cho bảo vệ luận văn.

Repo: https://github.com/BinhB2105696/2D-Strategic-Card-Game
Checkpoint cần xem: `Docs/AI_BOT_CHECKPOINT.md`, ngày2026-11-17. Nếu tôi cung cấp commit SHA, hãy cố định review vào commit đó.

Nếu bạn không truy cập được repo hoặc nội dung file, hãy nói rõ và yêu cầu đúng file cần tôi đính kèm; không giả định đã đọc. Đừng chỉ dựa vào tên file, phần tóm tắt hoặc số liệu tôi kể để kết luận code làm đúng.

## Cách đọc có mục tiêu

1. Đọc `AGENTS.md`, `Docs/HANDOFF.md`, `Docs/AI_BOT_CHECKPOINT.md`.
2. Đọc `Docs/AI_BOT_HISTORY.md`; dùng `Docs/AI_BOT_EVIDENCE/history/seed-comparison.json` khi cần đối chiếu H0/H1/H2/H3. Không cần đọc toàn bộ roadmap hoặc quét repo.
3. Review code Hard: `Assets/Scripts/Bots/HardBot.cs`, `HardTacticalPolicy.cs`, `HardStrategicEvaluation.cs`, `IStateObservingBot.cs`. Đọc phần liên quan của `NormalBot.cs`, `Core/GameState.Search.cs`, `Core/PlayerState.cs`, `Core/GameRules.cs` và định nghĩa hiệu ứng để kiểm chứng thuật toán và tính hợp lệ.
4. Xem `Docs/AI_BOT_EVIDENCE/h3-vs-normal/analysis.json`, `pair-classification.json`, `hard-profile.json`. Ưu tiên hai cặp toàn thua420047/420049; đối chiếu ca toàn thắng420041 và ca chia kết quả420040/420014. Chỉ mở các file `standout-*.json` tương ứng.
5. Với lịch sử, chọn vài ca `Docs/AI_BOT_EVIDENCE/history/case-*.json`:420048/420057 và một ca cải thiện420005 hoặc420050. Không cần đọc tất cả log.
6. Khi bàn ML, đọc `Assets/Scripts/Simulation/SimulationAiContract.cs`, `Tools/Training/action_features.py`, `prepare_imitation_data.py`, `test_action_features.py`, `smoke_train_config.json`.

## Trạng thái đã biết cần phân biệt

- Unity2022.3.62f3,41 card, ba deck chơi30 lá; hiện benchmark Hard chỉ dùng Starter Balance v4.
- Easy rule-based; Normal heuristic và dự báo chuỗi đánh cục bộ. Hard H3 là bounded single-observer ISMCTS có Normal guidance, rollout và đánh giá chiến thuật/hand+board. Đây không phải model neural tự học sau mỗi trận.
- H3 thắng Normal72/120=60%;60 paired seeds gồm14 cặp toàn thắng,2 toàn thua,44 chia kết quả. Mục tiêu>=75% chưa đạt và không phải bảo đảm.
- Seat index và thứ tự đi trước là hai biến khác nhau. Bài mở đầu có thể khiến cả hai bot đều không triển khai được; đừng mặc định mọi trận thua là lỗi thuật toán.
- H0/H1/H2 vs Easy lần lượt99/120,89/120,101/120. H3 vs Easy mới chạy5 ca chọn lọc, thắng3; không có tỷ lệ thắng H3/Easy đầy đủ.
- Hợp đồng AI v2, encoder và tập imitation nhỏ đã có; chưa có trainer, model/checkpoint hoặc inference. Dữ liệu phân tích trận không phải đầu vào hợp lệ của model.
- Máy Ryzen5500U,8GB RAM, Windows10; file trên D. Ưu tiên ngân sách nhỏ và số liệu có thể báo cáo.

## Những việc tôi muốn bạn làm trước

A. Giải thích cách Hard hiện tại khác Normal về thực chất; tìm giới hạn của sampling, candidate filtering/ranking, rollout, leaf evaluation và horizon. Hãy kiểm tra liệu các lớp heuristic có thể che mất lợi ích search hoặc cắt nhầm kế hoạch phối hợp.

B. Đánh giá các điểm nghẽn có bằng chứng, đặc biệt giữ sức ép, sống sót, lựa chọn vật tế, khai thác/triệt nguồn rút bài và phối hợp Main→Battle. Phân biệt rõ: lỗi xác nhận, quan sát trận đấu, giả thuyết, phần chưa đủ dữ liệu. Dẫn file/hàm và seed/seat/lượt cụ thể.

C. Đề xuất tối đa ba cải tiến chung, xếp theo lợi ích/chi phí. Nêu file dự kiến tác động, rủi ro, kiểm thử nhỏ và thí nghiệm đối chứng/ablation để biết cải tiến nào có ích. Không hard-code seed/card-ID để gỡ riêng một trận; không tăng ưu tiên quái cấp cao vô điều kiện; không hứa chắc75%.

D. Đề xuất bước imitation/deep-learning nhỏ khả thi trên máy8GB hoặc môi trường được tôi chấp thuận sau này: nhãn giáo viên, observation/candidate encoding, chia dữ liệu theo cả trận/cặp seed, chỉ số so với Easy/Normal/Hard và giới hạn RAM/thời gian/dung lượng. Accuracy bắt chước không thay thế tỷ lệ thắng.

E. Cho biết còn thiếu chính xác file/dữ liệu nào để kiểm chứng nhận xét. Các thư mục `SimulationResults/`, `Tools/Simulation/Control/` bị Git bỏ qua, nên không giả định bạn đã có raw traces/binaries/dataset.

## Ràng buộc

Chỉ review và thảo luận trước; chưa viết patch hay đề nghị áp dụng thay đổi hàng loạt. Giữ nguyên luật game, Core, card balance, Easy và Normal nếu chưa được tôi đồng ý cụ thể. Không đề xuất bot đọc bài ẩn/seed/thứ tự deck thật. Không nâng Editor/package, cài công cụ, train, upload, commit/push tự động. Khi cần thí nghiệm, hãy đề xuất gói để tôi chạy rồi cung cấp kết quả.

Bắt đầu bằng kết luận ngắn, sau đó bảng vấn đề–bằng chứng–ảnh hưởng, rồi ba đề xuất ưu tiên. Chỉ đưa kết luận từ những file bạn thực sự truy cập được.
