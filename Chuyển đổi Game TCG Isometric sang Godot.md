# **Báo Cáo Nghiên Cứu Kỹ Thuật Và Kiến Trúc Chuyển Đổi Dự Án TCG Card Shop Simulator Sang Godot 4.6 Tối Ưu Hóa Bởi Trí Tuệ Nhân Tạo**

Sự dịch chuyển một hệ thống mô phỏng quản lý cửa hàng thẻ bài phức tạp từ nền tảng công nghệ web (Phaser kết hợp Vue.js) sang một phần mềm phát triển trò chơi chuyên dụng (Godot 4.6) đòi hỏi một sự tái cấu trúc toàn diện về mặt tư duy thiết kế phần mềm. Dựa trên hình ảnh tham chiếu được cung cấp, dự án mục tiêu sở hữu một không gian đồ họa Isometric 2.5D chi tiết, kết hợp với giao diện người dùng (UI) đa tầng, hiển thị lượng lớn dữ liệu quản lý tài nguyên, nhân sự và không gian kiến trúc theo thời gian thực. Báo cáo này cung cấp một kế hoạch kiến trúc chuyên sâu, phân tích cấu trúc toán học của không gian Isometric, cơ chế chuyển đổi logic phản ứng (reactivity), và một lộ trình chỉ lệnh (prompt) chi tiết được thiết kế đặc biệt để điều phối các tác vụ lập trình thông qua trí tuệ nhân tạo (Cursor AI).

## **Tầm Nhìn Kiến Trúc Và Phân Tích Hình Ảnh Tham Chiếu**

Hình ảnh tham chiếu cung cấp một cái nhìn sâu sắc về mục tiêu đồ họa và cơ chế tương tác của dự án. Không gian trò chơi được xây dựng trên một lưới Isometric tiêu chuẩn, trong đó các đối tượng như bể cá (có thể được thay thế bằng tủ trưng bày thẻ bài TCG trong ngữ cảnh dự án), giá để hàng, và máy bán hàng tự động được đặt dọc theo các trục chéo. Các nhân vật di chuyển tự do nhưng rõ ràng tuân theo một hệ thống tìm đường ngầm định giữa các lối đi do đồ nội thất tạo ra. Giao diện người dùng được phân bổ rành mạch: thanh công cụ xây dựng nằm dọc bên trái, bảng chi tiết đối tượng (chứa thông số sinh tồn hoặc chỉ số thẻ bài) nổi ở góc trên bên phải, và thanh tài nguyên tổng thể (tiền tệ, điểm kinh nghiệm, thời gian trong ngày) cố định ở cạnh dưới màn hình.

Việc tái tạo chính xác trải nghiệm thị giác và tương tác này trong Godot 4.6 đòi hỏi sự tách biệt rõ ràng giữa logic mô phỏng thế giới (World Simulation) và logic hiển thị giao diện (UI Rendering). Trong Phaser, các yếu tố này thường chia sẻ chung một vòng lặp cập nhật hoặc phụ thuộc vào sự can thiệp của Vue.js để đồng bộ hóa.1 Tuy nhiên, Godot sử dụng một cây cấu trúc phân cấp (Node Tree), nơi không gian Isometric sẽ được quản lý bởi các nút Node2D bên dưới hệ thống phân giải chiều sâu, trong khi toàn bộ giao diện quản lý sẽ được tách biệt hoàn toàn trên một CanvasLayer sử dụng các nút Control để duy trì tỷ lệ hiển thị độc lập với camera của thế giới trò chơi.

## **Phương Pháp Luận Tích Hợp Trí Tuệ Nhân Tạo Lõi**

Việc sử dụng Cursor AI để chuyển đổi một cơ sở mã nguồn phức tạp tiềm ẩn rủi ro lớn về việc sinh ra mã lỗi (spaghetti code), phân mảnh logic, hoặc AI tự ý bỏ qua các tài liệu thiết kế cốt lõi. Để triệt tiêu rủi ro này, quá trình làm việc với Cursor phải tuân thủ nghiêm ngặt các phương pháp luận từ các dự án tối ưu hóa AI hàng đầu như Everything Claude Code (ECC) 2 và Claude Code Game Studios (CCGS).3

Hệ thống điều phối AI sẽ được xây dựng dựa trên triết lý cốt lõi: Cộng tác, không tự trị (Collaborative, Not Autonomous).4 Trí tuệ nhân tạo không được phép tự do viết toàn bộ hệ thống trong một lần chạy lệnh. Thay vào đó, AI phải hoạt động trong một hệ thống phân cấp studio ảo (Studio Hierarchy), đóng vai trò như các chuyên gia ở từng cấp độ khác nhau. Ở cấp độ Giám đốc Kỹ thuật (Tier 1), AI chịu trách nhiệm thiết lập các quy tắc toàn cục và kiến trúc bảo vệ tầm nhìn dự án.4 Ở cấp độ Trưởng nhóm (Tier 2), AI đảm nhận các miền logic cụ thể như Máy trạng thái thiết kế trò chơi (Game Design) hoặc Điều hướng mạng lưới. Cuối cùng, ở cấp độ Chuyên gia (Tier 3), AI thực hiện các tác vụ lập trình thực hành hẹp như viết Shader hoặc tinh chỉnh hoạt ảnh.4

Một khía cạnh quan trọng khác được áp dụng từ framework CCGS là các Quy tắc theo đường dẫn (Path-Scoped Rules).4 Hệ thống mã nguồn mới trong Godot sẽ được phân chia nghiêm ngặt. Khi AI làm việc trong thư mục src/gameplay/, nó bị buộc phải tập trung vào các giá trị định hướng dữ liệu và sử dụng tham số thời gian delta, tuyệt đối không được tham chiếu trực tiếp đến các nút giao diện.4 Tương tự, khi thao tác trong thư mục src/core/, mọi đoạn mã phải đảm bảo không có sự phân bổ bộ nhớ lãng phí trong các vòng lặp hoạt động mạnh (hot paths) để duy trì hiệu suất khung hình.4 Cơ chế Phát triển dựa trên kiểm chứng (Verification-Driven Development) cũng sẽ được lồng ghép vào mọi chỉ lệnh; AI phải đề xuất phương án, viết các kịch bản kiểm thử (test hooks), và chờ sự phê duyệt của con người trước khi can thiệp vào các tệp vật lý.4

## **Sự Dịch Chuyển Mô Hình: Từ Vue Reactivity Sang Godot Signals**

Trong dự án cũ, Vue.js đảm nhận trọng trách quản lý trạng thái toàn cục, cho phép các thành phần giao diện tự động cập nhật khi dữ liệu thay đổi thông qua cơ chế phản ứng hạt mịn (fine-grained reactivity) dựa trên JavaScript Proxies.5 Khi di chuyển sang Godot 4.6, việc duy trì tính chất "phản ứng" này là tối quan trọng để giao diện hiển thị tài nguyên, danh sách thẻ bài, và thông báo hệ thống hoạt động mượt mà mà không cần phải liên tục quét dữ liệu trong hàm \_process() mỗi khung hình, vốn là nguyên nhân hàng đầu gây sụt giảm hiệu năng.7

