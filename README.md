# PDFHub — ระบบจัดเก็บข้อมูล Drawing และไฟล์ PDF

เว็บภายในโรงงานสำหรับเก็บข้อมูล Drawing แทนไฟล์ Excel `DataDrawingNMB 2026` เดิม ค้นหา เปิดดู PDF
และบันทึก/แก้ไขข้อมูลผ่านเบราว์เซอร์จากทุกเครื่องในเครือข่าย

## ความสามารถ

- รายการ Drawing: ค้นหาหลายคำพร้อมกัน, กรองตาม Section / ปี / PO / มีไฟล์หรือไม่, เรียงทุกคอลัมน์, Export Excel
- เปิด PDF ในหน้าเว็บ หรือแชร์ลิงก์ `http://<server>/PDFHub/pdf/NB-06618.pdf`
- เพิ่ม/แก้ไข Drawing ตรวจ PdfCode ตามกฎเดิมใน Excel (VBA) ทันทีขณะพิมพ์ และมีปุ่ม "บันทึกและเพิ่มรายการถัดไป"
- นำเข้าจาก Excel เดิม (ตรวจสอบก่อนบันทึก) และอัปโหลด PDF ทั้งโฟลเดอร์ โดยจับคู่ตามชื่อไฟล์
- ไฟล์ PDF เก่าที่ถูกแทนที่หรือลบจะถูกเก็บไว้ใน `_archive` เสมอ
- ผู้ใช้ 3 ระดับ: ผู้ดูแลระบบ / ผู้บันทึกข้อมูล / ผู้ดูข้อมูล (ผู้ดูทั่วไปไม่ต้อง login)
- สำรองฐานข้อมูลอัตโนมัติวันละครั้ง

## โครงสร้าง

```
src/
  PDFHub.Domain/          Entity, DTO, กฎ PdfCode — ไม่พึ่งพาเฟรมเวิร์กใด
  PDFHub.Application/     Service และ interface (business logic)
  PDFHub.Infrastructure/  EF Core + SQLite, เก็บไฟล์ PDF, อ่าน/เขียน Excel, งานสำรองข้อมูล
  PDFHub.Api/             Controllers, login (cookie), เสิร์ฟหน้า React
  PDFHub.Web/             React 19 + JavaScript + Vite + Tailwind 4
tests/PDFHub.Tests/       xUnit: API integration + กฎ PdfCode + import Excel
scripts/                  ติดตั้ง IIS, deploy, สำรองข้อมูล
Doc/DEPLOYMENT.md         คู่มือติดตั้งบน Windows Server
```

ออกแบบตาม skills ใน `~/.agents/skills`: `backend-aspnet-clean-architecture-net9`, `backend-ef-core-infrastructure`,
`frontend-react-admin-shell-architecture`, `frontend-tailwind-design-tokens`, `frontend-clean-enterprise-console-ui`,
`frontend-react-async-ui-states`, `frontend-react-form-draft-submit-validation`,
`frontend-server-authoritative-data-operations`, `tools-windows-iis-deploy`

## รันบนเครื่องพัฒนา

ต้องมี .NET 10 SDK และ Node.js 20+

```powershell
dotnet run --project src/PDFHub.Api --launch-profile http     # API ที่ http://localhost:5079
npm install --prefix src/PDFHub.Web
npm run dev --prefix src/PDFHub.Web                           # เว็บที่ http://localhost:5217
```

Vite ส่งต่อ `/api` และ `/pdf` ไปที่ API จึงเป็น origin เดียวกันเหมือนตอนใช้งานจริง
ฐานข้อมูลและ PDF ตอนพัฒนาอยู่ที่ `src/PDFHub.Api/App_Data/` · login ครั้งแรก `admin` / `admin` (ระบบบังคับเปลี่ยนรหัส)

## ทดสอบ

```powershell
dotnet test
npm run lint --prefix src/PDFHub.Web
npm run build --prefix src/PDFHub.Web
```

## ติดตั้งใช้งานจริง

ดู [Doc/DEPLOYMENT.md](Doc/DEPLOYMENT.md)

## ตั้งค่า (`appsettings.Production.json` บน server)

| Key | ค่าเริ่มต้น | ความหมาย |
|---|---|---|
| `PdfHub:DataRoot` | `App_Data` | โฟลเดอร์ฐานข้อมูล + PDF + สำรอง (บน server ใช้ `D:\PDFHubData`) |
| `PdfHub:RequireLoginToView` | `false` | `true` = ต้อง login ก่อนดูข้อมูลทุกหน้า |
| `PdfHub:MaxPdfSizeMB` | `100` | ขนาด PDF สูงสุด (ถ้าเพิ่ม ต้องแก้ `web.config` ด้วย) |
| `PdfHub:BackupKeepDays` | `30` | เก็บไฟล์สำรองฐานข้อมูลย้อนหลังกี่วัน (`0` = ปิด) |
