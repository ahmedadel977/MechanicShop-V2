# دليل هندسي: الفروع المتعددة والتزامن في MechanicShop

## هدف الدليل وطريقة قراءته

الدليل ده بيشرح التغييرات الفعلية الخاصة بـMulti-Branch وOptimistic Concurrency في المشروع. أسماء الملفات والكلاسات والـMethods والـProperties مكتوبة زي ما هي في الكود، والشرح بالعربي. كل مسار موضح من منظور مسؤوليته وعلاقته بباقي الطبقات.

المشروع مبني على Clean Architecture: الـAPI تستقبل HTTP، والـApplication تنسق حالات الاستخدام، والـDomain يحمي قواعد النشاط، والـInfrastructure تتعامل مع EF Core وSQL Server وIdentity.

## 1. نظرة عامة: قبل التعديل وبعده

قبل الإضافة، كان WorkOrder سجل تشغيل للورشة من غير BranchId يحدد الفرع المالك. ماكانش فيه نموذج يحدد الموظفين المسموح لهم بكل فرع، أو يميز صلاحية مدير فرع عن مدير الشركة. كمان عمليات تعديل WorkOrder ماكانتش تحمل نسخة السجل التي قرأها المستخدم؛ لذلك كان تعديل أحدث مستخدم قادرًا على الكتابة فوق تعديل مستخدم تاني من غير اكتشاف.

بعد الإضافة، كل WorkOrder له BranchId مطلوب، والموظف له EmployeeBranchMemberships توضح الفروع المسموحة والفرع الافتراضي. التوكن يحمل الفرع المختار وAccessScope، وسياسة ActiveBranchAccess تراجع الصلاحية قبل الوصول للبيانات. EF Core يطبق عزلًا تلقائيًا على WorkOrders وInvoices. RowVersion تكشف تعديل النسخة القديمة وتحوله إلى Conflict بدل Lost Update.

| الموضوع | قبل | بعد |
|---|---|---|
| ملكية WorkOrder | لا يوجد BranchId | BranchId مطلوب وله FK إلى Branches |
| صلاحية الفرع | الدور وحده لا يصف عضوية الفرع | Role مع AccessScope وEmployeeBranchMembership |
| عزل البيانات | لا يوجد فلتر حسب الفرع | Global Query Filters على WorkOrders وInvoices |
| تعديل متزامن | ممكن ضياع تعديل أقدم | RowVersion واكتشاف التعارض |
| تهيئة قاعدة البيانات | إنشاء schema غير مرتبط دائمًا بالـMigrations | MigrateAsync وSeed ثابت قابل للتكرار |

التصميم الحالي يجعل Customer وVehicle وRepairTask catalog مشتركة، وWorkOrder هو السجل التشغيلي المرتبط بفرع. Invoice يتبع فرع WorkOrder المرتبط به، ولا يحتوي على BranchId مستقل.

## 2. خريطة الطبقات ومسؤولياتها

~~~mermaid
flowchart LR
    Client[عميل HTTP] --> Controller[API Controller]
    Controller --> Policy[ActiveBranchAccess Policy]
    Policy --> Context[CurrentBranchContext من JWT]
    Controller --> Sender[ISender / MediatR]
    Sender --> Handler[Application Command أو Query Handler]
    Handler --> Domain[Domain Entity وقواعد العمل]
    Handler --> DbContext[AppDbContext]
    DbContext --> Filter[Global Query Filter]
    Filter --> SQL[(SQL Server)]
    DbContext --> Events[Domain Events بعد نجاح الحفظ]
~~~

الـController لا يقرر قواعد النشاط. الـHandler ينسق العملية ويستخدم الـDomain للتحقق من الحالة. AppDbContext ينفذ الاستعلامات باستخدام EF Core ويضيف شروط الفرع تلقائيًا. Authorization policy تحمي الـEndpoints، والـQuery Filter طبقة عزل إضافية للاستعلامات.

## 3. Contracts Layer

الـContracts بتعرّف شكل الطلبات والاستجابات بين العميل والـAPI. هي لا تحتوي على صلاحية المستخدم أو منطق حفظ البيانات.

### Requests الجديدة للفروع

- Contracts/Requests/Branches/CreateBranchRequest.cs: اسم الفرع وكوده المطلوبان للإنشاء.
- Contracts/Requests/Branches/SetBranchActiveRequest.cs: القيمة الجديدة لـIsActive.
- Contracts/Requests/Branches/SetDefaultBranchRequest.cs: BranchId الذي يطلب الموظف تعيينه كفرع افتراضي.
- Contracts/Requests/Branches/SetEmployeeAccessScopeRequest.cs: AccessScope المطلوب للموظف.

كل Request يثبت شكل البيانات فقط. وجود BranchId في الطلب لا يعني إن المستخدم مسموح له يستخدم الفرع؛ الصلاحية تتراجع في Application وAuthorization.

### Requests المعدلة لـWorkOrders

اتضاف RowVersion إلى:

- Contracts/Requests/WorkOrders/AssignLaborRequest.cs
- Contracts/Requests/WorkOrders/ModifyRepairTaskRequest.cs
- Contracts/Requests/WorkOrders/RelocateWorkOrderRequest.cs
- Contracts/Requests/WorkOrders/UpdateWorkOrderStateRequest.cs

العميل يقرأ RowVersion من WorkOrder، ثم يعيد إرسال القيمة عند التعديل. لو النسخة قديمة، السيرفر يرفض التعديل بدل ما يطمس تغيير مستخدم آخر. الحذف يستخدم If-Match header بدل body token.

## 4. Domain Layer

Domain هي طبقة قواعد النشاط: تمنع تكوين حالة غير سليمة حتى لو جاء الطلب من API مختلفة أو من اختبار مباشر.

### Branch

الملفات: MechanicShop.Domain/Branches/Branch.cs وBranchErrors.cs.

Branch تمثل فرع الورشة. Branch.Create تتحقق من Guid غير فارغ واسم وكود غير فارغين، ثم تنظف الاسم وتحول الكود إلى Uppercase. ده يوحد الكود قبل وصوله إلى قاعدة البيانات. التكرار نفسه تمنعه قاعدة البيانات عبر Unique Index.

الـconstructor خاص، والخصائص لا تتغير من أي كود خارجي. التفعيل والتعطيل يمران عبر SetActive. الـprivate parameterless constructor موجود كي يستطيع EF Core إعادة بناء الكيان.

لو غابت هذه القواعد، كان ممكن إنشاء فرع بمعرف فارغ أو كود فارغ، وكان كل Handler مضطرًا لإعادة نفس التحقق.

### EmployeeBranchMembership

الملف: MechanicShop.Domain/Branches/EmployeeBranchMembership.cs.

العضوية تربط EmployeeId بـBranchId وتحمل IsDefault. Create ترفض معرف موظف أو فرع فارغًا. SetDefault تغير حالة الافتراضية.

العلاقة ليست many-to-many بسيطة؛ عضوية الموظف لها بيانات وقواعد، أهمها الفرع الافتراضي. مفتاحها المركب في EF هو EmployeeId وBranchId، لذلك لا تتكرر عضوية الموظف نفسها.

### AccessScope وEmployee

الملفات: MechanicShop.Domain/Identity/AccessScope.cs وMechanicShop.Domain/Employees/Employee.cs وEmployeeErrors.cs.

Role الحالي لم يتغير: Manager وLabor. AccessScope يضيف بُعدًا للصلاحية: Branch أو Company. Employee.Create وSetAccessScope يرفضان Company لأي Role غير Manager. دي قاعدة Domain وليست مجرد إخفاء زر في واجهة المستخدم.

FirstName وLastName وRole لا تتغير بعد الإنشاء في النموذج الحالي. AccessScope له private setter لأن تغييره يمر بدالة تتحقق من القاعدة. الـprivate constructor مخصص لاسترجاع EF Core.

### WorkOrder

الملفات: MechanicShop.Domain/Workorders/Workorder.cs وWorkOrderErrors.cs.

أضيف BranchId إلى WorkOrder، وCreate ترفضه لو كان Guid.Empty. RowVersion قيمة يملؤها SQL Server ويستخدمها EF Core؛ لا يحددها المستخدم داخل Domain.

باقي قواعد WorkOrder ما زالت مطبقة داخل الكيان: نهاية الموعد بعد بدايته، وجود Repair Tasks عند الإنشاء، وصحة Spot وانتقالات الحالة. إضافة الفرع والتزامن لم تنقل قواعد النشاط إلى Controller.

## 5. Application Layer

Application تمثل حالات الاستخدام. Command يطلب تغييرًا، وQuery يطلب قراءة، وHandler ينفذ حالة الاستخدام، وValidator يرفض مدخلات غير صالحة قبل دخول الـHandler.

### ICurrentBranchContext

الملف: MechanicShop.Application/Common/Interfaces/ICurrentBranchContext.cs.

الواجهة توفر EmployeeId وBranchId وIsCompanyWide. الـHandlers تعتمد على الواجهة بدل HttpContext، وInfrastructure توفر التنفيذ الذي يقرأ claims. ده يسهل الاختبار ويمنع ربط Application مباشرةً بالويب.

IAppDbContext اتعدل لإضافة BeginTransactionAsync، وIIdentityService اتعدل لتمرير الفرع المختار عند الحاجة لإصدار بيانات المستخدم أو تجديد التوكن.

### ملفات Feature الفروع بالتفصيل

في مجموعة AddEmployeeToBranch، الملف AddEmployeeToBranchCommand.cs يعرّف مدخلات الأمر، وAddEmployeeToBranchCommandValidator.cs يتحقق من BranchId، وAddEmployeeToBranchCommandHandler.cs ينفذ فحص الصلاحية والوجود والتكرار ثم الحفظ.

في مجموعة CreateBranch، الملف CreateBranchCommand.cs يعرّف Name وCode، وCreateBranchCommandValidator.cs يتحقق من المدخلات النصية، وCreateBranchCommandHandler.cs يطلب إنشاء Domain Branch ويحفظه ويتعامل مع تكرار الكود.

في SetBranchActive، الملف SetBranchActiveCommand.cs يعرّف BranchId وIsActive، وSetBranchActiveCommandHandler.cs يبحث عن الفرع، ويفحص default memberships، ثم يغير حالته ويحفظ.

في SetDefaultBranch، الملف SetDefaultBranchCommand.cs يحمل BranchId المطلوب، وSetDefaultBranchCommandHandler.cs يتحقق من هوية الموظف وعضويته ونشاط الفرع، ثم يغير default داخل Transaction.

في SetEmployeeAccessScope، الملف SetEmployeeAccessScopeCommand.cs يحمل EmployeeId وAccessScope، وSetEmployeeAccessScopeCommandValidator.cs يتحقق من قيمة النطاق، وSetEmployeeAccessScopeCommandHandler.cs يراجع صلاحية مدير الشركة ويستدعي قاعدة Employee.

في Queries، GetBranchesQuery.cs وGetBranchesQueryHandler.cs يطلبان القائمة وينفذانها حسب نطاق المستخدم؛ GetBranchByIdQuery.cs وGetBranchByIdQueryHandler.cs يطلبان فرعًا محددًا مع فحص الوصول؛ GetBranchEmployeesQuery.cs وGetBranchEmployeesQueryHandler.cs يطلبان أعضاء فرع ضمن قيود المدير والفرع. BranchDto.cs وBranchEmployeeDto.cs يحددان بيانات الخروج اللازمة لهذه القراءات.

### Feature الفروع

كل Commands وQueries موجودة تحت MechanicShop.Application/Features/Branches.

#### Commands

