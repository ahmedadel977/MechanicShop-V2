# 2. شرح Dockerfile

ملف التطبيق: `src/MechanicShop.Api/Dockerfile`. أمر Compose يمرر جذر الحل كسياق (`context: ..` من مجلد `docker-compose`)، لذلك أوامر `COPY` التالية تبدأ من الجذر لا من مجلد API.

| التعليمة | استخدامها هنا |
| --- | --- |
| `FROM` | يبدأ مرحلة runtime على ASP.NET 10.0.12 أو مرحلة build على SDK 10.0.401. |
| `WORKDIR` | يحدد مجلد العمل داخل الصورة مثل `/src` و`/app`. |
| `COPY` | ينسخ إعدادات NuGet وملفات المشاريع أولًا ثم بقية المصدر. |
| `RUN` | يثبت `curl` لفحص الصحة، ثم ينفذ `dotnet restore` و`dotnet publish`. |
| `EXPOSE` | يوثق أن API تستمع على 8080؛ ربط المضيف يُحدد في Compose. |
| `ENV` | يضبط منفذ HTTP إلى 8080. |
| `USER` | يشغّل الخدمة بحساب `$APP_UID` غير الجذري. |
| `ENTRYPOINT` | يبدأ التطبيق عبر `dotnet MechanicShop.Api.dll`. |

لا توجد تعليمات Docker اسمها `RESTORE` أو `BUILD` أو `PUBLISH` أو `CMD`. عمليات .NET هذه أوامر `dotnet` داخل `RUN`، و`ENTRYPOINT` هو طريقة بدء الحاوية. ملف المشروع يضبط `DockerDefaultTargetOS=Linux` و`DockerfileContext` إلى جذر الحل حتى يتوافق Visual Studio مع ملفات الحل.

## طبقة الاستعادة

نسخ ملفات `.csproj` وملفات إدارة البناء المركزية قبل المصدر يسمح بإعادة استخدام طبقة NuGet عند تغيير كود التطبيق فقط. إضافة أو تعديل package أو ملف مشروع يعيد تشغيل restore.

## أوامر محلية

```powershell
docker build -f src/MechanicShop.Api/Dockerfile -t mechanicshop-api:local .
docker run --rm -p 5106:8080 mechanicshop-api:local
```

الأمر الثاني وحده لا يكفي لتشغيل التطبيق؛ يجب توفير SQL وRedis وSeq وإعدادات السر كما يفعل Compose.
