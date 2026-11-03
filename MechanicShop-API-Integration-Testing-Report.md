# تقرير اختبارات API Integration لمشروع MechanicShop

## الحكم النهائي

**API Integration الجولة الأخيرة ناجحة**

آخر تشغيل موثق للمجموعة بعد استكمال سيناريوهات الـ API سجّل **80 Passed / 0 Failed / 0 Skipped**. هذا لا يعني اكتمال كل business scenarios لكل endpoint؛ النواقص المتبقية مذكورة في تحديث آخر التقرير. نتائج `392` لباقي Test Projects تخص تشغيلًا سابقًا ولم يُعَد تشغيلها ضمن هذه الجولة.

## ملخص النتائج

| البند | النتيجة |
| --- | ---: |
| عدد HTTP endpoints المكتشفة | 30 |
| SignalR hubs | 1 |
| Endpoints مكتملة السيناريوهات | 0 |
| Endpoints بها اختبارات جزئية | 30 |
| Endpoints بلا اختبارات مباشرة | 0 وفق جرد الاختبارات الحالي |
| API Integration tests في آخر تشغيل | 80 |
| API Integration | 80 Passed، 0 Failed، 0 Skipped |
| جميع Test Projects | لم تُشغّل في هذه الجولة؛ آخر نتيجة سابقة 392 Passed |
| Build API Integration project | ناجح، 0 Errors، 15 Warnings |

الـ 30 HTTP endpoints تشمل MVC routes و`/health`. تم تسجيل `/hubs/workorders` منفصلًا لأنه SignalR endpoint وليس HTTP controller action.

## جرد الـ endpoints

التصنيف `جزئي` يعني وجود اختبار HTTP مباشر أو استخدام غير مباشر، مع بقاء سيناريوهات مهمة غير مغطاة. لم أعتبر مجرد اختبار وجود الملف تغطية.

