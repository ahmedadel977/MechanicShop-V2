# مسار قراءة كود Multi-Branch وRowVersion خطوة بخطوة

الملف ده معمول عشان تقرأ كود الـProduction بنفسك بالترتيب وتفهم كل جزء قبل ما تنتقل للي بعده. ابدأ من أول خطوة، وافتح الملفات بالترتيب؛ كل خطوة بتوضح لك إيه تقرأ، وإيه تسأل نفسك عنه، وعلاقته بالخطوة التالية.

الشرح هنا خاص بكود Production. الاختبارات موجودة في آخر المسار عشان ترجع لها بعد فهم السلوك الأساسي.

## قبل ما تبدأ

التغيير بيحل مشكلتين مرتبطتين ببعض:

1. عزل بيانات الورشة بحيث WorkOrder وInvoice يتبعوا Branch، والموظف مايتعاملش إلا مع الفروع المسموح بها.
2. منع تعديل مستخدم لنسخة قديمة من WorkOrder فوق تعديل أحدث باستخدام RowVersion.

اقرأ كل ملف بترتيب الخطوات. ما تبدأش من Controller؛ ابدأ من القاعدة في Domain ثم اتبع انتقالها بين الطبقات.

## الخطوة 1: افهم معنى نطاق الوصول في Domain

افتح بالترتيب:

1. [AccessScope.cs](../MechanicShop.Domain/Identity/AccessScope.cs)
2. [Employee.cs](../MechanicShop.Domain/Employees/Employee.cs)
3. [EmployeeErrors.cs](../MechanicShop.Domain/Employees/EmployeeErrors.cs)

في `AccessScope.cs` اقرأ قيم `Branch` و`Company`. بعد كده في `Employee.cs` راجع `Create` و`SetAccessScope`.

ركّز على الأسئلة دي:

- ليه Role زي `Manager` و`Labor` مش كفاية لوحده؟
- إيه القيمة الافتراضية لـAccessScope عند إنشاء Employee؟
- ليه `Company` مسموح لـ`Manager` فقط؟
- هل القاعدة موجودة في Domain ولا متروكة للـAPI؟

**الخلاصة قبل ما تكمل:** Role يحدد نوع الموظف، وAccessScope يحدد مدى البيانات التي يسمح له بالتعامل معها.

## الخطوة 2: اقرأ كيان الفرع والعضوية

افتح:

1. [Branch.cs](../MechanicShop.Domain/Branches/Branch.cs)
2. [BranchErrors.cs](../MechanicShop.Domain/Branches/BranchErrors.cs)
3. [EmployeeBranchMembership.cs](../MechanicShop.Domain/Branches/EmployeeBranchMembership.cs)

في `Branch.cs` تابع الخصائص، والـconstructors، وبعدها `Create` و`SetActive`. في `EmployeeBranchMembership.cs` تابع `EmployeeId` و`BranchId` و`IsDefault` ثم `Create` و`SetDefault`.

اسأل نفسك:

- ليه `Branch.Create` هي طريقة الإنشاء العامة بدل constructor عام؟
- ليه كود الفرع بيتحول لـUppercase ويتشال منه المسافات الطرفية؟
- ليه Membership كيان لوحده بدل علاقة many-to-many مباشرة؟
- إيه الفرق بين قاعدة Domain وبين Unique Index الذي هنشوفه لاحقًا؟

**الخلاصة:** Branch يمثل الفرع، وEmployeeBranchMembership تمثل سماح موظف بفرع وتحمل خاصية الفرع الافتراضي.

## الخطوة 3: اربط WorkOrder بفرع وافهم قواعده

افتح:

1. [Workorder.cs](../MechanicShop.Domain/Workorders/Workorder.cs)
2. [WorkOrderErrors.cs](../MechanicShop.Domain/Workorders/WorkOrderErrors.cs)

في `Workorder.cs` اقرأ `BranchId` و`RowVersion`، ثم `Create`. بعد كده اقرأ `UpdateTiming` و`UpdateLabor` و`UpdateState` و`IsEditable`.