- AddEmployeeToBranchCommand: يطلب إضافة Employee إلى Branch. الـHandler يتحقق من نطاق المدير، وأن الفرع نشط، وأن الموظف والفرع موجودان، وأن العضوية غير مكررة. أول Membership للموظف تصبح default.
- CreateBranchCommand: يطلب إنشاء فرع. الـHandler يسمح بنطاق Company فقط، ينشئ Domain Branch، ويحفظه. تكرار Code يرجع Conflict.
- SetBranchActiveCommand: يغير IsActive. لا يسمح بتعطيل فرع له موظفون يعتبرونه فرعًا افتراضيًا؛ يجب نقل default memberships أولًا.
- SetDefaultBranchCommand: يغير الفرع الافتراضي للموظف الحالي بعد التحقق من عضويته ونشاط الفرع.
- SetEmployeeAccessScopeCommand: يغير نطاق موظف، مع فحص الدور وقاعدة Domain التي تمنع Company لغير Manager.

ملفات Commands وHandlers وValidators منفصلة، مثل AddEmployeeToBranchCommand.cs وAddEmployeeToBranchCommandHandler.cs وAddEmployeeToBranchCommandValidator.cs. الـValidator يتحقق من شكل المدخلات، والـHandler يتحقق من حالة قاعدة البيانات والصلاحية.

SetDefaultBranchCommandHandler ينفذ خطوتين داخل Transaction: يصفر IsDefault عن عضويات الموظف ويحفظ، ثم يجعل الفرع المطلوب default ويحفظ ويعمل Commit. السبب إن الـfiltered unique index يمنع وجود defaultين في الوقت نفسه. عند الفشل يحصل Rollback.

#### Queries وDTOs

- GetBranchesQuery وGetBranchesQueryHandler: مدير الشركة يرى الفروع كلها، والمستخدم العادي يرى الفروع التي له Membership فيها.
- GetBranchByIdQuery وGetBranchByIdQueryHandler: قراءة فرع واحد مع تطبيق قاعدة النطاق والعضوية.
- GetBranchEmployeesQuery وGetBranchEmployeesQueryHandler: عرض أعضاء فرع؛ هذا المسار مقيد بدور Manager في API.
- BranchDto وBranchEmployeeDto: شكل بيانات الخروج، بدل إرجاع EF entities للعميل.

### تعديلات WorkOrders

اتعدلت Commands وHandlers وValidators في AssignLabor وCreateWorkOrder وDeleteWorkOrder وRelocateWorkOrder وUpdateOrderState وUpdateWorkOrderRepairTasks.

التعديلات تضيف فحص RowVersion في الأوامر التي تعدل سجلًا قائمًا، وتتحقق من Membership للفني عند إسناده إلى WorkOrder فرع آخر. Validators ترفض RowVersion المفقودة أو الفارغة. Handlers ما زالت تفحص Not Found وقواعد الحالة ومواعيد العمل والتعارضات الخاصة بكل عملية.

WorkOrderDto وWorkOrderListItemDto وWorkOrderMapper وGetWorkOrdersQueryHandler اتعدلوا لإرجاع RowVersion ضمن نتيجة القراءة. بهذه الطريقة يحصل العميل على الرمز الذي يحتاجه في أمر التعديل التالي.

### Identity وLabors وCache

- AppUserDto أصبح يحمل BranchId وAccessScope.
- GenerateTokenQuery وGenerateTokenQueryhandler يمرران الفرع المختار لمسار إصدار التوكن.
- RefreshTokenQueryHandler يعيد تحميل بيانات المستخدم الحالية قبل إصدار توكن جديد، كي لا يعتمد على نطاق قديم.
- GetLaborsQueryHandler يطبق قيود العضوية المناسبة لعرض الفنيين في السياق.
- CachingBehavior أضاف Scope وBranchId إلى cache key كي لا تتشارك الاستعلامات نتائج فروع أو نطاقات مختلفة.
- ApplicationErrors أضيفت إليه أخطاء Application المستخدمة في حالات النطاق والفرع والتعارض.

Result Pattern يعني إعادة Result يحمل قيمة أو Error موحد، بدل استخدام الاستثناءات للحالات المتوقعة مثل NotFound وForbidden وConflict.

في ملفات Identity، GenerateTokenQuery.cs وGenerateTokenQueryhandler.cs يحملان اختيار الفرع إلى إصدار التوكن، وRefreshTokenQueryHandler.cs يعيد بناء معلومات الحساب الحالية عند التجديد. AppUserDto.cs هو النموذج الذي ينقل BranchId وAccessScope من Infrastructure إلى Application ثم TokenProvider.

## 6. Infrastructure Layer

### AppDbContext وGlobal Query Filters

الملف: MechanicShop.Infrastructure/Date/AppDbContext.cs.

اتضافت DbSets للفروع والعضويات، واتمرر ICurrentBranchContext للـContext. Global Query Filter هو شرط يضيفه EF Core تلقائيًا على استعلامات الكيان.

~~~csharp
builder.Entity<WorkOrder>()
    .HasQueryFilter(workOrder => IsCompanyWide
        || (CurrentBranchId.HasValue
            && workOrder.BranchId == CurrentBranchId.Value));
~~~

السطر الأول يحدد WorkOrder ككيان له فلتر. السطر الثاني يسمح بقراءة الشركة الواسعة، أو يطلب تطابق BranchId مع الفرع الحالي. Invoice له فلتر مشابه يمر عبر WorkOrder.BranchId، لأنه لا يحمل BranchId مستقلًا.

الـfilter يقلل احتمال نسيان شرط الفرع في Query، لكنه لا يثبت هوية المستخدم ولا يحمي Endpoint غير محمي؛ لذلك ActiveBranchAccess policy تظل ضرورية.

### Domain Events والحفظ

SaveChangesAsync يجمع الكيانات والأحداث قبل استدعاء EF Core. بعد نجاح الحفظ ينشر الأحداث، وبعد الفشل يمسحها ولا ينشرها. السبب إن الكيان قد يصبح Detached بعد الحذف، ولأن إرسال إشعار قبل نجاح الحفظ يعطي معلومة عن تغيير لم يثبت في قاعدة البيانات.

### EF Configurations

- BranchConfiguration: المفتاح الأساسي، أطوال Name وCode، وUnique Index على Code.
- EmployeeBranchMembershipConfiguration: مفتاح مركب EmployeeId وBranchId، علاقات FK للموظف والفرع، وUnique Filtered Index على EmployeeId بشرط IsDefault = 1.
- EmployeeConfiguration: تخزين Role وAccessScope كقيم نصية مع AccessScope مطلوب.
- WorkOrderConfiguration: BranchId مطلوب وFK إلى Branches مع Restrict، وRowVersion باستخدام IsRowVersion، وفهرس مركب على BranchId وStartAtUtc وEndAtUtc. تم الإبقاء على علاقات WorkOrder الأخرى الموجودة في المشروع.

Restrict على Branch يعني إن قاعدة البيانات ترفض حذف فرع ما زالت أوامر الشغل تشير إليه. أما Memberships فتحذف مع الموظف أو الفرع حسب إعداد Cascade.

### Identity وAuthorization Infrastructure

CurrentBranchContext يقرأ EmployeeId من ClaimTypes.NameIdentifier وBranchId من branch_id وCompany scope من access_scope.

ActiveBranchAccessRequirement يمثل شرط الـpolicy. ActiveBranchAccessHandler يقرأ claims، ويتأكد من وجود Employee، ومن تطابق Role وAccessScope داخل قاعدة البيانات مع claims، ثم يتحقق من نشاط الفرع وMembership. مدير الشركة يستثنى من شرط Membership فقط، لكنه يحتاج فرعًا نشطًا.

IdentityService يطبق فحصًا مشابهًا عند تسجيل الدخول أو تحميل بيانات الحساب: Manager/Company يستطيع اختيار أي فرع نشط، وباقي الحسابات تختار فرعًا عضوًا فيه أو default membership.

TokenProvider يضيف branch_id وaccess_scope والدور إلى JWT. توقيع التوكن يمنع تعديل محتواه من غير اكتشاف ذلك، والتحقق من قاعدة البيانات يمنع الاعتماد على claim لم تعد مطابقة للصلاحيات الحالية.

### Database Initialization وSeed Data

ApplicationDbContextInitialiser ينفذ MigrateAsync قبل Seed. لا يستخدم EnsureCreated كبديل عن Migrations. إذا كان Database:ResetOnStartup مفعّلًا، يسمح بالحذف فقط في Development أو Testing، ويرفض قواعد SQL النظامية.

~~~csharp
if (_configuration.GetValue<bool>("Database:ResetOnStartup"))
{
    if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("إعادة إنشاء قاعدة البيانات مسموحة في Development وTesting فقط.");

    await _context.Database.EnsureDeletedAsync();
}

await _context.Database.MigrateAsync();
~~~

الشرط الأول يجعل إعادة الضبط اختيارًا صريحًا من الإعداد. الشرط الثاني يوقف العملية في أي بيئة غير Development أو Testing. الحذف يقع فقط بعد اجتياز الشرطين، ثم MigrateAsync يطبق تاريخ الـschema على قاعدة البيانات. الكود الفعلي يحتوي كذلك على فحص يمنع حذف قواعد SQL النظامية.

SeedDataIds يحتوي GUIDs ثابتة للفروع وأوامر الشغل الأساسية. الـSeed ينشئ فرعي Central Workshop وNorth Workshop بكودي MAIN وNORTH، ثم يضيف Memberships للموظفين وبها default واحدة لكل موظف، ثم ينشئ WorkOrders بفرع وفني وVehicle وRepair Tasks متسقة.

الـMigration AddBranchesAndWorkOrderRowVersion ترفض الترحيل إذا كانت WorkOrders قديمة موجودة. هذا متعمد: لا يوجد mapping تاريخي معتمد لتوزيعها. بيئة Development/Test تقدر تستخدم reset المسموح، أما Production فتحتاج mapping أو خطة ترحيل بيانات معتمدة؛ لا يوجد حذف Production تلقائي.

## 7. API وSecurity

### BranchesController

الملف: MechanicShop.Api/Controllers/BranchesController.cs.

كل المسارات تحت api/v{version:apiVersion}/branches، وتستخدم ISender لإرسال Command أو Query. المسارات هي:

| المسار | الوظيفة | القيد الإضافي |
|---|---|---|
| GET /branches | عرض الفروع المتاحة | ActiveBranchAccess |
| GET /branches/{branchId} | عرض فرع | ActiveBranchAccess |
| GET /branches/{branchId}/employees | عرض موظفي فرع | ManagerOnly وActiveBranchAccess |
| POST /branches | إنشاء فرع | ManagerOnly، والـHandler يشترط Company scope |
| PUT /branches/{branchId}/active | تفعيل أو تعطيل | ManagerOnly، والـHandler يشترط Company scope |
| POST /branches/{branchId}/employees/{employeeId} | إضافة عضوية | ManagerOnly مع فحص نطاق الفرع |
| PUT /branches/employees/{employeeId}/access-scope | تغيير النطاق | ManagerOnly مع فحص Company scope في الـHandler |
| PUT /branches/me/default | تغيير default للمستخدم الحالي | Membership للفرع مطلوبة |

وجود Authorize وActiveBranchAccess على Controller يحمي المجموعة، وManagerOnly يضيف قيد الدور على عمليات الإدارة. الـHandlers تعيد فحص القاعدة حيث تكون مهمة للعملية؛ لذلك لا تعتمد العملية على واجهة المستخدم.

### WorkOrder API وHTTP 409

WorkOrderController يمرر RowVersion إلى Application. الحذف يأخذ If-Match، ويفك قيمة Base64 إلى byte array. إذا كانت القيمة غير سليمة يرجع 400.