| Feature | Method | Route | Authentication / Authorization | الحالة والسيناريوهات الحالية | النواقص المهمة |
| --- | --- | --- | --- | --- | --- |
| Identity | POST | `/identity/token/generate` | Anonymous؛ عليه `LoginPolicy` rate limit | جزئي؛ يُستخدم فعليًا للحصول على JWT، مع بيانات دخول خاطئة وطلب فارغ | شكل TokenResponse، rate limit |
| Identity | POST | `/identity/token/refresh-token` | Anonymous | جزئي؛ token غير صالح يرجع 409 Conflict | Refresh صالح/منتهي، تدوير وإبطال refresh token |
| Identity | GET | `/identity/current-user/claims` | Authenticated | جزئي؛ manager وLabor، و401 بدون token | identity غير موجودة |
| Customers | GET | `/api/v1.0/customers` | Authenticated | جزئي؛ مقارنة Customer موجود بقاعدة البيانات، و401 بدون token | empty result |
| Customers | GET | `/api/v1.0/customers/{customerId}` | Authenticated | جزئي؛ بيانات customer موجودة ومطابقة، و404 و401 | GUID فارغ |
| Customers | POST | `/api/v1.0/customers` | ManagerOnly | جزئي؛ نجاح مع Vehicle، request فارغ 400، duplicate email 409، و403 | قيود العلاقات |
| Customers | PUT | `/api/v1.0/customers/{customerId}` | Manager | جزئي؛ update محفوظ ويرجع 204 بلا body، وrequest فارغ 400 و403 | customer غير موجود |
| Customers | DELETE | `/api/v1.0/customers/{customerId}` | Manager | جزئي؛ حذف ناجح و404 و403 | رفض الحذف عند وجود علاقات |
| Dashboard | GET | `/api/v1.0/dashboard/stats?date=...` | Authenticated | جزئي؛ تاريخ مطلوب، أصفار لتاريخ خالٍ، totals محسوبة من WorkOrder وInvoice معروفين | تاريخ افتراضي، حدود تقسيم التاريخ |
| Invoices | POST | `/api/v1.0/invoices/workorders/{workOrderId}` | ManagerOnly | جزئي؛ issue ناجح، WorkOrder غير موجود، duplicate 409، 401/403 | الحسابات التفصيلية لكل نوع ضريبة/خصم |
| Invoices | GET | `/api/v1.0/invoices/{invoiceId}` | ManagerOnly | جزئي؛ Invoice موجود و404 و401/403 | مقارنة كل line item |
| Invoices | GET | `/api/v1.0/invoices/{invoiceId}/pdf` | ManagerOnly + rate limit | جزئي؛ PDF ناجح ببيانات عربية وخط مضمّن، و404 و401/403 | rate limit |
| Invoices | PUT | `/api/v1.0/invoices/{invoiceId}/payments` | ManagerOnly | جزئي؛ تسوية ناجحة، المكرر يرجع 400، 404 و401/403 | حالات Invoice إضافية |
| RepairTasks | GET | `/api/v1.0/repair-tasks` | Authenticated | جزئي؛ IDs المعادة موجودة في قاعدة البيانات | عدم وجود نتائج، authorization وcontract كامل |
| RepairTasks | GET | `/api/v1.0/repair-tasks/{repairTaskId}` | Authenticated | جزئي؛ استرجاع العنصر المحدد و404 عند الغياب | GUID فارغ |
| RepairTasks | POST | `/api/v1.0/repair-tasks` | Manager | جزئي؛ إنشاء مع Part محفوظ، request فارغ 400، و403 | duplicate name |
| RepairTasks | PUT | `/api/v1.0/repair-tasks/{repairTaskId}` | Manager | جزئي؛ تحديث Task وPart، request فارغ 400، و403 | غير موجود وownership/parts rules |
| RepairTasks | DELETE | `/api/v1.0/repair-tasks/{repairTaskId}` | Manager | جزئي؛ حذف ناجح و404 و403 | منع الحذف مع علاقات |
| Labors | GET | `/api/v1.0/labors` | Authenticated | جزئي؛ IDs المعادة مطابقة لموظفين مخزنين | unauthorized، empty list، محتوى DTO |
| WorkOrders | GET | `/api/v1.0/workorders` | Authenticated | جزئي؛ pagination، حدود غير صحيحة، اختبار فلاتر ببيانات مطابقة، unauthorized | صفحات متعددة، إجمالي ثابت، فلاتر منفردة ومجموعات edge cases |
| WorkOrders | GET | `/api/v1.0/workorders/{workOrderId}` | Authenticated | جزئي؛ موجود، غير موجود، unauthorized | DTO كامل وبيانات العلاقات، test isolation |
| WorkOrders | POST | `/api/v1.0/workorders` | ManagerOnly | جزئي؛ نجاح وvalidation وForbidden، يفحص response والبيانات المحفوظة، وتعارض المركبة | تعارض labor/spot، edge cases |
| WorkOrders | PUT | `/api/v1.0/workorders/{workOrderId}/relocation` | ManagerOnly | جزئي؛ نجاح مع فحص الوقت وSpot المحفوظين، و404 | تعارض المواعيد، boundary الوقت، Forbidden وvalidation |
| WorkOrders | PUT | `/api/v1.0/workorders/{workOrderId}/labor` | ManagerOnly | جزئي؛ نجاح مع فحص LaborId، GUID غير صالح، work order غير موجود، unauthorized | labor غير موجود بصيغة صحيحة، التعارض، Forbidden والبيانات بعد الرفض |
| WorkOrders | PUT | `/api/v1.0/workorders/{workOrderId}/state` | Manager أو labor المعيّن حسب `SelfScopedWorkOrderAccess` | جزئي؛ manager وlabor المعيّن، labor غير المعيّن Forbidden، persistence | الانتقالات غير الصالحة، حالات نهائية، manager/labor boundaries |
| WorkOrders | PUT | `/api/v1.0/workorders/{workOrderId}/repair-task` | Manager | جزئي؛ نجاح مع التحقق من علاقة repair tasks، 404، Forbidden، unauthorized | IDs غير صالحة/مكررة، قائمة فارغة، الحفاظ على الحالة بعد الرفض |
| WorkOrders | DELETE | `/api/v1.0/workorders/{workOrderId}` | ManagerOnly | جزئي؛ حذف فعلي، 404، Forbidden، unauthorized | علاقات invoice، التأكد من عدم حذف بيانات مرتبطة |
| Health | GET | `/health` | Anonymous | Healthy، SQL غير متاح، Redis غير متاح | لا يوجد سيناريو أساسي مفقود |
| WorkOrderHub | SignalR | `/hubs/workorders` | Anonymous | اتصال، استلام `WorkOrdersChanged`، إعادة اتصال واستلام الحدث مرة أخرى | اختبار transport drop غير متوقع إذا اعتُبر behavior تعاقديًا |
| WorkOrders | GET | `/api/v1.0/workorders/schedule/{date}` | Authenticated؛ يتطلب `X-TimeZone` | جزئي؛ التاريخ ووجود WorkOrder داخل الـ schedule، labor filter، unauthorized | timezone boundaries، اليوم الفارغ، قيم timezone غير صالحة |
| Settings | GET | `/api/settings/operating-hours` | Anonymous | جزئي؛ نجاح وفتح قبل الإغلاق | مقارنة القيم بإعدادات متوقعة، configuration boundaries |
| Health | GET | `/health` | لا يوجد `[Authorize]` | جزئي؛ Healthy مع SQL Server وRedis حقيقيين عبر Testcontainers | حالة unhealthy عند تعطل dependency |
| WorkOrderHub | WebSocket | `/hubs/workorders` | لا يوجد authorization attribute ظاهر على الـ hub | جزئي؛ اتصال anonymous واستلام `WorkOrdersChanged` ثم الإغلاق | انقطاع غير متوقع/إعادة الاتصال؛ الحدث الحالي بلا payload |

