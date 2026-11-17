# Kế hoạch bot đa cấp độ và AI/ML

Cập nhật checkpoint: 2026-11-17. Dự án: 2D Strategic Card Game - Graduation Thesis.

## 1. Cách dùng tài liệu và điểm tiếp tục

- Đọc AGENTS.md trước. Khi mở chat mới, đọc Docs/HANDOFF.md trước, sau đó chỉ đọc mục tiến độ và phần liên quan trong tài liệu này; không đọc lại toàn bộ project.
- Tài liệu này lưu mục tiêu, quyết định, lý do và tiến độ dài hạn; HANDOFF là bản giao việc ngắn cho phiên tiếp theo.
- Trạng thái hiện tại: Hard H3 đã đấu Normal đủ120 trận trên Starter Balance, thắng72/120 (60%); mục tiêu75% chưa đạt. Encoder/dữ liệu imitation đã chuẩn bị; chưa có trainer/model.
- Phạm vi đã được yêu cầu: bắt đầu lộ trình và phân loại SimulationResults; xử lý lỗi kiểm tra do thao tác Set sai zone. Không cài công cụ, upload cloud hoặc sửa cân bằng card.
- Điểm tiếp tục: đọc Docs/AI_BOT_CHECKPOINT.md để review Hard/ML và bằng chứng Git. Phân tích60 cặp seed:14 toàn thắng,2 toàn thua,44 chia kết quả; xem hai seed420047/420049 và các ca giữ sức ép/nguồn rút bài.
- HANDOFF và checkpoint đã được cập nhật theo yêu cầu chuẩn bị chia sẻ Git ngày2026-11-17. Các mục/lô cũ bên dưới là lịch sử, không phải trạng thái hiện tại; không tự commit/push.

## 2. Mục tiêu và ràng buộc

- Đề tài: “Xây dựng hệ thống bot đa cấp độ trong trò chơi thẻ bài chiến thuật 2D - Building a multi-level bot system in a 2D tactical card game”. Bảo vệ bằng tiếng Anh.
- Sản phẩm chính: game offline đúng luật, chơi được, có Easy/Normal/Hard với khác biệt sức mạnh được đo bằng thực nghiệm.
- Hard: mục tiêu thắng Normal từ 75% trở lên trên bộ đánh giá đã chốt; đo trực tiếp cả Hard–Easy. Đây là mục tiêu, chưa phải kết quả hoặc bảo đảm.
- ML: bắt đầu pipeline sớm, thu dữ liệu theo batch nhỏ, train thử sớm để phát hiện vấn đề; không chờ Hard hoàn thành mới bắt đầu ML.
- Bot học được dùng làm đối tượng so sánh riêng; chưa chốt thành mức khó thứ tư. Không mặc định model phải thắng Hard mới có giá trị.
- Giá trị có thể đo: sức mạnh, tốc độ quyết định, chi phí huấn luyện, khả năng tổng quát hóa và thích ứng sau thay đổi có kiểm soát.
- Giữ Unity 2022.3.62f3; không tự nâng Editor/package. Giữ mọi thay đổi chưa commit.
- Ràng buộc mới từ người dùng: luật và file nền game chỉ được đọc. Không sửa GameRules/GameSession/GameState/CardInstance/PlayerState, định nghĩa card/hiệu ứng hoặc nền tảng tương tự nếu chưa được người dùng đồng ý cụ thể. Nếu thấy lỗ hổng/thiếu sót, báo bằng chứng và ảnh hưởng trước, không tự khắc phục sâu. Các thay đổi Core đã làm trước ràng buộc này được giữ nguyên, không tự mở rộng hoặc hoàn tác.
- PvP và tài khoản online để cuối. Artwork và quy chuẩn text do người dùng tiếp tục trong chat khác.
- Không tự cài công cụ, kết nối tài khoản, upload dữ liệu hoặc chạy train khi chưa có yêu cầu tương ứng.
- Chưa biết hạn nộp/bảo vệ; chưa hứa số ngày hoàn thành hoặc thời gian train.

## 3. Máy và ngân sách dự kiến

- CPU: AMD Ryzen 5 5500U with Radeon Graphics; RAM 8 GB, usable khoảng 7,36 GB; Windows 10 Enterprise LTSC, x64.
- Dung lượng do người dùng cung cấp: C còn khoảng 60/176 GB; D còn khoảng 115/300 GB. Ưu tiên đầu ra trên D.
- Chưa xác nhận GPU rời. Phương án phải chạy được bằng CPU; Colab là lựa chọn bổ sung cho trainer.
- Đã xác nhận Python 3.12.0 tại D:/Python312/python.exe và Python 3.12.14 trong runtime có sẵn. Cả hai chưa có torch; runtime có NumPy 2.3.5 nhưng các bước chuẩn bị dữ liệu hiện dùng thư viện chuẩn. Không chạy WindowsApps/python.exe vì đó là alias có thể mở Store. Chưa tạo venv hoặc cài package.
- Sau build, nên đóng Unity Editor trước khi chạy runner; tránh Editor, runner và trainer cùng chạy trên máy 8 GB.

Các ngưỡng dưới đây là đề xuất, chưa được công cụ tự áp đầy đủ:

| Hạng mục | Ngân sách khởi đầu |
| --- | --- |
| Runner | Một tiến trình; mục tiêu RAM riêng không quá khoảng 1,5 GB |
| RAM hệ thống | Tạm dừng nếu RAM khả dụng xuống dưới khoảng 1,5 GB |
| Pilot đầu tiên | 12 trận, tối đa 30 phút hoặc tổng đầu ra 1 GB; chạm ngưỡng nào trước thì dừng |
| Phiên thu dữ liệu sau pilot | Khoảng 30–60 phút, tính cả replay/export; điều chỉnh theo số đo |
| Thử nghiệm AI ban đầu | Khoảng 10–20 GB trên D; giữ tối thiểu 40 GB trống |
| Model ban đầu | Mạng nhỏ, định hướng dưới khoảng một triệu tham số; batch 32–64 chỉ là điểm thử |

- Không cam kết mọi cache/tệp tạm đều nằm trên D; giữ dung lượng dự phòng C cho Windows/công cụ.
- Drive giải quyết lưu trữ, không thay thế RAM khi chạy chương trình. Chưa cần mua dung lượng hoặc upload ngay.

## 4. Nền tảng đã có và bằng chứng

| Thành phần | Trạng thái đã ghi nhận |
| --- | --- |
| Card | 41 thiết kế: 21 Monster, 17 Spell, 3 Trap; cần nghiệm thu đầy đủ tương tác expansion |
| Deck | Ba preset 30 lá: Starter Balance v4, Crimson Assault, Aegis Bastion |
| Easy | Rule-based, chấm điểm các hành động hợp lệ do luật game cung cấp |
| Normal | Heuristic chi tiết, đánh giá hiệu ứng/chi phí và dự báo chuỗi tấn công cục bộ |
| Forecast của Normal | Độ sâu tối đa 5, giới hạn 256 nút cho một lần forecast; không phải giới hạn tổng cho cả quyết định |
| Runner | Headless Windows/Mono, seed, kết quả, trace và metadata; cần build lại đồng bộ v4 trước thu dữ liệu mới |
| Replay | Kiểm chứng trạng thái, hành động, observation và kết quả; phụ thuộc đúng phiên bản/fingerprint |
| AI contract | v1 lịch sử: 17 global features. v2 hiện tại: 21 global features, 18 features/entity, quan hệ retaliationMarks theo chỉ số entity; cần rebuild Player trước khi dùng |
| Dataset | Xuất transitions.jsonl sau replay đạt; có cả hai phía; có thể xuất prefix được xác minh với truncated |
| Kiểm thử lịch sử | Tài liệu ghi Test-Replay 18/18 PASS; sáu trận với 922 quyết định; chưa chạy lại trong chat này trên expansion |
| Còn thiếu ở checkpoint2026-11-17 | Trainer, Gym adapter, model và inference; Hard H3 và search fork đã có |

- Commit 0027715: headless runner và testing notes. Commit f51a4e6: replay/state hash/AI contract, kèm SDK PvP.
- Hai commit đã được đối chiếu thuộc lịch sử ref origin/main trên máy; không fetch để xác nhận remote trực tiếp.
- Lịch sử Normal tăng khoảng 58% lên hơn 71% thắng Easy là kết quả bộ card/deck cũ; không dùng làm kết quả của expansion hiện tại.
- Nguồn sở hữu: Assets/Scripts/Core/IBot.cs, EasyBot.cs, BotGameStateView.cs; Assets/Scripts/Bots/NormalBot.cs; Assets/Scripts/Simulation/SimulationAiContract.cs, SimulationReplay.cs, SimulationStateHash.cs.
- Hướng dẫn liên quan: Tools/Simulation/README.md (replay/AI contract), TESTING_NOTES.md (kết quả và giới hạn); đọc đúng mục khi cần.

## 5. Thiết kế để chịu được thay đổi game

