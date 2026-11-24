# 7. خدمات البنية التحتية

## SQL Server

تستخدم الصورة SQL Server 2022 على Linux. يجب قبول الترخيص بوضوح عبر `MSSQL_ACCEPT_EULA=Y`، وتعيين `MSSQL_SA_PASSWORD` وفق متطلبات SQL. health check ينفذ استعلام `SELECT 1` باستخدام `sqlcmd` المرفق بالصورة. `MSSQL_PID=Developer` للتطوير فقط. في بيئة حقيقية اختر الإصدار والترخيص المناسبين.

## Redis

يستخدم التطبيق Redis كـcache/health check عبر اسم الخدمة `redis`. تم تشغيل AOF وحفظ نقطة RDB دورية، وتُخزن الملفات في volume. هذا لا يجعل Redis بديلًا لقاعدة البيانات ولا يلغي النسخ الاحتياطية.

## Seq

Seq يجمع سجلات Serilog من API وtraces التي يمررها Collector. يجب مراجعة شروط Seq وقبولها صراحةً. عيّن مستخدم وكلمة مرور إدارية؛ إعداد تعطيل المصادقة غير مستخدم. واجهة Seq داخل الشبكة على 80 والاستقبال على 5341.

## OpenTelemetry Collector وPrometheus

يستقبل Collector بروتوكول OTLP على gRPC 4317 أو HTTP 4318. يرسل traces إلى Seq على `http://seq/ingest/otlp` ويعرض المقاييس بصيغة Prometheus على 9464. Prometheus يسحب المقاييس كل 15 ثانية، ويحتفظ بها 15 يومًا افتراضيًا.

## Grafana

Grafana يجهّز Prometheus تلقائيًا كمصدر افتراضي ولوحة `MechanicShop overview`. الحساب الإداري يحتاج كلمة مرور محلية. التهيئة التلقائية لا تغني عن ضبط المصادقة وTLS والتخزين والنسخ الاحتياطية في الإنتاج.
