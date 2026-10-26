# Logbook – Designed UI

โปรเจกต์ ASP.NET Core MVC (.NET 9) ที่ออกแบบตามภาพตัวอย่าง โดยแยก Controllers / Models / Services / Data / Views / wwwroot

## หน้าเว็บที่ทำให้แล้ว
- Dashboard เปิดได้ก่อน Login
- หน้า Login ตรวจบัญชีจาก `web_users` และ BCrypt `password_hash`
- หลัง Login แสดง Sidebar และเมนูระบบ
- หน้าแสดงรายการครัวเรือนพร้อมค้นหาจากรหัสบ้าน/บ้านเลขที่/หมู่บ้าน
- หน้ารายละเอียดครัวเรือนและสมาชิก
- เมนูที่เหลือมีหน้า placeholder พร้อมลิงก์กลับ
- ถ้า Database ยังไม่เชื่อมต่อ Dashboard และหน้า list จะแสดงข้อมูลตัวอย่างเพื่อให้ดูหน้าตาได้

## การเปิดใน Visual Studio 2022
- ต้องใช้ Visual Studio 2022 เวอร์ชัน 17.12 ขึ้นไป และติดตั้ง workload `ASP.NET and web development` หรือมี .NET 9 SDK
1. แตกไฟล์ ZIP
2. เปิด `Logbook.sln`
3. คัดลอก `appsettings.Local.example.json` แล้วเปลี่ยนชื่อเป็น `appsettings.Local.json`
4. ใส่ Host, Port, User และรหัสผ่าน Aiven ของคุณในไฟล์ใหม่นี้
5. เลือกโปรไฟล์ `Logbook` ข้างปุ่ม Run แล้วกด `https` หรือ `http`
6. ถ้ายังไม่ได้ตั้ง Connection String ให้ Dashboard แสดงได้ แต่รายการจากฐานข้อมูลจริงจะยังไม่โหลด

ไฟล์ `appsettings.Local.json` อยู่ใน `.gitignore` และไม่ถูกรวมใน ZIP เพื่อป้องกันรหัสผ่านติดไปกับไฟล์ที่แชร์ หากไฟล์นี้มีอยู่ในเครื่องเดิมแล้ว ให้ตรวจสอบค่าในเครื่องนั้นได้โดยไม่ต้องคัดลอกทับ

## Preview หน้าเว็บโดยไม่ต้องตั้ง Database
เปิดไฟล์ `Preview/index.html` ด้วย Chrome เพื่อดูหน้าตัวอย่าง (Dashboard / Login / ครัวเรือน / รายละเอียด) และทดลองคลิกเมนูได้ทันที

## หมายเหตุด้านข้อมูล
- ตัวเลขในภาพตัวอย่างเป็นข้อมูล mockup ใช้สาธิตหน้าตาเท่านั้นเมื่อยังเชื่อมต่อฐานข้อมูลไม่ได้
- ตัวเลข Dashboard บางชุดในกราฟ และประกาศเป็นข้อมูลตัวอย่างสำหรับออกแบบ UI
- ฟังก์ชันเพิ่ม/แก้ไข/ลบแบบเต็ม และรายงานทุก View ยังเป็นงานพัฒนาขั้นต่อไป ไม่ได้สร้างการเปลี่ยนแปลงข้อมูลฐานข้อมูลในเวอร์ชันนี้


## ประวัติการช่วยเหลือ (เพิ่มในเวอร์ชันนี้)
- หน้ารายละเอียดครัวเรือนแสดงรายการช่วยเหลือ/คำร้องล่าสุดและสถานะสวัสดิการปัจจุบัน
- กด "ดูประวัติ" จากสมาชิกเพื่อกรองประวัติของคนนั้น พร้อมแสดงรายการช่วยเหลือระดับครัวเรือนร่วมด้วย
- เชื่อมข้อมูลเดิมจาก `repair_house_request`, `repair_medical_request` และ `member_welfare`
- เพิ่ม `assistance_history` สำหรับบันทึกเงิน สิ่งของ อุปกรณ์ หรือการช่วยเหลือจากโครงการอื่น ๆ
- ก่อนใช้งานบันทึกประวัติทั่วไป ให้รัน `Database/001_Create_Assistance_History.sql` ใน Aiven MySQL
- ตัวอย่างข้อมูลใน Preview/fallback เป็นข้อมูลจำลองเท่านั้น ไม่ถูกเพิ่มเข้า Database จริง
- คำร้องซ่อมแสดงสถานะตามจริง ไม่ตีความว่าได้รับความช่วยเหลือแล้วหากสถานะยังไม่เสร็จสิ้น