Godot cung cấp một giải pháp bản địa mạnh mẽ để thay thế mô hình của Vue: sự kết hợp giữa các lớp Resource và hệ thống Signal.6 Thay vì sử dụng Pinia hay Vuex, dữ liệu của trò chơi sẽ được đóng gói vào các tài nguyên tùy chỉnh (Custom Resources). Tính chất đặc biệt của Resource trong Godot là khả năng được lưu trữ trong bộ nhớ và chia sẻ tham chiếu giữa vô số nút khác nhau; bất kỳ thay đổi nào trên một thể hiện của tài nguyên đều lập tức được phản ánh trên toàn hệ thống.6

Để tạo ra một kho lưu trữ phản ứng (Reactive Store), hệ thống sẽ định nghĩa các biến trạng thái kèm theo các hàm thiết lập (setter functions) tùy chỉnh. Bất cứ khi nào một biến như số lượng tiền mặt (cash) bị thay đổi thông qua setter, lớp dữ liệu này sẽ tự động phát ra một tín hiệu (emit\_signal) mang theo giá trị mới. Các thành phần giao diện người dùng, chẳng hạn như nhãn văn bản hiển thị tiền tệ ở góc dưới màn hình, chỉ cần thực hiện một thao tác kết nối duy nhất với tín hiệu này trong quá trình khởi tạo (hàm \_ready()). Khi tín hiệu được phát đi, giao diện sẽ tự động đánh giá và kết xuất lại bản thân nó, sao chép hoàn hảo vòng đời phản ứng của các framework web hiện đại, nhưng với chi phí điện toán gần như bằng không do tận dụng kiến trúc định hướng sự kiện (Event-driven architecture) nội tại của C++ bên dưới Godot.5

## **Phân Tích Chuyên Sâu Về Kiến Trúc Đồ Họa Isometric 2.5D**

Không gian Isometric trong đồ họa pixel art không phải là một mô phỏng 3D thực sự, mà là một thủ thuật chiếu phối cảnh toán học nhằm tạo ra ảo giác về chiều sâu trên một mặt phẳng hai chiều. Dựa trên hình ảnh tham chiếu, toàn bộ cấu trúc sàn nhà, tường, và hướng nhìn của đồ vật đều tuân theo một quy chuẩn hình học cực kỳ nghiêm ngặt nhằm tránh hiện tượng rách hình hoặc sai lệch phối cảnh.

### **Tính Toán Toán Học Và Tỷ Lệ Chuẩn 2:1**

Góc chiếu Isometric lý thuyết theo hình học không gian là 30 độ. Tuy nhiên, trong môi trường nghệ thuật pixel (pixel art) và kết xuất 2D, việc sử dụng các đường chéo 30 độ thực sự sẽ tạo ra các đường viền răng cưa không đồng đều, phá hỏng tính thẩm mỹ của trò chơi. Do đó, tiêu chuẩn công nghiệp được áp dụng thay thế là tỷ lệ 2:1; nghĩa là cứ mỗi hai pixel di chuyển theo trục ngang, đường chéo sẽ dịch chuyển một pixel theo trục dọc.9 Quy tắc này tạo ra các hình thoi nền tảng hoàn hảo, định hình toàn bộ lưới tọa độ của trò chơi.

Godot 4.6 đã loại bỏ phương thức tiếp cận nút TileMap nguyên khối cũ, thay vào đó yêu cầu sử dụng nhiều nút TileMapLayer độc lập để tăng cường tính linh hoạt và tối ưu hóa việc phân nhóm hiển thị (batching updates).10 Quá trình nội suy từ tọa độ lưới ảo sang tọa độ điểm ảnh thực tế trên màn hình được thực hiện thông qua công thức biến đổi ma trận cốt lõi: Trục X trên màn hình bằng hiệu số của trục X và trục Y trên lưới nhân với một nửa chiều rộng của ô gạch, trong khi trục Y trên màn hình bằng tổng của hai trục lưới nhân với một nửa chiều cao của ô gạch.9 Hệ thống hàm map\_to\_local và local\_to\_map được tích hợp sẵn trong TileMapLayer sẽ tự động hóa quá trình tính toán phức tạp này, hỗ trợ trực tiếp cho hệ thống thao tác chuột khi xây dựng cửa hàng.11

### **Giải Quyết Bài Toán Chiều Sâu Bằng Y-Sorting**

Thách thức lớn nhất trong một không gian 2.5D dày đặc như hình ảnh tham chiếu là việc xác định đối tượng nào che khuất đối tượng nào. Khi một khách hàng bước ra phía sau một kệ trưng bày cao, phần thân dưới của họ phải bị che khuất, trong khi phần thân trên có thể vẫn hiển thị nếu kệ không đủ cao. Godot giải quyết vấn đề này thông qua thuộc tính phân loại theo trục Y (YSortEnabled).1

Khi tính năng này được kích hoạt trên một nhánh của cây nút, engine sẽ liên tục so sánh tọa độ Y cục bộ của tất cả các đối tượng con. Đối tượng có giá trị Y lớn hơn (nằm thấp hơn trên màn hình) sẽ được vẽ sau cùng, đè lên các đối tượng có giá trị Y nhỏ hơn (nằm cao hơn trên màn hình). Để hệ thống này hoạt động chính xác tuyệt đối, điểm gốc (Pivot/Origin) của mọi hình ảnh đồ họa (Sprite) bắt buộc phải được dịch chuyển từ vị trí trung tâm mặc định xuống vị trí trung tâm đáy, tức là ngay dưới bàn chân của nhân vật hoặc phần tiếp xúc mặt đất của đồ vật.9 Việc đặt sai điểm neo dù chỉ một vài pixel sẽ phá vỡ toàn bộ ảo giác không gian, khiến nhân vật trông như đang lơ lửng hoặc xuyên thấu qua các bức tường vững chắc.

## **Tiêu Chuẩn Đặc Tả Tài Nguyên Đồ Họa (Asset Specifications)**

Để hệ thống xử lý hình ảnh và engine hoạt động trơn tru theo logic toán học đã đề cập, cũng như tạo điều kiện thuận lợi cho quá trình người dùng tự tìm kiếm hoặc yêu cầu AI tạo mẫu đồ họa, toàn bộ tài nguyên (assets) phải tuân thủ nghiêm ngặt các tiêu chuẩn kỹ thuật số được hệ thống hóa trong bảng dưới đây:

| Hạng Mục Tài Nguyên | Khung Kích Thước Khuyến Nghị (Pixels) | Cấu Trúc Khung Hình Hình Học | Thiết Lập Điểm Neo Chiều Sâu (Pivot/Origin) | Phân Tích Kỹ Thuật Đồ Họa Chuyên Sâu |
| :---- | :---- | :---- | :---- | :---- |
| **Hệ thống Gạch Nền (Floor Tiles)** | 64x32 (Độ phân giải trung bình) hoặc 128x64 (Độ phân giải cao) | Hình thoi Isometric thuần túy theo tỷ lệ 2:1. | Tâm điểm của hình thoi (Vị trí x: 32, y: 16 đối với gạch 64x32). | Các ô gạch nền hoàn toàn không có độ cao ảo (Z-axis height \= 0). Mép của các hình thoi phải tiếp xúc hoàn hảo ở mức độ pixel để tránh tạo ra các đường nứt (seams) khi lặp lại.9 |
| **Cấu trúc Tường và Vách Ngăn (Walls)** | 64x64 (Tùy thuộc vào độ dày của bức tường ảo) | Dựa trên nền tảng 64x32, phát triển khối hình nón lên phía trên để mô phỏng độ cao 32px ảo. | Trung tâm tại mặt phẳng đáy tiếp xúc với lưới sàn (Vị trí x: 32, y: 48 đối với khối 64x64). | Chiều cao của tường phải được vẽ đè lên nửa trên của khung hình. Ánh sáng chiếu vào các mảng tường cần tuân thủ một hướng chiếu sáng thống nhất toàn cục (thường là từ trên cùng bên trái).9 |
| **Nội Thất & Tủ Trưng Bày (Furniture/Tanks)** | Các biến thể linh hoạt, luôn là bội số của 64 ngang (ví dụ: 64x64, 128x128) | Khối đa giác Isometric mở rộng. Các tủ lớn phải bao phủ toàn bộ diện tích của hai hoặc bốn ô lưới kim cương ghép lại. | Trung tâm của mặt đáy thấp nhất của đồ vật, nơi tiếp xúc với gạch nền. | Trong hình ảnh tham chiếu, bể cá và giá hàng có lớp kính trong suốt. Đồ họa nên xuất ra dưới định dạng PNG bảo toàn kênh Alpha (kênh trong suốt) nguyên bản để engine xử lý lớp nền mờ hiệu quả. Hộp va chạm (Hitbox) vật lý sẽ được vẽ khớp theo hình thoi đáy. |
| **Nhân Vật Chuyển Động (Player/NPCs/Staff)** | 32x32 hoặc 48x48 cho khung viền nhân vật. | Hình chiếu xiên (Oblique) hoặc Isometric thực sự. Nhân vật cần có sprite sheet cho ít nhất 4 hoặc 8 hướng di chuyển (Tây Nam, Đông Nam, v.v.). | Điểm neo được đặt chính xác ở giữa hai gót chân của nhân vật.9 | Kích thước khung viền bao quanh nhân vật nên được cắt xén gọn gàng nhất có thể nhằm giảm thiểu tải trọng bộ nhớ và tối ưu hóa bán kính phát hiện va chạm tâm. Khung hình không bao gồm bóng đổ, bóng đổ sẽ được vẽ bởi một nút đệm bên dưới chân nhân vật. |
| **Thẻ Bài TCG & Gói Mở Rộng (Packs/Cards)** | 63x88 (Tỷ lệ tiêu chuẩn vật lý TCG) nhân với hệ số tỷ lệ hiển thị. | Phối cảnh phẳng hoặc 2.5D tĩnh. | Trọng tâm hình học (Center) của tấm thẻ. | Do thẻ bài sẽ được hiển thị chi tiết trong giao diện mở gói (Gacha UI) với hiệu ứng xé giấy (Tear Shader), chúng cần có độ phân giải cao và các tệp hình ảnh tách biệt giữa mặt trước, mặt sau và mặt giấy bạc bọc ngoài gói.14 |

Bằng cách tuân thủ đúng tỷ lệ 2:1 và các điểm neo tọa độ này, hệ thống TileMapLayer và công cụ định tuyến đường dẫn của Godot 4.6 có thể vận hành mượt mà mà không gặp các lỗi chồng chéo đồ họa cơ bản.

## **Động Lực Học Lối Chơi Và Hệ Thống Mô Phỏng Cốt Lõi**

Để tái hiện lại trải nghiệm kinh doanh thẻ bài hấp dẫn từ bản phân tả kỹ thuật cũ (SPEC) 1, dự án cần xây dựng một kiến trúc kết nối chặt chẽ giữa các hệ thống quản lý, chuỗi cung ứng vật lý, và trí tuệ nhân tạo của khách hàng. Mọi hành động của người chơi đều ảnh hưởng trực tiếp đến trạng thái thế giới và vòng lặp kinh tế.

### **Chuỗi Cung Ứng Và Tương Tác Vật Lý (Supply Chain)**

Quá trình nhập hàng bắt đầu bằng việc người chơi đặt mua các hộp thẻ thông qua giao diện danh mục trực tuyến. Khi giao dịch thành công, hệ thống không chỉ đơn thuần cập nhật một con số vô hình, mà sẽ khởi tạo các đối tượng hộp hàng vật lý rớt xuống từ bên ngoài cửa hàng. Đây là một điểm chạm quan trọng về cảm giác trò chơi (game feel). Các hộp hàng này được mô phỏng bằng nút RigidBody2D kết hợp với hệ thống dò tia (raycasting) để phát hiện điểm va chạm với mặt đất.1

Sự tinh tế trong kỹ thuật lập trình Godot ở đây là việc chuyển đổi trạng thái vật lý một cách linh hoạt. Việc duy trì tính toán vật lý liên tục cho một đối tượng 2D trôi nổi trên một bề mặt Isometric ảo có thể gây ra những xung đột ngoài ý muốn (ví dụ: hộp va đập vào người chơi và đẩy họ bay xuyên qua tường). Do đó, khi người chơi tương tác để "bưng" hộp hàng, đối tượng sẽ thay đổi trạng thái từ một vật thể bị chi phối bởi trọng lực sang trạng thái RigidBody2D.FREEZE\_MODE\_KINEMATIC. Ở trạng thái này, mọi tính toán va chạm vật lý tạm thời bị vô hiệu hóa, hộp hàng được chuyển gốc tọa độ (reparent) trở thành một đối tượng con của người chơi và di chuyển lơ lửng phía trên đầu nhân vật, đảm bảo quá trình di chuyển trong cửa hàng diễn ra trơn tru tuyệt đối. Chỉ khi người chơi chủ động vứt hộp đi, trạng thái vật lý mới được phục hồi.1

### **Hệ Thống Lưới Xây Dựng Cửa Hàng (Grid Construction)**

Mảnh ghép quan trọng tiếp theo là khả năng tùy biến cửa hàng. Giống như giao diện hiển thị trong ảnh tham chiếu, người chơi có thể chọn đồ nội thất từ thanh công cụ bên trái và kéo thả chúng vào không gian. Đồ nội thất được phân chia thành nhiều hạng mục với các vai trò logic khác nhau: kệ bán lẻ (dành cho khách hàng tương tác), kho lưu trữ (chỉ người chơi và nhân viên tiếp cận), và tủ trưng bày cao cấp (cho các thẻ hiếm đã được chấm điểm PSA).1