- Luật game là nguồn quyết định hành động hợp lệ và kết quả; bot chỉ chọn chiến lược. Không chép lại toàn bộ luật vào từng bot.
- Hard dùng luật thật khi mô phỏng; trạng thái nhánh và RNG tìm kiếm phải độc lập với trận thật.
- Observation quy định thông tin được biết. Không dùng seed, deck ẩn, thứ tự deck hoặc full state thật làm đầu vào policy hay căn cứ chấm nhánh có lợi thế thông tin.
- Normal hiện nhận thêm dữ liệu công khai qua Observe; cần đối chiếu thông tin giáo viên dùng với thông tin model nhận, không chỉ so số lượng feature.
- Ưu tiên thuộc tính card, loại hiệu ứng, chi phí và kết quả mô phỏng; hạn chế điều kiện theo tên/cardId.
- Dùng hành động hợp lệ hiện tại làm ứng viên; model chấm điểm từng ứng viên, không gán ý nghĩa cố định cho selectedActionIndex giữa các trạng thái.
- Cần kiểm tra public observation cho retaliation mark, hạn hiệu ứng, điều kiện dựa trên đầu Main Phase và các pending choice có thể còn opaque. Bổ sung có phiên bản nếu thiếu thông tin cần thiết.
- Card mới dùng cơ chế cũ: kỳ vọng vẫn vận hành hợp lệ; sức mạnh cần đánh giá lại.
- Cơ chế hoặc action mới: có thể cần sửa luật, adapter/observation, heuristic và model. Không hứa tự hiểu mọi cơ chế.
- Thay luật: khả năng chạy đúng và khả năng chơi tốt là hai tiêu chí riêng; model có thể cần fine-tune/train lại.

## 6. Phương pháp: hướng ưu tiên và phần chưa chốt

- Hard ưu tiên tìm kiếm có giới hạn: dùng heuristic chọn nhánh triển vọng, mô phỏng chuỗi hiệu ứng–Energy–tấn công và phản ứng đối thủ.
- Chưa chốt Beam Search hay ISMCTS. Đo chi phí clone/apply và độ phân nhánh trước khi quyết định; không dùng chỉ số depth để so trực tiếp với forecast tấn công của Normal.
- Minimax/Alpha–Beta hữu ích trong mô phỏng xác định; game có bài ẩn/ngẫu nhiên cần giả định và xử lý bổ sung. Không gọi tìm kiếm trên full state thật là đối thủ công bằng.
- ML ưu tiên deep imitation learning: model nhỏ chấm điểm hành động hợp lệ; cân nhắc encoder entity dùng chung, tổng hợp trạng thái và liên kết source/target/tribute.
- Không chỉ dùng embedding cardId để kỳ vọng thích nghi card mới. Cân nhắc mô tả hiệu ứng/chi phí công khai; giữ vị trí/zone và quan hệ có ý nghĩa khi tổng hợp entity.
- Thứ tự đề xuất: học Normal để kiểm pipeline → học Hard → DAgger nhỏ → RL nếu throughput/thời gian cho phép.
- DAgger cần cơ chế hỏi giáo viên ở trạng thái model gặp, không chỉ nối thêm log giáo viên tự chơi.
- PPO chuẩn cần rollout từ policy đang học; kho log Normal không thay trực tiếp phần tương tác đó. Log hiện có dùng rõ nhất cho imitation/pretraining.
- Deep CFR và các hướng RL là nghiên cứu liên quan, chưa thuộc cam kết triển khai chính.
- Học bắt chước không bảo đảm vượt giáo viên; đo cả sức mạnh và lợi ích tốc độ.

## 7. Lộ trình và tiêu chí hoàn tất

| Giai đoạn | Việc chính | Bằng chứng cần có | Tiến độ |
| --- | --- | --- | --- |
| A | Kiểm observation/contract cho expansion; đồng bộ runner v4; kiểm các tương tác quan trọng | Kiểm chứng replay/export và quyền riêng tư trên phiên bản mới; ghi rõ phần còn thiếu | Foundation/Unity/Player replay-export và dataset v2 PASS; không phải nghiệm thu toàn bộ tương tác/chain của 41 card |
| B | Pilot 12 trận, đo tài nguyên; baseline Easy/Normal mới; train thử dữ liệu nhỏ | Số đo sinh/replay/export; nhãn đúng; checkpoint lưu/nạp được; kiểm inference đầu tiên | Hai batch smoke đã kiểm chứng; dữ liệu/encoder chia nhóm seed sẵn sàng; trainer/PyTorch/model chưa có |
| C | Thu dữ liệu Normal theo batch độc lập; phát triển Hard và khả năng mô phỏng nhánh | Dataset có manifest/độ phủ; clone/apply đúng; kết quả Hard–Normal/Easy | Đã thu hai batch smoke, đủ 41 card quan sát; chưa có Hard hoặc benchmark lớn |
| D | Học Hard, cân nhắc DAgger; kiểm tổng quát hóa | Đấu thật với bot; learning curve; so chất lượng/chi phí | Chưa bắt đầu |
| E | Đóng băng phiên bản, chạy đánh giá cuối, chuẩn bị báo cáo/bảo vệ tiếng Anh | Bảng kết quả tái lập được, giới hạn và nghiên cứu liên quan | Chưa bắt đầu |

- Mốc 1.000–3.000 quyết định đúng của Normal chỉ là dataset thử pipeline, không phải lượng dữ liệu đủ để chơi mạnh.
- Train thử sớm rồi mới quyết định tăng dữ liệu. Đo cả thời gian replay/export, không chỉ thời gian bot đấu.
- Build cần thao tác Unity: dừng Play, lưu scene cần giữ, chọn Card Game > Simulation > Build Headless Windows Runner. Chỉ hand off thao tác này khi đến giai đoạn được yêu cầu, không coi là việc người dùng phải làm ngay.
- Cấu hình pilot: Tools/Simulation/ai-v2-pilot.json; ba cặp cùng deck (0/0, 1/1, 2/2), hai seed 310001–310002, đổi bot hai phía = 12 trận được ghi, thêm hai trận warm-up không tính. Giữ 12 trace; dùng kiểm chứng pipeline/tài nguyên, không kết luận tỷ lệ thắng hoặc độ phủ mọi hiệu ứng từ mẫu này.
- Chạy từ Unity khi dừng Play: Card Game > Simulation > Run AI v2 Pilot. Công cụ khởi chạy Player trực tiếp, không dùng PowerShell hay mở cửa sổ terminal; tiến độ/kết quả/log được báo trong Console. Stop AI Pilot chỉ dừng đúng tiến trình do công cụ khởi chạy. Đóng Editor hoặc reload script sẽ dừng pilot và giữ dữ liệu dở.
- Menu pilot giám sát giới hạn 30 phút, RAM runner 1536 MiB, đầu ra 1024 MiB. RAM đọc mỗi giây, dung lượng mỗi năm giây nên có thể vượt nhẹ giữa hai lần kiểm tra; không đo RAM khả dụng toàn máy. Pilot/replay từ Unity đã chạy thành công; nguyên nhân gốc lỗi quyền trong tiến trình Codex chưa được chứng minh. Không sửa File.Replace hoặc luật để né lỗi.
- Pilot đầu từ Unity đã chạy thành công; báo cáo RAM ghi 0 nên xem là chưa đo được, không kết luận đã đáp ứng trần RAM. Đã thêm SimulationProcessResources: thử chỉ số Process rồi fallback Windows GetProcessMemoryInfo cho đúng tiến trình con; báo cáo mới ghi null/ramMeasurementAvailable=false nếu vẫn không đo được. Replay batch đã xác nhận helper đo được RAM đỉnh 104.157.184 byte (~99,33 MiB), không suy ngược thành RAM của pilot trước đó.
- Deck thu dữ liệu v1 (Tools/Simulation/collection-decks-v1.json): Collection Resources v1 có 17M/10S/3T; Collection Tributes v1 có 20M/7S/3T. Mỗi deck 30 lá, đã đối chiếu ID/copy limit với catalog thật; union cùng ba preset cũ là 41/41. Chỉ nối vào SimulationCatalog ở index 3/4 khi build; không sửa LocalDeckStore/profile/preset chơi. Cần kiểm độ phủ quan sát và hành động thực sau chạy.
- Build tool kiểm cả fingerprint danh sách deck trước build/retry; build-info mới có deckCount/collectionConfigHash. Run AI Collection Pilot chỉ chạy sau khi build khớp cấu hình, tránh dùng Player cũ chưa có deck index 3/4. Cấu hình riêng ai-v2-collection-pilot.json giữ 12 trace, hai seed mới 310003–310004, ba matchup cùng deck 0/0, 3/3, 4/4; không thay config pilot cũ.
- Replay/export: Card Game > Simulation > Replay and Export Latest AI Pilot; Stop AI Replay Export dừng đúng tiến trình đang sở hữu. Công cụ chạy tuần tự 12 trace, giới hạn 120 giây/trace, 30 phút/batch, RAM 1536 MiB và đầu ra replay 1024 MiB; chỉ chấp nhận verified-match hoàn tất. Có cơ chế nhận yêu cầu đường dẫn run từ ai-replay-request.txt trong Control, lưu dấu accepted và không sửa trace nguồn. Nếu đã có yêu cầu đang chờ thì chỉ chuyển focus Unity/chờ, tránh bấm menu tạo thêm batch trùng.
- Sửa ghi nhận đường dẫn bridge: SimulationBuildTool.Control thực tế là Tools/Simulation, nên build request đúng là Tools/Simulation/editor-request.txt, không phải Tools/Simulation/Control/editor-request.txt. File cũ gửi nhầm đã được giữ trong Control/request-history/obsolete-build-request_*.txt; không phải Unity tự mất yêu cầu. Collection/replay request vẫn đúng trong Tools/Simulation/Control. Không gửi lại batch cũ đã chạy.

## 8. Dataset và protocol đánh giá

