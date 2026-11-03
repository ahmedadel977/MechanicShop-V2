# تقرير مراجعة التحذيرات والتغطية — MechanicShop

**تاريخ التحقق:** 3 نوفمبر 2026
**Target Framework:** `.NET 10`
**النطاق:** مشاريع الحل وكل `Production Code` داخل `MechanicShop.Application`.

## الملخص

| التحقق | النتيجة |
| --- | --- |
| التحذيرات قبل الإصلاح | 70 |
| التحذيرات بعد الإصلاح | 4، وكلها تشخيصات API versioning/OpenAPI غير أمنية |
| أخطاء Build | 0 |
| الاختبارات | 412 Passed، 0 Failed، 0 Skipped |
| حزم معرضة حسب `dotnet list package --vulnerable --include-transitive` | لا توجد في أي مشروع |

## تصنيف التحذيرات والإصلاحات

الـ70 تحذيرًا السابقة تضمنت تحذيرات NuGet متكررة عبر المشاريع التابعة، وتحذير تعذر جلب بيانات NuGet في محاولة Restore، وتحذيرات كود وNullable وAPI versioning.

| الفئة | المعالجة |
| --- | --- |
| `System.Security.Cryptography.Pkcs 10.0.0`، تنبيهات متوسطة وعالية | تثبيت النسخة المصححة `10.0.13` في Infrastructure؛ النسخ حتى `10.0.12` متأثرة، و`10.0.13` هي النسخة المصححة حسب [GHSA-846f-f764-76mx](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx) و[GHSA-2fmv-rv2c-59x7](https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7). |
| `System.Security.Cryptography.Xml 9.0.18`، تنبيهات عالية | تحديث مباشر إلى `9.0.21`، وهي النسخة المصححة لفرع 9.x حسب [GHSA-7hqp-7c5m-4vq6](https://github.com/dotnet/runtime/security/advisories/GHSA-7hqp-7c5m-4vq6)، [GHSA-c399-9853-6jmj](https://github.com/dotnet/runtime/security/advisories/GHSA-c399-9853-6jmj)، و[GHSA-r955-8q5j-3h83](https://github.com/dotnet/runtime/security/advisories/GHSA-r955-8q5j-3h83). |
| `Microsoft.AspNetCore.SignalR.Common 10.0.2` متعدية من SignalR Client | تحديث `Microsoft.AspNetCore.SignalR.Client` إلى `10.0.13`؛ النسخة المصححة من Common هي `10.0.13` حسب [GHSA-334f-vvp4-7g6p](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-334f-vvp4-7g6p). |
| `CA2017` في تسجيل فشل تسجيل الدخول | إضافة قيمة `Errors` الناقصة إلى معاملات رسالة Logging. |
| `CS8602` في حذف Customer وإحصاءات Dashboard | إضافة فحوص صريحة للعلاقات nullable قبل الوصول إلى بياناتها. |
| `CS8604` في Authorization وInvoice PDF | رفض Authorization عند غياب اسم Policy، وجعل محتوى PDF واسمه قيمًا مطلوبة وغير nullable؛ endpoint يستخدم `ContentType` من DTO. |
| تحذيرات Nullable/غير مستخدم في الاختبارات | حماية assertion من null، حذف متغير غير مستخدم، واستخدام بيانات ثابتة في طلب Unauthorized. |
| تحذيرات `AV0016` و`AV0029` و`AV0030` | بقيت 4 instances تتعلق بإعدادات API versioning وتوليد OpenAPI. لم أغير إعدادات routes أو مستندات API لتفادي تغيير سلوكها ضمن مهمة معالجة التحذيرات. |

تحقق NuGet النهائي أعلن عدم وجود حزم معرضة في المشاريع الإحدى عشر. لم يتغير `TargetFramework` ولم تُضف suppressions للتحذيرات.

## نتائج Build والاختبارات

- `dotnet clean MechanicShop.slnx`: نجح.
- `dotnet build MechanicShop.slnx --no-restore`: نجح، 0 أخطاء و4 تحذيرات API versioning/OpenAPI.
- `dotnet list MechanicShop.slnx package --vulnerable --include-transitive`: لا توجد حزم معرضة في المشاريع الإحدى عشر.

| Test Project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| `MechanicShop.Api.IntegrationTests` | 81 | 0 | 0 |
| `MechanicShop.Application.SubcutaneiousTests` | 180 | 0 | 0 |
| `MechanicShop.Application.UniTests` | 46 | 0 | 0 |
| `MechanicShop.Domain.UniTests` | 105 | 0 | 0 |
| **الإجمالي** | **412** | **0** | **0** |

## Coverage لكل Feature

تم حساب Coverage من ملفات `Production Source` تحت `MechanicShop.Application` فقط. استُبعدت الاختبارات و`bin` و`obj` و`Migrations` والملفات المولدة. جُمعت النتائج من تقارير OpenCover الأربعة، مع إزالة التكرار حسب ملف المصدر والسطر والفرع والـmethod. عمود Subcutaneous يستخدم تقريره وحده.

| Feature | Line — كل المشاريع | Branch — كل المشاريع | Method — كل المشاريع | Line — Subcutaneous | Branch — Subcutaneous |
| --- | ---: | ---: | ---: | ---: | ---: |
| Billing | 205/218 — 94.04% | 19/22 — 86.36% | 18/19 — 94.74% | 199/218 — 91.28% | 19/22 — 86.36% |
| Customers | 231/243 — 95.06% | 25/32 — 78.12% | 29/31 — 93.55% | 228/243 — 93.83% | 25/32 — 78.12% |
| Dashboard | 83/83 — 100% | 12/16 — 75.00% | 3/3 — 100% | 83/83 — 100% | 12/16 — 75.00% |
| Identity | 71/77 — 92.21% | 20/22 — 90.91% | 7/7 — 100% | 71/77 — 92.21% | 20/22 — 90.91% |
| Labors | 15/16 — 93.75% | N/A (0/0) | 7/8 — 87.50% | 15/16 — 93.75% | N/A (0/0) |
| RepairTasks | 239/251 — 95.22% | 20/26 — 76.92% | 29/31 — 93.55% | 236/251 — 94.02% | 20/26 — 76.92% |
| Scheduling | 89/90 — 98.89% | 15/16 — 93.75% | 6/7 — 85.71% | 89/90 — 98.89% | 15/16 — 93.75% |
| WorkOrders | 577/621 — 92.91% | 138/200 — 69.00% | 42/44 — 95.45% | 544/621 — 87.60% | 118/200 — 59.00% |
| ApplicationOther | 176/181 — 97.24% | 24/26 — 92.31% | 25/26 — 96.15% | 137/181 — 75.69% | 11/26 — 42.31% |
| **Overall Application** | **1686/1780 — 94.72%** | **273/360 — 75.83%** | **166/176 — 94.32%** | **1602/1780 — 90.00%** | **240/360 — 66.67%** |

## مقارنة بالـbaseline السابق

الـbaseline مأخوذ من تقرير التغطية السابق الموجود في Git history قبل حذفه. أُعيد استخدام نفس نطاق `MechanicShop.Application` وطريقة إزالة التكرار.

| القياس | السابق | الحالي | التغير |
| --- | ---: | ---: | ---: |
| Subcutaneous Line | 1596/1774 — 89.97% | 1602/1780 — 90.00% | +0.03 نقطة |
| Subcutaneous Branch | 240/360 — 66.67% | 240/360 — 66.67% | دون تغيير |
| Subcutaneous Method | 158/176 — 89.77% | 158/176 — 89.77% | دون تغيير |
| اتحاد كل المشاريع — Line | 1662/1774 — 93.69% | 1686/1780 — 94.72% | +1.03 نقطة |
| اتحاد كل المشاريع — Branch | 255/360 — 70.83% | 273/360 — 75.83% | +5.00 نقاط |
| اتحاد كل المشاريع — Method | 166/176 — 94.32% | 166/176 — 94.32% | دون تغيير |

زادت نقاط الأسطر الإجمالية من 1774 إلى 1780 بسبب تعديلات المصدر التي أصلحت التحذيرات؛ الأسطر الجديدة مغطاة في التشغيل الحالي. تحسن WorkOrders في Branch ناتج عن اتحاد تقارير الاختبارات الحالية، وليس عن تغيير قواعد WorkOrders.

## أقل Features تغطية

| الترتيب | أقل Line Coverage | النسبة | أقل Branch Coverage | النسبة |
| ---: | --- | ---: | --- | ---: |
| 1 | Identity | 92.21% | WorkOrders | 69.00% |
| 2 | WorkOrders | 92.91% | Dashboard | 75.00% |
| 3 | Labors | 93.75% | RepairTasks | 76.92% |
| 4 | Billing | 94.04% | Customers | 78.12% |
| 5 | Customers | 95.06% | Billing | 86.36% |

لا تدخل `Labors` في ترتيب Branch لأن OpenCover لم يسجل له أي branch points.

## ملفات Coverage الخام

لكل Test Project ملفا OpenCover وCobertura حديثان تحت `CodeCovArtifacts/`. ملفات OpenCover محفوظة كمصدر خام للمراجعة وإعادة الحساب. ملفات Cobertura الخام تعرض assemblies التي وصلت إليها عملية الاختبار؛ أما جداول هذا التقرير فمفلترة إلى `MechanicShop.Application` فقط.