## ความปลอดภัย
- ไม่แจกจ่าย `appsettings.Local.json` ใน ZIP เพราะมีข้อมูลเชื่อมต่อส่วนตัว ให้คงไฟล์นี้ไว้เฉพาะเครื่องของคุณ
- ถ้า ZIP ใหม่ไม่มี `appsettings.Local.json` ให้คัดลอกไฟล์ตัวอย่างจาก `appsettings.Local.example.json` ไปเป็น `appsettings.Local.json` และใส่ connection string ใหม่

## เมนูข้อมูลที่เชื่อมกับตาราง/View
- สมาชิก: `t_house_member`
- สุขภาพ: `member_disease` + `m_disease`
- รายได้: `member_income` + `m_occupation`
- สวัสดิการ: `member_welfare` + `m_welfare_status`
- กลุ่มเปราะบาง: `member_vulnerable` + `m_vulnerable_type`
- คำร้องซ่อม: `repair_house_request` + `repair_medical_request`
- รายงาน: 12 View ที่มีใน Data Dictionary
- ข้อมูลอ้างอิง: ตาราง `m_*`
- จัดการผู้ใช้: `web_users` (แสดงเฉพาะ admin และไม่เลือกคอลัมน์รหัสผ่าน)

หากเชื่อมต่อฐานข้อมูลไม่ได้ หน้ารายการจะแสดงข้อมูลจำลองและมีป้ายแจ้งชัดเจนแทนการแสดงตารางว่าง ข้อมูลจำลองไม่ได้ถูก insert เข้า Database จริง


## Search + vulnerable household triage

- Data-table pages now have local search controls; household and household-picker searches also submit to the server.
- New page: `/Triage` (login required), linked from the sidebar. It dynamically lists types from `m_vulnerable_type`; it does not hardcode the category names.
- Suggested levels: Red = urgent review; Yellow = should receive needs assessment; Green = advice/monitoring. Reasons are displayed on each household row.
- This is a transparent, configurable screening aid—not an official policy and not an automatic eligibility decision. Staff must verify the current situation. It uses recorded open repair requests, disability/chronic/self-reliance flags, number of registered vulnerable members/types, and available assistance/welfare records. Missing `assistance_history` never counts as proof that no assistance was received.
- Aiven / database data is never seeded with the demo rows shown by Preview or fallback views.

## ปรับปรุงการอ่านและขนาดปุ่ม (26 ตุลาคม 2569)
- เพิ่มขนาดตัวอักษรภาษาไทยในหัวข้อ ตาราง ฟอร์ม เมนู และคำอธิบาย
- ปรับปุ่มและช่องค้นหาให้สูงอย่างน้อย 44px และเพิ่ม padding ให้สัมพันธ์กับตัวอักษร
- ปรับสถานะสีแดง/เหลือง/เขียวและตัวกรองให้มองเห็นชัดขึ้น
- เพิ่ม keyboard focus ที่มองเห็นได้ และคง responsive สำหรับมือถือ


## นำเว็บไซต์ขึ้น Hosting (Render + Aiven)
อ่าน `DEPLOY_RENDER.md` สำหรับขั้นตอน Publish ไป GitHub, Deploy ด้วย Render Blueprint, ตั้งค่า Environment Variable และอนุญาต IP ranges ใน Aiven. ห้ามบันทึกรหัสผ่านจริงลง GitHub.