- Chọn seed train/tuning/test tách biệt ngay từ đầu; chia theo cả trận/nhóm seed, không chia ngẫu nhiên từng dòng.
- Lọc đúng phía bot giáo viên; dataset có cả hai người chơi. Không chỉ giữ trận giáo viên thắng.
- Bộ dữ liệu chính ban đầu ưu tiên verified-match hoàn tất. verified-prefix/truncated giữ riêng; không gán thua hoặc hòa chỉ vì reward bằng 0.
- Chống trùng dữ liệu khi export lại cùng trace; định danh bằng trace/hash, player, sequence và phiên bản tương ứng.
- Ghi số trận hoàn tất/lỗi/bị cắt, quyết định của giáo viên, độ phủ deck/card/action, thời gian, RAM và dung lượng.
- Cố định rules/catalog/deck/policy/model/preprocessing version khi so sánh; không trộn âm thầm luật cũ và mới.
- Đấu theo seed ghép cặp và đổi ghế, giữ deck gắn với bot. Tổng hợp cân bằng matchup và báo chi tiết từng cặp deck.
- Lưu ý implementation hiện tại: SimulationRunner.Jobs() với swapBots chỉ đổi bot, không đổi deck0/deck1. Mục tiêu giữ deck gắn với bot ở dòng trên chưa được áp tự động khi hai deck khác nhau. Pilot dùng cùng deck hai phía để tránh nhập nhằng. Đánh giá đấu chéo cần cấu hình/lịch đấu được xác nhận riêng; chưa sửa runner vì phát hiện này.
- Báo thắng/hòa/thua, khoảng tin cậy theo nhóm seed, thời gian quyết định và fallback. Không bỏ âm thầm trận lỗi/bị cắt.
- Tỷ lệ thắng dùng W/(W+D+L) cho trận hoàn tất; nếu dùng score = win + 0,5 draw thì báo thành chỉ số khác.
- Mục tiêu quan sát đạt 75% khác với chứng minh cận dưới khoảng tin cậy trên 75%.
- Ví dụ đánh giá một cặp bot: 9 cặp deck × 30 seed × 2 ghế = 540 trận. Đây là cấu hình minh họa; chốt sau đo pilot.
- Khi đủ ngân sách, train khoảng ba seed để xem độ ổn định. Accuracy bắt chước không thay thế tỷ lệ thắng.
- Ablation đề xuất: Hard có/không search; giáo viên Normal/Hard; có/không DAgger. Kiểm deck phối hợp mới và card giữ ngoài train nếu thời gian cho phép.
- Chưa chứng minh sức mạnh có thứ tự hoặc tính tổng quát hóa; phải đo trực tiếp, không suy ra từ tên bot.
- Kết quả smoke đã xác minh: 12 episode, 1.203 transition, 2.406 current/next policy được kiểm tra; 620 quyết định Normal và 583 Easy. Không coi cả 1.203 mẫu là nhãn Normal. Chỉ có hai seed trong ba cặp deck cùng loại; chưa có tập holdout hay kết luận sức mạnh.
- Độ phủ: toàn bộ ba preset chỉ chứa 33 card khác nhau, cũng là 33 public card ID quan sát được. Thiếu Change of Plans, Energy Reallocation, Energy Venom, Gravebound Imp, Legendary Fighter, Static Leech, Temporal Lock, Veil Strategist. Thấy ID không chứng minh hiệu ứng đã kích hoạt. Cần deck simulation thu dữ liệu riêng; không tự sửa ba preset chơi, luật hoặc cân bằng để tăng độ phủ.
- Validate-AiDataset.ps1 (PowerShell 7 đã có sẵn) kiểm tra theo dòng: provenance, schema/dimensions, local entity indices/masks, reward/terminal, duplicate/per-player sequence, số mẫu đối chiếu replay/CSV, phân loại giáo viên và checksum. Chỉ viết báo cáo/bản kê mới, không sửa export/trace nguồn. Đây là công cụ kiểm dữ liệu, không bắt người dùng mở PowerShell để chạy trận.
- Dữ liệu gộp smoke 2026-11-16: 1.405 mẫu Normal, 1.391 mẫu Easy loại khỏi nhãn giáo viên. Loại thêm 281 mẫu Normal chỉ có một ứng viên; còn 1.124 mẫu có lựa chọn thực: 878 train/246 validation. Train seeds 310001–310003; validation seed 310004; giữ cả trận/cặp seed cùng nhóm, không có test set độc lập.
- Tools/Training/action_features.py: encoder v1 chỉ nhận public policy. Pool entity theo owner/zone, one-hot public card/action type, đặc trưng source/target/tribute và mark theo hai chiều; context 876 chiều, mỗi ứng viên 203 chiều. Card chưa biết vào UNK giữ kích thước, không bảo đảm chơi tốt. Đây là bộ mã hóa cố định cho baseline, chưa phải model neural đã học hoặc chứng minh observation đủ mọi thông tin Normal dùng trong chain.
- prepare_imitation_data.py kiểm checksum/provenance tương thích trước khi chọn Normal/chia seed; training_manifest.json giữ hash file, nguồn và thống kê. Không đưa group_seed/episode/provenance vào encode. Sáu kiểm tra encoder PASS: hoán vị ứng viên, đánh lại chỉ số entity, unknown card, tham chiếu sai, schema sai và metadata không ảnh hưởng đầu vào.
- smoke_train_config.json mới là cấu hình đề xuất: chấm điểm ứng viên bằng hai hidden layer 64/32 (~71.233 tham số với encoder hiện tại), batch 16, ba epoch, CPU hai thread, ngân sách năm phút. Chưa có trainer PyTorch, chưa cập nhật trọng số, checkpoint hoặc bot inference. Cần đồng ý trước khi cài PyTorch CPU vào môi trường riêng trên D, hoặc chọn Colab rồi chuẩn bị gói mang sang; không tự upload/kết nối.
- Teacher coverage notes: bảy card thiếu trước đây đã có hành động dùng làm source; Legendary Fighter có năm lần Normal summon. Energy Venom có 102 trạng thái teacher có ứng viên kích hoạt nhưng không được chọn lần nào. Đây là giới hạn hành vi giáo viên trong mẫu nhỏ, không phải bằng chứng lỗi luật; chưa sửa Normal/Core. Học bắt chước có thể học cả thói quen bỏ qua card này; không dùng phát hiện đó để trì hoãn mọi thử nghiệm trainer.

## 9. Lưu phiên bản, replay và cloud

- Ưu tiên tái lập thí nghiệm và dataset; UI playback không phải đường găng hiện tại.
- Mỗi đợt giữ runner/catalog tương ứng, source revision/fingerprint, rules/RNG/runtime version, config/deck/policy, trace, report và dataset đã xác minh.
- Model đi kèm observation schema, vocabulary/preprocessing, cấu hình train, train seed, dữ liệu nguồn và checkpoint (optimizer/tiến độ nếu cần resume).
- Replay cũ dùng môi trường tương ứng. Hash phát hiện lệch, không phải snapshot tự khôi phục hay cam kết tương thích Windows/Mono–Linux/IL2CPP.
- Dataset đã xác minh có thể dùng train lại với preprocessing tương ứng mà không replay lại mỗi lần. Đánh giá luật mới phải dùng trận mới.
- Runtime source fingerprint hiện được replay kiểm tra chặt; cần lưu bản runner khớp trước các thay đổi liên quan, không chỉ ghi số schema.
- Runner hiện chỉ được kiểm chứng Windows/Mono. Phương án đầu tiên: Windows sinh/xác minh dữ liệu → Colab train offline nếu cần → đánh giá model bằng runner Windows sau khi tích hợp.
- Drive lưu các batch đóng gói, manifest, kết quả và checkpoint. Chép shard nén sang ổ tạm Colab để train; không đọc hàng nghìn file nhỏ trực tiếp trên Drive.
- Colab miễn phí không bảo đảm GPU/RAM/thời gian; FAQ được kiểm tra trong chat nêu tối đa 12 giờ tùy khả dụng/sử dụng. Phải kiểm lại khi thiết lập thực tế.
- Google Account thông thường có 15 GB dùng chung Drive/Gmail/Photos, không phải dung lượng trống đã xác nhận của người dùng.
- Đề xuất giữ checkpoint tốt nhất và hai bản gần nhất; chỉ xóa bản khác sau khi đối chiếu giá trị tái lập và có yêu cầu phù hợp.

## 10. Thuật ngữ dễ hiểu

