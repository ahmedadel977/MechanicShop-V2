# 9. مرجع أوامر Docker

نفّذ الأوامر من مجلد `docker-compose` أوّلًا (`Set-Location docker-compose`). يلزم ملء `.env` هناك وقبول التراخيص يدويًا.

## تحقق وتشغيل

```powershell
docker version
docker info
docker compose version
docker compose config --quiet
docker compose up -d --build
docker compose ps
docker compose logs -f api
docker compose logs --tail=100 sqlserver
docker compose exec api sh
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q "SELECT name FROM sys.databases"
```

`exec api sh` أداة تشخيصية فقط؛ صورة API النهائية صغيرة وقد لا تحتوي shell أو أدوات إضافية. افحص محتوى الصورة قبل الاعتماد على الأمر.

## بناء وإعادة تشغيل

```powershell
docker compose build api
docker compose up -d --no-deps --force-recreate api
docker compose restart api
docker compose pull
docker image ls
docker container ls -a
docker network ls
docker volume ls
```

بعد تحديث الصور، راجع التغييرات ثم أعد إنشاء الخدمات المتأثرة. لا تستخدم `latest` في نشر يمكن إعادة إنتاجه.

## إيقاف وتنظيف

```powershell
docker compose stop
docker compose down
docker compose down --volumes
docker image prune
docker system prune
```

`down --volumes` يحذف قاعدة البيانات وبيانات الخدمات. أوامر `prune` قد تحذف صورًا أو cache يحتاجها عمل آخر؛ راجع ما سيُحذف قبل الموافقة. تجنب `docker system prune --all --volumes` على جهاز يحتوي بيانات مهمة.