عند اكتشاف DbUpdateConcurrencyException، Application يرجع Conflict Error. ProblemExtensions يحول ErrorKind.Conflict إلى HTTP 409. العميل بعدها يعيد قراءة WorkOrder ويقرر إن كان سيعيد تطبيق تعديله.

~~~csharp
_context.WorkOrders.Entry(workOrder).Property(order => order.RowVersion).OriginalValue = command.RowVersion!;
try
{
    await _context.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException)
{
    return WorkOrderErrors.ConcurrencyConflict;
}
~~~

السطر الأول يضع RowVersion القادمة من العميل كقيمة أصلية يتوقعها EF Core للسجل. SaveChangesAsync ينفذ UPDATE المشروط بها. لو لم يعد الصف يحمل نفس القيمة، EF يرمي DbUpdateConcurrencyException. الـcatch يحول الاستثناء إلى WorkOrderErrors.ConcurrencyConflict. علامة التعجب في command.RowVersion إخبار للمترجم أن Validator سبق أن ضمن وجود القيمة، وليست فحصًا وقت التشغيل.

## 8. شرح RowVersion بمثال

Race Condition هي نتيجة تعتمد على ترتيب متداخل لعمليات مستخدمين متزامنة. Lost Update هو نوع منها: مستخدم A ومستخدم B يقرآن الحالة نفسها، ثم يحفظ B تعديلًا، وبعده يحفظ A نسخته القديمة فيمحو تعديل B.

في SQL Server، RowVersion قيمة ثنائية يغيرها المحرك تلقائيًا عند تحديث الصف. EF Core مع IsRowVersion يعتبرها concurrency token.

~~~mermaid
sequenceDiagram
    participant A as المستخدم A
    participant B as المستخدم B
    participant API as MechanicShop API
    participant DB as SQL Server
    A->>API: قراءة WorkOrder مع RowVersion R1
    B->>API: قراءة WorkOrder مع RowVersion R1
    B->>API: تعديل مع R1
    API->>DB: UPDATE بشرط RowVersion = R1
    DB-->>API: حفظ، والنسخة تصبح R2
    A->>API: تعديل قديم مع R1
    API->>DB: UPDATE بشرط RowVersion = R1
    DB-->>API: صفر صفوف مطابقة
    API-->>A: 409 Conflict
~~~

شرط EF العملي يكون قريبًا من: تحديث الصف حيث Id يساوي المعرف وRowVersion تساوي القيمة الأصلية. لو القيمة القديمة لم تعد موجودة، يعتبر EF أن السجل تغير ويرمي DbUpdateConcurrencyException.

RowVersion لا يدمج التعديلين تلقائيًا، ولا يمنع إنشاء WorkOrders متعارضة زمنيًا؛ هو يحمي تحديث الصف نفسه. تعارض مواعيد الفني أو Vehicle يحتاج قواعد الجدولة الموجودة في الـHandlers.

## 9. قاعدة البيانات قبل وبعد

قبل التعديل لم يكن هناك جدول Branches أو EmployeeBranchMemberships، ولم يكن WorkOrders يحتوي BranchId أو RowVersion.

بعد التعديل:

~~~mermaid
erDiagram
    Branch ||--o{ WorkOrder : owns
    Branch ||--o{ EmployeeBranchMembership : permits
    Employee ||--o{ EmployeeBranchMembership : belongs
    Employee o|--o{ WorkOrder : assigned_labor
    Customer ||--o{ Vehicle : owns
    Vehicle ||--o{ WorkOrder : serviced_by
    WorkOrder ||--o| Invoice : bills
    WorkOrder }o--o{ RepairTask : includes
~~~

| الجدول | المفتاح/العلاقة | القيود والفهارس المهمة |
|---|---|---|
| Branches | PK على Id | Code مطلوب وفريد، IsActive مطلوب |
| EmployeeBranchMemberships | PK مركب EmployeeId وBranchId | FK للموظف والفرع؛ filtered unique index يضمن default واحدة كحد أقصى لكل موظف |
| Employees | PK على Id | Role وAccessScope يخزنان كنص؛ AccessScope مطلوب |
| WorkOrders | PK على Id؛ BranchId FK مطلوب | RowVersion من SQL Server؛ FK الفرع Restrict؛ فهرس BranchId وStartAtUtc وEndAtUtc |
| Invoices | مرتبطة بـWorkOrder بعلاقة واحد لواحد | نطاق الفرع يصل إليها عبر WorkOrder |
| WorkOrderRepairTasks | جدول ربط | علاقة Many-to-Many موجودة بين WorkOrder وRepairTask |

الـIndex الفريد على Code يمنع تكرار كود الفرع حتى لو وصل طلبان متزامنان. والـIndex المركب على الفرع والأوقات يساعد قاعدة البيانات في استعلامات WorkOrders ضمن فرع وفترة زمنية.

## 10. الاختبارات ولماذا أضيفت

### Domain Unit Tests

- tests/MechanicShop.Domain.UniTests/Employees/EmployeeTets.cs: يغطي AccessScope الافتراضي Branch، والسماح بـCompany للـManager، ورفضه لـLabor، ورفض enum غير صالح. الهدف حماية invariants داخل Employee.
- tests/MechanicShop.Domain.UniTests/WorkOrders/WorkOrderTests.cs: يغطي إنشاء WorkOrder بفرع مطلوب والتحقق من مدخلاته. الهدف منع كيان تشغيل بلا BranchId.

### Application Unit Tests

- tests/MechanicShop.Application.UniTests/Behaviours/CachingBehaviorTests.cs: يتحقق من اختلاف cache key بين branch context مختلف ونطاق Company. الهدف منع تسرب نتيجة بين نطاقي صلاحية.
- PerformanceBehaviourTests الموجودة تختبر مسار الطلب السريع والبطيء؛ شرط 500ms يجعل تغطية API وحدها حساسة لسرعة الجهاز.

### API Integration Tests

- tests/MechanicShop.Api.IntegrationTests/Controllers/BranchesControllerTests.cs: يختبر Endpoints الفروع والصلاحيات والعضويات من خلال HTTP فعلي.
- tests/MechanicShop.Api.IntegrationTests/Controllers/IdentityControllerTests.cs: يختبر اختيار الفرع أثناء Identity flow، وقصر Branch-scoped users على Membership.
- tests/MechanicShop.Api.IntegrationTests/Controllers/WorkOrdersControllerTests.cs: يختبر HTTP contract لأوامر الشغل، ومنها RowVersion وIf-Match وعزل الفروع.
- tests/MechanicShop.Api.IntegrationTests/Infrastructure/DatabaseInitializationTests.cs: يشغل التهيئة والـSeed مرتين ويفحص عدم التكرار، وصحة الفروع والعضويات وعلاقات WorkOrders.

اختيرت Integration Tests هنا لأن HTTP authentication وJWT وEF filters وSQL Server تتفاعل مع بعضها، ولا يكفي Mock لكل طبقة لإثبات السلوك النهائي.

### Subcutaneous Tests والبنية المشتركة

تم تعديل Handlers وValidators في tests/MechanicShop.Application.SubcutaneiousTests/Features/WorkOrders لاستخدام RowVersion والتحقق من الفرع والـMembership. تشمل الاختبارات إنشاء WorkOrder، إسناد الفني، النقل، تحديث الحالة والمهام، والحذف.

الاختبار Handle_WhenRowVersionIsStale_ShouldRejectUpdateWithoutDispatchingSideEffects يتحقق أن النسخة القديمة ترجع Conflict، وأن الحالة المحفوظة لا تتغير، ولا تخرج إشعارات أو رسائل Email/SMS نتيجة تعديل لم يُحفظ.

tests/MechanicShop.Tests.Common/Branches يحتوي TestCurrentBranchContext وTestBranchIds لمساعدة الاختبارات على إنشاء سياق فرع معروف. WorkOrderFactory اتعدل لتكوين WorkOrders بفرع صالح. WebAppFactory في API وSubcutaneous يجهز Testing environment وقاعدة مستقلة لكل مجموعة.

## 11. رحلة عمليات رئيسية

### إضافة موظف إلى فرع

1. العميل يرسل POST إلى مسار الموظف والفرع.
2. ActiveBranchAccess يتحقق من المستخدم والفرع النشط.
3. ManagerOnly يراجع الدور.
4. AddEmployeeToBranchCommandHandler يراجع هل المدير Company scope أو يطابق الفرع النشط.
5. يتحقق من وجود الفرع ونشاطه ووجود الموظف وعدم تكرار Membership.
6. ينشئ EmployeeBranchMembership ويحفظها.
7. لو دي أول Membership، يجعلها default.

### تعديل WorkOrder

1. العميل يقرأ WorkOrder ويحتفظ بـRowVersion.
2. يرسل التعديل مع RowVersion.
3. API يبني Command ويرسله عبر ISender.
4. Validator يتحقق من وجود RowVersion بالشكل المطلوب.
5. Handler يحمل WorkOrder ضمن query filter الخاص بالفرع.
6. Domain يراجع صلاحية الانتقال أو التغيير.
7. EF Core يحفظ بشرط تطابق RowVersion.
8. عند نجاح الحفظ تُرسل Domain Events؛ عند التعارض يرجع 409 ولا تُرسل side effects.

## 12. القرارات المعمارية والبدائل

| القرار | السبب | البديل | المفاضلة |
|---|---|---|---|
| Customer وVehicle مشتركان | بيانات العميل والمركبة على مستوى المنشأة في النموذج الحالي | نسخ العميل والمركبة لكل فرع | المشاركة تمنع التكرار، لكنها تتطلب سياسة واضحة لمن يرى بيانات العملاء مستقبلًا |
| Membership ككيان | العلاقة تحمل IsDefault وقواعد فريدة | many-to-many مباشر | البنية الحالية أوضح للقواعد، لكنها تضيف جدولًا وعمليات إدارية |
| AccessScope منفصل عن Role | نفس الدور قد يعمل على فرع أو الشركة | أدوار منفصلة لكل مستوى | فصل البعدين يقلل عدد الأدوار، لكنه يتطلب التحقق من اتساقهما |
| Global Query Filters | إضافة عزل الفرع تلقائيًا لاستعلامات EF المعتادة | كتابة شرط BranchId في كل Query | تقل أخطاء النسيان، لكن الفلاتر تحتاج انتباهًا في الاستعلامات التي تتجاوزها عمدًا |
| RowVersion | كشف النسخة القديمة بكلفة قليلة في SQL Server | قفل pessimistic أو مقارنة timestamps يدوية | لا يمنع التعديل المتزامن، بل يكتشف التعارض ويرجع 409؛ العميل يعالج إعادة المحاولة |
| Migrations وSeed ثابت | تكرار تهيئة Development/Test بشكل متوقع | EnsureCreated أو IDs عشوائية | Migrations تحافظ على تاريخ schema، لكن البيانات القديمة تحتاج mapping قبل فرض BranchId |

## 13. مراجعة الملفات المعدلة حسب الطبقة

القائمة التالية تربط الملفات التي ظهرت في تغييرات الفرع بمسؤوليتها، بما فيها تعديلات الاختبارات. الملفات الجديدة موضحة ضمن مجموعاتها، والملفات القريبة مجمعة لأن تعديلها يخدم نفس السلوك.

### Contracts وAPI

| الملف/المجموعة | التغيير وسبب الحاجة |
|---|---|
| Contracts/Requests/Branches/CreateBranchRequest.cs وSetBranchActiveRequest.cs وSetDefaultBranchRequest.cs وSetEmployeeAccessScopeRequest.cs | نماذج إدخال عمليات إنشاء الفرع وتفعيله وتغيير default وAccessScope؛ فصل HTTP payload عن Commands |
| Contracts/Requests/WorkOrders/AssignLaborRequest.cs وModifyRepairTaskRequest.cs وRelocateWorkOrderRequest.cs وUpdateWorkOrderStateRequest.cs | نقل RowVersion من العميل مع عمليات التعديل لمنع الكتابة على نسخة قديمة |
| MechanicShop.Api/Controllers/BranchesController.cs | Controller جديد يربط مسارات Branches بـQueries وCommands |
| MechanicShop.Api/Controllers/WorkOrderController.cs | تحويل RowVersion وIf-Match إلى أوامر Application، وإرجاع 400 للرمز غير السليم |
| MechanicShop.Api/Controllers/ApiController.cs | توحيد مسار تحويل أخطاء Result إلى استجابة API كما يستخدمه الـController |
| MechanicShop.Api/Controllers/DashboardController.cs وInvoicesController.cs وLaborsController.cs | فرض سياسة وصول الفرع على قراءات بيانات التشغيل ذات الصلة |
| MechanicShop.Api/DependencyInjection.cs | تسجيل CurrentBranchContext وAuthorization policies/handlers |
| MechanicShop.Api/Extensions/ProblemExtensions.cs | تحويل Conflict إلى HTTP 409، NotFound إلى 404، Validation إلى 400، وForbidden إلى 403 |
| MechanicShop.Api/appsettings.Development.json | تفعيل ResetOnStartup في إعداد Development فقط؛ الحماية داخل Initialiser تمنع استخدامه في Production |

### Application

| الملف/المجموعة | التغيير وسبب الحاجة |
|---|---|
| Common/Interfaces/ICurrentBranchContext.cs | abstraction لقراءة الموظف والفرع والنطاق داخل Application |
| Common/Interfaces/IAppDbContext.cs | إضافة بداية Transaction لتغيير default membership ذريًا |
| Common/Interfaces/IIdentityService.cs | تمرير اختيار الفرع عبر تدفق Identity |
| Common/Behaviours/CachingBehavior.cs | جعل cache key خاصًا بالفرع والنطاق لمنع تسرب نتائج cache |
| Common/Errors/ApplicationErrors.cs | تعريف أخطاء Application المستخدمة في حالات النطاق والفرع والتعارض |
| Features/Branches/Commands/AddEmployeeToBranch/AddEmployeeToBranchCommand.cs وAddEmployeeToBranchCommandHandler.cs وAddEmployeeToBranchCommandValidator.cs | تعريف أمر إضافة الموظف، التحقق من المدخلات، وفحص الفرع والموظف والعضوية والصلاحية قبل الحفظ |
| Features/Branches/Commands/CreateBranch/CreateBranchCommand.cs وCreateBranchCommandHandler.cs وCreateBranchCommandValidator.cs | تعريف أمر إنشاء الفرع، التحقق من الاسم والكود، وحفظ Branch مع تحويل تكرار الكود إلى Conflict |
| Features/Branches/Commands/SetBranchActive/SetBranchActiveCommand.cs وSetBranchActiveCommandHandler.cs | تعريف أمر تفعيل/تعطيل الفرع ومنع تعطيل فرع له default memberships |
| Features/Branches/Commands/SetDefaultBranch/SetDefaultBranchCommand.cs وSetDefaultBranchCommandHandler.cs | تعريف تغيير الفرع الافتراضي والتحقق من العضوية والنشاط، ثم التغيير داخل Transaction |
| Features/Branches/Commands/SetEmployeeAccessScope/SetEmployeeAccessScopeCommand.cs وSetEmployeeAccessScopeCommandHandler.cs وSetEmployeeAccessScopeCommandValidator.cs | تعريف تغيير نطاق الموظف والتحقق من شكل القيمة والدور والنطاق المسموح |
| Features/Branches/Queries/GetBranches/GetBranchesQuery.cs وGetBranchesQueryHandler.cs | طلب قائمة الفروع وتنفيذها حسب Company scope أو Memberships |
| Features/Branches/Queries/GetBranchById/GetBranchByIdQuery.cs وGetBranchByIdQueryHandler.cs | طلب فرع واحد والتحقق من صلاحية قراءته |
| Features/Branches/Queries/GetBranchEmployees/GetBranchEmployeesQuery.cs وGetBranchEmployeesQueryHandler.cs | طلب قائمة موظفي فرع وتنفيذ القراءة ضمن قيود الفرع |
| Features/Branches/Dtos/BranchDto.cs وBranchEmployeeDto.cs | نماذج خرج Branch وBranch Employee من غير إرجاع EF entities |
| Features/Identity/Dtos/AppUserDto.cs وQueries/GenerateTokens/GenerateTokenQuery.cs وGenerateTokenQueryhandler.cs وQueries/RefreshTokens/RefreshTokenQueryHandler.cs | نقل BranchId وAccessScope من Identity إلى إصدار وتجديد JWT |
| Features/Labors/Queries/GetLabors/GetLaborsQueryHandler.cs | جعل قائمة الفنيين تراعي عضوية الفرع المستخدمة في العملية |
| Features/WorkOrders/Dtos/WorkOrderDto.cs وWorkOrderListItemDto.cs وMappers/WorkOrderMapper.cs وQueries/GetWorkOrders/GetWorkOrdersQueryHandler.cs | إخراج RowVersion مع بيانات WorkOrder وتطبيق سياق الفرع |
| Features/WorkOrders/common/AssignLabor/AssignLaborCommand.cs وAssignLaborCommandHandler.cs وAssignLaborCommandValidator.cs | تمرير RowVersion وفحص عضوية الفني بالفرع عند الإسناد |
| Features/WorkOrders/common/CreateWorkORDER/CreateWorkOrderCommandHandler.cs | ربط WorkOrder الجديد بالفرع النشط والتحقق من ملاءمة الفني والجدولة |
| Features/WorkOrders/common/DeleteWorkOrder/DeleteWorkOrderCommand.cs وDeleteWorkOrderCommandHandler.cs وDeleteWorkOrderCommandValidator.cs | تمرير RowVersion والتحقق منها قبل الحذف |
| Features/WorkOrders/common/RelocateWorkOrder/RelocateWorkOrderCommand.cs وRelocateWorkOrderCommandHandler.cs وRescheduleAppointmentCommandValidator.cs | تمرير RowVersion مع النقل وإبقاء قواعد الجدولة والتحقق |
| Features/WorkOrders/common/UpdateOrderState/UpdateWorkOrderStateCommand.cs وUpdateWorkOrderStateCommandHandler.cs وUpdateWorkOrderStateCommandValidator.cs | التحقق من النسخة قبل تغيير الحالة وحماية side effects من التعديل الفاشل |
| Features/WorkOrders/common/UpdateWorkOrderRepairTasks/UpdateWorkOrderRepairTasksCommand.cs وUpdateWorkOrderRepairTasksCommandHandler.cs وUpdateWorkOrderRepairTasksCommandValidator.cs | التحقق من النسخة قبل تعديل Repair Tasks وتطبيق قواعد WorkOrder الحالية |

### Domain وInfrastructure

| الملف/المجموعة | التغيير وسبب الحاجة |
|---|---|
| Domain/Branches/Branch.cs وBranchErrors.cs | كيان الفرع وقواعد إنشائه وتفعيله والأخطاء الخاصة به |
| Domain/Branches/EmployeeBranchMembership.cs | نموذج عضوية الموظف والفرع والافتراضي |
| Domain/Identity/AccessScope.cs | تمثيل Branch أو Company scope من غير تغيير Role القائم |
| Domain/Employees/Employee.cs وEmployeeErrors.cs | إضافة AccessScope ومنع Company scope لغير Manager |
| Domain/Workorders/Workorder.cs وWorkOrderErrors.cs | BranchId المطلوب وأخطاء التزامن ضمن كيان WorkOrder |
| Infrastructure/Date/AppDbContext.cs | DbSets، فلاتر الفرع والفاتورة، Transactions، وترتيب إرسال Domain Events بعد نجاح الحفظ |
| Infrastructure/Date/Configurations/BranchConfiguration.cs | قيود Branch وUnique Code |
| Infrastructure/Date/Configurations/EmployeeBranchMembershipConfiguration.cs | PK مركب وFKs وIndex للفرع الافتراضي |
| Infrastructure/Date/Configurations/EmployeeConfiguration.cs | تخزين AccessScope كقيمة مطلوبة |
| Infrastructure/Date/Configurations/WorkOrderConfiguration.cs | Branch FK وRowVersion والفهرس المركب |
| Infrastructure/Date/ApplicationDbContextInitialiser.cs | MigrateAsync، reset الآمن، وإنشاء بيانات مرجعية وتشغيلية متسقة |
| Infrastructure/Date/SeedDataIds.cs | GUIDs ثابتة للفروع وأوامر Seed |
| Infrastructure/Migrations/20261103163357_AddBranchesAndWorkOrderRowVersion.cs و20261103163357_AddBranchesAndWorkOrderRowVersion.Designer.cs وAppDbContextModelSnapshot.cs | تعديل schema وsnapshot الخاصين بـEF Core |
| Infrastructure/DependencyInjection.cs | تسجيل خدمات CurrentBranchContext والـAuthorization المطلوبة |
| Infrastructure/identity/CurrentBranchContext.cs | تنفيذ الواجهة بقراءة JWT claims من HttpContext |
| Infrastructure/identity/policies/ActiveBranchAccessRequirement.cs وActiveBranchAccessHandler.cs | تنفيذ التحقق من المستخدم والدور والنطاق والفرع النشط والعضوية |
| Infrastructure/identity/IdentityService.cs | اختيار فرع مسموح أثناء Identity flow ومطابقة الدور والنطاق مع بيانات الموظف |
| Infrastructure/identity/TokenProvider.cs | إضافة branch_id وaccess_scope إلى JWT |

### الاختبارات والبنية المشتركة

المسارات في الجداول نسبية لجذر المستودع. في جداول Application، الاختصار Common أو Features يعني المجلد المقابل تحت MechanicShop.Application. وفي جدول الاختبارات، أسماء الملفات نسبةً إلى مجلد المشروع المكتوب في أول المسار.

| الملف/المجموعة | التغيير وسبب الحاجة |
|---|---|
| Api.IntegrationTests/Controllers/BranchesControllerTests.cs | اختبار Endpoints الفروع والصلاحيات والعضويات من خلال HTTP |
| Api.IntegrationTests/Infrastructure/DatabaseInitializationTests.cs | التحقق من تكرار التهيئة والـSeed والعلاقات |
| Api.IntegrationTests/Controllers/IdentityControllerTests.cs | اختبار اختيار الفرع وحدود الصلاحية في Identity |
| Api.IntegrationTests/Controllers/WorkOrdersControllerTests.cs | تحديث requests والاختبارات لاستخدام RowVersion والتحقق من الوصول حسب الفرع |
| Api.IntegrationTests/Common/AppHttpClient.cs | دعم إرسال بيانات التزامن المطلوبة للـAPI أثناء الاختبار |
| Api.IntegrationTests/Common/ITestDataBuilder{T}.cs | دعم إعداد بيانات اختبار مرتبطة بسياق الفرع |
| Api.IntegrationTests/Common/WebAppFactory.cs | بيئة Testing، قاعدة اختبار مستقلة، وتجهيز خدمات التطبيق للاختبارات |
| Application.SubcutaneiousTests/Common/WebAppFactory.cs | قاعدة منفصلة لـSubcutaneous وتهيئة الـmigrations والـseed وسياق الفرع |
| Application.SubcutaneiousTests/Common/SubcutaneousTestData.cs وTests.Common/WorkOrders/WorkOrderFactory.cs | تكوين WorkOrders صالحة مع BranchId والعلاقات اللازمة |
| Application.SubcutaneiousTests/Features/Identity/Queries/RefreshTokens/RefreshTokenQueryHandlerTests.cs | اختبار أن تجديد التوكن يعيد سياق الفرع والنطاق الحاليين |
| Application.SubcutaneiousTests/Features/Labors/Queries/GetLabors/GetLaborsQueryHandlerTests.cs | اختبار نتائج الفنيين في سياق الفرع |
| Application.SubcutaneiousTests/Features/WorkOrders/Commands/AssignLabor/AssignLaborCommandHandlerTests.cs وAssignLaborCommandValidatorTests.cs | تحديث اختبارات Handler وValidator لقواعد العضوية وRowVersion |
| Application.SubcutaneiousTests/Features/WorkOrders/Commands/CreateWorkOrder/CreateWorkOrderCommandHandlerTests.cs | إضافة سياق فرع صالح واختبار قواعد إنشاء WorkOrder المرتبطة به |
| Application.SubcutaneiousTests/Features/WorkOrders/Commands/DeleteWorkOrder/DeleteWorkOrderCommandHandlerTests.cs وDeleteWorkOrderCommandValidatorTests.cs | إرسال النسخة المطلوبة واختبار الحذف والتعارض |
| Application.SubcutaneiousTests/Features/WorkOrders/Commands/RelocateWorkOrder/RelocateWorkOrderCommandHandlerTests.cs وRelocateWorkOrderCommandValidatorTests.cs | تحديث النقل والـValidator لاستخدام RowVersion |
| Application.SubcutaneiousTests/Features/WorkOrders/Commands/UpdateOrderState/UpdateWorkOrderStateCommandHandlerTests.cs | تحديث الاختبارات لإرسال RowVersion وإضافة اختبار stale token وعدم إرسال side effects |
| Application.SubcutaneiousTests/Features/WorkOrders/Commands/UpdateWorkOrderRepairTasks/UpdateWorkOrderRepairTasksCommandHandlerTests.cs وUpdateWorkOrderRepairTasksCommandValidatorTests.cs | تحديث Handler والـValidator لاستخدام RowVersion |
| Application.SubcutaneiousTests/Features/WorkOrders/Queries/GetWorkOrders/GetWorkOrdersQueryHandlerTests.cs | تثبيت بيانات الفرع والتحقق من نتائج القراءة حسب السياق |
| Application.UniTests/Behaviours/CachingBehaviorTests.cs | اختبار اختلاف مفاتيح cache بين الفرع والنطاق |
| Domain.UniTests/Employees/EmployeeTets.cs | اختبار قواعد AccessScope في Employee |
| Domain.UniTests/WorkOrders/WorkOrderTests.cs | اختبار أن BranchId مطلوب عند إنشاء WorkOrder |
| tests/MechanicShop.Tests.Common/Branches/TestBranchIds.cs وTestCurrentBranchContext.cs | أدوات مشتركة لسياق وهوية فرع ثابتة في الاختبارات |

## 14. أسئلة مقابلة وإجابات نموذجية

### ما الفرق بين Role وAccessScope؟

Role يحدد نوع المستخدم، مثل Manager أو Labor. AccessScope يحدد مدى البيانات، أي Branch أو Company. فصل الاثنين يمنع إنشاء عدد كبير من الأدوار لكل تركيبة صلاحيات.

### هل Global Query Filter كافٍ وحده؟

لأ. هو يطبق شرطًا تلقائيًا على استعلامات EF المعتادة، لكنه لا يثبت أن المستخدم يملك صلاحية الفرع، ولا يحمي Endpoint غير مربوط بسياسة Authorization. لذلك نستخدم ActiveBranchAccess أيضًا.

### ليه Membership كيان مستقل؟

لأن العلاقة تحتاج IsDefault وقيدًا يضمن default واحدة. جدول ربط بلا كيان في Domain لا يمثل قواعد العلاقة بوضوح.

### إزاي EF Core يعرف إن RowVersion تعارضت؟

IsRowVersion يجعلها concurrency token. EF يضع القيمة الأصلية في شرط UPDATE. لو لم يُحدّث أي صف بسبب اختلاف القيمة، يرمي DbUpdateConcurrencyException، والتطبيق يحول الحالة إلى 409.

### هل RowVersion تمنع Race Condition بالكامل؟

لا. هي تكشف التعديلات المتزامنة على الصف نفسه. لا تدمج التغييرات، ولا تمنع تعارض مواعيد WorkOrders منفصلة؛ تلك تحتاج قواعد جدولة.

### ليه تغيير الفرع الافتراضي داخل Transaction؟

لأن التغيير يحتاج إلغاء الافتراضي القديم وتعيين الجديد. المعاملة تضمن عدم بقاء الموظف في حالة جزئية لو فشلت الخطوة الثانية.

## 15. تمارين عملية للمطور

1. أضف فرعًا جديدًا من API ثم اكتب اختبارًا يمنع تكرار Code.
2. أضف Employee إلى فرعين، غيّر default، وتحقق من عضوية واحدة افتراضية.
3. أضف كيانًا تشغيليًا تابعًا لفرع، ثم أضف FK وIndex وGlobal Query Filter واختبار عزل.
4. افتح WorkOrder في عميلين، احفظ من الأول، ثم أرسل RowVersion القديمة من الثاني وتحقق من 409.
5. أضف Authorization rule لفرع غير نشط، واختبرها على مستويي API وHandler.
6. أضف استعلامًا إلى Dashboard وتحقق أن Company scope يرى البيانات عبر الفروع بينما Branch scope لا يتجاوز فرعه.

## 16. نتائج التحقق وحدود ما تم التأكد منه

التحقق الأخير: Build كامل ناجح بدون أخطاء أو تحذيرات. مجموع الاختبارات المنفذة من المشاريع الأربعة هو 433 ناجحًا، صفر فاشل، وصفر متخطٍ. Docker وTestcontainers كانا متاحين. كما أن EF Core لم يجد تغييرات Model غير مغطاة بـMigration.

تغطية Application المدمجة والمزالة منها نقاط التكرار ظلت ثابتة بين تشغيلتين: 89.4190% Line و70.6278% Branch و91.9192% Method. تغطية API وحدها قد تختلف قليلًا لأن PerformanceBehaviour يعتمد على زمن فعلي أكبر من 500ms؛ لذلك يجب مقارنة التغطية المدمجة مع Unit Tests، لا الاعتماد على API run واحد.

الـMigration لا تعيّن WorkOrders تاريخية إلى فروع من تلقاء نفسها. هذا الجزء يحتاج mapping معتمد في Production، بينما Development وTesting يستخدمان استراتيجية reset وseed المحمية. لم يتم إنشاء بيانات تاريخية أو تعيينات موظفين إنتاجية بالتخمين.

التغييرات موجودة على الفرع feature/multi-branch-rowversion، ولم يتم عمل Commit أو Push للتغييرات الحالية. ملفات Coverage المؤقتة المستخدمة في قياس الثبات أزيلت بعد المقارنة، التزامًا بطلب إبقاء ملفات المشروع دون تقارير Coverage مولدة.

## 17. شرح مقاطع الكود المضافة: الشرح ثم الكود ثم تفسيره

في الأقسام التالية، كل مقطع كود يسبقه سبب عرضه، ويتبعه تفسير مباشر لأسطره. المقاطع مقتطفات من الملفات الفعلية وليست بديلًا عن الملفات الكاملة؛ الهدف توضيح نقاط التغيير المحورية من غير تكرار المشروع كله داخل Markdown. أسماء الـIdentifiers محفوظة كما هي في C#.

### 17.1 إنشاء Branch والتحقق من مدخلاته

احتجنا Factory Method بدل إنشاء Branch مباشرةً، عشان كل مسار إنشاء يمر على نفس قواعد Domain قبل ما الكيان يبقى صالحًا للاستخدام.

الملف: `MechanicShop.Domain/Branches/Branch.cs`.

~~~csharp
public static Result<Branch> Create(Guid id, string name, string code)
{
    if (id == Guid.Empty)
    {
        return BranchErrors.IdRequired;
    }

    if (string.IsNullOrWhiteSpace(name))
    {
        return BranchErrors.NameRequired;
    }

    if (string.IsNullOrWhiteSpace(code))
    {
        return BranchErrors.CodeRequired;
    }

    return new Branch(id, name.Trim(), code.Trim().ToUpperInvariant());
}
~~~

شرح الكود سطرًا بسطر:

1. `Create` تستقبل المعرف والاسم والكود، وترجع `Result<Branch>`؛ يعني إما Branch صالح أو أخطاء Domain معروفة.
2. فحص `Guid.Empty` يمنع كيانًا بلا هوية.
3. `IsNullOrWhiteSpace(name)` يرفض الاسم الفارغ أو المكوّن من مسافات.
4. الفحص نفسه على `code` يمنع كودًا لا يمكن استخدامه لتعريف الفرع.
5. `Trim()` يزيل المسافات الطرفية، و`ToUpperInvariant()` يوحد كتابة الكود بصيغة كبيرة مستقرة.
6. الـconstructor لا يصبح متاحًا مباشرةً خارج الكلاس؛ لذلك لا يمكن تجاوز هذه الفحوص باستخدام `new Branch(...)` من بقية التطبيق.

الـUnique Index على `Code` في قاعدة البيانات يكمل القاعدة: Domain ينظف قيمة الكود، وقاعدة البيانات تمنع تكراره حتى مع طلبين متزامنين.

### 17.2 ربط الموظف بالفرع ككيان له قاعدة افتراضية

العضوية ليست مجرد جدول ربط؛ لها `IsDefault`، لذلك تم تمثيلها ككيان Domain مستقل.

الملف: `MechanicShop.Domain/Branches/EmployeeBranchMembership.cs`.

~~~csharp
public static Result<EmployeeBranchMembership> Create(Guid employeeId, Guid branchId, bool isDefault)
{
    if (employeeId == Guid.Empty)
    {
        return BranchErrors.EmployeeIdRequired;
    }

    if (branchId == Guid.Empty)
    {
        return BranchErrors.IdRequired;
    }

    return new EmployeeBranchMembership(employeeId, branchId, isDefault);
}

public Result<Updated> SetDefault(bool isDefault)
{
    IsDefault = isDefault;
    return Result.Updated;
}
~~~

الفحص الأول يمنع عضوية من غير Employee صالح، والثاني يمنع Branch غير محدد. بعد نجاح الفحص فقط يتم إنشاء العلاقة. `SetDefault` هي نقطة تعديل الخاصية بدل إتاحتها كـsetter عام. ضمان وجود default واحدة لا يعتمد على هذا الميثود وحده؛ المعاملة في Handler والـfiltered unique index في SQL يكملان الحماية.

### 17.3 إضافة BranchId وRowVersion إلى WorkOrder

كان WorkOrder محتاج تعريف فرعه، كما كان محتاج token يكتشف إن نسخة المستخدم قديمة. المقطع التالي يوضح الجزء المضاف من مدخل الإنشاء والتحقق داخل Domain.

الملف: `MechanicShop.Domain/Workorders/Workorder.cs`.

~~~csharp
public Guid BranchId { get; private set; }
public byte[] RowVersion { get; private set; } = [];

public static Result<WorkOrder> Create(
    Guid id,
    Guid branchId,
    Guid vehicleId,
    DateTimeOffset startAt,
    DateTimeOffset endAt,
    Guid? laborId,
    Spot spot,
    List<RepairTask> repairTasks)
{
    if (branchId == Guid.Empty)
    {
        return WorkOrderErrors.BranchIdRequired;
    }

    // بقية قواعد الإنشاء الموجودة في الملف تظل مطبقة هنا.
    return new WorkOrder(id, branchId, vehicleId, startAt, endAt, laborId, spot,
        WorkOrderState.Scheduled, repairTasks);
}
~~~

`BranchId` له setter خاص، فالتغيير يتم من Domain أو EF وليس من أي كود عميل. `RowVersion` يبدأ كمصفوفة فارغة لأن SQL Server هو الذي يولّد قيمته عند الإدخال والتعديل؛ لا يختارها المستخدم. `Create` يرفض `Guid.Empty` قبل تكوين الكيان. التعليق داخل المثال يختصر قواعد الإنشاء القديمة ولا يعني حذفها من الملف: فحص الهوية والمركبة والمهام والفني والتوقيت وSpot ما زال موجودًا في المصدر الفعلي.

### 17.4 تمرير سياق الفرع من غير ربط Application بـHTTP

Application تحتاج معرفة الفرع الحالي، لكنها لا ينبغي أن تعتمد على `HttpContext`. لذلك تعتمد على interface صغيرة، وتنفذ Infrastructure قراءتها من Claims.

الملفان: `MechanicShop.Application/Common/Interfaces/ICurrentBranchContext.cs` و`MechanicShop.Infrastructure/identity/CurrentBranchContext.cs`.

~~~csharp
public interface ICurrentBranchContext
{
    Guid? BranchId { get; }
    Guid? EmployeeId { get; }
    bool IsCompanyWide { get; }
}
~~~

الواجهة تعرّف أقل معلومات تحتاجها الطبقات: معرف الفرع، معرف الموظف، وهل النطاق على مستوى الشركة. كون القيم nullable يسمح بتمثيل طلب مجهول أو Claim غير صالحة بدل اختراع معرف افتراضي.

~~~csharp
var value = httpContextAccessor.HttpContext?.User.FindFirstValue("branch_id");
return Guid.TryParse(value, out var branchId) && branchId != Guid.Empty
    ? branchId
    : null;
~~~

الـimplementation يحاول قراءة Claim المسماة `branch_id`. `TryParse` يتعامل مع قيمة تالفة من غير رمي استثناء، ورفض `Guid.Empty` يمنع اعتبار المعرف الفارغ فرعًا حقيقيًا. عند غياب HTTP context أو Claim تكون النتيجة `null`، ثم تمنع سياسة الوصول الطلب الذي يحتاج فرعًا صالحًا.

### 17.5 Authorization: التحقق من Claims مقابل قاعدة البيانات

وجود Claim في JWT وحده غير كافٍ لو تغيرت صلاحية الموظف بعد إصدار التوكن. لذلك `ActiveBranchAccessHandler` يقارن البيانات الحساسة بحالة Employee وMembership الحالية.

الملف: `MechanicShop.Infrastructure/identity/policies/ActiveBranchAccessHandler.cs`.

~~~csharp
var employee = await context.Employees
    .Where(item => item.Id == employeeId)
    .Select(item => new { item.Role, item.AccessScope })
    .SingleOrDefaultAsync(cancellationToken);
if (employee is null)
{
    authorizationContext.Fail();
    return;
}
~~~

الاستعلام يحمّل الدور والنطاق فقط بدل تحميل Employee كاملًا. لو الحساب غير موجود، ينهي الـHandler بالفشل. `cancellationToken` مأخوذ من `RequestAborted` في الميثود الأصلية، عشان إلغاء طلب HTTP يوقف استعلام قاعدة البيانات.

~~~csharp
var isCompanyManager = employee.Role == Role.Manager && employee.AccessScope == AccessScope.Company;
var hasAccess = isCompanyManager
    ? await context.Branches.AnyAsync(
        branch => branch.Id == branchId && branch.IsActive, cancellationToken)
    : await context.EmployeeBranchMemberships
        .Where(membership => membership.EmployeeId == employeeId && membership.BranchId == branchId)
        .Join(context.Branches,
            membership => membership.BranchId,
            branch => branch.Id,
            (_, branch) => branch.IsActive)
        .AnyAsync(isActive => isActive, cancellationToken);
~~~

مدير `Company` يحتاج أن يكون الفرع موجودًا ونشطًا، لكنه لا يحتاج Membership فردية. أي مستخدم آخر يحتاج Membership للفرع، كما يجب أن يكون الفرع نشطًا. الفصل بين الحالتين يحافظ على قاعدة أن `Company` ليست صلاحية عامة لكل Role.

### 17.6 عزل WorkOrders وInvoices باستعلام EF تلقائي

Global Query Filter شرط يضيفه EF Core للاستعلامات على كيان معين. أُضيف لتقليل احتمال نسيان قيد الفرع في Query.

الملف: `MechanicShop.Infrastructure/Date/AppDbContext.cs`.

~~~csharp
builder.Entity<WorkOrder>()
    .HasQueryFilter(workOrder => IsCompanyWide
        || (CurrentBranchId.HasValue
            && workOrder.BranchId == CurrentBranchId.Value));
builder.Entity<Invoice>()
    .HasQueryFilter(invoice => IsCompanyWide
        || (CurrentBranchId.HasValue
            && invoice.WorkOrder != null
            && invoice.WorkOrder.BranchId == CurrentBranchId.Value));
~~~

فلتر WorkOrder يسمح بقراءة الشركة الواسعة أو السجلات التي BranchId فيها يساوي الفرع الحالي. فلتر Invoice يتبع العلاقة إلى `WorkOrder` لأن Invoice لا يملك BranchId مستقلًا. شرط `CurrentBranchId.HasValue` يمنع طلبًا بلا فرع من رؤية أوامر الفروع.

الفلتر طبقة دفاع للبيانات وليس بديلًا عن Authorization: السياسة تتأكد من هوية المستخدم وعضويته، بينما الفلتر يقيد استعلام EF نفسه.

### 17.7 تعيين الفرع الافتراضي داخل Transaction

نقل default يحتاج إلغاء القيمة الحالية ثم تعيين الجديدة. العمليتان لازم ينجحا معًا أو يتم التراجع عنهما معًا.

الملف: `MechanicShop.Application/Features/Branches/Commands/SetDefaultBranch/SetDefaultBranchCommandHandler.cs`.

~~~csharp
await using var transaction = await context.BeginTransactionAsync(cancellationToken);
try
{
    var memberships = await context.EmployeeBranchMemberships
        .Where(membership => membership.EmployeeId == employeeId)
        .ToListAsync(cancellationToken);
    foreach (var membership in memberships)
    {
        membership.SetDefault(false);
    }

    await context.SaveChangesAsync(cancellationToken);
    memberships.Single(membership => membership.BranchId == command.BranchId).SetDefault(true);
    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Result.Updated;
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
~~~

أولًا يبدأ `Transaction`. بعدها يحمل عضويات الموظف ويزيل علامة default عنها، ثم يحفظ حتى لا يتعارض مع الـfiltered unique index أثناء الخطوة التالية. بعدها يحدد عضوية الفرع المطلوب ويحفظ. `CommitAsync` يثبت العمليتين. أي خطأ يسبب `RollbackAsync` ثم إعادة رمي الخطأ؛ لا نخفي الخطأ ولا نترك نصف التغيير محفوظًا.

### 17.8 حفظ التعديل مع RowVersion وإرجاع Conflict

عند تعديل WorkOrder، EF يحتاج القيمة التي قرأها العميل كـOriginalValue ليضعها في شرط UPDATE. المقطع الفعلي في Handler يربط القيمة ثم يحول تعارض SQL إلى خطأ Domain معروف.

الملف: `MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommandHandler.cs`.

~~~csharp
_context.WorkOrders.Entry(workOrder).Property(order => order.RowVersion).OriginalValue = command.RowVersion!;
try
{
    await _context.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException)
{
    return WorkOrderErrors.ConcurrencyConflict;
}
~~~

السطر الأول لا يغيّر RowVersion إلى قيمة يحددها العميل؛ هو يخبر EF بالقيمة التي توقعها العميل عند القراءة. SQL Server يحدّث الصف فقط إذا كانت القيمة ما زالت مطابقة. إذا تغيرت، EF يرمي `DbUpdateConcurrencyException` ويرجع Handler خطأ Conflict بدل الكتابة فوق التعديل الأحدث. `!` يفترض أن Validator رفض القيمة المفقودة قبل تنفيذ الـHandler.

### 17.9 تسجيل Branches endpoints وتمرير الطلب إلى Application

Controller يعرّف HTTP contract ويحوّل request إلى Command أو Query. قواعد الوصول النهائية تبقى في Policy والـHandler.

الملف: `MechanicShop.Api/Controllers/BranchesController.cs`.

~~~csharp
[Route("api/v{version:apiVersion}/branches")]
[ApiVersion("1.0")]
[Authorize]
[Authorize(Policy = "ActiveBranchAccess")]
public sealed class BranchesController(ISender sender) : ApiController
~~~

`Route` يحدد المسار مع رقم النسخة، و`ApiVersion` يعلن النسخة المدعومة. `Authorize` يطلب مستخدمًا مصادقًا، و`ActiveBranchAccess` يضيف فحص الفرع النشط والعضوية. `ISender` يرسل حالات الاستخدام إلى MediatR بدل وضع منطق قاعدة البيانات في Controller.

~~~csharp
var result = await sender.Send(
    new SetDefaultBranchCommand(request.BranchId),
    cancellationToken);
return result.Match(_ => NoContent(), Problem);
~~~

الـController ينشئ Command من Contract، ويمرر `CancellationToken` حتى يتوقف العمل عند إلغاء الاتصال. `Match` يحول النجاح إلى HTTP 204، والفشل إلى Problem Details عبر `Problem`.

### 17.10 ضبط FK وRowVersion والفهارس في EF Core

Configuration يترجم نموذج Domain إلى قيود قاعدة البيانات. وجود القاعدة في SQL يحمي البيانات حتى لو لم تمر الكتابة من نفس مسار الـApplication.

الملف: `MechanicShop.Infrastructure/Date/Configurations/WorkOrderConfiguration.cs`.

~~~csharp
builder.Property(w => w.BranchId).IsRequired();
builder.HasOne<MechanicShop.Domain.Branches.Branch>()
    .WithMany()
    .HasForeignKey(w => w.BranchId)
    .OnDelete(DeleteBehavior.Restrict);
builder.Property(w => w.RowVersion).IsRowVersion();
builder.HasIndex(w => new { w.BranchId, w.StartAtUtc, w.EndAtUtc });
~~~

`IsRequired` يجعل BranchId مطلوبًا في الجدول. الـForeign Key يمنع ربط WorkOrder بفرع غير موجود، و`Restrict` يمنع حذف Branch ما دامت هناك WorkOrders تابعة له. `IsRowVersion` يطلب من EF إعداد concurrency token المناسب لـSQL Server. الفهرس المركب يبدأ بـBranchId ثم أوقات الموعد لدعم استعلامات الفرع والفترة الزمنية؛ وهو لا يفرض وحده عدم تداخل المواعيد.

### 17.11 تهيئة قاعدة البيانات: Migrations بدل EnsureCreated

التطبيق يستخدم Migrations كسجل لتطور الـschema. الـReset اختياري ومسموح فقط في بيئات التطوير والاختبار، وبعدها تطبق التهيئة كل الـMigrations.

الملف: `MechanicShop.Infrastructure/Date/ApplicationDbContextInitialiser.cs`.

~~~csharp
if (_configuration.GetValue<bool>("Database:ResetOnStartup"))
{
    if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("إعادة إنشاء قاعدة البيانات مسموحة في Development وTesting فقط.");

    await _context.Database.EnsureDeletedAsync();
}

await _context.Database.MigrateAsync();
~~~

الإعداد الصريح هو الذي يطلب الحذف؛ لا يحدث reset تلقائيًا لمجرد تشغيل التطبيق. فحص البيئة يأتي قبل الحذف، ثم `MigrateAsync` ينشئ أو يحدّث الـschema حسب الـMigrations. يوجد في الملف الفعلي أيضًا فحص لقواعد SQL النظامية قبل السماح بالـreset. هذا المسار لا يجيز حذف Production.

### 17.12 اختبار يحمي عزل الفرع في cache

اختبار `CachingBehaviorTests` يقارن نتائج الطلب نفسه تحت سياق فرعين. الاختلاف المتوقع في المفتاح يمنع إعادة نتيجة فرع لمستخدم فرع آخر.

الملف: `tests/MechanicShop.Application.UniTests/Behaviours/CachingBehaviorTests.cs`.

~~~csharp
await behavior.Handle(
    new CachedQuery(),
    _ => Task.FromResult((Result<string>)"branch-data"),
    CancellationToken.None);

Assert.Equal($"branch:{branchId:N}:test-key", actualKey);
~~~

الاختبار يشغل `CachingBehavior` مع `ICurrentBranchContext` مضبوط على فرع محدد، ثم يتأكد أن المفتاح يبدأ بـ`branch:` ويتضمن GUID الفرع قبل مفتاح الطلب `test-key`. لو تغير سياق الفرع، يتغير المفتاح وبالتالي لا يعيد Cache بيانات الفرع الأول. الاختبار لا يثبت صحة كل Global Query Filter ولا يحل محل اختبارات SQL؛ هو يغطي تحديدًا تقسيم Cache حسب الفرع.

### 17.13 اختبار تهيئة قاعدة البيانات والـSeed

اختبار `DatabaseInitializationTests` يشغل التهيئة أكثر من مرة ثم يفحص invariants المهمة بدل الاكتفاء بأن الميثود لم ترمِ استثناء.

الملف: `tests/MechanicShop.Api.IntegrationTests/Infrastructure/DatabaseInitializationTests.cs`.

~~~csharp
await initializer.SeedAsync();
await initializer.SeedAsync();

var branches = await context.Branches.OrderBy(branch => branch.Code).ToListAsync(cancellationToken);
Assert.Equal(2, branches.Count);
Assert.Equal(TestBranchIds.Main, branches.Single(branch => branch.Code == "MAIN").Id);
Assert.Equal(TestBranchIds.North, branches.Single(branch => branch.Code == "NORTH").Id);
~~~

استدعاء `SeedAsync` مرتين يختبر قابلية تكرار الـSeed. العدد المتوقع فرعان، ثم يفحص الاختبار أن كود MAIN وNORTH مرتبطان بالـGUID الثابت الصحيح. بقية الاختبار الفعلي تتحقق أن لكل Employee عضوية default واحدة، وأن عضوياته لفروع نشطة، وأن WorkOrders الأربعة تشير إلى فرع وفني عضوين فيه. الاختبار يحذف WorkOrders قبل الفحص ويعيد تنظيفها في `finally` لعزل قاعدة الاختبار.

### 17.14 أثر التغيير على ملفات الاختبارات

في اختبارات WorkOrders، أضيفت فروع إلى بيانات الاختبار وقيم RowVersion للأوامر التي تتطلب concurrency token. التفسير العملي لكل تعديل هو الحفاظ على Arrange صالح بعد جعل BranchId إلزاميًا، ثم تمرير النسخة المتوقعة قبل تنفيذ Handler.

الملفات المعدلة تشمل `WorkOrderFactory.cs` واختبارات `AssignLaborCommandHandlerTests` و`CreateWorkOrderCommandHandlerTests` و`DeleteWorkOrderCommandHandlerTests` و`RelocateWorkOrderCommandHandlerTests` و`UpdateWorkOrderStateCommandHandlerTests` و`UpdateWorkOrderRepairTasksCommandHandlerTests` و`GetWorkOrdersQueryHandlerTests`، بالإضافة إلى اختبارات API وIdentity وCache وDomain المذكورة في القسم 13.

كل تعديل اختبار له غرض سلوكي: تجهيز Branch صالح، أو اختبار رفض الفني غير المنتمي، أو تمرير RowVersion، أو التأكد من عدم نشر side effects عند Conflict، أو التحقق من claim/access scope. تفاصيل كل حالة واسمها موجودة تحت قائمة الملف في القسم 13؛ لم تُحذف الاختبارات الموجودة لتجاوز حالات فشل.

**ملاحظة عن الشمول:** المقاطع أعلاه تشرح أسطر الكود التي تحمل القواعد والتدفق الجديد. ملفات DTOs وCommands وValidators وSeed IDs والـMigration وباقي الاختبارات مذكورة بالمسارات والأدوار في الأقسام السابقة، لكن لا يتم نسخ كل ملف كاملًا حرفيًا داخل هذا الدليل؛ ذلك كان سيكرر ملفات المصدر بدل شرحها. عند توسيع هذا الدليل لاحقًا، الأفضل إضافة مقتطف مستقل لكل ملف جديد بنفس النمط: سبب التعديل، الكود، ثم تفسيره مباشرة.

## 18. كود Production المضاف والمعدل فقط

القسم ده خاص بكود التشغيل داخل `Contracts` و`MechanicShop.Domain` و`MechanicShop.Application` و`MechanicShop.Infrastructure` و`MechanicShop.Api`. ملفات `tests` مستبعدة منه عمدًا. أسماء الملفات المذكورة في القسم 13 هي فهرس التغييرات؛ المقاطع هنا تعرض الكود التنفيذي المحوري وتشرح معناه تحته.

### 18.1 تعريف AccessScope وقاعدة Employee

اتضاف `AccessScope` عشان الدور الوظيفي وحده ماكانش كفاية للتمييز بين مدير فرع ومدير الشركة.

الملف: `MechanicShop.Domain/Identity/AccessScope.cs`.

~~~csharp
public enum AccessScope
{
    Branch,
    Company
}
~~~

`Branch` يعني إن الحساب يعمل ضمن الفروع المسموح بها في Membership، و`Company` يعني نطاقًا أوسع مخصصًا لمدير `Manager`. القيمة الافتراضية التي يختارها `Employee.Create` هي `Branch`، فلا يحصل الموظف الجديد على نطاق شركة ضمنيًا.

الملف: `MechanicShop.Domain/Employees/Employee.cs`.

~~~csharp
if (!Enum.IsDefined(accessScope) || (accessScope == AccessScope.Company && role != Role.Manager))
{
    return EmployeeErrors.AccessScopeInvalid;
}
~~~

الفحص يرفض قيمة enum غير معروفة، ويرفض `Company` إذا لم يكن Role هو `Manager`. القاعدة موجودة في Domain عشان ما تعتمدش على الـAPI أو الواجهة، وتُطبق سواء استُخدم `Create` أو `SetAccessScope`.

### 18.2 أوامر إدارة الفروع

كل Command يمثل طلب استخدام صغيرًا، والـHandler هو الذي يطبق الصلاحية ويتعامل مع قاعدة البيانات. مثال إنشاء Branch:

الملفات: `CreateBranchCommand.cs` و`CreateBranchCommandHandler.cs` و`CreateBranchCommandValidator.cs` تحت `MechanicShop.Application/Features/Branches/Commands/CreateBranch`.

~~~csharp
if (!currentBranchContext.IsCompanyWide)
{
    return Error.Forbidden("Branch.CompanyScope.Required", "إنشاء الفروع متاح لمدير الشركة فقط.");
}

var branchResult = Branch.Create(Guid.NewGuid(), command.Name, command.Code);
if (branchResult.IsError)
{
    return branchResult.Errors;
}
~~~

الـHandler يوقف العملية قبل الكتابة لو النطاق ليس Company. عند السماح، يفوض إنشاء الكيان إلى `Branch.Create` كي تبقى قواعد الهوية والاسم والكود في Domain. إذا رجع Domain أخطاء، يعيدها Handler بدل حفظ كيان غير صالح.

~~~csharp
RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
RuleFor(command => command.Code).NotEmpty().MaximumLength(20);
~~~

الـValidator يفحص شكل المدخل وحدوده قبل تنفيذ الـHandler. هذا لا يغني عن `Branch.Create`: الـValidator يتحقق من الطلب، وDomain يحمي invariants الكيان، وقاعدة البيانات تفرض التفرد.

إضافة موظف إلى فرع تمر بتسلسل مشابه:

الملف: `AddEmployeeToBranchCommandHandler.cs`.

~~~csharp
if (await context.EmployeeBranchMemberships.AnyAsync(
        membership => membership.EmployeeId == command.EmployeeId
            && membership.BranchId == command.BranchId,
        cancellationToken))
{
    return Error.Conflict("Branch.Membership.Duplicate", "الموظف عضو بالفعل في الفرع.");
}

var isDefault = !await context.EmployeeBranchMemberships.AnyAsync(
    membership => membership.EmployeeId == command.EmployeeId && membership.IsDefault,
    cancellationToken);
var membershipResult = EmployeeBranchMembership.Create(command.EmployeeId, command.BranchId, isDefault);
~~~

الاستعلام الأول يرفض تكرار العلاقة نفسها. الاستعلام الثاني يجعل أول عضوية للموظف default، والعضويات اللاحقة غير افتراضية. بعدها يمر الإنشاء عبر Domain factory للتحقق من المعرفين، ثم يحفظ الـHandler العضوية.

`SetBranchActiveCommandHandler` يمنع تعطيل فرع ما زال default لموظف، و`SetDefaultBranchCommandHandler` ينقل default داخل Transaction، و`SetEmployeeAccessScopeCommandHandler` يقتصر على Company scope ويستدعي `Employee.SetAccessScope`. لكل واحد منهم Command وValidator حيث يلزم؛ التفاصيل ومسارات الملفات مسجلة في القسمين 5 و13.

### 18.3 Queries وDTOs للفروع

الـQueries تعرض بيانات DTO بدل إرجاع EF entities مباشرةً. في Query قائمة الفروع:

الملف: `GetBranchesQueryHandler.cs`.

~~~csharp
var query = context.Branches.AsNoTracking();
if (!currentBranchContext.IsCompanyWide)
{
    if (!currentBranchContext.EmployeeId.HasValue)
    {
        return Error.Forbidden("Branch.Employee.Invalid", "حساب المستخدم غير مرتبط بموظف صالح.");
    }

    query = query.Where(branch => context.EmployeeBranchMemberships.Any(membership =>
        membership.EmployeeId == currentBranchContext.EmployeeId.Value
        && membership.BranchId == branch.Id));
}

var branches = await query.OrderBy(branch => branch.Code)
    .Select(branch => new BranchDto(branch.Id, branch.Name, branch.Code, branch.IsActive))
    .ToListAsync(cancellationToken);
~~~

`AsNoTracking` مناسب للقراءة لأنه لا يطلب من EF تتبع الكيانات للتعديل. مدير الشركة يحتفظ بالقائمة الأوسع، أما المستخدم الآخر فيضاف له شرط Membership. `OrderBy` يجعل الترتيب ثابتًا حسب Code، و`Select` يحول الحقول المطلوبة إلى `BranchDto` قبل جلب النتيجة.

`GetBranchByIdQueryHandler` يطبق شرط العضوية على فرع واحد. `GetBranchEmployeesQueryHandler` يربط Memberships بالموظفين ويعيد `BranchEmployeeDto` مرتبًا بالاسم؛ بذلك لا يخرج إلى العميل إلا العرض المقصود.

### 18.4 إسناد الفني إلى WorkOrder من نفس الفرع

اتضاف الفحص إلى Handler حتى لا يكفي وجود الفني في النظام؛ لازم يكون عضوًا في فرع WorkOrder.

الملف: `MechanicShop.Application/Features/WorkOrders/common/AssignLabor/AssignLaborCommandHandler.cs`.

~~~csharp
if (!await _context.EmployeeBranchMemberships.AnyAsync(
        membership => membership.EmployeeId == command.LaborId && membership.BranchId == workOrder.BranchId,
        ct))
{
    return Error.Conflict("Labor.Branch.Mismatch", "الفني مش تابع لفرع أمر الشغل.");
}
~~~

الفحص يحصل بعد العثور على WorkOrder والفني، وقبل تعديل الكيان. `AnyAsync` يسأل قاعدة البيانات عن وجود العضوية من غير تحميل قائمة كاملة. لو لم توجد العلاقة، يتوقف التعديل ولا يُسند الفني.

نفس invariant مطبق عند إنشاء WorkOrder في `CreateWorkOrderCommandHandler`: الفرع النشط يؤخذ من `ICurrentBranchContext`، والفني الاختياري يجب أن يكون عضوًا فيه. بذلك لا يستطيع العميل اختيار BranchId مختلف داخل request لإنشاء أمر خارج سياقه.

### 18.5 RowVersion في Contract وCommand وValidator

قيمة RowVersion تنتقل من نتيجة القراءة إلى طلب التعديل ثم إلى Handler.

الملفات: `Contracts/Requests/WorkOrders/AssignLaborRequest.cs`، `AssignLaborCommand.cs`، و`AssignLaborCommandValidator.cs`.

~~~csharp
public byte[]? RowVersion { get; set; }
~~~

الـContract يستقبل قيمة SQL Server الثنائية التي أعادها WorkOrder. جعلها nullable يسمح للـValidator بإرجاع خطأ تحقق واضح عند غيابها بدل افتراض وجودها.

~~~csharp
public sealed record AssignLaborCommand(Guid WorkOrderId, Guid LaborId, byte[]? RowVersion = null)
    : IRequest<Result<Updated>>;
~~~

الـCommand يحمل RowVersion من طبقة HTTP إلى Application. القيمة الافتراضية `null` تحافظ على إمكان إنشاء الأمر برمجيًا، لكن الـValidator يرفضها في المسار التنفيذي الذي يحتاج concurrency check.

~~~csharp
RuleFor(x => x.RowVersion)
    .NotNull()
    .Must(rowVersion => rowVersion is { Length: 8 })
    .WithErrorCode("WorkOrderRowVersion_Required")
    .WithMessage("A valid WorkOrder RowVersion is required.");
~~~

القاعدة ترفض القيمة الغائبة أو ذات طول غير مناسب قبل قاعدة البيانات. SQL Server RowVersion طوله 8 bytes. القاعدة نفسها موجودة في Validators لعمليات تعديل WorkOrder التي تحمل token، بينما حذف WorkOrder يستقبل القيمة عبر If-Match كما هو موضح في قسم API.

### 18.6 تحديث DTO وMapper لاسترجاع RowVersion

لو لم ترجع القراءة RowVersion، لن يملك العميل القيمة اللازمة للتعديل المتفائل.

الملفات: `WorkOrderDto.cs` و`WorkOrderListItemDto.cs` و`WorkOrderMapper.cs`.

~~~csharp
RowVersion = workOrder.RowVersion
~~~

الـMapper ينسخ الرمز الذي ولّده SQL من Domain entity إلى DTO. يتم إرجاعه ضمن تفاصيل WorkOrder وقائمة النتائج، ثم يعيد العميل إرساله في Command التالي. لا ينشئ Mapper الرمز ولا يغيره.

### 18.7 Claims وإعادة فحص صلاحيات Identity

`AppUserDto` و`GenerateTokenQuery` يحملان الفرع والنطاق المختارين إلى `TokenProvider`. عند إنشاء JWT أضيفت Claims:

الملف: `MechanicShop.Infrastructure/identity/TokenProvider.cs`.

~~~csharp
new ("branch_id", user.BranchId.ToString()),
new ("access_scope", user.AccessScope.ToString()),
~~~

`branch_id` يحدد الفرع النشط في الجلسة، و`access_scope` يحدد Branch أو Company. الـClaims الموقعة تسهل معرفة السياق في الطلب التالي، لكن `ActiveBranchAccessHandler` يراجع الدور والنطاق والعضوية من قاعدة البيانات كي لا تظل صلاحية قديمة صالحة بعد تغييرها.

في `RefreshTokenQueryHandler` يعاد تحميل بيانات المستخدم الحالية قبل إصدار التوكن الجديد. وفي `IdentityService` يتم التحقق أن Branch المختار نشط ومسموح به؛ حساب Manager/Company له قاعدة اختيار أوسع، وباقي الحسابات مقيدة بالعضويات.

### 18.8 CachingBehavior ومفتاح Cache المعزول

تغيير Cache key هو تغيير Production مقصود لحماية نتائج Query من المشاركة بين الفروع.

الملف: `MechanicShop.Application/Common/Behaviours/CachingBehavior.cs`.

~~~csharp
private string BuildCacheKey(string key)
{
    if (_currentBranchContext is null)
    {
        return key;
    }

    var scope = _currentBranchContext.IsCompanyWide ? "company" : "branch";
    var branchId = _currentBranchContext.BranchId?.ToString("N") ?? "none";
    return $"{scope}:{branchId}:{key}";
}
~~~

عند غياب سياق الفرع، يحتفظ السلوك بالمفتاح القديم لتوافق استخدامات لا تسجل هذه الخدمة. عند وجود السياق، يضيف نوع النطاق ومعرف الفرع إلى المفتاح. تنسيق `N` يزيل فواصل GUID لتكوين مفتاح ثابت. استخدام كلمة `company` مع معرف الفرع الموجود يمنع التداخل بين نطاقات مختلفة.

### 18.9 EF Configurations والقيود في قاعدة البيانات

`BranchConfiguration` يفرض طول الاسم والكود وفريدة Code. `EmployeeBranchMembershipConfiguration` يفرض مفتاحًا مركبًا وعلاقة الموظف والفرع، ويمنع أكثر من default واحدة لكل موظف عبر filtered unique index:

الملف: `EmployeeBranchMembershipConfiguration.cs`.

~~~csharp
builder.HasKey(membership => new { membership.EmployeeId, membership.BranchId });
builder.HasIndex(membership => membership.EmployeeId)
    .IsUnique()
    .HasFilter("[IsDefault] = 1");
~~~

المفتاح المركب يمنع تكرار Membership لنفس Employee والفرع. الفهرس الفريد المشروط يطبق التفرد على الصفوف التي IsDefault فيها true فقط، فيسمح للموظف بعضويات متعددة غير افتراضية مع default واحدة كحد أقصى.

`WorkOrderConfiguration` يضيف Branch FK وRowVersion وفهرس الفرع مع أوقات الموعد. `EmployeeConfiguration` يخزن Role وAccessScope كنصوص. هذه القيود تتكامل مع الفحوص داخل Domain وApplication، ولا تستبدلها.

### 18.10 Migration وSeed Data

الـMigration تنشئ Branches وEmployeeBranchMemberships وتضيف BranchId وRowVersion. قبل إضافة BranchId المطلوب، ترفض وجود WorkOrders قائمة؛ لا تخمن الفرع التاريخي.

الملف: `MechanicShop.Infrastructure/Migrations/20261103163357_AddBranchesAndWorkOrderRowVersion.cs`.

~~~csharp
migrationBuilder.Sql(
    "IF EXISTS (SELECT 1 FROM [WorkOrders]) THROW 51000, N'يجب إخلاء أوامر الشغل التجريبية بإعادة ضبط صريحة في Development أو Testing قبل تفعيل ملكية الفروع.', 1;");

migrationBuilder.AddColumn<Guid>(
    name: "BranchId",
    table: "WorkOrders",
    type: "uniqueidentifier",
    nullable: false);

migrationBuilder.AddColumn<byte[]>(
    name: "RowVersion",
    table: "WorkOrders",
    type: "rowversion",
    rowVersion: true,
    nullable: false);
~~~

أول أمر SQL يوقف الترحيل عند وجود بيانات WorkOrders؛ ده يتجنب تخمين mapping غير معتمد. بعد اجتياز الفحص، يضاف BranchId مطلوب وRowVersion يولده SQL Server. خطة Development/Test تعتمد على reset الصريح المقيّد بالبيئة ثم إعادة Seed، بينما Production لا يُحذف تلقائيًا.

الملف: `MechanicShop.Infrastructure/Date/SeedDataIds.cs`.

~~~csharp
public static readonly Guid MainBranch = Guid.Parse("2b01c8f5-04c1-4fb7-a88a-92fd0204eae1");
public static readonly Guid NorthBranch = Guid.Parse("f4b97e1c-4922-4d22-9688-f60a28c89d0d");
~~~

المعرفات ثابتة بين مرات التشغيل، فتستطيع الاختبارات والـSeed ربط البيانات بنفس الفروع كل مرة. `ApplicationDbContextInitialiser` ينفذ Migrations ثم يضيف الفروع والعضويات وأوامر الشغل بعلاقات متسقة؛ لا ينشئ GUIDs عشوائية للفروع عند كل تشغيل.

### 18.11 API وHTTP status الخاص بالتعارض

`BranchesController` يعرّف Routes ويرسل Commands وQueries عبر `ISender`. `WorkOrderController` يقرأ If-Match للحذف ويمرر RowVersion في عمليات التعديل. عند Conflict يحول `ProblemExtensions` الخطأ إلى HTTP 409.

التدفق هنا يحافظ على الفصل: Controller لا يقرر Membership ولا يقارن RowVersion بنفسه؛ ينقل البيانات، ثم Policy وHandler وEF يطبقون القواعد. ملفات `DashboardController` و`InvoicesController` و`LaborsController` و`WorkOrderController` و`ApiController` المعدلة مذكورة في فهرس Production بالقسم 13، وتشمل تمرير سياق الإلغاء ومعالجة أخطاء النتائج حيث ينطبق.

### 18.12 فهرس كامل لملفات Production التي تغيرت

القائمة دي تفصل ملفات Production عن الاختبارات، عشان تقدر تفتح كل تغيير من مكانه:

| الطبقة | نطاق الملفات المعدلة أو المضافة |
|---|---|
| Contracts | `Contracts/Requests/Branches/*` وطلبات WorkOrders الأربعة التي أضيف إليها `RowVersion` |
| Domain | `Branches/Branch.cs` و`EmployeeBranchMembership.cs` و`Identity/AccessScope.cs` وتعديلات `Employee` و`WorkOrder` وأخطائهما |
| Application | `ICurrentBranchContext`؛ Feature `Branches` كاملة؛ تغييرات WorkOrders وIdentity وLabors وDTOs وMapper وCachingBehavior وواجهات DbContext وIdentity وأخطاء Application |
| Infrastructure | `AppDbContext` و`ApplicationDbContextInitialiser` وConfigurations و`SeedDataIds` وMigration وIdentityService وTokenProvider و`CurrentBranchContext` وسياسة `ActiveBranchAccess` وDependencyInjection |
| API | `BranchesController` وتعديلات `ApiController` و`DashboardController` و`InvoicesController` و`LaborsController` و`WorkOrderController` و`ProblemExtensions` وDI وإعداد Development |

القسم 13 يذكر المسارات الفردية للملفات المتغيرة، والأقسام 17 و18 تشرح مقاطع التنفيذ الجديدة. لا يحتوي هذا الدليل على نسخة كاملة من كل ملف؛ المرجع الكامل للكود هو الملف المشار إليه في كل عنوان، والمقاطع هنا تنقل أسطرًا محورية مع شرحها مباشرةً.
