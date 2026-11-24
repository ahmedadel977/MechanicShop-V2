# 11. استكشاف الأخطاء

| العَرَض | الفحص والخطوة التالية |
| --- | --- |
| `permission denied ... docker API` أو named pipe | شغّل Docker Desktop، تأكد من Linux containers، ثم أعد `docker info`. |
| `Access is denied` على Buildx lock | أغلق عمليات بناء Docker الأخرى، تحقق من صلاحية مجلد Docker/Buildx، وأعد تشغيل Docker Desktop إذا لزم. |
| Compose يطلب متغيرًا مطلوبًا | انسخ `docker-compose/.env.example` إلى `docker-compose/.env`، املأ السر، وراجع قبول التراخيص. لا تترك الحقول المطلوبة فارغة. |
| تعارض منفذ | غيّر متغير المنفذ المقابل في `.env` ثم أعد `docker compose up -d`. |
| API لا تتصل بـSQL أو Redis | استخدم `sqlserver` و`redis` كأسماء مضيفين داخل Compose، وتحقق من `docker compose ps` وفحوص الصحة. |
| SQL يخرج مباشرة | تحقق من قوة كلمة مرور SA، وذاكرة Docker، وسجلات `docker compose logs sqlserver`. |
| API `/health` غير سليمة | تحقق من حالتي SQL وRedis، ثم سجلات API. فحص الصحة الحالي لا يعني أن migrations طُبقت في Production. |
| Seq لا يستقبل traces | تحقق من Collector على 4317/4318، عنوان `http://otel-collector:4317`، ومسار `http://seq/ingest/otlp`. |
| لا تظهر مقاييس في Prometheus | افتح Targets، تحقق من هدف `otel-collector:9464` وسجلات Collector وinstrumentation. |
| Grafana بلا بيانات | تحقق من أن datasource عنوانه `http://prometheus:9090`، وأن هدف Prometheus في حالة UP. |
| إعادة تشغيل أفقدت البيانات | افحص volumes. تجنب `down --volumes`، وتذكر أن Docker volume ليس نسخة احتياطية. |

ابدأ بتشخيص أقل نطاقًا:

```powershell
docker compose ps
docker compose logs --tail=100 api sqlserver redis otel-collector prometheus grafana seq
docker compose config --quiet
```

لا تنسخ سجلات تحتوي connection strings أو tokens عند طلب المساعدة؛ احذف القيم السرية أولًا.