| Thuật ngữ | Nghĩa trong dự án |
| --- | --- |
| Rule-based | Bot chọn theo luật ưu tiên do mình viết, ví dụ ưu tiên đòn kết liễu |
| Heuristic | Công thức ước lượng nước đi tốt/xấu, chưa cần biết chắc kết quả cuối trận |
| Lookahead / search | Thử vài chuỗi hành động trong mô phỏng trước khi chọn nước thật |
| Node / depth / beam | Một nút là điểm trong cây tìm kiếm; depth là độ sâu; beam là số nhánh tốt được giữ lại |
| Minimax / Alpha–Beta | Xét mình chọn tốt và đối thủ phản ứng bất lợi cho mình; Alpha–Beta bỏ các nhánh không cần xét trong điều kiện phù hợp |
| MCTS / ISMCTS | Dùng các lần mô phỏng để hướng tìm kiếm; ISMCTS xử lý theo tập trạng thái phù hợp thông tin đang biết |
| Observation | Thông tin bot được phép nhìn thấy; khác với toàn bộ trạng thái bên trong game |
| Legal action / mask | Nước đi được luật cho phép; mask đánh dấu ứng viên hợp lệ. Hợp đồng hiện chỉ liệt kê ứng viên hợp lệ nên mask đều bằng 1 |
| Policy | Cách bot chọn hành động từ thông tin đang có; có thể là luật, thuật toán hoặc model |
| Deep learning / neural network | Học các trọng số trong mạng nhiều lớp để tính điểm/dự đoán; không tự đồng nghĩa với học tăng cường |
| Imitation / behavior cloning | Học bắt chước lựa chọn của bot giáo viên từ ví dụ trạng thái–hành động |
| DAgger | Cho model chơi, hỏi giáo viên nên làm gì ở các trạng thái model gặp, rồi gộp nhãn để học tiếp |
| Reinforcement learning / PPO | Học qua tương tác và thưởng/phạt; PPO là một thuật toán cập nhật policy, cần các lượt tương tác phục vụ việc học |
| Self-play | Cho các policy đấu với nhau để sinh trải nghiệm; không chỉ có nghĩa một bot cố định tự đấu là tự học |
| Reward / value | Reward là điểm thưởng quan sát được; value là ước lượng lợi ích tương lai. Hiện reward thắng/thua chủ yếu ở cuối trận |
| Episode / transition | Episode là một trận; transition nối quyết định hiện tại đến quyết định tiếp theo của cùng người chơi, có thể đi qua lượt đối thủ |
| Dataset / batch / shard | Dataset là tập dữ liệu; batch có thể là lô trận hoặc nhóm mẫu học; shard là phần dữ liệu chia thành file vừa phải |
| Epoch / learning rate | Epoch là một lượt qua tập train trong cách học phù hợp; learning rate điều khiển mức thay đổi trọng số mỗi lần cập nhật |
| Checkpoint / inference / fine-tune | Bản lưu tiến độ học / dùng model để ra quyết định / học bổ sung từ model có sẵn |
| Train / validation / test | Dữ liệu để học / chọn cấu hình / đánh giá cuối chưa dùng để chọn model |
| Seed | Giá trị khởi tạo ngẫu nhiên giúp tái lập; cần phân biệt seed trận, seed search và seed train |
| Baseline / benchmark / matchup | Mốc so sánh / bộ điều kiện đánh giá chung / cặp bot-deck đấu nhau |
| Replay / state hash / fingerprint | Chạy lại hành động / dấu kiểm trạng thái / dấu nhận dạng code hoặc dữ liệu; có hash chưa có nghĩa replay đã đạt |
| Schema / manifest | Quy cách trường dữ liệu có phiên bản / bản kê phiên bản, cấu hình và nguồn của một đợt |
| Truncated / terminated | Trận bị cắt do giới hạn / trận kết thúc theo luật; không được đánh đồng |
| Overfitting / leakage | Học quá sát dữ liệu cũ / để lộ dữ liệu không được phép biết hoặc dữ liệu test vào quá trình chọn model |
| Generalization / ablation | Chơi được ở tình huống chưa học / bỏ một thành phần để đo đóng góp thực của nó |
| Confidence interval / p95 | Khoảng thể hiện bất định của ước lượng / mốc mà 95% số lần đo không vượt quá |
| Throughput / latency | Số trận/quyết định xử lý trong một đơn vị thời gian / thời gian chờ cho một quyết định |

## 11. Nguồn nghiên cứu đã tham khảo

- ISMCTS — Cowling, Powley, Whitehouse (2012): https://eprints.whiterose.ac.uk/id/eprint/75048/
- DAgger — Ross, Gordon, Bagnell (2011): https://proceedings.mlr.press/v15/ross11a.html
- PPO — Schulman và cộng sự (2017): https://arxiv.org/abs/1707.06347
- Deep CFR — Brown và cộng sự (2019): https://proceedings.mlr.press/v97/brown19b.html
- DouZero — Zha và cộng sự (2021): https://proceedings.mlr.press/v139/zha21a.html
- Deep Sets — Zaheer và cộng sự (2017): https://proceedings.neurips.cc/paper_files/paper/2017/hash/f22e4747da1aa27e363d86d40ff442fe-Abstract.html
- Colab FAQ: https://research.google.com/colaboratory/faq.html
- Google storage: https://support.google.com/drive/answer/9312312?hl=en

## 12. Cập nhật tiến độ và giao việc cho chat mới

- Sau công việc liên quan được yêu cầu: cập nhật trạng thái giai đoạn, file đổi, cấu hình/phiên bản, đường dẫn bằng chứng, kết quả và việc còn thiếu; không ghi PASS khi chưa chạy.
- Đưa điểm tiếp tục ngắn lên mục 1; cập nhật ngân sách bằng số đo thực, giữ nhãn “đề xuất” với phần chưa chốt.
- Khi cần chuyển chat, cập nhật tài liệu này rồi tạo/cập nhật Docs/HANDOFF.md tối đa khoảng 60 dòng theo AGENTS.md; không làm trái yêu cầu chưa cập nhật handoff của người dùng.
- HANDOFF chỉ giữ: mục tiêu hiện tại, đã làm và file đổi, tiến độ/bước kế tiếp, quyết định quan trọng, lệnh kiểm tra nhanh và liên kết đến đúng mục của tài liệu này.
- Không tự đọc lại toàn bộ tài liệu, chạy lại kiểm thử đã đạt hoặc mở rộng thành audit khi tiếp tục.

### Nhật ký mốc

| Ngày | Công việc / bằng chứng | Trạng thái |
| --- | --- | --- |
| 2026-11-15 | Đối chiếu có mục tiêu Easy/Normal, AI contract/replay và lịch sử hai commit; nghiên cứu phương pháp, thảo luận mục tiêu và nguồn lực | Chỉ đọc/thảo luận; không chạy lại kiểm thử |
| 2026-11-15 | Tạo Docs/AI_BOT_ROADMAP.md theo yêu cầu; giữ nguyên HANDOFF và thay đổi khác | Hoàn thành tài liệu; chưa triển khai A–E |
| 2026-11-15 | Lập SimulationResults/INDEX.md và inventory.json: 26 nhóm dữ liệu cũ khoảng 1,238 GiB, giữ nguyên nguồn; thêm nhóm Runs/Datasets/Models/Runners và script cập nhật mục lục | Hoàn tất phân loại bằng mục lục; không di chuyển/xóa dữ liệu |
| 2026-11-15 | Thêm snapshot đầu Main Phase/quan hệ mark vào AI contract v2; vị trí kết quả mới theo rules/catalog/AI version; cơ chế lưu runner trước build và -RunnerDirectory cho replay | Mã đã thêm; build/archive và replay Player thực tế chưa được chạy xác nhận |
| 2026-11-15 | Sửa fixture trong SimulationExpansionAiVerification.Verify: monster được dựng trạng thái ẩn bằng MoveToZone thay vì gọi SetFaceDownInSpellTrapZone; kiểm dấu đánh vẫn còn khi hiện lại | Runtime biên dịch 0 lỗi; ReplayFoundationVerification và expansion AI PASS trên Mono/thư viện Unity 2022.3.62f3. Bằng chứng: Tools/Simulation/Control/AiContractChecks/20261115_132555/compile.log và checks.log. Có cảnh báo CS0649 ở các trường serialize hiện có; chưa chạy lại 18 ca Test-Replay |
| 2026-11-15 | Người dùng gửi Console Replay foundation PASS sau khi chạy menu Verify Replay State and AI Contract; bổ sung giới hạn không sửa luật/file nền khi chưa được đồng ý | Unity verification PASS theo ảnh Console; chưa có bản Player mới |
| 2026-11-15 | Chuẩn bị ai-v2-pilot.json: 12 trận cùng deck hai phía, bao phủ ba preset; ghi nhận đúng semantics swapBots để tránh diễn giải nhầm đấu chéo | Cấu hình chuẩn bị; chưa chạy pilot, replay Player hoặc train |
| 2026-11-15 | Xác nhận build-status/build-info: Unity 2022.3.62f3, rules/catalog v4, AI v2, source fingerprint e0e8ceccdeb1ea3d9081c783c28afb4058d05a89ef1ac4b7471de27d5b57f771 | Build thành công 0 lỗi/0 cảnh báo; runner cũ đã có archive-manifest tại SimulationResults/Runners/20261115_134329_cb556af5 |
| 2026-11-15 | Thêm giới hạn tài nguyên vào Run-Simulation.ps1; pilot từ Codex dừng khi warm-up do UnauthorizedAccessException trong SimulationJson.Atomic/File.Replace | 0/12 hoàn tất; giữ run 20261115_134939_02b185b9 và Control/20261115_204937_27ad1939.log/.resources.json; không dùng làm dữ liệu train |
| 2026-11-15 | Thêm SimulationPilotTool.cs theo yêu cầu chạy từ Unity; không sửa luật/Core | Chờ chạy menu Unity; chưa có pilot hoàn tất/replay export/model |
| 2026-11-15 | Kiểm tra pilot Unity 20261115_140759_75f9ed04: 12/12 Finished, 12 trace, 41 card, ba preset đúng tên, rules/catalog v4, AI v2 | Wall 29,098 giây gồm warm-up/khởi chạy; đầu ra kèm log 23.426.107 byte (~22,34 MiB). Resource report: Control/editor_pilot_20261115_140759_504d06c4.resources.json. RAM=0 là số đo không hợp lệ; chưa có dataset replay-verified |
| 2026-11-15 | Thêm SimulationReplayTool.cs, SimulationProcessResources.cs; sửa giám sát RAM và khóa chạy đồng thời pilot/replay; gửi yêu cầu replay cho run pilot đã hoàn tất | Ba công cụ Editor biên dịch 0 lỗi, có cảnh báo CS0649 cho DTO deserialize. Bằng chứng: Control/EditorReplayChecks/20261115_141625/compile.log. Sau 40 giây yêu cầu vẫn pending; dừng chờ người dùng đưa Unity lên để import/biên dịch. Chưa xác nhận replay/export hoặc tính trọn vẹn dữ liệu |
| 2026-11-15 | Người dùng đưa Unity lên; replay/export hoàn tất 12/12 và được đối chiếu batch-report.json | Batch 20261115_142220_editor_6085b37c: 1.650 hành động/1.203 quyết định xác minh, 37,669 giây; RAM đỉnh runner ~99,33 MiB; 12 exports tổng 6.467.384 byte (~6,17 MiB) |
| 2026-11-15 | Thêm/chạy Validate-AiDataset.ps1: 12 episode, 1.203 transition (620 Normal/583 Easy), 1.210.695 kiểm tra cấu trúc/nhãn/provenance PASS | Report/episode manifest/coverage tại replay batch/validation/20261115_143731_3e695424. Đây là số assertion dữ liệu, không phải hơn một triệu ca kiểm thử luật độc lập |
| 2026-11-15 | Lập manifest dataset smoke trong SimulationResults/Datasets/pilot_rules-v4_ai-v2_20261115_140759_75f9ed04; xác nhận tám card vắng mặt trong mọi preset | Không sao chép raw exports; không thay luật/Core/preset; chưa train/cài/upload. Cập nhật HANDOFF để chuyển chat dài |
| 2026-11-15 | Người dùng yêu cầu tiếp tục trong chat này, tự quyết định thời điểm giao chat | Chỉ cập nhật roadmap, không cập nhật HANDOFF tiếp khi chưa được yêu cầu |
| 2026-11-15 | Chuẩn bị collection-decks-v1.json và ai-v2-collection-pilot.json; thêm kiểm tra/deck bổ sung trong SimulationBuildTool, menu/queue Run AI Collection Pilot trong SimulationPilotTool; validator cho phép thêm preset simulation | Kiểm 30 lá/copy limit/union 41 card PASS; công cụ Editor biên dịch 0 lỗi tại Control/CollectionEditorChecks/20261115_150250. Chưa build/chạy deck mới; đã xếp hai yêu cầu, sau chờ 35 giây cả hai còn pending, cần Unity nhận script mới |
| 2026-11-16 | Người dùng chạy menu build/collection; kiểm R=20261116_043203_8736d4bc: 12/12 Finished, 41 card/năm preset; ba preset đầu khớp dữ liệu cũ | Pilot 34,071 giây, RAM runner đỉnh 105.299.968 byte (~100,42 MiB), đầu ra kèm log 29.910.367 byte. Giữ hai deck collection; không sửa deck chơi |
| 2026-11-16 | Replay/export run collection qua Unity, batch 20261116_043948_editor_89ea685a; Validate-AiDataset PASS | Replay 12/12, 46,037 giây, RAM đỉnh 103.636.992 byte (~98,84 MiB); 1.593 transition (785 Normal/808 Easy). Validation/20261116_044211_e99225f7: report.json + dataset-manifest.json, 1.532.805 assertion dữ liệu PASS |
| 2026-11-16 | Gộp dữ liệu chuẩn bị smoke bằng prepare_imitation_data.py; chạy sáu test encoder | 878 train/246 validation, teacher thấy 41 card; tổng file chuẩn bị 3.027.539 byte (~2,89 MiB). Nguồn/seed/checksum trong normal_smoke_v1_20261116/training_manifest.json; kiểm tra tại Control/TrainingPreparationChecks/20261116_045126 |
| 2026-11-16 | Đo diagnostic hành vi Energy Venom, kiểm môi trường Python/PyTorch | teacher_coverage_notes.json: 102 cơ hội kích hoạt/0 lần chọn. Chưa cài torch, chưa viết/chạy trainer, chưa train/upload hoặc sửa luật/Core; chờ chọn môi trường học |
| 2026-11-16 | Người dùng yêu cầu bắt đầu Hard với khác biệt thuật toán, mục tiêu thắng Normal >75%, báo trước quyết định quan trọng/động tới nền game | Kiểm API có mục tiêu: GameSession.TryApply dùng trạng thái hiện tại, GameState chưa có clone/fork; chưa sửa Core hoặc triển khai Hard khi chưa có đồng ý cho phần nền cần thiết |

