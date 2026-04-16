-- ============================================================
-- SEED: 7 POI mau cho pho am thuc Vinh Khanh, Quan 4
-- Chay trong Supabase SQL Editor
-- Dung UPSERT de co the chay lai nhieu lan va cap nhat noi dung moi
-- ============================================================

-- ============================================================
-- BANG POI
-- ============================================================
INSERT INTO poi (id, tenpoi, vido, kinhdo, bankinh, mucuutien, trangthai, diachi, sdt, anhdaidien)
VALUES
    ('11111111-1111-1111-1111-111111111111',
     'Quan Oc Oanh', 10.758955, 106.701831, 35, 1, TRUE,
     '234 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0001',
     'https://images.unsplash.com/photo-1510130387422-82bed34b37e9?auto=format&fit=crop&w=1200&q=80'),

    ('22222222-2222-2222-2222-222222222222',
     'Bo To Co Ut', 10.759512, 106.700942, 30, 2, TRUE,
     '215 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0002',
     'https://images.unsplash.com/photo-1558030006-450675393462?auto=format&fit=crop&w=1200&q=80'),

    ('33333333-3333-3333-3333-333333333333',
     'Lau Ca Duoi 404', 10.758312, 106.702114, 30, 3, TRUE,
     '404 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0003',
     'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=80'),

    ('44444444-4444-4444-4444-444444444444',
     'Che Khanh Vy', 10.759884, 106.701221, 25, 4, TRUE,
     '180 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0004',
     'https://images.unsplash.com/photo-1563805042-7684c019e1cb?auto=format&fit=crop&w=1200&q=80'),

    ('55555555-5555-5555-5555-555555555555',
     'Hai San Nuong Ba Phat', 10.759100, 106.702450, 30, 5, TRUE,
     '310 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0005',
     'https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=1200&q=80'),

    ('66666666-6666-6666-6666-666666666666',
     'Bun Bo Hue Di Sau', 10.758640, 106.700560, 25, 6, TRUE,
     '152 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0006',
     'https://images.unsplash.com/photo-1617093727343-374698b1b08d?auto=format&fit=crop&w=1200&q=80'),

    ('77777777-7777-7777-7777-777777777777',
     'Nhau Via He Nam Beo', 10.760210, 106.701605, 28, 7, TRUE,
     '275 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
     '028 3940 0007',
     'https://images.unsplash.com/photo-1529563021893-cc83c992d75d?auto=format&fit=crop&w=1200&q=80')
ON CONFLICT (id) DO UPDATE SET
    tenpoi = EXCLUDED.tenpoi,
    vido = EXCLUDED.vido,
    kinhdo = EXCLUDED.kinhdo,
    bankinh = EXCLUDED.bankinh,
    mucuutien = EXCLUDED.mucuutien,
    trangthai = EXCLUDED.trangthai,
    diachi = EXCLUDED.diachi,
    sdt = EXCLUDED.sdt,
    anhdaidien = EXCLUDED.anhdaidien;