Khi người chơi ở chế độ xây dựng, một phiên bản bóng ma (ghost) của đồ vật sẽ bám theo con trỏ chuột. Phiên bản này sử dụng sự kết hợp của hàm local\_to\_map để chuyển đổi tọa độ chuột sang tọa độ ô lưới, sau đó lập tức dùng map\_to\_local để khóa (snap) bóng ma vào vị trí trung tâm của hình thoi Isometric tương ứng.15 Hệ thống phải sử dụng lõi không gian vật lý của Godot (Physics2DDirectSpaceState) để quét một cấu trúc hình học tương đương với diện tích đáy của đồ vật tại vị trí dự kiến. Nếu diện tích này chồng chéo với bức tường, người chơi, hoặc một món đồ khác, bóng ma sẽ chuyển sang màu đỏ báo hiệu không thể thao tác; ngược lại, màu xanh lá cây cho phép việc khởi tạo đối tượng thực.1

Một thiết kế thuật toán cốt lõi cho nội thất là hệ thống "Phân tầng" (Tiers). Mỗi kệ hàng không chỉ là một hình ảnh tĩnh, mà là một vùng chứa dữ liệu (data container) bao gồm nhiều tầng. Một thuật toán tự động lấp đầy sẽ quản lý dung lượng của từng tầng, kiểm soát việc một tầng có thể chứa tối đa 16 gói thẻ bài riêng lẻ hoặc 4 hộp lớn, ngăn chặn việc quá tải dữ liệu hoặc lỗi hiển thị đồ họa sai lệch.1

### **Trí Tuệ Nhân Tạo Hệ Thống Lưới (AStarGrid2D) Và Cỗ Máy Trạng Thái**

Để cửa hàng tràn đầy sức sống như trong ảnh tham chiếu, các nhân vật phi người chơi (NPC) bao gồm khách hàng và nhân viên cần có một hệ thống trí tuệ nhân tạo tinh vi. Không giống như các trò chơi hành động tự do, sự di chuyển trong một môi trường dày đặc chướng ngại vật hình thoi đòi hỏi một hệ thống tìm đường lưới tối ưu hóa. Bản đặc tả kỹ thuật chỉ ra việc sử dụng lưới 16px để điều hướng.1 Trong Godot 4.6, công nghệ này sẽ được nâng cấp tối đa thông qua lớp AStarGrid2D.18 Đây là một biến thể chuyên biệt có tốc độ truy xuất vượt trội so với AStar2D truyền thống, vì nó loại bỏ yêu cầu phải khởi tạo thủ công hàng ngàn điểm nút (nodes). Thuật toán tối ưu sẽ sử dụng phương pháp heuristic khoảng cách Manhattan để tính toán lộ trình ngắn nhất thông qua các ô vuông kề cạnh, mô phỏng chính xác cách con người lách qua các dãy kệ hàng.1 Đáng chú ý, mỗi khi người chơi đặt một món đồ nội thất mới xuống mặt sàn, hệ thống xây dựng sẽ ngay lập tức gửi tín hiệu đến GridManager để cập nhật tọa độ tương ứng thành vùng không thể đi qua (solid cell), buộc tất cả NPC đang di chuyển phải tính toán lại đường đi trong chớp mắt mà không làm gián đoạn trò chơi.18

Linh hồn của trí thông minh NPC nằm ở Cỗ máy trạng thái hữu hạn (FSM). Khách hàng được sinh ra ngẫu nhiên với một bộ định tuyến ý định dựa trên xác suất: 70% đến để mua hàng, 10% mang thẻ cũ đến bán lại, và 20% tìm kiếm bàn chơi để tham gia thi đấu.1 Tùy thuộc vào ý định này, cỗ máy FSM sẽ chuyển tiếp qua các trạng thái như lang thang, tìm kiếm vật phẩm cụ thể, hướng đến quầy thanh toán, hoặc rời đi khi thất vọng.

Cơ chế tính giá và quyết định mua hàng là một vòng lặp logic toán học tinh tế. Người chơi có quyền tự do thiết lập giá bán cho từng mặt hàng. Khi khách hàng tiếp cận một kệ hàng, một thuật toán định giá ngầm sẽ kích hoạt. Mức giá cuối cùng mà NPC đánh giá bằng giá do người chơi thiết lập nhân với hệ số điều chỉnh sự kiện của ngày hôm đó (ví dụ: ngày lễ TCG sẽ tăng hệ số chấp nhận giá). Ngưỡng chấp nhận mua của NPC được giới hạn ngẫu nhiên nằm trong khoảng từ 0.1 lần đến 5.0 lần giá trị thị trường chuẩn của tấm thẻ.1 Nếu mức giá người chơi đặt nằm trong ngưỡng chịu đựng tâm lý của NPC, giao dịch ảo được ghi nhận, món hàng bị trừ khỏi kệ, và NPC mang món hàng đó tiến về phía quầy thu ngân để hoàn tất quá trình thanh toán, kích hoạt hệ thống phản ứng dữ liệu (Reactive Store) để cộng tiền mặt vào tài khoản người chơi.

## **Cơ Chế Gacha Mở Gói Thẻ Và Siêu Trò Chơi (Meta-Game)**

Chiều sâu hấp dẫn nhất của một tựa game mô phỏng cửa hàng thẻ bài không nằm ở việc tính tiền, mà nằm ở quá trình thu thập và đầu cơ tài sản ảo. Các cơ chế này yêu cầu việc xử lý số liệu ngẫu nhiên phức tạp và hiệu ứng kết xuất đồ họa cấp thấp.

### **Thuật Toán Gacha Và Đồ Họa Cấp Thấp Bằng Shader (Tear Effect)**

Khi người chơi tự mình bóc một gói thẻ thay vì bán chúng, trò chơi chuyển sang một giao diện tập trung cao độ để tạo ra "kịch tính mở gói" (Pack Opening Drama).1 Mỗi gói chứa đúng 6 thẻ bài được trích xuất ngẫu nhiên từ cơ sở dữ liệu của một bộ sưu tập nguồn.1 Thuật toán Gacha sẽ thực hiện các vòng xoay xác suất có trọng số để định hình cấu trúc gói, đảm bảo tỷ lệ xuất hiện của các thẻ thông thường (Common) sẽ áp đảo các thẻ siêu hiếm (Ghost Rare \- Rank 10). Mảng 6 thẻ này ngay lập tức được sắp xếp ngẫu nhiên theo một điều kiện tiên quyết: tấm thẻ có giá trị độ hiếm cao nhất phải luôn được đặt ở vị trí cuối cùng, tối đa hóa sự hồi hộp của người chơi qua từng cú nhấp chuột.1

Để hiện thực hóa cảm giác chân thực khi xé vỏ bọc nilong hoặc giấy bạc của gói thẻ, việc sử dụng các hoạt ảnh khung hình (frame-by-frame animation) thông thường là quá tốn kém tài nguyên và thiếu tự nhiên. Godot 4.6 cung cấp một giải pháp nghệ thuật kỹ thuật mạnh mẽ thông qua ngôn ngữ Godot Shader (GLSL).19 Bằng cách viết một kịch bản đồ họa cấp thấp áp dụng lên chất liệu (ShaderMaterial) của gói thẻ, hệ thống sẽ sử dụng một kết cấu nhiễu (noise texture) làm mặt nạ che giấu (mask). Kết hợp với một nút Tween, chương trình sẽ nội suy một biến đầu vào (uniform) tên là cut\_progress từ giá trị 0.0 lên 1.0. Quá trình này sẽ từ từ triệt tiêu kênh alpha (độ mờ) dọc theo các cạnh lởm chởm do mẫu nhiễu sinh ra, tạo ra ảo giác hoàn hảo của một mảnh giấy đang bị xé toạc hoặc bốc cháy, để lộ các mặt thẻ đang úp ngược bên dưới.19

