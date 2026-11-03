# التدقيق النهائي للاختبارات — MechanicShop

**تاريخ التدقيق:** 2 نوفمبر 2026

## A. نتائج التشغيل

تم تنفيذ Restore، وبناء الـsolution، وتشغيل كل Test Project منفصلًا، ثم تشغيل المجموعة كاملة مع OpenCover. تم تشغيل SubcutaneousTests مرتين إضافيتين متتاليتين للتحقق من التذبذب.

| Test Project | Passed | Failed | Skipped | Build |
| --- | ---: | ---: | ---: | --- |
| MechanicShop.Domain.UniTests | 105 | 0 | 0 | نجح |
| MechanicShop.Application.UniTests | 46 | 0 | 0 | نجح |
| MechanicShop.Application.SubcutaneiousTests | 180 | 0 | 0 | نجح |
| MechanicShop.Api.IntegrationTests | 1 | 0 | 0 | نجح |
| **التشغيل الكامل للـsolution** | **332** | **0** | **0** | **نجح، 0 أخطاء** |

تكرار SubcutaneousTests: التشغيل الأول 180/180، والثاني 180/180. تشغيل الـsolution الكامل مع Coverlet: 332 Passed، 0 Failed، 0 Skipped. لا يوجد اختبار فاشل أو متخطٍ أو محجوب بالبنية التحتية.

Restore نجح بعد السماح بقراءة NuGet.Config الشخصي. المحاولة الأولى داخل الـsandbox فشلت بسبب Access denied؛ لم يتغير ملف الإعدادات أو إصدارات الحزم.

## B. مقارنة Coverage بالـBaseline السابق

قيم Previous محفوظة كما طُلب. أُعيد القياس من تقارير OpenCover جديدة بعد تعديلات التدقيق.

| Metric | Previous | Final | Difference |
| --- | ---: | ---: | ---: |
| Subcutaneous Line | 89.97% | 89.97% | 0.00 نقطة مئوية |
| Subcutaneous Branch | 66.67% | 66.67% | 0.00 نقطة مئوية |
| Subcutaneous Method | 89.77% | 89.77% | 0.00 نقطة مئوية |
| All Projects Line | 93.69% | 93.69% | 0.00 نقطة مئوية |
| All Projects Branch | 70.83% | 70.83% | 0.00 نقطة مئوية |
| All Projects Method | 94.32% | 94.32% | 0.00 نقطة مئوية |

القيم النهائية: SubcutaneousTests ‏1596/1774 Line، 240/360 Branch، 158/176 Method؛ اتحاد المشاريع ‏1662/1774 Line، 255/360 Branch، 166/176 Method. لم يتغير عدد نقاط التغطية لأن إصلاحات الإشعارات غيّرت توقيت dispatch، لا نقاط Production الفريدة المنفذة.

تقارير OpenCover الخام الجديدة محفوظة في TestResults:
- final-audit-subcutaneous.coverage.opencover.021126141008809.xml — SubcutaneousTests فقط.
- final-audit.coverage.opencover.021126140910136.xml
- final-audit.coverage.opencover.021126140910172.xml
- final-audit.coverage.opencover.021126140912564.xml
- final-audit.coverage.opencover.021126140931812.xml — تقارير كل Test Projects من التشغيل الكامل.

## C. تدقيق WorkOrders

### القواعد التي راجعتها الاختبارات

- State Management: انتقال Scheduled → InProgress → Completed، رفض تخطي InProgress، رفض بدء الأمر قبل موعده، رفض إكمال الأمر مرتين، ومنع تعديل/حذف الحالات غير القابلة للتعديل.
- Scheduling: إنشاء ونقل أمر بموعد متاح، العمل بدون Labor، تعارض Spot وVehicle وLabor، والحفاظ على الموعد الحالي عند الفشل. لا تغطي الاختبارات كل حدود التداخل واختلافات UTC/التوقيت المحلي.
- Data Integrity: كيانات مرتبطة مفقودة، RepairTask IDs مكررة، تحديث المهام، حفظ الحالة والموعد بعد النجاح، وعدم حفظ الجدول عند تعارض النقل. اختبار إنشاء WorkOrder بلا Labor يؤكد الاستمرار في إسناده لاحقًا.
- Notifications: إشعار إكمال العميل بالبريد وSMS، رفض التكرار دون إرسال إشعار ثانٍ، إشعار تغير القائمة بعد النقل والحذف وتغيير الحالة وإسناد Labor، تمرير CancellationToken في Event Handler، وظهور فشل notifier/email.