## 13. Thiết kế Hard đã được duyệt; yêu cầu bảo toàn luật

- Đã được người dùng duyệt: ISMCTS có ngân sách và rollout hướng dẫn bằng Normal; bổ sung API copy/fork an toàn trong Core. Không đổi GameRules, chi phí, chỉ số card, cân bằng hoặc deck chơi.
- Khác biệt thực: Easy chấm điểm ưu tiên; Normal đánh giá heuristic và forecast tấn công cục bộ; Hard thăm dò cây chuỗi hành động rộng hơn, gồm hiệu ứng/Energy/tấn công/phản ứng/phase và kết quả giả lập. Không chỉ tăng depth của Normal.
- Bài ẩn/ngẫu nhiên: cây tìm kiếm chỉ được dùng thông tin hợp lệ của người chơi. Không dùng seed thật/thứ tự deck/bài ẩn thật làm lợi thế; cần bộ tạo giả thuyết nhất quán với observation/public history và prior được công bố. Không gọi tìm kiếm full-information là bot công bằng.
- Không hứa ISMCTS sẽ đạt 75% trước thực nghiệm. So sánh biến thể có/không search và chất lượng theo ngân sách; ghi cả hạn chế giả định thông tin ẩn.
- Điểm chặn đã xác nhận: IBot hiện chỉ nhận view/legal actions; GameState/GameSession chưa có fork an toàn cho mô phỏng nhánh. Không tự dùng reflection để viết private state hoặc sửa luật nhằm né giới hạn này.
- Phần xin đồng ý: bổ sung API copy/fork cho nhóm state (GameState, PlayerState, CardInstance, chain/pending/modifier nếu có state mutable) và SeededGameRandom. Chỉ sao chép/cô lập dữ liệu; không sửa xử lý luật, chi phí, chỉ số, cân bằng hay preset chơi. Phạm vi file/hàm chi tiết sẽ được ghi khi triển khai; nếu cần sửa hành vi luật hoặc mở rộng ngoài nhóm copy state thì dừng báo tiếp.
- Kiểm thử bắt buộc trước gắn Hard: hash source giữ nguyên sau fork/search; clone ban đầu tương đương; hành động/RNG/collection trên nhánh không đổi source; giữ liên kết card/effect/chain vào đúng bản sao; hành động không hợp lệ không trả phí; dữ liệu ẩn bị thay thế không làm policy đọc seed/bài thật.
- Cây phải dựa InteractionPlayerIndex, không giả định hai người luôn luân phiên theo độ sâu vì game có chain/phản ứng/bước tự động.
- Ưu tiên ngân sách node/iteration và RNG search riêng để tái lập; wall-clock chỉ làm giới hạn an toàn. Thử các mức nhỏ rồi đo trên Ryzen 5500U, không chốt cứng độ sâu/thời gian trước số đo clone/apply.
- Thứ tự sau đồng ý: API fork + test → sampler thông tin ẩn/public adapter → lõi search + HardBot → runner/headless smoke → đánh giá/tinh chỉnh → tích hợp UI. Không thêm nút Hard giả hoạt động khi chỉ fallback Normal.
- Đánh giá: đóng băng Easy/Normal làm baseline; seed tuning/test tách biệt; cùng thông tin, deck và ghế cân bằng; báo W/D/L, khoảng tin cậy, quyết định p95, node/rollout và fallback/error. Mốc >75% là mục tiêu kết quả tổng hợp đã định nghĩa, không hạ sức mạnh Normal để đạt.
- Giữ mô hình học máy riêng: Hard được xác minh đủ tốt mới làm giáo viên chính; pipeline nhỏ/kiểm định dữ liệu hiện có vẫn tái dùng được. Chưa cài PyTorch hoặc train trong phần Hard này.


## 14. Hard implementation checkpoint — 2026-11-16