## تغطية كل Feature

| Feature | إجمالي endpoints | مكتملة | جزئية | بلا اختبار مباشر | أهم ما ينقص |
| --- | ---: | ---: | ---: | ---: | --- |
| WorkOrders | 9 | 0 | 9 | 0 | Business-rule conflicts، سيناريوهات validation وschedule boundaries |
| Customers | 5 | 0 | 5 | 0 | Authorization failures، قيود الحذف، دقة response contract لـ Update |
| Identity | 3 | 0 | 3 | 0 | Refresh token صالح وcredentials/rate limit |
| Dashboard | 1 | 0 | 1 | 0 | قيم المجاميع وحالات عدم وجود بيانات |
| Billing / Invoices | 4 | 0 | 4 | 0 | PDF happy path (يفشل بعيب font موثق)، duplicate/state rules |
| RepairTasks | 5 | 0 | 4 | 1 | By ID؛ به عيب موثق |
| Labors | 1 | 0 | 1 | 0 | Unauthorized وDTO كامل |
| Settings | 1 | 0 | 1 | 0 | قيم configuration الدقيقة |
| Health | 1 | 0 | 1 | 0 | حالات unhealthy عند تعطل SQL أو Redis |
| SignalR | 1 | 0 | 1 | 0 | اختبار انقطاع غير متوقع؛ الإشعار الحالي بلا payload |
| **الإجمالي** | **31** | **0** | **30** | **1** | — |

تُحسب endpoints التي جرى اختبارها جزئيًا على أنها `جزئية` حتى لو نجح happy path؛ لذلك لا يعني رقم 28 أن السيناريوهات الأساسية لكل endpoint اكتملت.

## مصفوفة السيناريوهات المطلوبة

الحالات في الجدول تصف وجود تحقق مباشر، مش مجرد استدعاء endpoint. `N/A` تعني إن السيناريو غير منطبق على نوع endpoint.