### عيوب مثبتة وإصلاحاتها

1. DeleteWorkOrderCommandHandler وUpdateWorkOrderStateCommandHandler كانا يضيفان WorkOrderCollectionModified بعد SaveChangesAsync. AppDbContext يوزع Domain Events أثناء الحفظ، ولذلك لم يصل الحدث إلى notifier. نُقل إنشاء الحدث إلى ما قبل الحفظ، وأضيفت assertions تكاملية للتحقق من استدعاء notifier مرة واحدة.
2. RelocateWorkOrderCommandHandler كان يضيف الحدث مرة قبل الحفظ ومرة بعده؛ النسخة الثانية كانت تظل معلقة. أزيلت الإضافة المكررة، واختبار النقل يتحقق من إشعار واحد.
3. AssignLaborCommandHandler لم يكن يرسل إشعار تغير القائمة بعد تغيير Labor. أضيف الحدث قبل الحفظ، واختبار SQL Server يتحقق من الإشعار ومن استمرار حفظ الإسناد.
4. RelocateWorkOrderCommandHandler كان يفحص نتيجة UpdateTiming بدل نتيجة UpdateSpot عند معالجة فشل تغيير Spot. أصبح يفحص النتيجة الصحيحة ويرجع أخطاءها. مسار Spot غير الصالح يُرفض عادة بالـValidator؛ التصحيح يحمي الـHandler عند فشل عملية التحديث نفسها.

### الفروع المتبقية

Branch Coverage لـWorkOrders هو 59.00% داخل SubcutaneousTests و60.50% لاتحاد كل المشاريع. أهم الفجوات في بعض بدائل GetWorkOrdersQueryHandler (الفلاتر/الفرز والبحث)، وحالات الفشل المركبة لتحديث RepairTasks، وفروع AssignLabor وRelocateWorkOrder غير المنفذة، وحالات بيانات العميل غير المكتملة في إشعار الإكمال. لا أوصي بمطاردة 100%؛ الأولوية للفلاتر والفرز المستخدمين وحالات فشل قواعد العمل ذات الأثر.

## D. عزل الاختبارات والبنية

- WebAppFactoryCollection تستخدم Collection واحدة مع DisableParallelization=true؛ ده يمنع تضارب الوصول لقاعدة البيانات أثناء التنفيذ.
- كل تشغيل ينشئ SQL Server Testcontainer جديدًا، وتُجهّز قاعدة البيانات عبر Startup/EF، ويُغلق الـcontainer عند انتهاء الـfixture. التشغيلان المتتاليان نجحا.
- WebAppFactory يزيل Hosted Services غير اللازمة، ويستبدل Redis secondary cache بـDistributedMemoryCache، ويستبدل خدمات التنبيهات بـrecording services. ده مناسب لاختبارات Application.
- DI scopes التي تنشئها CreateMediator وCreateAppDbContext تُحفظ ثم تُتلف عند إغلاق الـfixture، وليس بعد كل اختبار؛ ده يراكم scopes/DbContexts طوال عمر الـfixture.
- قاعدة البيانات ليست معزولة لكل Test: الـfixture ينظف WorkOrders عند البداية فقط ولا يمسح كل الجداول بين الاختبارات. أغلب البيانات لها IDs مولدة والاختبارات تسلسلية، وكل تشغيل يبدأ Container جديدًا؛ لكن الاستعلامات غير المقيّدة قد ترى بيانات من اختبارات سابقة في نفس التشغيل.
- RecordingNotificationService وRecordingWorkOrderNotifier Singleton على مستوى الـfixture. الاختبارات التي تفحص القيم تمسحها يدويًا قبل التنفيذ، لكن لا يوجد Reset آلي لكل Test.
- Cache داخل العملية مشتركة طوال عمر الـfactory ولا يوجد Reset عام لكل Test؛ الإبطال يتم من Handlers.

## E. تغييرات Production التي راجعتها

الجدول يغطي ملفات Production الموجودة في working tree diff من مهمة الاختبارات السابقة، إضافة إلى إصلاحات WorkOrders لهذا التدقيق.