- User approval: ISMCTS and safe state-copy support; report any required rule change before acting. No gameplay rule change was made. Do not update HANDOFF until requested.
- Core copy support: CardInstance.CopyForSearch, SeededGameRandom.CopyForSearch, PlayerState.CopyForSearch, ChainState.CopyForSearch, new GameState.Search.cs/SearchCloneContext. Deep-copy mutable lists/cards/chains/pending aliases/modifiers/usage counters/skips/retaliation marks and exact RNG; reuse immutable definitions/config. Exact fork verification: 1,780 checks PASS.
- Information-set boundary: explicit full public catalog; synthetic IDs, sampled opponent hand/set traps and hidden own draw order; independent policy RNG. Never derive a catalog/prior from the actual opponent deck or use the live seed/state hash. Quiet roots only; response/trigger roots explicitly use Normal fallback. Unsupported projections fail closed with diagnostics.
- v3 added initial OWN deck knowledge from the deck assigned to that bot by SimulationEngine. Subtract observed owned hand/field/graveyard cards, then resample remaining order. Never inspect hidden own order or opponent deck composition. Future card creation/transfer may require a richer belief model: fail closed rather than changing rules.
- HardBot.cs: bounded single-observer ISMCTS, availability UCT using InteractionPlayerIndex, automatic phases, real GameRules.ApplyAction, two-turn horizon, heuristic terminal/leaf values and Normal-guided rollouts. Generic action groups; no hardcoded playable card IDs. Default budget: 96 iterations, tree depth 6, rollout cap 64 interactive steps. Candidate pruning: top 12 heuristic groups plus action-type diversity. These approximations are limitations, not optimal-play claims.
- v4 experiment: pair root candidates with the Normal baseline in the SAME sampled worlds, equal complete rounds within the iteration cap; choose a deviation only above a conservative paired improvement heuristic. This heuristic is not a confidence interval. Record changedBaseline separately from searched/fallback decisions.
- NormalBot scoring is unchanged; it only implements IStateObservingBot and exposes an internal score accessor for search. SimulationEngine/SimulationRunner accept Hard, pass explicit full public catalog and OWN initial deck, and record profile/catalog hash/search counts/fallbacks/timing. UI Hard is not enabled yet.
- Editor menu: Card Game > Simulation > Verify Hard Bot Prototype. Tests: exact source hash isolation, legal original action objects, hidden identities/order/real RNG invariance, copy limits, known-own-deck multiset, inconsistent knowledge rejection, legal trajectories and terminal attack.
- Evidence root: Tools/Simulation/Control/HardChecks/20261116_133647/. Read-only public fixture reconstructed from SimulationResults/Runs/rules-v4/catalog-v4/ai-v2/20261116_043203_8736d4bc/run.json; exact serialization of all 41 definitions verified before matches. No card asset edits or Player rebuild yet; old matching runner remains intact.
- v2 smoke: full-catalog-pilot-v2/, 10/10 Finished, Hard 5/10, 50.09 s. Same seed 410001 for five same-deck pairs: seat 1 always starts and wins; too narrow to establish bot strength. Earlier v1 stopped after 8 matches with one TurnLimit; retain it as failed tuning evidence.
- v3 comparison: comparison-v3/, seeds 410002/410003, three playable presets, both Hard seats + Normal/Normal controls: 18/18 Finished, 0 rule rejections/unexpected fallbacks, Hard 3/12 (25%), 92.67 s. Verification: 590 checks PASS. More lookahead alone is not evidence of stronger play; do not call this Hard production-ready.
- v4 comparison completed: 18/18 Finished, Hard 7/12 (58.3%), no truncation/rule rejection/unexpected sampler fallback; p95 161.81 ms, max 253.49 ms over 733 Hard decisions; 544 searched, 42 response/trigger fallbacks, 185 deviations from Normal. Per preset: Starter Balance 0/4, Crimson 4/4, Aegis 3/4. Total match time 83.27 s; trace output about 49.40 MiB. analysis.json preserves metrics and compiled executable SHA-256. These are reused TUNING seeds, not held-out evaluation.
- No torch install, training, cloud access/upload, game balance changes, commit/push, or HANDOFF update. Process-memory CIM query was previously denied; do not retry that route. Mono Process.PeakWorkingSet64=0 is UNKNOWN, not zero RAM. Pilot output/time caps: 128 MiB/180 s; RAM is not reliably measured yet.

- v5 comparison completed: heuristic-ranked unvisited branches and actor-policy selection inside concealed opponent action groups (Sets/discards), using the same tuning seeds. 18/18 Finished; Hard 7/12 (58.3%); per preset Starter 3/4, Crimson 1/4, Aegis 3/4. p95 181.47 ms, max 352.33 ms, 822 Hard decisions, 610 searched, 55 explicit response/trigger fallbacks, 213 baseline deviations; 101.79 s match time and about 56.19 MiB traces. Verification 575 checks PASS. This corrects SEARCH MODEL choices; game-rule methods are unchanged. Keep all earlier evidence/executables; never select a per-deck algorithm solely to fit these few samples.

- Frozen v5 fresh DEVELOPMENT comparison completed: seed 410004, three playable same-deck pairs, both bot seats plus Normal/Normal controls; 21/21 Finished. Hard/Normal 1/6, Hard/Easy 4/6, Normal/Easy 6/6. Hard p95 157.86 ms, max 275.61 ms, 576 searched decisions, 48 explicit fallbacks, 159 deviations; 89.22 s and 57.85 MiB traces. Output: SimulationResults/Experiments/Hard/20261116_v5_levels_seed410004/analysis.json. Search quality does not generalize sufficiently; do not release this as a successful Hard or use as the main ML teacher. No algorithm changed during this batch; these are development results, not the final held-out thesis test.

- v6 one-turn ablation completed: 27/27 Finished (18 Hard/Normal + 9 Normal controls), Hard 6/18 (33.3%), no draws/truncations/rule rejections/unexpected sampler fallbacks. Per preset 0/6, 2/6, 4/6; p95 117.10 ms, max 295.02 ms; 732 searched, 74 response/trigger fallbacks, 190 baseline deviations. Match time 84.99 s; traces 69.26 MiB; verification 578 checks PASS. Lower latency did not solve strength. Seeds 410002-410004 are development/tuning only; 410101+ remain unused.

- CURRENT SOURCE: configurable v6, default turnHorizon restored to 2 (behavior equivalent to v5), 96 iterations/treeDepth 6/rollout cap 64. Final development executable compiles with 0 errors: HardDevelopmentCandidate.exe; no duplicate batch rerun after restoring the already tested setting. Keep the lower-performing one-turn executable/report as ablation evidence, not the selected default.
- Next scope: diagnose root decisions against the Normal baseline on frozen scenarios, improve tactical/leaf evaluation and uncertainty handling before broadening search budgets. Do not enable UI Hard or use it as the primary ML teacher yet. The >75% Normal target is NOT met. Do not fit/select algorithm versions per preset solely to obtain the target on these reused seeds.
- Results entry: SimulationResults/Experiments/Hard/index.json links every preserved tuning report/executable and the fresh multi-level comparison; results are NOT replay-verified training data. No historical source traces/decks were moved or overwritten. Hardware RAM remains unmeasured; no retry of the blocked CIM path. Batch time/output checks occur BETWEEN matches, not hard caps during a match.


## 15. Approved H0 fixed-deck baseline — 2026-11-16

- User approved ONLY the initial 3 batches x 40 matches before further tuning. Freeze current Hard as H0: prototype v6, 96 iterations/tree depth 6/rollout cap 64/paired-root sampling/two-turn horizon. No algorithm modification during this run.
- Opponent Easy; both seats use the exact 30-card Starter Balance v4 list. Batch 01 seeds 420001-420020; batch 02 420021-420040; batch 03 420041-420060. Each seed has two legs swapping Hard/Easy seats; 60 seed pairs, 120 attempts. Seeds 410101+ remain unused.
- Output: SimulationResults/Experiments/Hard/H0_StarterBalance_20261116_144210/. Each batch has matches.jsonl, matches.csv, protocol.json, batch-report.json and traces/. Root has frozen protocol/catalog, status/summary; _runner/ preserves the copied frozen runtime executable, standalone orchestration source/executable/compiler evidence, and source/binary hashes. Standalone Mono harness uses Unity 2022.3.62f3 libraries; it is not a Unity Player rebuild.
- Simulation limits fixed for this baseline: 200 turns/10,000 actions/60 s per match; between-match guards 900 s or 300 MiB per batch and 5 GiB total. Truncations retained separately, never counted as draws or silently removed. One sequential process; no PyTorch/training/install/upload.
- COMPLETED: 120/120 Finished, Hard 99 wins / Easy 21 wins (82.5%), zero draws/truncations/errors. Batch 01: 33/40 (82.5%); batch 02: 32/40 (80%); batch 03: 34/40 (85%). Wall time 610.79 s; stored output approximately 242.62 MiB before small analysis files. H0 remains unchanged; no tuning performed.
- Post-run: verify seed pairing/unchanged deck and algorithm headers/trace digests/selected-action indices, hash all traces, compare protected source hashes, aggregate counts/timing/by-seat/by-starting-player. These structural checks are NOT full rule replay verification. Do not auto-tune Hard after this baseline; user requested these initial batches first.

- H0 post-run validation PASS: all 60 seeds have both swapped-seat legs, identical starting-player schedule/deck lists and frozen algorithm profiles within pairs; 120 complete traces have matching result/action digests/state hashes and valid exported selected-action indices. Frozen binaries and every protected source hash are unchanged. Full trace SHA-256 manifest: data-manifest.json; aggregated matches.csv and detailed analysis.json are at the run root. This is structural/data consistency checking, not a separate rule replay.
- H0 diagnostics: 6,581 Hard decisions; 4,858 searched; 407 explicitly logged response/trigger root fallbacks (no other fallback reason); mean 64.47 ms, p95 155.78 ms, max 409.48 ms. Hard seat 0: 47/60; seat 1: 52/60. Hard starting: 49/60; Hard second: 50/60. Across seed pairs: 39 double wins, 21 split pairs, zero double losses.
- Next authorized checkpoint is review of this baseline. Preserve all 21 loss traces and matching swapped-seat wins for targeted action diagnosis. Do not infer >75% vs Normal from this Easy-only experiment, auto-change decks, enable UI Hard, or train an ML model from unverified exports.

- H0 decision review completed (all 120 traces, including 21 losses and swapped-seat wins); decision-review.json contains counts and trace examples. 19/21 losses leave Easy >=5000 LP (10 at full 6000). Flags: 111/665 allocation actions exceed attack requirement, involving 161 extra Energy; all 111 differ from Normal baseline, affecting 48 wins and 12 losses. These are candidate inefficiencies, not proof every extra allocation is wrong or causes a loss.
- Found 8 allocation->tribute sequences with no intervening attack (6 wins/2 losses), one temporary ATK buff->tribute sequence, 13 end-Battle choices with legal attacks (not automatically mistakes), one leave-Main-empty choice with legal summons. Concrete example: seed420050 leg1 turn22 actions230/231, allocate3 to Aegis Sentinel then immediately tribute it for Thorn Bastion; allocated Energy clears on graveyard entry. No full counterfactual outcomes tested.
- Discuss H1 next: improve marginal Energy utility and consistency between allocation/buffs/tribute plans entirely inside Hard. Preserve effect-based exceptions (extra energy can fuel effects); do not impose a game-rule ban or modify Normal guidance code. Evaluate H1 against the same 60 H0 paired development seeds before using fresh validation seeds. Current evidence suffices for this focused improvement; no extra deck/batch or Hard change performed during review.