ركّز على الفرق بين:

- `BranchId`: ملكية WorkOrder للفرع.
- `RowVersion`: علامة إصدار للسجل تُستخدم لاكتشاف التعديل المتزامن.
- `IsEditable` و`CanTransitionTo`: قواعد حالة WorkOrder الموجودة في Domain.

اتأكد إنك لاحظت أن `Create` ترفض الفرع الفارغ، وفي نفس الوقت ما زالت تتحقق من التوقيت والمهام والفني وSpot. إضافة Branch ما ألغتش قواعد WorkOrder السابقة.

## الخطوة 4: افهم إزاي الفرع الحالي يوصل للـApplication

افتح بالترتيب:

1. [ICurrentBranchContext.cs](../MechanicShop.Application/Common/Interfaces/ICurrentBranchContext.cs)
2. [CurrentBranchContext.cs](../MechanicShop.Infrastructure/identity/CurrentBranchContext.cs)
3. [TokenProvider.cs](../MechanicShop.Infrastructure/identity/TokenProvider.cs)
4. [IdentityService.cs](../MechanicShop.Infrastructure/identity/IdentityService.cs)
5. [AppUserDto.cs](../MechanicShop.Application/Features/Identity/Dtos/AppUserDto.cs)
6. [GenerateTokenQuery.cs](../MechanicShop.Application/Features/Identity/Queries/GenerateTokens/GenerateTokenQuery.cs)
7. [GenerateTokenQueryhandler.cs](../MechanicShop.Application/Features/Identity/Queries/GenerateTokens/GenerateTokenQueryhandler.cs)
8. [RefreshTokenQueryHandler.cs](../MechanicShop.Application/Features/Identity/Queries/RefreshTokens/RefreshTokenQueryHandler.cs)

تابع رحلة البيانات حسب نوع الحساب: `Company` Manager يسجل الدخول من غير اختيار فرع، وبيصدر له Token من غير `branch_id`. أما الحساب `Branch` فيستخدم الفرع الافتراضي النشط المسجل في `EmployeeBranchMembership`. بعد كده شوف إزاي `Role` و`AccessScope` و`BranchId` تنتقل إلى Claims عند اللزوم، وإزاي `CurrentBranchContext` يقرأها.

اقرأ أيضًا [ICurrentEmployeeAccess.cs](../MechanicShop.Application/Common/Interfaces/ICurrentEmployeeAccess.cs)، و[CurrentEmployeeAccess.cs](../MechanicShop.Application/Common/Models/CurrentEmployeeAccess.cs)، و[CurrentEmployeeAccessService.cs](../MechanicShop.Infrastructure/identity/CurrentEmployeeAccessService.cs). الخدمة تستخدم EmployeeId المستخرج من الهوية المصادق عليها، ثم تجلب Role وAccessScope الحاليين من قاعدة البيانات. ده مهم لأن الـClaims قد تكون قديمة بعد تغيير الدور أو النطاق.

في `IdentityService` قارن Role المستخدم في Identity مع Role الموظف في قاعدة البيانات، وشوف اختيار الفرع الافتراضي للحساب Branch-scoped. بعد كده اقرأ `RefreshTokenQueryHandler` وتأكد إن التحديث يعيد تحميل بيانات المستخدم الحالية بدل الاعتماد على BranchId قديم في الـToken.

اسأل نفسك:

- ليه Application تعتمد على `ICurrentBranchContext` بدل `HttpContext`؟
- ماذا ترجع `CurrentBranchContext` لو Claim غير موجودة أو مش Guid صحيحة؟
- ليه تجديد Token محتاج التأكد من بيانات الحساب الحالية؟
- هل وجود Claim في JWT وحده يثبت أن العضوية ما زالت فعالة؟

## الخطوة 5: اقرأ Authorization قبل عزل الاستعلامات

افتح:

1. [ActiveBranchAccessRequirement.cs](../MechanicShop.Infrastructure/identity/policies/ActiveBranchAccessRequirement.cs)
2. [ActiveBranchAccessHandler.cs](../MechanicShop.Infrastructure/identity/policies/ActiveBranchAccessHandler.cs)
3. [DependencyInjection.cs](../MechanicShop.Api/DependencyInjection.cs)
4. [DependencyInjection.cs](../MechanicShop.Infrastructure/DependencyInjection.cs)

في `ActiveBranchAccessHandler` اقرأ `HandleRequirementAsync` من أوله لآخره. امشِ بالترتيب: قراءة Claims، التحقق من Employee، مقارنة Role وAccessScope مع قاعدة البيانات، ثم التأكد من الفرع والعضوية.

قارن الحالتين داخل Handler:

- `Manager` مع `Company`: يسمح له بالوصول على مستوى الشركة، ولا يحتاج `branch_id` أو Membership لفرع معين.
- حساب `Branch`: لازم الـClaims تطابق Role وAccessScope من قاعدة البيانات، ولازم `branch_id` يشير لفرع نشط والموظف عضو فيه.
- أي حساب Labor لا يحصل على صلاحية Company-wide لمجرد وجود Role claim.

بعدها راجع تسجيل الـPolicy والـHandler في `DependencyInjection`، عشان تعرف إزاي الـFramework بيستدعيهما عند وصول الطلب.

**مهم:** Authorization يقرر هل المستخدم مسموح له دخول المسار. الخطوة التالية تشرح طبقة حماية ثانية داخل استعلامات EF.

## الخطوة 6: افهم حالات الاستخدام الجديدة للفروع

افتح الملفات في الترتيب التالي:

### إنشاء الفرع

1. [CreateBranchRequest.cs](../Contracts/Requests/Branches/CreateBranchRequest.cs)
2. [CreateBranchCommand.cs](../MechanicShop.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommand.cs)
3. [CreateBranchCommandValidator.cs](../MechanicShop.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommandValidator.cs)
4. [CreateBranchCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommandHandler.cs)
5. [BranchDto.cs](../MechanicShop.Application/Features/Branches/Dtos/BranchDto.cs)

اتبع البيانات من Request إلى Command، ثم راجع الفرق بين فحص `Validator` وفحص Company scope في Handler، وبعدها إنشاء Domain entity وحفظها.

### إضافة موظف لفرع

1. [AddEmployeeToBranchCommand.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommand.cs)
2. [AddEmployeeToBranchCommandValidator.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommandValidator.cs)
3. [AddEmployeeToBranchCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommandHandler.cs)

تتبع فحوص النطاق، والفرع النشط، ووجود Employee، وعدم تكرار Membership، ثم اختيار `IsDefault` لأول عضوية.

### تغيير حالة الفرع أو الفرع الافتراضي أو AccessScope

اقرأ كل زوج Command وHandler، ثم Validator حيث يوجد:

- [SetBranchActiveCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/SetBranchActive/SetBranchActiveCommandHandler.cs)
- [SetDefaultBranchCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/SetDefaultBranch/SetDefaultBranchCommandHandler.cs)
- [SetEmployeeAccessScopeCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/SetEmployeeAccessScope/SetEmployeeAccessScopeCommandHandler.cs)

في `SetDefaultBranchCommandHandler` ركز على Transaction، وليه يمسح default الحالية ويحفظ قبل تعيين الجديدة. في `SetBranchActiveCommandHandler` افهم ليه لا يسمح بتعطيل فرع ما زال default لموظف.

### راجع حدود الصلاحيات كجدول

قبل ما تقرأ الـController، اقرأ الـHandlers بالترتيب ده:

1. [AddEmployeeToBranchCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommandHandler.cs)
2. [CreateBranchCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/CreateBranch/CreateBranchCommandHandler.cs)
3. [SetBranchActiveCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/SetBranchActive/SetBranchActiveCommandHandler.cs)
4. [SetEmployeeAccessScopeCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/SetEmployeeAccessScope/SetEmployeeAccessScopeCommandHandler.cs)
5. [Employee.cs](../MechanicShop.Domain/Employees/Employee.cs)