### **Đánh Giá Chất Lượng PSA Và Toán Học Thi Đấu TCG**

Nhằm gia tăng tính kinh tế vĩ mô, dự án bao gồm một hệ thống gửi thẻ đi kiểm định chất lượng (PSA Grading).1 Đây là một module logic tính toán thuần túy. Khi người chơi kích hoạt dịch vụ, một xúc xắc xác suất có trọng số ẩn sẽ lăn, tạo ra một kết quả đánh giá phân loại từ 1 đến 10\. Điểm số này định đoạt sinh mệnh kinh tế của tấm thẻ; một tấm thẻ đạt điểm tuyệt đối (PSA 10\) sẽ áp dụng hệ số nhân khổng lồ x20.0 lên giá trị gốc, biến nó thành một báu vật thực sự, trong khi một tấm thẻ bị chấm điểm tệ hại (PSA 1 \- Poor) sẽ gánh chịu hình phạt làm giảm giá trị xuống chỉ còn 0.3x, thấp hơn cả lúc chưa kiểm định.1 Quá trình này không trả kết quả ngay lập tức mà yêu cầu thẻ phải được đóng gói vào các phiến nhựa cứng (Slabs) và gửi trả lại cửa hàng thông qua hệ thống giao hàng trong các ngày trong game tiếp theo.

Đối với những khách hàng có ý định chơi game (Play intent), cửa hàng cung cấp các bàn đấu. Hệ thống chiến đấu mô phỏng này lấy cảm hứng trực tiếp từ cơ chế Pokémon TCG truyền thống.1 Để tránh việc biến trò chơi thành một ứng dụng đánh bài rườm rà, động cơ chiến đấu (Battle Engine) giải quyết các giao tranh thông qua tính toán số liệu hậu nền. Công thức sát thương áp dụng quy tắc cơ sở: Sát thương thực tế bằng sát thương gốc nhân với hệ số điểm yếu (luôn cố định ở mức x2.0) trừ đi giá trị kháng cự tuyến tính (giảm trừ thẳng 30 điểm sát thương).1 Ở chế độ phức tạp hơn (Advanced mode), động cơ này tích hợp một chức năng kiểm tra năng lượng nghiêm ngặt, rà soát lượng năng lượng màu (specific type) và năng lượng vô sắc (colorless) đính kèm trên tấm thẻ trước khi cho phép đòn tấn công được phát động, mô phỏng hoàn chỉnh độ khó chiến thuật của một giải đấu thẻ bài thực thụ.1

## **Lộ Trình Triển Khai: Hệ Thống Chỉ Lệnh (Prompt) Cho AI Cursor**

Để đảm bảo trí tuệ nhân tạo (Cursor AI) thực thi chính xác các ý tưởng thiết kế phức tạp và sự dịch chuyển từ nền tảng web sang engine Godot 4.6 mà không sinh ra mã độc hại hay mã kém chất lượng, một hệ thống chỉ lệnh (prompt) được thiết kế tỉ mỉ là bắt buộc. Hệ thống này được chia thành 10 giai đoạn riêng biệt. Mỗi khi hoàn thành một giai đoạn, người lập trình phải trực tiếp khởi chạy Godot để Playtest, đảm bảo không có bất kỳ lỗi (bug) nào phát sinh trước khi cấp lệnh tiếp theo.

*(Lưu ý: Các prompt dưới đây được cung cấp bằng tiếng Anh kỹ thuật để tối ưu hóa khả năng đọc hiểu và sinh mã chính xác của các mô hình ngôn ngữ lớn (LLM) đằng sau Cursor, bám sát phương pháp CCGS và ECC).*

### **Phase 1: Core Architecture & Reactive State Setup (Quy Tắc Và Lõi Phản Ứng)**

**Mục tiêu:** Xây dựng hệ thống thư mục tiêu chuẩn, áp dụng các quy tắc hạn chế tầm vực, và lập trình bộ máy quản lý dữ liệu toàn cục mô phỏng reactivity của Vue thông qua Godot Resources.

**Prompt:**

Role: You are a Tier 1 Technical Director expert in Godot 4.6 and GDScript. We are converting a Phaser/Vue web game (TCG Card Shop Simulator) into a Godot 4.6 desktop game.

Task: Set up the core architecture rules, .cursorrules, and the global reactive state management system.

Requirements:

1. Define a strict folder structure: src/core/ (autoloads, state resources), src/gameplay/ (entities, controllers), src/ui/ (menus, HUD), assets/ (sprites, data).  
2. Create path-scoped documentation rules in a .cursorrules file dictating that UI components must only READ from Resource stores and listen to signals, never modifying data directly without calling a command method. Core logic must be zero-allocation in hot paths.  
3. Implement a Reactive State Management system using Godot Resources to replace Vue's reactivity. Create a base ReactiveStore.gd (inheriting from Resource) that automatically emits a state\_changed(property\_name, new\_value) signal whenever a setter is called via setget logic.  
4. Create EconomyStore.tres inheriting from this base. It must track current\_cash, player\_level, current\_xp, and daily\_modifier with specific setters emitting signals.  
5. Set up an Autoload GameManager to hold references to these persistent Resource stores.

Execution: Present the architecture draft, explain how it mirrors fine-grained reactivity without polling \_process(), then upon my approval, generate the GDScript files. Follow "Verification-Driven Development" by writing a simple unit test script.

### **Phase 2: Isometric World Building & Math Configuration (Thế Giới Và Toán Học Isometric)**

**Mục tiêu:** Định nghĩa ma trận lưới không gian 2.5D, cấu hình chiều sâu Y-Sort, và viết các hàm nội suy lưới màn hình tỷ lệ 2:1.

**Prompt:**

Role: You are a Tier 2 Lead Environment Programmer.

Task: Implement the core 2.5D Isometric World using Godot 4.6 TileMapLayer nodes and Y-Sorting.

Requirements:

1. Create a MainWorld.tscn scene with a Node2D root having y\_sort\_enabled \= true.  
2. Add multiple TileMapLayer nodes under the root: FloorLayer, WallLayer, FurnitureLayer. Ensure ALL layers have y\_sort\_enabled \= true.  
3. Configure the TileSet resource assigned to these layers to use an Isometric Shape with a Diamond Downward layout. Set the exact tile size to 64x32 to enforce the 2:1 isometric pixel art standard.  
4. Implement a custom grid math utility function in an autoload GridManager.gd to translate between isometric grid coordinates and screen coordinates, using the formulas: screen\_x \= (grid\_x \- grid\_y) \* (tile\_width / 2\) and screen\_y \= (grid\_x \+ grid\_y) \* (tile\_height / 2).  
5. Create a basic controllable Player character (CharacterBody2D) with an 8-way movement system. Ensure the player's Sprite2D pivot offset is strictly at the bottom-center (the feet) for perfect Y-sorting logic.

