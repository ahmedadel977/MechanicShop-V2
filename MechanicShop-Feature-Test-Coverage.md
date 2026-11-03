# تقرير التغطية النهائي لـ MechanicShop.Application

**تاريخ القياس:** 2 نوفمبر 2026  
**النطاق:** Production Source داخل `MechanicShop.Application` فقط. تم استبعاد `bin` و`obj` و`Migrations` والملفات المولدة والاختبارات.  
**مصدر الأرقام:** OpenCover الناتج من تشغيل فعلي ناجح لجميع Test Projects، مع تقرير مستقل لـ`SubcutaneousTests`.

## النتائج التنفيذية المؤكدة

| مجموعة الاختبارات | Line Coverage | Branch Coverage | Method Coverage | الاختبارات |
| --- | ---: | ---: | ---: | ---: |
| `SubcutaneousTests` فقط | 1596/1774 = **89.97%** | 240/360 = **66.67%** | 158/176 = **89.77%** | 180 Passed، 0 Failed، 0 Skipped |
| اتحاد كل Test Projects بعد إزالة التكرار | 1662/1774 = **93.69%** | 255/360 = **70.83%** | 166/176 = **94.32%** | 332 Passed، 0 Failed، 0 Skipped |

التغطية الهيكلية (وجود اختبار مباشر للـHandler/Validator/Query/Command) مختلفة عن Code Coverage: وجود اختبار مباشر لا يعني تنفيذ كل الأسطر والفروع داخل التنفيذ.

## تغطية Code لكل Feature

القيم في الأعمدة هي **SubcutaneousTests / اتحاد كل Test Projects**. الأعداد هي النقاط المغطاة/الإجمالية، والنسب محسوبة من النقاط الفريدة بعد دمج التقارير.

| Feature | Line Coverage | Branch Coverage | Method Coverage |
| --- | ---: | ---: | ---: |
| Billing | 91.16% (196/215) / 93.95% (202/215) | 86.36% (19/22) / 86.36% (19/22) | 84.21% (16/19) / 94.74% (18/19) |
| Customers | 93.85% (229/244) / 95.08% (232/244) | 78.12% (25/32) / 78.12% (25/32) | 90.32% (28/31) / 93.55% (29/31) |
| Dashboard | 100% (79/79) / 100% (79/79) | 75.00% (12/16) / 75.00% (12/16) | 100% (3/3) / 100% (3/3) |
| Identity | 92.21% (71/77) / 92.21% (71/77) | 90.91% (20/22) / 90.91% (20/22) | 100% (7/7) / 100% (7/7) |
| Labors | 93.75% (15/16) / 93.75% (15/16) | لا توجد فروع مسجلة (0/0) | 87.50% (7/8) / 87.50% (7/8) |
| RepairTasks | 94.02% (236/251) / 95.22% (239/251) | 76.92% (20/26) / 76.92% (20/26) | 90.32% (28/31) / 93.55% (29/31) |
| Scheduling | 98.89% (89/90) / 98.89% (89/90) | 93.75% (15/16) / 93.75% (15/16) | 85.71% (6/7) / 85.71% (6/7) |
| WorkOrders | 87.60% (544/621) / 90.50% (562/621) | 59.00% (118/200) / 60.50% (121/200) | 90.91% (40/44) / 95.45% (42/44) |
| ApplicationOther | 75.69% (137/181) / 95.58% (173/181) | 42.31% (11/26) / 88.46% (23/26) | 88.46% (23/26) / 96.15% (25/26) |

`Dashboard` هو الاسم المعروض للمجلد `Dashbord`، و`RepairTasks` للاسم الموجود `ReaparTasks`. `ApplicationOther` يضم ملفات Application المشتركة خارج مجلدات Features.

## أقل Features تغطية

### Line Coverage الأقل — اتحاد كل المشاريع

| الترتيب | Feature | Line Coverage |
| ---: | --- | ---: |
| 1 | WorkOrders | 90.50% |
| 2 | Identity | 92.21% |
| 3 | Labors | 93.75% |
| 4 | Billing | 93.95% |
| 5 | Customers | 95.08% |
### Branch Coverage الأقل — اتحاد كل المشاريع