اعمل لنفسك الجدول ده بعد ما تقرأ كل Handler:

| العملية | الشرط على المستخدم الحالي | فحوص إضافية |
|---|---|---|
| Add Employee to Branch | `Role.Manager` | Company Manager لأي فرع نشط؛ Branch Manager لفرع عنده Membership فيه فقط |
| Create Branch | `Role.Manager` و`AccessScope.Company` | منع Branch Manager وLabor |
| Set Branch Active | `Role.Manager` و`AccessScope.Company` | لا يمكن تعطيل فرع ما زال Default لموظف |
| Set Employee AccessScope | `Role.Manager` و`AccessScope.Company` | الموظف الهدف موجود؛ Domain يمنع Company scope عن Labor؛ منع المستخدم من ترقية نفسه |

في كل Handler لاحظ إن `ICurrentEmployeeAccess.GetCurrentAsync` يجيب بيانات المستخدم من DB. بعد كده تأكد إن نتيجة الخطأ من `Result` تتحول إلى HTTP مناسب عبر `ApiController` و`ProblemExtensions`.

### تتبع طلب إضافة موظف من HTTP لحد قاعدة البيانات

اقرأ بهذا الترتيب، وخلي طلبًا واحدًا في ذهنك أثناء القراءة:

1. [BranchesController.cs](../MechanicShop.Api/Controllers/BranchesController.cs): دور على Route إضافة الموظف للفرع، واعرف إيه اللي يرسله الـController إلى `ISender`.
2. [AddEmployeeToBranchCommand.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommand.cs): سجل أسماء القيم التي تدخل إلى Application.
3. [AddEmployeeToBranchCommandValidator.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommandValidator.cs): لاحظ إنه يفحص إن الـIDs مش فارغة، لكنه لا يقرر صلاحية المستخدم.
4. [AddEmployeeToBranchCommandHandler.cs](../MechanicShop.Application/Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommandHandler.cs): امشِ على الترتيب: قراءة المستخدم من DB، فحص Role وAccessScope وMembership، التأكد من نشاط الفرع ووجود الموظف، منع التكرار، ثم تحديد Default عند أول عضوية والحفظ.
5. [EmployeeBranchMembership.cs](../MechanicShop.Domain/Branches/EmployeeBranchMembership.cs): افهم الـDomain validation عند إنشاء العضوية.
6. [EmployeeBranchMembershipConfiguration.cs](../MechanicShop.Infrastructure/Date/Configurations/EmployeeBranchMembershipConfiguration.cs): راجع المفتاح المركب والفهرس الذي يسمح بفرع افتراضي واحد للموظف.
7. [BranchesControllerTests.cs](../tests/MechanicShop.Api.IntegrationTests/Controllers/BranchesControllerTests.cs): طابق كل شرط في الـHandler مع Test يثبت النتيجة وHTTP status.

بعد كل ملف، اكتب بجملة واحدة: "مين مسؤول عن القرار ده؟" مثلًا: Validator يفحص شكل المدخل، Authorization/Handler يقرر الصلاحية، Domain يحافظ على invariant، وSQL يفرض uniqueness والعلاقات.

### قراءة الفروع والموظفين

اقرأ ملفات `Query` ثم `Handler` ثم DTO:

- [GetBranchesQueryHandler.cs](../MechanicShop.Application/Features/Branches/Queries/GetBranches/GetBranchesQueryHandler.cs)
- [GetBranchByIdQueryHandler.cs](../MechanicShop.Application/Features/Branches/Queries/GetBranchById/GetBranchByIdQueryHandler.cs)
- [GetBranchEmployeesQueryHandler.cs](../MechanicShop.Application/Features/Branches/Queries/GetBranchEmployees/GetBranchEmployeesQueryHandler.cs)
- [BranchEmployeeDto.cs](../MechanicShop.Application/Features/Branches/Dtos/BranchEmployeeDto.cs)