Execution: Provide the node structures and the mathematical GDScript implementation. Validate how TileMapLayer handles the offsets.

### **Phase 3: Construction System & Validated Placement (Hệ Thống Xây Dựng Và Xác Thực Đặt Chỗ)**

**Mục tiêu:** Phát triển hệ thống kéo thả đồ nội thất của chế độ xây dựng, kết hợp snap vào lưới và quét không gian vật lý để cấm đặt chồng chéo.

**Prompt:**

Role: You are a Tier 2 Gameplay Programmer.

Task: Implement the Drag-and-Drop furniture placement system for the TCG Shop.

Context: Furniture serves different roles: Selling, Storage, and Display. They occupy specific 64x32 isometric grid cells.

Requirements:

1. Create a base Furniture.tscn (StaticBody2D). It must contain a Sprite2D (bottom-center pivot), a CollisionPolygon2D exactly matching the 64x32 diamond base, and y\_sort\_enabled \= true.  
2. Implement a PlacementController.gd. When in "Build Mode", a ghost representation of the selected furniture follows the mouse.  
3. The ghost must snap to the isometric grid using local\_to\_map and map\_to\_local from the FurnitureLayer TileMapLayer.  
4. Validate placement: Use Godot's PhysicsServer2D or Physics2DDirectSpaceState.intersect\_shape() to check if the footprint overlaps with walls, other furniture, or the player. Color the ghost modulate Red if invalid, Green if valid.  
5. On left-click (if valid), instance the furniture into the FurnitureLayer.  
6. Implement a base data structure inside the furniture script to hold nested array slots representing "Tiers" (e.g., up to 16 packs per tier).

Execution: Draft the placement algorithm logic first, specifically handling the translation from mouse screen coordinates to isometric grid coordinates and the physics shape query.

### **Phase 4: Dynamic Obstacles & AI Pathfinding Grid (Mạng Lưới Tìm Đường Động Của AI)**

**Mục tiêu:** Khởi tạo mạng lưới điều hướng siêu tốc và lập trình khả năng tự động cập nhật vùng cấm khi đồ nội thất được xây dựng.

**Prompt:**

Role: You are a Tier 2 Lead AI Programmer.

Task: Implement dynamic pathfinding using Godot 4.6's AStarGrid2D.

Requirements:

1. In GridManager.gd, initialize an AStarGrid2D instance mapping over the conceptual grid of the 64x32 isometric tiles.  
2. Configure the AStarGrid2D heuristic to use Manhattan distance, which is mathematically superior for 4-way or isometric staggered grid pathing. Set default cells as walkable.  
3. Implement a signal connection between the PlacementController and GridManager. Whenever a Furniture piece is successfully placed or removed, the GridManager must recalculate the covered grid cells and set their solid state using set\_point\_solid().  
4. Create a debug drawing script that overlays dots on walkable grid points to visually verify the pathfinding map during runtime.

Execution: Provide the setup code for AStarGrid2D and explain how memory allocation is kept low during dynamic obstacle updates.

### **Phase 5: Supply Chain Physics & Interactions (Vật Lý Chuỗi Cung Ứng Và Tương Tác)**

**Mục tiêu:** Lập trình vòng lặp đặt hàng, giao hàng vật lý, và cơ chế chuyển đổi trạng thái vật lý để bưng vác hộp hàng trên đầu nhân vật.

**Prompt:**

Role: You are a Tier 3 Gameplay Specialist.

Task: Implement the delivery and carry system using RigidBody2D state transitions.

Requirements:

1. Create a DeliveryBox.tscn using RigidBody2D. Add a collision shape and a Sprite.  
2. Implement spawn logic where the box falls from a simulated height (manipulating a visual Y-axis offset sprite while the physical body lands on the 2D plane).  
3. Implement an interaction Area2D on the Player. When pressing the "Interact" key near a box, the box enters the "Carried" state.  
4. "Carrying" logic: Reparent the DeliveryBox to the Player node, change its physics mode to RigidBody2D.FREEZE\_MODE\_KINEMATIC, disable its collision mask, and set its visual position hovering above the player's sprite.  
5. "Dropping" logic: Reparent the box back to the world, restore its physics mode to rigid, and apply a small impulse forward based on player facing direction.  
6. "Stocking" logic: If interacting with a Furniture node while carrying a box, run an animation to transfer contents, increment the shelf tier fill level, and queue\_free() the box.

Execution: Write the precise state transition code for handling Godot's RigidBody2D physics modes without causing physics server glitches or player clipping.

### **Phase 6: NPC State Machine & Economic Transactions (Máy Trạng Thái Và Giao Dịch Kinh Tế)**

**Mục tiêu:** Xây dựng tâm lý học, bộ định tuyến mục tiêu và thuật toán trả giá tự động cho khách hàng.

**Prompt:**

Role: You are a Tier 2 Gameplay Programmer.

Task: Implement the Customer NPC State Machine (FSM) and the Pricing Logic.

Requirements:

1. Create a CustomerNPC.tscn (CharacterBody2D) with Y-sort enabled and bottom-center pivot.  
2. Implement a node-based Finite State Machine. States: Spawn, Wander, SeekItem, GoToCheckout, PlayTable, Leave.  
3. On spawn, assign a probability-based intent: Buy (70%), Sell (10%), Play (20%).  
4. Path following: The NPC requests a path array from GridManager.get\_id\_path(), moving tile-by-tile using tweening or kinematic movement.  
5. Pricing Algorithm (Buy Intent): The NPC arrives at a selling furniture. Evaluate player\_set\_price. Final price evaluation threshold is randomly clamped between 0.1x and 5.0x of the card's base market price. Multiply player price by EconomyStore.daily\_modifier.  
6. If the price is acceptable, remove the item from the shelf data, instantiate a "carrying item" visual on the NPC, and transition to GoToCheckout.

Execution: Provide the FSM architecture and the isolated mathematical function for the pricing tolerance evaluation.

### **Phase 7: Gacha Pack Opening & Tear Shaders (Đồ Họa Mở Gói Gacha Bằng Shader)**

**Mục tiêu:** Hoàn thiện trải nghiệm hình ảnh siêu thực khi mở gói thẻ bài, kết hợp logic phân loại độ hiếm chuẩn xác.

**Prompt:**

Role: You are a Tier 3 UX and Shader Specialist.

Task: Implement the Gacha Pack Opening logic and visually stunning tear animation using Godot Shaders.

Context: Packs contain 6 cards sorted by rarity (Rank 0-10, Ghost Rare). The rarest card is always revealed last.

Requirements:

1. Implement PackOpener.gd. Roll 6 cards from a JSON database using weighted probabilities. Sort the resulting array by rarity\_rank ascending.  
2. Build a dedicated UI Phase (Control node hierarchy) for "Pack Opening Drama".  
3. Write a Godot Shader (CanvasItem) for the Pack Graphic. Use a noise texture uniform as a mask. Create a cut\_progress uniform. By animating cut\_progress from 0.0 to 1.0 via a Tween, dissolve the alpha channel along the jagged noise edges, simulating ripping foil/paper or a burn effect.  
4. Once fully torn, display the 6 cards face down. Implement a Tween flip animation for each card when clicked (scaling X-axis from 1 to 0, swapping texture, scaling 0 to 1).

Execution: Provide the exact GLSL/Godot Shader code for the paper tear effect and the GDScript logic handling the Tween sequence.

### **Phase 8: PSA Grading & Battle Math (Toán Học Kiểm Định Và Giao Tranh)**

**Mục tiêu:** Lập trình các vòng lặp tính toán vô hình cho hệ thống kiểm định chất lượng thẻ PSA và cơ chế mô phỏng thi đấu TCG.

**Prompt:**

Role: You are a Tier 2 Game Designer and Coder.

Task: Implement the PSA Grading roll system and the Battle Mini-game math from the SPEC.

Requirements:

1. Create GradingService.gd. Function submit\_card(card): Perform a weighted probability roll to return a grade from 1 to 10\.  
2. Apply a value multiplier to the card based on the grade (e.g., PSA 10 \= x20.0 base price, PSA 1 \= x0.3 base price). Convert card data into a "Slab" status.  
3. Create a lightweight BattleEngine.gd that handles logic at play tables without physics simulation.  
4. Implement damage calculations: final\_damage \= (base\_damage \* weakness\_multiplier) \- resistance\_reduction. Hardcode weakness multiplier to 2.0 and flat resistance reduction to 30\.  
5. Implement an "Advanced Mode" energy check function that validates if the attacking card has the correct required specific energy types and colorless counts attached before returning a valid attack boolean.

Execution: Focus strictly on the mathematical processing algorithms and state validation for these mechanics. No UI is needed for this prompt, only backend logic.

### **Phase 9: CanvasLayer UI Integration (Tích Hợp Giao Diện Người Dùng Đa Tầng)**

**Mục tiêu:** Xây dựng màn hình hiển thị HUD hoàn chỉnh, phản ánh chính xác tổ chức layout từ hình ảnh tham chiếu, tận dụng tối đa hệ thống tín hiệu kết nối với Reactive Store.

**Prompt:**

Role: You are a Tier 2 UI Developer.

Task: Build the responsive CanvasLayer HUD based on the reference layout, strictly hooking into the ReactiveState signals.

Requirements:

1. Create a HUD.tscn using a CanvasLayer to keep it independent of the 2D camera.  
2. Use MarginContainer, HBoxContainer, and VBoxContainer to replicate the layout:  
   * Left Sidebar: Build tools/furniture categories.  
   * Top Right: Inspection panel for selected objects (e.g., shelf contents).  
   * Bottom Left: Currency and Experience tracking.  
   * Bottom Right: Time and Day progression.  
3. In the \_ready() function of the currency and XP labels, connect to the state\_changed signal emitted by the EconomyStore (created in Phase 1).  
4. Ensure the UI updates dynamically ONLY when the signal fires, completely avoiding checking values in \_process().

Execution: Provide the node structure for the UI hierarchy and the GDScript snippets demonstrating the signal connection to the resources.

### **Phase 10: JSON Serialization & Data Persistence (Lưu Trữ Dữ Liệu Lâu Dài Và Tối Ưu Hóa)**

**Mục tiêu:** Xử lý rào cản chuyển đổi dữ liệu không đồng nhất để lưu giữ trạng thái toàn cục của thế giới thông qua JSON, tích hợp cơ chế chống giật lag khi lưu game.

**Prompt:**

Role: You are a Tier 1 Technical Director.

Task: Finalize the save/load persistence system using JSON serialization, accounting for Godot types.

Requirements:

1. Create a SaveManager.gd autoload.  
2. Implement a system where all stateful objects (GameManager, EconomyStore, InventoryStore, and placed Furniture) are added to a "Persist" group.  
3. The save function must loop through the "Persist" group and call a save\_data() method on each node.  
4. Address JSON limitations: Write custom logic inside save\_data() to convert Godot-specific types like Vector2 (positions) into arrays or dictionaries {"x": pos.x, "y": pos.y} because Godot's JSON.stringify does not support native Godot variants.  
5. Implement a Debouncer for auto-saving: Trigger the save function when major state-changing actions occur (End of Day, Large Transaction), but use a timer to debounce it by 5 seconds to prevent frame stuttering.

Execution: Provide the complete serialization and deserialization loop, specifically highlighting the Vector2 conversion workaround and the debouncing logic.

## **Quản Lý Rủi Ro Hệ Thống Và Tối Ưu Hóa Hiệu Suất**

Việc chuyển đổi kiến trúc đồ họa và logic từ web sang một phần mềm engine C++ như Godot không chỉ là vấn đề dịch thuật mã nguồn, mà còn là sự đối mặt với những nguyên lý quản lý tài nguyên phân cấp cực kỳ khắt khe. Trong quá trình sử dụng hệ thống lộ trình AI kể trên, người vận hành cần đặc biệt lưu tâm đến các vấn đề bảo trì tài nguyên và rủi ro rò rỉ bộ nhớ.

Vấn đề nghiêm trọng nhất thường phát sinh từ mô hình phản ứng dữ liệu thông qua hệ thống Signal là tình trạng các nút mồ côi (Orphan Nodes) và rò rỉ kết nối.7 Không giống như Vue.js sở hữu một bộ thu gom rác (garbage collector) chạy ngầm quản lý DOM một cách tự động, Godot yêu cầu sự giải phóng bộ nhớ minh bạch đối với cây cấu trúc nút. Khi người chơi quyết định bán một kệ hàng để lấy lại tiền, hoặc khi một gói thẻ bài đã được bóc xong, việc chỉ đơn giản ẩn đối tượng đó đi (hide()) là một sai lầm thảm họa. Đối tượng đó phải được tiêu hủy vĩnh viễn thông qua lệnh queue\_free(). Nếu một đối tượng bị xóa khỏi cây cấu trúc mà hệ thống kết nối tín hiệu (Signal connections) với ReactiveStore không bị ngắt, hoặc nếu nút bị gỡ ra (remove\_child) mà không được giải phóng, nó sẽ trôi nổi mãi mãi trong bộ nhớ RAM, dẫn đến việc trò chơi ngày càng chậm chạp và cuối cùng là sụp đổ hệ thống (crash) sau vài giờ chơi.

