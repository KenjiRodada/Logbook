# คู่มือนำ Logbook ขึ้น Hosting ด้วย Render + Aiven MySQL

## ก่อนเริ่ม
- ใช้ไฟล์โปรเจกต์นี้เป็น repository ที่ root ของ GitHub โดย `render.yaml`, `Dockerfile` และ `Logbook.csproj` ต้องอยู่ระดับเดียวกัน
- ไม่อัปโหลด `appsettings.Local.json` และห้ามใส่รหัสผ่านจริงใน GitHub
- โปรเจกต์ใน Visual Studio ยังคง `net9.0` เพื่อให้เข้ากับ Visual Studio 2022 แต่ Dockerfile จะ build เว็บไซต์ที่โฮสต์เป็น .NET 10 LTS

## A. ส่งโปรเจกต์ขึ้น GitHub
1. แตก ZIP แล้วเปิดโฟลเดอร์ `Logbook` ใน Visual Studio 2022 ด้วย `Logbook.sln`.
2. ใช้เมนู Git / Add to Source Control แล้ว Publish repository ไป GitHub แนะนำให้เลือก Private.
3. ตรวจสอบหน้า GitHub ว่ามี `render.yaml`, `Dockerfile`, `Logbook.csproj` อยู่ที่ root ของ repository.
4. ตรวจสอบว่าไม่มี `appsettings.Local.json` หรือรหัสผ่านอยู่ใน repository.

## B. สร้างเว็บไซต์บน Render
1. สมัคร/ลงชื่อเข้าใช้ Render ที่ https://render.com/.
2. จาก Dashboard เลือก New + > Blueprint.
3. เชื่อมบัญชี GitHub เลือก repository ของ Logbook ที่เพิ่ง Publish.
4. Render จะอ่าน `render.yaml` และขอค่า `ConnectionStrings__DefaultConnection`.
5. ใส่ connection string ด้วยค่าปัจจุบันจาก Aiven และใช้รหัสผ่านใหม่ที่เพิ่งหมุน (rotate) แล้ว รูปแบบตัวอย่าง:

   `Server=YOUR_AIVEN_HOST;Port=YOUR_AIVEN_PORT;Database=defaultdb;User ID=YOUR_AIVEN_USER;Password=YOUR_NEW_PASSWORD;SslMode=Required;`

6. กดยืนยัน Deploy Blueprint. Render จะ build ด้วย Dockerfile แล้วเผยแพร่เว็บ URL รูปแบบ `https://logbook-....onrender.com`.
7. เมื่อ Deploy เสร็จ เปิด Logs ตรวจว่า build และ startup ผ่าน แล้วลองเปิด `/health` ควรได้ HTTP 200.

หากไม่ใช้ Blueprint: เลือก New + > Web Service, เชื่อม GitHub repository, เลือก Runtime Docker แล้วตั้ง Environment Variables สองค่า `ASPNETCORE_ENVIRONMENT=Production` และ `ConnectionStrings__DefaultConnection` เป็น connection string ข้างต้น.

## C. อนุญาตการเชื่อมต่อจาก Render ไป Aiven
1. หลังสร้าง Web Service แล้ว เปิดหน้า service ใน Render > Connect > Outbound แล้วคัดลอก outbound IP ranges ทั้งหมดที่ Render แสดงสำหรับ service/region นี้.
2. ไป Aiven Console > MySQL service > Service settings > IP address allowlist และเพิ่ม CIDR ranges ของ Render.
3. หากยังใช้ MySQL Workbench จากเครื่องตัวเอง ให้เก็บ public IP ของเครื่องตัวเองไว้ใน allowlist ด้วย มิฉะนั้น Workbench อาจเชื่อมต่อไม่ได้.
4. อย่าเปิด allowlist `0.0.0.0/0` ค้างไว้ถ้าสามารถจำกัดเป็น IP ranges ที่จำเป็นได้.
5. ตรวจสอบว่า MySQL service เปิด public access และสถานะ Running แล้ว.

## D. ทดสอบ
- `/` หรือ `/Dashboard`: Dashboard สาธารณะตามข้อกำหนดเดิม
- `/Account/Login`: เข้าสู่ระบบจากตาราง `web_users`
- `/health`: health check ของ Hosting
- หลัง Login ลอง `/Household`, `/Assistance` และ `/Triage`.

ถ้าข้อมูลไม่ขึ้น ให้ดู Render > Logs; ตรวจ `ConnectionStrings__DefaultConnection`, allowlist Aiven และว่ารัน SQL `Database/001_Create_Assistance_History.sql` แล้วหากใช้หน้าประวัติการช่วยเหลือ.

## ข้อควรระวัง
- Render Free จะพัก service หลังไม่มี traffic ประมาณ 15 นาที และคำขอถัดไปอาจใช้เวลาประมาณหนึ่งนาทีในการตื่นขึ้นมา เหมาะกับทดลอง/เดโม ไม่เหมาะกับเว็บผลิตจริงที่ต้องพร้อมใช้งานตลอดเวลา.
- ฐานข้อมูลเกี่ยวข้องกับข้อมูลครัวเรือน สุขภาพ และกลุ่มเปราะบาง ควรทดลองด้วยข้อมูลที่ไม่ระบุตัวบุคคลก่อนเผยแพร่ ตรวจสิทธิ์ `admin/amphur/staff` ให้ครอบคลุมทุกหน้า และใช้แผน Hosting ที่เหมาะกับการปกป้องข้อมูลจริงก่อนเปิดใช้งานจริง.
- รหัสผ่าน Aiven เคยถูกส่งในแชต จึงควรเปลี่ยนรหัสผ่านก่อนนำขึ้น Hosting และใช้รหัสใหม่เฉพาะใน Environment Variable ของ Render.
- ตัวโปรเจกต์ในเครื่องยัง target `net9.0` เพื่อ Visual Studio 2022; Dockerfile สำหรับ Hosting ใช้ .NET 10 LTS. Microsoft ระบุว่า .NET 9 สิ้นสุดการสนับสนุนวันที่ 10 พฤศจิกายน 2026. ควรวางแผนอัปเกรดเครื่องพัฒนา/Visual Studio เป็นรุ่นที่รองรับ .NET 10 ในภายหลัง.
