# DIAGRAM_SYNC

## Muc dich

Tai lieu nay dung de quan ly dong bo giua `PlantUML` va code hien tai cua do an.  
PlantUML duoc xem la nguon so do chinh, nhung chi duoc tin dung khi trang thai dong bo ro rang.

## Y nghia trang thai

- `VERIFIED`: kha chac so do dang khop voi code hien tai.
- `WIP`: code hoac chuc nang con dang hoan thien, chua nen xem so do la ban chot.
- `NEEDS_SYNC`: nghi ngo code da doi hoac so do da treo nhip, can cap nhat truoc khi dung cho bao cao.

## Khi nao can cap nhat so do

- Khi doi luong xu ly chinh.
- Khi them, bot, doi thu tu cac buoc quan trong.
- Khi doi method, page, controller, endpoint ma so do dang tham chieu.
- Khi doi payload/API lam sequence khong con dung.
- Khi doi pham vi chuc nang trong do an.

## Nguyen tac cap nhat

- Chi sua so do theo module bi anh huong.
- Khong sua tran lan toan bo thu muc `docs/diagrams`.
- Khong tu suy dien them logic moi neu chua xac minh tu code hoac cau truc do an.
- Neu chua chac, giu nguyen logic cu va doi `Status` thanh `WIP` hoac `NEEDS_SYNC`.
- Moi file `.puml` phai co header comment de theo doi baseline va nguon code.

## Cach lam cho nhung lan sau

- Neu ban sua code, chi cap nhat cac file so do lien quan.
- Sau moi dot sua, xem lai `Status` cua file:
  - con khop thi giu nguyen
  - chua chac thi de `WIP`
  - da lech thi doi thanh `NEEDS_SYNC`
- Truoc khi dua vao bao cao, uu tien ra soat lai cac file khong o trang thai `VERIFIED`.
