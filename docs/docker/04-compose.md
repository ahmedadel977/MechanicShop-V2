# 4. Compose وبيئات التشغيل

Compose يعرّف كل خدمة كحاوية مستقلة. `depends_on` ينتظر صحة SQL وRedis قبل بدء API، بينما Seq وCollector يعتمدان على ترتيب بدء الحاوية فقط. `restart: unless-stopped` يعيد تشغيل الخدمة بعد الانهيار أو إعادة تشغيل المحرك.

## الملف الأساسي وملف override

- `docker-compose/docker-compose.yml`: الخدمات والشبكة والـvolumes والاتصالات الداخلية وربط API بمنفذ قابل للضبط.
- `docker-compose/docker-compose.override.yml`: منافذ التطوير على المضيف ويضبط `ASPNETCORE_ENVIRONMENT=Development`.
- `docker-compose/docker-compose.dcproj`: مشروع Visual Studio الذي يشغّل Compose ويحمّل `docker-compose/.env`.

في التطوير يُستخدم المنفذ 5106 للـAPI، و1433 لـSQL، و6379 لـRedis، و5341 لـSeq، و9090 لـPrometheus، و3000 لـGrafana. غيّرها من `docker-compose/.env` عند الحاجة. لا تُفتح منافذ SQL وRedis على المضيف في الملف الأساسي.

## إعدادات Compose

`build.context: ..` يجعل جذر الحل متاحًا من مجلد Compose. `dockerfile` يشير إلى `src/MechanicShop.Api/Dockerfile` نسبةً إلى build context. `environment` يمرر إعدادات .NET عبر تحويل `__` إلى `:`. `expose` يوثق منافذ الخدمات داخل الشبكة؛ `ports` ينشرها على جهاز التطوير. مسارات bind mounts مثل `../prometheus/prometheus.yml` تُحل نسبةً إلى ملف Compose الأساسي. `healthcheck` يفحص SQL وRedis وPrometheus وصحة API.

يُستخدم ملف `.env` للاستبدال داخل YAML، ولا يعني وحده أن كل متغير سيصبح بيئة داخل الحاوية. `environment` هو الذي يمرر القيم للعملية. لا يوجد `env_file:` في هذا الإعداد حتى لا تُمرّر متغيرات المضيف كلها للتطبيق.

## تشغيل وتطوير في Visual Studio 2026

```powershell
Set-Location docker-compose
docker compose config --quiet
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

تم تسجيل مشروع Compose في الحل، ويربط مشروع API بملف `docker-compose.dcproj` عبر `DockerComposeProjectPath`. ملف `Properties/launchSettings.json` يبدأ API في وضع Debug وباقي الخدمات دون Debug، ويفتح `/health`. خاصية `DependencyAwareStart` تفعّل انتظار health checks في إصدارات Visual Studio الحديثة. اختر `docker-compose` كـStartup Project واضغط F5؛ يلزم تشغيل Docker Desktop وقبول التراخيص وتعبئة الأسرار أولًا.