-- ============================================================
-- BANG THUYET MINH
-- ============================================================
INSERT INTO thuyetminh (id, poiid, thutu, trangthai)
VALUES
    ('a1111111-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 1, TRUE),
    ('a2222222-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', 1, TRUE),
    ('a3333333-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333', 1, TRUE),
    ('a4444444-0000-0000-0000-000000000004', '44444444-4444-4444-4444-444444444444', 1, TRUE),
    ('a5555555-0000-0000-0000-000000000005', '55555555-5555-5555-5555-555555555555', 1, TRUE),
    ('a6666666-0000-0000-0000-000000000006', '66666666-6666-6666-6666-666666666666', 1, TRUE),
    ('a7777777-0000-0000-0000-000000000007', '77777777-7777-7777-7777-777777777777', 1, TRUE)
ON CONFLICT (id) DO UPDATE SET
    poiid = EXCLUDED.poiid,
    thutu = EXCLUDED.thutu,
    trangthai = EXCLUDED.trangthai;

-- ============================================================
-- BANG BAN DICH
-- ============================================================
INSERT INTO bandich (id, thuyetminhid, ngonngu, noidung, fileaudio)
VALUES
    ('b1010101-0000-0000-0000-000000000001',
     'a1111111-0000-0000-0000-000000000001', 'vi',
     'Ban dang den gan Quan Oc Oanh, mot diem an dem noi tieng tren pho Vinh Khanh. Quan phuc vu manh tu 16:00 den khuya va duoc nhieu thuc khach nho nho vi mon oc len xao dua beo thom, oc huong rang muoi ot va ngheu hap Thai chua cay. Khong gian quan binh dan, dong vui, thich hop cho nhom ban va du khach muon trai nghiem van hoa am thuc via he Sai Gon.',
     NULL),
    ('b1010101-0000-0000-0000-000000000002',
     'a1111111-0000-0000-0000-000000000001', 'en',
     'You are approaching Oanh Snail Restaurant, one of the most recognizable late-night food spots on Vinh Khanh Street. The venue is popular for coconut-sauteed mud creeper snails, salt-and-chili sea snails, and Thai-style steamed clams. It opens from late afternoon until midnight and offers a lively local street-food atmosphere.',
     NULL),
    ('b1010101-0000-0000-0000-000000000003',
     'a1111111-0000-0000-0000-000000000001', 'zh',
     '您正靠近Oanh螺类餐馆。这是一家永庆街知名的夜间美食店，以椰汁炒嗦螺、椒盐海螺和泰式蒸蛤蜊而受欢迎。餐厅从傍晚营业到深夜，气氛热闹，适合体验西贡街头美食。',
     NULL),

    ('b2020202-0000-0000-0000-000000000001',
     'a2222222-0000-0000-0000-000000000002', 'vi',
     'Ban dang den gan Bo To Co Ut. Quan noi bat voi cac mon bo to che bien theo phong cach Nam Bo nhu bo nuong la lot, be thui cuon banh trang va bo nhung giam. Thit duoc lam moi trong ngay, tam uop vua vi, phu hop cho nhom khach muon an toi no va thu nhieu mon de cuon cung rau song, chuoi chat va mam nem.',
     NULL),
    ('b2020202-0000-0000-0000-000000000002',
     'a2222222-0000-0000-0000-000000000002', 'en',
     'You are approaching Co Ut Young Beef Restaurant. This place is known for Southern-style beef dishes such as grilled beef in wild betel leaves, roasted veal with rice paper, and vinegar hotpot beef sets. The menu is designed for sharing, with fresh herbs, green banana, and fermented anchovy dipping sauce.',
     NULL),
    ('b2020202-0000-0000-0000-000000000003',
     'a2222222-0000-0000-0000-000000000002', 'zh',
     '您正靠近Co Ut嫩牛肉馆。这里主打南部风味牛肉料理，如蒌叶烤嫩牛、烤小牛肉米纸卷和醋涮牛肉。餐点适合多人分享，通常搭配香草、青香蕉和发酵鱼露蘸酱。',
     NULL),

    ('b3030303-0000-0000-0000-000000000001',
     'a3333333-0000-0000-0000-000000000003', 'vi',
     'Ban dang den gan Lau Ca Duoi 404. Day la mot trong nhung quan lau duoc nhac den nhieu khi di food tour Vinh Khanh. Nuoc lau co vi chua thanh tu me va sau, them sa va ot tao mui thom dac trung. Mon lau duoc an kem rau rung, dau bap va bun tuoi; ngoai ra quan con co ca duoi nuong va muc trung hap gung de goi them.',
     NULL),
    ('b3030303-0000-0000-0000-000000000002',
     'a3333333-0000-0000-0000-000000000003', 'en',
     'You are approaching Stingray Hotpot 404. This stop is famous on Vinh Khanh food tours for its tamarind-and-sour-fruit broth, fragrant with lemongrass and chili. Guests often pair the hotpot with wild greens, okra, and fresh noodles, then order grilled stingray or ginger-steamed squid roe as side dishes.',
     NULL),
    ('b3030303-0000-0000-0000-000000000003',
     'a3333333-0000-0000-0000-000000000003', 'zh',
     '您正靠近404魟鱼火锅店。这里因罗望子与酸果熬制的酸辣汤底而闻名，带有香茅和辣椒香气。食客通常搭配野菜、秋葵和米粉一起食用，也会加点烤魟鱼或姜蒸墨鱼卵。',
     NULL),

    ('b4040404-0000-0000-0000-000000000001',
     'a4444444-0000-0000-0000-000000000004', 'vi',
     'Ban dang den gan Che Khanh Vy, diem dung chan ngot mat giua pho am thuc. Quan phuc vu hon 30 loai che va mon an vat lanh, phu hop de giai nhiet sau khi thu cac mon nuong va hai san. Mon duoc goi nhieu gom che ba mau, che khuc bach, tau hu tran chau duong den va sua tuoi tran chau duong den.',
     NULL),
    ('b4040404-0000-0000-0000-000000000002',
     'a4444444-0000-0000-0000-000000000004', 'en',
     'You are approaching Khanh Vy Dessert Shop, a sweet break in the middle of the busy food street. The stall offers a broad range of Vietnamese desserts and chilled snacks, ideal after grilled seafood and rich hotpot dishes. Popular picks include three-color sweet soup, almond jelly, tofu pudding with pearls, and brown-sugar fresh milk.',
     NULL),
    ('b4040404-0000-0000-0000-000000000003',
     'a4444444-0000-0000-0000-000000000004', 'zh',
     '您正靠近Khanh Vy甜品店，这是热闹美食街中的清爽一站。店内提供多种越南甜汤与冰品，适合在海鲜和火锅之后解腻。人气品项包括三色甜汤、杏仁冻、黑糖珍珠豆花和黑糖鲜奶。',
     NULL),

    ('b5050505-0000-0000-0000-000000000001',
     'a5555555-0000-0000-0000-000000000005', 'vi',
     'Ban dang den gan Hai San Nuong Ba Phat. Quan co the manh o cac mon nuong than hoa, hai san tuoi chon tai quay va cac mon sot me, sot trung muoi. Mui khoi than va huong toi phi tao nen khong khi rat dac trung cua pho Vinh Khanh vao buoi toi. Day la diem phu hop cho khach thich hai san va muon an theo kieu goi mon de chia se.',
     NULL),
    ('b5050505-0000-0000-0000-000000000002',
     'a5555555-0000-0000-0000-000000000005', 'en',
     'You are approaching Ba Phat Grilled Seafood. The restaurant specializes in charcoal-grilled seafood, tank-fresh shellfish, and rich tamarind or salted-egg sauces. The aroma of smoke and fried garlic defines this evening stop, making it a great choice for groups who want to order many dishes to share.',
     NULL),
    ('b5050505-0000-0000-0000-000000000003',
     'a5555555-0000-0000-0000-000000000005', 'zh',
     '您正靠近Ba Phat烤海鲜店。餐厅主打炭烤海鲜、现点现做的贝类，以及罗望子酱和咸蛋黄酱风味料理。炭火与蒜香构成了这里夜晚最鲜明的味道，非常适合多人一起点菜分享。',
     NULL),

    ('b6060606-0000-0000-0000-000000000001',
     'a6666666-0000-0000-0000-000000000006', 'vi',
     'Ban dang den gan Bun Bo Hue Di Sau. Quan mo tu sang som va thuong dong khach truoc gio trua. Nuoc dung duoc ham voi xuong, sa va mam ruoc cho mui vi dam da; to bun day du gom bap bo, cha cua, moc va huyet. Day la lua chon phu hop neu ban muon bat dau hanh trinh food tour bang mot bua an nong va no bung.',
     NULL),
    ('b6060606-0000-0000-0000-000000000002',
     'a6666666-0000-0000-0000-000000000006', 'en',
     'You are approaching Di Sau Hue Beef Noodle Soup. This spot opens early and is often busy before noon. Its broth is simmered with bones, lemongrass, and fermented shrimp paste for a deep central-Vietnamese flavor, while the bowl is topped with beef shank, crab cake, meatballs, and pork blood cake.',
     NULL),
    ('b6060606-0000-0000-0000-000000000003',
     'a6666666-0000-0000-0000-000000000006', 'zh',
     '您正靠近Di Sau顺化牛肉粉店。店铺从清晨开始营业，中午前常常客满。汤底以骨头、香茅和虾酱慢熬而成，味道浓郁，常见配料有牛腱、蟹饼、肉丸和猪血糕。',
     NULL),

    ('b7070707-0000-0000-0000-000000000001',
     'a7777777-0000-0000-0000-000000000007', 'vi',
     'Ban dang den gan Nhau Via He Nam Beo. Day la kieu quan nhau binh dan mo den khuya, khong gian ngoi sat via he, phu hop cho nhom ban muon an vat va tro chuyen. Quan noi tieng voi kho muc nuong, goi ngo sen tom thit, dau ca loc hap sa va long nuong. Gia mon o muc de tiep can, de goi nhieu dia nho cung luc.',
     NULL),
    ('b7070707-0000-0000-0000-000000000002',
     'a7777777-0000-0000-0000-000000000007', 'en',
     'You are approaching Nam Beo Sidewalk Eatery. This is a casual late-night drinking and snacking stop with open-air sidewalk seating. It is especially popular for charcoal-grilled dried squid, lotus stem salad with shrimp and pork, steamed snakehead fish head, and grilled offal. Prices are approachable, so guests often order several small plates.',
     NULL),
    ('b7070707-0000-0000-0000-000000000003',
     'a7777777-0000-0000-0000-000000000007', 'zh',
     '您正靠近Nam Beo街边小吃馆。这是一家营业到深夜的平价露天小馆，适合朋友聚会小酌。招牌菜包括炭烤鱿鱼干、莲梗虾肉沙拉、香茅蒸乌鱼头和烤内脏，通常会一次点上好几盘。',
     NULL)
ON CONFLICT (id) DO UPDATE SET
    thuyetminhid = EXCLUDED.thuyetminhid,
    ngonngu = EXCLUDED.ngonngu,
    noidung = EXCLUDED.noidung,
    fileaudio = EXCLUDED.fileaudio;

-- ============================================================
-- BANG MON AN
-- ============================================================
INSERT INTO monan (id, poiid, tenmonan, mota, dongia, phanloai, hinhanh, tinhtrang)
VALUES
    ('c1000001-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111',
     'Oc len xao dua', 'Oc len tuoi xao cung nuoc cot dua beo, rau ram va banh mi nong.', 79000, 'Mon noi bat',
     'https://placehold.co/600x400/FF7043/FFFFFF?text=Oc+Len+Xao+Dua', TRUE),
    ('c1000002-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111',
     'Oc huong rang muoi ot', 'Oc huong size vua rang kho voi toi phi, muoi hat va ot xanh cay nhe.', 99000, 'Oc rang',
     'https://placehold.co/600x400/FF8A65/FFFFFF?text=Oc+Huong+Rang+Muoi+Ot', TRUE),
    ('c1000003-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111',
     'Ngheu hap Thai', 'Ngheu hap kieu Thai voi sa, la chanh, ot tuoi va nuoc dung chua cay.', 89000, 'Oc hap',
     'https://placehold.co/600x400/FFAB91/FFFFFF?text=Ngheu+Hap+Thai', TRUE),
    ('c1000004-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111',
     'So diep nuong mo hanh', 'So diep nuong than, phu mo hanh va dau phong rang gion.', 109000, 'Hai san nuong',
     'https://placehold.co/600x400/FFCCBC/FFFFFF?text=So+Diep+Nuong+Mo+Hanh', TRUE),

    ('c2000001-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222',
     'Bo to nuong la lot', 'Cuon bo to nong mem nuong than hoa, an kem rau song va mo hanh.', 89000, 'Mon nuong',
     'https://placehold.co/600x400/8D6E63/FFFFFF?text=Bo+To+Nuong+La+Lot', TRUE),
    ('c2000002-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222',
     'Be thui cuon banh trang', 'Thit be thui da vang gion, cuon banh trang voi chuoi chat va khom.', 129000, 'Mon cuon',
     'https://placehold.co/600x400/795548/FFFFFF?text=Be+Thui+Cuon+Banh+Trang', TRUE),
    ('c2000003-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222',
     'Bo nhung giam', 'Phan bo thai mong nhung trong nuoc giam ngot thanh, an kem bun va rau.', 169000, 'Lau nhe',
     'https://placehold.co/600x400/6D4C41/FFFFFF?text=Bo+Nhung+Giam', TRUE),
    ('c2000004-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222',
     'Bo la lach sa te', 'Thit bo la lach xao sa te cay thom, phu hop an cung bia lanh.', 119000, 'Mon xao',
     'https://placehold.co/600x400/5D4037/FFFFFF?text=Bo+La+Lach+Sa+Te', TRUE),

    ('c3000001-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333',
     'Lau ca duoi nho', 'Lau cho 2-3 nguoi an voi ca duoi tuoi, bam chuoi, rau muong va bun tuoi.', 189000, 'Lau',
     'https://placehold.co/600x400/1565C0/FFFFFF?text=Lau+Ca+Duoi+Nho', TRUE),
    ('c3000002-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333',
     'Lau ca duoi lon', 'Phan lau day dan hon cho nhom 4-5 nguoi, vi chua cay dam da de an.', 269000, 'Lau',
     'https://placehold.co/600x400/1976D2/FFFFFF?text=Lau+Ca+Duoi+Lon', TRUE),
    ('c3000003-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333',
     'Ca duoi nuong muoi ot', 'Ca duoi uop muoi ot, nuong than den khi da se gion va thit con do am.', 119000, 'Mon nuong',
     'https://placehold.co/600x400/0D47A1/FFFFFF?text=Ca+Duoi+Nuong+Muoi+Ot', TRUE),
    ('c3000004-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333',
     'Muc trung hap gung', 'Muc trung hap cuc nhanh de giu do ngot, an kem nuoc mam gung.', 109000, 'Mon hap',
     'https://placehold.co/600x400/42A5F5/FFFFFF?text=Muc+Trung+Hap+Gung', TRUE),

    ('c4000001-0000-0000-0000-000000000004', '44444444-4444-4444-4444-444444444444',
     'Che ba mau', 'Che dau do, dau xanh, suong sao va nuoc cot dua da xay.', 28000, 'Che truyen thong',
     'https://placehold.co/600x400/E91E63/FFFFFF?text=Che+Ba+Mau', TRUE),
    ('c4000002-0000-0000-0000-000000000004', '44444444-4444-4444-4444-444444444444',
     'Che khuc bach', 'Khuc bach mem thom mui hanh nhan, an cung vai va hat chia.', 35000, 'Mon lanh',
     'https://placehold.co/600x400/F06292/FFFFFF?text=Che+Khuc+Bach', TRUE),
    ('c4000003-0000-0000-0000-000000000004', '44444444-4444-4444-4444-444444444444',
     'Tau hu tran chau duong den', 'Tau hu mem min voi tran chau dai mem va nuoc duong den nong am.', 30000, 'Dessert',
     'https://placehold.co/600x400/EC407A/FFFFFF?text=Tau+Hu+Tran+Chau+Duong+Den', TRUE),
    ('c4000004-0000-0000-0000-000000000004', '44444444-4444-4444-4444-444444444444',
     'Sua tuoi tran chau duong den', 'Sua tuoi lanh ket hop tran chau den dai, vi beo nhe de uong.', 32000, 'Do uong',
     'https://placehold.co/600x400/AD1457/FFFFFF?text=Sua+Tuoi+Tran+Chau', TRUE),

    ('c5000001-0000-0000-0000-000000000005', '55555555-5555-5555-5555-555555555555',
     'Muc nuong muoi ot', 'Muc ong tuoi nuong than, cat khoanh day, phuc vu cung muoi tieu chanh.', 129000, 'Hai san nuong',
     'https://placehold.co/600x400/FF5722/FFFFFF?text=Muc+Nuong+Muoi+Ot', TRUE),
    ('c5000002-0000-0000-0000-000000000005', '55555555-5555-5555-5555-555555555555',
     'Ghe rang me', 'Ghe xanh sot me chua ngot, vo ao sot dam da va de boc an.', 159000, 'Hai san sot',
     'https://placehold.co/600x400/F4511E/FFFFFF?text=Ghe+Rang+Me', TRUE),
    ('c5000003-0000-0000-0000-000000000005', '55555555-5555-5555-5555-555555555555',
     'So huyet xao toi', 'So huyet xao nhanh tren lua lon voi toi phi va can tay gion.', 99000, 'Mon xao',
     'https://placehold.co/600x400/FF7043/FFFFFF?text=So+Huyet+Xao+Toi', TRUE),
    ('c5000004-0000-0000-0000-000000000005', '55555555-5555-5555-5555-555555555555',
     'Tom su nuong pho mai', 'Tom su size lon nuong voi lop pho mai chay vang beo thom.', 149000, 'Mon nuong',
     'https://placehold.co/600x400/BF360C/FFFFFF?text=Tom+Su+Nuong+Pho+Mai', TRUE),

    ('c6000001-0000-0000-0000-000000000006', '66666666-6666-6666-6666-666666666666',
     'Bun bo Hue dac biet', 'To bun day du voi bap bo, cha cua, moc, huyet va hanh ngoi.', 65000, 'Bun nuoc',
     'https://placehold.co/600x400/D84315/FFFFFF?text=Bun+Bo+Hue+Dac+Biet', TRUE),
    ('c6000002-0000-0000-0000-000000000006', '66666666-6666-6666-6666-666666666666',
     'Bun bo Hue thuong', 'Phan vua cho bua sang gon nhe, van giu nuoc dung dam vi.', 50000, 'Bun nuoc',
     'https://placehold.co/600x400/E64A19/FFFFFF?text=Bun+Bo+Hue+Thuong', TRUE),
    ('c6000003-0000-0000-0000-000000000006', '66666666-6666-6666-6666-666666666666',
     'Cha cua them', 'Phan cha cua vien tron, beo thom, goi rieng de an kem to bun.', 20000, 'Topping',
     'https://placehold.co/600x400/F4511E/FFFFFF?text=Cha+Cua+Them', TRUE),
    ('c6000004-0000-0000-0000-000000000006', '66666666-6666-6666-6666-666666666666',
     'Tra tac xa', 'Ly tra tac giai nhiet, giup can bang vi cay cua to bun bo.', 18000, 'Do uong',
     'https://placehold.co/600x400/FF7043/FFFFFF?text=Tra+Tac+Xa', TRUE),

    ('c7000001-0000-0000-0000-000000000007', '77777777-7777-7777-7777-777777777777',
     'Kho muc nuong than', 'Muc kho nuong thom, xe soi bang chay, cham tuong ot xanh.', 89000, 'Mon nhau',
     'https://placehold.co/600x400/388E3C/FFFFFF?text=Kho+Muc+Nuong+Than', TRUE),
    ('c7000002-0000-0000-0000-000000000007', '77777777-7777-7777-7777-777777777777',
     'Goi ngo sen tom thit', 'Ngo sen gion, tom luoc, thit ba chi va rau ram tron nuoc mam chua ngot.', 79000, 'Goi',
     'https://placehold.co/600x400/43A047/FFFFFF?text=Goi+Ngo+Sen+Tom+Thit', TRUE),
    ('c7000003-0000-0000-0000-000000000007', '77777777-7777-7777-7777-777777777777',
     'Dau ca loc hap sa', 'Dau ca loc hap sa gung, phan nuoc duoc nem vua de dung nong.', 99000, 'Mon hap',
     'https://placehold.co/600x400/2E7D32/FFFFFF?text=Dau+Ca+Loc+Hap+Sa', TRUE),
    ('c7000004-0000-0000-0000-000000000007', '77777777-7777-7777-7777-777777777777',
     'Long nuong sa te', 'Long heo lam sach, uop sa te va nuong nhanh tren than hong.', 109000, 'Mon nuong',
     'https://placehold.co/600x400/1B5E20/FFFFFF?text=Long+Nuong+Sa+Te', TRUE)
ON CONFLICT (id) DO UPDATE SET
    poiid = EXCLUDED.poiid,
    tenmonan = EXCLUDED.tenmonan,
    mota = EXCLUDED.mota,
    dongia = EXCLUDED.dongia,
    phanloai = EXCLUDED.phanloai,
    hinhanh = EXCLUDED.hinhanh,
    tinhtrang = EXCLUDED.tinhtrang;

-- ============================================================
-- TUY CHON: gia han 60 ngay cho toan bo 7 POI
-- Bo comment neu ban muon cac POI chac chan hien tren app
-- ============================================================
-- UPDATE poi
-- SET ngayhethanduytri = NOW() + INTERVAL '60 days'
-- WHERE id IN (
--     '11111111-1111-1111-1111-111111111111',
--     '22222222-2222-2222-2222-222222222222',
--     '33333333-3333-3333-3333-333333333333',
--     '44444444-4444-4444-4444-444444444444',
--     '55555555-5555-5555-5555-555555555555',
--     '66666666-6666-6666-6666-666666666666',
--     '77777777-7777-7777-7777-777777777777'
-- );