| Feature / Endpoint | Method | Happy Path | Validation | Authorization | Not Found | Business Rules | Persistence | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Generate Token | POST | جزئي | ناقص | N/A | N/A | rate limit ناقص | N/A | Partial |
| Refresh Token | POST | ناقص | جزئي | N/A | N/A | تدوير token ناقص | ناقص | Partial |
| Current User Claims | GET | جزئي | N/A | جزئي | ناقص | N/A | N/A | Partial |
| Customers list | GET | جزئي | N/A | جزئي | N/A | N/A | N/A | Partial |
| Customer by ID | GET | جزئي | N/A | ناقص | ناقص | N/A | N/A | Partial |
| Create Customer | POST | جزئي | جزئي | ناقص | N/A | duplicate/relationships ناقصة | جزئي | Partial |
| Update Customer | PUT | جزئي | جزئي | ناقص | ناقص | response contract defect | جزئي | Partial |
| Delete Customer | DELETE | جزئي | N/A | ناقص | جزئي | قيود العلاقات ناقصة | جزئي | Partial |
| Dashboard stats | GET | جزئي | N/A | ناقص | N/A | حسابات القيم ناقصة | N/A | Partial |
| Issue Invoice | POST | جزئي | N/A | ناقص | جزئي | duplicate/state rules ناقصة | جزئي | Partial |
| Get Invoice | GET | جزئي | N/A | ناقص | جزئي | N/A | N/A | Partial |
| Download Invoice PDF | GET | ناقص؛ ثبت 500 لعيب font | N/A | ناقص | جزئي | N/A | N/A | Partial |
| Settle Invoice | PUT | جزئي | N/A | ناقص | جزئي | Already Paid ناقص | جزئي | Partial |
| RepairTasks list | GET | جزئي | N/A | ناقص | N/A | N/A | N/A | Partial |
| RepairTask by ID | GET | ناقص | N/A | ناقص | ناقص | عيب query معروف | N/A | Missing |
| Create RepairTask | POST | جزئي | جزئي | ناقص | N/A | duplicate name ناقص | جزئي | Partial |
| Update RepairTask | PUT | جزئي | جزئي | ناقص | ناقص | part ownership ناقص | جزئي | Partial |
| Delete RepairTask | DELETE | جزئي | N/A | ناقص | جزئي | علاقات تمنع الحذف ناقصة | جزئي | Partial |
| Labors list | GET | جزئي | N/A | ناقص | N/A | N/A | N/A | Partial |
| WorkOrders list | GET | جزئي | جزئي | جزئي | N/A | filtering combinations ناقصة | N/A | Partial |
| WorkOrder by ID | GET | جزئي | N/A | جزئي | جزئي | N/A | N/A | Partial |
| Create WorkOrder | POST | جزئي | جزئي | جزئي | N/A | scheduling conflicts ناقصة | جزئي | Partial |
| Relocate WorkOrder | PUT | جزئي | ناقص | ناقص | جزئي | conflicts ناقصة | جزئي | Partial |
| Assign Labor | PUT | جزئي | جزئي | ناقص | جزئي | availability conflicts ناقصة | جزئي | Partial |
| Update WorkOrder State | PUT | جزئي | ناقص | جزئي | جزئي | transition matrix ناقص | جزئي | Partial |
| Update WorkOrder RepairTasks | PUT | جزئي | جزئي | جزئي | جزئي | duplicate/empty rules ناقصة | جزئي | Partial |
| Delete WorkOrder | DELETE | جزئي | N/A | جزئي | جزئي | invoice relationship ناقص | جزئي | Partial |
| WorkOrder schedule | GET | جزئي | جزئي | جزئي | N/A | timezone boundaries ناقصة | N/A | Partial |
| Operating Hours | GET | جزئي | N/A | N/A | N/A | configuration values ناقصة | N/A | Partial |
| Health Check | GET | جزئي | N/A | N/A | N/A | unhealthy dependency behavior ناقص | N/A | Partial |
| WorkOrderHub | WebSocket | جزئي | N/A | جزئي؛ Anonymous مسموح حسب الإعداد الحالي | N/A | استلام الحدث مثبت؛ لا يوجد payload | N/A | Partial |

## التغييرات المنفذة