| الترتيب | Feature | Branch Coverage |
| ---: | --- | ---: |
| 1 | WorkOrders | 60.50% |
| 2 | Dashboard | 75.00% |
| 3 | RepairTasks | 76.92% |
| 4 | Customers | 78.12% |
| 5 | Billing | 86.36% |
> الترتيب العددي في الجدول هو تصاعدي حسب النسبة؛ `Labors` مستبعد من ترتيب Branch لأن الأداة لم تسجل له أي Branch Points.

## Feature/Handler وValidator Coverage

الجرد مبني على أنواع التنفيذ الموجودة في Production، وليس عدد ملفات الاختبار. تشمل أعداد Handler الاثنين Event Handlers في WorkOrders.

| Feature | Handlers المختبرة/الإجمالي | Validators المختبرة/الإجمالي | Queries المختبرة/الإجمالي | Commands المختبرة/الإجمالي |
| --- | ---: | ---: | ---: | ---: |
| Billing | 4/4 (100%) | 4/4 (100%) | 2/2 (100%) | 2/2 (100%) |
| Customers | 5/5 (100%) | 6/6 (100%) | 2/2 (100%) | 5/5 (100%) |
| Dashboard | 1/1 (100%) | 1/1 (100%) | 1/1 (100%) | 0/0 |
| Identity | 3/3 (100%) | 1/1 (100%) | 3/3 (100%) | 0/0 |
| Labors | 1/1 (100%) | 0/0 | 1/1 (100%) | 0/0 |
| RepairTasks | 5/5 (100%) | 6/6 (100%) | 2/2 (100%) | 4/4 (100%) |
| Scheduling | 1/1 (100%) | 0/0 | 1/1 (100%) | 0/0 |
| WorkOrders | 10/10 (100%) | 7/7 (100%) | 2/2 (100%) | 6/6 (100%) |
| **الإجمالي** | **30/30 (100%)** | **25/25 (100%)** | **14/14 (100%)** | **17/17 (100%)** |

تُعدّ Commands وQueries كأنواع Request. ثلاثة من أنواع Command تُستخدم كمدخلات متداخلة ولا تملك Handler منفصلًا؛ لذلك عدد Command Handler الفعلي أقل من عدد أنواع Commands. أعداد التغطية الهيكلية لا تقيس اكتمال قواعد كل Validator أو جميع فروع Handler.

## ملخص سيناريوهات Handler

السيناريوهات التالية تحققت من خلال اختبارات السلوك الفعلية؛ لا يعني ذلك أن كل فرع محتمل مغطى. التفاصيل المتبقية تستند إلى مسارات الإنتاج التي لم تنفذها تقارير OpenCover والاختبارات الحالية.

| Feature | سيناريوهات موجودة بالاختبارات | فجوات سيناريوهات/فروع بارزة |
| --- | --- | --- |
| Billing | إصدار وتسوية الفاتورة، Not Found، منع إعادة الإصدار/التسوية، الحسابات والحفظ | حالات حدود المبالغ والضرائب والتنافس المتزامن |
| Customers | الإنشاء والتحديث والحذف والاستعلام، تكرار البريد، ملكية المركبات والحفظ | حالات مركبات إضافية/قوائم حدودية والتنافس على الملكية |
| Dashboard | إحصاءات بحالات وفواتير مختلطة، عدم وجود فواتير والإيراد الصفري | فروع تاريخ/حالة إضافية حسب بيانات الإنتاج |
| Identity | إنشاء Token، بيانات اعتماد غير صحيحة، Refresh صالح/منتهي/غير معروف/مستخدم مختلف، UserId غير صالح | حالات Token/claims غير القياسية وفروع أخطاء الهوية الأخرى |
| Labors | الاستعلام والتصفية الأساسية | مسارات نتيجة فارغة/قيم بحث وأدوار إضافية |
| RepairTasks | الإنشاء والتعديل والحذف والاستعلام، أجزاء ناقصة/مكررة ومدخلات Validator الفارغة | حدود أجزاء وتكلفة/مدة وحالات قاعدة البيانات النادرة |
| Scheduling | الجدول اليومي مع وقت/موعد مشغول وLabor اختياري | تداخلات وأطراف الوقت/التاريخ وتعدد المواعيد |
| WorkOrders | إنشاء بلا Labor أو به، إسناد، حذف حسب الحالة، نقل وتعارضات، تحديث الحالة والمهام، استعلامات متعددة، Event Handlers وإشعاراتها | نسبة Branch الأقل؛ لم تغطّ كل انتقال حالة/توقيت/تداخل أو كل أخطاء الإشعار وإعادة المحاولة |
| ApplicationOther | سلوك أخطاء ومكونات مشتركة مستخدمة عبر Features | بعض فروع الأخطاء والـLogging/behavior metadata لم تُنفذ |