لاحظ استخدام `AsNoTracking` للقراءة، وشرط Membership للمستخدم Branch-scoped، وتحويل النتيجة إلى DTO.

## الخطوة 7: تتبع إنشاء WorkOrder وإسناد الفني

افتح:

1. [CreateWorkOrderCommandHandler.cs](../MechanicShop.Application/Features/WorkOrders/common/CreateWorkORDER/CreateWorkOrderCommandHandler.cs)
2. [AssignLaborCommandHandler.cs](../MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommandHandler.cs)
3. [GetWorkOrdersQueryHandler.cs](../MechanicShop.Application/Features/WorkOrders/Queries/GetWorkOrders/GetWorkOrdersQueryHandler.cs)
4. [WorkOrderMapper.cs](../MechanicShop.Application/Features/WorkOrders/Mappers/WorkOrderMapper.cs)

في الإنشاء، افهم اختلاف مصدر `BranchId`: Branch Manager يستخدم الفرع الموجود في السياق المصادق عليه، ولا يستطيع استبداله بفرع آخر من الطلب. Company Manager يرسل الفرع المستهدف مع طلب إنشاء WorkOrder؛ الـHandler يتحقق أن الفرع موجود ونشط. في الحالتين لا تحدد قيمة `BranchId` Role المستخدم أو نطاقه. بعدها راجع Membership للفني قبل إنشاء WorkOrder.

افتح [CreateWorkOrderRequest.cs](../Contracts/Requests/WorkOrders/CreateWorkOrderRequest.cs)، ثم `CreateWorkOrder` في [WorkOrderController.cs](../MechanicShop.Api/Controllers/WorkOrderController.cs)، ثم `CreateWorkOrderCommand.cs` وبعدها الـHandler. قارن `BranchId` القادم كهدف عملية مع `ICurrentBranchContext` الذي يمثل فرع الحساب المصادق عليه.

في الإسناد، قارن BranchId الخاص بـWorkOrder مع Membership الخاصة بالفني. لاحظ إن Handler يرفض الفني غير المنتمي للفرع قبل الحفظ.

بعدها تتبع `RowVersion` في Query وMapper؛ لازم ترجع للعميل عشان يرسلها في التعديل التالي.

## الخطوة 8: اقرأ عزل قاعدة البيانات والقيود

افتح بالترتيب:

1. [AppDbContext.cs](../MechanicShop.Infrastructure/Date/AppDbContext.cs)
2. [BranchConfiguration.cs](../MechanicShop.Infrastructure/Date/Configurations/BranchConfiguration.cs)
3. [EmployeeBranchMembershipConfiguration.cs](../MechanicShop.Infrastructure/Date/Configurations/EmployeeBranchMembershipConfiguration.cs)
4. [EmployeeConfiguration.cs](../MechanicShop.Infrastructure/Date/Configurations/EmployeeConfiguration.cs)
5. [WorkOrderConfiguration.cs](../MechanicShop.Infrastructure/Date/Configurations/WorkOrderConfiguration.cs)
6. [IAppDbContext.cs](../MechanicShop.Application/Common/Interfaces/IAppDbContext.cs)

في `AppDbContext` اقرأ تعريف `DbSets`، ثم حقول السياق، ثم `OnModelCreating` و`HasQueryFilter`. تتبع فلتر Invoice عبر WorkOrder لأن Invoice ما عندوش BranchId مستقل.

في Configurations راجع:

- Unique index على Branch.Code.
- المفتاح المركب للـMembership.
- الفهرس الفريد المشروط بـ`IsDefault`.
- FK الإلزامي من WorkOrder إلى Branch.
- `IsRowVersion` والفهرس المركب للفرع والتوقيت.

اسأل نفسك: أي قواعد موجودة في Domain فقط، وأيها محمية أيضًا من SQL؟

## الخطوة 9: اقرأ Migration والتهيئة والـSeed

افتح:

1. [20261103163357_AddBranchesAndWorkOrderRowVersion.cs](../MechanicShop.Infrastructure/Migrations/20261103163357_AddBranchesAndWorkOrderRowVersion.cs)
2. [SeedDataIds.cs](../MechanicShop.Infrastructure/Date/SeedDataIds.cs)
3. [ApplicationDbContextInitialiser.cs](../MechanicShop.Infrastructure/Date/ApplicationDbContextInitialiser.cs)
4. [AppDbContextModelSnapshot.cs](../MechanicShop.Infrastructure/Migrations/AppDbContextModelSnapshot.cs)

في Migration اقرأ `Up` ثم `Down`. افهم فحص WorkOrders القديمة قبل إضافة BranchId المطلوب، وأنه يرفض التخمين في توزيع البيانات التاريخية.

في `ApplicationDbContextInitialiser` اقرأ `InitialiseAsync` أولًا: reset اختياري ومقيد ببيئات Development وTesting، ثم `MigrateAsync`. بعد كده اقرأ `SeedBranchesAsync` وSeed العضويات و`SeedWorkOrdersAsync`.

قارن GUIDs الموجودة في `SeedDataIds` بالبيانات المنشأة. الهدف أن إعادة تشغيل Seed تستخدم نفس Branch IDs والعلاقات ولا تكرر الفروع.

## الخطوة 10: افهم RowVersion من قاعدة البيانات لحد HTTP

اقرأ الملفات بالترتيب ده:

1. [WorkOrderConfiguration.cs](../MechanicShop.Infrastructure/Date/Configurations/WorkOrderConfiguration.cs) — إعداد `IsRowVersion`.
2. [WorkOrderDto.cs](../MechanicShop.Application/Features/WorkOrders/Dtos/WorkOrderDto.cs) و[WorkOrderListItemDto.cs](../MechanicShop.Application/Features/WorkOrders/Dtos/WorkOrderListItemDto.cs) — إرجاع القيمة للعميل.
3. [AssignLaborRequest.cs](../Contracts/Requests/WorkOrders/AssignLaborRequest.cs) — استقبال القيمة في Request.
4. [AssignLaborCommand.cs](../MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommand.cs) — نقلها إلى Application.
5. [AssignLaborCommandValidator.cs](../MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommandValidator.cs) — التحقق من وجود 8 bytes.
6. [AssignLaborCommandHandler.cs](../MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommandHandler.cs) — تمرير القيمة كـOriginalValue ومعالجة التعارض.
7. [WorkOrderErrors.cs](../MechanicShop.Domain/Workorders/WorkOrderErrors.cs) — تعريف Conflict.
8. [ProblemExtensions.cs](../MechanicShop.Api/Extensions/ProblemExtensions.cs) — تحويل Conflict إلى HTTP 409.

اقرأ هذا المسار كقصة واحدة: SQL يولد RowVersion، Query يعيدها، العميل يرسلها، Validator يفحص شكلها، EF يستخدمها في UPDATE، والتطبيق يرجع Conflict إذا كانت قديمة.

كرر المسار مع `RelocateWorkOrder` و`UpdateWorkOrderState` و`UpdateWorkOrderRepairTasks`. بعد ما تفهم التعديل، راجع حذف WorkOrder في [WorkOrderController.cs](../MechanicShop.Api/Controllers/WorkOrderController.cs): القيمة تأتي عبر `If-Match` بدل Request body.

## الخطوة 11: افهم API وCache والملفات العرضية

افتح:

- [BranchesController.cs](../MechanicShop.Api/Controllers/BranchesController.cs)
- [WorkOrderController.cs](../MechanicShop.Api/Controllers/WorkOrderController.cs)
- [ProblemExtensions.cs](../MechanicShop.Api/Extensions/ProblemExtensions.cs)
- [CachingBehavior.cs](../MechanicShop.Application/Common/Behaviours/CachingBehavior.cs)
- [GetLaborsQueryHandler.cs](../MechanicShop.Application/Features/Labors/Queries/GetLabors/GetLaborsQueryHandler.cs)
- [DashboardController.cs](../MechanicShop.Api/Controllers/DashboardController.cs)
- [InvoicesController.cs](../MechanicShop.Api/Controllers/InvoicesController.cs)