Thứ hai, việc áp dụng vật lý vào một thế giới 2.5D tĩnh là một con dao hai lưỡi. Động lực học của Godot tính toán va chạm theo từng khung hình vật lý (\_physics\_process). Việc để lộ ra một rào chắn RigidBody2D di động (hộp giao hàng) ngay trên lối đi của hệ thống AI tìm đường AStarGrid2D có thể dẫn đến việc cỗ máy trạng thái (FSM) của khách hàng bị rơi vào vòng lặp vô hạn (infinite loop) do cố gắng tìm đường vượt qua một chướng ngại vật liên tục thay đổi tọa độ vi mô. Đây chính là nguyên nhân sâu xa dẫn đến quyết định bắt buộc phải đưa hộp giao hàng về trạng thái FREEZE\_MODE\_KINEMATIC và tước bỏ lớp va chạm của nó khi nó được người chơi nhấc bổng khỏi mặt đất, bảo vệ hệ sinh thái đường đi của NPC một cách an toàn nhất.1

Cuối cùng, hiệu năng kết xuất của hệ thống TileMapLayer trong Godot 4.6 đã được cải thiện vượt bậc nhờ cơ chế gom cụm dữ liệu tự động (batching).11 Tuy nhiên, nếu hàm set\_cell được gọi hàng trăm lần mỗi khung hình (ví dụ: một hệ thống cọ quét xây dựng sàn nhà diện rộng viết cẩu thả), trò chơi sẽ bị nghẽn cổ chai (bottleneck) tại luồng CPU chính. Để ngăn chặn điều này, bất kỳ thao tác thay đổi hàng loạt nào trên bản đồ đều phải được đẩy vào cấu trúc trì hoãn lệnh call\_deferred(), cho phép engine gom toàn bộ các lệnh cập nhật lưới vào cuối khung hình và kết xuất chúng trong một chu kỳ bộ nhớ duy nhất.11 Việc hiểu và giám sát chặt chẽ các ranh giới kiến trúc cấp thấp này sẽ là chìa khóa bảo đảm sản phẩm cuối cùng không chỉ tái tạo trọn vẹn sức hút của phiên bản gốc, mà còn vươn tới một cấp độ mượt mà và ổn định chuẩn mực của ngành công nghiệp phát triển trò chơi đương đại.

#### **Works cited**

1. SPEC.md  
2. affaan-m/everything-claude-code: The agent harness ... \- GitHub, accessed April 28, 2026, [https://github.com/affaan-m/everything-claude-code](https://github.com/affaan-m/everything-claude-code)  
3. Donchitos Claude-Code-Game-Studios · Discussions \- GitHub, accessed April 28, 2026, [https://github.com/Donchitos/Claude-Code-Game-Studios/discussions](https://github.com/Donchitos/Claude-Code-Game-Studios/discussions)  
4. Donchitos/Claude-Code-Game-Studios: Turn Claude Code ... \- GitHub, accessed April 28, 2026, [https://github.com/Donchitos/Claude-Code-Game-Studios](https://github.com/Donchitos/Claude-Code-Game-Studios)  
5. Reactivity Models Compared: React, Vue, Angular, Svelte \- OpenReplay Blog, accessed April 28, 2026, [https://blog.openreplay.com/reactivity-react-vue-angular-svelte/](https://blog.openreplay.com/reactivity-react-vue-angular-svelte/)  
6. State Management in Godot with a Vue.js Twist \- Tumeo Space, accessed April 28, 2026, [https://tumeo.space/gamedev/2023/10/18/godot-states/](https://tumeo.space/gamedev/2023/10/18/godot-states/)  
7. Rdot, Reactivity like in Vue, Solid.js or Qwik\! : r/godot \- Reddit, accessed April 28, 2026, [https://www.reddit.com/r/godot/comments/1bz06k7/rdot\_reactivity\_like\_in\_vue\_solidjs\_or\_qwik/](https://www.reddit.com/r/godot/comments/1bz06k7/rdot_reactivity_like_in_vue_solidjs_or_qwik/)  
8. Resources — Godot Engine (stable) documentation in English, accessed April 28, 2026, [https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html](https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html)  
9. Isometric pixel art for games — grids, sprites, and tilesets, accessed April 28, 2026, [https://www.sprite-ai.art/guides/isometric-pixel-art](https://www.sprite-ai.art/guides/isometric-pixel-art)  
10. Godot 4.6 TileMapLayers Basics – One Tile, Endless Backgrounds \- YouTube, accessed April 28, 2026, [https://www.youtube.com/watch?v=ZPKz1zNRR0A](https://www.youtube.com/watch?v=ZPKz1zNRR0A)  
11. TileMapLayer — Godot Engine (4.4) documentation in English, accessed April 28, 2026, [https://docs.godotengine.org/en/4.4/classes/class\_tilemaplayer.html](https://docs.godotengine.org/en/4.4/classes/class_tilemaplayer.html)  
12. Move & Snap Objects to an Isometric Grid | Godot 4.4 \- YouTube, accessed April 28, 2026, [https://www.youtube.com/watch?v=UsEYLZ3LiVw](https://www.youtube.com/watch?v=UsEYLZ3LiVw)  
13. Why my character is not on front? I turn the y sorting still : r/godot \- Reddit, accessed April 28, 2026, [https://www.reddit.com/r/godot/comments/1gz2f24/why\_my\_character\_is\_not\_on\_front\_i\_turn\_the\_y/](https://www.reddit.com/r/godot/comments/1gz2f24/why_my_character_is_not_on_front_i_turn_the_y/)  
14. UNIQUE Cards & CARD FLIP Animation \- Godot 4 Card Game Tutorial \#6 \- YouTube, accessed April 28, 2026, [https://www.youtube.com/watch?v=L1dEuHr5AGU](https://www.youtube.com/watch?v=L1dEuHr5AGU)  
15. Grid-based movement :: Godot 4 Recipes \- KidsCanCode, accessed April 28, 2026, [https://kidscancode.org/godot\_recipes/4.x/2d/grid\_movement/index.html](https://kidscancode.org/godot_recipes/4.x/2d/grid_movement/index.html)  
16. Moving on an isometric grid \- Programming \- Godot Forum, accessed April 28, 2026, [https://forum.godotengine.org/t/moving-on-an-isometric-grid/127824](https://forum.godotengine.org/t/moving-on-an-isometric-grid/127824)  
17. Godot Tilemap Collisions | Depth Sorting Explained \- YouTube, accessed April 28, 2026, [https://www.youtube.com/watch?v=3VW8R5pHhPY](https://www.youtube.com/watch?v=3VW8R5pHhPY)  
18. AStarGrid2D — Godot Engine (4.4) documentation in English, accessed April 28, 2026, [https://docs.godotengine.org/en/4.4/classes/class\_astargrid2d.html](https://docs.godotengine.org/en/4.4/classes/class_astargrid2d.html)  
19. Godot Shader Pack \- the product description \- YouTube, accessed April 28, 2026, [https://www.youtube.com/watch?v=VWP-JJDVMDM](https://www.youtube.com/watch?v=VWP-JJDVMDM)  
20. Godot Shaders: How to Make Paper Burn & Dissolve \- Reddit, accessed April 28, 2026, [https://www.reddit.com/r/godot/comments/b1529n/godot\_shaders\_how\_to\_make\_paper\_burn\_dissolve/](https://www.reddit.com/r/godot/comments/b1529n/godot_shaders_how_to_make_paper_burn_dissolve/)