### Handlers وValidators بلا اختبارات مباشرة

وفق جرد الكود وفئات الاختبار الحالية، لا يوجد Handler أو Validator بلا اختبار مباشر. يتضمن ذلك `WorkOrderCollectionModifiedEventHandler` و`SendWorkOrderCompletedEmailHandler` واختبارات Validator لـ`RemoveRepairTaskCommandValidator`. هذا لا ينفي وجود حالات تفصيلية غير مغطاة موضحة أعلاه.

## البناء وتشغيل الاختبارات

- `dotnet build MechanicShop.slnx --no-restore -v:q`: **نجاح، 0 أخطاء**. ظهرت تحذيرات NuGet vulnerability (`System.Security.Cryptography.Pkcs` و`System.Security.Cryptography.Xml`) وتحذيرات تحليل/Nullable؛ لم تُخفَ أو تُعدّل في مهمة قياس التغطية.
- `dotnet test tests/MechanicShop.Application.SubcutaneiousTests/MechanicShop.Application.SubcutaneiousTests.csproj --no-build --no-restore -v:q`: **180 Passed، 0 Failed، 0 Skipped**.
- `dotnet test MechanicShop.slnx --no-build --no-restore --coverlet ...`: **332 Passed، 0 Failed، 0 Skipped** عبر أربعة Test Projects.
- Testcontainers وSQL Server اشتغلا في التشغيل؛ لا يوجد اختبار محجوب بسبب Docker في التحقق النهائي.

## ملفات OpenCover الخام

تم الاحتفاظ بالتقارير الخام دون تعديلها تحت `TestResults/` لإمكانية إعادة الحساب:

- `subcutaneous-final.coverage.opencover.021126135505362.xml` — قياس SubcutaneousTests المنفصل.
- `allprojects-final.coverage.opencover.021126135528177.xml`
- `allprojects-final.coverage.opencover.021126135529060.xml`
- `allprojects-final.coverage.opencover.021126135530893.xml`
- `allprojects-final.coverage.opencover.021126135545019.xml` — تقارير التشغيل لجميع Test Projects.

جُمعت النقاط المكررة بأقصى hit count لكل مفتاح فريد: `Source File + Line` للـLine، و`Source File + Method + IL Offset + Branch Path` للـBranch، و`Source File + Method` للـMethod. جمع تقارير التشغيل يزيل التكرار ولا يضيف Test Project إلى المقام مرتين. تقارير Domain/API لا تضيف نقاطًا من `MechanicShop.Application`؛ تقارير Application Unit وSubcutaneous تساهم في اتحاد التغطية.

## توصيات عملية

1. الأولوية لرفع Branch في `WorkOrders` (60.50% على اتحاد المشاريع) عبر الحالات غير المنفذة لانتقالات الحالة والتعارضات الزمنية/الاستثناءات، مع الحفاظ على قواعد العمل.
2. افحص الفروع المشتركة المتبقية في `ApplicationOther` و`RepairTasks` و`Dashboard` واكتب اختبارات للسلوك ذي القيمة فقط.
3. ارفع Subcutaneous Line/Method إلى 90% على الأقل بإضافة اختبارات تستهدف النقاط المتبقية ذات المعنى؛ القياس الحالي قريب من الهدف لكنه أقل منه قليلًا. لا تضف اختبارات شكلية لمجرد النسبة.
4. أعد توليد OpenCover بعد أي تغييرات اختبارات، واحتفظ بالتقارير الخام منفصلة حسب Test Project قبل تكوين الاتحاد.