- تنظيم الاختبارات في classes حسب الـ Feature: [CustomersControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/CustomersControllerTests.cs)، [IdentityControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/IdentityControllerTests.cs)، [InvoicesControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/InvoicesControllerTests.cs)، [RepairTasksControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/RepairTasksControllerTests.cs)، [LaborsControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/LaborsControllerTests.cs)، [DashboardControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/DashboardControllerTests.cs)، و[SettingsControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/SettingsControllerTests.cs). بقيت endpoints الخاصة بـ WorkOrders في [WorkOrdersControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/WorkOrdersControllerTests.cs).
- توزيع الاختبارات المكتشفة: Customers 7، Identity 3، Invoices 5، RepairTasks 5، Labors 1، Dashboard 1، Settings 1، WorkOrders 36، Health 1، WorkOrderHub 1.
- تحسين [WorkOrdersControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/WorkOrdersControllerTests.cs): اختبار الفلاتر بعمل سجل متوافق ثم التحقق من ظهوره، والتحقق من الـ persistence بعد Create/Relocate/Assign/Update/Delete، والتحقق من علاقة RepairTasks ومحتوى Schedule. تم تصحيح اسم اختبار self-scope غير المعيّن ليطابق النتيجة المتوقعة `Forbidden`.
- تعديل [WebAppFactory.cs](tests/MechanicShop.Api.IntegrationTests/Common/WebAppFactory.cs): تشغيل SQL Server وRedis عبر Testcontainers، وتوجيه health check إلى Redis container. يظل `IDistributedCache` داخل التطبيق مستبدلًا بـ in-memory في test host لتجنب الاعتماد على Redis في عمليات invalidation؛ ده لا يغيّر production services.
- إضافة [HealthControllerTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/HealthControllerTests.cs) للتحقق من Healthy status وفحص SQL Server وRedis. وإضافة [WorkOrderHubTests.cs](tests/MechanicShop.Api.IntegrationTests/Controllers/WorkOrderHubTests.cs) للتحقق من اتصال SignalR المجهول، وصول event `WorkOrdersChanged`، والإغلاق الطبيعي.
- إضافة `Testcontainers` بنفس إصدار `Testcontainers.MsSql` لاستخدام generic Redis container، و`Microsoft.AspNetCore.SignalR.Client` لتشغيل SignalR integration client.
- تحسين ملكية `IServiceScope` في classes التي تتعامل مع `IAppDbContext`: كل test class instance يمتلك scope ويعمل له dispose بعد الاختبار، بدل ترك DbContext scopes مفتوحة حتى نهاية fixture.
- سبق خلال تنفيذ المهمة إضافة `Testcontainers.MsSql` لمشروع الاختبارات وإكمال الـ usings اللازمة، وتحديث [AppHttpClient.cs](tests/MechanicShop.Api.IntegrationTests/Common/AppHttpClient.cs) وtoken provider في الـ factory لتفادي استهلاك LoginPolicy عند كل اختبار. تم الاحتفاظ بطلب token HTTP الفعلي مرة واحدة لكل user.

لم يتغير أي Production file ضمن تنفيذ اختبارات API في هذه المهمة. إعدادات الإنتاج والـ business behavior لم تُعدّل.

## Build ونتائج التنفيذ

- بناء `MechanicShop.Api.csproj`: **ناجح، 0 Errors**.
- بناء `MechanicShop.Api.IntegrationTests.csproj` مع project references مبنية مسبقًا: **ناجح، 0 Errors، 15 Warnings**.
- آخر تشغيل مباشر لـ `MechanicShop.Api.IntegrationTests.exe`: **80 Passed / 0 Failed / 0 Skipped**. السجل الخام: `api-integration-verified.log`.
- لم أعد تشغيل بقية Test Projects في هذه الجولة؛ آخر نتيجة سابقة لها كانت الإجمالي **392 Passed** متضمنًا 61 API tests، وليست نتيجة لهذه النسخة الحالية.
- بناء الـ solution كاملًا في الجولة الحالية تعطل عند `MechanicShop.Client` بسبب MSBuild `ComputeWasmBuildAssets` task host (`MSB4216` و`MSB4027`). لم أغيّر إعدادات Client غير المرتبطة بالمهمة.
- `dotnet test` كان يعيد `Zero tests ran` مع تكامل Microsoft.Testing.Platform؛ لذلك استُخدم ملف `.exe` لتشغيل الاختبارات فعليًا.

## Code Coverage

تم تشغيل Coverlet MTP مع API Integration، وحُفظ OpenCover الخام هنا:

[`api-integration-audit-final.coverage.opencover.031126131414843.xml`](tests/MechanicShop.Api.IntegrationTests/bin/Debug/net10.0/TestResults/api-integration-audit-final.coverage.opencover.031126131414843.xml)

نتيجة الـ assembly-level لـ `MechanicShop.Application.dll` من تشغيل API Integration المحدث: **77.9% Line، 52.8% Branch، 80.7% Method**. وبلغ `MechanicShop.Api.dll` **82.2% Line، 41.7% Branch، 81.2% Method**. هذه أرقام assemblies كاملة عند تشغيل API Integration tests؛ ليست نسبًا لكل Feature. لا يوجد في التقرير تقسيم موثوق حسب Feature لأن OpenCover instrumentation المتاح يجمع classes حسب assembly، ولم أقدّر أرقامًا لكل Feature. شمل تقرير التشغيل assemblies إضافية مثل Domain وInfrastructure وTests.Common؛ لا ينبغي جمع نسب assemblies مباشرة.