| الملفات | المشكلة والتعديل | مراجعة قواعد العمل والآثار |
| --- | --- | --- |
| MechanicShop.Domain/Workorders/Workorder.cs؛ MechanicShop.Infrastructure/Date/Configurations/WorkOrderConfiguration.cs؛ MechanicShop.Infrastructure/Migrations/AppDbContextModelSnapshot.cs؛ MechanicShop.Infrastructure/Migrations/20261102160000_MakeWorkOrderLaborOptional.cs | إنشاء WorkOrder كان يشترط Labor. أصبح LaborId اختياريًا وأضيفت Migration لدعم إنشاء الأمر ثم إسناد الفني لاحقًا. | Guid.Empty ما زال مرفوضًا، والمفتاح الأجنبي يتحقق عند وجوده. Down يمنع فقد البيانات إذا وجدت أوامر بلا Labor. الأثر المقصود هو السماح بأوامر غير مسندة. |
| MechanicShop.Application/Features/WorkOrders/common/CreateWorkORDER/CreateWorkOrderCommandHandler.cs؛ CreateWorkOrderCommandValidator.cs؛ MechanicShop.Application/Features/WorkOrders/Mappers/WorkOrderMapper.cs؛ MechanicShop.Application/Features/Scheduling/Queries/GetDailyScheduleQuery/GetDailyScheduleQueryHandler.cs؛ MechanicShop.Application/Features/WorkOrders/common/UpdateWorkOrderRepairTasks/UpdateWorkOrderRepairTasksCommandHandler.cs | الـhandlers وDTO mapping كانوا يفترضون وجود Labor؛ أصبحت عمليات البحث/التعارض مشروطة بوجوده والـmapping يقبل null. | Labor المقدم يظل مطلوبًا أن يكون صالحًا وغير مشغول؛ تعارض Vehicle وSpot مستقل. اختبارات السيناريو الاختياري نجحت. |
| MechanicShop.Application/Common/Errors/ApplicationErrors.cs؛ MechanicShop.Application/Features/Billing/Command/IssueInvoice/IssueInvoiceCommandHandler.cs | إعادة إصدار الفاتورة كانت تصل لفشل DB بدل Conflict متوقع؛ أضيف InvoiceAlreadyIssued وفحص قبل الإدخال. | يمنع التكرار التسلسلي، لكنه لا يمنع سباق طلبين متزامنين دون قيد فريد في DB. |
| MechanicShop.Application/Common/Errors/ApplicationErrors.cs؛ MechanicShop.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommandHandler.cs | تحديث العميل كان قد يسمح بنقل Vehicle يملكه عميل آخر؛ أضيف تحقق ملكية ويرجع Conflict. | يمنع النقل في الطلب العادي ويترك البيانات كما هي عند الرفض؛ لا يمنع سباق تحديث متزامن. |
| MechanicShop.Application/Features/Dashbord/Queris/GetWOkorderstats/GetWorkorderStatsQueryHandler.cs | جمع Invoice.Total ضمن EF query لم يكن مضمون الترجمة؛ أصبح الحساب من line items والخصم والضريبة. | الصيغة تطابق Invoice.Total في Domain. اختبارات Dashboard نجحت. يجلب إجماليات الفواتير المطابقة للذاكرة قبل التجميع؛ راقب الأداء مع زيادة حجم النتائج. |
| MechanicShop.Application/Features/Identity/Queries/GetUserInfo/GetUserByIdQueryHanlder.cs؛ MechanicShop.Infrastructure/identity/IdentityService.cs | UserId الفارغ كان يصل للخدمة، والمستخدم المفقود كان يرمي InvalidOperationException؛ أصبح الفارغ Validation Error والمفقود NotFound Result. | يظل سلوك المستخدم الموجود وأدواره كما هو؛ اختبارات حالات الإدخال والمستخدم غير الموجود نجحت. |
| MechanicShop.Application/Features/ReaparTasks/Queries/GerReapairTaskByid/GetRepairTaskByIdQuery.cs | Cache Tags وExpiration كانا يرميان NotImplementedException؛ أضيف Tag ومدة 10 دقائق. | يمنع الاستثناء عند قراءة cache metadata، ويلزم بقاء الإبطال على نفس Tag. |
| MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommandHandler.cs؛ DeleteWorkOrderCommandHandler.cs؛ RelocateWorkOrderCommandHandler.cs؛ UpdateOrderState/UpdateWorkOrderStateCommandHandler.cs | تغييرات هذا التدقيق موضحة في القسم C: dispatch مفقود/مكرر، حدث ناقص في AssignLabor، وفحص نتيجة Spot خاطئ. | الاختبارات الجديدة تثبت الإشعار مرة واحدة والحفظ؛ لم تتغير قواعد الحالة أو التداخل. |
| MechanicShop.Infrastructure/Srvices/InvoicePdfGenerator.cs | استبدال emoji بأيقونة SVG، تثبيت تنسيق التاريخ والأرقام، وتثبيت العملة على EGP بدل رمز العملة حسب ثقافة النظام. | يغيّر مخرجات PDF حسب الثقافة ويثبت العملة. لا يرتبط مباشرة بتدقيق الاختبارات ولا توجد assertions حالية على نص/شكل PDF؛ مبرره غير مثبت هنا ويحتاج قرار منتج واختبارًا منفصلًا. لم أغيّره في هذا التدقيق لأن مصدر التعديل السابق غير محسوم. |