في كل Controller، افصل بين تعريف المسار وبين قواعد العمل التي ينفذها Handler أو Policy. في `CachingBehavior` افهم كيف يضاف Scope وBranchId للمفتاح. راجع `GetLaborsQueryHandler` وEndpoints التي تغيرت للتأكد أن البيانات المعروضة أو الأخطاء لا تتجاوز السياق.

`GetLaborsQueryHandler` يعرض كل الفنيين لمدير الشركة، ويقصر النتيجة على أعضاء الفرع الحالي للحساب Branch-scoped. اربط ده باختبار `GetLaborsQueryHandlerTests` واختبار API عند قراءة قائمة الفنيين.

## الخطوة 12: بعد فهم Production، راجع الاختبارات

الاختبارات وسيلة لتأكيد السلوك الذي قرأته، وليست بديلًا عن فهمه.

ابدأ بـ:

1. [EmployeeTets.cs](../tests/MechanicShop.Domain.UniTests/Employees/EmployeeTets.cs)
2. [WorkOrderTests.cs](../tests/MechanicShop.Domain.UniTests/WorkOrders/WorkOrderTests.cs)
3. [CachingBehaviorTests.cs](../tests/MechanicShop.Application.UniTests/Behaviours/CachingBehaviorTests.cs)
4. [DatabaseInitializationTests.cs](../tests/MechanicShop.Api.IntegrationTests/Infrastructure/DatabaseInitializationTests.cs)
5. [BranchesControllerTests.cs](../tests/MechanicShop.Api.IntegrationTests/Controllers/BranchesControllerTests.cs)
6. [WorkOrdersControllerTests.cs](../tests/MechanicShop.Api.IntegrationTests/Controllers/WorkOrdersControllerTests.cs)
7. اختبارات `MechanicShop.Application.SubcutaneiousTests/Features/WorkOrders`.

لكل Test اسأل: إيه الحالة التي يثبتها؟ هل هو Domain unit test، Application test، أم API integration test؟ هل يفحص النتيجة فقط، أم كمان قاعدة البيانات وside effects؟

## خريطة العلاقات النهائية

~~~mermaid
flowchart TD
    Domain[Domain: Branch وMembership وWorkOrder] --> Application[Application: Commands وQueries وHandlers]
    JWT[JWT Claims: branch_id وaccess_scope] --> Context[CurrentBranchContext]
    Context --> Policy[ActiveBranchAccessHandler]
    Context --> Handlers[Application Handlers]
    API[API Controllers] --> Policy
    API --> Application
    Application --> EF[AppDbContext وConfigurations]
    EF --> SQL[(SQL Server: FK وIndexes وRowVersion)]
    SQL --> Migration[EF Migration وSeed]
~~~

اقرأ الرسم من أعلى: Domain يحدد الكيانات والقواعد. API يتحقق من المستخدم عبر Policy ثم يرسل حالة الاستخدام إلى Application. Infrastructure توصل السياق بقاعدة البيانات، وEF وSQL يفرضان العزل والعلاقات والتزامن.

## لما تخلص القراءة

جرّب تشرح بصوتك من غير ما تبص على الإجابة:

1. إزاي النظام يمنع Labor من الوصول لفرع مش عضو فيه؟
2. ليه عندنا Authorization policy وGlobal Query Filter معًا؟
3. إزاي RowVersion تمنع Lost Update؟
4. ليه تعيين default محتاج Transaction؟
5. إيه الفرق بين Validator وDomain rule وDatabase constraint؟
6. إيه اللي يحصل لو Migration لقت WorkOrders قديمة؟ وليه؟

لو قدرت تتبع كل إجابة من الملف الذي يبدأ منها السلوك حتى الملف الذي ينفذه، يبقى فهمت التغيير كمسار كامل بين الطبقات.