## 16. Approved H1 tactics and paired comparison — 2026-11-16

- User authorized defense-first posture, first global turn Defense, Defense when summon leaves insufficient attack Energy, over-half-current-HP team trade only with known affordable Basic reinforcements, profitable attacks, and preserving useful/support/high-stat tributes unless at risk of visible one-hit KO. Full game rules, Easy and Normal remain fixed.
- Only bot files changed: HardBot.cs integration plus new HardTacticalPolicy.cs. H1 uses public/owned card projections and visible monster flags, public retaliation marks, current HP and current field modifiers. No hidden opponent hand/deck/seed access. Added first-turn/energy/posture/trade/attack filters over supplied legal actions, tribute utility scores, per-turn Energy investment tracking and effect-fuel exceptions. Forecasts are heuristic, not guarantees of the next turn.
- Consistency: H1's own simulated tree/rollout choices use the same tactical policy; sampled opponent choices still use the original Normal guide. Copy public per-turn investment intent into each simulation, never share mutations back to live state. Search budgets unchanged: 96 iterations, depth6, rollout64, horizon2.
- Verification PASS: 571 isolation/privacy/legal trajectory checks plus 14 targeted tactical assertions (first-turn/insufficient-Energy defense, wall posture, direct attack, no surplus without fuel, no invest-then-tribute, preserve aura, half-HP reinforcement trade, fatal reflection, Legendary Fighter fuel exception). Reflection is evaluated before lifesteal as in the real rules.
- H1 frozen output: SimulationResults/Experiments/Hard/H1_StarterBalance_20261116_155129/. Same batch layout, exact Starter Balance v4 list, seeds420001-420060 with swapped legs and limits as H0. _runner contains frozen H1Runtime.exe, source snapshots of both changed bot files, test/compiler logs, and source/binary hashes. All non-Hard source hashes were identical to H0 before launching.
- COMPLETED: 120/120 Finished, no errors/truncations/draws. H1 wins 30/40, 28/40, 31/40 = 89/120 (74.17%), versus H0 33/40,32/40,34/40 = 99/120 (82.5%). Matched outcomes: 87 both-win, 19 both-loss, 12 H0-win/H1-loss, 2 H0-loss/H1-win. Runtime 660.80 s. Same reused development seeds, not held-out validation. No changes during the frozen batch.

- H1 behavior checks: 182 global-turn1 summons, all Defense; 115 summons with insufficient post-summon attack Energy, all Defense. Surplus allocations 111->0, allocate-then-tribute without attack 8->0; temporary ATK buff-then-tribute remains 1. Leaving Main empty with a legal summon 1->0. End-Battle with a legal Attack 13->15 (not automatically errors: zero damage/reflection/unsafe exchanges can justify skipping). H1 summons: 613 Basic,187 Advanced,98 Supreme. Mean match length 13.04->13.46 turns; decision p95 155.78->189.96 ms, max410.66 ms.
- Frozen H1 binaries and source hashes unchanged during matches. All non-Hard source files identical to H0. Original H0 evidence preserved. No automatic rollback/promotion: H1 remains the requested candidate in source, H0 remains the stronger incumbent benchmark. Further changes require discussion; no second unrequested tuning round performed.
- IMPORTANT diagnostic compatibility finding: schema1 SimulationStateHash uses Type.FullName, which includes executable assembly names inside closed generic types. H0 assembly HardDevelopmentCandidate and H1Runtime therefore have different raw state hashes even for the same state. No Core/hash change made. External read-only audit reconstructed all 60 initial states with EACH exact frozen executable, verified original raw hashes against all 240 trace headers, normalized ONLY generic-type assembly-name tokens (and token lengths), then compared the full private graphs. All 60 equal; all 120 paired initial conditions validated. Details: initial-state-audit.json and _runner/InitialAudit.cs. This canonical comparison is diagnostic only, not policy input or a change to replay verification.
- Interpretation: the combined tactical package reduced obvious Energy waste but did not improve win rate on this protocol. Cannot attribute regressions to a single preference from a combined test. Next discussion should consider isolating Energy/tribute consistency from posture/risk preferences, and revisiting hard exclusions versus soft priorities. Do not change game rules or Normal to compensate, select results by deck/seed, or call this trained ML.

- Subsequent user-requested READ-ONLY regression review completed: all12 H0-win/H1-loss matched trace pairs. regression-cases.json includes labeled sequences/states and per-case findings; regression-patterns.json has counts; regression-analysis.json has ranked implementation issues and uncertainty. No bot changes or new matches performed.
- Concrete issues: H1 can skip Battle from Main after allocating Energy/changing to Attack (420002), or enter Battle without affordable preparation (Sky Sage cases420021/420047). The profitable-attack gate checks existing legal Attack actions, which normally do not exist in Main. Summon stance filtering evaluates current board plus ONE summon, excluding joint plans with other hand cards; in420038 H0 deals1050 to Siegebreaker1700, H1 locks three Basics in Defense and deals0. Normal-derived activation values can still choose a temporary ATK buff when H1 later refuses to attack (420008).
- Attack-order evidence420048: Guardian Shift protects whichever monster is attacked first. H0 uses Battle Herald first then Thorn Bastion gets a KO/500LP; H1 uses Thorn first and misses that KO. Do not expose hidden Trap identity to fix this. Some first differences are mere action-order changes; five cases still need more targeted causal isolation. Observed patterns do not prove how many full-match losses a fix will reverse. Discuss plan consistency/joint preparation before adding more defensive rules; keep Core/Easy/Normal fixed.


## 17. H2 prepared; long batches MUST be user-triggered — 2026-11-16

- NEW PERSISTENT USER WORKFLOW: assistant improves Hard, performs small targeted verification, PREPARES a frozen batch package, then STOPS. User launches long seed batches through Unity or PowerShell, reports completion, then assistant reads/analyzes. Do not launch the many-match runner automatically in future turns. This turn chose PowerShell, avoiding an Editor bridge/build dependency.
- H2 only changes HardBot.cs and HardTacticalPolicy.cs, plus new orchestration Tools/Simulation/Run-HardBatch.ps1 and result-package files. Core/rule/Easy/Normal sources unchanged against H0 hashes. No card/assets/Editor/packages/account/cloud/training changes. H0 and H1 results preserved.
- H2 planning changes: block direct Main->End when a useful ready attack exists; finish affordable allocation/stance preparation before leaving Main; maintain a shared per-turn attack Energy commitment, release it on completed attack/defense/removed card and transfer unpaid commitments across tribute upgrades. Respect actual printed costs, including LP-deficit discounts; SetTrap itself has no Energy cost. No new game-rule restriction is introduced.
- Keep first-turn Defense, insufficient-Energy summon Defense and effect-fuel exceptions. One-hit/half-HP risk now supplies a soft preference rather than removing all Attack summons before joint plans can be searched. In Battle, finish favorable exchanges rather than reapplying a moving half-HP threshold mid-wave. Ignore unusable temporary ATK-only buffs; preserve Normal tie preference among equal tactical scores; removed H1 empty-board+600 summon bias. Existing anti-invest-then-tribute/support preservation remains. This is heuristic plan consistency, not an optimal full-turn planner or proof of increased strength.
- Search budgets unchanged: 96 iterations, tree depth6, rollout cap64, two-turn horizon. New optional read-only HardBot.DecisionDiagnostics event records actual maxTreeDepth reached per decision and aggregate rolloutActions across all trials. No subscriber => no event record allocation. Frozen runner writes a separate diagnostics JSONL per match with match/decision ordinals; maxTreeDepth is NOT a claim every branch reaches that depth.
- Verification: final _runner/checks.log has privacy/isolation/legal trajectory PASS plus retained H1 tactical cases and10 new H2 cases (Main skip, Sky Sage funding->stance, unusable buff, shared budget, soft risky summon branch, attack tie handling, unpaid commitment transfer). Only quick checks executed; no 120-match batch launched. Script AST and -ValidateOnly PASS.
- Prepared result folder: SimulationResults/Experiments/Hard/H2_StarterBalance_20261116_181012/. status.json is prepared, completed0/planned120. manifest.json/expected-profile.json/public-catalog.json preserve protocol; package.json hashes binaries/payload; _runner/source snapshots both changed Hard files and includes tests/compile logs/frozen H2Runtime.exe/HardBatch.exe.
- Exact comparison protocol: Starter Balance v4 both seats; batches1/2/3 use seeds420001-420020/420021-420040/420041-420060; two swapped-bot legs per seed, 40 matches per batch. Same limits as H0/H1 (200 turns/10000 actions/60s per match, between-match900s/300MiB per batch and5GiB total); sequential native Mono uses Unity2022.3.62f3 libraries. No Unity Player rebuild needed for this prepared standalone run.
- User command from project root: .\Tools\Simulation\Run-HardBatch.ps1 (all3 batches); optional -Batch 1 for a single batch, -ValidateOnly to check package without running. Default pointer: Tools/Simulation/Control/prepared-hard-batch.txt. Frozen package uses its copied binaries regardless of later Unity source edits.
- Runner safeguards: exclusive run lock, frozen binary/profile/catalog validation, SHA markers for each completed result/trace/diagnostics file. Rerun skips verified completed matches; partial outputs are moved into that batch attempts/ before retry, never deleted. Truncations/errors retained; rule/bot errors stop the run. A time guard can be resumed; a storage guard needs review before continuing. Per-batch results/traces/diagnostics/completed folders; PowerShell aggregates matches.csv/matches.jsonl/batch-report.json and root summary.json. It neither tunes the bot nor triggers another experiment.
- After user reports completion: read status/summary and compare H0/H1/H2 matched seeds. Audit raw state-hash portability as before (generic FullName contains assembly name; H2Runtime differs from H0/H1), do not modify the hash foundation. Review behavioral regressions and actual search-depth diagnostics; do not claim H2 stronger or training-ready before results/replay validation.