## إصلاحات Production التي ثبتتها اختبارات رجوعية

1. `RepairTaskController.GetById` كان يرسل `GetRepairTasksQuery` ويرجع قائمة بدل مورد واحد. أصبح يرسل `GetRepairTaskByIdQuery(repairTaskId)`. اختبارات النجاح و404 تثبت السلوك الجديد، واختبار قائمة repair tasks لم يتغير.
2. `InvoicePdfGenerator` كان يفشل في رسم النص العربي لغياب glyphs مناسبة. أُضيف خط Noto Naskh Arabic مرخّص بـ OFL كمورد embedded، وأُضيف اسم العميل إلى PDF بعد تحميل علاقة Customer. اختبار API ينشئ بيانات عربية فعلية ويتحقق من 200 و`application/pdf` وترويسة `%PDF-` وملف غير فارغ؛ نجح الاختبار.
3. `CustomersController.Update` كان يرجع 200 مع body من نوع `Updated` بينما الـ endpoint يعلن 204. العقد المختار هو `204 NoContent`، وتم ضبط response metadata واختبار خلوّ body وحفظ التغييرات.

لم أغيّر قواعد العمل في Production لتجاوز الاختبارات. بقيت تعديلات Production الأقدم الموجودة في working tree خارج نطاق هذه التغييرات.

## البنية التحتية والعزل

- Docker Desktop كان متاحًا، لكن الوصول إلى named pipe كان مرفوضًا داخل التشغيل المقيد؛ تم تشغيل build والاختبارات بصلاحية وصول Docker، ونجح SQL Server Testcontainer.
- قبل استبدال cache في test host، فشلت 7 عمليات كتابة WorkOrders بـ HTTP 500 لأن invalidation لـ Redis حاول الاتصال بـ `localhost:6379`. تم تصنيفها كمشكلة إعداد test infrastructure لا كعيب handler. بعد إضافة `IDistributedCache` in-memory في `WebAppFactory`، نجحت الاختبارات النهائية مرتين.
- الـ global rate limiter في التطبيق كان يقيّد طلبات الاختبارات كلها تحت Remote IP واحد؛ بعد توسيع المجموعة ظهر 429. تم تعطيل الـ global limiter في test host فقط، مع إبقاء `LoginPolicy` و`ApiUserPolicy` المسمّات كما هما. لذلك لا تعتبر نتائج هذه الجولات تحققًا من global rate limit نفسه.
- OpenCover أظهر أن API PDF route غير مغطى بمسار نجاح؛ اختبار النجاح التجريبي كشف عيب الخط أعلاه، لذلك بقي السيناريو كـ documented production limitation بدل تغيير الإنتاج أو إخفاء الفشل.
- لا يوجد `Skip` لإخفاء اختبارات. Health يختبر Redis وSQL عند التوقف/الإيقاف المؤقت، ويتحقق من حالة dependency الأخرى.
- خزّن `WebAppFactory` connection strings عند بدء الحاويات كي لا يحاول إعادة استنتاج المنفذ بعد توقفها. الاختبارات serialized داخل collection وتزيل السجلات التي تنشئها في `finally`، وتستخدم ترتيبًا ثابتًا عند اختيار seed في `WorkOrdersControllerTests`.
- اختبارات Hub تثبت إعادة الاتصال عبر دورة اتصال جديدة واستلام الحدث بعد الاتصال؛ لا يوجد سلوك reconnect تلقائي مخصص في الـ Hub.

## التغطية المتبقية

- لم تُغطَّ كل التركيبات الطرفية لكل endpoint؛ خصوصًا سيناريوهات rate limiting، بعض حالات refresh-token، وقيود حذف RepairTask/Customer المرتبطة ببيانات أخرى.
- اختبارات Dashboard تغطي مجموعات صفرية ومجموعة محسوبة من fixture ثابتة، لكنها لا تغطي كل حالات تقسيم التاريخ.
- WorkOrders يغطي تعارض المركبة وحدود التاريخ المحلي في Montreal؛ لا يغطي جميع مناطق الزمن وتغييرات DST.
- تقرير Code Coverage الخام السابق لا يعكس إصلاحات Production الحالية، لذلك لا ينبغي التعامل مع نسبه كقياس حديث لهذه النسخة.