تغييرات إعدادات/بنية مجاورة: MechanicShop.slnx يضيف مشاريع الاختبارات للحل؛ MechanicShop.Api/IAssemblyMarker.cs يحدد API assembly لـWebAppFactory؛ global.json يختار Microsoft.Testing.Platform. ملفات Api/Api.csproj وApi/Program.cs وملفات الإعدادات وlaunchSettings.json ظاهرة كمحذوفة بينما الحل الحالي يستخدم MechanicShop.Api. لم أنشئ هذه الحذوفات في جولة التدقيق ولا أقدر إثبات سببها؛ يلزم التأكد قبل commit أنها إزالة/نقل مقصود.

## F. Dependencies وConfiguration

- مشاريع الاختبارات الأربعة تستخدم xunit.v3 4.0.2 وxunit.runner.visualstudio 4.0.1. project.assets.json لا يشير إلى xunit.core أو xunit.assert الخاصين بـxUnit v2. تعارض CS0433 السابق غير موجود، والـBuild والتشغيل نجحا.
- المشاريع تستخدم UseMicrosoftTestingPlatformRunner=true، وglobal.json يختار Microsoft.Testing.Platform. dotnet test اشتغل فعليًا. Coverlet المستخدم coverlet.MTP 10.1.0 وأنتج OpenCover.
- لا يوجد Directory.Packages.props أو Directory.Build.props/targets في الجذر؛ إصدارات الحزم مباشرة في csproj. لم أرفع أي إصدار NuGet في التدقيق.
- كشف dotnet list package --vulnerable --include-transitive تحذيرات لا توقف البناء: System.Security.Cryptography.Pkcs 10.0.0 (High وModerate) كاعتماد عابر، وSystem.Security.Cryptography.Xml 9.0.18 (عدة High advisories) مباشر في Infrastructure وعابر في مشاريع أخرى. راجع advisories قبل CI؛ لم أغيّر الحزم تلقائيًا.
- global.json ملف جديد غير متعقب حاليًا ويحتوي اختيار MTP فقط؛ متوافق مع إعداد المشاريع، ويجب مراجعته وإضافته صراحةً قبل commit.

## G. فجوات ومخاطر متبقية

1. MechanicShop.Api.IntegrationTests/UnitTest1.cs يحتوي Fact فارغًا بلا Host أو HTTP request أو assertion؛ نتيجة 1/1 لا تثبت HTTP pipeline. أضف اختبار API حقيقي قبل اعتبار الطبقة مغطاة.
2. قاعدة البيانات والـCache لا يتم reset لهما لكل اختبار، والـscopes لا تُتلف إلا بنهاية الـfixture. مرتان متتاليتان نجحتا، لكن ذلك لا يثبت استقلال ترتيب الاختبارات بالكامل.
3. تغطية Subcutaneous أقل قليلًا من الهدف: Line أقل من 90% بـ0.03 نقطة، Method أقل من 90% بـ0.23 نقطة؛ Branch هي 66.67%. لا تطارد النسب بلا فائدة، واستهدف الفروع المهمة.
4. توجد advisories أمنية High للحزم المذكورة في القسم F.
5. تغيير PDF وحذف ملفات API مصدرها وقبولها غير محسومين في working tree؛ راجعها قبل الدمج.
6. فحص الفاتورة وملكية المركبة لا يغطي وحده التنافس المتزامن.

## Verdict

**NOT READY – CRITICAL ISSUES REMAIN.**

كل الاختبارات الحالية خضراء، والاختبارات الحساسة لإشعارات WorkOrders متحققة. لكن طبقة API لا تحتوي اختبار HTTP حقيقي، وهناك تبعيات عليها advisories High، وعزل قاعدة البيانات غير كامل، كما توجد تغييرات PDF وملفات API غير محسومة في الـworking tree. أوصي بحسم المخاطر الأمنية ومراجعة التغييرات، وإضافة اختبار API فعلي وتحسين reset/عمر scopes قبل اعتماد CI/CD كـquality gate.