## 18. User-completed H2 analysis — 2026-11-16

- User ran the prepared H2 package. Assistant only analyzed and performed a quick read-only initial-state reconstruction audit, no new match batch or bot edit. Output: SimulationResults/Experiments/Hard/H2_StarterBalance_20261116_181012/analysis.json. 120/120 Finished, wins101/losses19, no draws/truncations/errors. Batches35/40,32/40,34/40 =>84.17%; H0=99/120 (82.5%), H1=89/120 (74.17%).
- Integrity PASS: original frozen payload hashes, all120 per-match result/trace/diagnostic completion markers, seed/seat/deck/algorithm headers and selected legal-action indices. Non-Hard source files remain unchanged. All60 frozen initial private state graphs match H0 after normalizing ONLY assembly-name components of generic FullName tokens and lengths;240 H0/H2 original trace headers verified. initial-state-audit.json; no hash/Core modifications or full match replay claimed.
- Matched H1->H2:13 losses become wins,1 win becomes loss. H0->H2:7 losses become wins,5 wins become losses. H2 recovers8 of the12 H1 regressions. Remaining H0-win/H2-loss seeds/legs:420033/1 (new versus H1 too),420038/1,420048/1,420057/0,420059/1. Reused tuning seeds; do not call +2 wins vs H0 conclusive general improvement.
- Behavior: surplus allocations0, allocation->tribute without attack0, ready-attacker Main skip0. All182 first-global-turn summons Defense; all102 insufficient-post-summon-Energy summons Defense.10 Battle endings retain legal Attack choices (may be justified).5 temporary buffs have no later attack by their target, all occur in matches H2 wins;1 buff->tribute remains. remaining-buff-cases.json gives sequences. Do not treat all five as causes of the19 losses.
- Concrete plan improvement420038/leg1/turn2: H1 dealt0 to Siegebreaker1700; H2 coordinates two Bloodfang attacks for1300, leaves400HP (H0 left650). H2 still loses this match eventually, so early tactical improvement does not establish a winning full-game plan.420059/leg1 now prepares Fire Dragon and damages Sky Sage2500->1500 on turn2, unlike H1, but also still loses later.
- Measured search:6343 Hard decisions,4746 actually searched; peak tree-depth histogram among searched decisions:1=84,2=205,3=749,4=781,5=940,6=1987. Mean per-decision PEAK4.74;41.87% reach the configured depth6. This is not every-branch depth, six turns, or comparable directly to Normal's five-attack forecast. Aggregate rolloutActions per searched decision averages1008 across all trials, not depth1008.349 explicit response/trigger Normal fallbacks, no other reasons.
- Timing: mean84.65ms, p95214.76ms, max600.03ms over Hard decisions; sum of match durations712.41s. Hardware/Unity/background load may differ from assistant-run H0/H1; timings descriptive, not a controlled performance benchmark. Mean match length12.28 turns (H0=13.04,H1=13.46). Seat0 wins50/60,seat1 wins51/60; Hard first50/60,second51/60.
- Recommended discussion: inspect the5 H0 regressions (especially sole newly regressed420033/1), then choose whether to isolate another improvement or freeze H2 for an unseen-seed validation batch on the SAME deck. No new experiment prepared/launched automatically. All long batches remain USER-TRIGGERED. No demonstrated >=75% vs Normal, unseen-deck strength or ML teacher readiness yet.

- User-requested five-regression READ-ONLY review completed. H0-H2-five-regressions.json has full turn-by-turn named actions, public boards/LP/hands and per-case reviews; five-regressions-analysis.json ranks findings. No bot edits or new matches.
- CONFIRMED HARD FORECAST BUG: HardTacticalPolicy.Uses(Hand) calls CardExpansion.PassiveAmount, but the live helper correctly returns null unless the card is alive/face-up in MonsterZone. This misses Logistics Squire CannotAttackOnSummonTurn before summon; H2 chooses Attack in420048/1 turn3 despite that restriction. Potentially also affects hand-candidate PierceDefense/Lifesteal forecasts. Fix inside Hard only with public-definition/isolated prospective-field projection; never change the live-rule helper. H2 already fixed the earlier H1 attack order in this case, so do not blame Guardian Shift order again.
- Strategic observations:420033/1 and420057/0 H0 develops Sky Sage2500HP/draw engine while H2 chooses Siegebreaker1700HP and later runs dry.420057 spends Wisp early and loses the future two-tribute package.420038/1 H2 early damage is better than H0, but later loses the more durable lineup and pauses offense.420059/1 early attack is fixed; late empty board plus unplayable high-rank/spell hand is the unresolved issue. Do not infer knowledge of future draws or assert a sole cause from action order alone.
- Suggested discussion: isolate H3a pre-summon passive forecasting (plus small Squire/Pierce/Lifesteal tests), keep all weights/budgets fixed and PREPARE the same120 seeds for USER launch only. Study playable-hand/recurring-draw/survival/future-tribute value separately. Current horizon+2 stops at the next own Main boundary rather than simulating that Main. No per-seed exceptions, Core/Easy/Normal edits, new long batches, or HANDOFF update.

- H3 implemented at user request: Hard-only prospective field-passive projection fixes hand Squire/Pierce/Lifesteal; shared portfolio scoring at root and leaf considers affordable hand/board tribute packages, useful Basic reserves, high-rank deployment and survival/remaining-deck-discounted repeatable draw. Immediate legal net draw gets a modest bonus. No deeper turn horizon or per-seed/card-ID exceptions.
- H3 verification PASS: prototype578 checks/51 searched decisions/4601 rule applications; H1 tactical10 + extra4, H2 turn-plan10, H3 forecast/portfolio22 assertions. All80 H2 protected source hashes unchanged; rules/config/deck/catalog match H2.
- PREPARED, NOT RUN: SimulationResults/Experiments/Hard/H3_FiveRegressions_20261116_123457; exactly420033/1,420038/1,420048/1,420057/0,420059/1. USER runs Tools/Simulation/Run-HardBatch.ps1; launcher reads package batch counts, retaining old3x40 defaults. Baseline comparison saved; no development-seed matches executed by assistant. Selected-case recovery cannot establish overall win rate.
- H3 USER-run5 completed and inspected:3 recovered wins420033/1(11turns),420038/1(26),420057/0(20); still losses420048/1(14),420059/1(11).0 errors/truncations; frozen payload and all5 result/trace/diagnostic hashes PASS;80 protected source hashes unchanged. Analysis and named H0/H2/H3 timelines saved in H3 run root.
- H3 observed intended behaviors: Squire420048/1 correctly Defense on summon and funded/attacks next own turn, though match still lost; Sky draw engine in420033, staged2-Basic-to-Sky sequence within turn4 in420057. Remaining candidates: full-turn heal/stance/team-attack coordination420048 and survival/target sequencing420059. No unique causal attribution or overall strength claim from these selected5. No additional bot edits or matches after review.
- USER CORRECTION: next experiment is latest H3 versus Normal,3x40 matches with both seats per seed420001-420060, Starter Balance v4 both seats; not another selected5-case batch. Frozen H3Runtime.exe reused byte-for-byte, no bot/rule changes. Prepared SimulationResults/Experiments/Hard/H3_vs_Normal_StarterBalance_20261116_132958; USER launches Run-HardBatch.ps1. All80 protected source hashes unchanged; profile/catalog/deck/config same as H3.
- Launcher summary generalized opponent losses and reports Normal/Easy wins separately; old Easy-only condition would miscount Hard-seat1 losses against Normal.120 matches remain development/tuning evidence, not held-out proof of75% target. No new matches run by assistant.
- H3-vs-Normal USER120 completed:72W/48L=60%; batches23/40,27/40,22/40.60 paired seeds classified14 both wins,2 both losses(420047,420049),44 split. Hardseat0=32/60,seat1=40/60; first41/60,second31/60. Within split pairs first seat wins both legs27,second seat17. All120 finished,0 errors/truncations; frozen/completion hashes PASS;80 protected source hashes unchanged.
- User-requested standout review saved in pair-classification.json,standout-pair-review.json,analysis.json under H3_vs_Normal_StarterBalance_20261116_132958.420040: both bots atseat0 have0 legal summons turns2/4;seat1 first wins both at6000LP.420014:seat1 second wins both at6000LP.420041 Hard wins both5500/5000LP.420047 Normal early Sky/draw advantage;420049 leg0 Hard defensive lineup loses pressure,leg1 Normal draw engine persists. Suggest sustainable pressure/engine preservation over more unconditional rank bias. No further bot edits/matches;60% is development evidence, not held-out success or75% target.
- 2026-11-17: user authorized commit/push of the AI/bot checkpoint. Added Git-reviewable H0/H1/H2 results for all120 seed/seat cells, H3 selected-case outcomes, seven historical named-action cases and a Vietnamese Claude review prompt. Full raw data/runtimes remain local; no new matches or game/source edits for publishing.
